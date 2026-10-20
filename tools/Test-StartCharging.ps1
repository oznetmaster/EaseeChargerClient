[CmdletBinding()]
param([Parameter(Mandatory)][string]$Profile, [switch]$AuthorizeSingleStartCommand)
$ErrorActionPreference='Stop'
if(!$AuthorizeSingleStartCommand){throw 'Explicit authorization for one start-charging command is required.'}
$root=Split-Path -Parent $PSScriptRoot
$old=$env:EASEE_ENABLE_SINGLE_START_TEST
try {
    $env:EASEE_ENABLE_SINGLE_START_TEST='1'
    dotnet run --project (Join-Path $root 'EaseeChargerClient.TestConsole') -c Release -f net10.0-windows -- with-live-profile --profile $Profile -- dotnet test (Join-Path $root 'EaseeChargerClient.LiveTests') -c Release -f net10.0-windows --filter 'FullyQualifiedName=EaseeChargerClient.LiveTests.LiveStartChargingTests.OneAuthorizedStartCommandIsAcceptedAndTheSelectedChargerRemainsObservable' --settings (Join-Path $root 'Live.runsettings.example') --results-directory (Join-Path $root 'TestResults/LiveControl') --logger trx
    if($LASTEXITCODE){throw 'The authorized start-charging test did not pass; inspect its retained result before any retry.'}
} finally {$env:EASEE_ENABLE_SINGLE_START_TEST=$old}
