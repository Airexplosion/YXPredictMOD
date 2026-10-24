param([string]$Root = 'C:\yx-prediction', [string]$Node = 'C:\Program Files\nodejs\node.exe')
$ErrorActionPreference = 'Stop'
$env:ROOM_HOST = '127.0.0.1'
$env:ROOM_PORT = '8123'
$env:ROOM_DATA = Join-Path $Root 'data'
Set-Location -LiteralPath $Root
& $Node (Join-Path $Root 'app/server.mjs') >> (Join-Path $Root 'server.log') 2>&1
exit $LASTEXITCODE
