#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Verify SHA256 checksum and GitHub Artifact Attestation for a ClipSave bundle.

.DESCRIPTION
    This script enforces two checks for an unsigned Dev/Release artifact:
    1) SHA256 checksum validation using SHA256SUMS.txt.
    2) Provenance validation using gh attestation verify.

.PARAMETER BundlePath
    Path to the .msixbundle file to verify.

.PARAMETER ChecksumPath
    Path to SHA256SUMS.txt (default: .\SHA256SUMS.txt).

.PARAMETER Channel
    Artifact channel. Determines expected signer workflow:
    - dev     -> .github/workflows/dev-build.yml
    - release -> .github/workflows/release-build.yml

.PARAMETER Repo
    Repository in owner/name format (default: tnagata012/ClipSave).

.PARAMETER SourceRef
    Optional exact git ref for stronger verification (for example refs/heads/main).
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$BundlePath,

    [string]$ChecksumPath = ".\SHA256SUMS.txt",

    [ValidateSet("dev", "release")]
    [string]$Channel = "dev",

    [string]$Repo = "tnagata012/ClipSave",

    [string]$SourceRef = $null
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Host "`n[ERROR] $Message" -ForegroundColor Red
    exit 1
}

function Resolve-WorkflowPath([string]$Value) {
    if ($Value -eq "dev") {
        return ".github/workflows/dev-build.yml"
    }
    return ".github/workflows/release-build.yml"
}

if (-not (Test-Path $BundlePath)) {
    Fail "Bundle file not found: $BundlePath"
}
if (-not (Test-Path $ChecksumPath)) {
    Fail "Checksum file not found: $ChecksumPath"
}
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Fail "'gh' command not found. Install GitHub CLI first."
}

$bundle = Get-Item -Path $BundlePath
$bundleName = $bundle.Name
$bundlePathNormalized = $bundle.FullName.Replace('\', '/')

$lines = @(Get-Content -Path $ChecksumPath | Where-Object { $_ -and $_.Trim() -ne "" })
if ($lines.Count -eq 0) {
    Fail "Checksum file is empty: $ChecksumPath"
}

$entries = @()
foreach ($line in $lines) {
    $trimmed = $line.Trim()
    if ($trimmed.StartsWith("#")) {
        continue
    }

    $match = [regex]::Match($trimmed, '^(?<hash>[A-Fa-f0-9]{64})\s+\*?(?<path>.+)$')
    if (-not $match.Success) {
        continue
    }

    $entryPath = $match.Groups["path"].Value.Trim().Replace('\', '/')
    $entryName = [System.IO.Path]::GetFileName($entryPath)

    $entries += [pscustomobject]@{
        Hash = $match.Groups["hash"].Value.ToLowerInvariant()
        Path = $entryPath
        Name = $entryName
    }
}

if ($entries.Count -eq 0) {
    Fail "No valid checksum entries found in: $ChecksumPath"
}

$matchingEntries = @($entries | Where-Object {
    $_.Path -eq $bundleName -or
    $_.Path -eq $bundlePathNormalized -or
    $_.Name -eq $bundleName
})

$selected = $null
if ($matchingEntries.Count -eq 1) {
    $selected = $matchingEntries[0]
} elseif ($matchingEntries.Count -gt 1) {
    Fail "Multiple checksum entries matched bundle '$bundleName' in $ChecksumPath"
} elseif ($entries.Count -eq 1) {
    $selected = $entries[0]
} else {
    $known = ($entries | Select-Object -ExpandProperty Path) -join ", "
    Fail "No checksum entry matched '$bundleName'. Entries: $known"
}

$actualHash = (Get-FileHash -Path $bundle.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $selected.Hash) {
    Fail "SHA256 mismatch. Expected=$($selected.Hash), Actual=$actualHash"
}

Write-Host "[OK] SHA256 verified: $bundleName" -ForegroundColor Green

$workflowPath = Resolve-WorkflowPath -Value $Channel
$signerWorkflow = "$Repo/$workflowPath"

$verifyArgs = @(
    "attestation", "verify", $bundle.FullName,
    "--repo", $Repo,
    "--signer-workflow", $signerWorkflow,
    "--predicate-type", "https://slsa.dev/provenance/v1",
    "--deny-self-hosted-runners"
)

if ($SourceRef -and $SourceRef.Trim() -ne "") {
    $verifyArgs += @("--source-ref", $SourceRef.Trim())
}

Write-Host "Running attestation verification..." -ForegroundColor Cyan
gh @verifyArgs
if ($LASTEXITCODE -ne 0) {
    Fail "Attestation verification failed."
}

Write-Host "[OK] Attestation verified via $workflowPath" -ForegroundColor Green
exit 0
