#!/usr/bin/env pwsh
# Build documentation using DocFX

param(
    [switch]$Serve,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

Write-Host "Discord Bot Documentation Builder" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

$solutionRoot = $PSScriptRoot
Set-Location $solutionRoot

Write-Host "`nRestoring .NET tools..." -ForegroundColor Yellow
dotnet tool restore

if ($Clean) {
    Write-Host "`nCleaning previous build..." -ForegroundColor Yellow
    if (Test-Path "_site") {
        Remove-Item "_site" -Recurse -Force
    }
    if (Test-Path "api") {
        Get-ChildItem "api" -Filter "*.yml" | Remove-Item -Force
    }
}

Write-Host "`nBuilding solution..." -ForegroundColor Yellow
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

if ($Serve) {
    Write-Host "`nBuilding and serving documentation..." -ForegroundColor Yellow
    dotnet docfx docfx.json --serve
} else {
    Write-Host "`nBuilding documentation..." -ForegroundColor Yellow
    dotnet docfx docfx.json

    if ($LASTEXITCODE -eq 0) {
        Write-Host "`nDocumentation built successfully!" -ForegroundColor Green
        Write-Host "Output: $solutionRoot\_site" -ForegroundColor Cyan
        Write-Host "`nTo serve locally, run: .\build-docs.ps1 -Serve" -ForegroundColor Gray
    }
}
