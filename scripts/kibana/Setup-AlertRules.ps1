<#
.SYNOPSIS
    Sets up Kibana alerting rules for DiscordBot monitoring.

.DESCRIPTION
    This script creates alerting rules in Kibana using the Alerting API.
    Rules are configured for monitoring errors, performance, rate limits,
    bot connectivity, and auto-moderation activity.

    IMPORTANT: Alert actions (email, Slack, PagerDuty) must be configured
    manually in Kibana after running this script. The connectors require
    sensitive credentials that cannot be automated.

.PARAMETER KibanaUrl
    The base URL of the Kibana instance (e.g., http://localhost:5601)

.PARAMETER ApiKey
    Optional API key for authentication. If not provided, assumes no authentication required.

.PARAMETER SpaceName
    Kibana space to create rules in. Defaults to "default".

.PARAMETER DryRun
    If specified, shows what would be created without actually creating rules.

.EXAMPLE
    # Create rules in local Kibana
    ./Setup-AlertRules.ps1 -KibanaUrl "http://localhost:5601"

.EXAMPLE
    # Dry run to see what rules would be created
    ./Setup-AlertRules.ps1 -KibanaUrl "http://localhost:5601" -DryRun

.EXAMPLE
    # Create rules with authentication
    ./Setup-AlertRules.ps1 -KibanaUrl "https://kibana.example.com" -ApiKey "your-api-key"

.NOTES
    Requires Kibana 8.x or later with alerting feature enabled.
    Rules are defined in ./objects/alert-rules.json
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$KibanaUrl,

    [Parameter(Mandatory = $false)]
    [string]$ApiKey,

    [Parameter(Mandatory = $false)]
    [string]$SpaceName = "default",

    [Parameter(Mandatory = $false)]
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Script directory for resolving object files
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RulesFile = Join-Path $ScriptDir "objects\alert-rules.json"

# Build base URL with space
$BaseUrl = $KibanaUrl.TrimEnd('/')
if ($SpaceName -ne "default") {
    $BaseUrl = "$BaseUrl/s/$SpaceName"
}

function Write-Status {
    param([string]$Message, [string]$Type = "Info")

    $Color = switch ($Type) {
        "Success" { "Green" }
        "Warning" { "Yellow" }
        "Error" { "Red" }
        default { "Cyan" }
    }

    $Prefix = switch ($Type) {
        "Success" { "[OK]" }
        "Warning" { "[WARN]" }
        "Error" { "[ERROR]" }
        default { "[INFO]" }
    }

    Write-Host "$Prefix $Message" -ForegroundColor $Color
}

function Get-AuthHeaders {
    $Headers = @{
        "kbn-xsrf" = "true"
        "Content-Type" = "application/json"
    }

    if ($ApiKey) {
        $Headers["Authorization"] = "ApiKey $ApiKey"
    }

    return $Headers
}

function Test-KibanaConnection {
    Write-Status "Testing connection to Kibana at $KibanaUrl..."

    try {
        $StatusUrl = "$BaseUrl/api/status"
        $Headers = Get-AuthHeaders
        $Headers.Remove("Content-Type")

        $Response = Invoke-RestMethod -Uri $StatusUrl -Method Get -Headers $Headers -TimeoutSec 10
        Write-Status "Connected to Kibana version $($Response.version.number)" -Type "Success"
        return $true
    }
    catch {
        Write-Status "Failed to connect to Kibana: $_" -Type "Error"
        return $false
    }
}

function Get-ExistingRules {
    Write-Status "Checking for existing alerting rules..."

    try {
        $RulesUrl = "$BaseUrl/api/alerting/rules/_find?per_page=100&filter=tags:discordbot"
        $Headers = Get-AuthHeaders
        $Headers.Remove("Content-Type")

        $Response = Invoke-RestMethod -Uri $RulesUrl -Method Get -Headers $Headers -TimeoutSec 30

        return $Response.data
    }
    catch {
        Write-Status "Failed to fetch existing rules: $_" -Type "Warning"
        return @()
    }
}

function New-AlertRule {
    param(
        [Parameter(Mandatory = $true)]
        [PSObject]$RuleConfig
    )

    $RuleName = $RuleConfig.name
    Write-Status "Creating rule: $RuleName"

    if ($DryRun) {
        Write-Status "  [DRY RUN] Would create rule with:" -Type "Info"
        Write-Status "    Type: $($RuleConfig.rule_type_id)" -Type "Info"
        Write-Status "    Schedule: $($RuleConfig.schedule.interval)" -Type "Info"
        Write-Status "    Tags: $($RuleConfig.tags -join ', ')" -Type "Info"
        return $true
    }

    try {
        $RulesUrl = "$BaseUrl/api/alerting/rule"
        $Headers = Get-AuthHeaders

        $Body = @{
            name = $RuleConfig.name
            rule_type_id = $RuleConfig.rule_type_id
            consumer = $RuleConfig.consumer
            schedule = $RuleConfig.schedule
            params = $RuleConfig.params
            actions = $RuleConfig.actions
            tags = $RuleConfig.tags
            notify_when = $RuleConfig.notify_when
            throttle = $RuleConfig.throttle
            enabled = $RuleConfig.enabled
        } | ConvertTo-Json -Depth 10

        $Response = Invoke-RestMethod -Uri $RulesUrl -Method Post -Headers $Headers -Body $Body -TimeoutSec 30

        Write-Status "  Created rule ID: $($Response.id)" -Type "Success"
        return $true
    }
    catch {
        $ErrorMessage = $_.Exception.Message
        if ($_.ErrorDetails.Message) {
            try {
                $ErrorBody = $_.ErrorDetails.Message | ConvertFrom-Json
                $ErrorMessage = $ErrorBody.message
            }
            catch { }
        }
        Write-Status "  Failed to create rule: $ErrorMessage" -Type "Error"
        return $false
    }
}

function Remove-ExistingRule {
    param(
        [string]$RuleId,
        [string]$RuleName
    )

    Write-Status "Removing existing rule: $RuleName ($RuleId)"

    if ($DryRun) {
        Write-Status "  [DRY RUN] Would delete rule" -Type "Info"
        return $true
    }

    try {
        $DeleteUrl = "$BaseUrl/api/alerting/rule/$RuleId"
        $Headers = Get-AuthHeaders
        $Headers.Remove("Content-Type")

        Invoke-RestMethod -Uri $DeleteUrl -Method Delete -Headers $Headers -TimeoutSec 30

        Write-Status "  Deleted" -Type "Success"
        return $true
    }
    catch {
        Write-Status "  Failed to delete: $_" -Type "Warning"
        return $false
    }
}

function Main {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Host "  Kibana Alert Rules Setup Script" -ForegroundColor Magenta
    Write-Host "  DiscordBot Monitoring" -ForegroundColor Magenta
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Host ""

    if ($DryRun) {
        Write-Status "DRY RUN MODE - No changes will be made" -Type "Warning"
        Write-Host ""
    }

    # Test connection
    if (-not (Test-KibanaConnection)) {
        Write-Status "Cannot proceed without Kibana connection" -Type "Error"
        exit 1
    }

    # Load rules configuration
    if (-not (Test-Path $RulesFile)) {
        Write-Status "Rules file not found: $RulesFile" -Type "Error"
        exit 1
    }

    Write-Status "Loading rules from $RulesFile..."
    $RulesConfig = Get-Content $RulesFile -Raw | ConvertFrom-Json

    Write-Host ""

    # Check for existing rules
    $ExistingRules = Get-ExistingRules
    $ExistingCount = ($ExistingRules | Measure-Object).Count

    if ($ExistingCount -gt 0) {
        Write-Status "Found $ExistingCount existing DiscordBot rules" -Type "Warning"
        Write-Host ""

        $Confirmation = Read-Host "Do you want to delete existing rules and recreate them? (y/N)"
        if ($Confirmation -eq 'y' -or $Confirmation -eq 'Y') {
            foreach ($Rule in $ExistingRules) {
                Remove-ExistingRule -RuleId $Rule.id -RuleName $Rule.name | Out-Null
            }
            Write-Host ""
        }
        else {
            Write-Status "Keeping existing rules. New rules with same names may fail." -Type "Info"
            Write-Host ""
        }
    }

    # Create rules
    Write-Status "Creating alerting rules..."
    Write-Host ""

    $SuccessCount = 0
    $FailedCount = 0

    foreach ($Rule in $RulesConfig.rules) {
        if (New-AlertRule -RuleConfig $Rule) {
            $SuccessCount++
        }
        else {
            $FailedCount++
        }
    }

    # Summary
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Host "  Setup Summary" -ForegroundColor Magenta
    Write-Host "========================================" -ForegroundColor Magenta

    $TotalRules = ($RulesConfig.rules | Measure-Object).Count
    Write-Status "Rules processed: $TotalRules" -Type "Info"
    Write-Status "Successful: $SuccessCount" -Type $(if ($SuccessCount -gt 0) { "Success" } else { "Warning" })
    Write-Status "Failed: $FailedCount" -Type $(if ($FailedCount -gt 0) { "Error" } else { "Success" })

    Write-Host ""

    if (-not $DryRun) {
        Write-Host "IMPORTANT: Next Steps" -ForegroundColor Yellow
        Write-Host "=====================" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Alert rules have been created but have NO ACTIONS configured." -ForegroundColor White
        Write-Host "To receive notifications, you must:" -ForegroundColor White
        Write-Host ""
        Write-Host "  1. Open Kibana at $KibanaUrl" -ForegroundColor Cyan
        Write-Host "  2. Go to Stack Management > Rules and Connectors > Connectors" -ForegroundColor Cyan
        Write-Host "  3. Create connectors for:" -ForegroundColor Cyan
        Write-Host "     - Email (requires SMTP server)" -ForegroundColor White
        Write-Host "     - Slack (requires webhook URL)" -ForegroundColor White
        Write-Host "     - PagerDuty (requires routing key) [optional]" -ForegroundColor White
        Write-Host ""
        Write-Host "  4. Go to Stack Management > Rules and Connectors > Rules" -ForegroundColor Cyan
        Write-Host "  5. Edit each rule to add actions using your connectors" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Rule severity reference:" -ForegroundColor Yellow
        Write-Host "  - Critical: Bot Disconnection (PagerDuty + Email)" -ForegroundColor Red
        Write-Host "  - High: Error Rate, Database Errors (Email + Slack)" -ForegroundColor DarkYellow
        Write-Host "  - Medium: Slow Transactions, Rate Limits (Slack)" -ForegroundColor Yellow
        Write-Host "  - Low: Auto-Mod Spike (Slack)" -ForegroundColor Green
        Write-Host ""
    }
}

# Run main
Main
