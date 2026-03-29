#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Create a new release branch from main

.DESCRIPTION
    Creates `release/X.Y` from `main`, sets repository version files to `X.Y.0`,
    and leaves `main` unchanged at `0.0.1`.

.PARAMETER Version
    Target release series (e.g., 1.3)

.PARAMETER MainBranch
    Trunk branch name (default: main)

.PARAMETER SkipPull
    Skip pulling latest changes from origin before branching

.PARAMETER Push
    Push release branch to origin (default: false)

.EXAMPLE
    .\create-release-branch.ps1 -Version 1.3

.EXAMPLE
    .\create-release-branch.ps1 -Version 1.3 -Push
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$MainBranch = "main",
    [switch]$SkipPull = $false,
    [switch]$Push = $false
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot

function Fail([string]$Message) {
    Write-Host "`n[ERROR] $Message" -ForegroundColor Red
    exit 1
}

. "$projectRoot\scripts\release-series-policy.ps1"
. "$projectRoot\scripts\version-file-support.ps1"

$Version = $Version.Trim()
$MainBranch = $MainBranch.Trim()

Write-Host "=== Create Release Branch ===" -ForegroundColor Cyan

Push-Location $projectRoot
try {
    git rev-parse --is-inside-work-tree *> $null
    if ($LASTEXITCODE -ne 0) {
        Fail "Not inside a git repository: $projectRoot"
    }

    $status = git status --porcelain
    if ($status) {
        Fail "Working directory has uncommitted changes. Commit or stash them first."
    }

    git remote get-url origin *> $null
    $hasOrigin = $LASTEXITCODE -eq 0
    if ($Push -and -not $hasOrigin) {
        Fail "Cannot push because remote 'origin' is not configured."
    }

    Write-Host "[1/6] Switching to $MainBranch..." -ForegroundColor Yellow
    git checkout $MainBranch
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to checkout $MainBranch."
    }

    if ($SkipPull) {
        Write-Host "[2/6] Skipping pull." -ForegroundColor Gray
    } elseif ($hasOrigin) {
        Write-Host "[2/6] Pulling latest $MainBranch..." -ForegroundColor Yellow
        git pull origin $MainBranch
        if ($LASTEXITCODE -ne 0) {
            Fail "Failed to pull from origin/$MainBranch."
        }
    } else {
        Write-Host "[2/6] Remote 'origin' not found. Skipping pull." -ForegroundColor Yellow
    }

    Write-Host "[3/6] Validating main branch version policy..." -ForegroundColor Yellow
    & "$projectRoot\scripts\assert-version-policy.ps1" -ProjectRoot $projectRoot -BranchName $MainBranch
    if ($LASTEXITCODE -ne 0) {
        Fail "Version validation failed on $MainBranch."
    }

    [xml]$mainProps = Get-Content (Join-Path $projectRoot "Directory.Build.props")
    $mainVersion = [string]$mainProps.Project.PropertyGroup.Version
    $resolution = Resolve-PrepareReleaseSeries -MainVersion $mainVersion -RequestedSeries $Version
    $releaseInfo = Get-ReleaseSeriesInfo -Series $resolution.ResolvedReleaseSeries
    $releaseVersion = $releaseInfo.Version
    $branchName = "release/$($releaseInfo.Series)"

    Write-Host "Current main version: $($resolution.CurrentMainVersion)" -ForegroundColor White
    Write-Host "Release branch: $branchName" -ForegroundColor White
    Write-Host "Release version: $releaseVersion" -ForegroundColor White

    Write-Host "[4/6] Checking branch existence..." -ForegroundColor Yellow
    git show-ref --verify --quiet "refs/heads/$branchName"
    if ($LASTEXITCODE -eq 0) {
        Fail "Local branch '$branchName' already exists."
    }

    if ($hasOrigin) {
        git ls-remote --exit-code --heads origin $branchName *> $null
        if ($LASTEXITCODE -eq 0) {
            Fail "Remote branch '$branchName' already exists on origin."
        }
    }

    Write-Host "[5/6] Creating release branch..." -ForegroundColor Yellow
    git checkout -b $branchName
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to create '$branchName'."
    }

    $releaseFiles = Set-RepositoryVersionFiles -ProjectRoot $projectRoot -Version $releaseVersion
    Write-Host "  [OK] Directory.Build.props = $($releaseFiles.Version)" -ForegroundColor Green
    Write-Host "  [OK] Package.appxmanifest = $($releaseFiles.ManifestVersion)" -ForegroundColor Green

    git add Directory.Build.props src/ClipSave.Package/Package.appxmanifest
    git diff --staged --quiet
    if ($LASTEXITCODE -ne 0) {
        git commit -m "chore: set release version to $releaseVersion"
        if ($LASTEXITCODE -ne 0) {
            Fail "Failed to commit version update on release branch."
        }
    } else {
        Write-Host "  [INFO] No version changes to commit on release branch." -ForegroundColor Gray
    }

    & "$projectRoot\scripts\assert-version-policy.ps1" -ProjectRoot $projectRoot -BranchName $branchName
    if ($LASTEXITCODE -ne 0) {
        Fail "Version validation failed on $branchName."
    }

    if ($Push) {
        Write-Host "[6/6] Pushing release branch..." -ForegroundColor Yellow
        git push -u origin $branchName
        if ($LASTEXITCODE -ne 0) {
            Fail "Failed to push '$branchName'."
        }
    } else {
        Write-Host "[6/6] Skipping push." -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "[OK] Release branch workflow completed." -ForegroundColor Green
    Write-Host "Summary:" -ForegroundColor Cyan
    Write-Host "  Release: $branchName -> $releaseVersion" -ForegroundColor White
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Create or update issue 'Release Notes: $($releaseInfo.Series)'."
    Write-Host "2. Move the shipped bullets from 'Release Notes: Unreleased'."
    Write-Host "3. Keep stabilizing changes on $branchName via PRs."
    Write-Host "4. RC Build triggers on push to release/*."
}
finally {
    Pop-Location
}
