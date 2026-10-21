# Auditoria de cobertura do backup integral

Data: 2026-10-21
Estado: cobertura integral ainda parcial; restore rejeita ZIP corrompido e conteúdo JSON relacional inválido/incompatível antes de sobrescrever arquivos.

## Escopo verificado

`Jellyfin.Server.Implementations.FullSystemBackup.BackupService` injeta somente
`IDbContextFactory<MulletaFlixDbContext>`. O arquivo lógico da base inclui os
`DbSet` públicos desse contexto e `HistoryRow`; a restauração purga/restaura
esse mesmo contexto. As entradas ausentes para tabelas são ignoradas, exceto o
histórico de migrations, que é obrigatório.

### Reconciliação dos contextos EF e bancos

Foram identificados nove contextos EF Core de produção: `MulletaFlixDbContext`,
`UsersDbContext`, `MoviesDbContext`, `SeriesDbContext`, `ChannelsDbContext`,
`BooksDbContext`, `SystemDbContext`, `IntroSkipperDbContext` e
`DetectionCacheDbContext`. `ServiceCollectionExtensions.AddMulletaFlixDbContext`
registra os sete primeiros com a mesma instância de `efCoreConfiguration`; o
backup geral enumera apenas propriedades do contexto central. `UsersDbContext`
e `SystemDbContext` contêm entidades cujas tabelas também estão no modelo
central, portanto não devem ser adicionados como exportações duplicadas sem
antes definir a propriedade física e a fonte de verdade.

Os quatro modelos de domínio (`MoviesDbContext`, `SeriesDbContext`,
`ChannelsDbContext` e `BooksDbContext`) mapeiam tabelas próprias que não são
enumeradas pelo backup central. `DomainSchemaInitializer` usa explicitamente
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

- O manifesto valida `BackupEngineVersion` por igualdade exata (`0.2.0`). Uma
  evolução de formato precisa manter leitura explícita dos arquivos legados;
  apenas adicionar tabelas à versão atual pode resultar em incompatibilidade. A
  validação exige que `DatabaseTables` corresponda exatamente aos DbSets
  exportados pelo `MulletaFlixDbContext` mais `HistoryRow`, e que cada entrada
  JSON correspondente exista no ZIP.
- A restauração valida CRC e comprimento de todas as entradas do ZIP antes de
  verificar espaço e antes de gravar qualquer arquivo ou banco. Também
  desserializa em streaming as entidades JSON de cada tabela do contexto
  relacional central antes de copiar `Config`, `Data` ou `Root` e antes de abrir
  o banco; JSON malformado, nulo ou incompatível é rejeitado sem sobrescrever a
  configuração existente. Isso não torna a restauração transacional: falhas de
  constraints do banco, componentes não cobertos ou falhas durante a aplicação
  ainda podem deixar estado parcialmente aplicado.
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

Antes de chamar o recurso de backup integral novamente, executar nesta ordem:

1. Mapear propriedade física de cada tabela/schema e cada coleção externa,
   distinguindo fonte de verdade, dados duplicados e compatibilidade legada.
2. Evoluir o manifesto para declarar componentes relacionais, Mongo e Supabase,
   omissões e hashes; manter compatibilidade legada de `0.2.0`. A pré-validação
   semântica agora protege as entradas relacionais do backup central, mas ainda
   não cobre contextos EF independentes nem coleções Mongo.
3. Definir criptografia do arquivo, armazenamento e rotação/recuperação de
   chave; não copiar credenciais para o manifesto ou documentação.
4. Implementar snapshots consistentes e restore por componente com testes
   isolados em MariaDB/MongoDB, incluindo falha intermediária e idempotência.
5. Expor cobertura e última execução por componente na UI/API; não confundir
   sincronização Supabase de subconjuntos com backup integral.
6. Manter os testes de restore isolado e homologação em regressão contínua. A
   correção de coleção customizada está validada e publicada em 12.1.29. O ponto
   de partida desta release era 12.1.28. Esta auditoria não declara cobertura
   integral; instalação, host Linux nativo e produção não foram homologados por
   esta sessão.

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
