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

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Fail "'gh' command not found. Install GitHub CLI first."
}

$targetSha = $Sha.Trim().ToLowerInvariant()
$ref = "tags/$Tag"
$updated = $false

# Try update first. If tag does not exist, create it.
gh api --method PATCH "repos/$Repo/git/refs/$ref" -f sha="$targetSha" -F force=true *> $null
if ($LASTEXITCODE -eq 0) {
    $updated = $true
} else {
    gh api --method POST "repos/$Repo/git/refs" -f ref="refs/$ref" -f sha="$targetSha" *> $null
    if ($LASTEXITCODE -eq 0) {
        $updated = $true
    }
}

if (-not $updated) {
    Fail "Failed to create or update tag '$Tag' in '$Repo'."
}

$resolvedSha = gh api "repos/$Repo/git/ref/$ref" --jq '.object.sha' 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($resolvedSha)) {
    Fail "Failed to resolve updated tag reference: $Tag"
}

$resolvedSha = $resolvedSha.Trim().ToLowerInvariant()
if ($resolvedSha -ne $targetSha) {
    Fail "Tag update verification failed. Expected: $targetSha, Actual: $resolvedSha"
}

Write-Host "Floating tag updated: $Tag -> $resolvedSha"
