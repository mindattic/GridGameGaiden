# Headless verification gate.
#   pwsh tools/test.ps1            → core simulation + meta tests (dotnet, seconds)
#   pwsh tools/test.ps1 -Compile   → also a Unity batchmode compile check
#   pwsh tools/test.ps1 -Ui        → also build the player and run the 35-check UI gate (tools/uicheck.py)
param([switch]$Compile, [switch]$Ui)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
dotnet test "$root/Tests/Core.Tests/Core.Tests.csproj" --logger "console;verbosity=normal"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$env:PYTHONIOENCODING = "utf-8"
python "$PSScriptRoot/check_glyphs.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
python "$PSScriptRoot/check_assets.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if ($Compile) { & "$PSScriptRoot/unity.ps1" -Method GridGame.EditorTools.GaidenBuild.CompileCheck -Log compile; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }
if ($Ui) {
    & "$PSScriptRoot/build.ps1"; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    python "$PSScriptRoot/uicheck.py"; exit $LASTEXITCODE
}
