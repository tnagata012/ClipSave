#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Store deployment checklist validator

.DESCRIPTION
    Validates prerequisites before submitting to Microsoft Store.
    Checks a target finalized version, tests when the working tree matches it,
    and provides an interactive checklist.

.EXAMPLE
    .\store-checklist.ps1

.EXAMPLE
    .\store-checklist.ps1 -Version 1.3.0
#>

param(
    [string]$Version = $null,
    [string]$ReleaseBranch = $null
)

$ErrorActionPreference = "Stop"

# Get project root
$projectRoot = Split-Path -Parent $PSScriptRoot

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

function Get-RepositoryFileContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath,
        [string]$GitRef = $null
    )

    if (-not $GitRef) {
        $absolutePath = Join-Path $projectRoot $RelativePath
        if (-not (Test-Path $absolutePath)) {
            return $null
        }

        return Get-Content -LiteralPath $absolutePath -Raw
    }

    $objectSpec = "${GitRef}:$RelativePath"
    $content = git show $objectSpec 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    return ($content -join [Environment]::NewLine)
}

function Get-GitRefCommit {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Ref
    )

    $commit = git rev-list -n 1 $Ref 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $commit) {
        return $null
    }

    $commit = $commit.Trim()
    if ($commit -notmatch '^[0-9a-f]{40}$') {
        return $null
    }

    return $commit
}

function Resolve-BranchVerificationRef {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BranchName
    )

    $localRef = "refs/heads/$BranchName"
    git show-ref --verify --quiet $localRef
    if ($LASTEXITCODE -eq 0) {
        return $localRef
    }

    $remoteRef = "refs/remotes/origin/$BranchName"
    git show-ref --verify --quiet $remoteRef
    if ($LASTEXITCODE -eq 0) {
        return $remoteRef
    }

    return $null
}

Push-Location $projectRoot
try {
    git rev-parse --is-inside-work-tree *> $null
    if ($LASTEXITCODE -ne 0) {
        Fail "Not inside a git repository: $projectRoot"
    }

    $propsPath = Join-Path $projectRoot "Directory.Build.props"
    if (-not (Test-Path $propsPath)) {
        Fail "Directory.Build.props not found: $propsPath"
    }

    [xml]$workingProps = Get-Content $propsPath
    $workingVersion = $workingProps.Project.PropertyGroup.Version
    if ($workingVersion) {
        $workingVersion = $workingVersion.Trim()
    }
    $currentBranch = git branch --show-current 2>$null
    if ($currentBranch) {
        $currentBranch = $currentBranch.Trim()
    }
    $currentHeadCommit = Get-GitRefCommit -Ref "HEAD"
    $workingTreeIsClean = -not (git status --porcelain)

    $targetVersion = $Version
    if ($targetVersion) {
        $targetVersion = $targetVersion.Trim()
    }
    if (-not $targetVersion) {
        $targetVersion = $workingVersion
    }
    if (-not $targetVersion) {
        Fail "Could not resolve target version. Specify -Version X.Y.Z or run from a checkout with Directory.Build.props."
    }
    if ($targetVersion -notmatch '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$') {
        Fail "Invalid target version format: $targetVersion (expected X.Y.Z)."
    }

    $versionSeries = "release/$($matches['major']).$($matches['minor'])"

    $targetReleaseBranch = $ReleaseBranch
    if ($targetReleaseBranch) {
        $targetReleaseBranch = $targetReleaseBranch.Trim()
    } else {
        $targetReleaseBranch = $versionSeries
    }
    if ($targetReleaseBranch -notmatch '^release/\d+\.\d+$') {
        Fail "Invalid target release branch format: $targetReleaseBranch (expected release/X.Y)."
    }
    if ($targetReleaseBranch -ne $versionSeries) {
        Fail "Target version '$targetVersion' and release branch '$targetReleaseBranch' point to different release series."
    }

    $versionSpecified = $PSBoundParameters.ContainsKey("Version")
    $targetGitRef = if ($versionSpecified) { "refs/tags/$targetVersion" } else { $null }
    $targetCommit = if ($versionSpecified) { Get-GitRefCommit -Ref $targetGitRef } else { $null }
    $branchVerificationRef = if ($versionSpecified) { Resolve-BranchVerificationRef -BranchName $targetReleaseBranch } else { $null }
    $testSkipReason = "Working tree does not match target version/tag. Tests were not rerun locally for this checklist."
    if ($versionSpecified) {
        $workingTreeMatchesTarget = ($currentHeadCommit -eq $targetCommit) -and $workingTreeIsClean
        if (-not $targetCommit) {
            $testSkipReason = "Target tag '$targetGitRef' is not available locally. Tests were not rerun locally for this checklist."
        } elseif (-not $workingTreeIsClean) {
            $testSkipReason = "Working tree has local modifications. Tests were not rerun against the exact target tag checkout."
        } elseif ($currentHeadCommit -ne $targetCommit) {
            $testSkipReason = "Working tree is not checked out at target tag '$targetVersion'. Tests were not rerun locally for this checklist."
        }
    } else {
        $workingTreeMatchesTarget = ($workingVersion -eq $targetVersion) -and ($currentBranch -eq $targetReleaseBranch)
    }

    Write-Host "=== Microsoft Store Deployment Checklist ===" -ForegroundColor Green
    Write-Host "Target Version : $targetVersion" -ForegroundColor Cyan
    Write-Host "Target Branch  : $targetReleaseBranch" -ForegroundColor Cyan
    Write-Host "Working Branch : $currentBranch" -ForegroundColor Cyan
    Write-Host "Working Version: $workingVersion`n" -ForegroundColor Cyan

    $allPassed = $true
    $ghAvailable = Get-Command gh -ErrorAction SilentlyContinue
    $repo = $null
    if ($ghAvailable) {
        $repo = Resolve-GitHubRepository
    }

    # Technical Validation
    Write-Host "[Technical Validation]" -ForegroundColor Yellow

    # Check 1: Target release branch
    Write-Host -NoNewline "  Checking target release branch... "
    if ($targetReleaseBranch -match '^release/\d+\.\d+$') {
        Write-Host "PASS" -ForegroundColor Green
    } else {
        Write-Host "FAIL" -ForegroundColor Red
        Write-Host "    Store submission requires target release branch release/X.Y." -ForegroundColor Gray
        $allPassed = $false
    }

    # Check 2: Target version format
    Write-Host -NoNewline "  Checking target version format... "
    if ($targetVersion -match '^\d+\.\d+\.\d+$') {
        Write-Host "PASS" -ForegroundColor Green
    } else {
        Write-Host "FAIL" -ForegroundColor Red
        Write-Host "    Store submission requires version X.Y.Z." -ForegroundColor Gray
        $allPassed = $false
    }

    # Check 3: Target version validation
    Write-Host -NoNewline "  Checking target version consistency... "
    if ($versionSpecified) {
        if (-not $targetCommit) {
            Write-Host "FAIL" -ForegroundColor Red
            Write-Host "    Target tag '$targetGitRef' is not available locally. Fetch tags and try again." -ForegroundColor Gray
            $allPassed = $false
        } else {
            & "$projectRoot\scripts\assert-version-policy.ps1" `
                -ProjectRoot $projectRoot `
                -BranchName $targetReleaseBranch `
                -GitRef $targetGitRef *>$null
            if ($LASTEXITCODE -ne 0) {
                Write-Host "FAIL" -ForegroundColor Red
                $allPassed = $false
            } elseif (-not $branchVerificationRef) {
                Write-Host "FAIL" -ForegroundColor Red
                Write-Host "    Target release branch '$targetReleaseBranch' is not available locally. Fetch it and try again." -ForegroundColor Gray
                $allPassed = $false
            } else {
                git merge-base --is-ancestor $targetCommit $branchVerificationRef *> $null
                $mergeBaseExit = $LASTEXITCODE
                if ($mergeBaseExit -eq 0) {
                    Write-Host "PASS" -ForegroundColor Green
                } elseif ($mergeBaseExit -eq 1) {
                    Write-Host "FAIL" -ForegroundColor Red
                    Write-Host "    Target tag '$targetVersion' is not contained in '$targetReleaseBranch'." -ForegroundColor Gray
                    $allPassed = $false
                } else {
                    Write-Host "FAIL" -ForegroundColor Red
                    Write-Host "    Failed to verify whether target tag '$targetVersion' is contained in '$targetReleaseBranch'." -ForegroundColor Gray
                    $allPassed = $false
                }
            }
        }
    } else {
        & "$projectRoot\scripts\assert-version-policy.ps1" `
            -ProjectRoot $projectRoot `
            -BranchName $targetReleaseBranch *>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "PASS" -ForegroundColor Green
        } else {
            Write-Host "FAIL" -ForegroundColor Red
            $allPassed = $false
        }
    }

    # Check 4: Latest finalized version
    Write-Host -NoNewline "  Checking latest finalized version... "
    if ($ghAvailable) {
        if (-not $repo) {
            Write-Host "SKIPPED" -ForegroundColor Yellow
            Write-Host "    Could not resolve GitHub repository from remote.origin.url." -ForegroundColor Gray
            $allPassed = $false
        } else {
            $latestArgs = @{
                Repository = $repo
                Version    = $targetVersion
                ReleaseBranch = $targetReleaseBranch
            }

            & "$projectRoot\scripts\assert-latest-finalized-version.ps1" @latestArgs *>$null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "PASS" -ForegroundColor Green
            } else {
                Write-Host "FAIL" -ForegroundColor Red
                Write-Host "    Store submission is allowed only for the latest finalized version." -ForegroundColor Gray
                $allPassed = $false
            }
        }
    } else {
        Write-Host "SKIPPED" -ForegroundColor Yellow
        Write-Host "    GitHub CLI not available. Install gh to verify the latest finalized version." -ForegroundColor Gray
        $allPassed = $false
    }

    # Check 5: Tests
    Write-Host -NoNewline "  Running tests (Unit/Integration)... "
    if ($workingTreeMatchesTarget) {
        & "$projectRoot\scripts\run-tests.ps1" -Configuration Release -Verbosity quiet *>$null
        $testPassed = $LASTEXITCODE -eq 0
        if ($testPassed) {
            Write-Host "PASS" -ForegroundColor Green
        } else {
            Write-Host "FAIL" -ForegroundColor Red
            $allPassed = $false
        }
    } else {
        Write-Host "SKIPPED" -ForegroundColor Yellow
        Write-Host "    $testSkipReason" -ForegroundColor Gray
    }

    # Check 6: RC artifact and release archive exist
    Write-Host -NoNewline "  Checking RC artifact and release archive... "
    if ($ghAvailable) {
        if (-not $repo) {
            Write-Host "SKIPPED" -ForegroundColor Yellow
            Write-Host "    Could not resolve GitHub repository from remote.origin.url." -ForegroundColor Gray
            $allPassed = $false
        } else {
            $candidateArtifactName = "rc-package-$targetVersion"
            $missingArtifacts = @()
            $queryFailed = $false

            $jqFilter = ".artifacts[] | select(.name == `"$candidateArtifactName`" and .expired == false) | .id"
            $artifactId = gh api --paginate "repos/$repo/actions/artifacts?per_page=100" --jq $jqFilter 2>$null | Select-Object -First 1
            if ($LASTEXITCODE -ne 0) {
                $queryFailed = $true
            }

            if (-not $queryFailed -and [string]::IsNullOrWhiteSpace($artifactId)) {
                $missingArtifacts += $candidateArtifactName
            }

            $releaseArchiveVerified = $false
            if (-not $queryFailed) {
                gh release view $targetVersion --repo $repo *> $null
                $releaseArchiveVerified = $LASTEXITCODE -eq 0
            }

            if ($queryFailed) {
                Write-Host "SKIPPED" -ForegroundColor Yellow
                Write-Host "    Failed to query Actions artifacts via GitHub CLI." -ForegroundColor Gray
                $allPassed = $false
            } elseif ($missingArtifacts.Count -eq 0 -and $releaseArchiveVerified) {
                Write-Host "PASS" -ForegroundColor Green
            } else {
                Write-Host "NOT FOUND" -ForegroundColor Yellow
                foreach ($artifact in $missingArtifacts) {
                    Write-Host "    Missing artifact: $artifact" -ForegroundColor Gray
                }
                if (-not $releaseArchiveVerified) {
                    Write-Host "    Missing release archive GitHub Release: $targetVersion" -ForegroundColor Gray
                }
                Write-Host "    Run RC Build / Release Finalize and confirm outputs are retained." -ForegroundColor Gray
                $allPassed = $false
            }
        }
    } else {
        Write-Host "SKIPPED" -ForegroundColor Yellow
        Write-Host "    GitHub CLI not available. Install gh to verify RC/archive artifacts." -ForegroundColor Gray
        $allPassed = $false
    }

    # Check 7: Changelog
    Write-Host -NoNewline "  Checking changelog... "
    $changelogGitRef = if ($versionSpecified) { $targetGitRef } else { $null }
    $changelog = Get-RepositoryFileContent -RelativePath "CHANGELOG.md" -GitRef $changelogGitRef
    if ($changelog) {
        $versionPattern = [regex]::Escape($targetVersion)
        $modernPattern = "(?m)^##\s+\[$versionPattern\]\s*-\s*\d{4}-\d{2}-\d{2}\s*$"

        if ($changelog -match $modernPattern) {
            Write-Host "FOUND" -ForegroundColor Green
        } else {
            Write-Host "NOT FOUND" -ForegroundColor Yellow
            Write-Host "    Changelog entry not found in expected format: ## [$targetVersion] - YYYY-MM-DD" -ForegroundColor Gray
            $allPassed = $false
        }
    } else {
        Write-Host "NOT FOUND" -ForegroundColor Yellow
        if ($versionSpecified) {
            Write-Host "    CHANGELOG.md was not found at target tag $targetGitRef. Fetch the tag locally and try again." -ForegroundColor Gray
        } else {
            Write-Host "    CHANGELOG.md was not found at $projectRoot" -ForegroundColor Gray
        }
        $allPassed = $false
    }

    Write-Host ""

    # Interactive Checklist
    Write-Host "[Manual Verification Required]" -ForegroundColor Yellow
    Write-Host "Please confirm the following items:`n"

    $checklist = @(
        "RC package artifact (rc-package-$targetVersion, unsigned) reviewed for at least 24 hours",
        "Confirmed tag X.Y.Z (=$targetVersion) created and pushed",
        "Release archive GitHub Release X.Y.Z (=$targetVersion) created by Release Finalize",
        "No critical bugs reported",
        "Partner Center app description updated (Japanese)",
        "Partner Center app description updated (English)",
        "Screenshots updated (if needed)",
        "Age rating verified",
        "Privacy policy URL up to date",
        "Support contact information current"
    )

    $manualChecks = @()
    foreach ($item in $checklist) {
        $response = Read-Host "  [ ] $item (y/n)"
        $manualChecks += [PSCustomObject]@{
            Item = $item
            Confirmed = ($response -eq 'y')
        }
    }

    Write-Host ""

    # Summary
    $allManualPassed = ($manualChecks | Where-Object { -not $_.Confirmed }).Count -eq 0

    if ($allPassed -and $allManualPassed) {
        Write-Host "All checks passed. Ready for Store submission." -ForegroundColor Green
        Write-Host "`nNext step: Run Release Finalize workflow with version=$targetVersion and build_store_package=true" -ForegroundColor Cyan
        exit 0
    } else {
        Write-Host "Some checks did not pass:" -ForegroundColor Yellow

        if (-not $allPassed) {
            Write-Host "  - Technical validation issues detected" -ForegroundColor Red
        }

        $failedManual = $manualChecks | Where-Object { -not $_.Confirmed }
        if ($failedManual) {
            Write-Host "  - Manual verification incomplete:" -ForegroundColor Yellow
            foreach ($item in $failedManual) {
                Write-Host "    • $($item.Item)" -ForegroundColor Gray
            }
        }

        Write-Host "`nPlease resolve these issues before Store submission." -ForegroundColor Yellow
        exit 1
    }
}
finally {
    Pop-Location
}
