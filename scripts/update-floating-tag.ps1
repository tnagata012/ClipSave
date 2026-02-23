#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Create or move a floating tag to a specific commit.

.DESCRIPTION
    Force-pushes a floating tag to a commit SHA and verifies
    the remote ref.

.PARAMETER Repo
    Repository in owner/name format (example: tnagata012/ClipSave).
    Used as a safety check against the origin remote URL.

.PARAMETER Tag
    Tag name to move (example: dev-latest, release-1.3-latest).

.PARAMETER Sha
    Target full commit SHA (40 hex characters).
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Repo,

    [Parameter(Mandatory = $true)]
    [string]$Tag,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$Sha
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Host "`n[ERROR] $Message" -ForegroundColor Red
    exit 1
}

function Invoke-Git([string[]]$ArgList) {
    $output = & git @ArgList 2>&1
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = (($output | ForEach-Object { $_.ToString() }) -join "`n").TrimEnd("`r", "`n")
    }
}

function Get-Detail([pscustomobject]$Result) {
    if ($Result -and -not [string]::IsNullOrWhiteSpace($Result.Output)) {
        return $Result.Output
    }
    return "(no output)"
}

function Normalize-RemoteUrl([string]$RemoteUrl) {
    $url = $RemoteUrl.Trim().TrimEnd("/")
    if ($url.EndsWith(".git", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $url.Substring(0, $url.Length - 4)
    }
    return $url
}

function Get-RemoteTagSha([string]$TagName) {
    $result = Invoke-Git @("ls-remote", "--tags", "origin", "refs/tags/$TagName")
    if ($result.ExitCode -ne 0) {
        return [pscustomobject]@{
            Success = $false
            Error = "Failed to query remote tag refs. $(Get-Detail $result)"
        }
    }

    $line = $null
    foreach ($candidate in ($result.Output -split "`r?`n")) {
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            $line = $candidate.Trim()
            break
        }
    }

    if ([string]::IsNullOrWhiteSpace($line)) {
        return [pscustomobject]@{
            Success = $false
            Error = "Remote tag '$TagName' was not found."
        }
    }

    $parts = @($line -split '\s+', 2)
    if ($parts.Count -lt 2 -or $parts[0] -notmatch '^[0-9a-fA-F]{40}$') {
        return [pscustomobject]@{
            Success = $false
            Error = "Could not parse remote refs for '$TagName'. Raw: $line"
        }
    }

    return [pscustomobject]@{
        Success = $true
        Sha = $parts[0].ToLowerInvariant()
        Raw = $line
    }
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Fail "'git' command not found."
}

$expectedRepo = $Repo.Trim()
if ([string]::IsNullOrWhiteSpace($expectedRepo)) {
    Fail "Repo is empty."
}

$targetSha = $Sha.Trim().ToLowerInvariant()
$tagName = $Tag.Trim()
$tagRef = "refs/tags/$tagName"

if ([string]::IsNullOrWhiteSpace($tagName)) {
    Fail "Tag name is empty."
}

$tagFormatCheck = Invoke-Git @("check-ref-format", $tagRef)
if ($tagFormatCheck.ExitCode -ne 0) {
    Fail "Invalid tag name '$tagName'. $(Get-Detail $tagFormatCheck)"
}

$originResult = Invoke-Git @("remote", "get-url", "origin")
if ($originResult.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($originResult.Output)) {
    Fail "Failed to resolve origin remote URL. $(Get-Detail $originResult)"
}

$originUrl = $originResult.Output.Trim()
$normalizedOrigin = (Normalize-RemoteUrl $originUrl).ToLowerInvariant()
$normalizedRepo = $expectedRepo.ToLowerInvariant()

if (-not $normalizedOrigin.EndsWith("/$normalizedRepo") -and -not $normalizedOrigin.EndsWith(":$normalizedRepo")) {
    Fail "Repo mismatch. Expected '$expectedRepo', but origin is '$originUrl'."
}

$commitCheck = Invoke-Git @("rev-parse", "--verify", "$targetSha^{commit}")
if ($commitCheck.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($commitCheck.Output)) {
    Fail "Target SHA is not a valid commit in the current repository: $targetSha"
}

# Push SHA directly to remote tag ref to avoid mutating local tags.
$pushSpec = "${targetSha}:$tagRef"
$pushResult = Invoke-Git @("push", "origin", $pushSpec, "--force")
if ($pushResult.ExitCode -ne 0) {
    Fail "Failed to push tag '$tagName' to origin. $(Get-Detail $pushResult)"
}

$maxVerifyAttempts = 8
$verifyDelaySeconds = 2
$lastError = "Unknown verification error."
$lastRaw = ""

for ($attempt = 1; $attempt -le $maxVerifyAttempts; $attempt++) {
    $state = Get-RemoteTagSha -TagName $tagName
    if ($state.Success) {
        if ($state.Sha -eq $targetSha) {
            Write-Host "Floating tag updated: $tagName -> $($state.Sha) (remote refs: $($state.Raw))"
            exit 0
        }
        $lastError = "Expected: $targetSha, Actual: $($state.Sha)"
        $lastRaw = $state.Raw
    } else {
        $lastError = $state.Error
        $lastRaw = ""
    }

    if ($attempt -lt $maxVerifyAttempts) {
        Start-Sleep -Seconds $verifyDelaySeconds
    }
}

if ([string]::IsNullOrWhiteSpace($lastRaw)) {
    Fail "Tag update verification failed. $lastError"
}

Fail "Tag update verification failed. $lastError, RawRefs: $lastRaw"
