#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Measure download statistics for GitHub releases

.DESCRIPTION
    Fetches release information and displays download counts.
    Automatically detects repository from git remote.

.PARAMETER IncludePrerelease
    Include prerelease versions in the output (default: true)

.EXAMPLE
    .\measure-downloads.ps1
    .\measure-downloads.ps1 -IncludePrerelease:$false
#>

param(
    [bool]$IncludePrerelease = $true
)

$ErrorActionPreference = "Stop"

# Auto-detect repository from git remote
function Get-RepoInfo {
    $remoteUrl = git remote get-url origin 2>$null
    if (-not $remoteUrl) {
        Write-Error "Not a git repository or no origin remote configured"
        exit 1
    }

    if ($remoteUrl -match "github\.com[:/](?<owner>[^/]+)/(?<repo>[^/.]+)") {
        return @{
            Owner = $matches['owner']
            Repo = $matches['repo']
        }
    }

    Write-Error "Could not parse GitHub repository from remote URL: $remoteUrl"
    exit 1
}

$repo = Get-RepoInfo
$Owner = $repo.Owner
$Repo = $repo.Repo

Write-Host "=== Download Statistics ===" -ForegroundColor Cyan
Write-Host "Repository: $Owner/$Repo" -ForegroundColor White
Write-Host ""

try {
    $releases = gh api "repos/$Owner/$Repo/releases" | ConvertFrom-Json

    if ($releases.Count -eq 0) {
        Write-Host "No releases found" -ForegroundColor Yellow
        exit 0
    }

    # Filter prereleases if needed
    $displayReleases = if ($IncludePrerelease) {
        $releases
    } else {
        $releases | Where-Object { -not $_.prerelease }
    }

    Write-Host ("{0,-25} {1,12} {2,12}" -f "Version", "Downloads", "Published") -ForegroundColor White
    Write-Host ("-" * 52) -ForegroundColor Gray

    $totalDownloads = 0
    $releaseDownloads = 0
    $prereleaseDownloads = 0

    foreach ($release in $displayReleases) {
        $downloads = ($release.assets | Measure-Object -Property download_count -Sum).Sum
        if (-not $downloads) { $downloads = 0 }

        $totalDownloads += $downloads
        if ($release.prerelease) {
            $prereleaseDownloads += $downloads
        } else {
            $releaseDownloads += $downloads
        }

        $publishedDate = [DateTime]::Parse($release.published_at).ToString("yyyy-MM-dd")
        $version = $release.tag_name

        $label = if ($release.prerelease) { " (dev)" } else { "" }
        $displayVersion = "$version$label"

        $color = if ($release.prerelease) { "Gray" }
                 elseif ($downloads -gt 100) { "Green" }
                 elseif ($downloads -gt 10) { "Yellow" }
                 else { "White" }

        Write-Host ("{0,-25} {1,12} {2,12}" -f $displayVersion, $downloads, $publishedDate) -ForegroundColor $color
    }

    Write-Host ("-" * 52) -ForegroundColor Gray
    Write-Host ("{0,-25} {1,12}" -f "Total Downloads", $totalDownloads) -ForegroundColor Cyan

    if ($IncludePrerelease -and $prereleaseDownloads -gt 0) {
        Write-Host ""
        Write-Host "Breakdown:" -ForegroundColor White
        Write-Host "  Release    : $releaseDownloads" -ForegroundColor Green
        Write-Host "  Prerelease : $prereleaseDownloads" -ForegroundColor Gray
    }

    # Show top assets
    $allAssets = $releases | ForEach-Object { $_.assets } | Where-Object { $_.download_count -gt 0 }
    if ($allAssets) {
        $topAssets = $allAssets | Sort-Object -Property download_count -Descending | Select-Object -First 3

        Write-Host ""
        Write-Host "Top Downloaded Assets:" -ForegroundColor Yellow
        foreach ($asset in $topAssets) {
            Write-Host "  $($asset.download_count) - $($asset.name)" -ForegroundColor White
        }
    }

} catch {
    Write-Error "Failed to fetch download statistics: $_"
    Write-Host "`nMake sure GitHub CLI is installed and authenticated:" -ForegroundColor Yellow
    Write-Host "  gh auth login" -ForegroundColor White
    exit 1
}
