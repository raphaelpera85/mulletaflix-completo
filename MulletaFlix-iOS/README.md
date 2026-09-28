# MulletaFlix iOS

## Política de release

A primeira release pública do cliente iOS será a versão `1.0.0`. O runtime usa `AppIdentity.version`
para manter headers HTTP e SyncPlay alinhados com o target Release. Um artefato só
pode ser publicado depois de `swift test` e do build de produção `xcodebuild`
em macOS/Xcode concluírem com código 0. As notas devem descrever somente o diff
validado e o artefato deve ser assinado para distribuição, nunca um build
`Debug` ou um simulador tratado como release.

Sempre que uma release iOS for criada, as páginas
`portal-site/index.html`, `portal-site/downloads.html` e `portal-site/docs.html`
devem ser atualizadas com a mesma versão, link do artefato, notas e status de
validação. A publicação não deve anunciar um link antes de o artefato existir.

As preferências de áudio e legenda usam a mesma normalização do Android: aliases
como `por` e `pt-BR` representam o mesmo idioma, a opção de legendas desativadas
remove explicitamente a seleção do AVPlayer e, quando a preferência não está
disponível, a faixa padrão do servidor é usada antes do primeiro fallback.

Primeiro vertical slice nativo do cliente Apple, alinhado ao contrato do
`MulletaFlix-android`: autenticação por usuário/senha, sessão no Keychain,
consulta de itens recentes/em andamento e resolução de artwork pelo servidor.

Falhas de autenticação e conexão seguem mensagens acionáveis equivalentes às do
Android: credenciais inválidas, falta de permissão, servidor ausente e timeout
são diferenciados, preservando também mensagens específicas devolvidas pela API.
O cadastro usa a mesma política e não exibe descrições brutas do transporte.
Os fluxos de catálogo, TV ao vivo, SyncPlay, controle remoto, playlists e
downloads também traduzem falhas de transporte antes de exibi-las.

## Abrir no Xcode

No macOS, abra `MulletaFlix.xcodeproj` no Xcode 16 e execute o scheme
`MulletaFlix` em um simulador iOS 18+. O projeto já inclui o target executável,
a camada de domínio/rede e as telas de login, Quick Connect, Home, bibliotecas,
detalhe, busca, TV ao vivo e perfil. `Package.swift` continua disponível para
executar os testes da camada Core isoladamente.

No Windows, execute `scripts/validate-structure.ps1` para verificar XML,
versionamento, conformidade de distribuição, guards de release e testes
declarados. Essa checagem não substitui `swift test` nem `xcodebuild`, que
continuam exigindo macOS/Xcode.

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
Enquanto a aba está selecionada, canais, guia, gravações e timers são atualizados
a cada 60 segundos; a tarefa é cancelada ao trocar de aba. Refreshes concorrentes
de TV ao vivo são protegidos por geração e sessão, portanto respostas antigas não
substituem os dados da atualização ou da conta atuais.
Falhas parciais preservam os dados já carregados e exibem retry contextual na tela.
Agendamentos em andamento ficam protegidos contra toques duplicados e respostas
da conta anterior, com progresso visível no programa correspondente.
Na tela de login, "Encontrar na rede local" replica a descoberta UDP do Android
(porta 7359, mensagem `who is MulletaFlixServer?`). O perfil consulta as
permissões reais do usuário no servidor e exibe o avatar quando há
`PrimaryImageTag` disponível.

O perfil também permite alternar entre usuários públicos do mesmo servidor,
solicitando a senha somente quando o servidor exigir.

Durante o Quick Connect, falhas transitórias de rede mantêm o código aguardando
até o prazo final; respostas terminais como 401, 403 e 404 exibem uma mensagem
acionável sem revelar descrições brutas do transporte.

O carregamento do perfil possui estado próprio e retry contextual; quando uma
atualização falha, as permissões e os dados já exibidos permanecem visíveis.

A Home também mantém um snapshot privado e versionado de “Continuar assistindo”
e “Minha Lista”, separado por servidor e usuário. O snapshot guarda somente
metadados dos cards, nunca URLs de mídia, fontes ou credenciais; respostas
online substituem a seção correspondente, enquanto apenas falhas transitórias
de rede podem exibir o conteúdo salvo, identificado na interface como “salvo”.
Falhas de autenticação não reutilizam dados antigos.

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
O polling da tela ocorre a cada cinco segundos, mas ignora um ciclo em segundo
plano enquanto a consulta anterior está ativa; o refresh manual continua podendo
substituir a consulta atual.

O Perfil também oferece controle remoto das sessões ativas em outros dispositivos.
O fluxo consulta `Sessions?controllableByUserId=...&activeWithinSeconds=300`,
exclui o dispositivo iOS atual e ignora sessões sem `NowPlayingItem` antes de
enviar `PlayPause`, `Stop` ou `Seek` com o usuário autenticado. Falhas de
atualização limpam a lista e mostram retry; falhas de comando preservam as
sessões e ações concorrentes na mesma sessão são bloqueadas.
Quando a sessão informa duração, o iOS mostra progresso e tempo decorrido e
limita o avanço de 30 segundos ao fim conhecido; sessões sem duração não exibem
progresso inventado.
Enquanto a tela está aberta, as sessões são atualizadas a cada cinco segundos;
um ciclo em segundo plano é ignorado quando a consulta anterior ainda está em
andamento, evitando cancelar ou sobrepor requisições.

Relatos de falha de reprodução usam a sessão e o escopo servidor/usuário atuais.
Falhas de transporte entram numa fila persistente limitada a 50 itens, com
descrições normalizadas em até 1.000 caracteres; payloads corrompidos não são
tratados como uma fila vazia nem sobrescritos silenciosamente.
Quando a fila está ilegível, o Perfil informa o estado e o cliente bloqueia
novos envios até recuperação, preservando o payload original.

A qualidade padrão “Automático” acompanha a rede: em conexão celular ou com
Modo de Poucos Dados ativo, o streaming aplica um teto de 720p/4 Mbps sem
alterar a preferência salva. Qualidades escolhidas manualmente continuam
respeitadas; downloads locais não recebem esse limite.

Na guia de TV ao vivo, os timers agendados preservam o `Id` retornado por
`LiveTv/Timers`. Quando esse ID está disponível, a ação “Cancelar gravação” usa
`DELETE LiveTv/Timers/{timerId}`; atualizações iniciadas antes de um agendamento
ou cancelamento não podem restaurar um estado antigo.

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
a posição retornada pelo servidor como fallback. Legendas externas suportadas
também são baixadas como sidecars de texto, isoladas por servidor e usuário,
validadas contra conteúdo binário/HTML e removidas junto com o download; o player
local as oferece sem rede quando o sidecar foi salvo.
Durante a reprodução de downloads, capítulos e segmentos também não são
consultados no servidor quando a indisponibilidade já foi confirmada.
Ao sair do player, a posição local é persistida imediatamente e limitada à
duração conhecida; sessões online enviam `Sessions/Playing/Stopped` uma única
vez, mesmo quando o usuário fecha a tela antes do fim da mídia.

Em detalhes de séries, a temporada selecionada pode ser preparada em lote para
download. O iOS deduplica episódios já enfileirados ou disponíveis, exibe o
progresso acessível, permite cancelar a preparação e mantém os episódios já
adicionados na fila offline.

A fila pode ser pausada e retomada globalmente; novos downloads permanecem
enfileirados enquanto a fila estiver pausada.
Com a preferência “Baixar somente no Wi-Fi”, novos downloads ficam enfileirados
em rede móvel e a fila retoma automaticamente quando o Wi-Fi volta.
Mesmo com essa preferência desativada, a fila permanece parada enquanto a
indisponibilidade geral da rede estiver confirmada.
O sistema também recebe os eventos de conclusão da sessão em background quando
o aplicativo é suspenso. Ao iniciar novamente, a fila preserva o estado,
reassocia tarefas background ainda existentes e reinicia pela URL persistida as
que não puderam ser recuperadas.
Se o app for encerrado durante uma pausa global, os itens interrompidos são
normalizados para a fila e aguardam a retomada manual antes de iniciar.

O cliente monitora a conectividade local e, ao detectar reconexão, atualiza
Home, bibliotecas abertas, Favoritos, Perfil, playlists, TV ao vivo, SyncPlay e
controle remoto automaticamente. Enquanto estiver offline, um
indicador informa que o conteúdo já armazenado pode continuar sendo usado; a
Home também identifica se “Continuar assistindo” e “Minha Lista” vieram do
snapshot local e exibe a data desse cache. Sem snapshot, orienta o usuário a
abrir Downloads para reproduzir mídias baixadas.

A tela de downloads também oferece busca local, filtro por estado, resumo de
armazenamento e limpeza em lote de downloads concluídos ou falhos.
Downloads concluídos também podem ser reproduzidos diretamente nessa tela,
usando o arquivo local e a posição offline persistida, sem depender de metadados
online. Quando o item fornece arte, uma cópia limitada é persistida no mesmo
escopo do download e apresentada offline; se ela não puder ser salva, a URL
autenticada continua sendo um fallback opcional, sem tornar a imagem requisito
para o player.
Episódios preservam opcionalmente série, temporada e número do episódio; esse
contexto aparece na fila, na busca local e nos controles do player offline.
Ao iniciar um download concluído, o player também oferece o próximo episódio
concluído da mesma série, priorizando o arquivo local e preservando suas legendas.
Índices antigos continuam válidos e itens sem metadados de episódio mantêm o
título original.

O detalhe permite listar playlists, criar uma nova playlist já com o item atual
e adicionar o item a uma playlist existente usando as rotas `Playlists` do
servidor.

O Perfil também oferece “Minhas playlists”. A tela carrega os itens de cada
playlist pela rota paginada `Playlists/{playlistId}/Items`, incluindo imagens e
dados do usuário, preserva a playlist selecionada durante a navegação e oferece
retry quando a consulta falha. Cada título abre o mesmo detalhe e fluxo de
reprodução do catálogo.

O formulário “Solicitar mídia” consulta sugestões do catálogo STRM em
`UserFeedback/MediaSuggestions` após 250 ms de inatividade. As sugestões ficam
limitadas a dez itens, são descartadas quando a sessão muda ou uma consulta
mais nova termina depois, e a seleção preenche título, tipo e ano antes do
envio da solicitação.

O histórico de buscas recentes é persistido por servidor e usuário autenticado,
com remoção individual e limpeza completa sem compartilhar termos entre contas.

Na tela de acesso, o iOS também lê QR codes de servidor com `VisionKit`. O
payload pode ser uma URL HTTP/HTTPS direta ou o esquema
`mulletaflix://server?url=...`; o parser normaliza esquema, host e barra final,
e rejeita credenciais, query, fragmentos, esquemas não HTTP e payloads inválidos.

Metadados de detalhe como gêneros, classificação indicativa, nota, duração e
elenco/equipe são preservados do payload do servidor e apresentados no iOS,
incluindo itens de música e livros quando o catálogo os fornece.

Faixas de legenda externas também preservam codec, idioma, título, estado
forçado e `DeliveryUrl`. O cliente iOS valida os formatos suportados e expõe a
rota autenticada `Items/{itemId}/Subtitles/{index}/Stream` para a reprodução.
No player, essas faixas aparecem junto das legendas embutidas, são carregadas
com autenticação e exibidas como cues WebVTT/SRT sobre o vídeo.
O tamanho (14–36 pt) e a cor das legendas externas podem ser ajustados em
Preferências > Legendas.

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
Enquanto o player está ativo, o iOS publica título, posição, duração e estado
de reprodução no Control Center e aceita play/pause pelos comandos remotos do
sistema; essas informações são removidas ao sair do player.

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
O progresso e o encerramento remoto são limitados ao duration conhecido antes
de serem convertidos em ticks, evitando posições inválidas no servidor.

Detalhes de séries carregam temporadas e episódios pelas rotas
`Shows/{id}/Seasons` e `Shows/{id}/Episodes`, com seleção de temporada e
navegação para cada episódio. Ao final de um episódio, o player também consulta
o próximo episódio da temporada ou da temporada seguinte e oferece a ação
contextual para continuar assistindo; com reprodução automática habilitada,
essa ação inicia após uma contagem regressiva de cinco segundos cancelável.
Quando o episódio atual e o próximo já estão concluídos nos Downloads, essa
sequência é resolvida localmente por série, temporada e episódio, sem depender
de conexão; sidecars de legenda baixados acompanham essa transição para o
player local.

O detalhe mostra “Continuar” quando existe progresso local ou remoto e “Assistir”
para títulos sem posição salva. Também permite marcar e desmarcar títulos como assistidos usando
`Users/{userId}/PlayedItems/{itemId}`, preservando o contexto de temporada e
episódio no estado local.

A busca oferece histórico, sugestões, filtros por tipo — incluindo Livros — e
entrada por voz com `Speech`/`AVAudioEngine`, respeitando as permissões nativas
do iOS. O filtro de Livros envia `IncludeItemTypes=Book`, mantendo o contrato
de busca do Android.
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
Picture-in-Picture, pulo manual e automático da introdução, velocidade, qualidade padrão e oito temas), idiomas preferidos de áudio/legenda e
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
O pulo automático da introdução é opcional, fica desligado por padrão e só
avança segmentos `Intro` durante reprodução seekable; créditos e outros tipos
de segmento nunca são pulados automaticamente.
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

Relatos de problemas de reprodução podem ser abertos no detalhe do item ou
diretamente no player, pelo OSD e pelo cartão de erro. Eles também sobrevivem a
falhas transitórias de rede: o iOS grava a fila por servidor e usuário, informa
que o relato foi salvo e reenvia somente quando a mesma sessão voltar a ter
conectividade. Respostas de autorização ou erros permanentes não entram na fila.

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

Quando a ordenação é por nome ascendente, coleções com múltiplos títulos também
exibem um índice alfabético lateral para saltar entre letras, normalizando
acentos e agrupando nomes sem letra em `#`.

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
Quando o dispositivo está offline, nenhuma consulta de busca é iniciada; a
interface informa que a busca será retomada e repete o termo e filtro atuais
assim que a conectividade retorna.
Detalhes e contexto de séries preservam os metadados já carregados e não
iniciam consultas auxiliares enquanto o offline estiver confirmado.
As cargas de Home, Biblioteca, Favoritos e TV ao vivo também não iniciam novas
requisições enquanto a indisponibilidade já foi confirmada; dados de snapshot
continuam visíveis quando existentes e a atualização é refeita na reconexão.
Perfil, SyncPlay e controle remoto seguem a mesma regra: consultas e comandos
online são bloqueados durante o offline confirmado, sem apagar o estado já
apresentado.
Ações de favorito, assistido, gravação, playlist e solicitação de mídia também
são recusadas localmente nesse estado, evitando alterações otimistas que não
poderiam ser confirmadas pelo servidor.
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
