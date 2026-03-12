#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Delete stale managed GitHub Release assets while keeping the current bundle/checksum set.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Repository,
    [Parameter(Mandatory = $true)]
    [string]$Tag,
    [Parameter(Mandatory = $true)]
    [string[]]$RetainAssetNames,
    [switch]$IgnoreDeleteFailures = $false
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Host "`n[ERROR] $Message" -ForegroundColor Red
    exit 1
}

if ([string]::IsNullOrWhiteSpace($Repository) -or $Repository -notmatch '^[^/\s]+/[^/\s]+$') {
    Fail "Repository must be in 'owner/name' format. Actual: '$Repository'"
}

if ([string]::IsNullOrWhiteSpace($Tag)) {
    Fail "Tag is empty."
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Fail "GitHub CLI 'gh' is required."
}

$retain = @(
    $RetainAssetNames |
        Where-Object { $_ -and $_.Trim() -ne "" } |
        ForEach-Object { $_.Trim() }
)
if ($retain.Count -eq 0) {
    Fail "RetainAssetNames must contain at least one asset name."
}

$assetNames = gh release view $Tag --repo $Repository --json assets --jq '.assets[].name' 2>$null
if ($LASTEXITCODE -ne 0) {
    if ($IgnoreDeleteFailures) {
        Write-Warning "Failed to query release assets for '$Tag' in '$Repository'. Skipping stale asset cleanup."
        exit 0
    }

    Fail "Failed to query release assets for '$Tag' in '$Repository'."
}

$managedAssets = @(
    $assetNames |
        Where-Object { $_ -and $_.Trim() -ne "" } |
        Where-Object { $_ -eq "SHA256SUMS.txt" -or $_ -match '\.msixbundle$' }
)

$staleAssets = @($managedAssets | Where-Object { $retain -notcontains $_ })
if ($staleAssets.Count -eq 0) {
    Write-Host "No stale managed assets to cleanup in '$Tag'."
    exit 0
}

$deleteFailures = @()
foreach ($asset in $staleAssets) {
    gh release delete-asset $Tag $asset --repo $Repository --yes *> $null
    if ($LASTEXITCODE -ne 0) {
        $deleteFailures += $asset
        if ($IgnoreDeleteFailures) {
            Write-Warning "Failed to delete stale managed asset '$asset' from release '$Tag'."
            continue
        }

        Fail "Failed to delete stale managed asset '$asset' from release '$Tag'."
    }

    Write-Host "Deleted stale managed asset: $asset"
}

if ($deleteFailures.Count -gt 0 -and $IgnoreDeleteFailures) {
    Write-Warning "Some stale managed assets could not be deleted from '$Tag': $($deleteFailures -join ', ')"
}
