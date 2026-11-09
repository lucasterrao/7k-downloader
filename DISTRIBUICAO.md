# Publicação e instalação

O pacote Windows é gerado com scripts/package-release.ps1. Ele usa uma lista explícita de arquivos, não copia a pasta de desenvolvimento inteira. Histórico, cookies, downloads, backups e ferramentas locais ficam de fora. O ZIP contém o aplicativo e as bibliotecas WebView2 com seus avisos. Node, yt-dlp e FFmpeg são baixados diretamente pelo usuário na primeira instalação; os binários GPL do FFmpeg não são redistribuídos neste pacote.

Para outra pessoa: baixar o ZIP da seção Releases, verificar SHA256SUMS.txt, extrair tudo em uma pasta pessoal gravável, executar Instalar componentes.cmd com internet e então abrir 7K Downloader.exe. Instalar .NET Framework 4.8 e Microsoft WebView2 Runtime pelos sites oficiais se faltarem. A interface é Windows x64; não roda nativamente em macOS, Linux ou Windows 32 bits. Não copie apenas o EXE. Os componentes de download podem ser grandes.

Para publicar: envie o conteúdo de dist/source/ para o repositório público lucasterrao/7k-downloader. Use o workflow manual Build Windows para compilar e anexar os artefatos a uma Release quando solicitado. Alternativamente crie uma Release v3.3.1 e anexe o ZIP Windows e SHA256SUMS.txt produzidos localmente. O código-fonte não contém as DLLs: scripts/setup-webview.ps1 obtém o SDK verificado para compilar. A licença MIT aplica-se ao código próprio; componentes externos mantêm suas licenças.

Antes de anunciar suporte, teste o ZIP extraído em um Windows limpo com usuário padrão. Testes locais automatizados não substituem esse teste. Não há certificado de assinatura de código configurado. Habilite secret scanning e private vulnerability reporting no GitHub quando disponíveis.
