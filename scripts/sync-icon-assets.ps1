#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generate ClipSave icon assets for WPF and MSIX from one source image.

.DESCRIPTION
    Outputs:
      - src/ClipSave/Assets/ClipSave.ico
      - src/ClipSave.Package/Images/Square44x44Logo*.png
      - src/ClipSave.Package/Images/Square150x150Logo*.png
      - src/ClipSave.Package/Images/Wide310x150Logo*.png
      - src/ClipSave.Package/Images/SplashScreen*.png
      - src/ClipSave.Package/Images/StoreLogo.png

.PARAMETER Source
    Source image path (ICO or PNG). Relative paths are resolved from repository root.

.EXAMPLE
    .\scripts\sync-icon-assets.ps1

.EXAMPLE
    .\scripts\sync-icon-assets.ps1 -Source C:\assets\ClipSave.ico
#>

param(
    [string]$Source = "assets/icon/ClipSave.master.ico"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = if ([System.IO.Path]::IsPathRooted($Source)) {
    $Source
} else {
    Join-Path $projectRoot $Source
}

if (-not (Test-Path $sourcePath)) {
    Write-Error "Source image not found: $sourcePath"
    exit 1
}

Add-Type -AssemblyName System.Drawing

function New-FittedBitmap {
    param(
        [Parameter(Mandatory = $true)][System.Drawing.Image]$SourceImage,
        [Parameter(Mandatory = $true)][int]$Width,
        [Parameter(Mandatory = $true)][int]$Height,
        [double]$ScaleFactor = 1.0
    )

    $bitmap = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

        $safeScaleFactor = [Math]::Min([Math]::Max($ScaleFactor, 0.01), 1.0)
        $scaleX = [double]$Width / [double]$SourceImage.Width
        $scaleY = [double]$Height / [double]$SourceImage.Height
        $scale = [Math]::Min($scaleX, $scaleY) * $safeScaleFactor

        $drawWidth = [int][Math]::Round([double]$SourceImage.Width * $scale)
        $drawHeight = [int][Math]::Round([double]$SourceImage.Height * $scale)

        if ($drawWidth -lt 1) { $drawWidth = 1 }
        if ($drawHeight -lt 1) { $drawHeight = 1 }

        $offsetX = [int][Math]::Floor(($Width - $drawWidth) / 2.0)
        $offsetY = [int][Math]::Floor(($Height - $drawHeight) / 2.0)
        $graphics.DrawImage($SourceImage, $offsetX, $offsetY, $drawWidth, $drawHeight)

        return $bitmap
    }
    finally {
        $graphics.Dispose()
    }
}

function Save-Png {
    param(
        [Parameter(Mandatory = $true)][System.Drawing.Bitmap]$Bitmap,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Get-PngBytes {
    param(
        [Parameter(Mandatory = $true)][System.Drawing.Bitmap]$Bitmap
    )

    $stream = New-Object System.IO.MemoryStream
    try {
        $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally {
        $stream.Dispose()
    }
}

function Write-IcoFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][array]$Frames
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $writer = New-Object System.IO.BinaryWriter($stream)
        try {
            $writer.Write([UInt16]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]$Frames.Count)

            $offset = 6 + (16 * $Frames.Count)
            foreach ($frame in $Frames) {
                $size = [int]$frame.Size
                $bytes = [byte[]]$frame.Bytes

                $widthByte = if ($size -ge 256) { [byte]0 } else { [byte]$size }
                $heightByte = if ($size -ge 256) { [byte]0 } else { [byte]$size }

                $writer.Write($widthByte)
                $writer.Write($heightByte)
                $writer.Write([byte]0)
                $writer.Write([byte]0)
                $writer.Write([UInt16]1)
                $writer.Write([UInt16]32)
                $writer.Write([UInt32]$bytes.Length)
                $writer.Write([UInt32]$offset)

                $offset += $bytes.Length
            }

            foreach ($frame in $Frames) {
                $writer.Write([byte[]]$frame.Bytes)
            }
        }
        finally {
            $writer.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

$packageImagesDirectory = Join-Path $projectRoot "src/ClipSave.Package/Images"
$wpfIconPath = Join-Path $projectRoot "src/ClipSave/Assets/ClipSave.ico"

$packageAssets = @(
    @{ Name = "Square44x44Logo.png"; Width = 44; Height = 44; ScaleFactor = 1.0 },
    @{ Name = "Square44x44Logo.scale-200.png"; Width = 88; Height = 88; ScaleFactor = 1.0 },
    @{ Name = "Square150x150Logo.png"; Width = 150; Height = 150; ScaleFactor = 1.0 },
    @{ Name = "Square150x150Logo.scale-200.png"; Width = 300; Height = 300; ScaleFactor = 1.0 },
    @{ Name = "Wide310x150Logo.png"; Width = 310; Height = 150; ScaleFactor = 1.0 },
    @{ Name = "Wide310x150Logo.scale-200.png"; Width = 620; Height = 300; ScaleFactor = 1.0 },
    @{ Name = "SplashScreen.png"; Width = 620; Height = 300; ScaleFactor = 1.0 },
    @{ Name = "SplashScreen.scale-200.png"; Width = 1240; Height = 600; ScaleFactor = 1.0 },
    @{ Name = "StoreLogo.png"; Width = 50; Height = 50; ScaleFactor = 1.0 }
)

$square44TargetSizes = @(16, 20, 24, 30, 32, 36, 40, 44, 48, 64, 96, 256)
foreach ($targetSize in $square44TargetSizes) {
    $packageAssets += @{
        Name = "Square44x44Logo.targetsize-$targetSize`_altform-unplated.png"
        Width = $targetSize
        Height = $targetSize
        ScaleFactor = 1.0
    }
}

$icoSizes = @(16, 20, 24, 32, 40, 48, 64, 256)

$sourceImage = [System.Drawing.Image]::FromFile($sourcePath)
try {
    Write-Host "Source: $sourcePath ($($sourceImage.Width)x$($sourceImage.Height))" -ForegroundColor Cyan

    foreach ($asset in $packageAssets | Sort-Object Name) {
        $outputPath = Join-Path $packageImagesDirectory $asset.Name
        $bitmap = New-FittedBitmap `
            -SourceImage $sourceImage `
            -Width $asset.Width `
            -Height $asset.Height `
            -ScaleFactor $asset.ScaleFactor
        try {
            Save-Png -Bitmap $bitmap -Path $outputPath
            Write-Host "Generated: $outputPath" -ForegroundColor Gray
        }
        finally {
            $bitmap.Dispose()
        }
    }

    $icoFrames = @()
    foreach ($icoSize in $icoSizes) {
        $bitmap = New-FittedBitmap -SourceImage $sourceImage -Width $icoSize -Height $icoSize -ScaleFactor 1.0
        try {
            $icoFrames += [PSCustomObject]@{
                Size = $icoSize
                Bytes = Get-PngBytes -Bitmap $bitmap
            }
        }
        finally {
            $bitmap.Dispose()
        }
    }

    Write-IcoFile -Path $wpfIconPath -Frames $icoFrames
    Write-Host "Generated: $wpfIconPath" -ForegroundColor Gray
}
finally {
    $sourceImage.Dispose()
}

Write-Host "Icon asset sync completed." -ForegroundColor Green
