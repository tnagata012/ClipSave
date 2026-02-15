#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run dependency vulnerability checks and SAST.

.DESCRIPTION
    Executes two checks:
    1) Vulnerability scan via `dotnet list package --vulnerable --include-transitive`.
    2) Roslyn-based SAST for app projects under src/ with security rules enabled.

.PARAMETER Configuration
    Build configuration used for SAST build (default: Release).

.PARAMETER NoRestore
    Pass --no-restore to SAST build.
#>

param(
    [string]$Configuration = "Release",
    [switch]$NoRestore = $false
)

$ErrorActionPreference = "Stop"

function Get-VulnerabilityCount {
    param(
        [object]$Framework
    )

    $count = 0

    foreach ($package in @($Framework.topLevelPackages)) {
        if ($null -ne $package.vulnerabilities) {
            $count += @($package.vulnerabilities).Count
        }
    }

    foreach ($package in @($Framework.transitivePackages)) {
        if ($null -ne $package.vulnerabilities) {
            $count += @($package.vulnerabilities).Count
        }
    }

    return $count
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$srcRoot = Join-Path $projectRoot "src"

Push-Location $projectRoot
try {
    $projects = Get-ChildItem -Path $projectRoot -Recurse -Filter *.csproj |
        Sort-Object FullName

    if (-not $projects -or $projects.Count -eq 0) {
        Write-Error "No .csproj files found under: $projectRoot"
        exit 1
    }

    $totalVulnerabilities = 0

    Write-Host "Running dependency vulnerability scan..." -ForegroundColor Cyan
    foreach ($project in $projects) {
        Write-Host "  -> $($project.FullName)" -ForegroundColor Gray

        $listArgs = @(
            "list",
            $project.FullName,
            "package",
            "--vulnerable",
            "--include-transitive",
            "--format", "json"
        )
        if ($NoRestore) {
            $listArgs += "--no-restore"
        }

        $json = dotnet @listArgs | Out-String
        if ($LASTEXITCODE -ne 0) {
            Write-Error "dotnet list package failed: $($project.FullName)"
            exit $LASTEXITCODE
        }

        $scanResult = $json | ConvertFrom-Json -Depth 100
        $projectVulnerabilities = 0

        foreach ($scanProject in @($scanResult.projects)) {
            foreach ($framework in @($scanProject.frameworks)) {
                $projectVulnerabilities += Get-VulnerabilityCount -Framework $framework
            }
        }

        if ($projectVulnerabilities -gt 0) {
            $totalVulnerabilities += $projectVulnerabilities
            Write-Host "     Vulnerabilities detected: $projectVulnerabilities" -ForegroundColor Red
        }
    }

    if ($totalVulnerabilities -gt 0) {
        Write-Error "Dependency vulnerability scan failed. Total vulnerabilities: $totalVulnerabilities"
        exit 1
    }

    Write-Host "Dependency vulnerability scan passed." -ForegroundColor Green

    if (-not (Test-Path $srcRoot)) {
        Write-Error "Source directory not found: $srcRoot"
        exit 1
    }

    $appProjects = Get-ChildItem -Path $srcRoot -Recurse -Filter *.csproj |
        Sort-Object FullName

    if (-not $appProjects -or $appProjects.Count -eq 0) {
        Write-Error "No app .csproj files found under: $srcRoot"
        exit 1
    }

    Write-Host "Running SAST (Roslyn security analyzers)..." -ForegroundColor Cyan
    foreach ($project in $appProjects) {
        Write-Host "  -> $($project.FullName)" -ForegroundColor Gray

        $args = @(
            "build",
            $project.FullName,
            "--configuration", $Configuration,
            "-p:EnableNETAnalyzers=true",
            "-p:AnalysisLevel=latest",
            "-p:AnalysisMode=None",
            "-p:AnalysisModeSecurity=All",
            "-p:TreatWarningsAsErrors=true"
        )

        if ($NoRestore) {
            $args += "--no-restore"
        }

        dotnet @args
        if ($LASTEXITCODE -ne 0) {
            Write-Error "SAST build failed: $($project.FullName)"
            exit $LASTEXITCODE
        }
    }

    Write-Host "SAST checks passed." -ForegroundColor Green
}
finally {
    Pop-Location
}
