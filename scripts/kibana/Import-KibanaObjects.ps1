<#
.SYNOPSIS
    Imports Kibana saved objects (data views, saved searches, visualizations, dashboards) for DiscordBot monitoring.

.DESCRIPTION
    This script imports pre-configured Kibana objects using the Saved Objects API.
    It creates index patterns, saved searches, visualizations, and dashboards
    for monitoring the Discord bot application.

.PARAMETER KibanaUrl
    The base URL of the Kibana instance (e.g., http://localhost:5601)

.PARAMETER ApiKey
    Optional API key for authentication. If not provided, assumes no authentication required.

.PARAMETER SpaceName
    Kibana space to import objects into. Defaults to "default".

.PARAMETER Overwrite
    If specified, overwrites existing objects with the same IDs.

.EXAMPLE
    # Import to local Kibana without authentication
    ./Import-KibanaObjects.ps1 -KibanaUrl "http://localhost:5601"

.EXAMPLE
    # Import to remote Kibana with API key
    ./Import-KibanaObjects.ps1 -KibanaUrl "https://kibana.example.com" -ApiKey "your-api-key"

.EXAMPLE
    # Import to a specific space with overwrite
    ./Import-KibanaObjects.ps1 -KibanaUrl "http://localhost:5601" -SpaceName "discordbot" -Overwrite

.NOTES
    Requires Kibana 8.x or later.
    Objects are defined in the ./objects/ subdirectory as NDJSON files.
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
    [switch]$Overwrite
)

$ErrorActionPreference = "Stop"

# Script directory for resolving object files
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ObjectsDir = Join-Path $ScriptDir "objects"

# Build headers
$Headers = @{
    "kbn-xsrf" = "true"
    "Content-Type" = "application/x-ndjson"
}

if ($ApiKey) {
    $Headers["Authorization"] = "ApiKey $ApiKey"
}

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

function Test-KibanaConnection {
    Write-Status "Testing connection to Kibana at $KibanaUrl..."

    try {
        $StatusUrl = "$BaseUrl/api/status"
        $TestHeaders = @{ "kbn-xsrf" = "true" }
        if ($ApiKey) {
            $TestHeaders["Authorization"] = "ApiKey $ApiKey"
        }

        $Response = Invoke-RestMethod -Uri $StatusUrl -Method Get -Headers $TestHeaders -TimeoutSec 10
        Write-Status "Connected to Kibana version $($Response.version.number)" -Type "Success"
        return $true
    }
    catch {
        Write-Status "Failed to connect to Kibana: $_" -Type "Error"
        return $false
    }
}

function Import-NdjsonFile {
    param(
        [string]$FilePath,
        [string]$Description
    )

    if (-not (Test-Path $FilePath)) {
        Write-Status "File not found: $FilePath" -Type "Warning"
        return $false
    }

    Write-Status "Importing $Description from $(Split-Path -Leaf $FilePath)..."

    try {
        # Read the NDJSON file as raw bytes to preserve line endings
        $FileContent = [System.IO.File]::ReadAllBytes($FilePath)

        # Build import URL
        $ImportUrl = "$BaseUrl/api/saved_objects/_import"
        if ($Overwrite) {
            $ImportUrl += "?overwrite=true"
        }

        # Create multipart form data
        $Boundary = [System.Guid]::NewGuid().ToString()
        $LF = "`r`n"

        # Build the multipart body
        $BodyLines = @(
            "--$Boundary",
            "Content-Disposition: form-data; name=`"file`"; filename=`"$(Split-Path -Leaf $FilePath)`"",
            "Content-Type: application/x-ndjson",
            "",
            [System.Text.Encoding]::UTF8.GetString($FileContent),
            "--$Boundary--"
        )
        $Body = $BodyLines -join $LF

        $ImportHeaders = @{
            "kbn-xsrf" = "true"
            "Content-Type" = "multipart/form-data; boundary=$Boundary"
        }
        if ($ApiKey) {
            $ImportHeaders["Authorization"] = "ApiKey $ApiKey"
        }

        $Response = Invoke-RestMethod -Uri $ImportUrl -Method Post -Headers $ImportHeaders -Body $Body -TimeoutSec 60

        $SuccessCount = ($Response.successResults | Measure-Object).Count
        $ErrorCount = ($Response.errors | Measure-Object).Count

        if ($ErrorCount -gt 0) {
            Write-Status "Imported $SuccessCount objects with $ErrorCount errors" -Type "Warning"
            foreach ($Error in $Response.errors) {
                Write-Status "  - $($Error.id): $($Error.error.message)" -Type "Error"
            }
        }
        else {
            Write-Status "Successfully imported $SuccessCount objects" -Type "Success"
        }

        return $true
    }
    catch {
        Write-Status "Failed to import $Description`: $_" -Type "Error"
        return $false
    }
}

function Main {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Host "  Kibana Objects Import Script" -ForegroundColor Magenta
    Write-Host "  DiscordBot Monitoring Setup" -ForegroundColor Magenta
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Host ""

    # Test connection
    if (-not (Test-KibanaConnection)) {
        Write-Status "Cannot proceed without Kibana connection" -Type "Error"
        exit 1
    }

    Write-Host ""
    Write-Status "Target space: $SpaceName"
    Write-Status "Overwrite existing: $Overwrite"
    Write-Host ""

    # Import objects in dependency order
    $ImportOrder = @(
        @{ File = "data-views.ndjson"; Description = "Data Views (Index Patterns)" },
        @{ File = "saved-searches.ndjson"; Description = "Saved Searches" },
        @{ File = "visualizations.ndjson"; Description = "Visualizations" },
        @{ File = "dashboards.ndjson"; Description = "Dashboards" }
    )

    $TotalSuccess = 0
    $TotalFailed = 0

    foreach ($Item in $ImportOrder) {
        $FilePath = Join-Path $ObjectsDir $Item.File
        if (Import-NdjsonFile -FilePath $FilePath -Description $Item.Description) {
            $TotalSuccess++
        }
        else {
            $TotalFailed++
        }
        Write-Host ""
    }

    # Summary
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Host "  Import Summary" -ForegroundColor Magenta
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Status "Successful imports: $TotalSuccess" -Type $(if ($TotalSuccess -gt 0) { "Success" } else { "Warning" })
    Write-Status "Failed imports: $TotalFailed" -Type $(if ($TotalFailed -gt 0) { "Error" } else { "Success" })
    Write-Host ""

    if ($TotalFailed -eq 0) {
        Write-Status "All objects imported successfully!" -Type "Success"
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Cyan
        Write-Host "  1. Open Kibana at $KibanaUrl" -ForegroundColor White
        Write-Host "  2. Navigate to Analytics > Dashboards" -ForegroundColor White
        Write-Host "  3. Open 'Bot Overview' or 'APM Service Overview'" -ForegroundColor White
        Write-Host ""
        Write-Host "  To set up alerting rules, run:" -ForegroundColor Cyan
        Write-Host "  ./Setup-AlertRules.ps1 -KibanaUrl `"$KibanaUrl`"" -ForegroundColor White
        Write-Host ""
    }
    else {
        Write-Status "Some imports failed. Check errors above and retry with -Overwrite if needed." -Type "Warning"
        exit 1
    }
}

# Run main
Main
