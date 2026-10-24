param([switch]$CoreOnly, [switch]$Demo)
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$env:DOTNET_CLI_HOME = Join-Path $root '.cache/dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_PACKAGES = Join-Path $root '.cache/nuget'
function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw ('dotnet执行失败，退出码：' + $LASTEXITCODE) }
}
Push-Location -LiteralPath $root
try {
    if (!(Test-Path -LiteralPath 'vendor/yixianpai-mod-sdk/sdk/net40/Yx.ModSdk.Core.dll')) { throw '先运行 scripts/restore-sdk.ps1。' }
    Invoke-Dotnet restore tests/PredictionChecks.csproj --configfile NuGet.Config
    Invoke-Dotnet run --project tests/PredictionChecks.csproj --no-restore
    & (Join-Path $PSScriptRoot 'check-interpreter.ps1')
    if ($Demo) { Invoke-Dotnet run --project tests/PredictionChecks.csproj --no-build -- --demo }
    if (!$CoreOnly -and !(Test-Path -LiteralPath 'refs/UnityEngine.UnityWebRequestModule.dll')) { throw '完整游戏版需要先运行 scripts/prepare-game.ps1。纯逻辑验证可用 -CoreOnly。' }
    Invoke-Dotnet restore mod/PredictionPoints.csproj --configfile NuGet.Config
    if ($CoreOnly) { Invoke-Dotnet build mod/PredictionPoints.csproj -c Release --no-restore -p:CoreOnly=true }
    else { Invoke-Dotnet build mod/PredictionPoints.csproj -c Release --no-restore }
    Invoke-Dotnet vendor/yixianpai-mod-sdk/tools/yx-patch.dll check --warn-as-error --manifest mod/manifest.json mod/plugins/PredictionPoints.dll
    if (!$CoreOnly) {
        Invoke-Dotnet vendor/yixianpai-mod-sdk/tools/yx-patch.dll pack mod --out dist
        # 上游pack只收manifest/plugins/assets/data；在同一包根补入使用说明。
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $manifest = Get-Content -LiteralPath 'mod/manifest.json' -Raw | ConvertFrom-Json
        $package = Join-Path $root ('dist/' + $manifest.id + '-' + $manifest.version + '.zip')
        $zip = [System.IO.Compression.ZipFile]::Open($package, [System.IO.Compression.ZipArchiveMode]::Update)
        try {
            $entryPath = $manifest.id + '/README.md'
            $previous = $zip.GetEntry($entryPath)
            if ($null -ne $previous) { $previous.Delete() }
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $root 'mod/README.md'), $entryPath) | Out-Null
        } finally { $zip.Dispose() }
    }
} finally { Pop-Location }
