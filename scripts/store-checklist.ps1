#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Store deployment checklist validator

.DESCRIPTION
    Validates prerequisites before submitting to Microsoft Store.
    Checks version, tests, and provides interactive checklist.

.EXAMPLE
    .\store-checklist.ps1
#>

$ErrorActionPreference = "Stop"

# Get project root
$projectRoot = Split-Path -Parent $PSScriptRoot

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

Push-Location $projectRoot
try {
    # Get version
    [xml]$props = Get-Content "$projectRoot\Directory.Build.props"
    $version = $props.Project.PropertyGroup.Version
    $currentBranch = git branch --show-current 2>$null

    Write-Host "=== Microsoft Store Deployment Checklist ===" -ForegroundColor Green
    Write-Host "Version: $version" -ForegroundColor Cyan
    Write-Host "Branch : $currentBranch`n" -ForegroundColor Cyan

    $allPassed = $true

    # Technical Validation
    Write-Host "[Technical Validation]" -ForegroundColor Yellow

    # Check 1: Release branch
    Write-Host -NoNewline "  Checking current branch... "
    if ($currentBranch -and $currentBranch -match '^release/\d+\.\d+$') {
        Write-Host "PASS" -ForegroundColor Green
    } else {
        Write-Host "FAIL" -ForegroundColor Red
        Write-Host "    Switch to release/X.Y before Store submission." -ForegroundColor Gray
        $allPassed = $false
    }

    # Check 2: Version format
    Write-Host -NoNewline "  Checking stable version format... "
    if ($version -match '^\d+\.\d+\.\d+$') {
        Write-Host "PASS" -ForegroundColor Green
    } else {
        Write-Host "FAIL" -ForegroundColor Red
        Write-Host "    Store submission requires version X.Y.Z." -ForegroundColor Gray
        $allPassed = $false
    }

    # Check 3: Version validation
    Write-Host -NoNewline "  Checking version consistency... "
    if ($currentBranch) {
        & "$projectRoot\scripts\assert-version-policy.ps1" -ProjectRoot $projectRoot -BranchName $currentBranch *>$null
    } else {
        & "$projectRoot\scripts\assert-version-policy.ps1" -ProjectRoot $projectRoot *>$null
    }
    if ($LASTEXITCODE -eq 0) {
        Write-Host "PASS" -ForegroundColor Green
    } else {
        Write-Host "FAIL" -ForegroundColor Red
        $allPassed = $false
    }

    # Check 4: Tests
    Write-Host -NoNewline "  Running tests (Unit/Integration)... "
    & "$projectRoot\scripts\run-tests.ps1" -Configuration Release -Verbosity quiet *>$null
    $testPassed = $LASTEXITCODE -eq 0
    if ($testPassed) {
        Write-Host "PASS" -ForegroundColor Green
    } else {
        Write-Host "FAIL" -ForegroundColor Red
        $allPassed = $false
    }

    # Check 5: Release artifacts exist
    Write-Host -NoNewline "  Checking release artifacts... "
    $ghAvailable = Get-Command gh -ErrorAction SilentlyContinue
    if ($ghAvailable) {
        $repo = Resolve-GitHubRepository
        if (-not $repo) {
            Write-Host "SKIPPED" -ForegroundColor Yellow
            Write-Host "    Could not resolve GitHub repository from remote.origin.url." -ForegroundColor Gray
            $allPassed = $false
        } else {
            $requiredArtifacts = @(
                "release-package-$version"
            )
            $missingArtifacts = @()
            $queryFailed = $false

            foreach ($artifactName in $requiredArtifacts) {
                $jqFilter = ".artifacts[] | select(.name == `"$artifactName`" and .expired == false) | .id"
                $artifactId = gh api --paginate "repos/$repo/actions/artifacts?per_page=100" --jq $jqFilter 2>$null | Select-Object -First 1
                if ($LASTEXITCODE -ne 0) {
                    $queryFailed = $true
                    break
                }

                if ([string]::IsNullOrWhiteSpace($artifactId)) {
                    $missingArtifacts += $artifactName
                }
            }

            if ($queryFailed) {
                Write-Host "SKIPPED" -ForegroundColor Yellow
                Write-Host "    Failed to query Actions artifacts via GitHub CLI." -ForegroundColor Gray
                $allPassed = $false
            } elseif ($missingArtifacts.Count -eq 0) {
                Write-Host "PASS" -ForegroundColor Green
            } else {
                Write-Host "NOT FOUND" -ForegroundColor Yellow
                foreach ($artifact in $missingArtifacts) {
                    Write-Host "    Missing artifact: $artifact" -ForegroundColor Gray
                }
                Write-Host "    Run Release Build and confirm artifacts are retained." -ForegroundColor Gray
                $allPassed = $false
            }
        }
    } else {
        Write-Host "SKIPPED" -ForegroundColor Yellow
        Write-Host "    GitHub CLI not available. Install gh to verify release artifacts." -ForegroundColor Gray
        $allPassed = $false
    }

    # Check 6: Changelog
    Write-Host -NoNewline "  Checking changelog... "
    $changelogPath = Join-Path $projectRoot "CHANGELOG.md"

    if (Test-Path $changelogPath) {
        $changelog = Get-Content $changelogPath -Raw
        $versionPattern = [regex]::Escape($version)
        $modernPattern = "(?m)^##\s+\[$versionPattern\]\s*-\s*\d{4}-\d{2}-\d{2}\s*$"

        if ($changelog -match $modernPattern) {
            Write-Host "FOUND" -ForegroundColor Green
        } else {
            Write-Host "NOT FOUND" -ForegroundColor Yellow
            Write-Host "    Changelog entry not found in expected format: ## [$version] - YYYY-MM-DD" -ForegroundColor Gray
            $allPassed = $false
        }
    } else {
        Write-Host "NOT FOUND" -ForegroundColor Yellow
        Write-Host "    CHANGELOG.md was not found at $projectRoot" -ForegroundColor Gray
        $allPassed = $false
    }

    Write-Host ""

    # Interactive Checklist
    Write-Host "[Manual Verification Required]" -ForegroundColor Yellow
    Write-Host "Please confirm the following items:`n"

    $checklist = @(
        "Release package artifact (release-package-$version, unsigned) reviewed for at least 24 hours",
        "Selected source ref for Store Publish recorded (recommended: X.Y.Z tag; exception: commit SHA)",
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
        Write-Host "`nNext step: Run .\scripts\build-store-package.ps1" -ForegroundColor Cyan
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
