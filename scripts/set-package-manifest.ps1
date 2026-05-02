#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Apply a predefined identity profile and version to Package.appxmanifest.

.DESCRIPTION
    This script keeps Store and preview package identities explicit and
    centralized so workflows can switch profiles without duplicating manifest
    patch logic.
#>

param(
    [ValidateSet("preview", "store", "unsigned")]
    [string]$Profile = "preview",

    [string]$Version = $null,

    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$profiles = @{
    preview = @{
        # Local signed deployment/debugging profile.
        Name = "ClipSave.Preview"
        Publisher = "CN=ClipSavePreview"
        PackageDisplayName = "ms-resource:PackageDisplayName"
        VisualDisplayName = "ms-resource:PreviewVisualDisplayName"
        Description = "ms-resource:PreviewDescription"
        PublisherDisplayName = "ms-resource:PublisherDisplayName"
        StartupTaskDisplayName = "ms-resource:PreviewStartupTaskDisplayName"
    }
    unsigned = @{
        # Unsigned artifact profile for Dev / RC / Archive channels.
        Name = "ClipSave.Preview"
        Publisher = "CN=ClipSavePreview, OID.2.25.311729368913984317654407730594956997722=1"
        PackageDisplayName = "ms-resource:PackageDisplayName"
        VisualDisplayName = "ms-resource:PreviewVisualDisplayName"
        Description = "ms-resource:PreviewDescription"
        PublisherDisplayName = "ms-resource:PublisherDisplayName"
        StartupTaskDisplayName = "ms-resource:PreviewStartupTaskDisplayName"
    }
    store = @{
        Name = "tnagata012.ClipSave"
        Publisher = "CN=6ECD54B7-8ED5-46BA-81AD-ECBC0E843959"
        PackageDisplayName = "ms-resource:PackageDisplayName"
        VisualDisplayName = "ms-resource:StoreVisualDisplayName"
        Description = "ms-resource:StoreDescription"
        PublisherDisplayName = "tnagata012"
        StartupTaskDisplayName = "ms-resource:StoreStartupTaskDisplayName"
    }
}

$selectedProfile = $profiles[$Profile]
if (-not $selectedProfile) {
    throw "Unknown profile: $Profile"
}

$manifestPath = Join-Path $ProjectRoot "src\ClipSave.Package\Package.appxmanifest"
if (-not (Test-Path $manifestPath)) {
    throw "Package.appxmanifest not found: $manifestPath"
}

$manifestText = (Get-Content -LiteralPath $manifestPath -Raw).TrimStart([char]0xFEFF)

function Replace-ManifestValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,
        [Parameter(Mandatory = $true)]
        [string]$Pattern,
        [Parameter(Mandatory = $true)]
        [string]$Value,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $regex = [regex]::new($Pattern)
    if (-not $regex.IsMatch($Text)) {
        throw "$Description not found in $manifestPath"
    }

    return $regex.Replace(
        $Text,
        {
            param($match)
            return $match.Groups["prefix"].Value + $Value + $match.Groups["suffix"].Value
        },
        1
    )
}

$manifestText = Replace-ManifestValue `
    -Text $manifestText `
    -Pattern '(?<prefix><Identity\b[^>]*\bName=")(?<value>[^"]+)(?<suffix>")' `
    -Value $selectedProfile.Name `
    -Description "Identity/@Name"

$manifestText = Replace-ManifestValue `
    -Text $manifestText `
    -Pattern '(?<prefix><Identity\b[^>]*\bPublisher=")(?<value>[^"]+)(?<suffix>")' `
    -Value $selectedProfile.Publisher `
    -Description "Identity/@Publisher"

if ($Version) {
    $manifestText = Replace-ManifestValue `
        -Text $manifestText `
        -Pattern '(?<prefix><Identity\b[^>]*\bVersion=")(?<value>[^"]+)(?<suffix>")' `
        -Value $Version `
        -Description "Identity/@Version"
}

$manifestText = Replace-ManifestValue `
    -Text $manifestText `
    -Pattern '(?<prefix><Properties>\s*<DisplayName>)(?<value>[^<]+)(?<suffix></DisplayName>)' `
    -Value $selectedProfile.PackageDisplayName `
    -Description "Properties/DisplayName"

$manifestText = Replace-ManifestValue `
    -Text $manifestText `
    -Pattern '(?<prefix><PublisherDisplayName>)(?<value>[^<]+)(?<suffix></PublisherDisplayName>)' `
    -Value $selectedProfile.PublisherDisplayName `
    -Description "Properties/PublisherDisplayName"

$manifestText = Replace-ManifestValue `
    -Text $manifestText `
    -Pattern '(?<prefix><uap:VisualElements\b[^>]*\bDisplayName=")(?<value>[^"]+)(?<suffix>")' `
    -Value $selectedProfile.VisualDisplayName `
    -Description "uap:VisualElements/@DisplayName"

$manifestText = Replace-ManifestValue `
    -Text $manifestText `
    -Pattern '(?<prefix><uap:VisualElements\b[^>]*\bDescription=")(?<value>[^"]+)(?<suffix>")' `
    -Value $selectedProfile.Description `
    -Description "uap:VisualElements/@Description"

$manifestText = Replace-ManifestValue `
    -Text $manifestText `
    -Pattern '(?<prefix><desktop:StartupTask\b[^>]*\bDisplayName=")(?<value>[^"]+)(?<suffix>")' `
    -Value $selectedProfile.StartupTaskDisplayName `
    -Description "desktop:StartupTask/@DisplayName"

$utf8Bom = New-Object System.Text.UTF8Encoding($true)
[System.IO.File]::WriteAllText($manifestPath, $manifestText, $utf8Bom)

Write-Host "Configured Package.appxmanifest profile '$Profile'" -ForegroundColor Cyan
Write-Host "  Name      : $($selectedProfile.Name)" -ForegroundColor White
Write-Host "  Publisher : $($selectedProfile.Publisher)" -ForegroundColor White
if ($Version) {
    Write-Host "  Version   : $Version" -ForegroundColor White
}
