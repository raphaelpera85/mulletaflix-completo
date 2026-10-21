# Auditoria de cobertura do backup integral

Data: 2026-10-21
Estado: cobertura integral ainda parcial; pré-validação CRC implementada para restore.

## Escopo verificado

`Jellyfin.Server.Implementations.FullSystemBackup.BackupService` injeta somente
`IDbContextFactory<MulletaFlixDbContext>`. O arquivo lógico da base inclui os
`DbSet` públicos desse contexto e `HistoryRow`; a restauração purga/restaura
esse mesmo contexto. As entradas ausentes para tabelas são ignoradas, exceto o
histórico de migrations, que é obrigatório.

Além de `MulletaFlixDbContext`, a aplicação registra `UsersDbContext`,
`MoviesDbContext`, `SeriesDbContext`, `ChannelsDbContext`, `BooksDbContext` e
`SystemDbContext`. Essas fábricas usam o provider/configuração EF central; não
se deve inferir um banco físico separado para cada contexto. Seus modelos não
são enumerados pelo `BackupService` e precisam ser reconciliados por tabela
física antes de ampliar o formato. A extensão IntroSkipper registra ainda
`IntroSkipperDbContext` e `DetectionCacheDbContext` no schema separado
`mulletaflix_introskipper`; esses dados também não aparecem no backup geral.

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
  apenas adicionar tabelas à versão atual pode resultar em restore parcial
  silencioso.
- A restauração agora valida CRC e comprimento de todas as entradas do ZIP
  antes de verificar espaço e antes de gravar qualquer arquivo ou banco. ZIP
  corrompido é rejeitado sem sobrescrever a configuração existente. Isso não
  torna a restauração transacional: erros semânticos posteriores, como JSON
  inválido, componente ausente ou falha no banco, ainda podem deixar estado
  parcialmente aplicado.
- Arquivos de configuração podem conter dados sensíveis. A ausência de
  criptografia explícita exige uma decisão de proteção e gestão de chave antes
  de incluir mais dados de usuário/operacionais no mesmo arquivo.
- O provider MariaDB tem um dump nativo usado em backups de migração; o fluxo
  `BackupService` não o incorpora. Reutilizá-lo exigiria validar disponibilidade
  de ferramentas, consistência, portabilidade e semântica de restauração.

## Evidência e próximos gates

Em 2026-10-21, `FullSystemBackup` passou 33/33 testes Release, incluindo a
regressão `RestoreBackupAsync_RejectsCorruptedArchiveBeforeOverwritingFiles`.
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
2. Definir um manifesto versionado que declare componentes incluídos,
   ausentes e hashes; manter leitor compatível com `0.2.0` e rejeitar restore
   semanticamente incompleto antes de sobrescrever arquivos. CRC ZIP é agora
   pré-validado, mas não substitui essa validação semântica.
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
