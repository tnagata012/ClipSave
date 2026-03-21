#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build a local MSIX package for Store preflight verification

.DESCRIPTION
    Builds a Store upload package (.msixupload) locally for preflight/debugging.
    Final submission packages must be produced by the Release Finalize workflow
    in Store package mode from a fixed X.Y.Z tag to preserve reproducibility.

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

function Resolve-MSBuildPath {
    $command = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($command -and $command.Source) {
        return $command.Source
    }

    $vswherePath = "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswherePath) {
        $resolved = & $vswherePath `
            -latest `
            -products * `
            -requires Microsoft.Component.MSBuild `
            -find MSBuild\**\Bin\MSBuild.exe 2>$null |
            Select-Object -First 1
        if ($resolved) {
            return $resolved.Trim()
        }
    }

    throw "MSBuild.exe was not found. Install Visual Studio/Build Tools with MSBuild, or run from a Developer PowerShell where msbuild is available."
}

# Get project root
$projectRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $projectRoot "src\ClipSave.Package\Package.appxmanifest"
$manifestBackupPath = $null

Push-Location $projectRoot
try {
    # Validate current branch (Store package must be built from release branch)
    $currentBranch = git branch --show-current 2>$null
    if (-not $currentBranch -or $currentBranch -notmatch '^release/\d+\.\d+$') {
        Write-Error "Current branch is '$currentBranch'. Switch to a release branch (release/X.Y) before building Store package."
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

    $versionMatch = [regex]::Match($Version, '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$')
    $segments = @(
        [int]$versionMatch.Groups['major'].Value,
        [int]$versionMatch.Groups['minor'].Value,
        [int]$versionMatch.Groups['patch'].Value,
        0
    )
    if ($segments | Where-Object { $_ -lt 0 -or $_ -gt 65535 }) {
        Write-Error "MSIX version segments must be within 0..65535. Resolved segments: $($segments -join '.')"
        exit 1
    }

    $shortSha = (git rev-parse --short=7 HEAD 2>$null).Trim()
    if (-not $shortSha -or $shortSha.Length -ne 7) {
        Write-Error "Failed to resolve short SHA from current commit"
        exit 1
    }

    $assemblyVersion = "$($versionMatch.Groups['major'].Value).$($versionMatch.Groups['minor'].Value).0.0"
    $fileVersionValue = "$Version.0"
    $informationalVersion = "$Version+sha.$shortSha"
    $msixVersion = "$Version.0"

    $tagsOnHead = @(
        git tag --points-at HEAD 2>$null |
        Where-Object { $_ -and $_.Trim() -ne "" }
    )
    if ($tagsOnHead -notcontains $Version) {
        Write-Warning "Current HEAD is not tagged '$Version'. Final Store submission must use Release Finalize workflow with version=$Version and build_store_package=true from refs/tags/$Version."
    }

    Write-Host "`n=== Building Store Package for ClipSave v$Version ===" -ForegroundColor Green
    Write-Host "Branch: $currentBranch" -ForegroundColor Cyan
    Write-Host "InformationalVersion: $informationalVersion" -ForegroundColor Cyan
    Write-Warning "This script is for local preflight only. Final submission packages must come from the Release Finalize workflow in Store package mode."
    $msbuildPath = Resolve-MSBuildPath
    Write-Host "MSBuild: $msbuildPath" -ForegroundColor Cyan

    # Verify version in both files
    Write-Host "`nVerifying version consistency..." -ForegroundColor Yellow
    & "$projectRoot\scripts\assert-version-policy.ps1" -ProjectRoot $projectRoot -BranchName $currentBranch
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Version validation failed"
        exit 1
    }

    # Restore dependencies
    Write-Host "`nRestoring dependencies..." -ForegroundColor Yellow
    dotnet restore ClipSave.slnx
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to restore dependencies"
        exit 1
    }

    # Run dependency/SAST checks
    Write-Host "`nRunning security checks..." -ForegroundColor Yellow
    & "$projectRoot\scripts\run-security-checks.ps1" -Configuration Release -NoRestore
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Security checks failed"
        exit 1
    }

    # Build app project (avoid DesktopBridge dependency during dotnet build)
    Write-Host "`nBuilding app project..." -ForegroundColor Yellow
    dotnet build src/ClipSave/ClipSave.csproj --configuration Release --no-restore `
        /p:Version="$Version" `
        /p:AssemblyVersion="$assemblyVersion" `
        /p:FileVersion="$fileVersionValue" `
        /p:InformationalVersion="$informationalVersion"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }

    # Run tests
    Write-Host "`nRunning tests..." -ForegroundColor Yellow
    & "$projectRoot\scripts\run-tests.ps1" -Configuration Release -Verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed"
        exit 1
    }

    # Desktop Bridge packaging restores/publishes the app as win-x86 internally.
    # Keep this even when package platform is AnyCPU, otherwise NETSDK1047 occurs.
    Write-Host "`nRestoring app assets for MSIX runtime..." -ForegroundColor Yellow
    dotnet restore src/ClipSave/ClipSave.csproj --runtime win-x86
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to restore app assets for MSIX runtime"
        exit 1
    }

    Write-Host "`nPreparing Store Package.appxmanifest..." -ForegroundColor Yellow
    $manifestBackupPath = Join-Path ([System.IO.Path]::GetTempPath()) ("ClipSave.Package.appxmanifest.{0}.bak" -f [guid]::NewGuid())
    Copy-Item $manifestPath $manifestBackupPath -Force
    & "$projectRoot\scripts\set-package-manifest.ps1" -ProjectRoot $projectRoot -Profile store -Version $msixVersion
    Write-Host "Prepared Package.appxmanifest for Store profile" -ForegroundColor Cyan

    # Build Store package
    Write-Host "`nBuilding Store upload package..." -ForegroundColor Yellow
    $outputDir = Join-Path $projectRoot "StorePackage"
    if (Test-Path $outputDir) {
        Remove-Item $outputDir -Recurse -Force
    }

    & $msbuildPath "$projectRoot\src\ClipSave.Package\ClipSave.Package.wapproj" `
        /p:Configuration=Release `
        /p:Platform=AnyCPU `
        /p:Version="$Version" `
        /p:AssemblyVersion="$assemblyVersion" `
        /p:FileVersion="$fileVersionValue" `
        /p:InformationalVersion="$informationalVersion" `
        /p:UapAppxPackageBuildMode=StoreUpload `
        /p:AppxBundle=Always `
        /p:AppxPackageDir="$outputDir\" `
        /p:AppxPackageVersion=$msixVersion `
        /p:AppxBundleManifestVersion=$msixVersion `
        /p:AppxManifestIdentityVersion=$msixVersion `
        /p:AppxPackageSigningEnabled=false `
        /verbosity:minimal

    if ($LASTEXITCODE -ne 0) {
        Write-Error "MSIX build failed"
        exit 1
    }

    # Verify output
    Write-Host "`nVerifying output..." -ForegroundColor Yellow
    $uploads = @(Get-ChildItem -Path $outputDir -Filter "*.msixupload" -Recurse -File)
    if ($uploads.Count -eq 0) {
        Write-Error "No .msixupload file found in output directory"
        exit 1
    }
    if ($uploads.Count -ne 1) {
        $actual = ($uploads | Select-Object -ExpandProperty FullName) -join ", "
        Write-Error "Expected exactly one .msixupload file, found $($uploads.Count): $actual"
        exit 1
    }
    $msixUpload = $uploads[0]
    $expectedPattern = "_$([regex]::Escape($msixVersion))_"
    if ($msixUpload.Name -notmatch $expectedPattern) {
        Write-Error "Store upload filename does not contain expected version '$msixVersion'. Found: $($msixUpload.Name)"
        exit 1
    }

    Write-Host "`n✅ Store package built successfully!" -ForegroundColor Green
    Write-Host "`nPackage location:" -ForegroundColor Cyan
    Write-Host "  $($msixUpload.FullName)" -ForegroundColor White
    Write-Host "`nFile size: $([math]::Round($msixUpload.Length / 1MB, 2)) MB" -ForegroundColor Cyan

    Write-Host "`n=== Next Steps ===" -ForegroundColor Green
    Write-Host "1. Use this local package only for preflight/debugging"
    Write-Host "2. For final submission, run Release Finalize workflow with version=$Version and build_store_package=true"
    Write-Host "3. Confirm workflow summary shows refs/tags/$Version, Store Package Mode=built, and the expected commit SHA"
    Write-Host "4. Upload the workflow artifact .msixupload in Partner Center"

    Write-Host "`nTip: Run .\scripts\store-checklist.ps1 to verify pre-submission requirements" -ForegroundColor Yellow
}
finally {
    if ($manifestBackupPath -and (Test-Path $manifestBackupPath)) {
        Copy-Item $manifestBackupPath $manifestPath -Force
        Remove-Item $manifestBackupPath -Force
    }
    Pop-Location
}
