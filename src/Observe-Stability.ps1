# Observe the published app during a real gaming/sleep/display recovery session.
# Read-only: does not start/stop Pulsewave or change settings/monitor topology.
param([ValidateRange(1,1440)][int]$Minutes=240,[ValidateRange(5,300)][int]$IntervalSeconds=30)
$ErrorActionPreference='Stop'
$report=Join-Path $PSScriptRoot ('stability-observation-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.csv')
$instances=@(Get-Process -Name Pulsewave -ErrorAction SilentlyContinue)
if($instances.Count -ne 1) { throw 'Run exactly one published Pulsewave.exe before starting this observer.' }
$observedId=$instances[0].Id
Write-Host "Observing Pulsewave process $observedId. Report: $report. Ctrl+C stops observation."
$deadline=(Get-Date).AddMinutes($Minutes)
while((Get-Date) -lt $deadline) {
    $observed=Get-Process -Id $observedId -ErrorAction SilentlyContinue
    if($null -eq $observed) { throw "Pulsewave exited. Review the last entries in $report." }
    [pscustomobject]@{Time=(Get-Date).ToString('o');ProcessId=$observedId;CpuSeconds=$observed.CPU;PrivateBytes=$observed.PrivateMemorySize64;WorkingSetBytes=$observed.WorkingSet64;Handles=$observed.HandleCount;Threads=$observed.Threads.Count} | Export-Csv -LiteralPath $report -NoTypeInformation -Append
    Start-Sleep -Seconds $IntervalSeconds
}
Write-Host "Observation complete: $report"
