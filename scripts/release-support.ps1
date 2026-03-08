# Shared helpers for release support policy checks.

function Get-LatestFinalizedReleaseVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository,
        [switch]$AllowMissing = $false
    )

    if ([string]::IsNullOrWhiteSpace($Repository) -or $Repository -notmatch '^[^/\s]+/[^/\s]+$') {
        throw "Repository must be in 'owner/name' format. Actual: '$Repository'"
    }

    $ghAvailable = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $ghAvailable) {
        throw "GitHub CLI 'gh' is required to query finalized GitHub Releases."
    }

    $releasePagesJson = gh api --paginate --slurp "repos/$Repository/releases?per_page=100" 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to query GitHub Releases for '$Repository'."
    }

    if ([string]::IsNullOrWhiteSpace($releasePagesJson)) {
        throw "GitHub Releases query for '$Repository' returned no data."
    }

    try {
        $releasePages = $releasePagesJson | ConvertFrom-Json -Depth 100
    }
    catch {
        throw "Failed to parse GitHub Releases response for '$Repository'."
    }

    $finalizedReleases = @()
    foreach ($page in @($releasePages)) {
        if ($null -eq $page) {
            continue
        }

        $entries = if ($page -is [System.Array]) { $page } else { @($page) }
        foreach ($entry in $entries) {
            if ($null -eq $entry -or $entry.draft -eq $true) {
                continue
            }

            $tagName = [string]$entry.tag_name
            $versionMatch = [regex]::Match($tagName, '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$')
            if (-not $versionMatch.Success) {
                continue
            }

            $finalizedReleases += [PSCustomObject]@{
                TagName = $tagName
                Major   = [int]$versionMatch.Groups['major'].Value
                Minor   = [int]$versionMatch.Groups['minor'].Value
                Patch   = [int]$versionMatch.Groups['patch'].Value
            }
        }
    }

    if ($finalizedReleases.Count -eq 0) {
        if ($AllowMissing) {
            return $null
        }

        throw "No finalized GitHub Releases (X.Y.Z) found for '$Repository'."
    }

    $latestVersion = $finalizedReleases |
        Sort-Object `
            @{ Expression = { $_.Major }; Descending = $true }, `
            @{ Expression = { $_.Minor }; Descending = $true }, `
            @{ Expression = { $_.Patch }; Descending = $true } |
        Select-Object -First 1 -ExpandProperty TagName

    return $latestVersion.Trim()
}

function Resolve-ReleaseSeriesTarget {
    [CmdletBinding()]
    param(
        [string]$Version = $null,
        [string]$ReleaseBranch = $null
    )

    $semverPattern = '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$'
    $releaseBranchPattern = '^release/(?<major>\d+)\.(?<minor>\d+)$'

    if ([string]::IsNullOrWhiteSpace($Version) -and [string]::IsNullOrWhiteSpace($ReleaseBranch)) {
        throw "Either -Version (X.Y.Z) or -ReleaseBranch (release/X.Y) is required."
    }

    $targetSeries = $null
    $targetLabel = $null

    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        $versionMatch = [regex]::Match($Version.Trim(), $semverPattern)
        if (-not $versionMatch.Success) {
            throw "Invalid version format: '$Version' (expected X.Y.Z)."
        }

        $targetSeries = "$($versionMatch.Groups['major'].Value).$($versionMatch.Groups['minor'].Value)"
        $targetLabel = "release/$targetSeries"
    }

    if (-not [string]::IsNullOrWhiteSpace($ReleaseBranch)) {
        $branchMatch = [regex]::Match($ReleaseBranch.Trim(), $releaseBranchPattern)
        if (-not $branchMatch.Success) {
            throw "Invalid release branch format: '$ReleaseBranch' (expected release/X.Y)."
        }

        $branchSeries = "$($branchMatch.Groups['major'].Value).$($branchMatch.Groups['minor'].Value)"
        if ($targetSeries -and $targetSeries -ne $branchSeries) {
            throw "Version '$Version' and release branch '$ReleaseBranch' point to different release series."
        }

        $targetSeries = $branchSeries
        $targetLabel = $ReleaseBranch.Trim()
    }

    return [PSCustomObject]@{
        Series = $targetSeries
        Label  = $targetLabel
    }
}

function Assert-LatestFinalizedVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository,
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [string]$ReleaseBranch = $null,
        [switch]$Quiet = $false,
        [switch]$AllowMissingFinalizedRelease = $false
    )

    $target = Resolve-ReleaseSeriesTarget -Version $Version -ReleaseBranch $ReleaseBranch
    $latestVersion = Get-LatestFinalizedReleaseVersion -Repository $Repository -AllowMissing:$AllowMissingFinalizedRelease

    if ([string]::IsNullOrWhiteSpace($latestVersion)) {
        if (-not $Quiet) {
            Write-Host "[OK] No finalized GitHub Releases exist yet. Skipping latest-version guard for $Version." -ForegroundColor Green
        }

        return [PSCustomObject]@{
            TargetSeries            = $target.Series
            TargetLabel             = $target.Label
            TargetVersion           = $Version.Trim()
            LatestVersion           = $null
            MissingFinalizedRelease = $true
            Status                  = "missing_finalized_release"
        }
    }

    $targetVersion = $Version.Trim()
    if ($targetVersion -ne $latestVersion) {
        throw "Target version '$targetVersion' is not the latest finalized version. Latest finalized version is '$latestVersion' (series 'release/$($target.Series)' requested via '$($target.Label)')."
    }

    if (-not $Quiet) {
        Write-Host "[OK] $targetVersion is the latest finalized version." -ForegroundColor Green
    }

    return [PSCustomObject]@{
        TargetSeries            = $target.Series
        TargetLabel             = $target.Label
        TargetVersion           = $targetVersion
        LatestVersion           = $latestVersion
        MissingFinalizedRelease = $false
        Status                  = "latest_finalized_version"
    }
}
