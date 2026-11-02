# Copyright (c) 2026 Neil Colvin. MIT License; see ../LICENSE.
[CmdletBinding()]
param(
    [string]$ResultsDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'TestResults'),
    [datetime]$NotBefore = [datetime]::MinValue
)
$ErrorActionPreference = 'Stop'
$expected = @{}
foreach ($framework in @('net472','net10.0')) { $expected["EaseeChargerClient.Tests/$framework"] = $false }
foreach ($project in @('EaseeChargerClient.Hosting.Tests','EaseeChargerClient.TestConsole.Tests')) {
    foreach ($framework in @('net472','net10.0-windows')) { $expected["$project/$framework"] = $false }
}
$expected['EaseeChargerClient.App.Tests/net10.0-windows'] = $false
$verified = Join-Path $ResultsDirectory 'OfflineVerified'
New-Item -ItemType Directory -Path $verified -Force | Out-Null
$files = @(Get-ChildItem -LiteralPath $ResultsDirectory -Filter *.trx -File | Where-Object { $_.LastWriteTimeUtc -ge $NotBefore.ToUniversalTime() })
foreach ($file in $files) {
    [xml]$trx = Get-Content -LiteralPath $file.FullName -Raw
    $summary = $trx.TestRun.ResultSummary
    $counters = $summary.Counters
    if ($summary.outcome -ne 'Completed' -or [long]$counters.failed -ne 0) { throw "Unsuccessful result: $($file.Name)" }
    if ([long]$counters.total -eq 0) {
        # The unchanged solution-wide offline command also discovers the excluded LiveTests assembly.
        # This is an exclusion record, never evidence of an executed/passed test.
        $text = [string]$summary.Output.StdOut
        if ([long]$counters.executed -ne 0 -or [long]$counters.passed -ne 0 -or
            $text -notmatch 'EaseeChargerClient\.LiveTests[/\\]bin[/\\]Release[/\\](net472|net10\.0-windows)[/\\]EaseeChargerClient\.LiveTests\.dll' -or
            [string]$summary.RunInfos.RunInfo.Text -notmatch 'No test matches the given testcase filter `TestCategory!=Live`') {
            throw "Unexpected empty test report: $($file.Name)"
        }
        continue
    }
    if ([long]$counters.passed -le 0 -or [long]$counters.executed -ne [long]$counters.passed -or [long]$counters.total -lt [long]$counters.executed) { throw "Incomplete test report: $($file.Name)" }
    $sources = @($trx.TestRun.TestDefinitions.UnitTest | ForEach-Object { $_.storage.ToLowerInvariant().Replace('\','/') } | Select-Object -Unique)
    if ($sources.Count -ne 1 -or $sources[0] -notmatch '/([^/]+)/bin/release/([^/]+)/[^/]+\.dll$') { throw "Unexpected test source: $($file.Name)" }
    $key = "$($Matches[1])/$($Matches[2])"
    if (!$expected.ContainsKey($key) -or $expected[$key]) { throw "Unexpected or duplicate offline suite: $key" }
    $expected[$key] = $true
    Move-Item -LiteralPath $file.FullName -Destination (Join-Path $verified ($key.Replace('/','-')+'.trx')) -Force
}
$missing = @($expected.Keys | Where-Object { !$expected[$_] })
if ($missing.Count) { throw "Missing fresh offline suites: $($missing -join ', ')" }
Write-Output 'Verified all seven offline project/framework suites; live-only empty reports are retained separately.'
