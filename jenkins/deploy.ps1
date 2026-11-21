Write-Host "Stopping old backend..."
Get-Process -Name "Dixon.LabelVerification.API" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 3
Write-Host "Starting new backend..."
$backendPath = "C:\Deploy\LabelQA\backend\Dixon.LabelVerification.API.exe"
Start-Process -FilePath $backendPath -ArgumentList '--urls','http://0.0.0.0:5200' -WorkingDirectory "C:\Deploy\LabelQA\backend"
Write-Host "Deploy done!"