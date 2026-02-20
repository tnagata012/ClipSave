# Assert version policy consistency between files.
# Usage: .\assert-version-policy.ps1 [-BranchName <string>] [-ProjectRoot <path>]
#
# Examples:
#   .\assert-version-policy.ps1
#   .\assert-version-policy.ps1 -BranchName "main"
#   .\assert-version-policy.ps1 -BranchName "release/1.2"
#   .\assert-version-policy.ps1 -ProjectRoot "C:\path\to\repo"
#
# Rules:
# - Directory.Build.props: always X.Y.Z
# - release/X.Y: X.Y.Z, and X.Y must match branch name
# - Package.appxmanifest: always X.Y.Z.0
# - CI-injected assembly metadata (InformationalVersion/FileVersion) is out of scope

param(
    [string]$BranchName = $null,
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$MainBranchName = "main",
    [string]$ReleaseBranchPattern = '^release/(?<major>\d+)\.(?<minor>\d+)$'
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Host "`n[ERROR] $Message" -ForegroundColor Red
    exit 1
}

Write-Host "Validating version consistency..." -ForegroundColor Cyan

$propsPath = Join-Path $ProjectRoot "Directory.Build.props"
$manifestPath = Join-Path $ProjectRoot "src/ClipSave.Package/Package.appxmanifest"

if (-not (Test-Path $propsPath)) {
    Fail "Directory.Build.props not found: $propsPath"
}
if (-not (Test-Path $manifestPath)) {
    Fail "Package.appxmanifest not found: $manifestPath"
}

# Read Directory.Build.props
[xml]$props = Get-Content $propsPath
$version = $props.Project.PropertyGroup.Version
if ($version) {
    $version = $version.Trim()
}

if (-not $version) {
    Fail "Directory.Build.props version is empty. File: $propsPath"
}

Write-Host "`nDirectory.Build.props: $version" -ForegroundColor White

$semverPattern = '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$'
$versionMatch = [regex]::Match($version, $semverPattern)
if (-not $versionMatch.Success) {
    Fail "Invalid version format in Directory.Build.props. Expected X.Y.Z. Actual: $version"
}

$major = [int]$versionMatch.Groups['major'].Value
$minor = [int]$versionMatch.Groups['minor'].Value
$patch = [int]$versionMatch.Groups['patch'].Value
$coreVersion = "$major.$minor.$patch"

# Read Package.appxmanifest
[xml]$manifest = Get-Content $manifestPath
$manifestVersion = $manifest.Package.Identity.Version
if ($manifestVersion) {
    $manifestVersion = $manifestVersion.Trim()
}

Write-Host "Package.appxmanifest : $manifestVersion" -ForegroundColor White

if (-not $manifestVersion -or $manifestVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    Fail "Invalid Package.appxmanifest version format. Expected X.Y.Z.0. Actual: $manifestVersion"
}

$manifestVersionMatch = [regex]::Match($manifestVersion, '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)\.(?<revision>\d+)$')
$manifestRevision = [int]$manifestVersionMatch.Groups['revision'].Value
if ($manifestRevision -ne 0) {
    Fail "Package.appxmanifest revision must be 0 in repository. Actual: $manifestVersion"
}

# Validate file consistency
$expectedManifestVersion = "$coreVersion.0"
if ($manifestVersion -ne $expectedManifestVersion) {
    Fail "Version mismatch. Expected Package.appxmanifest=$expectedManifestVersion, Actual=$manifestVersion"
}

Write-Host "[OK] File version consistency check passed" -ForegroundColor Green

# Validate branch rule if provided
if ($BranchName) {
    Write-Host "`nBranch name: $BranchName" -ForegroundColor White

    if ($BranchName -eq $MainBranchName) {
        Write-Host "[OK] $MainBranchName branch validation passed" -ForegroundColor Green
    } else {
        $releaseBranchMatch = [regex]::Match($BranchName, $ReleaseBranchPattern)
        if ($releaseBranchMatch.Success) {
            $branchMajor = [int]$releaseBranchMatch.Groups['major'].Value
            $branchMinor = [int]$releaseBranchMatch.Groups['minor'].Value
            if ($branchMajor -ne $major -or $branchMinor -ne $minor) {
                Fail "release branch name and file version mismatch. Branch=release/$branchMajor.$branchMinor, File=$coreVersion"
            }

            Write-Host "[OK] release branch validation passed" -ForegroundColor Green
        } else {
            Write-Host "[WARN] Branch does not match main/release rules. Skipping branch-specific checks." -ForegroundColor Yellow
        }
    }
}

Write-Host "`n[OK] All validation checks passed" -ForegroundColor Green
exit 0
