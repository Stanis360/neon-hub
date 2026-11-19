# =====================================================================
#  NeonMonitor – Installation / Update
#  Baut das Programm, kopiert es nach %LOCALAPPDATA%\NeonMonitor\App
#  und legt Verknüpfungen auf dem Desktop und im Startmenü an.
#  Für Updates einfach erneut ausführen (Einstellungen bleiben erhalten).
# =====================================================================
$ErrorActionPreference = 'Stop'

function Pause-AndExit([int]$code) {
    Write-Host ''
    Read-Host 'Enter drücken zum Schließen'
    exit $code
}

# --- 1. Adminrechte holen (nötig, um ein laufendes NeonMonitor zu beenden) ---
$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}

Set-Location $PSScriptRoot
Write-Host ''
Write-Host '  NeonMonitor wird installiert …' -ForegroundColor Green
Write-Host ''

# --- 2. .NET SDK vorhanden? ---
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host 'Das .NET 10 SDK fehlt. Bitte zuerst ausführen:' -ForegroundColor Red
    Write-Host '    winget install Microsoft.DotNet.SDK.10'
    Write-Host 'Danach dieses Skript erneut starten.'
    Pause-AndExit 1
}

# --- 3. Laufendes NeonMonitor beenden (sonst ist die EXE gesperrt) ---
$running = Get-Process -Name NeonMonitor -ErrorAction SilentlyContinue
if ($running) {
    Write-Host 'Beende laufendes NeonMonitor …'
    $running | Stop-Process -Force
    Start-Sleep -Seconds 1
}

# --- 4. Bauen und veröffentlichen ---
$target = Join-Path $env:LOCALAPPDATA 'NeonMonitor\App'
dotnet publish 'src\NeonMonitor\NeonMonitor.csproj' -c Release -r win-x64 --self-contained false -o $target --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host ''
    Write-Host 'Build fehlgeschlagen – bitte die Meldungen oben an Claude schicken.' -ForegroundColor Red
    Pause-AndExit 1
}

# --- 5. Verknüpfungen anlegen ---
$exe = Join-Path $target 'NeonMonitor.exe'
$shell = New-Object -ComObject WScript.Shell
$places = @(
    [Environment]::GetFolderPath('Desktop'),
    (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs')
)
foreach ($dir in $places) {
    $link = $shell.CreateShortcut((Join-Path $dir 'NeonMonitor.lnk'))
    $link.TargetPath = $exe
    $link.WorkingDirectory = $target
    $link.IconLocation = "$exe,0"
    $link.Description = 'Hardware-Überwachung'
    $link.Save()
}

# --- 6. PawnIO-Hinweis ---
$pawn = Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO'
if (-not $pawn) {
    Write-Host ''
    Write-Host 'Hinweis: PawnIO-Treiber fehlt – CPU-Temperatur/-Leistung bleiben leer.' -ForegroundColor Yellow
    Write-Host '    winget install namazso.PawnIO'
}

Write-Host ''
Write-Host '  Fertig! NeonMonitor startet jetzt.' -ForegroundColor Green
Write-Host '  Künftig einfach das Symbol "NeonMonitor" auf dem Desktop doppelklicken.'
Start-Process $exe
Start-Sleep -Seconds 4
