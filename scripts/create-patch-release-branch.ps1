#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Create a patch release initialization branch from release/X.Y.

.DESCRIPTION
    Creates a patch-start branch from a release branch and bumps version once.
    - release/X.Y current version: X.Y.Z
    - patch init branch: chore/release-X.Y.(Z+1)-init
    - updates Directory.Build.props and Package.appxmanifest to X.Y.(Z+1)
    - intended for PR: patch init branch -> release/X.Y

.PARAMETER ReleaseBranch
    Target release branch (e.g., release/1.3).
    If omitted, uses current branch when it matches release/X.Y.

.PARAMETER SkipPull
    Skip pulling latest changes from origin before branching.

.PARAMETER Push
    Push patch init branch to origin (default: false).

.EXAMPLE
    .\create-patch-release-branch.ps1 -ReleaseBranch release/1.3

.EXAMPLE
    .\create-patch-release-branch.ps1 -ReleaseBranch release/1.3 -Push
#>

param(
    [string]$ReleaseBranch = $null,
    [switch]$SkipPull = $false,
    [switch]$Push = $false
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$releasePattern = '^release/(?<major>\d+)\.(?<minor>\d+)$'
$semverPattern = '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$'
$propsPath = Join-Path $projectRoot "Directory.Build.props"
$manifestPath = Join-Path $projectRoot "src/ClipSave.Package/Package.appxmanifest"

function Fail([string]$Message) {
    Write-Host "`n[ERROR] $Message" -ForegroundColor Red
    exit 1
}

Write-Host "=== Create Patch Release Branch ===" -ForegroundColor Cyan

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

    if (-not $ReleaseBranch) {
        $currentBranch = git branch --show-current
        if ($currentBranch -match $releasePattern) {
            $ReleaseBranch = $currentBranch
        } else {
            Fail "ReleaseBranch is required when current branch is not release/X.Y."
        }
    }

    if ($ReleaseBranch -notmatch $releasePattern) {
        Fail "Invalid ReleaseBranch format: $ReleaseBranch (expected release/X.Y)."
    }

    $releaseMajor = [int]$matches['major']
    $releaseMinor = [int]$matches['minor']

    git remote get-url origin *> $null
    $hasOrigin = $LASTEXITCODE -eq 0
    if ($Push -and -not $hasOrigin) {
        Fail "Cannot push because remote 'origin' is not configured."
    }

    Write-Host "Release branch: $ReleaseBranch" -ForegroundColor White

    Write-Host "[1/8] Switching to $ReleaseBranch..." -ForegroundColor Yellow
    git checkout $ReleaseBranch
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to checkout $ReleaseBranch."
    }

    if ($SkipPull) {
        Write-Host "[2/8] Skipping pull (use -SkipPull:`$false to enable)." -ForegroundColor Gray
    } elseif ($hasOrigin) {
        Write-Host "[2/8] Pulling latest $ReleaseBranch..." -ForegroundColor Yellow
        git pull origin $ReleaseBranch
        if ($LASTEXITCODE -ne 0) {
            Fail "Failed to pull from origin/$ReleaseBranch."
        }
    } else {
        Write-Host "[2/8] Remote 'origin' not found. Skipping pull." -ForegroundColor Yellow
    }

    Write-Host "[3/8] Validating release branch version policy..." -ForegroundColor Yellow
    & "$projectRoot\scripts\assert-version-policy.ps1" -ProjectRoot $projectRoot -BranchName $ReleaseBranch
    if ($LASTEXITCODE -ne 0) {
        Fail "Version validation failed on $ReleaseBranch."
    }

    if (-not (Test-Path $propsPath)) {
        Fail "Directory.Build.props not found: $propsPath"
    }
    if (-not (Test-Path $manifestPath)) {
        Fail "Package.appxmanifest not found: $manifestPath"
    }

    [xml]$props = Get-Content $propsPath
    $currentVersion = $props.Project.PropertyGroup.Version
    if (-not $currentVersion -or $currentVersion -notmatch $semverPattern) {
        Fail "Invalid version format in Directory.Build.props: $currentVersion"
    }

    $currentMajor = [int]$matches['major']
    $currentMinor = [int]$matches['minor']
    $currentPatch = [int]$matches['patch']

    if ($currentMajor -ne $releaseMajor -or $currentMinor -ne $releaseMinor) {
        Fail "Release branch and version mismatch. Branch=$ReleaseBranch, File=$currentVersion"
    }

    if ($currentPatch -ge 65535) {
        Fail "Cannot increment patch. Current patch is $currentPatch (max supported 65535)."
    }

    $nextPatch = $currentPatch + 1
    $nextVersion = "$currentMajor.$currentMinor.$nextPatch"
    $patchInitBranch = "chore/release-$nextVersion-init"

    Write-Host "Current version: $currentVersion" -ForegroundColor White
    Write-Host "Next version   : $nextVersion" -ForegroundColor White
    Write-Host "Patch branch   : $patchInitBranch" -ForegroundColor White

    Write-Host "[4/8] Checking branch existence..." -ForegroundColor Yellow
    git show-ref --verify --quiet "refs/heads/$patchInitBranch"
    if ($LASTEXITCODE -eq 0) {
        Fail "Local branch '$patchInitBranch' already exists."
    }
    if ($hasOrigin) {
        git ls-remote --exit-code --heads origin $patchInitBranch *> $null
        if ($LASTEXITCODE -eq 0) {
            Fail "Remote branch '$patchInitBranch' already exists on origin."
        }
    }

    Write-Host "[5/8] Creating patch init branch..." -ForegroundColor Yellow
    git checkout -b $patchInitBranch
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to create '$patchInitBranch'."
    }

    Write-Host "[6/8] Updating version files to $nextVersion..." -ForegroundColor Yellow
    [xml]$props = Get-Content $propsPath
    $props.Project.PropertyGroup.Version = $nextVersion
    $props.Save($propsPath)
    Write-Host "  [OK] Directory.Build.props = $nextVersion" -ForegroundColor Green

    [xml]$manifest = Get-Content $manifestPath
    $manifest.Package.Identity.Version = "$nextVersion.0"
    $manifest.Save($manifestPath)
    Write-Host "  [OK] Package.appxmanifest = $nextVersion.0" -ForegroundColor Green

    git add Directory.Build.props src/ClipSave.Package/Package.appxmanifest
    git diff --staged --quiet
    if ($LASTEXITCODE -eq 0) {
        Fail "No staged changes detected for patch init branch."
    }

    git commit -m "chore: start $nextVersion patch release"
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to commit version update on $patchInitBranch."
    }

    & "$projectRoot\scripts\assert-version-policy.ps1" -ProjectRoot $projectRoot -BranchName $ReleaseBranch
    if ($LASTEXITCODE -ne 0) {
        Fail "Version validation failed after update."
    }

    if ($Push) {
        Write-Host "[7/8] Pushing $patchInitBranch..." -ForegroundColor Yellow
        git push -u origin $patchInitBranch
        if ($LASTEXITCODE -ne 0) {
            Fail "Failed to push $patchInitBranch."
        }
        Write-Host "  [OK] Pushed $patchInitBranch" -ForegroundColor Green
    } else {
        Write-Host "[7/8] Skipping push (use -Push to push automatically)" -ForegroundColor Gray
    }

    Write-Host "[8/8] Completed patch init branch creation." -ForegroundColor Green
    Write-Host ""
    Write-Host "Summary:" -ForegroundColor Cyan
    Write-Host "  Base branch : $ReleaseBranch" -ForegroundColor White
    Write-Host "  New version : $nextVersion" -ForegroundColor White
    Write-Host "  Patch branch: $patchInitBranch" -ForegroundColor White
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    if (-not $Push) {
        Write-Host "1. Push branch: git push -u origin $patchInitBranch"
        Write-Host "2. Create PR:   $patchInitBranch -> $ReleaseBranch"
    } else {
        Write-Host "1. Create PR: $patchInitBranch -> $ReleaseBranch"
    }
}
finally {
    Pop-Location
}

