# Shared helpers for resolving which release series can be prepared from main.

function Get-SemVerInfo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [Parameter(Mandatory = $false)]
        [string]$Label = "Version"
    )

    if ([string]::IsNullOrWhiteSpace($Version)) {
        throw "$Label is required."
    }

    $trimmedVersion = $Version.Trim()
    $match = [regex]::Match($trimmedVersion, '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$')
    if (-not $match.Success) {
        throw "$Label must use X.Y.Z format. Actual: '$Version'"
    }

    $major = [int]$match.Groups['major'].Value
    $minor = [int]$match.Groups['minor'].Value
    $patch = [int]$match.Groups['patch'].Value

    return [PSCustomObject]@{
        Major   = $major
        Minor   = $minor
        Patch   = $patch
        Series  = "$major.$minor"
        Version = "$major.$minor.$patch"
    }
}

function Get-ReleaseSeriesInfo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Series,
        [Parameter(Mandatory = $false)]
        [string]$Label = "Release series"
    )

    if ([string]::IsNullOrWhiteSpace($Series)) {
        throw "$Label is required."
    }

    $trimmedSeries = $Series.Trim()
    $match = [regex]::Match($trimmedSeries, '^(?<major>\d+)\.(?<minor>\d+)$')
    if (-not $match.Success) {
        throw "$Label must use X.Y format. Actual: '$Series'"
    }

    $major = [int]$match.Groups['major'].Value
    $minor = [int]$match.Groups['minor'].Value

    return [PSCustomObject]@{
        Major   = $major
        Minor   = $minor
        Series  = "$major.$minor"
        Version = "$major.$minor.0"
    }
}

function Resolve-PrepareReleaseSeries {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$MainVersion,
        [string]$RequestedSeries = $null
    )

    $main = Get-SemVerInfo -Version $MainVersion -Label "Current main version"
    $nextMinorSeries = "$($main.Major).$($main.Minor + 1)"
    $nextMajorSeries = "$($main.Major + 1).0"

    if ($main.Patch -eq 0) {
        $defaultSeries = $main.Series
        $allowedExplicitSeries = @($main.Series, $nextMajorSeries)
    } else {
        $defaultSeries = $nextMinorSeries
        $allowedExplicitSeries = @($nextMinorSeries, $nextMajorSeries)
    }

    $requestedTrimmed = if ([string]::IsNullOrWhiteSpace($RequestedSeries)) { $null } else { $RequestedSeries.Trim() }
    if ([string]::IsNullOrWhiteSpace($requestedTrimmed)) {
        $resolvedSeries = $defaultSeries
        $source = "default"
    } else {
        $requested = Get-ReleaseSeriesInfo -Series $requestedTrimmed -Label "Requested release series"
        if ($allowedExplicitSeries -notcontains $requested.Series) {
            $allowedList = ($allowedExplicitSeries | Select-Object -Unique) -join ", "
            throw "Requested release series '$($requested.Series)' is not allowed from current main version '$($main.Version)'. Default release series: '$defaultSeries'. Allowed explicit series: $allowedList."
        }

        $resolvedSeries = $requested.Series
        $source = "input"
    }

    $resolved = Get-ReleaseSeriesInfo -Series $resolvedSeries -Label "Resolved release series"

    return [PSCustomObject]@{
        CurrentMainVersion     = $main.Version
        CurrentMainSeries      = $main.Series
        CurrentMainPatch       = $main.Patch
        DefaultReleaseSeries   = $defaultSeries
        AllowedExplicitSeries  = @($allowedExplicitSeries | Select-Object -Unique)
        ResolvedReleaseSeries  = $resolved.Series
        ResolvedReleaseVersion = $resolved.Version
        ResolutionSource       = $source
        NextMajorSeries        = $nextMajorSeries
    }
}
