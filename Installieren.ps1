# =====================================================================
#  NeonMonitor – Installation / Update
#
#  Funktioniert in beiden Fällen:
#   * fertige Release-ZIP von GitHub (NeonMonitor.exe liegt daneben) → kopieren
#   * Quellcode (Ordner src\ vorhanden)                               → bauen
#  Ziel: %LOCALAPPDATA%\NeonMonitor\App, Verknüpfungen auf Desktop und im Startmenü.
#  Einstellungen und Aufnahmen bleiben bei Updates erhalten.
# =====================================================================
param([switch]$FromUpdate)   # vom Programm selbst gestartet (Update-Knopf)
$ErrorActionPreference = 'Stop'

function Pause-AndExit([int]$code) {
    Write-Host ''
    if (-not $FromUpdate -or $code -ne 0) { Read-Host 'Enter drücken zum Schließen' }
    exit $code
}

# --- 1. Adminrechte holen (nötig, um ein laufendes NeonMonitor zu beenden) ---
$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $argsList = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    if ($FromUpdate) { $argsList += ' -FromUpdate' }
    Start-Process powershell.exe -Verb RunAs -ArgumentList $argsList
    exit
}

Set-Location $PSScriptRoot
$target = Join-Path $env:LOCALAPPDATA 'NeonMonitor\App'
$fromSource = Test-Path 'src\NeonMonitor\NeonMonitor.csproj'
$fromRelease = Test-Path (Join-Path $PSScriptRoot 'NeonMonitor.exe')

Write-Host ''
Write-Host '  NeonMonitor wird installiert …' -ForegroundColor Green
Write-Host ''

if (-not $fromSource -and -not $fromRelease) {
    Write-Host 'Weder Quellcode noch NeonMonitor.exe gefunden – bitte die ZIP vollständig entpacken.' -ForegroundColor Red
    Pause-AndExit 1
}

# --- 2. Laufendes NeonMonitor sauber beenden ---
#     Erst höflich bitten (gibt Lüfter ans Mainboard zurück, schließt Aufnahmen ab), notfalls hart beenden.
$running = Get-Process -Name NeonMonitor -ErrorAction SilentlyContinue
if ($running) {
    Write-Host 'Beende laufendes NeonMonitor …'
    try {
        $exitEvent = [System.Threading.EventWaitHandle]::OpenExisting('Local\NeonMonitor.Exit')
        [void]$exitEvent.Set()
    } catch { }
    $running | Wait-Process -Timeout 15 -ErrorAction SilentlyContinue
    Get-Process -Name NeonMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
}

if ($fromSource) {
    # --- 3a. Aus dem Quellcode bauen ---
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Host 'Das .NET 10 SDK fehlt. Bitte zuerst ausführen:' -ForegroundColor Red
        Write-Host '    winget install Microsoft.DotNet.SDK.10'
        Write-Host 'Oder statt des Quellcodes die fertige ZIP unter „Releases“ auf GitHub verwenden.'
        Pause-AndExit 1
    }
    dotnet publish 'src\NeonMonitor\NeonMonitor.csproj' -c Release -r win-x64 --self-contained false -o $target --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Host ''
        Write-Host 'Build fehlgeschlagen – bitte die Meldungen oben an Claude schicken.' -ForegroundColor Red
        Pause-AndExit 1
    }
}
else {
    # --- 3b. Fertige Release-Dateien kopieren ---
    $selfContained = Test-Path (Join-Path $PSScriptRoot 'coreclr.dll')
    if (-not $selfContained) {
        # Diese Variante braucht die .NET 10 Desktop Runtime
        $hasRuntime = $false
        if (Get-Command dotnet -ErrorAction SilentlyContinue) {
            $hasRuntime = (dotnet --list-runtimes | Select-String 'Microsoft.WindowsDesktop.App 10\.') -ne $null
        }
        if (-not $hasRuntime) {
            Write-Host 'Die .NET 10 Desktop Runtime fehlt.' -ForegroundColor Yellow
            $answer = Read-Host 'Jetzt automatisch installieren (winget)? [J/n]'
            if ($answer -eq '' -or $answer -match '^[jJyY]') {
                winget install --id Microsoft.DotNet.DesktopRuntime.10 -e --accept-source-agreements --accept-package-agreements
            } else {
                Write-Host 'Alternativ die „-portable.zip“ verwenden – sie enthält .NET bereits.'
                Pause-AndExit 1
            }
        }
    }
    if (Test-Path $target) { Remove-Item (Join-Path $target '*') -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -Path (Join-Path $PSScriptRoot '*') -Destination $target -Recurse -Force
}

# --- 4. Verknüpfungen anlegen ---
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

# --- 5. PawnIO-Hinweis ---
$pawn = Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO'
if (-not $pawn) {
    Write-Host ''
    Write-Host 'Hinweis: PawnIO-Treiber fehlt – CPU-Temperatur/-Leistung und Mainboard-Lüfter bleiben leer.' -ForegroundColor Yellow
    Write-Host '    winget install namazso.PawnIO'
}

Write-Host ''
Write-Host '  Fertig! NeonMonitor startet jetzt.' -ForegroundColor Green
Write-Host '  Künftig einfach das Symbol "NeonMonitor" auf dem Desktop doppelklicken.'
Start-Process $exe
Start-Sleep -Seconds 3
