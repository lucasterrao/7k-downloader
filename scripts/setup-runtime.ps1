$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not [Environment]::Is64BitOperatingSystem) { throw 'Requer Windows x64.' }
$release = Get-ItemPropertyValue -Path 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release -ErrorAction SilentlyContinue
if (-not $release -or $release -lt 528040) { throw 'Instale o .NET Framework 4.8 pelo site da Microsoft antes de continuar.' }
$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Path $bin -Force | Out-Null
# LTS fixado para tornar a versão publicada reproduzível. Atualizações são explícitas.
$version = 'v26.13.0'
$base = 'https://nodejs.org/dist/' + $version + '/'
$sums = (Invoke-WebRequest -UseBasicParsing -Uri ($base + 'SHASUMS256.txt')).Content
$line = ($sums -split "`n") | Where-Object { $_ -match '^([a-fA-F0-9]{64})\s+win-x64/node.exe\s*$' } | Select-Object -First 1
if (-not $line) { throw 'Checksum oficial do Node ausente.' }
$expected = ($line.Trim() -split '\s+')[0]
$partial = Join-Path $bin 'node.exe.download'
Invoke-WebRequest -UseBasicParsing -Uri ($base + 'win-x64/node.exe') -OutFile $partial
if ((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ne $expected) { throw 'Checksum Node incorreto; instalação bloqueada.' }
$signature = Get-AuthenticodeSignature -LiteralPath $partial
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'OpenJS Foundation') { throw 'Assinatura digital Node inválida.' }
Move-Item -LiteralPath $partial -Destination (Join-Path $bin 'node.exe') -Force
& (Join-Path $PSScriptRoot 'install-tools.ps1')
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw 'Falha na instalação dos componentes.' }
Write-Host 'Instalação concluída. Abra 7K Downloader.exe. Para o navegador integrado, instale Microsoft WebView2 Runtime se necessário.' -ForegroundColor Green
