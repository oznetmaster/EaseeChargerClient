[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')][string]$Version)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    [xml]$props = Get-Content Directory.Build.props -Raw
    if ([string]$props.Project.PropertyGroup.Version -ne $Version) { throw 'The requested release version must match Directory.Build.props.' }
    # Use a unique staging directory so previous output can never enter a release.
    $stage = Join-Path $root ('artifacts/staging/' + [Guid]::NewGuid().ToString('N'))
    $release = Join-Path $root 'artifacts/release'
    if ((Test-Path $release) -and @(Get-ChildItem $release -File).Count) { throw 'artifacts/release already contains files. Move the earlier release outputs before staging a new release.' }
    New-Item -ItemType Directory -Force $stage,$release | Out-Null
    dotnet pack EaseeChargerClient/EaseeChargerClient.csproj -c Release -p:ContinuousIntegrationBuild=true -o $stage
    if ($LASTEXITCODE) { throw 'Library packaging failed.' }
    $package = Join-Path $stage "EaseeChargerClient.$Version.nupkg"
    & "$PSScriptRoot/Test-Package.ps1" -Package $package
    Copy-Item -LiteralPath $package -Destination $release
    $library = Join-Path $stage 'library'
    foreach ($framework in 'net10.0','net472') {
        $dest = Join-Path $library $framework
        New-Item -ItemType Directory -Force $dest | Out-Null
        foreach ($extension in 'dll','xml') { Copy-Item "EaseeChargerClient/bin/Release/$framework/EaseeChargerClient.$extension" $dest }
    }
    foreach ($file in 'README.md','CHANGELOG.md','RELEASE-NOTES.md','LICENSE') { Copy-Item $file $library }
    Compress-Archive -Path "$library/*" -DestinationPath (Join-Path $release "EaseeChargerClient-$Version-library.zip")
    foreach ($app in @(@{Project='EaseeChargerClient.App';Framework='net10.0-windows';Name='desktop'},@{Project='EaseeChargerClient.TestConsole';Framework='net10.0-windows';Name='console-net10'},@{Project='EaseeChargerClient.TestConsole';Framework='net472';Name='console-net472'})) {
        $output = Join-Path $stage $app.Name
        dotnet publish "$($app.Project)/$($app.Project).csproj" -c Release -f $app.Framework --self-contained false -o $output
        if ($LASTEXITCODE) { throw "Application publication failed: $($app.Name)" }
        foreach ($file in 'README.md','CHANGELOG.md','RELEASE-NOTES.md','LICENSE') { Copy-Item $file $output }
        Compress-Archive -Path "$output/*" -DestinationPath (Join-Path $release "EaseeChargerClient-$Version-$($app.Name).zip")
    }
    if (!(Test-Path docfx_project/_site/index.html)) { throw 'Build the HTML documentation before staging a release.' }
    Compress-Archive -Path docfx_project/_site/* -DestinationPath (Join-Path $release "EaseeChargerClient-$Version-documentation.zip")
    Copy-Item README.md,CHANGELOG.md,RELEASE-NOTES.md,LICENSE $release
    Get-ChildItem $release -File | Sort-Object Name | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name } | Set-Content (Join-Path $release 'SHA256SUMS.txt') -Encoding utf8
    Write-Host "Release assets staged locally in $release. No package or release was published."
} finally { Pop-Location }
