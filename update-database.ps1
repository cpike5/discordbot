# Update database with EF Core migrations
# Run from solution root directory

param(
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "Applying EF Core migrations..." -ForegroundColor Cyan

$args = @(
    "ef", "database", "update",
    "--project", "src/DiscordBot.Infrastructure",
    "--startup-project", "src/DiscordBot.Bot"
)

if ($Verbose) {
    $args += "--verbose"
}

dotnet @args

if ($LASTEXITCODE -eq 0) {
    Write-Host "Database updated successfully." -ForegroundColor Green
} else {
    Write-Host "Database update failed." -ForegroundColor Red
    exit $LASTEXITCODE
}
