$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = (Get-Content -LiteralPath (Join-Path $root 'package.json') -Raw | ConvertFrom-Json).version
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$stage = Join-Path $dist ('build-' + [Guid]::NewGuid().ToString('N'))
$app = Join-Path $stage '7K Downloader'
$source = Join-Path $stage 'source'
New-Item -ItemType Directory -Path $app,$source -Force | Out-Null
# Explicit allowlist: never publish runtime data, browser profiles, cookies or old EXEs.
$common = @('server.mjs','package.json','README.md','LICENSE','SECURITY.md','DISTRIBUICAO.md','Iniciar.cmd','Instalar componentes.cmd')
foreach ($file in $common) { Copy-Item -LiteralPath (Join-Path $root $file) -Destination $app; Copy-Item -LiteralPath (Join-Path $root $file) -Destination $source }
foreach ($folder in @('lib','public','assets','scripts','test','.github')) {
    Copy-Item -LiteralPath (Join-Path $root $folder) -Destination $source -Recurse
    if ($folder -in @('lib','public','assets')) { Copy-Item -LiteralPath (Join-Path $root $folder) -Destination $app -Recurse }
}
Copy-Item -LiteralPath (Join-Path $root '.gitignore') -Destination $source
New-Item -ItemType Directory -Path (Join-Path $app 'scripts'),(Join-Path $app 'licenses') -Force | Out-Null
foreach ($file in @('setup-runtime.ps1','install-tools.ps1')) { Copy-Item -LiteralPath (Join-Path $root ('scripts\' + $file)) -Destination (Join-Path $app 'scripts') }
foreach ($file in @('7K Downloader.exe','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll')) { Copy-Item -LiteralPath (Join-Path $root $file) -Destination $app }
foreach ($file in @('LICENSE.txt','NOTICE.txt')) { Copy-Item -LiteralPath (Join-Path $root ('bin\webview-sdk\' + $file)) -Destination (Join-Path $app ('licenses\WebView2-' + $file)) }
$manifest = Get-ChildItem -LiteralPath $app -Recurse -File | ForEach-Object { @{ path = $_.FullName.Substring($app.Length + 1); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() } }
@{ version = $version; platform = 'windows-x64'; files = @($manifest) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $app 'manifest.json') -Encoding UTF8
$zip = Join-Path $dist ('7K-Downloader-' + $version + '-windows-x64.zip')
$code = Join-Path $dist ('7K-Downloader-' + $version + '-source.zip')
Compress-Archive -LiteralPath $app -DestinationPath $zip -Force
Compress-Archive -Path (Join-Path $source '*') -DestinationPath $code -Force
@($zip,$code) | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) } | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ASCII
Write-Host ('Pacotes: ' + $dist)
Write-Host ('Código limpo: ' + $source)
