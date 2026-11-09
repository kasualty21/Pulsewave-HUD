# Development build. Requires .NET 10 SDK on Windows.
$ErrorActionPreference = 'Stop'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host 'Install .NET SDK: winget install Microsoft.DotNet.SDK.10'
    exit 1
}
dotnet run --project (Join-Path $PSScriptRoot 'Pulsewave\Pulsewave.csproj') -c Debug
