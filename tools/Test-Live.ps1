[CmdletBinding()]
param([Parameter(Mandatory)][string]$Profile, [ValidateSet('net10.0-windows','net472')][string]$Framework='net10.0-windows')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$console = Join-Path $root 'EaseeChargerClient.TestConsole/EaseeChargerClient.TestConsole.csproj'
$tests = Join-Path $root 'EaseeChargerClient.LiveTests/EaseeChargerClient.LiveTests.csproj'
dotnet run --project $console -c Release -f net10.0-windows -- with-live-profile --profile $Profile -- dotnet test $tests -c Release -f $Framework --filter 'TestCategory=Live&TestCategory!=LiveControl' --settings (Join-Path $root 'Live.runsettings.example') --results-directory (Join-Path $root 'TestResults/Live') --logger trx
if ($LASTEXITCODE) { throw 'Live NUnit suites failed.' }
