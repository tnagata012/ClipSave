#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Assert that a GitHub Release has the finalized archive assets.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Repository,
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\release-support.ps1"

try {
    Assert-FinalizedReleaseArchive `
        -Repository $Repository `
        -Version $Version | Out-Null
}
catch {
    Write-Host "`n[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
