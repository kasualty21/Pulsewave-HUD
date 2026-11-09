# Pulsewave System Monitor v0.8.0 - run in Windows PowerShell, NO administrator required.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Pulsewave\Pulsewave.csproj'
$destination = Join-Path $PSScriptRoot 'publish-pulsewave-win-x64-v0.8.0'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host 'The .NET 10 SDK is not installed or is not on PATH.' -ForegroundColor Yellow
    Write-Host 'Install it with: winget install Microsoft.DotNet.SDK.10'
    exit 1
}

Write-Host 'Publishing Pulsewave System Monitor as a self-contained Windows x64 executable...'
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $destination
if ($LASTEXITCODE -ne 0) { throw 'Publish failed. See compiler errors above.' }

Write-Host ''
Write-Host 'Build complete.' -ForegroundColor Green
Write-Host "Launch: $destination\Pulsewave.exe" -ForegroundColor Cyan
Write-Host 'No Rainmeter or .NET runtime installation is needed on the destination PC (Windows 10 1809 or later).'





