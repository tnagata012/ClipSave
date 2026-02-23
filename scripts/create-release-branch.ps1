#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Create a new release branch (Trunk-Based Development)

.DESCRIPTION
    Creates a release branch from main and prepares a PR branch for main version bump.
    - release/X.Y: X.Y.0 (stable)
    - chore/* branch from main: X.(Y+1).0 (next development line, PR required)
    - Package.appxmanifest always keeps numeric X.Y.Z.0

.PARAMETER Version
    Target release version (e.g., 1.3.0)

.PARAMETER MainBranch
    Trunk branch name (default: main)

.PARAMETER SkipPull
    Skip pulling latest changes from origin before branching

.PARAMETER Push
    Push branches to origin (default: false)

.EXAMPLE
    .\create-release-branch.ps1 -Version 1.3.0
    # Creates release/1.3 at 1.3.0 and creates a PR branch for main=1.4.0

.EXAMPLE
    .\create-release-branch.ps1 -Version 1.3.0 -Push
    # Same as above, then pushes release + PR branch
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [string]$MainBranch = "main",
    [switch]$SkipPull = $false,
    [switch]$Push = $false
)

$ErrorActionPreference = "Stop"

# Get project root
$projectRoot = Split-Path -Parent $PSScriptRoot

function Fail([string]$Message) {
    Write-Host "`n[ERROR] $Message" -ForegroundColor Red
    exit 1
}

# Validate target release version format (X.Y.Z)
if ($Version -notmatch '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$') {
    Fail "Invalid version format. Use X.Y.Z (example: 1.3.0)."
}

$major = [int]$matches['major']
$minor = [int]$matches['minor']
$patch = [int]$matches['patch']

if ($patch -ne 0) {
    Fail "Release branch creation only supports .0 versions (example: 1.3.0). Use release branch updates for patch releases."
}

$branchName = "release/$major.$minor"
$nextMinor = $minor + 1
$nextMainVersion = "$major.$nextMinor.0"
$mainBumpBranch = "chore/bump-$MainBranch-to-$nextMainVersion"

$propsPath = Join-Path $projectRoot "Directory.Build.props"
$manifestPath = Join-Path $projectRoot "src/ClipSave.Package/Package.appxmanifest"

Write-Host "=== Create Release Branch (Trunk-Based Development) ===" -ForegroundColor Cyan
Write-Host "Release branch: $branchName (version $Version)" -ForegroundColor White
Write-Host "Main branch   : $MainBranch (target version $nextMainVersion via PR branch)" -ForegroundColor White
Write-Host "PR branch     : $mainBumpBranch" -ForegroundColor White
Write-Host ""

Push-Location $projectRoot
try {
    # Ensure we are in a git repository
    git rev-parse --is-inside-work-tree *> $null
    if ($LASTEXITCODE -ne 0) {
        Fail "Not inside a git repository: $projectRoot"
    }

    # Check for uncommitted changes
    $status = git status --porcelain
    if ($status) {
        Fail "Working directory has uncommitted changes. Commit or stash them first."
    }

    # Check origin availability once
    git remote get-url origin *> $null
    $hasOrigin = $LASTEXITCODE -eq 0
    if ($Push -and -not $hasOrigin) {
        Fail "Cannot push because remote 'origin' is not configured."
    }

    # 1. Switch to main branch
    Write-Host "[1/9] Switching to $MainBranch..." -ForegroundColor Yellow
    git checkout $MainBranch
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to checkout $MainBranch."
    }

    # 2. Pull latest branch unless skipped
    if ($SkipPull) {
        Write-Host "[2/9] Skipping pull (use -SkipPull:$false to enable)." -ForegroundColor Gray
    } elseif ($hasOrigin) {
        Write-Host "[2/9] Pulling latest $MainBranch..." -ForegroundColor Yellow
        git pull origin $MainBranch
        if ($LASTEXITCODE -ne 0) {
            Fail "Failed to pull from origin/$MainBranch."
        }
    } else {
        Write-Host "[2/9] Remote 'origin' not found. Skipping pull." -ForegroundColor Yellow
    }

    # 3. Validate current main branch version policy
    Write-Host "[3/9] Validating $MainBranch version policy..." -ForegroundColor Yellow
    & "$projectRoot\scripts\assert-version-policy.ps1" -ProjectRoot $projectRoot -BranchName $MainBranch
    if ($LASTEXITCODE -ne 0) {
        Fail "Version validation failed on $MainBranch."
    }

    # Guard against releasing the wrong major/minor line.
    [xml]$mainProps = Get-Content $propsPath
    $mainVersion = $mainProps.Project.PropertyGroup.Version
    if (-not $mainVersion -or $mainVersion -notmatch '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$') {
        Fail "Current $MainBranch version format is invalid: $mainVersion"
    }

    $mainMajor = [int]$matches['major']
    $mainMinor = [int]$matches['minor']
    if ($mainMajor -ne $major -or $mainMinor -ne $minor) {
        Fail "Target release version $Version does not match current $MainBranch line $mainVersion. Use a matching X.Y.0 version."
    }

    # 4. Check if target branches already exist (local or remote)
    Write-Host "[4/9] Checking branch existence..." -ForegroundColor Yellow
    git show-ref --verify --quiet "refs/heads/$branchName"
    if ($LASTEXITCODE -eq 0) {
        Fail "Local branch '$branchName' already exists."
    }
    git show-ref --verify --quiet "refs/heads/$mainBumpBranch"
    if ($LASTEXITCODE -eq 0) {
        Fail "Local branch '$mainBumpBranch' already exists."
    }

    if ($hasOrigin) {
        git ls-remote --exit-code --heads origin $branchName *> $null
        if ($LASTEXITCODE -eq 0) {
            Fail "Remote branch '$branchName' already exists on origin."
        }
        git ls-remote --exit-code --heads origin $mainBumpBranch *> $null
        if ($LASTEXITCODE -eq 0) {
            Fail "Remote branch '$mainBumpBranch' already exists on origin."
        }
    }

    # 5. Create release branch
    Write-Host "[5/9] Creating release branch..." -ForegroundColor Yellow
    git checkout -b $branchName
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to create '$branchName'."
    }

    # 6. Update and commit release branch versions
    Write-Host "[6/9] Updating release branch version to $Version..." -ForegroundColor Yellow
    [xml]$props = Get-Content $propsPath
    $props.Project.PropertyGroup.Version = $Version
    $props.Save($propsPath)
    Write-Host "  [OK] Directory.Build.props = $Version" -ForegroundColor Green

    [xml]$manifest = Get-Content $manifestPath
    $manifest.Package.Identity.Version = "$Version.0"
    $manifest.Save($manifestPath)
    Write-Host "  [OK] Package.appxmanifest = $Version.0" -ForegroundColor Green

    git add Directory.Build.props src/ClipSave.Package/Package.appxmanifest
    git diff --staged --quiet
    if ($LASTEXITCODE -ne 0) {
        git commit -m "chore: set release version to $Version"
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

    # 7. Switch to main and create PR branch for next development version
    Write-Host "[7/9] Creating PR branch for $MainBranch version bump..." -ForegroundColor Yellow
    git checkout $MainBranch
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to switch back to $MainBranch."
    }
    git checkout -b $mainBumpBranch
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to create PR branch '$mainBumpBranch'."
    }

    Write-Host "[8/9] Updating $mainBumpBranch to $nextMainVersion..." -ForegroundColor Yellow
    [xml]$props = Get-Content $propsPath
    $props.Project.PropertyGroup.Version = $nextMainVersion
    $props.Save($propsPath)
    Write-Host "  [OK] Directory.Build.props = $nextMainVersion" -ForegroundColor Green

    [xml]$manifest = Get-Content $manifestPath
    $manifest.Package.Identity.Version = "$nextMainVersion.0"
    $manifest.Save($manifestPath)
    Write-Host "  [OK] Package.appxmanifest = $nextMainVersion.0" -ForegroundColor Green

    git add Directory.Build.props src/ClipSave.Package/Package.appxmanifest
    git diff --staged --quiet
    if ($LASTEXITCODE -ne 0) {
        git commit -m "chore: bump $MainBranch version to $nextMainVersion"
        if ($LASTEXITCODE -ne 0) {
            Fail "Failed to commit version update on $mainBumpBranch."
        }
    } else {
        Write-Host "  [INFO] No version changes to commit on $mainBumpBranch." -ForegroundColor Gray
    }

    & "$projectRoot\scripts\assert-version-policy.ps1" -ProjectRoot $projectRoot -BranchName $MainBranch
    if ($LASTEXITCODE -ne 0) {
        Fail "Version validation failed on $mainBumpBranch after update."
    }

    # 9. Push if requested
    if ($Push) {
        Write-Host "[9/9] Pushing branches..." -ForegroundColor Yellow
        git push -u origin $branchName
        if ($LASTEXITCODE -ne 0) {
            Fail "Failed to push $branchName."
        }
        Write-Host "  [OK] Pushed $branchName" -ForegroundColor Green

        git push -u origin $mainBumpBranch
        if ($LASTEXITCODE -ne 0) {
            Fail "Failed to push $mainBumpBranch."
        }
        Write-Host "  [OK] Pushed $mainBumpBranch" -ForegroundColor Green
    } else {
        Write-Host "[9/9] Skipping push (use -Push to push automatically)" -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "[OK] Release branch workflow completed." -ForegroundColor Green
    Write-Host ""
    Write-Host "Summary:" -ForegroundColor Cyan
    Write-Host "  Release: $branchName -> $Version" -ForegroundColor White
    Write-Host "  Main PR: $mainBumpBranch -> $nextMainVersion (target: $MainBranch)" -ForegroundColor White
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Keep user-facing changes in CHANGELOG.md under [Unreleased]."
    Write-Host "2. At release finalization, move shipped items to [$Version] - YYYY-MM-DD (see docs/ops/ReleaseNotes.md)."
    if (-not $Push) {
        Write-Host "3. Push both branches:"
        Write-Host "   git push -u origin $branchName"
        Write-Host "   git push -u origin $mainBumpBranch"
        Write-Host "4. Create PR: $mainBumpBranch -> $MainBranch"
        Write-Host "5. Release Build triggers on push to release/*."
    } else {
        Write-Host "3. Create PR: $mainBumpBranch -> $MainBranch"
        Write-Host "4. Release Build will run automatically (already pushed)."
    }
    Write-Host ""
    Write-Host "Patch release reminder:" -ForegroundColor Cyan
    Write-Host "  git checkout $MainBranch"
    Write-Host "  # make fixes and merge to $MainBranch"
    Write-Host "  git checkout $branchName"
    Write-Host "  git cherry-pick <commit-hash>"
}
finally {
    Pop-Location
}
