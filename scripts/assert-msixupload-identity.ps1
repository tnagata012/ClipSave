#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validate the package identity embedded in a Store upload package.

.DESCRIPTION
    Opens a .msixupload, reads the bundled Appx bundle manifest and each
    packaged app manifest, and verifies that Name / Publisher / Version /
    PublisherDisplayName match the expected Store identity.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedName,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedPublisher,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion,

    [Parameter(Mandatory = $false)]
    [string]$ExpectedPublisherDisplayName
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Host "`n[ERROR] $Message" -ForegroundColor Red
    exit 1
}

if ($ExpectedVersion -notmatch '^\d+\.\d+\.\d+\.0$') {
    Fail "Store upload package Version must use a zero revision segment (X.Y.Z.0). ExpectedVersion was '$ExpectedVersion'."
}

function Get-XmlFromZipEntry {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchiveEntry]$Entry
    )

    $stream = $Entry.Open()
    try {
        $reader = New-Object System.IO.StreamReader($stream)
        try {
            [xml]$xml = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }

    return $xml
}

function Open-NestedZipArchive {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchiveEntry]$Entry
    )

    $memory = New-Object System.IO.MemoryStream
    $stream = $Entry.Open()
    try {
        $stream.CopyTo($memory)
    }
    finally {
        $stream.Dispose()
    }

    $null = $memory.Seek(0, [System.IO.SeekOrigin]::Begin)
    $archive = New-Object System.IO.Compression.ZipArchive($memory, [System.IO.Compression.ZipArchiveMode]::Read, $false)

    return [PSCustomObject]@{
        Memory  = $memory
        Archive = $archive
    }
}

function Get-BundleIdentity {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchive]$Archive
    )

    $entry = $Archive.GetEntry("AppxMetadata/AppxBundleManifest.xml")
    if (-not $entry) {
        throw "AppxMetadata/AppxBundleManifest.xml was not found."
    }

    $manifest = Get-XmlFromZipEntry -Entry $entry
    $ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
    $ns.AddNamespace("bundle", "http://schemas.microsoft.com/appx/2013/bundle")
    $identity = $manifest.SelectSingleNode("/bundle:Bundle/bundle:Identity", $ns)
    if (-not $identity) {
        throw "Bundle identity was not found."
    }

    return [PSCustomObject]@{
        Name      = [string]$identity.Name
        Publisher = [string]$identity.Publisher
        Version   = [string]$identity.Version
    }
}

function Get-AppPackageIdentities {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchive]$BundleArchive
    )

    $packageEntries = @(
        $BundleArchive.Entries |
            Where-Object { $_.FullName -match '\.(msix|appx)$' }
    )
    if ($packageEntries.Count -eq 0) {
        throw "No .msix or .appx payloads were found in the bundle."
    }

    $identities = @()
    foreach ($packageEntry in $packageEntries) {
        $nested = Open-NestedZipArchive -Entry $packageEntry
        try {
            $manifestEntry = $nested.Archive.GetEntry("AppxManifest.xml")
            if (-not $manifestEntry) {
                throw "AppxManifest.xml was not found in payload '$($packageEntry.FullName)'."
            }

            $manifest = Get-XmlFromZipEntry -Entry $manifestEntry
            $ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
            $ns.AddNamespace("appx", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
            $identity = $manifest.SelectSingleNode("/appx:Package/appx:Identity", $ns)
            if (-not $identity) {
                throw "Package identity was not found in payload '$($packageEntry.FullName)'."
            }

            $publisherDisplayNameNode = $manifest.SelectSingleNode("/appx:Package/appx:Properties/appx:PublisherDisplayName", $ns)

            $identities += [PSCustomObject]@{
                Path                 = $packageEntry.FullName
                Name                 = [string]$identity.Name
                Publisher            = [string]$identity.Publisher
                Version              = [string]$identity.Version
                PublisherDisplayName = if ($publisherDisplayNameNode) { [string]$publisherDisplayNameNode.InnerText } else { "" }
            }
        }
        finally {
            $nested.Archive.Dispose()
            $nested.Memory.Dispose()
        }
    }

    return $identities
}

if (-not (Test-Path -LiteralPath $PackagePath)) {
    Fail "Store upload package not found: $PackagePath"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$resolvedPath = (Resolve-Path -LiteralPath $PackagePath).Path
$uploadArchive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPath)
try {
    $bundleEntries = @(
        $uploadArchive.Entries |
            Where-Object { $_.FullName -match '\.msixbundle$' }
    )
    if ($bundleEntries.Count -ne 1) {
        $actual = if ($bundleEntries.Count -gt 0) {
            ($bundleEntries | Select-Object -ExpandProperty FullName) -join ", "
        } else {
            "(none)"
        }
        Fail "Expected exactly one .msixbundle in '$resolvedPath'. Found $($bundleEntries.Count): $actual"
    }

    $nestedBundle = Open-NestedZipArchive -Entry $bundleEntries[0]
    try {
        $bundleIdentity = Get-BundleIdentity -Archive $nestedBundle.Archive
        $payloadIdentities = @(Get-AppPackageIdentities -BundleArchive $nestedBundle.Archive)
    }
    finally {
        $nestedBundle.Archive.Dispose()
        $nestedBundle.Memory.Dispose()
    }
}
finally {
    $uploadArchive.Dispose()
}

if (
    $bundleIdentity.Name -ne $ExpectedName -or
    $bundleIdentity.Publisher -ne $ExpectedPublisher -or
    $bundleIdentity.Version -ne $ExpectedVersion
) {
    Fail "Bundle identity mismatch. Expected Name='$ExpectedName', Publisher='$ExpectedPublisher', Version='$ExpectedVersion'. Actual Name='$($bundleIdentity.Name)', Publisher='$($bundleIdentity.Publisher)', Version='$($bundleIdentity.Version)'."
}

$payloadMismatch = $payloadIdentities | Where-Object {
    $_.Name -ne $ExpectedName -or
    $_.Publisher -ne $ExpectedPublisher -or
    $_.Version -ne $ExpectedVersion
}
if ($payloadMismatch) {
    $details = $payloadMismatch | ForEach-Object {
        "'$($_.Path)' => Name='$($_.Name)', Publisher='$($_.Publisher)', Version='$($_.Version)'"
    }
    Fail "Payload identity mismatch detected. Expected Name='$ExpectedName', Publisher='$ExpectedPublisher', Version='$ExpectedVersion'. Actual: $($details -join '; ')"
}

if ($ExpectedPublisherDisplayName) {
    $publisherDisplayNameMismatch = $payloadIdentities | Where-Object {
        $_.PublisherDisplayName -ne $ExpectedPublisherDisplayName
    }
    if ($publisherDisplayNameMismatch) {
        $details = $publisherDisplayNameMismatch | ForEach-Object {
            "'$($_.Path)' => PublisherDisplayName='$($_.PublisherDisplayName)'"
        }
        Fail "Payload PublisherDisplayName mismatch detected. Expected '$ExpectedPublisherDisplayName'. Actual: $($details -join '; ')"
    }
}

Write-Host "[OK] Store upload identity validated:" -ForegroundColor Green
Write-Host "  Bundle Name      : $($bundleIdentity.Name)" -ForegroundColor White
Write-Host "  Bundle Publisher : $($bundleIdentity.Publisher)" -ForegroundColor White
Write-Host "  Bundle Version   : $($bundleIdentity.Version)" -ForegroundColor White
Write-Host "  Payload Count    : $($payloadIdentities.Count)" -ForegroundColor White
if ($ExpectedPublisherDisplayName) {
    Write-Host "  Publisher Display Name : $ExpectedPublisherDisplayName" -ForegroundColor White
}
