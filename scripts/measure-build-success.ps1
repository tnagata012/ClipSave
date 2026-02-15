#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Measure build success rate from GitHub Actions

.DESCRIPTION
    Fetches workflow run statistics and calculates success rate.
    Automatically detects repository from git remote if not specified.

.PARAMETER Limit
    Number of recent runs to analyze (default: 50)

.PARAMETER Workflow
    Filter by workflow name (optional)

.EXAMPLE
    .\measure-build-success.ps1
    .\measure-build-success.ps1 -Limit 100
    .\measure-build-success.ps1 -Workflow "Dev Build"
#>

param(
    [int]$Limit = 50,
    [string]$Workflow = $null
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

Write-Host "=== Build Statistics ===" -ForegroundColor Cyan
Write-Host "Repository: $Owner/$Repo" -ForegroundColor White

# Build API query
$apiUrl = "repos/$Owner/$Repo/actions/runs?per_page=$Limit"
if ($Workflow) {
    Write-Host "Workflow  : $Workflow" -ForegroundColor White
}

try {
    $runs = gh api $apiUrl | ConvertFrom-Json

    # Filter by workflow name if specified
    $filteredRuns = if ($Workflow) {
        $runs.workflow_runs | Where-Object { $_.name -eq $Workflow }
    } else {
        $runs.workflow_runs
    }

    $total = $filteredRuns.Count
    if ($total -eq 0) {
        Write-Host "`nNo workflow runs found" -ForegroundColor Yellow
        exit 0
    }

    $successful = ($filteredRuns | Where-Object { $_.conclusion -eq 'success' }).Count
    $failed = ($filteredRuns | Where-Object { $_.conclusion -eq 'failure' }).Count
    $cancelled = ($filteredRuns | Where-Object { $_.conclusion -eq 'cancelled' }).Count
    $inProgress = ($filteredRuns | Where-Object { $_.status -eq 'in_progress' }).Count

    $completedTotal = $successful + $failed + $cancelled
    $successRate = if ($completedTotal -gt 0) { [math]::Round(($successful / $completedTotal) * 100, 1) } else { 0 }

    Write-Host ""
    Write-Host ("{0,-15} {1,8}" -f "Status", "Count") -ForegroundColor White
    Write-Host ("-" * 25) -ForegroundColor Gray
    Write-Host ("{0,-15} {1,8}" -f "Successful", $successful) -ForegroundColor Green
    Write-Host ("{0,-15} {1,8}" -f "Failed", $failed) -ForegroundColor Red
    Write-Host ("{0,-15} {1,8}" -f "Cancelled", $cancelled) -ForegroundColor Gray
    if ($inProgress -gt 0) {
        Write-Host ("{0,-15} {1,8}" -f "In Progress", $inProgress) -ForegroundColor Yellow
    }
    Write-Host ("-" * 25) -ForegroundColor Gray
    Write-Host ("{0,-15} {1,8}" -f "Total", $total) -ForegroundColor White

    Write-Host ""
    if ($successRate -ge 95) {
        Write-Host "✅ Success Rate: $successRate% (target: ≥95%)" -ForegroundColor Green
    } elseif ($successRate -ge 80) {
        Write-Host "⚠️  Success Rate: $successRate% (target: ≥95%)" -ForegroundColor Yellow
    } else {
        Write-Host "❌ Success Rate: $successRate% (target: ≥95%)" -ForegroundColor Red
    }

    # Show recent failures
    $recentFailures = $filteredRuns | Where-Object { $_.conclusion -eq 'failure' } | Select-Object -First 3
    if ($recentFailures) {
        Write-Host "`nRecent Failures:" -ForegroundColor Yellow
        foreach ($failure in $recentFailures) {
            $date = [DateTime]::Parse($failure.created_at).ToString("yyyy-MM-dd HH:mm")
            Write-Host "  - $($failure.name) @ $date" -ForegroundColor Red
            Write-Host "    $($failure.html_url)" -ForegroundColor Gray
        }
    }

} catch {
    Write-Error "Failed to fetch build statistics: $_"
    Write-Host "`nMake sure GitHub CLI is installed and authenticated:" -ForegroundColor Yellow
    Write-Host "  gh auth login" -ForegroundColor White
    exit 1
}
