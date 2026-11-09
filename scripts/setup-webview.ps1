$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sdkDirectory = Join-Path $projectRoot 'bin\webview-sdk'
New-Item -ItemType Directory -Path $sdkDirectory -Force | Out-Null
$version = '1.0.4258.31'
$package = Join-Path $sdkDirectory 'package.zip'
$baseUri = 'https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/' + $version + '/microsoft.web.webview2.' + $version + '.nupkg'
if (-not (Test-Path -LiteralPath $package)) { Invoke-WebRequest -UseBasicParsing -Uri $baseUri -OutFile $package }
$registration = Invoke-RestMethod -Uri ('https://api.nuget.org/v3/registration5-gz-semver2/microsoft.web.webview2/' + $version + '.json')
$catalog = Invoke-RestMethod -Uri $registration.catalogEntry
$expectedHash = $catalog.packageHash
if ($catalog.packageHashAlgorithm -ne 'SHA512' -or -not $expectedHash) { throw 'Hash oficial do pacote ausente.' }
$hasher = [Security.Cryptography.SHA512]::Create()
$stream = [IO.File]::OpenRead($package)
try { $actualHash = [Convert]::ToBase64String($hasher.ComputeHash($stream)) } finally { $stream.Dispose(); $hasher.Dispose() }
if ($actualHash -ne $expectedHash) { throw 'Checksum SHA-512 do pacote WebView2 incorreto.' }
Expand-Archive -LiteralPath $package -DestinationPath (Join-Path $sdkDirectory 'unpacked') -Force
foreach ($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')) {
    Copy-Item -LiteralPath (Join-Path $sdkDirectory ('unpacked\lib\net462\' + $name)) -Destination (Join-Path $projectRoot $name) -Force
}
Copy-Item -LiteralPath (Join-Path $sdkDirectory 'unpacked\runtimes\win-x64\native\WebView2Loader.dll') -Destination (Join-Path $projectRoot 'WebView2Loader.dll') -Force
Get-ChildItem -LiteralPath (Join-Path $sdkDirectory 'unpacked') -File | Where-Object { $_.Name -match 'LICENSE|NOTICE' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $sdkDirectory $_.Name) -Force }
Write-Host ('WebView2 SDK ' + $version + ' instalado e verificado.')
