# Builds the Windows development player (with the automation endpoint compiled in).
#   pwsh tools/build.ps1 [-Release] [-Target Windows|Android|WebGL]
param([switch]$Release, [string]$Target = "Windows")
$method = switch ($Target) {
    "Android" { "Gaiden.EditorTools.GaidenBuild.BuildAndroid" }
    "WebGL" { "Gaiden.EditorTools.GaidenBuild.BuildWebGL" }
    default { if ($Release) { "Gaiden.EditorTools.GaidenBuild.BuildWindowsRelease" } else { "Gaiden.EditorTools.GaidenBuild.BuildWindowsDev" } }
}
& "$PSScriptRoot/unity.ps1" -Method $method -Log "build-$Target"
exit $LASTEXITCODE
