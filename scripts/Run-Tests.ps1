[CmdletBinding()]
param(
    # Extra arguments for the regression runner (e.g. --stress or a shots folder).
    [string[]]$TestArgs = @()
)
# Builds and runs the regression suite in a new folder under work\test-runs, so a running Lumen HUD
# (which locks src\Pulsewave\bin and the tests' usual bin folder) never blocks a test build.
# Nothing under src is written apart from obj\ (not locked by a running app). Old runs can be deleted by hand.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$source = Join-Path $root 'src'
$run = Join-Path $root ('work\test-runs\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
$bin = Join-Path $run 'bin'
$output = Join-Path $run 'output'
New-Item -ItemType Directory -Force -Path $bin, $output | Out-Null

& dotnet build (Join-Path $source 'Tests\RegressionChecks.csproj') -c Release -o $bin -nologo -v:minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

$oldSource = $env:PULSEWAVE_TEST_SOURCE_ROOT; $oldOutput = $env:PULSEWAVE_TEST_OUTPUT_DIR; $oldTmp = $env:TMP
try {
    # The suite finds the source tree from this variable instead of its own folder's position.
    $env:PULSEWAVE_TEST_SOURCE_ROOT = $source
    $env:PULSEWAVE_TEST_OUTPUT_DIR = $output
    $env:TMP = $output
    & (Join-Path $bin 'RegressionChecks.exe') @TestArgs
    $code = $LASTEXITCODE
} finally {
    $env:PULSEWAVE_TEST_SOURCE_ROOT = $oldSource; $env:PULSEWAVE_TEST_OUTPUT_DIR = $oldOutput; $env:TMP = $oldTmp
}
Write-Host "Test run folder: $run"
exit $code
