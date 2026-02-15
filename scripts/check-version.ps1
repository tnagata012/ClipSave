#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Check current version information

.DESCRIPTION
    Displays version information from:
    - Installed MSIX package (if any)
    - Project files (Directory.Build.props, Package.appxmanifest)
    - Latest GitHub releases

.EXAMPLE
    .\check-version.ps1
#>

$ErrorActionPreference = "SilentlyContinue"

# Get project root
$projectRoot = Split-Path -Parent $PSScriptRoot

Push-Location $projectRoot

Write-Host "=== ClipSave Version Information ===" -ForegroundColor Cyan
Write-Host ""

# Section 1: Project Files
Write-Host "[Project Files]" -ForegroundColor Yellow

$propsPath = Join-Path $projectRoot "Directory.Build.props"
$coreVersion = $null

if (Test-Path $propsPath) {
    [xml]$props = Get-Content $propsPath
    $propsVersion = $props.Project.PropertyGroup.Version
    Write-Host "  Directory.Build.props : $propsVersion" -ForegroundColor White

    $versionMatch = [regex]::Match($propsVersion, '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$')
    if ($versionMatch.Success) {
        $coreVersion = "$($versionMatch.Groups['major'].Value).$($versionMatch.Groups['minor'].Value).$($versionMatch.Groups['patch'].Value)"
        Write-Host "  SemVer mode            : stable (X.Y.Z)" -ForegroundColor Green
    } else {
        Write-Host "  SemVer mode            : invalid format" -ForegroundColor Red
    }
} else {
    Write-Host "  Directory.Build.props : Not found" -ForegroundColor Red
}

$manifestPath = Join-Path $projectRoot "src/ClipSave.Package/Package.appxmanifest"
if (Test-Path $manifestPath) {
    [xml]$manifest = Get-Content $manifestPath
    $manifestVersion = $manifest.Package.Identity.Version
    Write-Host "  Package.appxmanifest  : $manifestVersion" -ForegroundColor White
} else {
    Write-Host "  Package.appxmanifest  : Not found" -ForegroundColor Red
}

# Version consistency check
if ($propsVersion -and $manifestVersion) {
    if ($coreVersion) {
        $expectedManifest = "$coreVersion.0"
    } else {
        $expectedManifest = "$propsVersion.0"
    }

    if ($manifestVersion -eq $expectedManifest) {
        Write-Host "  Status                : [OK] Consistent" -ForegroundColor Green
    } else {
        Write-Host "  Status                : [ERROR] Mismatch (expected $expectedManifest)" -ForegroundColor Red
    }
}

Write-Host ""

# Section 2: Installed Package
Write-Host "[Installed Package]" -ForegroundColor Yellow
$package = Get-AppxPackage -Name "*ClipSave*" 2>$null

if ($package) {
    Write-Host "  Name    : $($package.Name)" -ForegroundColor White
    Write-Host "  Version : $($package.Version)" -ForegroundColor White

    $versionParts = $package.Version.Split('.')
    $revision = [int]$versionParts[3]

    if ($revision -eq 0) {
        Write-Host "  Type    : Release Build" -ForegroundColor Green
    } else {
        Write-Host "  Type    : Dev Build (Build #$revision)" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Not installed" -ForegroundColor Gray
}

Write-Host ""

# Section 3: GitHub Releases
Write-Host "[GitHub Releases]" -ForegroundColor Yellow

# Check if gh is available
$ghAvailable = Get-Command gh -ErrorAction SilentlyContinue

if ($ghAvailable) {
    # Get latest stable release
    $latestRelease = gh release view --json tagName,publishedAt,isPrerelease 2>$null | ConvertFrom-Json
    if ($latestRelease -and -not $latestRelease.isPrerelease) {
        $publishedDate = [DateTime]::Parse($latestRelease.publishedAt).ToString("yyyy-MM-dd")
        Write-Host "  Latest Stable : $($latestRelease.tagName) ($publishedDate)" -ForegroundColor Green
    }

    # Get dev release
    $devRelease = gh release view dev-latest --json tagName,publishedAt 2>$null | ConvertFrom-Json
    if ($devRelease) {
        $publishedDate = [DateTime]::Parse($devRelease.publishedAt).ToString("yyyy-MM-dd HH:mm")
        Write-Host "  Dev (Latest)  : $($devRelease.tagName) ($publishedDate)" -ForegroundColor Yellow
    }

    if (-not $latestRelease -and -not $devRelease) {
        Write-Host "  No releases found" -ForegroundColor Gray
    }
} else {
    Write-Host "  GitHub CLI not available (install with: winget install GitHub.cli)" -ForegroundColor Gray
}

Write-Host ""

# Section 4: Current Branch
Write-Host "[Git Branch]" -ForegroundColor Yellow
$currentBranch = git branch --show-current 2>$null
if ($currentBranch) {
    Write-Host "  Current : $currentBranch" -ForegroundColor White

    if ($currentBranch -match '^release/') {
        Write-Host "  Type    : Release branch" -ForegroundColor Green
    } elseif ($currentBranch -eq 'main') {
        Write-Host "  Type    : Main branch (trunk)" -ForegroundColor Cyan
    } else {
        Write-Host "  Type    : Working branch" -ForegroundColor Gray
    }
} else {
    Write-Host "  Not in a git repository" -ForegroundColor Gray
}

Pop-Location
