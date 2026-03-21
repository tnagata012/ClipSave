#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Verify and install an unsigned ClipSave artifact.

.DESCRIPTION
    This helper script wraps the recommended unsigned artifact workflow:
    1) Optional checksum + attestation verification.
    2) Admin privilege check for executable-content packages.
    3) Optional removal of an existing Preview package.
    4) Add-AppxPackage -AllowUnsigned.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$BundlePath,

    [string]$ChecksumPath = $null,

    [ValidateSet("dev", "rc", "archive")]
    [string]$Channel = "dev",

    [string]$Repo = "tnagata012/ClipSave",

    [string]$SourceRef = $null,

    [switch]$SkipVerify,

    [switch]$RemoveExisting
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Host "`n[ERROR] $Message" -ForegroundColor Red
    exit 1
}

function Get-BundleIdentity([string]$Path) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $archive.GetEntry("AppxMetadata/AppxBundleManifest.xml")
        if (-not $entry) {
            throw "AppxBundleManifest.xml not found in bundle: $Path"
        }

        $stream = $entry.Open()
        try {
            $reader = New-Object System.IO.StreamReader($stream)
            try {
                [xml]$manifest = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }

        $ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
        $ns.AddNamespace("bundle", "http://schemas.microsoft.com/appx/2013/bundle")
        $identity = $manifest.SelectSingleNode("/bundle:Bundle/bundle:Identity", $ns)
        if (-not $identity) {
            throw "Identity element not found in bundle manifest: $Path"
        }

        return [pscustomobject]@{
            Name = [string]$identity.Name
            Publisher = [string]$identity.Publisher
            Version = [version]([string]$identity.Version)
        }
    }
    finally {
        $archive.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $BundlePath)) {
    Fail "Bundle file not found: $BundlePath"
}

$bundleItem = Get-Item -LiteralPath $BundlePath
$bundlePathResolved = $bundleItem.FullName

if (-not $SkipVerify) {
    if (-not $ChecksumPath) {
        $candidateChecksumPath = Join-Path $bundleItem.Directory.FullName "SHA256SUMS.txt"
        if (-not (Test-Path -LiteralPath $candidateChecksumPath)) {
            Fail "Checksum file not found. Pass -ChecksumPath or use -SkipVerify. Expected: $candidateChecksumPath"
        }
        $ChecksumPath = $candidateChecksumPath
    }

    $verifyArgs = @(
        "-BundlePath", $bundlePathResolved,
        "-ChecksumPath", (Resolve-Path -LiteralPath $ChecksumPath).Path,
        "-Channel", $Channel,
        "-Repo", $Repo
    )
    if ($SourceRef) {
        $verifyArgs += @("-SourceRef", $SourceRef)
    }

    & (Join-Path $PSScriptRoot "verify-artifact.ps1") @verifyArgs
    if ($LASTEXITCODE -ne 0) {
        Fail "Artifact verification failed."
    }
}

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator
)
if (-not $isAdmin) {
    Fail "Run this script from an elevated PowerShell window. Unsigned ClipSave packages contain executable activation and require administrator privilege."
}

$identity = Get-BundleIdentity -Path $bundlePathResolved
Write-Host "Resolved bundle identity:" -ForegroundColor Cyan
Write-Host "  Name      : $($identity.Name)" -ForegroundColor White
Write-Host "  Publisher : $($identity.Publisher)" -ForegroundColor White
Write-Host "  Version   : $($identity.Version)" -ForegroundColor White

$existingPackages = @(Get-AppxPackage | Where-Object { $_.Name -eq $identity.Name })
if ($existingPackages.Count -gt 0) {
    $existingSummary = ($existingPackages | ForEach-Object {
        "{0} ({1})" -f $_.PackageFullName, $_.Version
    }) -join ", "

    if (-not $RemoveExisting) {
        $publisherMismatches = @($existingPackages | Where-Object { $_.Publisher -ne $identity.Publisher })
        $sameOrNewerPackages = @($existingPackages | Where-Object { [version]$_.Version -ge $identity.Version })

        if ($publisherMismatches.Count -gt 0) {
            Fail "Existing package identity does not match the bundle publisher: $existingSummary`nRe-run with -RemoveExisting to uninstall it before installing the new bundle."
        }

        if ($sameOrNewerPackages.Count -gt 0) {
            Fail "Existing package version is the same or newer than the bundle: $existingSummary`nRe-run with -RemoveExisting to force replacement."
        }

        Write-Host "Existing older package found. Continuing with in-place update: $existingSummary" -ForegroundColor Cyan
    } else {
        Write-Host "Removing existing package(s): $existingSummary" -ForegroundColor Yellow
        foreach ($pkg in $existingPackages) {
            Remove-AppxPackage -Package $pkg.PackageFullName
        }
    }
}

Add-AppxPackage -Path $bundlePathResolved -AllowUnsigned
Write-Host "[OK] Installed unsigned package: $($identity.Name) $($identity.Version)" -ForegroundColor Green
