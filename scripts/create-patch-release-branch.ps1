#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Create a patch release initialization branch from release/X.Y.

.DESCRIPTION
    Creates a patch-start branch from a release branch and bumps version once.
    - release/X.Y current version: X.Y.Z
    - requires finalized tag X.Y.Z and GitHub Release X.Y.Z with archive assets
    - release/X.Y HEAD may be ahead of the finalized tag only by docs/site/docs-only workflow files
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

.PARAMETER ProjectRoot
    Override the repository root path when running the script from an external tooling checkout.

.EXAMPLE
    .\create-patch-release-branch.ps1 -ReleaseBranch release/1.3

.EXAMPLE
    .\create-patch-release-branch.ps1 -ReleaseBranch release/1.3 -Push
#>

param(
    [string]$ReleaseBranch = $null,
    [switch]$SkipPull = $false,
    [switch]$Push = $false,
    [string]$ProjectRoot = $null
)

$ErrorActionPreference = "Stop"

if ($ProjectRoot) {
    try {
        $projectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
    }
    catch {
        Write-Host "`n[ERROR] ProjectRoot not found: $ProjectRoot" -ForegroundColor Red
        exit 1
    }
} else {
    $projectRoot = Split-Path -Parent $PSScriptRoot
}

$scriptRoot = $PSScriptRoot
$releasePattern = '^release/(?<major>\d+)\.(?<minor>\d+)$'
$semverPattern = '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$'
$propsPath = Join-Path $projectRoot "Directory.Build.props"
$manifestPath = Join-Path $projectRoot "src/ClipSave.Package/Package.appxmanifest"

. (Join-Path $scriptRoot "release-support.ps1")
. (Join-Path $scriptRoot "version-file-support.ps1")

function Fail([string]$Message) {
    Write-Host "`n[ERROR] $Message" -ForegroundColor Red
    exit 1
}

function Resolve-GitHubRepository {
    $remoteUrl = git config --get remote.origin.url 2>$null
    if (-not $remoteUrl) {
        return $null
    }

    $pattern = 'github\.com[:/](?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$'
    $match = [regex]::Match($remoteUrl.Trim(), $pattern)
    if (-not $match.Success) {
        return $null
    }

    return "$($match.Groups['owner'].Value)/$($match.Groups['repo'].Value)"
}

function Get-RemoteTagCommit([string]$Remote, [string]$TagName) {
    $escapedTag = [regex]::Escape($TagName)
    $lines = @(git ls-remote --tags $Remote "refs/tags/$TagName" "refs/tags/$TagName^{}" 2>$null)
    if ($LASTEXITCODE -ne 0 -or $lines.Count -eq 0) {
        return $null
    }

    foreach ($line in $lines) {
        if ($line -match "^(?<sha>[0-9a-f]{40})\s+refs/tags/$escapedTag\^\{\}$") {
            return $matches['sha']
        }
    }

    foreach ($line in $lines) {
        if ($line -match "^(?<sha>[0-9a-f]{40})\s+refs/tags/$escapedTag$") {
            return $matches['sha']
        }
    }

    return $null
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

    Write-Host "[1/9] Switching to $ReleaseBranch..." -ForegroundColor Yellow
    git checkout $ReleaseBranch
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to checkout $ReleaseBranch."
    }

    if ($SkipPull) {
        Write-Host "[2/9] Skipping pull (use -SkipPull:`$false to enable)." -ForegroundColor Gray
    } elseif ($hasOrigin) {
        Write-Host "[2/9] Pulling latest $ReleaseBranch..." -ForegroundColor Yellow
        git pull origin $ReleaseBranch
        if ($LASTEXITCODE -ne 0) {
            Fail "Failed to pull from origin/$ReleaseBranch."
        }
    } else {
        Write-Host "[2/9] Remote 'origin' not found. Skipping pull." -ForegroundColor Yellow
    }

    Write-Host "[3/9] Validating release branch version policy..." -ForegroundColor Yellow
    & (Join-Path $scriptRoot "assert-version-policy.ps1") -ProjectRoot $projectRoot -BranchName $ReleaseBranch
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

    Write-Host "[4/9] Verifying current version finalization..." -ForegroundColor Yellow
    if (-not $hasOrigin) {
        Fail "Cannot verify current version finalization because remote 'origin' is not configured."
    }

    $tagCommit = Get-RemoteTagCommit -Remote "origin" -TagName $currentVersion
    if (-not $tagCommit) {
        Fail "Current version '$currentVersion' is not finalized yet. Create and push tag '$currentVersion', run Release Finalize, then start the next patch cycle."
    }

    $headCommit = (git rev-parse HEAD).Trim()
    if (-not $headCommit -or $headCommit.Length -ne 40) {
        Fail "Failed to resolve HEAD commit for $ReleaseBranch."
    }
    if ($headCommit -ne $tagCommit) {
        $blockingFiles = Get-BlockingFilesAheadOfFinalizedTag -TagCommit $tagCommit -HeadCommit $headCommit
        if ($blockingFiles.Count -gt 0) {
            $blockingList = $blockingFiles -join ", "
            Fail "Release branch HEAD ($headCommit) is ahead of finalized tag '$currentVersion' ($tagCommit) with product-affecting changes: $blockingList. Start the next patch cycle only from the finalized commit or after removing those changes."
        }

        Write-Warning "Release branch HEAD is ahead of finalized tag '$currentVersion' only by non-product files. Continuing from HEAD."
    }

    $repo = Resolve-GitHubRepository
    if (-not $repo) {
        Fail "Cannot resolve GitHub repository from remote 'origin'. Use the workflow or configure a GitHub origin before starting the next patch cycle."
    }

    $ghAvailable = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $ghAvailable) {
        Fail "GitHub CLI 'gh' is required to verify Release Finalize archive for '$currentVersion'. Install gh or use the Prepare Patch Release workflow."
    }

    & (Join-Path $scriptRoot "assert-finalized-release-archive.ps1") `
        -Repository $repo `
        -Version $currentVersion
    if ($LASTEXITCODE -ne 0) {
        Fail "Release Finalize archive verification failed for '$currentVersion'."
    }
    Write-Host "  [OK] Current version finalized: $currentVersion ($tagCommit)" -ForegroundColor Green

    if ($currentPatch -ge 65535) {
        Fail "Cannot increment patch. Current patch is $currentPatch (max supported 65535)."
    }

    $nextPatch = $currentPatch + 1
    $nextVersion = "$currentMajor.$currentMinor.$nextPatch"
    $patchInitBranch = "chore/release-$nextVersion-init"

    Write-Host "Current version: $currentVersion" -ForegroundColor White
    Write-Host "Next version   : $nextVersion" -ForegroundColor White
    Write-Host "Patch branch   : $patchInitBranch" -ForegroundColor White

    Write-Host "[5/9] Checking branch existence..." -ForegroundColor Yellow
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

    Write-Host "[6/9] Creating patch init branch..." -ForegroundColor Yellow
    git checkout -b $patchInitBranch
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to create '$patchInitBranch'."
    }

    Write-Host "[7/9] Updating version files to $nextVersion..." -ForegroundColor Yellow
    $versionFiles = Set-RepositoryVersionFiles -ProjectRoot $projectRoot -Version $nextVersion
    Write-Host "  [OK] Directory.Build.props = $($versionFiles.Version)" -ForegroundColor Green
    Write-Host "  [OK] Package.appxmanifest = $($versionFiles.ManifestVersion)" -ForegroundColor Green

    git add Directory.Build.props src/ClipSave.Package/Package.appxmanifest
    git diff --staged --quiet
    if ($LASTEXITCODE -eq 0) {
        Fail "No staged changes detected for patch init branch."
    }

    git commit -m "chore: start $nextVersion patch release"
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to commit version update on $patchInitBranch."
    }

    & (Join-Path $scriptRoot "assert-version-policy.ps1") -ProjectRoot $projectRoot -BranchName $ReleaseBranch
    if ($LASTEXITCODE -ne 0) {
        Fail "Version validation failed after update."
    }

    if ($Push) {
        Write-Host "[8/9] Pushing $patchInitBranch..." -ForegroundColor Yellow
        git push -u origin $patchInitBranch
        if ($LASTEXITCODE -ne 0) {
            Fail "Failed to push $patchInitBranch."
        }
        Write-Host "  [OK] Pushed $patchInitBranch" -ForegroundColor Green
    } else {
        Write-Host "[8/9] Skipping push (use -Push to push automatically)" -ForegroundColor Gray
    }

    Write-Host "[9/9] Completed patch init branch creation." -ForegroundColor Green
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

