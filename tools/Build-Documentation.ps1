[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet tool restore
    if ($LASTEXITCODE) { throw 'DocFX tool restore failed.' }
    dotnet restore EaseeChargerClient.slnx
    if ($LASTEXITCODE) { throw 'Solution restore failed.' }
    # These folders contain generated files only. Reject links and verify the
    # absolute target before recursive cleanup, so renamed APIs cannot linger.
    foreach ($relative in 'docfx_project/api','docfx_project/hosting','docfx_project/_site') {
        $target = [IO.Path]::GetFullPath((Join-Path $root $relative))
        $allowed = [IO.Path]::GetFullPath((Join-Path $root 'docfx_project')) + [IO.Path]::DirectorySeparatorChar
        if (!$target.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid documentation output path.' }
        if (Test-Path -LiteralPath $target) {
            if ((Get-Item -LiteralPath $target -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'A generated documentation directory must not be a link.' }
            Remove-Item -LiteralPath $target -Recurse -Force
        }
    }
    dotnet tool run docfx docfx_project/docfx.json --warningsAsErrors
    if ($LASTEXITCODE) { throw 'HTML documentation build failed.' }
    if (!(Test-Path docfx_project/_site/index.html)) { throw 'Documentation home page was not generated.' }
    Write-Host 'HTML documentation is ready in docfx_project/_site.'
} finally { Pop-Location }
