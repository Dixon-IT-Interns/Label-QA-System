Write-Host "Stopping old backend..."
Get-NetTCPConnection -LocalPort 5200 -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
Stop-ScheduledTask -TaskName "LabelQA-Backend" -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3
Write-Host "Creating Scheduled Task..."
$dotnet = "C:\Users\Dxnintents\.dotnet\dotnet.exe"
$dll = "C:\Deploy\LabelQA\backend\Dixon.LabelVerification.API.dll"
$action = New-ScheduledTaskAction -Execute $dotnet -Argument "`"$dll`" --urls http://0.0.0.0:5200" -WorkingDirectory "C:\Deploy\LabelQA\backend"
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddSeconds(5)
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName "LabelQA-Backend" -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null
Write-Host "Starting backend via Task Scheduler..."
Start-ScheduledTask -TaskName "LabelQA-Backend"
Start-Sleep -Seconds 5
Get-NetTCPConnection -LocalPort 5200 -ErrorAction SilentlyContinue | Select-Object LocalAddress, LocalPort, State
Write-Host "Deploy done!"
