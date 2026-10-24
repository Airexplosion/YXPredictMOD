param([string]$Destination = (Join-Path $PSScriptRoot '..\vendor\yixianpai-mod-sdk'))
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$revision = '744137755d7daa60a8e9e4492bb7bcecad92fa76'
$files = @(
    @{ Path = '.gitignore'; GitHash = '76cd8bc9948d7a3ff3ffaf53adc431570b12207e' }
    @{ Path = 'Directory.Build.props'; GitHash = '51265ab0e66208258828ef660b6d52e12e1dad42' }
    @{ Path = 'LICENSE'; GitHash = '93811fda32c4b0e416b1fe591c6062e27384a481' }
    @{ Path = 'README.md'; GitHash = '130765abb25995ab9cb0e2a802d426521946447e' }
    @{ Path = 'docs/getting-started.md'; GitHash = '3e905e8f17924f408078f94c43032be22d352da0' }
    @{ Path = 'docs/hooks.md'; GitHash = '79d5a1157e99c42a0e66a4709fe433750d217b24' }
    @{ Path = 'docs/ilruntime-rules.md'; GitHash = '940b30ba0b38d05633912242ae10c5a271f120e3' }
    @{ Path = 'docs/manifest.md'; GitHash = 'a5a3991e181f16bf7b4877f2e9aa7f8f3df789ed' }
    @{ Path = 'docs/sdk-reference.md'; GitHash = '1fbe917361e74d1b98531413db561276727a340a' }
    @{ Path = 'docs/testing.md'; GitHash = '2ec0893613efcc31d22a81f56e008857786b3baa' }
    @{ Path = 'docs/two-layer-model.md'; GitHash = 'ef3d1927493bfa3f61c82ff0c1092d44555c6a10' }
    @{ Path = 'examples/hello/Hello.csproj'; GitHash = '030f876bc2b8e83d9dc73bb94c32dbe27d5b8abb' }
    @{ Path = 'examples/hello/README.md'; GitHash = '3c0f1fd6c1b9d5459afffecffb0b068265b7f8bc' }
    @{ Path = 'examples/hello/manifest.json'; GitHash = '1a535a95d0ae3ad9d88e663ab446fd32fb3a0b37' }
    @{ Path = 'examples/hello/src/HelloMod.cs'; GitHash = '22daed1b54e5c3d6a12151ec62c47f627b9715bf' }
    @{ Path = 'examples/roundnote/README.md'; GitHash = 'f755cbf77c61f9ee4b2d9f0cf7a65a6cebf80a95' }
    @{ Path = 'examples/roundnote/RoundNote.csproj'; GitHash = 'e532442f60c2c3e98cbf85680da7fa7f988acb81' }
    @{ Path = 'examples/roundnote/manifest.json'; GitHash = '3fb5c9d2bbf2f8f9024a9b49e081782d6443ddeb' }
    @{ Path = 'examples/roundnote/src/Game/NotePanel.cs'; GitHash = '3912c966b5218830ebbd164361e4559ee6073f01' }
    @{ Path = 'examples/roundnote/src/RoundNoteMod.cs'; GitHash = '2c8df0a59f55fc51e3590149e7f85106c88ade00' }
    @{ Path = 'examples/roundnote/src/RoundStats.cs'; GitHash = '88755ea1ac9c3aa6ea9ebb859f8330f4eceaa66f' }
    @{ Path = 'examples/roundnote/tests/AssemblyInfo.cs'; GitHash = '5f031f7de9a6b20b1c2c4aa2879b492fc105648d' }
    @{ Path = 'examples/roundnote/tests/RoundNote.Tests.csproj'; GitHash = '657485bcb949c5f5f7f3a602962f0b306ac31d16' }
    @{ Path = 'examples/roundnote/tests/RoundNoteTests.cs'; GitHash = 'bd2791189c3bda1b4f0b8c31eb42589d700caaef' }
    @{ Path = 'my-mod/MyMod.csproj'; GitHash = '0aab879d2d6ba52275d7cc00a025537c2eede320' }
    @{ Path = 'my-mod/README.md'; GitHash = '2da680f7e7fb8634f8916b7ba9821c2d213fa47b' }
    @{ Path = 'my-mod/manifest.json'; GitHash = '25f93852601c6d8f24eb0c8737d9ef441eacb96d' }
    @{ Path = 'my-mod/src/ModMain.cs'; GitHash = '221527567113b83f869b94a3ae0a192ad33e0220' }
    @{ Path = 'my-mod/tests/AssemblyInfo.cs'; GitHash = '5f031f7de9a6b20b1c2c4aa2879b492fc105648d' }
    @{ Path = 'my-mod/tests/ModMainTests.cs'; GitHash = '46ff0b37be819ac95244ae621d210349d59eda56' }
    @{ Path = 'my-mod/tests/MyMod.Tests.csproj'; GitHash = '9a49a96604b3f4eafb3be97081f0ae1267bfd778' }
    @{ Path = 'sdk/net40/Yx.ModSdk.Core.dll'; GitHash = '80b8714f01209e30e6d3a19d323d6ff169d7d55a' }
    @{ Path = 'sdk/net40/Yx.ModSdk.Core.xml'; GitHash = '204a7a3dad3c23e63cc24c681c32f4b15a50e9d6' }
    @{ Path = 'sdk/net40/Yx.ModSdk.dll'; GitHash = 'acfa42c7712969d31034e17f9c44ac46b8e7e76b' }
    @{ Path = 'sdk/net40/Yx.ModSdk.xml'; GitHash = '88db20cfabee0b38f78c9c447e563e645e058fac' }
    @{ Path = 'sdk/netstandard2.0/Yx.ModSdk.Core.dll'; GitHash = 'e35c274cf0ee15bd40f888ac0ec687173dff3930' }
    @{ Path = 'sdk/netstandard2.0/Yx.ModSdk.Core.xml'; GitHash = '204a7a3dad3c23e63cc24c681c32f4b15a50e9d6' }
    @{ Path = 'sdk/netstandard2.0/Yx.ModSdk.Testing.dll'; GitHash = '371074b71d1c262fe8e1fee0c6010815a540d568' }
    @{ Path = 'sdk/netstandard2.0/Yx.ModSdk.Testing.xml'; GitHash = 'b3405b62e2093bf407088188a89674e64a95f510' }
    @{ Path = 'tools/AssetsTools.NET.dll'; GitHash = 'eb1fadfba8a43dd4ebd941103dbc94bce234899b' }
    @{ Path = 'tools/Mono.Cecil.Mdb.dll'; GitHash = '55f9ee2524eb7bcc757484cddc5347544e79f4af' }
    @{ Path = 'tools/Mono.Cecil.Pdb.dll'; GitHash = '0462a8f2f413b5dc01b685c1189a640cc82e494a' }
    @{ Path = 'tools/Mono.Cecil.Rocks.dll'; GitHash = '561b0d747537800c270d66997eba81e552801c35' }
    @{ Path = 'tools/Mono.Cecil.dll'; GitHash = '488bd5cbbc7218333829a0dc29bc1253c891c62f' }
    @{ Path = 'tools/classdata.tpk'; GitHash = 'a68aff1288d95bd6a58ec6ad99813ea9c59e0abc' }
    @{ Path = 'tools/classdata.tpk.LICENSE'; GitHash = '106be6419fc102ed00aa212571f7005d5a54b04d' }
    @{ Path = 'tools/yx-patch.deps.json'; GitHash = 'e4e467a98a57fb522f2139f35ab4f16c83a06188' }
    @{ Path = 'tools/yx-patch.dll'; GitHash = '7de7d07238998688285a4b1fa7a8020e567b26ae' }
    @{ Path = 'tools/yx-patch.exe'; GitHash = '63554bef6e7059b8fc9489704a2a18063f90ada7' }
    @{ Path = 'tools/yx-patch.runtimeconfig.json'; GitHash = 'd7845156463f0c509f42229babf37fdb750f96ad' }
)
$root = [System.IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Path $root -Force | Out-Null
foreach ($file in $files) {
    $target = Join-Path $root $file.Path
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    if (!(Test-Path -LiteralPath $target)) {
        for ($attempt = 1; $attempt -le 3; $attempt++) {
            try {
                Invoke-WebRequest -Uri ("https://raw.githubusercontent.com/Airexplosion/yixianpai-mod-sdk/" + $revision + "/" + $file.Path) -OutFile ($target + '.download') -TimeoutSec 30
                Move-Item -LiteralPath ($target + '.download') -Destination $target -Force
                break
            } catch {
                if ($attempt -eq 3) { throw }
                Start-Sleep -Seconds 1
            }
        }
    }
    $bytes = [System.IO.File]::ReadAllBytes($target)
    $header = [System.Text.Encoding]::ASCII.GetBytes("blob " + $bytes.Length + [char]0)
    $data = New-Object byte[] ($header.Length + $bytes.Length)
    [Array]::Copy($header, 0, $data, 0, $header.Length)
    [Array]::Copy($bytes, 0, $data, $header.Length, $bytes.Length)
    $sha = [System.Security.Cryptography.SHA1]::Create()
    $actual = ([BitConverter]::ToString($sha.ComputeHash($data))).Replace('-', '').ToLowerInvariant()
    $sha.Dispose()
    if ($actual -ne $file.GitHash) { throw ("SDK file hash mismatch: " + $file.Path) }
    Write-Output ("Restored " + $file.Path)
}
Write-Output ("SDK revision: " + $revision)

