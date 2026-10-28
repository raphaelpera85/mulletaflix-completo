# Auditoria de cobertura do backup integral

Data: 2026-10-21
Estado: cobertura integral ainda parcial; formato 0.3.0 cobre cinco contextos relacionais com snapshot comum e restore relacional atômico, além de rollback compensatório de arquivos em falhas observadas. Restore rejeita ZIP/JSON inválidos e reparse points descendentes antes de sobrescrever arquivos; crash recovery e outras stores continuam fora.

## Inclusão local dos contextos Movies/Series/Channels/Books — 2026-10-21

No checkout v12.1.35, a alteração local evolui o formato relacional de `0.2.0`
para `0.3.0`: o manifesto/export/validação/restauração passam a incluir as
tabelas dos contextos `MoviesDbContext`, `SeriesDbContext`,
`ChannelsDbContext` e `BooksDbContext`, além do contexto central. Backups `0.2.0`
continuam aceitos e não limpam/restauram tabelas de domínio ausentes no formato
legado.

Estado da validação foi substituído pela seção “Snapshot consistente e rollback
relacional entre todos os contextos — 2026-10-21”, abaixo.
`IntroSkipperDbContext`, `DetectionCacheDbContext`,
MongoDB, Supabase, criptografia, retenção, rotação e integridade autenticada
continuam fora da cobertura. Não houve instalação nem release.

## Snapshot consistente e rollback relacional entre todos os contextos — 2026-10-21

Além do restore atômico entre os cinco contextos relacionais, a criação do
arquivo agora abre contexts não agrupados somente para a operação, associa todos
à conexão do contexto central e inicia uma transação `RepeatableRead` antes da
leitura do histórico e das tabelas. Isso fornece um snapshot relacional comum
durante a exportação e evita alterar as conexões dos contexts reutilizáveis do
pool. A transação é concluída antes do descarte dos contexts.

O teste MariaDB injeta atualizações em todos os quatro contexts de domínio após
a serialização do histórico; o backup precisa continuar contendo os valores
anteriores em todos eles. A regressão passou junto dos demais casos em
`BackupRestoreMariaDbTests` 6/6 (MariaDB 11.4.4 descartável, loopback 33099),
`FullSystemBackup` 42/42 e build Release de `Jellyfin.Server.csproj` (0 avisos,
0 erros). A porta operacional 3306 não foi acessada.

O teste não converte o backup em cópia integral: o snapshot comum cobre os
cinco contexts relacionais; filesystem não participa da transação, e
IntroSkipper/DetectionCache, MongoDB e Supabase seguem pendentes. Sem instalação,
produção, publicação de release ou homologação formal nesta etapa.

## Revalidação do limite transacional — 2026-10-21

Registro da etapa anterior ao teste intercontextos; o limite de restore descrito
abaixo foi corrigido na seção “Rollback relacional entre todos os contextos”.

No checkout de auditoria baseado no commit `d51f4f38` (tag local
`v12.1.35`), o manifesto passou a registrar os nomes reais das entradas de
tabela (antes todos os `DbSet` eram declarados como ``DbSet`1``), e a restauração
relacional agora propaga falhas de inserção e confirma uma transação antes de
copiar `Config`, `Data`, `Root` e metadados. A criação da tabela de histórico
EF fica antes da transação, pois MariaDB executa esse comando com transação
suprimida; as alterações de histórico e entidades ficam no bloco transacional.

Validação em MariaDB 11.4.4 isolado, `127.0.0.1:13316`, com diretório de dados
temporário: `BackupRestoreMariaDbTests` passou 4/4, incluindo restauração de
linhas e injeção de falha SQL que comprova rollback e preservação dos arquivos.
O serviço de produção em `127.0.0.1:3306` permaneceu intocado. `FullSystemBackup`
passou 40/40; testes Nebula de sugestões passaram 14/14 e `NebulaUploadEngine`
224/224. O build Release focado de `Jellyfin.Server.Implementations.csproj`
terminou com 0 avisos e 0 erros. A compilação do projeto de integração emitiu
avisos de documentação/analisadores preexistentes; esses avisos não foram
tratados nesta alteração.

Limites restantes: o commit/restore atômico entre banco e filesystem não é
possível com a transação relacional; falha durante a cópia posterior ainda pode
deixar arquivos parciais. Esta alteração cobre somente
`MulletaFlixDbContext`, não as factories/contextos de mídia do trabalho
paralelo. Mudanças seguem locais, sem commit, publicação ou release; ainda não
estão instaladas.

## Rollback relacional entre todos os contextos — 2026-10-21

Uma injeção de falha em `Series` reproduziu a perda de dados anteriores: o
restore central e `Movies` já haviam sido confirmados quando a gravação da
série falhou. `BackupService` agora constrói contextos de domínio não agrupados
somente para o restore, associa-os à mesma conexão/transação do
`MulletaFlixDbContext`, restaura todas as tabelas no mesmo commit e devolve cada
contexto à sua conexão original antes do descarte. Os pools usados pelo restante
do servidor não são alterados. Backups legados `0.2.0` continuam sem tentar
limpar/restaurar tabelas de domínio ausentes.

`BackupRestoreMariaDbTests` passou 6/6 contra MariaDB 11.4.4 descartável em
`127.0.0.1:33099`, incluindo falhas injetadas em `ActivityLogs`, `Series` e
`Books`, preservação de linhas nos cinco contextos e arquivos intactos, além do
round-trip das tabelas de domínio. `FullSystemBackup` passou 42/42; build
Release de `Jellyfin.Server.csproj` passou com 0 avisos/0 erros. A integração
compilou com avisos preexistentes de analisadores. A instância operacional em
`127.0.0.1:3306` permaneceu sem acesso; a instância descartável foi parada.

Esta etapa anterior validou o rollback relacional do restore. A consistência
temporal da criação do snapshot foi validada posteriormente na seção acima; não
há atomicidade entre commit do banco e cópia posterior do filesystem.
`IntroSkipperDbContext`, `DetectionCacheDbContext`, MongoDB,
Supabase, criptografia, retenção e instalação continuam pendentes. Alterações
locais, não publicadas nem instaladas.

## Escopo verificado

`Jellyfin.Server.Implementations.FullSystemBackup.BackupService` usa o contexto
central e os quatro contextos de mídia. O construtor DI recebe opções/loggers
para instanciar contextos não agrupados no restore; factories agrupadas continuam
para o uso comum. O formato `0.3.0` cobre os cinco contextos e `HistoryRow`.
Entradas relacionais obrigatórias são validadas antes da substituição de dados;
backups `0.2.0` são mantidos sem purgar tabelas de domínio que não contêm.

### Reconciliação dos contextos EF e bancos

Foram identificados nove contextos EF Core de produção: `MulletaFlixDbContext`,
`UsersDbContext`, `MoviesDbContext`, `SeriesDbContext`, `ChannelsDbContext`,
`BooksDbContext`, `SystemDbContext`, `IntroSkipperDbContext` e
`DetectionCacheDbContext`. `ServiceCollectionExtensions.AddMulletaFlixDbContext`
registra os sete primeiros com a mesma instância de `efCoreConfiguration`; o
backup atual enumera tabelas do contexto central e dos quatro contextos de mídia. `UsersDbContext`
e `SystemDbContext` contêm entidades cujas tabelas também estão no modelo
central, portanto não devem ser adicionados como exportações duplicadas sem
antes definir a propriedade física e a fonte de verdade.

Os quatro modelos de domínio (`MoviesDbContext`, `SeriesDbContext`,
`ChannelsDbContext` e `BooksDbContext`) mapeiam tabelas próprias, incluídas pelo
formato 0.3.0 no backup geral; na versão 0.2.0 essas tabelas não eram
enumeradas. `DomainSchemaInitializer` usa explicitamente
`DatabaseNames.Main` para criá-las, e a rotina de migração chama esse
inicializador com os contextos de domínio. O setup MariaDB também cria bancos
com nomes `mulletaflix_users`, `mulletaflix_movies`, `mulletaflix_series`,
`mulletaflix_channels` e `mulletaflix_books`, mas os factories observados não
selecionam esses nomes individualmente. Isso prova a configuração no código,
não a topologia física de uma instalação nem o provider ativo em produção.

IntroSkipper registra `IntroSkipperDbContext` e `DetectionCacheDbContext` com a
opção de banco `mulletaflix_introskipper`; seus nove conjuntos de tabelas não
aparecem no backup central. A confirmação de cobertura requer declarar o
ownership por banco/tabela e testar a combinação real de provider e migração;
não se deve tratar nomes dos contextos como prova de bancos independentes.

O banco MongoDB do Nebula também é externo ao `BackupService`. A fonte identifica
seis coleções usadas pelo servidor: `files`, `nebula_admission`, `users`,
`bot_tokens`, `operation_replays` e `stats` (esta última é consultada como
opcional para estatísticas do feeder). A configuração `BotTokensCollection`
também permite ler outra coleção Mongo com nome configurável. O fluxo de
sincronização Nebula/Supabase é separado e não prova cobertura integral dessas
coleções nem dos outros contextos relacionais. Nenhum banco remoto ou produção
foi acessado nesta auditoria.

Regressão encontrada nesta sessão: `BotTokensCollection` era usada pelo
carregamento operacional, mas a sincronização Supabase lia e restaurava somente
`bot_tokens`. A fonte foi alterada para propagar o nome configurado ao backup,
restore e importação de tokens. A regressão exercita coleções padrão e
customizada; os dois casos passaram com MongoDB 8.3.7 descartável limitado a
`127.0.0.1:27099`. A suíte `FullyQualifiedName~Nebula` passou 730/730 com o
mesmo fixture. Isso valida o fluxo local, não a conexão real do Supabase nem
backup integral/produção. A correção está incluída em `12.1.29`.

O ZIP também inclui arquivos XML/JSON de configuração de primeiro nível,
`Config/users`, tarefas agendadas, `Root`, coleções e playlists; legendas,
trickplay e metadados dependem das opções. Não há criptografia explícita na
criação do ZIP.

## Riscos e compatibilidade

- O manifesto aceita explicitamente os formatos `0.2.0` e `0.3.0` e rejeita
  versões futuras/desconhecidas. O formato 0.2.0 mantém semântica legada e não
  deve purgar tabelas de domínio ausentes. O 0.3.0 exige tabelas dos cinco
  contextos relacionais e `HistoryRow`, com entradas JSON correspondentes.
- A restauração valida CRC e comprimento de todas as entradas do ZIP e
  desserializa o conteúdo relacional obrigatório antes de sobrescrever dados.
  Restore 0.3.0 compartilha uma transação MariaDB entre os cinco contextos;
  arquivos usam staging/rename e compensação in-process para reverter falhas
  observadas. Isso não cobre stores não incluídos nem recuperação após crash ou
  perda de energia entre efeitos no filesystem e commit relacional. O guard de
  caminhos rejeita reparse points em descendentes, mas swap TOCTOU por ator
  privilegiado ainda depende de ACLs.
- A validação semântica do componente relacional ocorre antes de copiar
  `Config`, `Data` ou `Root` e antes de abrir o banco. Declaração ausente,
  tabela não suportada ou entrada ausente produz `InvalidDataException`.
- Arquivos de configuração podem conter dados sensíveis. A ausência de
  criptografia explícita exige uma decisão de proteção e gestão de chave antes
  de incluir mais dados de usuário/operacionais no mesmo arquivo.
- O provider MariaDB tem um dump nativo usado em backups de migração; o fluxo
  `BackupService` não o incorpora. Reutilizá-lo exigiria validar disponibilidade
  de ferramentas, consistência, portabilidade e semântica de restauração.

## Evidência e próximos gates

Reconciliação estática dos contextos, factories e inicializador feita em
2026-10-21: `ServiceCollectionExtensions.cs`, `DomainDbContexts.cs`,
`UsersDbContext.cs`, `DomainSchemaInitializer.cs`,
`MariaDbProcessManager.cs` e `PluginServiceRegistrator.cs`. Nenhum banco ativo,
provider de produção ou instalação foi consultado; a inferência sobre bancos
físicos efetivos continua pendente.

Em 2026-10-21, `FullSystemBackup` passou 39/39 testes Release. Três novos casos
provam que JSON sintaticamente inválido, item nulo e entidade incompatível são
rejeitados sem sobrescrever configuração nem abrir o contexto de banco. Os
casos prévios também cobrem declaração ausente, entrada ausente e tabela
desconhecida. A suíte completa `Jellyfin.Server.Implementations.Tests` passou
1.515, ignorou 65 casos opt-in/plataforma e teve zero falhas.
O build de `Jellyfin.Server.Implementations` passou em Release sem erros; a
última execução reportou quatro avisos XML em projetos referenciados. Os
builders Windows e Linux também geraram os pacotes da release
12.1.29; os pacotes Windows, ZIP e tar.gz foram inspecionados quanto à presença
do servidor, frontend e worker Nebula, e hashes/tamanhos foram registrados em
`docs/releases/12.1.29.md`. A regressão Supabase/Mongo para coleção padrão e
customizada passou 2/2 e a suíte Nebula passou 730/730, com Mongo descartável
em `127.0.0.1:27099`. Scripts Node de release/packaging passaram 23/23. Estes
gates não comprovam cobertura de todos os contextos/coleções nem um restore
integral de ponta a ponta.

Em 2026-10-21, a regressão ampla em Windows revelou três testes de criação de
backup dependentes do espaço livre real do disco do agente: o host tinha menos
de 5 GiB, então casos que não deveriam exercitar capacidade falharam antes de
validar seu comportamento. `BackupService` agora consulta `IStorageSpaceProvider`,
registrado no container e implementado por `StorageSpaceProvider` via
`StorageHelper`; os testes usam um provedor controlado e um caso verifica a
recusa real abaixo do limite sem depender do disco. `FullSystemBackup` passou
40/40. A suíte Release com filtro `FullyQualifiedName!~Integration` passou
5.091 casos, ignorou 68 opt-in/plataforma e teve zero falhas. A validação não
altera o diagnóstico anterior de cobertura parcial dos demais contextos e
coleções nem equivale a restore integral.

Antes de chamar o recurso de backup integral novamente, executar nesta ordem:

1. Mapear propriedade física de cada tabela/schema e cada coleção externa,
   distinguindo fonte de verdade, dados duplicados e compatibilidade legada.
  2. Evoluir o manifesto para declarar componentes relacionais, Mongo e Supabase,
   omissões e hashes; manter compatibilidade legada de `0.2.0`. A pré-validação
    semântica cobre entradas dos cinco contextos relacionais, mas ainda não cobre
    coleções Mongo ou Supabase.
3. Definir criptografia do arquivo, armazenamento e rotação/recuperação de
   chave; não copiar credenciais para o manifesto ou documentação.
4. Completar restore das bases IntroSkipper/DetectionCache e coleções externas,
   com testes isolados, falha intermediária e idempotência. Snapshot comum dos
   cinco contexts relacionais foi validado em 2026-10-21.
5. Expor cobertura e última execução por componente na UI/API; não confundir
   sincronização Supabase de subconjuntos com backup integral.
6. Manter os testes de restore isolado e homologação em regressão contínua. A
   correção de coleção customizada está validada e publicada em 12.1.29. O ponto
   de partida desta release era 12.1.28. Esta auditoria não declara cobertura
   integral; instalação, host Linux nativo e produção não foram homologados por
   esta sessão.

### Defesa contra symlink/junction dentro do destino — 2026-10-21

O restore agora normaliza e verifica contenção do caminho e rejeita reparse
points já existentes em descendentes da raiz de destino antes de criar pastas,
gravar o temporário ou instalar o arquivo. A raiz configurada em si permanece
confiável, permitindo destinos explicitamente apontados a um link; links abaixo
dela são recusados. O teste `RestoreBackupAsync_RejectsReparsePointWithinConfiguredTargetRoot`
criou um symlink local e passou, confirmando que o conteúdo externo não foi
alterado. `FullSystemBackup` passou 44/44 e `NebulaUploadCheckpointJournalTests`
5/5 em Release (49/49 no filtro combinado); `git diff --check` passou.

Limites ainda abertos: uma troca concorrente do caminho por ator privilegiado
entre revalidação e rename (TOCTOU) depende de ACLs; compensação em memória não
recupera desligamento abrupto entre gravação de arquivos e commit relacional;
restore positivo legado 0.2.0 precisa de fixture MariaDB. A auditoria Nebula
também identificou que operações de escrita usam `worker_id` sem sempre fazer
fencing pelo `lease_id`; isso continua pendente. O usuário confirmou manter
retentativas automáticas Telegram; o comportamento continua at-least-once e não
garante exactly-once quando não se consegue reconciliar uma publicação aceita.
Nenhum Mongo/Supabase/Telegram remoto, runtime instalado, produção, instalação
ou release foi acessado nesta sessão. A homologação formal segue aberta.

## Fontes primárias

- `Jellyfin.Server.Implementations/FullSystemBackup/BackupService.cs`
- `Jellyfin.Server.Implementations/FullSystemBackup/BackupManifest.cs`
- `Jellyfin.Server.Implementations/Extensions/ServiceCollectionExtensions.cs`
- `MulletaFlix.Plugin.IntroSkipper/PluginServiceRegistrator.cs`
- `MulletaFlix.Plugin.IntroSkipper/Db/IntroSkipperDbContext.cs`
- `MulletaFlix.Plugin.IntroSkipper/Db/DetectionCacheDbContext.cs`
- `Jellyfin.Server.Implementations/Nebula/NebulaMongoContext.cs`
- `Jellyfin.Server.Implementations/Nebula/NebulaSupabaseSyncService.cs`
- `tests/Jellyfin.Server.Implementations.Tests/FullSystemBackup/BackupServiceTests.cs`

## Rollback compensatório de arquivos no restore — 2026-10-21

O restore não sobrescreve mais o destino diretamente. Cada entrada é gravada
temporariamente ao lado do destino e instalada por rename no mesmo volume; o
arquivo anterior é mantido como backup temporário até a restauração relacional
terminar. Falha de cópia ou do restore relacional desfaz as substituições já
aplicadas em ordem reversa. Falhas ao desfazer preservam o backup temporário e
geram log crítico para recuperação manual. Mantém-se a semântica de merge: itens
ausentes do arquivo não são removidos.

Regressão isolada reproduziu falha no segundo arquivo e comprovou a preservação
do primeiro. `FullSystemBackup` passou 43/43 e o build Release de
`Jellyfin.Server.csproj` terminou com 0 avisos/erros. A regressão MariaDB foi
ampliada para verificar também que falha de filesystem não modifica o banco e
que falha relacional restaura os arquivos anteriores; os 3 casos foram
compilados, mas ignorados nesta execução porque não havia MariaDB descartável
configurado. Homologação formal não foi pontuada por esses testes focais.

Limite explícito: a compensação trata exceções observáveis durante o processo,
mas não oferece recuperação garantida para queda abrupta ou perda de energia
entre renames/commit. Um journal durável ainda é necessário para essa garantia.
O restore legado dos contextos de domínio, quando factories não agrupados não
estão disponíveis, continua com commits separados. Não houve instalação,
produção nem release do servidor.
Evidência de andamento publicada no portal Git `main`, commit
`8716cbeeb81bf5c1ffdcc9198141af9db834c223`; os 212 testes de contrato passaram,
Vercel Production `READY` (`dpl_9hycv9vn5LpuY79CoQqE2DuSXwjP`) e o endpoint
público retornou HTTP 200 com os dois eventos desta sessão. Isso não altera o
gate formal de homologação.

### Revalidação 2026-10-21 — construtor sem factories atômicas

O restore 0.3.0 em `BackupService` agora falha fechado quando as factories que
compartilham a transação não foram fornecidas. A validação ocorre antes de
abrir `MulletaFlixDbContext` e antes de aplicar arquivos; nenhuma tabela nem
configuração é alterada. O formato 0.2.0, sem tabelas de domínio, continua
compatível. A regressão unitária verificou que a factory não é chamada e que
o arquivo de configuração preserva os bytes originais. `Jellyfin.Server.Implementations.Tests`
passou com 1.537 aprovados, 70 ignorados opt-in e nenhuma falha. Testes MariaDB
isolados continuam opt-in e não foram executados nesta sessão.
