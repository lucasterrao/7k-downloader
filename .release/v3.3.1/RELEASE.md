# 7K Downloader 3.3.1 — resposta do botão Colar link

Correção dos caminhos em que Colar link ficava desativado ou retornava sem explicar o problema.

- O botão permanece acessível e informa quais componentes faltam (Node, yt-dlp, FFmpeg ou FFprobe).
- Falhas de comunicação e inicialização pendente passam a ter mensagens visíveis, também no menu Adicionar links e ao arrastar texto.
- Área de transferência vazia ou indisponível abre a entrada manual; texto inválido é preservado para correção.
- Empacotamento verifica se a versão do executável corresponde à versão do ZIP.

## Instalação / atualização
Baixe o ZIP Windows, extraia tudo em uma pasta pessoal gravável e execute Instalar componentes.cmd com internet. Aguarde Instalação concluída e abra 7K Downloader.exe. Feche a versão anterior antes de usar a nova. A atualização do botão não substitui a instalação dos componentes e não confirma a causa de qualquer falha em outra máquina.

## Validação
Testes C# passaram para instalação incompleta, inicialização pendente, falha de conexão e estado pronto. Integração real C#/Node, consulta de mídia, download MP3, salvamento na pasta escolhida e remoção de histórico sem apagar mídia passaram. Compilado para Windows x64. O novo pacote ainda não foi validado no computador que apresentou o problema; nenhuma garantia de ausência de falhas ou de suporte a todos os sites. Mantenha o antivírus ativo.

.NET Framework 4.8; WebView2 Runtime para o navegador integrado. O instalador obtém Node, yt-dlp e FFmpeg das fontes oficiais com verificação de integridade. Cookies, histórico, downloads e backups não estão incluídos. Consulte SECURITY.md. Os hashes dos dois ZIPs constam de SHA256SUMS.txt.
