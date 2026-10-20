[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet test EaseeChargerClient.slnx -c $Configuration --filter 'TestCategory!=Live' --settings Offline.runsettings --results-directory (Join-Path $root 'TestResults') --logger trx
    if ($LASTEXITCODE) { throw 'Offline NUnit suites failed.' }
} finally { Pop-Location }
