[CmdletBinding()]
param(
    [string]$SourcePath = '',
    [string]$EvidenceDirectory = '',
    [switch]$CreateSourceArchive
)
$ErrorActionPreference = 'Stop'
$scriptDirectory = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDirectory) -and -not [string]::IsNullOrWhiteSpace($PSCommandPath)) { $scriptDirectory = Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrWhiteSpace($scriptDirectory) -and $MyInvocation.MyCommand.Path) { $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path }
if ([string]::IsNullOrWhiteSpace($scriptDirectory)) { throw 'Cannot resolve script location; invoke Check-Release.ps1 with -File.' }
if ([string]::IsNullOrWhiteSpace($SourcePath)) { $SourcePath = Join-Path $scriptDirectory '..\src' }
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) { $EvidenceDirectory = Join-Path $scriptDirectory ('..\work\release-check-' + [guid]::NewGuid().ToString('N')) }
$source = (Resolve-Path -LiteralPath $SourcePath).Path
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
if ($evidence.StartsWith($source.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or $evidence -eq $source) { throw 'Evidence directory must be outside the source tree.' }
if ($evidence -match '(?i)[\\/]releases([\\/]|$)') { throw 'Immutable releases directories cannot be used for staging.' }
if (Test-Path -LiteralPath $evidence) { throw 'Use a new evidence directory; existing evidence is never overwritten.' }
New-Item -ItemType Directory -Path $evidence | Out-Null
$stage = Join-Path $evidence 'source'
New-Item -ItemType Directory -Path $stage | Out-Null
$excluded = @('bin','obj','publish-win-x64','publish-pulsewave-win-x64-v0.7.1','publish-pulsewave-win-x64-v0.8.0','.git','work','releases')
$generated = @('layout-preview.png','settings-preview.png','corrupt-palette.png','soak-report.json','soak-session.csv')
$manifest = @()
foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -File) {
    $relative = $file.FullName.Substring($source.Length).TrimStart('\','/')
    if (@($relative -split '[\\/]' | Where-Object { $_ -in $excluded }).Count -or $file.Name -in $generated -or $file.Name -like 'stability-observation-*.csv') { continue }
    $target = Join-Path $stage $relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target
    $manifest += [pscustomobject]@{ Path=$relative; SHA256=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash }
}
$manifest | Sort-Object Path | Export-Csv -NoTypeInformation -LiteralPath (Join-Path $evidence 'source-manifest.csv')
$project = Join-Path $stage 'Pulsewave\Pulsewave.csproj'
$tests = Join-Path $stage 'Tests\RegressionChecks.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$version = [string]$projectXml.Project.PropertyGroup.Version
if (-not $version) { throw 'Source project has no explicit version.' }
$steps = [Collections.Generic.List[object]]::new()
function Invoke-Dotnet([string]$Name, [string[]]$Arguments) {
    $log = Join-Path $evidence ($Name + '.log')
    & dotnet @Arguments 2>&1 | Tee-Object -FilePath $log | Out-Host
    $code = $LASTEXITCODE
    $steps.Add([pscustomobject]@{Name=$Name;ExitCode=$code;Log=$log})
    if ($code -ne 0) { throw "$Name failed with exit code $code. See $log" }
}
$oldSource = $env:PULSEWAVE_TEST_SOURCE_ROOT
$oldOutput = $env:PULSEWAVE_TEST_OUTPUT_DIR
$status = 'Failed'
$binary = $null
$archive = $null
try {
    & dotnet --info 2>&1 | Out-File (Join-Path $evidence 'dotnet-info.txt')
    if ($LASTEXITCODE -ne 0) { throw 'dotnet SDK discovery failed.' }
    Invoke-Dotnet 'build' @('build',$project,'-c','Release')
    $env:PULSEWAVE_TEST_SOURCE_ROOT = $stage
    $env:PULSEWAVE_TEST_OUTPUT_DIR = Join-Path $evidence 'test-artifacts'
    Invoke-Dotnet 'regression' @('run','--project',$tests,'-c','Release')
    $publish = Join-Path $evidence 'publish-win-x64'
    Invoke-Dotnet 'publish' @('publish',$project,'-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true','-p:DebugType=None','-p:DebugSymbols=false','-o',$publish)
    $exe = Join-Path $publish 'Pulsewave.exe'
    if (-not (Test-Path -LiteralPath $exe)) { throw 'Published executable missing.' }
    $exeVersion = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
    if (($exeVersion -split '\+')[0] -ne $version) { throw "Published version $exeVersion differs from source $version." }
    # Packaging integrity: the staged input is unchanged by compilation and tests.
    foreach ($entry in $manifest) {
        if ((Get-FileHash -LiteralPath (Join-Path $stage $entry.Path) -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "Staged source changed: $($entry.Path)" }
    }
    $binary = [pscustomobject]@{Path=$exe;ProductVersion=$exeVersion;SHA256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash}
    $archive = $null
    if ($CreateSourceArchive) {
        $archivePath = Join-Path $evidence ("Pulsewave-v$version-source-candidate.zip")
        $package = Join-Path $evidence 'package'
        New-Item -ItemType Directory -Path $package | Out-Null
        foreach ($entry in $manifest) {
            $target = Join-Path $package $entry.Path
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
            Copy-Item -LiteralPath (Join-Path $stage $entry.Path) -Destination $target
        }
        Compress-Archive -Path (Join-Path $package '*') -DestinationPath $archivePath
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [IO.Compression.ZipFile]::OpenRead($archivePath)
        try {
            $zipFiles = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
            foreach ($item in $zip.Entries) {
                $canonicalName = $item.FullName.Replace([char]92,[char]47)
                if ($canonicalName.EndsWith('/')) { continue }
                if ($zipFiles.ContainsKey($canonicalName)) { throw "Duplicate archive path: $canonicalName" }
                $zipFiles.Add($canonicalName, $item)
            }
            if ($zipFiles.Count -ne $manifest.Count) { throw 'Archive file count mismatch.' }
            foreach ($entry in $manifest) {
                $canonicalName = $entry.Path.Replace([char]92,[char]47)
                $zipEntry = $null
                if ($zipFiles.ContainsKey($canonicalName)) { $zipEntry = $zipFiles[$canonicalName] }
                if ($null -eq $zipEntry) { throw "Archive missing $($entry.Path)" }
                $stream = $zipEntry.Open()
                $sha = [Security.Cryptography.SHA256]::Create()
                try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') } finally { $sha.Dispose(); $stream.Dispose() }
                if ($hash -ne $entry.SHA256) { throw "Archive checksum mismatch: $($entry.Path)" }
            }
        } finally { $zip.Dispose() }
        $archive = [pscustomobject]@{Path=$archivePath;SHA256=(Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash}
    }
    $status = 'Passed'
} finally {
    $env:PULSEWAVE_TEST_SOURCE_ROOT = $oldSource
    $env:PULSEWAVE_TEST_OUTPUT_DIR = $oldOutput
    [pscustomobject]@{Status=$status;Utc=[DateTime]::UtcNow.ToString('o');Source=$source;StagedSource=$stage;Version=$version;Steps=@($steps.ToArray());Binary=$binary;Archive=$archive;HardwareAcceptance='Not performed by this script';Scope='Isolated build, regression, app-owned renders and publish. No overlay launch or live settings writes.'} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence 'evidence.json')
}
Write-Host "Release checks passed. Evidence: $evidence"
