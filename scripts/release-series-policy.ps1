# Shared helpers for resolving release series from an explicit X.Y input.

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
        [Parameter(Mandatory = $true)]
        [string]$RequestedSeries
    )

    $main = Get-SemVerInfo -Version $MainVersion -Label "Current main version"
    if ($main.Version -ne "0.0.1") {
        throw "Current main version must stay fixed at 0.0.1. Actual: '$($main.Version)'"
    }

    $requested = Get-ReleaseSeriesInfo -Series $RequestedSeries -Label "Requested release series"

    return [PSCustomObject]@{
        CurrentMainVersion     = $main.Version
        ResolvedReleaseSeries  = $requested.Series
        ResolvedReleaseVersion = $requested.Version
    }
}
