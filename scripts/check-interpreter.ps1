$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cache = Join-Path $root '.cache/ILRuntime'
$archive = Join-Path $cache 'v2.1.0.zip'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
if (!(Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -Uri 'https://codeload.github.com/Ourpalm/ILRuntime/zip/refs/tags/v2.1.0' -OutFile $archive -TimeoutSec 90
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '4CED573A8BB842F3AD5576DFBDACD103DCDA2DC64F0B3F4B14563271048F951E') { throw 'ILRuntime源码归档哈希不符。' }
if (!(Test-Path -LiteralPath (Join-Path $cache 'source/ILRuntime-2.1.0/ILRuntime/Runtime/Enviorment/AppDomain.cs'))) {
    Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $cache 'source') -Force
}
$env:DOTNET_CLI_HOME = Join-Path $root '.cache/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $root '.cache/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
function Invoke-InterpreterDotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw ('解释器检查失败，退出码：' + $LASTEXITCODE) }
}
Push-Location -LiteralPath $root
try {
    Invoke-InterpreterDotnet restore tests/InterpreterProbe/InterpreterProbe.csproj --configfile NuGet.Config
    Invoke-InterpreterDotnet build tests/InterpreterProbe/InterpreterProbe.csproj -c Release --no-restore
    Invoke-InterpreterDotnet restore tools/InterpreterChecks/InterpreterChecks.csproj --configfile NuGet.Config
    Invoke-InterpreterDotnet run --no-restore -c Release --project tools/InterpreterChecks -- vendor/yixianpai-mod-sdk/sdk/net40/Yx.ModSdk.Core.dll tests/InterpreterProbe/bin/Release/net40/InterpreterProbe.dll
} finally { Pop-Location }
