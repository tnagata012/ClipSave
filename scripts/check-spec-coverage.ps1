<#
.SYNOPSIS
    Checks SPEC-ID traceability between Specification.md and test files.

.DESCRIPTION
    Extracts all SPEC-IDs from docs/dev/Specification.md and compares them
    against [Spec("SPEC-xxx-yyy")] attributes in Integration/UI test files.
    Reports covered and uncovered specifications.

.PARAMETER ProjectRoot
    Root directory of the repository. Defaults to the script's parent directory.

.EXAMPLE
    .\scripts\check-spec-coverage.ps1
    .\scripts\check-spec-coverage.ps1 -ProjectRoot C:\path\to\ClipSave
#>

param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$specFile = Join-Path $ProjectRoot 'docs/dev/Specification.md'
$testsDir = Join-Path $ProjectRoot 'tests'
$integrationTestsDir = Join-Path $testsDir 'ClipSave.IntegrationTests'
$uiTestsDir = Join-Path $testsDir 'ClipSave.UiTests'

if (-not (Test-Path $specFile)) {
    Write-Error "Specification file not found: $specFile"
    exit 1
}

if (-not (Test-Path $testsDir)) {
    Write-Error "Tests directory not found: $testsDir"
    exit 1
}

if (-not (Test-Path $integrationTestsDir)) {
    Write-Error "Integration tests directory not found: $integrationTestsDir"
    exit 1
}

if (-not (Test-Path $uiTestsDir)) {
    Write-Error "UI tests directory not found: $uiTestsDir"
    exit 1
}

# Extract SPEC-IDs from Specification.md
$specContent = Get-Content $specFile -Raw
$specIds = [regex]::Matches($specContent, '\| (SPEC-\d{3}-\d{3}) \|') |
    ForEach-Object { $_.Groups[1].Value } |
    Sort-Object -Unique

Write-Host "=== SPEC Coverage Report ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Specifications defined: $($specIds.Count)" -ForegroundColor White

# Extract [Spec("SPEC-xxx-yyy")] from Integration/UI test files
$testFiles = @(
    Get-ChildItem -Path $integrationTestsDir -Filter '*.cs' -Recurse
    Get-ChildItem -Path $uiTestsDir -Filter '*.cs' -Recurse
)
$coveredSpecs = @{}

foreach ($file in $testFiles) {
    $content = Get-Content $file.FullName -Raw
    $matches = [regex]::Matches($content, '\[Spec\("(SPEC-\d{3}-\d{3})"\)\]')
    foreach ($match in $matches) {
        $specId = $match.Groups[1].Value
        $relativePath = $file.FullName.Substring($ProjectRoot.Length + 1) -replace '\\', '/'
        if (-not $coveredSpecs.ContainsKey($specId)) {
            $coveredSpecs[$specId] = @()
        }
        $coveredSpecs[$specId] += $relativePath
    }
}

# Categorize
$covered = @($specIds | Where-Object { $coveredSpecs.ContainsKey($_) })
$uncovered = @($specIds | Where-Object { -not $coveredSpecs.ContainsKey($_) })

# Detect orphaned specs (in tests but not in Specification.md)
$orphaned = @($coveredSpecs.Keys | Where-Object { $_ -notin $specIds } | Sort-Object)

Write-Host "Covered by tests:      $($covered.Count)" -ForegroundColor Green
Write-Host "Not covered:           $($uncovered.Count)" -ForegroundColor Yellow
if ($orphaned.Count -gt 0) {
    Write-Host "Orphaned (in tests only): $($orphaned.Count)" -ForegroundColor Red
}

$coveragePercent = if ($specIds.Count -gt 0) {
    [math]::Round(($covered.Count / $specIds.Count) * 100, 1)
} else { 0 }
Write-Host "Coverage:              $coveragePercent%" -ForegroundColor White

# Details
if ($uncovered.Count -gt 0) {
    Write-Host ""
    Write-Host "--- Uncovered Specifications ---" -ForegroundColor Yellow
    $currentCategory = ''
    foreach ($id in $uncovered) {
        $category = $id.Substring(5, 3)
        if ($category -ne $currentCategory) {
            $currentCategory = $category
            Write-Host "  Category $category`:" -ForegroundColor DarkYellow
        }
        Write-Host "    $id"
    }
}

if ($orphaned.Count -gt 0) {
    Write-Host ""
    Write-Host "--- Orphaned Specs (in tests but not in Specification.md) ---" -ForegroundColor Red
    foreach ($id in $orphaned) {
        $files = ($coveredSpecs[$id] | Select-Object -Unique) -join ', '
        Write-Host "  $id -> $files" -ForegroundColor DarkRed
    }
}

Write-Host ""

# Exit with non-zero if orphaned specs exist (indicates stale references)
if ($orphaned.Count -gt 0) {
    Write-Host "FAIL: Orphaned SPEC-IDs detected. Update tests or Specification.md." -ForegroundColor Red
    exit 1
}

Write-Host "OK" -ForegroundColor Green
exit 0
