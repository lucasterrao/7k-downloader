# 7K Downloader — aplicativo Windows

## Download público e primeira instalação

Baixe o ZIP Windows x64 na seção [Releases](https://github.com/lucasterrao/7k-downloader/releases/latest). Extraia **todos os arquivos** em uma pasta pessoal gravável, execute **Instalar componentes.cmd** com internet e, ao concluir, abra **7K Downloader.exe**. O ZIP de distribuição não inclui Node, yt-dlp ou FFmpeg: o instalador os obtém diretamente das fontes oficiais e verifica sua integridade. Não requer Node previamente instalado nem execução como administrador.

Requer Windows 10/11 x64 e .NET Framework 4.8. A navegação integrada requer Microsoft WebView2 Runtime. Não há suporte nativo a macOS, Linux ou Windows 32 bits. Consulte [segurança e privacidade](SECURITY.md) e [distribuição](DISTRIBUICAO.md). Não desative o antivírus; esta versão não tem certificado próprio de assinatura de código. Ainda é necessário validar em outro computador antes de anunciar compatibilidade ampla.

Na versão 3.3.1, **Colar link** permanece acessível para informar componentes ausentes, inicialização pendente ou falhas de comunicação. Se não for possível ler um link da área de transferência, abre a entrada manual. Isso não substitui a instalação dos componentes: leia a mensagem e execute `Instalar componentes.cmd` caso solicitado.

Aplicativo independente para Windows, com janela e controles nativos em português. A versão 3.0 tem tema escuro, organização inspirada no 4K Video Downloader+, botão verde Colar link, modo inteligente, opções na barra superior, lista de downloads e navegador de sites integrado via Microsoft WebView2. Usa código próprio para interface, API local, fila e integração com **yt-dlp** e **FFmpeg**. Não reutiliza código ou recursos visuais do 4K Video Downloader.

## Abrir

Abra **7K Downloader.exe** ou **Iniciar.cmd**. O programa abre sua própria janela do Windows, com ícone na barra de tarefas. Mantenha o executável junto de `server.mjs`, `lib`, `assets`, `bin` e das DLLs WebView2 incluídas: este é um aplicativo portátil composto por vários arquivos. Não requer Node instalado quando `bin/node.exe` está presente. A navegação nos sites requer o Microsoft Edge WebView2 Runtime, já detectado neste computador.

Com **Modo inteligente** ativado, copie um link e clique em **Colar link** (ou Ctrl+V). O download usa o conteúdo, a qualidade e o formato escolhidos na barra superior. Desative o modo inteligente para revisar os links e as opções em uma janela antes de baixar. O menu **Arquivo → Adicionar links** sempre abre essa janela.

**Ferramentas → Preferências** define playlists e legendas. **Ctrl+F** pesquisa no histórico e **Ctrl+O** abre a pasta. Os filtros separam vídeos, áudios, downloads ativos e concluídos. Cada linha permite cancelar, tentar novamente, abrir o arquivo, ver os detalhes ou mostrar o arquivo na pasta.

Antes de iniciar cada envio ou nova tentativa, o programa solicita a **pasta de destino**. O arquivo final é salvo diretamente nela, sem pastas de tipo, tarefa ou ID. Nomes repetidos recebem sufixos como `(2)`, preservando o arquivo existente. Arquivos temporários ficam em `data/work`; uma tarefa concluída limpa seus temporários. Tarefas interrompidas conservam os temporários para retomada. O motor via API usa `downloads` apenas como destino padrão quando nenhum destino é informado.

**••• → Remover do histórico** remove uma tarefa parada individualmente. **Limpar** ou **Editar → Limpar histórico** remove os registros finalizados, preservando tarefas em andamento. Nenhuma dessas ações apaga os arquivos baixados.

As preferências estão organizadas em **Geral**, **Avançadas**, **Conexão**, **Notificações** e **Autorização**. Incluem tema escuro/claro, execução em segundo plano, até oito transferências simultâneas, fragmentos paralelos, prevenção de suspensão, retomada, playlists, legendas e idiomas, tentativas, tempo limite, velocidade máxima, proxy, avisos e cookies. Valores de concorrência elevados podem aumentar as restrições impostas pelos sites.

Os ícones dos serviços abrem uma janela de navegação **dentro do programa**. Ela oferece navegação, pesquisa e **Baixar página atual**. Não abre Chrome/Edge como aplicativo separado. O perfil do navegador integrado fica em `data/browser`; ele é separado do arquivo cookies.txt usado pelo yt-dlp, e entrar no site nessa janela não exporta cookies automaticamente para o motor de downloads.

Se aparecer um aviso de componentes ausentes, execute **Instalar componentes.cmd**. Esse instalador baixa yt-dlp e FFmpeg dos repositórios oficiais e verifica SHA-256. Ele também pode ser executado novamente para atualizar os componentes, com o aplicativo fechado. Os arquivos de licença ficam em `bin`, e as fontes de download e versões ficam em `bin/components.json`.

## Recursos

- Vídeo: MP4 (H.264 de 8 bits / AAC, adequado para importação no Premiere) ou MKV, limite de 480p a 4K ou melhor disponível.
- Áudio: MP3, M4A ou Opus, com taxa escolhida entre 128 e 320 kbps.
- Até 30 links por envio, dois downloads simultâneos e fila de até 100 tarefas.
- Playlist opcional, limitada a 100 itens por link.
- Legendas em português e inglês, incluindo automáticas quando disponíveis, salvas em arquivos separados.
- Consulta do título e resoluções disponíveis do primeiro link.
- Progresso, cancelamento, nova tentativa e histórico local.
- Consulta automática do título ao adicionar o link, inclusive para registros antigos sem nome. Links públicos do YouTube são consultados pelo endpoint oEmbed; outros conteúdos usam os metadados do yt-dlp. Se o site não fornecer o título, a lista informa que ele está indisponível, sem exibir a URL como nome.
- O arquivo usa o título do conteúdo, sem acrescentar o ID ao nome, diretamente na pasta escolhida. Nomes repetidos recebem sufixos. Caracteres incompatíveis com Windows e títulos muito longos são ajustados.
- Arquivos parciais são preservados para retomada em `data/work`.

O limite de qualidade não aumenta a resolução original. Em links diretos sem informação de resolução, o arquivo disponível é aceito mesmo sem ser possível verificar esse limite antes do download. Na versão 3.2, MP4 prioriza as faixas H.264/AVC e AAC já oferecidas pelo site, evitando recodificação. O valor de qualidade é um teto: pode ser usada uma resolução H.264 menor para evitar converter VP9/AV1, inclusive ao selecionar melhor disponível. Se não houver AAC, preserva H.264 e converte somente o áudio. Se não houver H.264, usa a fonte disponível e mantém a conversão de segurança. Uma junção rápida de faixas separadas ainda pode ser necessária, sem recodificação. A seleção segue os filtros de codecs do yt-dlp (https://github.com/yt-dlp/yt-dlp#format-selection). MP4 passa por inspeção com FFprobe: vídeo H.264/yuv420p e áudio AAC são preservados; VP9, AV1 ou outros codecs incompatíveis são convertidos para H.264 de 8 bits e AAC antes da entrega. A conversão preserva resolução e usa a taxa média da fonte como taxa constante quando disponível, com qualidade CRF 18. Áudio AAC já existente é copiado. Arquivos compatíveis não são recodificados. Isso pode aumentar o tempo de finalização e o tamanho do arquivo; recodificação de vídeo é um processo com perdas, mesmo em alta qualidade. MKV mantém a opção sem essa conversão para edição. A conversão de áudio não melhora a qualidade da fonte, e codecs como Opus podem ajustar a taxa suportada. Não há promessa de suporte a todos os sites: os extratores dependem do yt-dlp e dos serviços. Esta versão não implementa formulário de login, DRM, agendamento ou download ao vivo com limite de duração. Links ao vivo podem continuar baixando até você cancelar.

## YouTube pedindo confirmação de sessão

Se aparecer “Sign in to confirm you're not a bot”, o site não autorizou a extração sem uma sessão válida. O aplicativo mostra uma mensagem em português e o botão **Configurar sessão**. Não é possível garantir que um download bloqueado funcione sem a autorização do site.

1. Entre no YouTube no seu navegador e conclua pessoalmente qualquer confirmação solicitada pelo site.
2. Exporte os cookies da sua própria sessão no formato **Netscape cookies.txt**, conforme a [documentação oficial do yt-dlp](https://github.com/yt-dlp/yt-dlp/wiki/Extractors#exporting-youtube-cookies).
3. Abra **Ferramentas → Preferências → Selecionar cookies.txt**, selecione o arquivo e clique em **Salvar**.
4. No menu **•••** do download, escolha **Tentar novamente**.

O arquivo contém credenciais da sessão: mantenha-o privado e não o envie no chat. O aplicativo armazena apenas o caminho em `data/auth-settings.json` e passa o arquivo local ao yt-dlp; não copia seu conteúdo para o histórico nem extrai credenciais automaticamente de outros programas. Os cookies são apresentados aos sites pelo yt-dlp nas consultas autenticadas. Cookies vencidos, inválidos ou outras restrições do site ainda podem impedir um download. **Remover sessão** desativa o uso do arquivo, sem apagar o arquivo original.

O programa é destinado a conteúdo próprio ou que você tenha autorização para baixar. Links, logs e nomes de arquivos permanecem no histórico `data/history.json`; os links são enviados aos sites correspondentes pelo yt-dlp. As preferências ficam em `data/ui-settings.json`.

Fechar a janela encerra o programa e seu motor, salvo se **Continuar em segundo plano** estiver ativado nas preferências. Nesse caso, use o ícone na área de notificação para reabrir ou **Encerrar**. Se houver downloads em andamento ao encerrar, ele pede confirmação; os registros são preservados para nova tentativa ao reabrir.

## Desenvolvimento

Requer Node.js 22+ e Windows x64 com .NET Framework 4.6.2+ para compilar o aplicativo. O SDK WebView2 1.0.4258.31 é obtido do NuGet oficial e verificado com SHA-512 publicado no catálogo do pacote.

```powershell
node --test test/*.test.mjs
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/setup-webview.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/install-tools.ps1
.\'7K Downloader.exe'
```

Não há dependências npm. A interface nativa em C# / Windows Forms está em `scripts/NativeApp.cs` e `scripts/DesktopComponents.cs`; `scripts/build.ps1` compila o executável com ícone próprio e manifesto DPI. O WebView2 é usado para visitar sites em uma janela integrada; a interface principal permanece nativa. Ela se comunica com o motor Node por uma API restrita a `127.0.0.1`, porta dinâmica e token de sessão. Essa autenticação local não é uma barreira contra outros processos do mesmo usuário do Windows. Os componentes externos são executados com argumentos estruturados, sem shell. Não são aceitos argumentos arbitrários fornecidos pela interface.

Referências: [yt-dlp](https://github.com/yt-dlp/yt-dlp), [instalação](https://github.com/yt-dlp/yt-dlp/wiki/Installation), [FFmpeg Builds](https://github.com/yt-dlp/FFmpeg-Builds), [FFmpeg](https://ffmpeg.org/). Este projeto não foi compilado a partir de decompilação do aplicativo proprietário.

Para distribuir, use `scripts/package-release.ps1`, que prepara o código limpo e o ZIP do aplicativo com os avisos WebView2. Não envie a pasta de desenvolvimento inteira. Node, yt-dlp e FFmpeg são instalados diretamente das fontes oficiais na máquina de quem recebe o pacote; não são redistribuídos nesse ZIP.
