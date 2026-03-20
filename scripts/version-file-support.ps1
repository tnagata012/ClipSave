# Shared helpers for updating repository version files without rewriting XML layout.

function Get-TextFileEncoding {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return [System.Text.UTF8Encoding]::new($true)
    }

    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        return [System.Text.UnicodeEncoding]::new($false, $true)
    }

    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
        return [System.Text.UnicodeEncoding]::new($true, $true)
    }

    return [System.Text.UTF8Encoding]::new($false)
}

function Set-TextFileContentPreservingEncoding {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $encoding = Get-TextFileEncoding -Path $Path
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Set-TextValueByRegex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Pattern,
        [Parameter(Mandatory = $true)]
        [string]$Value,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required file not found: $Path"
    }

    $content = [System.IO.File]::ReadAllText($Path)
    $regex = [System.Text.RegularExpressions.Regex]::new($Pattern)
    $matches = $regex.Matches($content)
    if ($matches.Count -ne 1) {
        throw "$Description match count must be exactly 1 in '$Path'. Actual: $($matches.Count)"
    }

    $currentValue = $matches[0].Groups['value'].Value
    if ($currentValue -eq $Value) {
        return $false
    }

    $updated = $regex.Replace(
        $content,
        {
            param($match)
            return $match.Groups['prefix'].Value + $Value + $match.Groups['suffix'].Value
        },
        1
    )

    Set-TextFileContentPreservingEncoding -Path $Path -Content $updated
    return $true
}

function Set-RepositoryVersionFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot,
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    $resolvedRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
    if ($Version -notmatch '^\d+\.\d+\.\d+$') {
        throw "Invalid repository version format: '$Version' (expected X.Y.Z)."
    }

    $propsPath = Join-Path $resolvedRoot "Directory.Build.props"
    $manifestPath = Join-Path $resolvedRoot "src/ClipSave.Package/Package.appxmanifest"
    $manifestVersion = "$Version.0"

    $propsUpdated = Set-TextValueByRegex `
        -Path $propsPath `
        -Pattern '(?<prefix><Version>)(?<value>[^<]+)(?<suffix></Version>)' `
        -Value $Version `
        -Description "Directory.Build.props <Version>"

    $manifestUpdated = Set-TextValueByRegex `
        -Path $manifestPath `
        -Pattern '(?<prefix><Identity\b[^>]*\bVersion=")(?<value>[^"]+)(?<suffix>")' `
        -Value $manifestVersion `
        -Description "Package.appxmanifest Identity/@Version"

    return [PSCustomObject]@{
        ProjectRoot      = $resolvedRoot
        Version          = $Version
        ManifestVersion  = $manifestVersion
        PropsUpdated     = $propsUpdated
        ManifestUpdated  = $manifestUpdated
    }
}
