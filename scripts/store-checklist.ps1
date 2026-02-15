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
    if ($currentBranch -and $currentBranch -match '^release/\d+\.\d+\.x$') {
        Write-Host "PASS" -ForegroundColor Green
    } else {
        Write-Host "FAIL" -ForegroundColor Red
        Write-Host "    Switch to release/X.Y.x before Store submission." -ForegroundColor Gray
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
        & "$projectRoot\scripts\validate-version.ps1" -ProjectRoot $projectRoot -BranchName $currentBranch *>$null
    } else {
        & "$projectRoot\scripts\validate-version.ps1" -ProjectRoot $projectRoot *>$null
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

    # Check 5: GitHub Release exists
    Write-Host -NoNewline "  Checking GitHub Release... "
    $ghAvailable = Get-Command gh -ErrorAction SilentlyContinue
    if ($ghAvailable) {
        $releaseExists = gh release view "v$version" --json tagName 2>$null
        if ($releaseExists) {
            Write-Host "EXISTS" -ForegroundColor Green
        } else {
            Write-Host "NOT FOUND" -ForegroundColor Yellow
            Write-Host "    GitHub Release v$version not found. Consider creating it first." -ForegroundColor Gray
        }
    } else {
        Write-Host "SKIPPED" -ForegroundColor Yellow
        Write-Host "    GitHub CLI not available. Install gh to verify releases." -ForegroundColor Gray
    }

    # Check 6: Release notes
    Write-Host -NoNewline "  Checking release notes... "
    $releaseNotesPath = Join-Path $projectRoot "RELEASE_NOTES.md"
    if (Test-Path $releaseNotesPath) {
        $releaseNotes = Get-Content $releaseNotesPath -Raw
        if ($releaseNotes -match "## v$([regex]::Escape($version))") {
            Write-Host "FOUND" -ForegroundColor Green
        } else {
            Write-Host "NOT FOUND" -ForegroundColor Yellow
            Write-Host "    Release notes for v$version not found in RELEASE_NOTES.md" -ForegroundColor Gray
        }
    } else {
        Write-Host "NOT FOUND" -ForegroundColor Yellow
        Write-Host "    RELEASE_NOTES.md not found at $releaseNotesPath" -ForegroundColor Gray
    }

    Write-Host ""

    # Interactive Checklist
    Write-Host "[Manual Verification Required]" -ForegroundColor Yellow
    Write-Host "Please confirm the following items:`n"

    $checklist = @(
        "GitHub Release tested for at least 24 hours",
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
