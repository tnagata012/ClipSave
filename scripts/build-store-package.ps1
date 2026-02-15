#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build MSIX package for Microsoft Store submission

.DESCRIPTION
    Builds a Store upload package (.msixupload) for submission to Partner Center.
    Validates version, runs security checks, and runs tests before building.

.PARAMETER Version
    Version to build (e.g., "1.0.0"). If not specified, reads from Directory.Build.props

.EXAMPLE
    .\build-store-package.ps1
    .\build-store-package.ps1 -Version "1.2.0"
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

# Get project root
$projectRoot = Split-Path -Parent $PSScriptRoot

Push-Location $projectRoot
try {
    # Validate current branch (Store package must be built from release branch)
    $currentBranch = git branch --show-current 2>$null
    if (-not $currentBranch -or $currentBranch -notmatch '^release/\d+\.\d+\.x$') {
        Write-Error "Current branch is '$currentBranch'. Switch to a release branch (release/X.Y.x) before building Store package."
        exit 1
    }

    # Get version from Directory.Build.props
    [xml]$props = Get-Content "$projectRoot\Directory.Build.props"
    $fileVersion = $props.Project.PropertyGroup.Version

    # Use file version when not specified, or validate provided version
    if (-not $Version) {
        $Version = $fileVersion
        Write-Host "Using version from Directory.Build.props: $Version" -ForegroundColor Cyan
    } elseif ($Version -ne $fileVersion) {
        Write-Error "Version mismatch: Directory.Build.props has $fileVersion but -Version is $Version"
        exit 1
    }

    # Validate version format
    if ($Version -notmatch '^\d+\.\d+\.\d+$') {
        Write-Error "Invalid version format: $Version (expected X.Y.Z)"
        exit 1
    }

    Write-Host "`n=== Building Store Package for ClipSave v$Version ===" -ForegroundColor Green
    Write-Host "Branch: $currentBranch" -ForegroundColor Cyan

    # Verify version in both files
    Write-Host "`n[1/7] Verifying version consistency..." -ForegroundColor Yellow
    & "$projectRoot\scripts\validate-version.ps1" -ProjectRoot $projectRoot -BranchName $currentBranch
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Version validation failed"
        exit 1
    }

    # Restore dependencies
    Write-Host "`n[2/7] Restoring dependencies..." -ForegroundColor Yellow
    dotnet restore ClipSave.slnx
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to restore dependencies"
        exit 1
    }

    # Run dependency/SAST checks
    Write-Host "`n[3/7] Running security checks..." -ForegroundColor Yellow
    & "$projectRoot\scripts\run-security-checks.ps1" -Configuration Release -NoRestore
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Security checks failed"
        exit 1
    }

    # Build app project (avoid DesktopBridge dependency during dotnet build)
    Write-Host "`n[4/7] Building app project..." -ForegroundColor Yellow
    dotnet build src/ClipSave/ClipSave.csproj --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }

    # Run tests
    Write-Host "`n[5/7] Running tests..." -ForegroundColor Yellow
    & "$projectRoot\scripts\run-tests.ps1" -Configuration Release -Verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed"
        exit 1
    }

    # Build Store package
    Write-Host "`n[6/7] Building Store upload package..." -ForegroundColor Yellow
    $outputDir = Join-Path $projectRoot "StorePackage"
    if (Test-Path $outputDir) {
        Remove-Item $outputDir -Recurse -Force
    }

    msbuild "$projectRoot\src\ClipSave.Package\ClipSave.Package.wapproj" `
        /p:Configuration=Release `
        /p:Platform=AnyCPU `
        /p:UapAppxPackageBuildMode=StoreUpload `
        /p:AppxBundle=Always `
        /p:AppxPackageDir="$outputDir\" `
        /p:AppxPackageSigningEnabled=false `
        /verbosity:minimal

    if ($LASTEXITCODE -ne 0) {
        Write-Error "MSIX build failed"
        exit 1
    }

    # Verify output
    Write-Host "`n[7/7] Verifying output..." -ForegroundColor Yellow
    $msixUpload = Get-ChildItem -Path $outputDir -Filter "*.msixupload" -Recurse | Select-Object -First 1
    if (-not $msixUpload) {
        Write-Error "No .msixupload file found in output directory"
        exit 1
    }

    Write-Host "`n✅ Store package built successfully!" -ForegroundColor Green
    Write-Host "`nPackage location:" -ForegroundColor Cyan
    Write-Host "  $($msixUpload.FullName)" -ForegroundColor White
    Write-Host "`nFile size: $([math]::Round($msixUpload.Length / 1MB, 2)) MB" -ForegroundColor Cyan

    Write-Host "`n=== Next Steps ===" -ForegroundColor Green
    Write-Host "1. Go to Partner Center: https://partner.microsoft.com/dashboard"
    Write-Host "2. Create a new submission"
    Write-Host "3. Upload the .msixupload file"
    Write-Host "4. Update release notes and metadata"
    Write-Host "5. Submit for review"

    Write-Host "`nTip: Run .\scripts\store-checklist.ps1 to verify pre-submission requirements" -ForegroundColor Yellow
}
finally {
    Pop-Location
}
