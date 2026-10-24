$src = Join-Path $PSScriptRoot "..\mod"
$profiles = @("321", "默认")

foreach ($p in $profiles) {
    $dst = "C:\Users\zd117\AppData\Roaming\YxModManager\profiles\$p\mods\local.yxp.predictionpoints"
    if (Test-Path $dst) {
        Write-Host "Deploying to $dst..."
        Copy-Item (Join-Path $src "manifest.json") (Join-Path $dst "manifest.json") -Force
        Copy-Item (Join-Path $src "plugins\PredictionPoints.dll") (Join-Path $dst "plugins\PredictionPoints.dll") -Force
        Copy-Item (Join-Path $src "assets\*") (Join-Path $dst "assets\") -Recurse -Force
        Write-Host "Deployed to $p successfully."
    }
}

$cache = "C:\Users\zd117\AppData\Roaming\YxModManager\cache\patched"
if (Test-Path $cache) {
    Write-Host "Clearing patched cache in $cache..."
    Get-ChildItem -Path $cache | Remove-Item -Recurse -Force
    Write-Host "Patched cache cleared."
}
Write-Host "All done!"
