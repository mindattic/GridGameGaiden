#Requires -Version 5.1
<#
.SYNOPSIS
    Creates (or refreshes) a Desktop shortcut that launches the published GridGameGaiden build
    directly from C:\Apps\GridGameGaiden\GridGameGaiden.exe, with no rebuild step.

.DESCRIPTION
    This is the "/run" counterpart to deploy.bat's "/deploy": deploy.bat (-> tools\deploy.ps1)
    builds and publishes; this shortcut just opens whatever is currently published. Re-run this
    script any time (e.g. after moving the Desktop folder) to recreate the shortcut — it always
    points at the fixed path below, so a redeploy never invalidates it.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\make-desktop-shortcut.ps1
#>
$ErrorActionPreference = 'Stop'

$exe = 'C:\Apps\GridGameGaiden\GridGameGaiden.exe'
$lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'GridGameGaiden.lnk'

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($lnk)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = Split-Path $exe
$shortcut.IconLocation = $exe
$shortcut.Description = 'Launch the published GridGameGaiden build'
$shortcut.Save()

Write-Host "Shortcut created: $lnk -> $exe" -ForegroundColor Green
if (-not (Test-Path $exe)) {
    Write-Host "Note: $exe doesn't exist yet - run deploy.bat (or /deploy) first." -ForegroundColor Yellow
}
