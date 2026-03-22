# Shared helpers for release support policy checks.

function Get-ReleaseAssetSummary {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        $Assets
    )

    $assetNames = @()
    foreach ($asset in @($Assets)) {
        if ($null -eq $asset) {
            continue
        }

        $name = [string]$asset.name
        if ([string]::IsNullOrWhiteSpace($name)) {
            continue
        }

        $assetNames += $name.Trim()
    }

    $bundleAssets = @($assetNames | Where-Object { $_ -match '\.msixbundle$' })
    $hasChecksum = $assetNames -contains "SHA256SUMS.txt"

    return [PSCustomObject]@{
        AssetNames        = $assetNames
        BundleAssets      = $bundleAssets
        BundleCount       = $bundleAssets.Count
        HasChecksum       = $hasChecksum
        HasArchiveAssets  = ($bundleAssets.Count -eq 1 -and $hasChecksum)
    }
}

function Get-MarkdownSection {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$Body,
        [Parameter(Mandatory = $true)]
        [string]$Heading
    )

    if ([string]::IsNullOrWhiteSpace($Body) -or [string]::IsNullOrWhiteSpace($Heading)) {
        return $null
    }

    $pattern = "(?ms)^##\s+$([regex]::Escape($Heading))\s*\r?\n(?<content>.*?)(?=^\s*##\s+|\z)"
    $match = [regex]::Match($Body, $pattern)
    if (-not $match.Success) {
        return $null
    }

    return $match.Groups["content"].Value.Trim()
}

function Get-StoreSubmissionLogState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$Body
    )

    $section = Get-MarkdownSection -Body $Body -Heading "Store Submission Log"
    $records = @()

    if (-not [string]::IsNullOrWhiteSpace($section)) {
        $matches = [regex]::Matches($section, '(?im)^\s*StoreSubmission:\s*(?<line>.+)$')
        foreach ($match in $matches) {
            $line = $match.Groups['line'].Value.Trim()
            if ([string]::IsNullOrWhiteSpace($line)) {
                continue
            }

            $commit = $null
            $status = $null

            $commitMatch = [regex]::Match($line, '\bcommit=(?<commit>[0-9a-f]{40})\b')
            if ($commitMatch.Success) {
                $commit = $commitMatch.Groups['commit'].Value.Trim()
            }

            $statusMatch = [regex]::Match($line, '\bstatus=(?<status>[^|]+?)(?=\s*(?:\||$))')
            if ($statusMatch.Success) {
                $status = $statusMatch.Groups['status'].Value.Trim()
            }

            $records += [PSCustomObject]@{
                Line   = $line
                Commit = $commit
                Status = $status
            }
        }
    }

    $latestRecord = $null
    if ($records.Count -gt 0) {
        $latestRecord = $records[$records.Count - 1]
    }

    return [PSCustomObject]@{
        Section         = $section
        HasSubmission   = ($records.Count -gt 0)
        SubmissionCount = $records.Count
        LatestLine      = if ($latestRecord) { $latestRecord.Line } else { $null }
        LatestCommit    = if ($latestRecord) { $latestRecord.Commit } else { $null }
        LatestStatus    = if ($latestRecord) { $latestRecord.Status } else { $null }
        Records         = $records
    }
}

function Get-ReleaseArchiveCommitFromBody {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$Body
    )

    $section = Get-MarkdownSection -Body $Body -Heading "Release Archive (Unsigned)"
    if ([string]::IsNullOrWhiteSpace($section)) {
        return $null
    }

    $commitMatch = [regex]::Match($section, '(?im)^\s*\|\s*\*\*Commit\*\*\s*\|.*?(?<commit>[0-9a-f]{40}).*$')
    if (-not $commitMatch.Success) {
        return $null
    }

    return $commitMatch.Groups['commit'].Value.Trim()
}

function Get-GitHubIssues {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository
    )

    if ([string]::IsNullOrWhiteSpace($Repository) -or $Repository -notmatch '^[^/\s]+/[^/\s]+$') {
        throw "Repository must be in 'owner/name' format. Actual: '$Repository'"
    }

    $ghAvailable = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $ghAvailable) {
        throw "GitHub CLI 'gh' is required to query issues."
    }

    $issuesPagesJson = gh api --paginate --slurp "repos/$Repository/issues?state=all&per_page=100" 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($issuesPagesJson)) {
        throw "Failed to query GitHub issues for '$Repository'."
    }

    try {
        $issuePages = $issuesPagesJson | ConvertFrom-Json -Depth 100
    }
    catch {
        throw "Failed to parse GitHub issues response for '$Repository'."
    }

    $issues = @()
    foreach ($page in @($issuePages)) {
        if ($null -eq $page) {
            continue
        }

        $entries = if ($page -is [System.Array]) { $page } else { @($page) }
        foreach ($entry in $entries) {
            if ($null -eq $entry -or $null -ne $entry.pull_request) {
                continue
            }

            $issues += [PSCustomObject]@{
                Number = [int]$entry.number
                Title  = ([string]$entry.title).Trim()
                Url    = [string]$entry.html_url
                Body   = [string]$entry.body
                State  = [string]$entry.state
            }
        }
    }

    return $issues
}

function Resolve-PreferredIssueMatch {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        $Matches,
        [Parameter(Mandatory = $true)]
        [string]$DuplicateOpenMessage,
        [Parameter(Mandatory = $true)]
        [string]$DuplicateClosedMessage
    )

    $matchArray = @($Matches | Where-Object { $null -ne $_ })
    if ($matchArray.Count -eq 0) {
        return $null
    }

    $openMatches = @(
        $matchArray |
            Where-Object { ([string]$_.State).Trim().ToLowerInvariant() -eq "open" }
    )
    if ($openMatches.Count -eq 1) {
        return $openMatches[0]
    }

    $candidates = if ($openMatches.Count -gt 0) { $openMatches } else { $matchArray }
    if ($candidates.Count -eq 1) {
        return $candidates[0]
    }

    $details = $candidates |
        Sort-Object `
            @{ Expression = { if (([string]$_.State).Trim().ToLowerInvariant() -eq "open") { 0 } else { 1 } } }, `
            @{ Expression = { $_.Number }; Descending = $true } |
        ForEach-Object {
            $state = ([string]$_.State).Trim()
            if (-not $state) {
                $state = "unknown"
            }

            "#$($_.Number) [$state] $($_.Url)"
        }

    if ($openMatches.Count -gt 1) {
        throw "$DuplicateOpenMessage Matches: $($details -join '; ')"
    }

    throw "$DuplicateClosedMessage Matches: $($details -join '; ')"
}

function Get-GitHubIssueByExactTitle {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository,
        [Parameter(Mandatory = $true)]
        [string]$Title,
        [switch]$AllowMissing = $false
    )

    if ([string]::IsNullOrWhiteSpace($Title)) {
        throw "Title is required."
    }

    $exactTitle = $Title.Trim()
    $matches = @(Get-GitHubIssues -Repository $Repository | Where-Object { $_.Title -eq $exactTitle })

    if ($matches.Count -eq 0) {
        if ($AllowMissing) {
            return $null
        }

        throw "Issue '$exactTitle' was not found in '$Repository'."
    }

    return Resolve-PreferredIssueMatch `
        -Matches $matches `
        -DuplicateOpenMessage "Multiple open issues found with exact title '$exactTitle' in '$Repository'. Keep a single open issue per release-notes title." `
        -DuplicateClosedMessage "Multiple closed issues found with exact title '$exactTitle' in '$Repository'. Reopen or rename the canonical issue so a single match remains."
}

function Get-ReleaseArchiveStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository,
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    if ([string]::IsNullOrWhiteSpace($Repository) -or $Repository -notmatch '^[^/\s]+/[^/\s]+$') {
        throw "Repository must be in 'owner/name' format. Actual: '$Repository'"
    }

    $versionPattern = '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$'
    $versionMatch = [regex]::Match($Version.Trim(), $versionPattern)
    if (-not $versionMatch.Success) {
        throw "Invalid version format: '$Version' (expected X.Y.Z)."
    }

    $ghAvailable = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $ghAvailable) {
        throw "GitHub CLI 'gh' is required to query finalized GitHub Releases."
    }

    $releaseJson = gh release view $Version --repo $Repository --json tagName,isDraft,assets 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($releaseJson)) {
        throw "GitHub Release '$Version' was not found in '$Repository'."
    }

    try {
        $release = $releaseJson | ConvertFrom-Json -Depth 100
    }
    catch {
        throw "Failed to parse GitHub Release metadata for '$Version' in '$Repository'."
    }

    $assetSummary = Get-ReleaseAssetSummary -Assets $release.assets

    return [PSCustomObject]@{
        TagName          = [string]$release.tagName
        IsDraft          = ($release.isDraft -eq $true)
        HasChecksum      = $assetSummary.HasChecksum
        BundleCount      = $assetSummary.BundleCount
        AssetNames       = $assetSummary.AssetNames
        HasArchiveAssets = $assetSummary.HasArchiveAssets
    }
}

function Get-ReleaseFinalizationState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository,
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [switch]$AllowMissing = $false
    )

    if ([string]::IsNullOrWhiteSpace($Repository) -or $Repository -notmatch '^[^/\s]+/[^/\s]+$') {
        throw "Repository must be in 'owner/name' format. Actual: '$Repository'"
    }

    $versionPattern = '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$'
    $versionMatch = [regex]::Match($Version.Trim(), $versionPattern)
    if (-not $versionMatch.Success) {
        throw "Invalid version format: '$Version' (expected X.Y.Z)."
    }

    $ghAvailable = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $ghAvailable) {
        throw "GitHub CLI 'gh' is required to query release finalization state."
    }

    $releaseJson = gh release view $Version --repo $Repository --json tagName,isDraft,assets,body 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($releaseJson)) {
        if ($AllowMissing) {
            return [PSCustomObject]@{
                Exists                 = $false
                TagName                = $Version.Trim()
                IsDraft                = $false
                AssetNames             = @()
                HasChecksum            = $false
                BundleCount            = 0
                HasArchiveAssets       = $false
                Body                   = ""
                ArchiveCommit          = $null
                HasStoreSubmission     = $false
                StoreSubmissionCount   = 0
                StoreSubmissionCommit  = $null
                StoreSubmissionStatus  = $null
                StoreSubmissionSection = $null
            }
        }

        throw "GitHub Release '$Version' was not found in '$Repository'."
    }

    try {
        $release = $releaseJson | ConvertFrom-Json -Depth 100
    }
    catch {
        throw "Failed to parse GitHub Release metadata for '$Version' in '$Repository'."
    }

    $body = ""
    if ($release.body) {
        $body = [string]$release.body
    }

    $assetSummary = Get-ReleaseAssetSummary -Assets $release.assets
    $storeSubmission = Get-StoreSubmissionLogState -Body $body
    $archiveCommit = Get-ReleaseArchiveCommitFromBody -Body $body

    return [PSCustomObject]@{
        Exists                 = $true
        TagName                = [string]$release.tagName
        IsDraft                = ($release.isDraft -eq $true)
        AssetNames             = $assetSummary.AssetNames
        HasChecksum            = $assetSummary.HasChecksum
        BundleCount            = $assetSummary.BundleCount
        HasArchiveAssets       = $assetSummary.HasArchiveAssets
        Body                   = $body
        ArchiveCommit          = $archiveCommit
        HasStoreSubmission     = $storeSubmission.HasSubmission
        StoreSubmissionCount   = $storeSubmission.SubmissionCount
        StoreSubmissionCommit  = $storeSubmission.LatestCommit
        StoreSubmissionStatus  = $storeSubmission.LatestStatus
        StoreSubmissionSection = $storeSubmission.Section
    }
}

function Assert-FinalizedReleaseArchive {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository,
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [switch]$Quiet = $false
    )

    $status = Get-ReleaseArchiveStatus -Repository $Repository -Version $Version
    if ($status.IsDraft) {
        throw "GitHub Release '$Version' exists but is still a draft."
    }
    if (-not $status.HasArchiveAssets) {
        $assetList = if ($status.AssetNames.Count -gt 0) { $status.AssetNames -join ", " } else { "(none)" }
        throw "GitHub Release '$Version' does not have the finalized archive assets yet. Expected exactly one .msixbundle and SHA256SUMS.txt. Assets: $assetList"
    }

    if (-not $Quiet) {
        Write-Host "[OK] $Version has finalized archive assets." -ForegroundColor Green
    }

    return [PSCustomObject]@{
        TagName          = $status.TagName
        HasChecksum      = $status.HasChecksum
        BundleCount      = $status.BundleCount
        HasArchiveAssets = $status.HasArchiveAssets
        Status           = "finalized_archive"
    }
}

function Assert-StoreSubmissionRecorded {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository,
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [switch]$Quiet = $false
    )

    $state = Get-ReleaseFinalizationState -Repository $Repository -Version $Version

    if ($state.IsDraft) {
        throw "GitHub Release '$Version' exists but is still a draft."
    }
    if (-not $state.HasArchiveAssets) {
        $assetList = if ($state.AssetNames.Count -gt 0) { $state.AssetNames -join ", " } else { "(none)" }
        throw "GitHub Release '$Version' does not have the finalized archive assets yet. Expected exactly one .msixbundle and SHA256SUMS.txt. Assets: $assetList"
    }
    if (-not $state.HasStoreSubmission) {
        throw "GitHub Release '$Version' does not have a Store Submission Log entry yet. Record the Partner Center submission before starting the next patch cycle."
    }
    if (-not $state.StoreSubmissionCommit) {
        throw "GitHub Release '$Version' has a Store Submission Log entry but no parsable commit. Fix the Store Submission Log before continuing."
    }

    if (-not $Quiet) {
        $status = if ($state.StoreSubmissionStatus) { $state.StoreSubmissionStatus } else { "recorded" }
        Write-Host "[OK] $Version has a Store submission record ($status) for commit $($state.StoreSubmissionCommit)." -ForegroundColor Green
    }

    return [PSCustomObject]@{
        TagName               = $state.TagName
        ArchiveCommit         = $state.ArchiveCommit
        StoreSubmissionCommit = $state.StoreSubmissionCommit
        StoreSubmissionStatus = $state.StoreSubmissionStatus
        Status                = "store_submission_recorded"
    }
}

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

            $assetSummary = Get-ReleaseAssetSummary -Assets $entry.assets
            if (-not $assetSummary.HasArchiveAssets) {
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

        throw "No finalized GitHub Releases (X.Y.Z with archive assets) found for '$Repository'."
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

function Get-BlockingFilesAheadOfFinalizedTag {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$TagCommit,
        [Parameter(Mandatory = $true)]
        [string]$HeadCommit
    )

    if ([string]::IsNullOrWhiteSpace($TagCommit) -or $TagCommit -notmatch '^[0-9a-f]{40}$') {
        throw "TagCommit must be a full 40-character SHA. Actual: '$TagCommit'"
    }

    if ([string]::IsNullOrWhiteSpace($HeadCommit) -or $HeadCommit -notmatch '^[0-9a-f]{40}$') {
        throw "HeadCommit must be a full 40-character SHA. Actual: '$HeadCommit'"
    }

    git merge-base --is-ancestor $TagCommit $HeadCommit
    $mergeBaseExit = $LASTEXITCODE
    if ($mergeBaseExit -eq 1) {
        throw "Finalized tag commit '$TagCommit' is not an ancestor of release branch HEAD '$HeadCommit'."
    }
    if ($mergeBaseExit -ne 0) {
        throw "Failed to verify whether finalized tag commit '$TagCommit' is an ancestor of release branch HEAD '$HeadCommit'."
    }

    $changedFiles = @(git diff --name-only --find-renames "$TagCommit..$HeadCommit")
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to inspect changes between finalized tag commit '$TagCommit' and release branch HEAD '$HeadCommit'."
    }

    $changedFiles = @(
        $changedFiles |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )

    $allowedPatterns = @(
        '^docs/',
        '^site/',
        '^\.github/workflows/deploy-pages\.yml$',
        '^\.github/workflows/release-finalize\.yml$',
        '^scripts/build-store-package\.ps1$',
        '^scripts/show-version-report\.ps1$',
        '^scripts/store-checklist\.ps1$',
        '^[^/]+\.md$'
    )

    $blockingFiles = @()
    foreach ($changedFile in $changedFiles) {
        $isAllowed = $false
        foreach ($pattern in $allowedPatterns) {
            if ($changedFile -match $pattern) {
                $isAllowed = $true
                break
            }
        }

        if (-not $isAllowed) {
            $blockingFiles += $changedFile
        }
    }

    return $blockingFiles
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
