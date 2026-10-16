#Requires -Version 5.1
<#
.SYNOPSIS
    Shuts down any running GridGameGaiden, clears the release build output, rebuilds the Windows
    Release player from scratch, and publishes a standalone copy to C:\Apps\GridGameGaiden\ — same
    process UnityEve/Automata/KdpPublish/IdiotProof use to always have a fresh, independent deployed
    copy instead of a possibly-stale one.

.DESCRIPTION
    1. Stops any running GridGameGaiden.exe process launched from *this* repo's Build\ output or
       from the deployed copy at C:\Apps\GridGameGaiden\ — never another git worktree's dev player
       (GG-T21: Get-Process only sees the process name, so an unscoped Stop-Process -Force here
       would silently kill a sibling worktree's unrelated long-running session, indistinguishable
       to that session from an unexplained crash).
    2. Removes Build\WindowsRelease — the build OUTPUT only, never Library\ (Unity's asset
       import / script-compile cache, which survives exactly as it does for a normal
       Editor rebuild) — so the player build below is never a stale incremental copy.
    3. Runs tools\unity.ps1 -Method Gaiden.EditorTools.GaidenBuild.BuildWindowsRelease in Unity
       batch mode (requires the Editor closed) -> Build\WindowsRelease\GridGameGaiden.exe.
    4. Copies Build\WindowsRelease\* -> C:\Apps\GridGameGaiden\.

.PARAMETER Launch
    After publishing, immediately run the deployed GridGameGaiden.exe.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\deploy.ps1
    powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 -Launch
#>
param([switch]$Launch)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoDir  = Split-Path $PSScriptRoot   # tools\ -> GridGameGaiden\
$out      = 'C:\Apps\GridGameGaiden'
$exeName  = 'GridGameGaiden.exe'
$buildDir = Join-Path $repoDir 'Build\WindowsRelease'

# ── Stop running instance ──────────────────────────────────────────────────
Write-Host ''
Write-Host '  Stopping running instance...' -ForegroundColor Yellow
$procs = Get-Process 'GridGameGaiden' -ErrorAction SilentlyContinue | Where-Object {
    $exePath = $_.Path
    $exePath -and ($exePath.StartsWith($repoDir, [System.StringComparison]::OrdinalIgnoreCase) `
                   -or $exePath.StartsWith($out, [System.StringComparison]::OrdinalIgnoreCase))
}
if ($procs) {
    $procs | Stop-Process -Force
    Write-Host "    Stopped ($(@($procs).Count) process(es))" -ForegroundColor DarkYellow
    Start-Sleep -Milliseconds 800
} else {
    Write-Host '    Nothing running.' -ForegroundColor DarkGray
}

# ── Clear build output ───────────────────────────────────────────────────────
# Build\WindowsRelease only — never Library\, which is Unity's import/compile cache, not
# build output, and must survive a redeploy exactly as it survives a normal rebuild.
Write-Host ''
Write-Host '  Clearing build output (Build\WindowsRelease)...' -ForegroundColor Cyan
if (Test-Path $buildDir) {
    Remove-Item -Recurse -Force $buildDir
}

# ── Build ────────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '  Building Windows Release player (Unity batch mode)...' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'unity.ps1') -Method Gaiden.EditorTools.GaidenBuild.BuildWindowsRelease -Log deploy-build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed (exit $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

$builtExe = Join-Path $buildDir $exeName
if (-not (Test-Path $builtExe)) {
    Write-Host "Build completed but $exeName not found at $builtExe." -ForegroundColor Red
    exit 1
}

# ── Publish ─────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host "  Publishing $builtExe" -ForegroundColor Cyan
Write-Host "    -> $out\$exeName"

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item -Recurse -Force (Join-Path $buildDir '*') $out

$exe = Join-Path $out $exeName
if (-not (Test-Path $exe)) {
    Write-Host "Publish completed but $exeName not found at $exe." -ForegroundColor Red
    exit 1
}

# ── Summary ────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '  Published successfully.' -ForegroundColor Green
Write-Host "    Exe : $exe ($([math]::Round(((Get-Item $exe).Length / 1MB), 1)) MB)" -ForegroundColor Gray
Write-Host ''

if ($Launch) {
    Write-Host '  Launching...' -ForegroundColor Cyan
    Start-Process $exe
}
