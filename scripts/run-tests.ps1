#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run all test projects under tests/

.DESCRIPTION
    Executes dotnet test for each *.csproj in tests/ recursively.
    Use this script instead of testing the full solution to avoid
    package project/tooling dependencies unrelated to test execution.

.PARAMETER Configuration
    Build configuration (default: Release)

.PARAMETER NoBuild
    Pass --no-build to dotnet test

.PARAMETER Verbosity
    dotnet test verbosity (default: normal)

.PARAMETER EmitTrx
    Emit TRX files for each test project.

.PARAMETER ResultsDirectory
    Output directory for TRX files. Used when EmitTrx is enabled.

.EXAMPLE
    .\run-tests.ps1

.EXAMPLE
    .\run-tests.ps1 -Configuration Debug -NoBuild -Verbosity quiet

.EXAMPLE
    .\run-tests.ps1 -Configuration Debug -EmitTrx -ResultsDirectory .\TestResults
#>

param(
    [string]$Configuration = "Release",
    [switch]$NoBuild = $false,
    [ValidateSet("quiet", "minimal", "normal", "detailed", "diagnostic")]
    [string]$Verbosity = "normal",
    [switch]$EmitTrx = $false,
    [string]$ResultsDirectory = ""
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$testsRoot = Join-Path $projectRoot "tests"

if (-not (Test-Path $testsRoot)) {
    Write-Error "Tests directory not found: $testsRoot"
    exit 1
}

Push-Location $projectRoot
try {
    $resolvedResultsDirectory = $null
    if ($EmitTrx) {
        if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
            $resolvedResultsDirectory = Join-Path $projectRoot "TestResults"
        }
        elseif ([System.IO.Path]::IsPathRooted($ResultsDirectory)) {
            $resolvedResultsDirectory = $ResultsDirectory
        }
        else {
            $resolvedResultsDirectory = Join-Path $projectRoot $ResultsDirectory
        }

        New-Item -Path $resolvedResultsDirectory -ItemType Directory -Force | Out-Null
        Write-Host "TRX output directory: $resolvedResultsDirectory" -ForegroundColor Gray
    }

    $allProjects = Get-ChildItem -Path $testsRoot -Recurse -Filter *.csproj | Sort-Object FullName
    if (-not $allProjects -or $allProjects.Count -eq 0) {
        Write-Error "No .csproj files found under: $testsRoot"
        exit 1
    }

    $testProjects = @()
    foreach ($project in $allProjects) {
        $isTestProject = $false

        try {
            [xml]$projectXml = Get-Content $project.FullName

            foreach ($propertyGroup in $projectXml.Project.PropertyGroup) {
                if ($null -ne $propertyGroup.IsTestProject -and $propertyGroup.IsTestProject.Trim().ToLowerInvariant() -eq "true") {
                    $isTestProject = $true
                    break
                }
            }

            if (-not $isTestProject) {
                foreach ($itemGroup in $projectXml.Project.ItemGroup) {
                    foreach ($packageReference in $itemGroup.PackageReference) {
                        if ($packageReference.Include -eq "Microsoft.NET.Test.Sdk") {
                            $isTestProject = $true
                            break
                        }
                    }
                    if ($isTestProject) {
                        break
                    }
                }
            }
        } catch {
            # Ignore parse errors and fallback to naming convention.
        }

        if (-not $isTestProject -and $project.BaseName -match '(?i)(^|[.\-_])tests?$') {
            $isTestProject = $true
        }

        if ($isTestProject) {
            $testProjects += $project
        }
    }

    if ($testProjects.Count -eq 0) {
        Write-Error "No test projects detected under: $testsRoot"
        exit 1
    }

    Write-Host "Running tests for $($testProjects.Count) project(s)..." -ForegroundColor Cyan
    foreach ($project in $testProjects) {
        Write-Host "  -> $($project.FullName)" -ForegroundColor Gray

        $args = @(
            "test",
            $project.FullName,
            "--configuration", $Configuration,
            "--verbosity", $Verbosity
        )
        if ($NoBuild) {
            $args += "--no-build"
        }
        if ($EmitTrx) {
            $trxName = "$($project.BaseName).trx"
            $args += "--logger"
            $args += "trx;LogFileName=$trxName"
            $args += "--results-directory"
            $args += $resolvedResultsDirectory
        }

        dotnet @args
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Test failed: $($project.FullName)"
            exit $LASTEXITCODE
        }
    }

    Write-Host "All test projects passed." -ForegroundColor Green
}
finally {
    Pop-Location
}
