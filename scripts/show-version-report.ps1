#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Show current version report

.DESCRIPTION
    Displays version information from:
    - Installed MSIX package (if any)
    - Project files (Directory.Build.props, Package.appxmanifest)
    - GitHub distribution channels (dev/rc tags + archive artifacts)

.EXAMPLE
    .\show-version-report.ps1
#>

$ErrorActionPreference = "SilentlyContinue"

# Get project root
$projectRoot = Split-Path -Parent $PSScriptRoot

Push-Location $projectRoot

Write-Host "=== ClipSave Version Information ===" -ForegroundColor Cyan
Write-Host ""

# Section 1: Project Files
Write-Host "[Project Files]" -ForegroundColor Yellow

$propsPath = Join-Path $projectRoot "Directory.Build.props"
$coreVersion = $null

if (Test-Path $propsPath) {
    [xml]$props = Get-Content $propsPath
    $propsVersion = $props.Project.PropertyGroup.Version
    Write-Host "  Directory.Build.props : $propsVersion" -ForegroundColor White

    $versionMatch = [regex]::Match($propsVersion, '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$')
    if ($versionMatch.Success) {
        $coreVersion = "$($versionMatch.Groups['major'].Value).$($versionMatch.Groups['minor'].Value).$($versionMatch.Groups['patch'].Value)"
        Write-Host "  SemVer mode            : stable (X.Y.Z)" -ForegroundColor Green
    } else {
        Write-Host "  SemVer mode            : invalid format" -ForegroundColor Red
    }
} else {
    Write-Host "  Directory.Build.props : Not found" -ForegroundColor Red
}

$manifestPath = Join-Path $projectRoot "src/ClipSave.Package/Package.appxmanifest"
if (Test-Path $manifestPath) {
    [xml]$manifest = Get-Content $manifestPath
    $manifestIdentityName = $manifest.Package.Identity.Name
    $manifestIdentityPublisher = $manifest.Package.Identity.Publisher
    $manifestVersion = $manifest.Package.Identity.Version
    Write-Host "  Package identity      : $manifestIdentityName" -ForegroundColor White
    Write-Host "  Package publisher     : $manifestIdentityPublisher" -ForegroundColor White
    Write-Host "  Package.appxmanifest  : $manifestVersion" -ForegroundColor White
} else {
    Write-Host "  Package.appxmanifest  : Not found" -ForegroundColor Red
}

# Version consistency check
if ($propsVersion -and $manifestVersion) {
    if ($coreVersion) {
        $expectedManifest = "$coreVersion.0"
    } else {
        $expectedManifest = "$propsVersion.0"
    }

    if ($manifestVersion -eq $expectedManifest) {
        Write-Host "  Status                : [OK] Consistent" -ForegroundColor Green
    } else {
        Write-Host "  Status                : [ERROR] Mismatch (expected $expectedManifest)" -ForegroundColor Red
    }
}

Write-Host ""

# Section 2: Installed Package
Write-Host "[Installed Package]" -ForegroundColor Yellow

$packageProfiles = @(
    @{
        Label = "Preview"
        Name = "ClipSave.Preview"
    },
    @{
        Label = "Store"
        Name = "tnagata012.ClipSave"
    }
)

$installedPackages = @()
foreach ($profile in $packageProfiles) {
    $package = @(Get-AppxPackage -Name $profile.Name 2>$null | Sort-Object Version -Descending) | Select-Object -First 1
    if (-not $package) {
        continue
    }

    $packageType = "Store"
    if ($profile.Label -eq "Preview") {
        $versionParts = $package.Version.Split('.')
        $revision = [int]$versionParts[3]
        if ($revision -eq 0) {
            $packageType = "Preview (RC/Archive)"
        } else {
            $packageType = "Preview Dev (Build #$revision)"
        }
    }

    $installedPackages += [pscustomobject]@{
        Label = $profile.Label
        Name = $package.Name
        Version = $package.Version
        Type = $packageType
    }
}

if ($installedPackages.Count -eq 0) {
    Write-Host "  Not installed" -ForegroundColor Gray
} else {
    foreach ($package in $installedPackages) {
        Write-Host "  [$($package.Label)]" -ForegroundColor White
        Write-Host "    Name    : $($package.Name)" -ForegroundColor White
        Write-Host "    Version : $($package.Version)" -ForegroundColor White
        Write-Host "    Type    : $($package.Type)" -ForegroundColor White
    }
}

Write-Host ""

# Capture once for both GitHub channel resolution and branch display.
$currentBranch = git branch --show-current 2>$null

# Section 3: GitHub Distribution
Write-Host "[GitHub Distribution]" -ForegroundColor Yellow

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

$ghAvailable = Get-Command gh -ErrorAction SilentlyContinue

if ($ghAvailable) {
    $repo = Resolve-GitHubRepository
    if (-not $repo) {
        Write-Host "  Repository   : unresolved (remote.origin.url is not a GitHub URL)" -ForegroundColor Gray
    } else {
        # Dev channel (prerelease tag)
        $devRelease = gh release view dev-latest --repo $repo --json tagName,publishedAt 2>$null | ConvertFrom-Json
        if ($devRelease) {
            $publishedDate = [DateTime]::Parse($devRelease.publishedAt).ToString("yyyy-MM-dd HH:mm")
            Write-Host "  Dev Channel  : $($devRelease.tagName) ($publishedDate)" -ForegroundColor Yellow
        } else {
            Write-Host "  Dev Channel  : dev-latest not found" -ForegroundColor Gray
        }

        # RC channel (branch-scoped floating tag: rc-X.Y-latest)
        $resolvedReleaseTag = $null
        $releaseBranchMatch = if ($currentBranch) { [regex]::Match($currentBranch, '^release/(?<major>\d+)\.(?<minor>\d+)$') } else { $null }
        if ($releaseBranchMatch -and $releaseBranchMatch.Success) {
            $releaseTag = "rc-$($releaseBranchMatch.Groups['major'].Value).$($releaseBranchMatch.Groups['minor'].Value)-latest"
            $releaseChannel = gh release view $releaseTag --repo $repo --json tagName,publishedAt 2>$null | ConvertFrom-Json
            if ($releaseChannel) {
                $publishedDate = [DateTime]::Parse($releaseChannel.publishedAt).ToString("yyyy-MM-dd HH:mm")
                Write-Host "  RC Tag       : $($releaseChannel.tagName) ($publishedDate)" -ForegroundColor Green
                $resolvedReleaseTag = $releaseChannel.tagName
            } else {
                Write-Host "  RC Tag       : $releaseTag not found" -ForegroundColor Gray
            }
        } else {
            $latestReleaseLineTag = gh api --paginate --slurp "repos/$repo/releases?per_page=100" `
                --jq '([.[].[] | select(.tag_name | test("^rc-[0-9]+\\.[0-9]+-latest$"))] | sort_by(.published_at) | reverse | .[0]? | [.tag_name, .published_at] | @tsv) // empty' 2>$null |
                Select-Object -First 1

            if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($latestReleaseLineTag)) {
                $parts = $latestReleaseLineTag -split "`t", 2
                $tagName = $parts[0]
                $publishedAt = if ($parts.Count -ge 2) { [DateTime]::Parse($parts[1]).ToString("yyyy-MM-dd HH:mm") } else { "unknown" }
                Write-Host "  RC Tag       : $tagName ($publishedAt)" -ForegroundColor Green
                $resolvedReleaseTag = $tagName
            } else {
                Write-Host "  RC Tag       : rc-X.Y-latest not found" -ForegroundColor Gray
            }
        }

        # RC base (Actions artifacts)
        $latestReleaseArtifactLine = gh api --paginate --slurp "repos/$repo/actions/artifacts?per_page=100" `
            --jq '([.[].artifacts[]? | select((.expired == false) and (.name | startswith("rc-package-")))] | sort_by(.created_at) | reverse | .[0]? | [.name, .created_at] | @tsv) // empty' 2>$null |
            Select-Object -First 1

        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($latestReleaseArtifactLine)) {
            $parts = $latestReleaseArtifactLine -split "`t", 2
            $artifactName = $parts[0]
            $createdAt = if ($parts.Count -ge 2) { [DateTime]::Parse($parts[1]).ToString("yyyy-MM-dd HH:mm") } else { "unknown" }
            Write-Host "  RC Base      : $artifactName ($createdAt)" -ForegroundColor Green
        } else {
            Write-Host "  RC Base      : rc-package-* artifact not found" -ForegroundColor Gray
        }

        if ($coreVersion) {
            Write-Host "  Store Hint   : version=$coreVersion (workflow resolves refs/tags/$coreVersion)" -ForegroundColor Cyan
            if ($resolvedReleaseTag) {
                Write-Host "                 Candidate channel tag: $resolvedReleaseTag (create finalized tag X.Y.Z from adopted commit)" -ForegroundColor Gray
            }
        }
    }
} else {
    Write-Host "  GitHub CLI not available (install with: winget install GitHub.cli)" -ForegroundColor Gray
}

Write-Host ""

# Section 4: Current Branch
Write-Host "[Git Branch]" -ForegroundColor Yellow
if ($currentBranch) {
    Write-Host "  Current : $currentBranch" -ForegroundColor White

    if ($currentBranch -match '^release/') {
        Write-Host "  Type    : Release branch" -ForegroundColor Green
    } elseif ($currentBranch -eq 'main') {
        Write-Host "  Type    : Main branch (trunk)" -ForegroundColor Cyan
    } else {
        Write-Host "  Type    : Working branch" -ForegroundColor Gray
    }
} else {
    Write-Host "  Not in a git repository" -ForegroundColor Gray
}

Pop-Location
