$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    python tools/generate.py
    if ($LASTEXITCODE) { throw 'API generation failed.' }
    dotnet format whitespace EaseeChargerClient.slnx --no-restore --include-generated
    if ($LASTEXITCODE) { throw 'Formatting failed.' }
} finally { Pop-Location }
