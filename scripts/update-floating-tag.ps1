#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Create or move a floating tag to a specific commit.

.DESCRIPTION
    Uses standard git commands to update a floating tag and verifies
    that the remote tag points to the expected commit.

.PARAMETER Repo
    Repository in owner/name format (example: tnagata012/ClipSave).
    Used as a safety check against the origin remote.

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

function Invoke-Git([string[]]$Args) {
    $stderrPath = [System.IO.Path]::GetTempFileName()
    try {
        $stdout = & git @Args 2> $stderrPath
        $exitCode = $LASTEXITCODE
        $stdoutText = ($stdout | ForEach-Object { $_.ToString() }) -join "`n"
        $stderrText = if (Test-Path $stderrPath) {
            Get-Content -Path $stderrPath -Raw -ErrorAction SilentlyContinue
        } else {
            ""
        }

        return [pscustomobject]@{
            ExitCode = $exitCode
            StdOut = "$stdoutText".TrimEnd("`r", "`n")
            StdErr = "$stderrText".TrimEnd("`r", "`n")
        }
    } finally {
        Remove-Item -Path $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Format-GitError([pscustomobject]$Result) {
    $parts = @()
    if ($Result -and -not [string]::IsNullOrWhiteSpace($Result.StdErr)) {
        $parts += "stderr: $($Result.StdErr)"
    }
    if ($Result -and -not [string]::IsNullOrWhiteSpace($Result.StdOut)) {
        $parts += "stdout: $($Result.StdOut)"
    }
    if ($parts.Count -eq 0) {
        return "(no output)"
    }
    return ($parts -join " | ")
}

function Parse-GitHubRepoFromRemoteUrl([string]$RemoteUrl) {
    if ([string]::IsNullOrWhiteSpace($RemoteUrl)) {
        return $null
    }

    $url = $RemoteUrl.Trim()

    $httpsMatch = [regex]::Match($url, '^https://github\.com/(?<repo>[^/\s]+/[^/\s]+?)(?:\.git)?/?$')
    if ($httpsMatch.Success) {
        return $httpsMatch.Groups['repo'].Value
    }

    $sshMatch = [regex]::Match($url, '^git@github\.com:(?<repo>[^/\s]+/[^/\s]+?)(?:\.git)?$')
    if ($sshMatch.Success) {
        return $sshMatch.Groups['repo'].Value
    }

    return $null
}

function Resolve-RemoteTagCommit([string]$TagName) {
    $lsResult = Invoke-Git @("ls-remote", "--tags", "origin", "refs/tags/$TagName", "refs/tags/$TagName^{}")
    if ($lsResult.ExitCode -ne 0) {
        return [pscustomobject]@{
            Success = $false
            Error = "Failed to query remote tag refs. $(Format-GitError $lsResult)"
        }
    }

    $lines = @($lsResult.StdOut -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($lines.Count -eq 0) {
        return [pscustomobject]@{
            Success = $false
            Error = "Remote tag '$TagName' was not found."
            Raw = ""
        }
    }

    $rawSha = $null
    $peeledSha = $null
    foreach ($line in $lines) {
        $match = [regex]::Match($line.Trim(), '^(?<sha>[0-9a-fA-F]{40})\s+refs/tags/(?<name>.+?)(?<peeled>\^\{\})?$')
        if (-not $match.Success) {
            continue
        }
        $sha = $match.Groups['sha'].Value.ToLowerInvariant()
        $isPeeled = $match.Groups['peeled'].Success
        if ($isPeeled) {
            $peeledSha = $sha
        } else {
            $rawSha = $sha
        }
    }

    $resolvedSha = if (-not [string]::IsNullOrWhiteSpace($peeledSha)) { $peeledSha } else { $rawSha }
    if ([string]::IsNullOrWhiteSpace($resolvedSha)) {
        return [pscustomobject]@{
            Success = $false
            Error = "Could not parse remote refs for '$TagName'."
            Raw = ($lines -join " | ")
        }
    }

    return [pscustomobject]@{
        Success = $true
        RawSha = $rawSha
        ResolvedSha = $resolvedSha
        Raw = ($lines -join " | ")
    }
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Fail "'git' command not found."
}

$targetSha = $Sha.Trim().ToLowerInvariant()
$tagName = $Tag.Trim()

if ([string]::IsNullOrWhiteSpace($tagName)) {
    Fail "Tag name is empty."
}

$originUrlResult = Invoke-Git @("remote", "get-url", "origin")
if ($originUrlResult.ExitCode -ne 0) {
    Fail "Failed to resolve origin remote URL. $(Format-GitError $originUrlResult)"
}

$originUrl = $originUrlResult.StdOut.Trim()
$originRepo = Parse-GitHubRepoFromRemoteUrl $originUrl
if (-not [string]::IsNullOrWhiteSpace($Repo) -and -not [string]::IsNullOrWhiteSpace($originRepo)) {
    if ($Repo.Trim() -ne $originRepo) {
        Fail "Repo mismatch. Expected '$Repo', but origin is '$originRepo' ($originUrl)."
    }
}

$commitCheck = Invoke-Git @("cat-file", "-t", $targetSha)
if ($commitCheck.ExitCode -ne 0 -or $commitCheck.StdOut.Trim().ToLowerInvariant() -ne "commit") {
    Fail "Target SHA is not a valid commit in the current repository: $targetSha"
}

# Create/update a lightweight local tag at the target commit.
$tagResult = Invoke-Git @("tag", "-f", $tagName, $targetSha)
if ($tagResult.ExitCode -ne 0) {
    Fail "Failed to create/update local tag '$tagName'. $(Format-GitError $tagResult)"
}

# Push floating tag ref explicitly and force-update remote.
$pushResult = Invoke-Git @("push", "origin", "refs/tags/$tagName", "--force")
if ($pushResult.ExitCode -ne 0) {
    Fail "Failed to push tag '$tagName' to origin. $(Format-GitError $pushResult)"
}

$maxVerifyAttempts = 8
$verifyDelaySeconds = 2
$lastState = $null

for ($attempt = 1; $attempt -le $maxVerifyAttempts; $attempt++) {
    $state = Resolve-RemoteTagCommit -TagName $tagName
    $lastState = $state

    if ($state.Success -and $state.ResolvedSha -eq $targetSha) {
        $raw = if ($state.Raw) { $state.Raw } else { "(no raw refs)" }
        Write-Host "Floating tag updated: $tagName -> $($state.ResolvedSha) (remote refs: $raw)"
        exit 0
    }

    if ($attempt -lt $maxVerifyAttempts) {
        Start-Sleep -Seconds $verifyDelaySeconds
    }
}

if (-not $lastState -or -not $lastState.Success) {
    $detail = if ($lastState -and $lastState.Error) { $lastState.Error } else { "Unknown verification error." }
    Fail "Tag update verification failed. Expected: $targetSha. $detail"
}

Fail "Tag update verification failed. Expected: $targetSha, Actual: $($lastState.ResolvedSha), RawRefs: $($lastState.Raw)"
