[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    foreach ($project in 'EaseeChargerClient','EaseeChargerClient.Hosting') {
        $file = "$project/$project.csproj"
        $suppressed = dotnet msbuild $file -getProperty:NoWarn -p:TargetFramework=net472
        if ($LASTEXITCODE) { throw 'Cannot inspect compiler warning settings.' }
        if (($suppressed -split '[;, ]+') -contains 'CS1591' -or ($suppressed -split '[;, ]+') -contains '1591') { throw "$project suppresses missing XML documentation warnings." }
        dotnet build $file -c $Configuration --no-incremental -p:TreatWarningsAsErrors=true -p:GenerateDocumentationFile=true
        if ($LASTEXITCODE) { throw "$project failed its strict documentation build." }
    }
    dotnet build tools/DocumentationAudit/DocumentationAudit.csproj -c $Configuration
    if ($LASTEXITCODE) { throw 'Documentation audit tool failed to build.' }
    $audit = "tools/DocumentationAudit/bin/$Configuration/net10.0/DocumentationAudit.dll"
    dotnet $audit --self-test
    if ($LASTEXITCODE) { throw 'Documentation audit negative controls failed.' }
    $reportDirectory = Join-Path $root 'artifacts/documentation'
    New-Item -ItemType Directory -Force $reportDirectory | Out-Null
    $report = dotnet $audit EaseeChargerClient EaseeChargerClient.Hosting Shared
    $exitCode = $LASTEXITCODE
    $report | Set-Content (Join-Path $reportDirectory 'coverage.json') -Encoding utf8
    if ($exitCode) { $report | Write-Host; throw 'Public XML documentation is incomplete.' }
    foreach ($pair in @(@('EaseeChargerClient','net10.0'),@('EaseeChargerClient','net472'),@('EaseeChargerClient.Hosting','net10.0-windows'),@('EaseeChargerClient.Hosting','net472'))) {
        $xmlPath = Join-Path $root "$($pair[0])/bin/$Configuration/$($pair[1])/$($pair[0]).xml"
        [xml]$xml = Get-Content -LiteralPath $xmlPath -Raw
        if (!$xml.doc.members.member.Count) { throw "No XML members in $xmlPath" }
    }
    $summary = $report | ConvertFrom-Json
    Write-Host "XML documentation passed: $($summary.Checked) public declarations; no missing required tags."
} finally { Pop-Location }
