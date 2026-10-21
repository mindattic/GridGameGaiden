# Builds the Windows development player (with the automation endpoint compiled in).
#   pwsh tools/build.ps1 [-Release] [-Target Windows|Android|WebGL]
param(
    [switch]$Release,
    [ValidateSet("Windows", "Android", "WebGL")]
    [string]$Target = "Windows"
)
$method = switch ($Target) {
    "Android" { "GridGame.EditorTools.GaidenBuild.BuildAndroid" }
    "WebGL" { "GridGame.EditorTools.GaidenBuild.BuildWebGL" }
    "Windows" { if ($Release) { "GridGame.EditorTools.GaidenBuild.BuildWindowsRelease" } else { "GridGame.EditorTools.GaidenBuild.BuildWindowsDev" } }
}
& "$PSScriptRoot/unity.ps1" -Method $method -Log "build-$Target"
exit $LASTEXITCODE
