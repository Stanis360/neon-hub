# NeonMonitor entfernen (Programm + Verknüpfungen). Einstellungen/Aufzeichnungen bleiben erhalten.
$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}
Get-Process -Name NeonMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
Remove-Item (Join-Path $env:LOCALAPPDATA 'NeonMonitor\App') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) 'NeonMonitor.lnk') -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\NeonMonitor.lnk') -ErrorAction SilentlyContinue
Write-Host 'NeonMonitor wurde entfernt.' -ForegroundColor Green
Write-Host 'Einstellungen: %AppData%\NeonMonitor  ·  Berichte/Sitzungen: Dokumente\NeonMonitor'
Start-Sleep -Seconds 4
