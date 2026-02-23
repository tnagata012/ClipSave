#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Create or move a floating tag to a specific commit.

.DESCRIPTION
    Updates `refs/tags/<tag>` in GitHub using `gh api`.
    If the tag does not exist, it creates it.

.PARAMETER Repo
    Repository in owner/name format (example: tnagata012/ClipSave).

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

function Invoke-GhApi([string[]]$Args) {
    $stderrPath = [System.IO.Path]::GetTempFileName()
    try {
        $stdout = & gh @Args 2> $stderrPath
        $exitCode = $LASTEXITCODE
        $stdoutText = ($stdout | ForEach-Object { $_.ToString() }) -join "`n"
        $stderrText = if (Test-Path $stderrPath) {
            Get-Content -Path $stderrPath -Raw -ErrorAction SilentlyContinue
        } else {
            ""
        }

        return [pscustomobject]@{
            ExitCode = $exitCode
            StdOut = $stdoutText.TrimEnd("`r", "`n")
            StdErr = "$stderrText".TrimEnd("`r", "`n")
        }
    } finally {
        Remove-Item -Path $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Get-GhScalar([string[]]$Args, [string]$Description) {
    $result = Invoke-GhApi $Args
    if ($result.ExitCode -ne 0) {
        $detail = if (-not [string]::IsNullOrWhiteSpace($result.StdErr)) { $result.StdErr } else { $result.StdOut }
        return [pscustomobject]@{
            Success = $false
            Error = "Failed to $Description. $detail"
        }
    }

    $lines = @($result.StdOut -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($lines.Count -eq 0) {
        return [pscustomobject]@{
            Success = $false
            Error = "Failed to $Description. No output returned."
        }
    }

    return [pscustomobject]@{
        Success = $true
        Value = $lines[$lines.Count - 1].Trim()
    }
}

function Resolve-RefCommit([string]$Repo, [string]$Ref, [int]$MaxDepth = 5) {
    $refTypeResult = Get-GhScalar @("api", "repos/$Repo/git/ref/$Ref", "--jq", ".object.type") "read ref type for '$Ref'"
    if (-not $refTypeResult.Success) {
        return [pscustomobject]@{
            Success = $false
            Error = $refTypeResult.Error
        }
    }

    $refShaResult = Get-GhScalar @("api", "repos/$Repo/git/ref/$Ref", "--jq", ".object.sha") "read ref sha for '$Ref'"
    if (-not $refShaResult.Success) {
        return [pscustomobject]@{
            Success = $false
            Error = $refShaResult.Error
        }
    }

    $rawType = "$($refTypeResult.Value)".Trim().ToLowerInvariant()
    $rawSha = "$($refShaResult.Value)".Trim().ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($rawType) -or [string]::IsNullOrWhiteSpace($rawSha)) {
        return [pscustomobject]@{
            Success = $false
            Error = "Ref '$Ref' did not return a valid object."
        }
    }

    $resolvedType = $rawType
    $resolvedSha = $rawSha
    $depth = 0

    while ($resolvedType -eq "tag") {
        if ($depth -ge $MaxDepth) {
            return [pscustomobject]@{
                Success = $false
                Error = "Tag dereference exceeded max depth ($MaxDepth) for '$Ref'."
                RawType = $rawType
                RawSha = $rawSha
            }
        }

        $depth++
        $tagTypeResult = Get-GhScalar @("api", "repos/$Repo/git/tags/$resolvedSha", "--jq", ".object.type") "resolve tag object type '$resolvedSha'"
        if (-not $tagTypeResult.Success) {
            return [pscustomobject]@{
                Success = $false
                Error = $tagTypeResult.Error
                RawType = $rawType
                RawSha = $rawSha
            }
        }

        $tagShaResult = Get-GhScalar @("api", "repos/$Repo/git/tags/$resolvedSha", "--jq", ".object.sha") "resolve tag object sha '$resolvedSha'"
        if (-not $tagShaResult.Success) {
            return [pscustomobject]@{
                Success = $false
                Error = $tagShaResult.Error
                RawType = $rawType
                RawSha = $rawSha
            }
        }

        $resolvedType = "$($tagTypeResult.Value)".Trim().ToLowerInvariant()
        $resolvedSha = "$($tagShaResult.Value)".Trim().ToLowerInvariant()
        if ([string]::IsNullOrWhiteSpace($resolvedType) -or [string]::IsNullOrWhiteSpace($resolvedSha)) {
            return [pscustomobject]@{
                Success = $false
                Error = "Tag object '$rawSha' did not contain a valid target object."
                RawType = $rawType
                RawSha = $rawSha
            }
        }
    }

    return [pscustomobject]@{
        Success = $true
        RawType = $rawType
        RawSha = $rawSha
        ResolvedType = $resolvedType
        ResolvedSha = $resolvedSha
    }
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Fail "'gh' command not found. Install GitHub CLI first."
}

$targetSha = $Sha.Trim().ToLowerInvariant()
$ref = "tags/$Tag"
$updated = $false
$patchResult = $null
$createResult = $null

# Try update first. If tag does not exist, create it.
$patchResult = Invoke-GhApi @("api", "--method", "PATCH", "repos/$Repo/git/refs/$ref", "-f", "sha=$targetSha", "-F", "force=true")
if ($patchResult.ExitCode -eq 0) {
    $updated = $true
} else {
    $createResult = Invoke-GhApi @("api", "--method", "POST", "repos/$Repo/git/refs", "-f", "ref=refs/$ref", "-f", "sha=$targetSha")
    if ($createResult.ExitCode -eq 0) {
        $updated = $true
    }
}

if (-not $updated) {
    $patchError = if ($patchResult -and -not [string]::IsNullOrWhiteSpace($patchResult.StdErr)) {
        $patchResult.StdErr
    } elseif ($patchResult -and -not [string]::IsNullOrWhiteSpace($patchResult.StdOut)) {
        $patchResult.StdOut
    } else {
        "(no output)"
    }
    $createError = if ($createResult -and -not [string]::IsNullOrWhiteSpace($createResult.StdErr)) {
        $createResult.StdErr
    } elseif ($createResult -and -not [string]::IsNullOrWhiteSpace($createResult.StdOut)) {
        $createResult.StdOut
    } else {
        "(not attempted or no output)"
    }
    Fail "Failed to create or update tag '$Tag' in '$Repo'. PATCH: $patchError | POST: $createError"
}

$maxVerifyAttempts = 8
$verifyDelaySeconds = 2
$lastState = $null

for ($attempt = 1; $attempt -le $maxVerifyAttempts; $attempt++) {
    $state = Resolve-RefCommit -Repo $Repo -Ref $ref
    $lastState = $state

    if ($state.Success -and $state.ResolvedType -eq "commit" -and $state.ResolvedSha -eq $targetSha) {
        Write-Host "Floating tag updated: $Tag -> $($state.ResolvedSha) (raw: $($state.RawType):$($state.RawSha))"
        exit 0
    }

    if ($attempt -lt $maxVerifyAttempts) {
        Start-Sleep -Seconds $verifyDelaySeconds
    }
}

if (-not $lastState -or -not $lastState.Success) {
    $errorText = if ($lastState -and $lastState.Error) { $lastState.Error } else { "Unknown verification error." }
    Fail "Tag update verification failed. Expected: $targetSha. $errorText"
}

Fail "Tag update verification failed. Expected: $targetSha, Actual: $($lastState.ResolvedSha), ResolvedType: $($lastState.ResolvedType), RawRef: $($lastState.RawType):$($lastState.RawSha)"
