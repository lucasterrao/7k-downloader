param([switch]$Staging)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$binDirectory = Join-Path $projectRoot 'bin'
New-Item -ItemType Directory -Force -Path $binDirectory | Out-Null
$nodeCommand = Get-Command node -ErrorAction Stop
if (-not (Test-Path -LiteralPath (Join-Path $binDirectory 'node.exe'))) { Copy-Item -LiteralPath $nodeCommand.Source -Destination (Join-Path $binDirectory 'node.exe') -Force }
$nodeLicense = Join-Path (Split-Path $nodeCommand.Source) 'LICENSE'
if (Test-Path -LiteralPath $nodeLicense) { Copy-Item -LiteralPath $nodeLicense -Destination (Join-Path $binDirectory 'Node-LICENSE.txt') -Force }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Compilador .NET Framework não encontrado.' }
$output = Join-Path $projectRoot $(if ($Staging) { '7K Downloader.next.exe' } else { '7K Downloader.exe' })
& (Join-Path $PSScriptRoot 'make-icon.ps1')
foreach ($required in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll')) { if (-not (Test-Path -LiteralPath (Join-Path $projectRoot $required))) { throw 'Execute scripts\setup-webview.ps1 para preparar o navegador integrado.' } }
& $compiler /nologo /codepage:65001 /target:winexe /platform:x64 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/reference:$(Join-Path $projectRoot 'Microsoft.Web.WebView2.Core.dll')" "/reference:$(Join-Path $projectRoot 'Microsoft.Web.WebView2.WinForms.dll')" "/win32manifest:$(Join-Path $PSScriptRoot 'app.manifest')" "/win32icon:$(Join-Path $projectRoot 'assets\app.ico')" "/out:$output" (Join-Path $PSScriptRoot 'NativeApp.cs') (Join-Path $PSScriptRoot 'DesktopComponents.cs')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o aplicativo Windows.' }
Write-Host ('Pronto: ' + $output)
