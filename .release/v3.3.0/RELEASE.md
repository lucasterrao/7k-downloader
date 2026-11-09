# 7K Downloader 3.3.0

Aplicativo independente para Windows 10/11 x64.

## Instalar
1. Baixe `7K-Downloader-3.3.0-windows-x64.zip` e extraia todos os arquivos em uma pasta pessoal gravável.
2. Execute `Instalar componentes.cmd` com internet. Ele instala Node LTS, yt-dlp e FFmpeg das fontes oficiais com verificação de integridade.
3. Abra `7K Downloader.exe`.

Requer .NET Framework 4.8. O navegador integrado requer Microsoft WebView2 Runtime. Não precisa instalar Node previamente nem executar como administrador. Não execute dentro do ZIP e não copie só o EXE.

## Nesta versão
- MP4 prioriza H.264/AAC oferecido pelo site para evitar recodificação; pode usar resolução menor dentro do limite escolhido.
- Escolha da pasta antes de baixar, salvamento sem subpastas e histórico removível sem apagar mídia.
- Bloqueio da abertura de executáveis e pacote sem cookies, histórico ou dados locais.

## Validação e limites
Testes automatizados de fila/API, seleção com yt-dlp real, conversão de codecs, download HTTP local e integração C# passaram. Os ZIPs anexados foram gerados localmente e seus hashes são verificados antes da publicação. Não houve validação em um segundo computador limpo nem auditoria de segurança externa. Não há assinatura de código própria; não desative antivírus. Consulte SECURITY.md e DISTRIBUICAO.md. Não há garantia de suporte a todos os sites ou a conteúdo protegido por DRM. Baixe apenas conteúdo autorizado.

`SHA256SUMS.txt` contém os hashes dos dois ZIPs. Node, yt-dlp e FFmpeg não estão incluídos no ZIP; as bibliotecas WebView2 incluem seus avisos de licença.
