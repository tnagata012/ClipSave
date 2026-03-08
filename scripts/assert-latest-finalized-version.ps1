#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Assert that a target version is the latest finalized GitHub Release version.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Repository,
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$ReleaseBranch = $null,
    [switch]$AllowMissingFinalizedRelease = $false
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\release-support.ps1"

try {
    Assert-LatestFinalizedVersion `
        -Repository $Repository `
        -Version $Version `
        -ReleaseBranch $ReleaseBranch `
        -AllowMissingFinalizedRelease:$AllowMissingFinalizedRelease | Out-Null
}
catch {
    Write-Host "`n[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
