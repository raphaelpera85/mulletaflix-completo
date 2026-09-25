# MulletaFlix iOS

Primeiro vertical slice nativo do cliente Apple, alinhado ao contrato do
`MulletaFlix-android`: autenticação por usuário/senha, sessão no Keychain,
consulta de itens recentes/em andamento e resolução de artwork pelo servidor.

Falhas de autenticação e conexão seguem mensagens acionáveis equivalentes às do
Android: credenciais inválidas, falta de permissão, servidor ausente e timeout
são diferenciados, preservando também mensagens específicas devolvidas pela API.

## Abrir no Xcode

No macOS, abra `MulletaFlix.xcodeproj` no Xcode 16 e execute o scheme
`MulletaFlix` em um simulador iOS 18+. O projeto já inclui o target executável,
a camada de domínio/rede e as telas de login, Quick Connect, Home, bibliotecas,
detalhe, busca, TV ao vivo e perfil. `Package.swift` continua disponível para
executar os testes da camada Core isoladamente.

A Home apresenta um destaque principal com o título mais recente, além dos
trilhos de Minha Lista, Continuar assistindo, Próximo episódio, Adicionados
recentemente, Mais populares, Filmes, Séries e TV ao vivo.

A tela dedicada de Minha Lista atualiza seus favoritos independentemente da
Home, mostrando carregamento, erro com retry e o vazio somente após uma consulta
concluída.

Durante a atualização, a Home mostra carregamento; faixas sem itens não são
renderizadas. Falhas do feed preservam o conteúdo que já chegou e exibem retry
contextual, enquanto o estado vazio só aparece após uma carga concluída sem
conteúdo.

Quando disponíveis, a Home também exibe o trilho de próximo episódio (`Shows/NextUp`)
e canais de TV ao vivo, mantendo a mesma descoberta contextual do Android.

A aba TV ao vivo inclui canais, guia das próximas 24 horas, gravações existentes
e agendamento de gravação usando `LiveTv/Timers/Defaults` antes de criar o timer.
Refreshes concorrentes de TV ao vivo são protegidos por geração e sessão, portanto
respostas antigas não substituem os dados da atualização ou da conta atuais.
Falhas parciais preservam os dados já carregados e exibem retry contextual na tela.
Agendamentos em andamento ficam protegidos contra toques duplicados e respostas
da conta anterior, com progresso visível no programa correspondente.
Na tela de login, "Encontrar na rede local" replica a descoberta UDP do Android
(porta 7359, mensagem `who is MulletaFlixServer?`). O perfil consulta as
permissões reais do usuário no servidor e exibe o avatar quando há
`PrimaryImageTag` disponível.

O perfil também permite alternar entre usuários públicos do mesmo servidor,
solicitando a senha somente quando o servidor exigir.

O carregamento do perfil possui estado próprio e retry contextual; quando uma
atualização falha, as permissões e os dados já exibidos permanecem visíveis.

No Perfil, a URL do servidor conectado pode ser copiada diretamente para a
área de transferência, com confirmação visual e acessível.

O perfil também oferece salas SyncPlay para listar, criar, entrar e sair via
`SyncPlay/List`, `SyncPlay/New`, `SyncPlay/Join` e `SyncPlay/Leave`. Ao entrar em
uma sala, o cliente abre a conexão WebSocket nativa do servidor e decodifica os
eventos `SyncPlayGroupUpdate` e `SyncPlayCommand`; comandos de pausa, retomada e
seek são encaminhados ao `AVPlayer` quando a sala está ativa durante a reprodução.
A conexão tenta até três reconexões curtas quando a rede oscila.
Na sala atual, o iOS também oferece os controles de pausar, retomar e parar a
reprodução do grupo pelos endpoints `SyncPlay/Pause`, `SyncPlay/Unpause` e
`SyncPlay/Stop`.
Se uma atualização falhar, as salas já carregadas permanecem na tela e o erro
oferece retry contextual, sem apagar o conteúdo stale.

Downloads offline usam uma `URLSession` de background e persistem um índice
isolado por servidor e usuário em `Documents/Downloads/scope-*/index.json`, com estados enfileirado, baixando, concluído e
falho. O progresso reporta bytes transferidos e tamanho total quando o servidor
fornece esse valor. Itens concluídos são reproduzidos pelo arquivo local antes
de consultar o servidor. Downloads em andamento podem ser pausados e retomados;
a retomada usa `resumeData` do `URLSessionDownloadTask` quando o servidor
fornece dados parciais válidos. Arquivos offline que carregam múltiplas faixas
também expõem seleção nativa de áudio e legendas a partir do próprio contêiner,
sem depender de metadados online. O progresso de reprodução offline é persistido
por servidor e usuário, restaurado ao reabrir o arquivo e removido quando a mídia
termina ou o download é excluído; quando não existe posição local, o cliente usa
a posição retornada pelo servidor como fallback.

A fila pode ser pausada e retomada globalmente; novos downloads permanecem
enfileirados enquanto a fila estiver pausada.
Com a preferência “Baixar somente no Wi-Fi”, novos downloads ficam enfileirados
em rede móvel e a fila retoma automaticamente quando o Wi-Fi volta.
O sistema também recebe os eventos de conclusão da sessão em background quando
o aplicativo é suspenso. Ao iniciar novamente, a fila preserva o estado,
reassocia tarefas background ainda existentes e reinicia pela URL persistida as
que não puderam ser recuperadas.
Se o app for encerrado durante uma pausa global, os itens interrompidos são
normalizados para a fila e aguardam a retomada manual antes de iniciar.

O cliente monitora a conectividade local e, ao detectar reconexão, atualiza
Home, bibliotecas e TV ao vivo automaticamente. Enquanto estiver offline, um
indicador informa que o conteúdo já armazenado pode continuar sendo usado.

A tela de downloads também oferece busca local, filtro por estado, resumo de
armazenamento e limpeza em lote de downloads concluídos ou falhos.
Downloads concluídos também podem ser reproduzidos diretamente nessa tela,
usando o arquivo local e a posição offline persistida, sem depender de metadados
online. Quando o item fornece arte, a referência é persistida junto da fila e
apresentada como artwork opcional, sem tornar a imagem requisito para o player.

O detalhe permite listar playlists, criar uma nova playlist já com o item atual
e adicionar o item a uma playlist existente usando as rotas `Playlists` do
servidor.

Metadados de detalhe como gêneros, classificação indicativa, nota, duração e
elenco/equipe são preservados do payload do servidor e apresentados no iOS,
incluindo itens de música e livros quando o catálogo os fornece.

Detalhes de filmes e séries também carregam rails de itens semelhantes e
recursos especiais pelas rotas de itens semelhantes e recursos especiais do
servidor.

Detalhes de álbuns musicais carregam as faixas filhas com `ParentId` e
`IncludeItemTypes=Audio`; cada faixa pode iniciar a reprodução diretamente.

O detalhe também oferece compartilhamento nativo com link web do título; links
gerados a partir de endereços locais usam o endpoint público configurado.

Links `mulletaflix://...` e links web oficiais com o identificador da mídia
abrem diretamente o detalhe no iOS. Links recebidos antes do login ficam
pendentes e são resolvidos depois da autenticação. Quando o link carrega
`serverId`, o app impede a abertura em outro servidor identificado.

O player consulta segmentos de mídia e oferece o botão contextual para pular
introduções ou créditos quando o servidor fornece esses marcadores.

Durante a reprodução, o temporizador oferece contagens regressivas de 15 a 120
minutos, pausa ao fim da mídia ou cancelamento.

O player também permite alterar a velocidade durante a reprodução entre 0,5x e
2x, além da velocidade padrão persistida nas preferências.

Quando o servidor fornece múltiplas faixas, o player permite trocar o áudio e
as legendas manualmente, além de desativar as legendas.
Os índices padrão de áudio e legenda da `MediaSource` também são enviados ao
`PlaybackInfo`, mantendo a mesma seleção inicial do cliente Android.

Durante a reprodução remota, o player envia início, progresso a cada cinco
segundos e encerramento para `Sessions/Playing`, `Sessions/Playing/Progress` e
`Sessions/Playing/Stopped`. Títulos remotos retomam `PlaybackPositionTicks` via
`StartTimeTicks`; arquivos offline retomam localmente sem baixar somente um
trecho e não geram eventos remotos. O encerramento remoto é enviado tanto ao
final da mídia quanto ao sair do player, sem duplicar o evento.

Detalhes de séries carregam temporadas e episódios pelas rotas
`Shows/{id}/Seasons` e `Shows/{id}/Episodes`, com seleção de temporada e
navegação para cada episódio. Ao final de um episódio, o player também consulta
o próximo episódio da temporada ou da temporada seguinte e oferece a ação
contextual para continuar assistindo; com reprodução automática habilitada,
essa ação inicia após uma contagem regressiva de cinco segundos cancelável.

O detalhe mostra “Continuar” quando existe progresso local ou remoto e “Assistir”
para títulos sem posição salva. Também permite marcar e desmarcar títulos como assistidos usando
`Users/{userId}/PlayedItems/{itemId}`, preservando o contexto de temporada e
episódio no estado local.

A busca oferece histórico, sugestões, filtros por tipo e entrada por voz com
`Speech`/`AVAudioEngine`, respeitando as permissões nativas do iOS.
Quando o servidor informa um total maior que os itens exibidos, a busca mostra
um aviso explícito de truncamento; se o total não vier na resposta, nenhum
número é inventado.

A tela de acesso mantém até oito servidores usados recentemente, com nome,
versão e remoção individual; as credenciais continuam protegidas no Keychain.
Falhas de autenticação e conexão traduzem códigos HTTP e erros de transporte em
mensagens acionáveis, diferenciando credenciais inválidas, falta de permissão,
servidor ausente e timeout.
Quando disponível, o aviso personalizado de login do servidor é carregado por
`Branding/Configuration` após a verificação.
O mesmo fluxo consulta `Health` para exibir o estado de saúde retornado pelo
servidor; falhas nesse endpoint não impedem a autenticação.

O perfil inclui preferências persistentes de reprodução (início automático,
Picture-in-Picture, velocidade, qualidade padrão e oito temas), idiomas preferidos de áudio/legenda e
densidade da grade do catálogo. A qualidade pode ser Automática, 4K, 1440p,
1080p, 720p ou 480p; quando o stream oferece múltiplas variantes, ela envia
o bitrate máximo ao `PlaybackInfo` e também limita o `AVPlayer`. O tema pode seguir o sistema ou ser fixado em
modo claro/escuro. Quando o servidor fornece opções de mídia com locale
compatível, os idiomas são selecionados automaticamente no AVPlayer.
As opções de qualidade do menu são derivadas das faixas de vídeo `MediaStreams`
reais do servidor, com fallback para a lista padrão quando esse campo não existe.
Álbuns musicais exibem as faixas e permitem abrir as letras fornecidas por
`Audio/{itemId}/Lyrics`.
O player usa `AVPlayerViewController` para oferecer Picture-in-Picture nativo
quando a preferência está habilitada.
Também habilita AirPlay e reprodução em telas externas por meio da sessão nativa
de áudio do iOS, com seletor AirPlay explícito no player para TVs e alto-falantes
compatíveis. Interrupções do sistema e remoção de fones/rotas pausam o player;
a retomada automática só ocorre quando o iOS autoriza a continuação.
Falhas transitórias de rede recebem uma mensagem acionável, e descrições de erro
que contenham `api_key`, tokens ou autorização são sanitizadas antes de aparecer
na tela. Em streaming remoto, falhas de rede transitórias recebem até três
tentativas automáticas com backoff limitado; falhas de codec ou conteúdo não
entram em loop de retry.
Se a falha ocorrer enquanto o dispositivo estiver offline, a reprodução é
reativada automaticamente quando a conectividade retornar.

A ordenação padrão das bibliotecas também pode ser escolhida por nome, datas ou
avaliação, em ordem ascendente ou descendente, e é enviada ao endpoint de itens
do servidor. A apresentação alterna entre grade e lista e fica persistida nas
preferências do aplicativo.

O carregamento da Biblioteca diferencia progresso, catálogo vazio e falha de
rede. Quando a consulta falha, a tela oferece retry contextual; respostas
atrasadas são descartadas se o usuário trocar de biblioteca, sessão ou sair da
tela.

A listagem inicial de bibliotecas também possui carregamento e erro próprios,
com retry no contexto da lista e preservação das bibliotecas já exibidas.

A Biblioteca também oferece filtros persistentes por gênero, ano, status de
reprodução e Minha Lista; os valores são enviados como `Genres`, `Years`,
`IsPlayed` e `IsFavorite`, mantendo o mesmo contrato do cliente Android.

A Home inclui a faixa “Minha Lista”, carregada com `Filters=IsFavorite` e
`IsFavorite=true`, além dos trilhos de continuar assistindo e adicionados
recentemente. Uma falha isolada desse endpoint não bloqueia os demais trilhos.

Ao voltar ao foreground, a Home só inicia uma nova atualização quando as
requisições de Home, bibliotecas e TV ao vivo estão ociosas e existe conexão;
isso evita invalidar uma carga que ainda está em andamento.

A busca também consulta `Search/Hints` durante a digitação e apresenta sugestões
do servidor antes da grade completa de resultados. A busca completa pode ser
filtrada por tudo, filmes, séries, episódios, músicas ou pessoas via
`IncludeItemTypes`.
Consultas concluídas também aparecem em um histórico local limitado a dez itens,
com remoção individual ou limpeza completa no perfil de sessão.

Bibliotecas, busca e dados de TV ao vivo usam paginação incremental com limite
de segurança e deduplicação por identificador, acompanhando o comportamento do
cliente Android para catálogos grandes.

Na TV ao vivo, os horários do guia e das gravações são convertidos de UTC para
o fuso local do dispositivo. Datas ausentes ou inválidas não são exibidas como
se fossem horários confiáveis.

O perfil também oferece uma tela dedicada “Minha Lista”, equivalente à rota de
favoritos do Android, com grade, navegação para detalhes e atualização manual.

Antes da reprodução remota, o cliente consulta `Items/{itemId}/PlaybackInfo` e
abre fontes de tuner com `LiveStreams/Open` quando `RequiresOpening` é retornado.

O player também expõe um painel de estatísticas com origem da mídia, posição,
duração, qualidade, bitrate máximo, velocidade e estado do AVPlayer para
facilitar o diagnóstico de streaming no dispositivo ou simulador. O painel
permite selecionar o texto e compartilhar os dados técnicos.
Gestos horizontais permitem buscar proporcionalmente na mídia, toque duplo
avança ou retorna dez segundos e o gesto vertical no lado esquerdo ajusta o
brilho; no lado direito o iOS mantém o volume sob os controles oficiais do
sistema.
Quando o AVPlayer falha no fim da mídia, o iOS mostra o erro e permite
recriar a fonte e retomar a partir da posição anterior.
A preferência persistente controla a exibição do botão de pular introdução e
créditos, seguindo o comportamento do Android.
Também é possível escolher entre ajustar à proporção original, preencher com
zoom ou esticar a imagem; a seleção é aplicada diretamente ao
`AVPlayerViewController`.
As Configurações oferecem limpeza do cache temporário de imagens sem remover
downloads offline ou a sessão autenticada.

## Quality bar

```sh
swift test
```

O ambiente atual é Windows e não possui `swift`/Xcode instalados; portanto a
execução do compilador Apple precisa ocorrer em um runner macOS ou no Xcode.
O workflow de CI inclui um job `macos-14` que executa `swift test` e compila o
scheme `MulletaFlix` para o simulador sem assinatura.
O roteiro manual está em [`MACOS-VALIDATION.md`](MACOS-VALIDATION.md).

O servidor padrão e servidores na rede local podem usar HTTP, refletindo o
cliente Android. O `Info.plist` libera apenas rede local e o domínio público
oficial; produção deve preferir HTTPS quando disponível.

O login verifica primeiro `System/Info/Public`, exibindo o nome e a versão do
servidor antes de autenticar por senha ou Quick Connect.

Ao carregar o perfil, os idiomas de áudio e legenda configurados pelo servidor
preenchem as preferências locais quando o usuário ainda não escolheu valores.
