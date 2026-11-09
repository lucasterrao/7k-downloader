# Segurança e privacidade

Suporte planejado: Windows 10/11 x64, .NET Framework 4.8 e WebView2 Runtime atualizado para a navegação integrada. Extraia o ZIP em uma pasta gravável do usuário; não execute dentro do ZIP ou em Program Files. Não requer administrador. Esta aplicação não é um sandbox: mídia e sites não confiáveis ainda podem explorar vulnerabilidades nos componentes ou no player/editor.

O motor usa somente loopback, token aleatório, validação de Host/Origin, chamadas sem shell e lista restrita de operações. O botão Abrir permite somente mídia. Não há execução automática dos arquivos baixados. Os componentes são obtidos de fontes oficiais por HTTPS e verificados antes de instalação. Checksum protege integridade em relação à fonte, não elimina comprometimento da própria fonte. A aplicação não tem assinatura de código própria; SmartScreen pode avisar. Não desative antivírus para executá-la.

Feche o programa e execute Instalar componentes.cmd para atualizar yt-dlp/FFmpeg e instalar o Node LTS fixado nesta versão. Mantenha Windows, WebView2 e seu editor/player atualizados. Não publique data/, downloads/, cookies, perfis de navegador, chaves ou logs. URLs podem conter tokens; detalhes de tarefas devem ser revisados antes de compartilhados. Cookies são credenciais e não são necessários para todos os sites. O token da API não isola processos executados na mesma conta do Windows.

O programa acessa URLs escolhidas por você, inclusive servidores locais; não é adequado como serviço público multiusuário. Não exponha sua porta na rede. Não contorna DRM, direitos autorais, autenticação ou bloqueios dos sites. Suporte a plataformas depende dos extratores do yt-dlp e pode mudar sem aviso. Não há garantia de funcionamento em todos os sites ou máquinas.

Relate suspeitas por um canal privado do mantenedor ou GitHub Private Vulnerability Reporting, se habilitado. Não abra issue pública contendo cookies, senhas, tokens ou exemplos de exploração ainda não corrigidos. Apenas a versão publicada mais recente recebe correções; não foi realizada auditoria externa nem teste em todas as máquinas.
