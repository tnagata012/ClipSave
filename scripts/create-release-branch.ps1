#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Create a new release branch (Trunk-Based Development)

.DESCRIPTION
    Creates a release branch from main and prepares a PR branch for main version bump.
    - release/X.Y: X.Y.0 (stable)
    - chore/* branch from main: configurable next development line, PR required
    - Package.appxmanifest always keeps numeric X.Y.Z.0

.PARAMETER Version
    Target release series (e.g., 1.3)

.PARAMETER MainBranch
    Trunk branch name (default: main)

.PARAMETER NextMainVersion
    Explicit next development version for the PR branch (e.g., 0.5.0).
    If omitted, defaults to the next minor version on the same major line.

.PARAMETER SkipPull
    Skip pulling latest changes from origin before branching

.PARAMETER Push
    Push branches to origin (default: false)

.EXAMPLE
    .\create-release-branch.ps1 -Version 1.3
    # Creates release/1.3 at 1.3.0 and creates a PR branch for main=1.4.0

.EXAMPLE
    .\create-release-branch.ps1 -Version 0.1 -NextMainVersion 0.5.0
    # Creates release/0.1 at 0.1.0 and creates a PR branch for main=0.5.0

.EXAMPLE
    .\create-release-branch.ps1 -Version 1.3 -Push
    # Same as above, then pushes release + PR branch
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [string]$MainBranch = "main",
    [string]$NextMainVersion = $null,
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

# Validate target release series format (X.Y)
if ($Version -notmatch '^(?<major>\d+)\.(?<minor>\d+)$') {
    Fail "Invalid version format. Use X.Y (example: 1.3)."
}

$major = [int]$matches['major']
$minor = [int]$matches['minor']
$releaseVersion = "$major.$minor.0"

$branchName = "release/$major.$minor"
$releaseVersionTuple = @($major, $minor, 0)

if ($NextMainVersion) {
    if ($NextMainVersion -notmatch '^(?<nextMajor>\d+)\.(?<nextMinor>\d+)\.(?<nextPatch>\d+)$') {
        Fail "Invalid NextMainVersion format. Use X.Y.Z (example: 0.5.0)."
    }

    $nextMajor = [int]$matches['nextMajor']
    $nextMinor = [int]$matches['nextMinor']
    $nextPatch = [int]$matches['nextPatch']

    if ($nextPatch -ne 0) {
        Fail "NextMainVersion must use a .0 patch version (example: 0.5.0)."
    }

    $nextVersionTuple = @($nextMajor, $nextMinor, $nextPatch)
    $isGreater =
        ($nextVersionTuple[0] -gt $releaseVersionTuple[0]) -or
        ($nextVersionTuple[0] -eq $releaseVersionTuple[0] -and $nextVersionTuple[1] -gt $releaseVersionTuple[1]) -or
        ($nextVersionTuple[0] -eq $releaseVersionTuple[0] -and $nextVersionTuple[1] -eq $releaseVersionTuple[1] -and $nextVersionTuple[2] -gt $releaseVersionTuple[2])

    if (-not $isGreater) {
        Fail "NextMainVersion must be greater than release version $releaseVersion. Actual: $NextMainVersion"
    }

    $nextMainVersion = "$nextMajor.$nextMinor.$nextPatch"
} else {
    $nextMinor = $minor + 1
    $nextMainVersion = "$major.$nextMinor.0"
}

$mainBumpBranch = "chore/bump-$MainBranch-to-$nextMainVersion"

$propsPath = Join-Path $projectRoot "Directory.Build.props"
$manifestPath = Join-Path $projectRoot "src/ClipSave.Package/Package.appxmanifest"

Write-Host "=== Create Release Branch (Trunk-Based Development) ===" -ForegroundColor Cyan
Write-Host "Release branch: $branchName (version $releaseVersion)" -ForegroundColor White
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
        Fail "Target release series $Version does not match current $MainBranch line $mainVersion. Use a matching X.Y series."
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
    Write-Host "[6/9] Updating release branch version to $releaseVersion..." -ForegroundColor Yellow
    [xml]$props = Get-Content $propsPath
    $props.Project.PropertyGroup.Version = $releaseVersion
    $props.Save($propsPath)
    Write-Host "  [OK] Directory.Build.props = $releaseVersion" -ForegroundColor Green

    [xml]$manifest = Get-Content $manifestPath
    $manifest.Package.Identity.Version = "$releaseVersion.0"
    $manifest.Save($manifestPath)
    Write-Host "  [OK] Package.appxmanifest = $releaseVersion.0" -ForegroundColor Green

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
    Write-Host "  Release: $branchName -> $releaseVersion" -ForegroundColor White
    Write-Host "  Main PR: $mainBumpBranch -> $nextMainVersion (target: $MainBranch)" -ForegroundColor White
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Keep user-facing changes in CHANGELOG.md under [Unreleased] while release contents are still moving."
    Write-Host "2. Before tagging, move shipped items in the last release-side PR (typically a stabilization/RC PR) to [$releaseVersion] - YYYY-MM-DD (see docs/release/ReleaseNotes.md)."
    if (-not $Push) {
        Write-Host "3. Push both branches:"
        Write-Host "   git push -u origin $branchName"
        Write-Host "   git push -u origin $mainBumpBranch"
        Write-Host "4. Create PR: $mainBumpBranch -> $MainBranch"
        Write-Host "5. RC Build triggers on push to release/*."
    } else {
        Write-Host "3. Create PR: $mainBumpBranch -> $MainBranch"
        Write-Host "4. RC Build will run automatically (already pushed)."
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
