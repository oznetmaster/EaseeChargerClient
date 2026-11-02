$started = [datetime]::UtcNow
./tools/Test-Offline.ps1
./build/Verify-OfflineResults.ps1 -NotBefore $started
