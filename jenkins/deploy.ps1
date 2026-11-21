Write-Host "Stopping old backend..."
Get-NetTCPConnection -LocalPort 5200 -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
Get-Process -Name "Dixon.LabelVerification.API" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 3

Write-Host "Starting new backend..."
$dotnet = "C:\Users\Dxnintents\.dotnet\dotnet.exe"
$dll = "C:\Deploy\LabelQA\backend\Dixon.LabelVerification.API.dll"
Start-Process -FilePath $dotnet -ArgumentList "`"$dll`" --urls http://0.0.0.0:5200" -WorkingDirectory "C:\Deploy\LabelQA\backend"
Write-Host "Deploy done!"