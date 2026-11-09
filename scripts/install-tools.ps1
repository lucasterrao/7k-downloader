$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$binDirectory = Join-Path $projectRoot 'bin'
New-Item -ItemType Directory -Force -Path $binDirectory | Out-Null
$headers = @{ 'User-Agent' = '7K-Downloader-Setup'; 'Accept' = 'application/vnd.github+json' }

function Save-Asset($asset, [string]$destination, [string]$expectedHash = '') {
    Write-Host ('Baixando ' + $asset.name + '...')
    $partial = $destination + '.download'
    Invoke-WebRequest -UseBasicParsing -Uri $asset.browser_download_url -Headers $headers -OutFile $partial
    if (-not $expectedHash -and $asset.digest -and $asset.digest.StartsWith('sha256:')) { $expectedHash = $asset.digest.Substring(7) }
    if (-not $expectedHash) { throw ('Checksum oficial ausente: ' + $asset.name) }
    if ($expectedHash) {
        $expected = $expectedHash.ToLowerInvariant()
        $actual = (Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $expected) { throw ('Checksum incorreto: ' + $asset.name) }
    }
    Move-Item -LiteralPath $partial -Destination $destination -Force
}

Write-Host 'Instalando componentes oficiais na pasta do 7K Downloader.'
$yt = Invoke-RestMethod -Uri 'https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest' -Headers $headers
$ytAsset = $yt.assets | Where-Object { $_.name -eq 'yt-dlp.exe' } | Select-Object -First 1
$hashAsset = $yt.assets | Where-Object { $_.name -eq 'SHA2-256SUMS' } | Select-Object -First 1
if (-not $ytAsset -or -not $hashAsset) { throw 'Release do yt-dlp sem executavel ou checksums esperados.' }
$ytDestination = Join-Path $binDirectory 'yt-dlp.exe'
$hashFile = Join-Path $binDirectory 'yt-dlp-SHA2-256SUMS.txt'
Invoke-WebRequest -UseBasicParsing -Uri $hashAsset.browser_download_url -Headers $headers -OutFile $hashFile
$hashLine = Get-Content -LiteralPath $hashFile | Where-Object { $_ -match '^([a-fA-F0-9]{64})\s+\*?yt-dlp\.exe$' } | Select-Object -First 1
if (-not $hashLine) { throw 'Checksum do yt-dlp.exe ausente.' }
$expectedYt = ($hashLine -split '\s+')[0].ToLowerInvariant()
Save-Asset $ytAsset $ytDestination $expectedYt
if ((Get-FileHash -LiteralPath $ytDestination -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedYt) { throw 'Checksum do yt-dlp.exe incorreto.' }

$ff = Invoke-RestMethod -Uri 'https://api.github.com/repos/yt-dlp/FFmpeg-Builds/releases/latest' -Headers $headers
$ffAsset = $ff.assets | Where-Object { $_.name -eq 'ffmpeg-master-latest-win64-gpl.zip' } | Select-Object -First 1
if (-not $ffAsset) { throw 'Build Windows x64 do FFmpeg nao encontrado.' }
if (-not $ffAsset.digest -or -not $ffAsset.digest.StartsWith('sha256:')) { throw 'Release do FFmpeg sem digest SHA-256; instalacao interrompida.' }
$archive = Join-Path $binDirectory 'ffmpeg-package.zip'
Save-Asset $ffAsset $archive
$unpack = Join-Path $binDirectory ('ffmpeg-unpack-' + [Guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $archive -DestinationPath $unpack
foreach ($name in @('ffmpeg.exe', 'ffprobe.exe')) {
    $source = Get-ChildItem -LiteralPath $unpack -Recurse -File -Filter $name | Select-Object -First 1
    if (-not $source) { throw ('Arquivo ausente: ' + $name) }
    Copy-Item -LiteralPath $source.FullName -Destination (Join-Path $binDirectory $name) -Force
}
$ffLicense = Get-ChildItem -LiteralPath $unpack -Recurse -File | Where-Object { $_.Name -match '^(LICENSE|COPYING)' } | Select-Object -First 1
if ($ffLicense) { Copy-Item -LiteralPath $ffLicense.FullName -Destination (Join-Path $binDirectory 'FFmpeg-LICENSE.txt') -Force }
$ytLicense = Join-Path $binDirectory 'yt-dlp-LICENSE.txt'
Invoke-WebRequest -UseBasicParsing -Uri ('https://raw.githubusercontent.com/yt-dlp/yt-dlp/' + $yt.tag_name + '/LICENSE') -OutFile $ytLicense
Invoke-WebRequest -UseBasicParsing -Uri ('https://raw.githubusercontent.com/yt-dlp/yt-dlp/' + $yt.tag_name + '/THIRD_PARTY_LICENSES.txt') -OutFile (Join-Path $binDirectory 'yt-dlp-THIRD_PARTY_LICENSES.txt')
if (Test-Path -LiteralPath (Join-Path $binDirectory 'node.exe')) {
    $nodeVersion = (& (Join-Path $binDirectory 'node.exe') --version).Trim()
    if ($nodeVersion -notmatch '^v\d+\.\d+\.\d+$') { throw 'Versao do Node invalida.' }
    Invoke-WebRequest -UseBasicParsing -Uri ('https://raw.githubusercontent.com/nodejs/node/' + $nodeVersion + '/LICENSE') -OutFile (Join-Path $binDirectory 'Node-LICENSE.txt')
}
@{ ytDlp = $yt.tag_name; ffmpeg = $ff.tag_name; ytDlpSource = $ytAsset.browser_download_url; ffmpegSource = $ffAsset.browser_download_url; installedAt = (Get-Date).ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $binDirectory 'components.json') -Encoding UTF8
$checkedArchive = [IO.Path]::GetFullPath($archive)
$checkedUnpack = [IO.Path]::GetFullPath($unpack)
if ((Split-Path -Parent $checkedArchive) -ne $binDirectory -or (Split-Path -Parent $checkedUnpack) -ne $binDirectory) { throw 'Caminho temporario fora da pasta bin.' }
Remove-Item -LiteralPath $checkedArchive
Remove-Item -LiteralPath $checkedUnpack -Recurse
Write-Host 'Componentes instalados. Abra 7K Downloader.exe.' -ForegroundColor Green
