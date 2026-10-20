[CmdletBinding()]
param([Parameter(Mandatory)][string]$Package)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Package))
try {
    foreach ($name in 'README.md','CHANGELOG.md','RELEASE-NOTES.md','LICENSE','lib/net10.0/EaseeChargerClient.dll','lib/net472/EaseeChargerClient.dll','lib/net10.0/EaseeChargerClient.xml','lib/net472/EaseeChargerClient.xml') {
        $entry = $archive.GetEntry($name)
        if (!$entry -or !$entry.Length) { throw "Missing or empty package file: $name" }
        if ($name.EndsWith('.xml')) {
            $reader = [IO.StreamReader]::new($entry.Open())
            try { [xml]$xml = $reader.ReadToEnd(); if (!$xml.doc.members.member.Count) { throw "Empty XML member list: $name" } }
            finally { $reader.Dispose() }
        }
    }
    $nuspec = @($archive.Entries | Where-Object FullName -Like '*.nuspec')
    if ($nuspec.Count -ne 1) { throw 'Expected one package manifest.' }
    $reader = [IO.StreamReader]::new($nuspec[0].Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    foreach ($dependency in $manifest.SelectNodes('//*[local-name()="dependency"]')) {
        if ($dependency.id -match 'NUnit|TestAdapter|Test[.]Sdk|Newtonsoft|log4net') { throw "Unexpected production dependency: $($dependency.id)" }
    }
    Write-Host 'Package documentation, framework assemblies and production dependencies passed.'
} finally { $archive.Dispose() }
