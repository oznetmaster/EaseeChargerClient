[xml]$props = Get-Content Directory.Build.props -Raw
./tools/Test-Package.ps1 -Package "artifacts/nuget/EaseeChargerClient.$($props.Project.PropertyGroup.Version).nupkg"
