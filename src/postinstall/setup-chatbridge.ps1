# Chaos Mode post-install: downloads ChatBridge and puts it in <game folder>\ChatBridge.
# ChatBridge reads stream chat (YouTube / TikTok / Kick) and serves the OBS overlay.
# Creator Mode in Chaos Mode stays locked until it is installed.
#
# What this script does, and nothing else:
#   1. Downloads ChatBridge-v1.2.2.zip from the Chaos Mode GitHub release (https://github.com/wcixv/chaos-mode/releases/tag/v1.2.2)
#   2. Checks the file against the SHA-256 below and stops if it doesn't match
#   3. Extracts it to <game folder>\ChatBridge (your chatbridge.json settings are kept)
param(
    [Parameter(Mandatory = $true)][string]$GameDir
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$url  = 'https://github.com/wcixv/chaos-mode/releases/download/v1.2.2/ChatBridge-v1.2.2.zip'
$hash = '7ccbc7b5f8cfdd4cd31303fe45398e4fa23aef47316c5321fbb14c98daae3f6e'

if (-not (Test-Path -LiteralPath $GameDir -PathType Container)) { throw "Game folder not found: $GameDir" }

$tmp = Join-Path ([IO.Path]::GetTempPath()) ('chaosmode-chatbridge-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    $zip = Join-Path $tmp 'ChatBridge.zip'
    Write-Host 'Downloading ChatBridge...'
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing

    $got = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLower()
    if ($got -ne $hash) { throw "Download doesn't match the expected checksum ($got). Nothing was installed." }

    $dest = Join-Path $GameDir 'ChatBridge'
    $cfg  = Join-Path $dest 'chatbridge.json'
    $keep = $null
    if (Test-Path -LiteralPath $cfg) { $keep = Join-Path $tmp 'chatbridge.json'; Copy-Item -LiteralPath $cfg -Destination $keep }

    Expand-Archive -LiteralPath $zip -DestinationPath $GameDir -Force

    if ($keep) { Copy-Item -LiteralPath $keep -Destination $cfg -Force }

    foreach ($f in 'ChatBridge.exe', 'chatbridge-core.exe') {
        if (-not (Test-Path -LiteralPath (Join-Path $dest $f))) { throw "Install incomplete: $f is missing." }
    }
    Write-Host "ChatBridge installed to $dest"
    Write-Host 'Open ChatBridge\ChatBridge.exe to add your channels. Windows may warn it is unsigned: More info > Run anyway.'
}
finally {
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
