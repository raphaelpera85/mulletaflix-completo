# Testes de integração MariaDB

## Rechecagem do log de deadlock — 2026-10-27 12:52 BRT

O anexo é idêntico aos registros anteriores (SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`). `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 20/20 em Windows/.NET 10 Release. Três regressões de integração foram selecionadas, mas ignoradas porque esta rodada não configurou uma conexão MariaDB descartável; portanto não contam como validação integrada. A instalação foi conferida somente por metadados/hash da DLL 12.1.35; a fonte local contém a proteção, mas não se confirmou qual runtime produziu o log. O teste MariaDB de concorrência do mesmo item havia passado 1/1 às 08:19, em instância descartável. Banco operacional, produção, CI hospedada e instalação não foram alterados/validados nesta rechecagem.

### Concorrência determinística entre refresh de imagem e exclusão — 2026-10-26 21:15 BRT

`DeleteItem_ConcurrentWithBlockedImageRefresh_CompletesWithoutDeadlock` segura
o advisory lock da stripe do item em uma sessão MariaDB, confirma que
`SaveImagesAsync` está bloqueado em `GET_LOCK`, só então inicia `DeleteItem` e
verifica que ambas as operações aguardam. Ao liberar a stripe, espera as duas
concluírem e confirma que item e imagens foram removidos. Em MariaDB 11.4.4
descartável, Release recompilado passou 3/3 junto com os testes separados de
espera de SaveImagesAsync e DeleteItem. A instância local usou somente
127.0.0.1:33259, foi encerrada e a porta fechou; a porta operacional 3306 não
foi acessada. O datadir sintético temporário permanece porque remoção recursiva
foi bloqueada pela ferramenta. Esta prova local não correlaciona o log antigo
com um binário, nem substitui carga prolongada, CI hospedada ou validação de
produção.

### Reinício do MariaDB durante persistência de imagens — 2026-10-26

O job `user-mariadb-linux-tests` valida recuperação após interrupção do processo
durante `SaveImagesAsync`. O teste cria um gatilho `SLEEP` no schema exclusivo e
grava um marcador somente quando o `DELETE` está bloqueado. O script confirma a
identidade única do container MariaDB 11.4 pela imagem e porta publicada 33099,
encerra e inicia esse mesmo container, espera `mariadb-admin ping` e grava um
segundo marcador. O teste só aprova se o restart foi confirmado e a imagem nova
ficou persistida como conjunto único. Os marcadores ficam no diretório temporário
do runner; o container tem datadir efêmero do job. Nenhum endpoint configurado
arbitrariamente pode ser reiniciado.

`SaveImagesAsync` permite até dez tentativas para falhas de conexão, com o atraso
linear já existente; deadlock e lock timeout (1205/1213) continuam limitados a
três tentativas. Os unitários focados passaram 35/35. Na preparação inicial, a
integração compilou, mas foi ignorada por ausência de endpoint configurado; a
execução real local foi concluída depois e está registrada na seção “Gate de
reinício do processo MariaDB”. O resultado hospedado ainda aguarda CI.

### Suíte de persistência de imagens — 2026-10-26 15:37 BRT

`ServerPersistenceMariaDbTests` passou 23/24 em Windows/.NET 10 Release contra
MariaDB 11.4.4 descartável (`max_connections=256`): 23 aprovados, 1 ignorado,
zero falhas. O skip é o cenário de restart, porque esta execução não recebeu os
marcadores do harness; o reinício real durante `DELETE` já passou 1/1 em execução
separada. O processo de teste foi validado e encerrado; a porta 33159 fechou e a
instância 3306/PID 31236 permaneceu intocada. O log detalhado foi mantido em
`%TEMP%\mfx-mariadb-restart-local\server-persistence-suite.log`.

A última execução hospedada observada está no SHA remoto antigo e terminou sem
runner/steps; portanto não valida este checkout. Escrita/`DELETE` sob carga,
upgrade de schema sob carga, runtime instalado e produção seguem pendentes.

### Upgrade da migração de proveniência de imagem — 2026-10-26

`LatestImageProvenanceMigration_PreservesExistingImageRows` cria uma imagem no
schema atual, volta à migração imediatamente anterior
(`20260926050701_DropUnusedFullTextSearchIndex`), reaplica
`20261022212236_AddRemoteImageProvenance` e confirma que o caminho da imagem
continua persistido e que as colunas novas ficam nulas para os registros antigos.
O caso passou 1/1 em MariaDB 11.4.4 descartável. A classe
`ServerPersistenceMariaDbTests` passou 24/25 (24 aprovados, 1 skip esperado do
restart que exige marcadores, zero falhas) em 42 s. Processo e datadir exclusivos
foram descartados/encerrados; a instância operacional 3306 não foi acessada.

O startup (`Program`) aguarda `CoreInitialisation` antes de `InitializeServices`,
então executar o schema upgrade com tráfego normal da aplicação não representa
o ciclo suportado. Permanecem relevantes a migração de dados existentes, o teste
de restart isolado, carga de escrita/`DELETE` no runtime, CI hospedada e runtime/
produção instalados.

### Benchmark sintético do índice de imagens — 2026-10-26

Em MariaDB 11.4.4 descartável, foi comparada a consulta seletiva por `ItemId`
em uma tabela sintética com 100.000 linhas e quatro imagens do item consultado.
`mysqlslap` executou cinco rodadas de 1.000 consultas sequenciais: com a chave
primária composta `(ItemId, ImageType)`, média 0,068 s por rodada (mín. 0,062;
máx. 0,079); após remover a chave, média 28,809 s (mín. 27,547; máx. 31,547).
O plano mudou de `ref`/estimativa 4 para `ALL`/estimativa 99.881, cerca de 423
vezes mais tempo neste experimento. A tabela e o schema eram sintéticos e foram
descartados; a instância ficou em loopback/33159 e foi encerrada. Este é um
benchmark de uma consulta e um cliente, não representa concorrência, operação
`DELETE`, hardware/carga de produção ou impacto global do índice.

### Gate de reinício do processo MariaDB — 2026-10-26

`MariaDbTestEnvironment.Validate` restringe o endpoint a loopback e porta não
padrão, mas não comprova propriedade/isolamento do processo. Por isso o caso de
restart nunca deve parar o endpoint apenas porque a connection string aponta
para loopback. O gate CI identifica o container MariaDB 11.4 exclusivo do job,
porta 33099, e reinicia esse mesmo container.

Em 2026-10-26, o caso `SaveImagesAsync_RetriesAfterMariaDbProcessRestartDuringDelete`
também passou 1/1 localmente contra MariaDB 11.4.4 em processo e datadir
descartáveis, bind `127.0.0.1:33159`. O runner confirmou PID, executável,
datadir e listener antes de interromper somente esse processo durante o trigger
`DELETE`; após reinício e readiness, `SaveImagesAsync` concluiu e a imagem nova
foi persistida. A porta 33159 foi fechada ao final. A porta operacional 3306,
PID 31236, não foi acessada nem alterada. A execução hospedada no GitHub Actions
e os cenários de carga/esquema continuam pendentes.

## Estado consolidado das fixtures e da CI — 2026-10-26 11:55 BRT

Auditoria dos callsites MariaDB confirmou o padrão nas fixtures `MariaDbFixture`,
`UsersMariaDbTestDatabase`, `SqliteToMariaDbMigrationTests`,
`ServerPersistenceMariaDbTests`, `JellyfinApplicationFactory` e
`BackupRestoreMariaDbTests`: a conta administrativa provisiona/remove schemas e
usuários; operações da aplicação usam credenciais limitadas aos schemas
descartáveis. O serviço root do workflow é efêmero e serve ao bootstrap das
fixtures. As anotações históricas abaixo descrevem o estado na data de cada
auditoria; pendências de fixtures foram reconciliadas pelas evidências de
10:32–11:48.

Consulta GitHub Actions em 26/10 confirmou que a última CI remota visível ainda
é o run `39608614450`, SHA `d51f4f38d2b4148ab49394c001567ad273bd57aa`, enquanto
o checkout local está em `64a33dcf5595fb6f429c65206be20dbdd4ae0bd0`. Os 13 jobs
encerraram em 3–4 segundos com `steps: null` e sem `logs_url`; nenhum comando
de teste aparece como executado. Isso é falha de execução/alocação do runner,
sem causa interna confirmada, e não resultado de teste do código atual. WSL e
Docker não estão disponíveis neste host. CI Linux do checkout atual permanece
pendente.

### Restore/backup com identidade de aplicação restrita — 2026-10-26

`BackupRestoreMariaDbTests` provisiona os schemas e uma conta aleatória com admin;
restore, contextos do plugin IntroSkipper e participante independente usam a
conta limitada aos schemas de fixture. O teste confirma que `CREATE DATABASE`
externo é negado. Bootstrap e teardown usam admin. Os testes de replay após
terminação do processo e restore de linhas passaram 2/2, repetidos duas vezes,
em Windows/.NET 10 Release contra MariaDB 11.4.13 autenticado e descartável em
`127.0.0.1:33146`. O job CI já seleciona ambos; sua conta root existe somente no
serviço MariaDB efêmero para provisionamento. Processo encerrado, porta fechada;
execução hospedada Linux deste checkout ainda pendente. Nenhum banco operacional
foi acessado.

### Fixture MariaDB do plugin IntroSkipper com identidade restrita — 2026-10-26

`MariaDbFixture` recria o schema fixo do plugin com a conta administrativa e cria uma identidade de aplicação aleatória. A identidade recebe grants somente em `mulletaflix_introskipper`, aceita conexão apenas de `127.0.0.1`, executa inicialização e operações EF, e não pode criar banco externo (MariaDB 1044). Metadados de tabelas e teardown de usuário usam bootstrap administrativo. `IntroSkipper.Integration.Tests` passou 61/61 em Windows/.NET 10 Release contra MariaDB 11.4.13 descartável em `127.0.0.1:33144`; o listener foi encerrado.

Na mesma instância, `BillingDatabaseTests|UserManagerNormalizedUsernameTests` passou 31/32; um caso ignorado exige `@@lower_case_table_names == 0`. Os grants dessas fixtures também estão limitados a `127.0.0.1` e ao schema do teste. Uma primeira execução do plugin revelou divergência entre host do usuário e host do GRANT; o grant foi alinhado e a suíte completa repetida com sucesso. Sem banco operacional, release ou publicação remota.

Auditoria estática confirmou que `SqliteToMariaDbMigrationTests` passa credenciais administrativas para DDL e gravação da migração; privilégio mínimo é viável por schema, mas a fixture ainda não foi migrada. `BackupRestoreMariaDbTests` e bootstrap CI seguem pendentes.
### Fixture de usuários/cobrança com identidade de schema restrita — 2026-10-26

`UsersMariaDbTestDatabase` cria um schema e um usuário aleatórios usando a identidade administrativa descartável; `UsersDbContext` recebe somente grants nesse schema. O teste `ApplicationIdentity_CannotCreateAnotherDatabase` confirma a negação de `CREATE DATABASE` global. Em MariaDB 11.4.13 descartável, Windows/.NET 10 Release, `BillingDatabaseTests|UserManagerNormalizedUsernameTests` passou 31/32; o único ignorado requer `@@lower_case_table_names == 0`. A instância foi encerrada, porta 33142 fechada. A fixture não cobre o bootstrap CI nem outras fixtures.

### Ferramenta SQLite→MariaDB com credencial limitada — 2026-10-26

`SqliteToMariaDbMigrationTests` cria uma identidade aleatória de host `127.0.0.1`. A conta administrativa cria o schema-alvo e concede grants somente nele; ferramenta de migração e contextos de verificação usam a identidade restrita. Os casos dry-run e arquivo de origem ausente continuam sem criar schema. O teardown administrativo remove schema e usuário. `IntroSkipper.Integration.Tests` completo passou 61/61 em MariaDB 11.4.13 descartável, Windows/.NET 10 Release, `127.0.0.1:33145`; processo encerrado e porta fechada. Não acessou banco operacional. Bootstrap CI e `BackupRestoreMariaDbTests` seguem pendentes.
### Host HTTP de integração com identidade restrita — 2026-10-26

`MulletaFlixApplicationFactory` usa a conta administrativa apenas para provisionar
schema, usuário e grants. O host recebe um usuário aleatório com privilégios nos
dois bancos necessários: o schema principal descartável e o schema fixo
`mulletaflix_introskipper`, exigido pelo plugin. O teste de fronteira nega
`CREATE DATABASE` fora desses schemas. O teardown remove schema principal e
usuário; o schema compartilhado do plugin permanece até o descarte da instância
MariaDB, pois outros hosts de teste podem usá-lo. Em MariaDB 11.4.13 descartável,
Windows/.NET 10 Release, `Jellyfin.Server.Integration.Tests` passou 145/145 em
`127.0.0.1:33140`; zero skips/falhas. Isso valida esse host HTTP, não outras
fixtures nem o bootstrap administrativo do CI. Nenhum banco operacional foi
acessado.

### Conta de aplicação limitada à persistência — 2026-10-26

`ServerPersistenceMariaDbTests` usa a conexão administrativa apenas para criar
um schema e um usuário aleatórios. A aplicação, migrations e bootstrap de
usuários conectam como esse usuário, com permissões limitadas a
`<schema-descartável>.*`; um teste confirma que ele não consegue criar outro
schema. O teardown remove schema e usuário. Em MariaDB 11.4.13 descartável,
Windows/.NET 10 Release, a suíte passou 19/19 em `127.0.0.1:33137`. Isso valida
somente esta fixture: outras suítes e o bootstrap do CI ainda podem usar a conta
administrativa e não foram convertidos nesta mudança. Nenhum banco operacional
foi acessado.

## Verificação autenticada — 2026-10-26

MariaDB 11.4.13 descartável em `127.0.0.1:33099`, inicializada com senha root aleatória e grants ativos, passou no ping TCP autenticado. Os filtros MariaDB do workflow passaram: `UserManagerNormalizedUsernameTests|BillingDatabaseTests` 30 aprovados, 1 ignorado porque exige `@@lower_case_table_names == 0`, 0 falhas; `IntroSkipper.Integration.Tests` selecionado passou 16/16. A conexão usa root, portanto não valida privilégios mínimos da aplicação nem os demais testes de integração. A instância foi encerrada e a porta fechada. Esta rodada Windows não substitui o job Linux hospedado.

Os testes de integração que criam ou removem schemas só usam MariaDB quando
`MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` está configurada. Sem a variável,
os casos que dependem do banco são ignorados; nenhum endpoint padrão é sondado.

O valor é validado antes de qualquer operação destrutiva. São aceitos apenas um
host loopback (`127.0.0.1`, `::1` ou `localhost`) e uma porta diferente de `3306`.
O nome do banco é removido da conexão administrativa; cada suíte cria e remove
apenas seus próprios schemas descartáveis. O fixture web consome a instância
fornecida e não inicia nem encerra serviços MariaDB. Nunca aponte a variável para
produção nem para um serviço compartilhado.

Exemplo PowerShell para uma instância MariaDB descartável local em `33099`:

```powershell
$env:MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING = "Server=127.0.0.1;Port=33099;User ID=test;Password=<senha-local>;CharSet=utf8mb4;"
dotnet test tests/IntroSkipper.Integration.Tests/IntroSkipper.Integration.Tests.csproj --configuration Release
Remove-Item Env:MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING
```

As suítes de Intro Skipper, persistência, migração SQLite→MariaDB, restore de
backup e integração HTTP do servidor usam essa configuração. Inicie antes uma
instância descartável MariaDB local e execute, por exemplo:

```powershell
$env:MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING = "Server=127.0.0.1;Port=33099;User ID=test;Password=<senha-local>;CharSet=utf8mb4;"
dotnet test tests/Jellyfin.Server.Integration.Tests/Jellyfin.Server.Integration.Tests.csproj --configuration Release
Remove-Item Env:MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING
```

As classes `UserManagerNormalizedUsernameTests` e `BillingDatabaseTests` do
projeto `tests/Jellyfin.Server.Implementations.Tests` também exigem a mesma
instância descartável. O helper cria schemas aleatórios com prefixo reservado e
remove cada schema no descarte da fixture. Em 2026-10-19, 30 testes dessas duas
classes passaram em Release contra MariaDB 11.4.4 no loopback/33099; isso não
homologa instalação nem produção.

Na regressão ampla da solution em 2026-10-19, esses 30 casos foram ignorados
porque o processo não recebeu a variável de conexão. A execução ampla totalizou
5.044 aprovados, 64 ignorados e 0 falhas; os resultados MariaDB focais acima
foram executados separadamente com a instância descartável. Não some nem trate
os casos ignorados como aprovação.

### Collation binária de usernames e casing Linux — 2026-10-19

O modelo EF e as migrations mapeiam a entidade para a tabela `Users` (U
maiúsculo). O bootstrap MariaDB deve usar exatamente esse nome em criação,
chaves estrangeiras e consulta a `information_schema`; em Linux com
`lower_case_table_names=0`, `users` e `Users` são tabelas diferentes. O teste
`EnsureSchemaCreated_UsesEfMappedUsersTableWithBinaryUsernameCollations`
confirma a tabela mapeada e as duas collations `utf8mb4_bin` quando executado
com MariaDB. O predicado em `information_schema` usa `BINARY` nos dois lados
para não misturar eventual tabela legada com nome/casing diferente.

Após alinhar o bootstrap à tabela `Users`, a regressão Release da solution
(`FullyQualifiedName!~Integration`) terminou com 5.044 aprovados, 65
`NotExecuted` e 0 falhas em 5.109 casos. A nova regressão MariaDB ficou entre os
não executados por ausência de `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`; os
30 testes focais anteriormente aprovados não cobriam o casing Linux. O resultado
precisa ser repetido com MariaDB Linux `lower_case_table_names=0` antes de
considerar esta compatibilidade validada. Não remova tabelas `users` legadas
automaticamente: dados e necessidade de migração precisam ser investigados
antes de qualquer operação destrutiva.

### Gate automatizado de casing em Linux — 2026-10-19

O workflow `.github/workflows/ci.yml` inclui `user-mariadb-linux-tests`, em
`ubuntu-latest`, usando MariaDB 11.4 como serviço descartável e porta 33099.
O filtro executa as classes `UserManagerNormalizedUsernameTests` e
`BillingDatabaseTests`; o teste de bootstrap verifica também
`@@lower_case_table_names = 0` antes de confirmar a tabela `Users` e as
collations. Assim, execução em MariaDB case-insensitive é marcada como skip e
não confundida com validação Linux.

O contrato do workflow está em
`tools/release/tests/ci-mariadb-linux-coverage.test.mjs`. O contrato local e o
contrato Mongo passaram 2/2, e o YAML foi parseado com sucesso em 19/10/2026.
O teste real C# continua pendente até a execução do job Ubuntu; nesta máquina,
sem a variável de conexão, os 31 casos ficam `NotExecuted`. Não publicar Release
nem declarar homologação Linux concluída até o resultado remoto verde.

### Cobertura MariaDB dos testes HTTP — 2026-10-20

O assembly `Jellyfin.Server.Integration.Tests` lista 145 casos. Em MariaDB
11.4.4 temporário no Windows, a agregação dos TRX por nome de teste confirmou
145/145 aprovados, zero ignorados, zero falhas e nenhum teste listado sem
resultado aprovado. Os resultados vieram de filtros focais e uma tentativa
integral do assembly; a tentativa integral foi cancelada após 78 aprovações em
17m03s, e os filtros posteriores cobriram os demais nomes. Filtros finais:
`VideosControllerTests` 13/13, `DashboardControllerTests` 12/12,
`MediaStructureControllerTests|PlaystateControllerTests` 13/13 e
`EncodedQueryStringTest|RobotsRedirectionMiddlewareTests|WebSocketTests|ActivityLogControllerTests`
11/11. Testes focais anteriores cobriram upload de imagem, usuários e
biblioteca. Todos os bancos usaram datadir exclusivo, `--no-defaults`, bind
`127.0.0.1`, porta 33099 e foram encerrados; o listener de produção observado
no PID 20736/3306 permaneceu intacto.

Dos 198 casos MariaDB opt-in identificados na regressão ampla, 197 agora
passaram em Windows: os 142 casos `Server.Integration`, 25 casos
`IntroSkipper.Integration` e 30 casos de `UserManagerNormalizedUsernameTests` /
`BillingDatabaseTests`. Um caso permanece ignorado porque exige
`@@lower_case_table_names=0` em filesystem Linux case-sensitive. A compatibilidade
Linux ainda depende do job Ubuntu do CI; estes testes locais não homologam CI,
runtime instalado, produção ou reprodução de mídia real. Não conte a tentativa
integral cancelada como suite green; use a agregação explícita dos TRX focais.

Publicação da evidência: `Mulletaflix-Portal:main` commit
`98041b5b8f7be028e02b8f4ee25dc7a90bf99c83`; a suíte do contrato do portal
passou 168/168. O deployment Vercel Production `dpl_9pRUoKrUEXW9c3Xph5Rsm3MVJRz9`
está READY para esse SHA e o JSON público respondeu HTTP 200 com o evento
145/145. Isso documenta resultados locais, sem afirmar homologação de runtime,
produção, CI Linux ou playback real.

Em 2026-10-19, os testes positivos `GetItem_UserIdAndItemId_Valid`,
`GetIntros_UserIdAndItemId_Valid` e `LocalTrailersAndSpecialFeatures_UserIdAndItemId_Valid`
voltaram a executar na suíte HTTP. A classe passou 16/16 e a suíte completa passou
144/144 contra a instância MariaDB descartável; os resultados não homologam o
runtime instalado ou produção.

A senha deve ser fornecida somente no ambiente local do processo; não a grave
em arquivos versionados ou no vault.

### Regressão ampla de implementações com bancos descartáveis — 2026-10-26

`Jellyfin.Server.Implementations.Tests` foi executado em Windows/.NET 10 Release
com `FullyQualifiedName!~Integration`, MariaDB 11.4.13 e MongoDB 8.3.7 em
datadirs novos, loopback e portas exclusivas 33099/27099. Resultado final:
1.742 aprovados, 24 ignorados, 0 falhas/1.766. Os casos opt-in de ambos os bancos
foram exercitados. MariaDB usou `--skip-grant-tables` para esta instância
descartável, então essa rodada não valida autenticação nem grants. A instância
Mongo de 27017 e o MariaDB de 3306 não foram consultados. Os serviços de teste
foram encerrados e as portas fecharam.

Uma tentativa anterior da mesma regressão terminou com falhas de conexão Mongo
porque a instância de teste ainda não havia iniciado. Não conte essa tentativa;
a execução final repetida com os dois bancos disponíveis passou. Os skips
remanescentes e Linux/CI hospedada/runtime/produção continuam sem validação.
Homologação formal permanece 2/7 (28,6%), sem crédito ou release.

### Regressão inclusiva no Windows — 2026-10-20

`dotnet test MulletaFlix.sln --configuration Release --no-restore --filter
'FullyQualifiedName!~DramaFindsLiveIntegrationTests' --logger trx --results-directory
<scratch> --verbosity quiet` produziu 17 TRX completos. A agregação pelos
resultados individuais (`UnitTestResult`) registrou 5.099 aprovados, 235
`NotExecuted` e 0 falhas em 5.334 casos selecionados. Diferente do gate filtrado
anterior, os assemblies de integração foram executados: `Server.Integration`
passou 3 e ignorou 142; `IntroSkipper.Integration` passou 14 e ignorou 25. Os
resultados TRX não expõem o código final do processo; ele não foi recuperado
após a sessão de execução desaparecer, então não se afirma exit code 0. Os
skips com MariaDB foram explicados pela ausência de
`MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`; nenhum banco real foi sondado.
`Server.Implementations.Tests` teve 1.508 aprovados/64 ignorados e também incluiu
testes opt-in MariaDB/Mongo. Não foram executados 198 casos MariaDB opt-in e 24
casos Mongo opt-in. A classe `DramaFindsLiveIntegrationTests` foi excluída para
evitar chamada à API externa ao vivo; casos Unix/Linux também ficaram ignorados
no Windows. Esses resultados não comprovam MariaDB/Mongo reais, compatibilidade
Linux nem homologação de instalação/produção. Resultados publicados no portal
em `Mulletaflix-Portal:main`, commit `d1ed0f97f773c570902855182ff9ee464656923b`;
endpoint público confirmou a homologação em 2026-10-20 02:03 (-03).

### Reexecução focal MariaDB de usuários e cobrança — 2026-10-20 07:23 (-03)

`UserManagerNormalizedUsernameTests|BillingDatabaseTests` foi reexecutado em
Release contra MariaDB 11.4.4 em instância temporária exclusiva, com datadir
novo, bind `127.0.0.1` e porta 33099: 30 aprovados, 1 `NotExecuted`, 0 falhas
(31 total; exit 0). O único caso não executado foi
`EnsureSchemaCreated_UsesEfMappedUsersTableWithBinaryUsernameCollations`, que
exige MariaDB Linux com `@@lower_case_table_names=0`. O listener descartável foi
encerrado e a porta 33099 ficou fechada; o listener de produção permaneceu no
PID 20736/3306. A conexão do teste apontou exclusivamente para o loopback/33099.

Esta rodada focal é evidência suplementar: não altera nem se soma ao snapshot
amplo de TRX. Os casos continuam `NotExecuted` naquele snapshot por ausência da
variável opt-in no processo da rodada ampla. Compatibilidade Linux/CI, runtime
instalado, produção e reprodução real não foram homologados; não criar release
com base apenas nesta rodada. Evidência publicada no portal via `main`, commit
`674c799fc0cb7a599e2dac6ac064217d614d1f18`; contrato do portal 174/174. Vercel
Production `dpl_A2iQsE9yjnV8tr4LYqEiqxBcLnUv` ficou `READY` para o SHA publicado,
e o JSON público respondeu HTTP 200 com o evento.

### Classificação dos skips restantes da regressão Release — 2026-10-20 07:29 (-03)

A recontagem dos 17 TRX por `System.IO.File.ReadAllText` (evitando interpretação
de `[` e `]` nos nomes dos arquivos) confirmou 5.071 `Passed`, 67 `NotExecuted`
e 0 `Failed` em 5.138 casos. A partição exata dos 67 é: 31 MariaDB opt-in, 24
MongoDB opt-in, 10 testes específicos de plataforma/filesystem e 2 benchmarks
explicitamente ignorados. Execuções focais separadas já passaram 30/31 MariaDB
(um caso exige Linux) e 24/24 MongoDB; os casos continuam `NotExecuted` no
snapshot amplo, sem soma ou reclassificação.

Os 10 skips de plataforma são: três `AvatarFileStoreTests` (identidade de
entrada Linux, FIFO via O_PATH/procfs e permissões POSIX), um teste de modos
Unix em `ServerConfigurationManagerFilePermissionsTests` e seis casos
`ManagedFileSystemTests` (symlink quebrado, quatro cenários de caminhos Unix e
movimentação cross-filesystem que exige `/dev/shm`). Os dois métodos de
`QueryPerformanceBenchmark` têm `[Fact(Skip = "Benchmark")]` na fonte e não são
testes de regressão executáveis na suíte padrão.

`wsl.exe --status` confirmou que WSL não está instalado; Docker e Podman não
estão disponíveis. A API do GitHub revalidou os runs CI `39037932557` e Release
`39212993752`: falhas sem steps executados; o registro anterior atribui o
bloqueio à alocação de runners/billing. Assim, Linux, a execução desses casos
Unix e o gate remoto seguem pendentes. Nenhum dado de produção foi usado e
nenhuma release foi criada. Homologação publicada em `Mulletaflix-Portal:main`
commit `3b45fb5ecf849d71fe0807d8688559e89cd9ff17`; contrato 175/175. Vercel
Production `dpl_DVwkcQ12uqQCA32nxUcqkPgEsroM` está `READY`; endpoint público
HTTP 200 confirmou o evento e preservou o snapshot amplo 5.099/5.334.

### Lock de exclusão em sessão MariaDB distinta — 2026-10-25

O job `user-mariadb-linux-tests` em `.github/workflows/ci.yml` executa o teste
`DeleteItem_WaitsForDatabaseAdvisoryLockHeldByAnotherSession` junto às
regressões MariaDB de persistência e restore. O teste usa o serviço MariaDB
descartável loopback/33099 do job. Ele verifica que `DeleteItem` espera pela
stripe mantida por outra sessão e conclui a exclusão após a liberação do lock.
A mudança do workflow e o contrato local não provam execução hospedada; o run
Ubuntu precisa terminar verde antes de declarar cobertura CI. Consulta à API
GitHub em 25/10/2026 confirmou que os runs mais recentes ainda são CI
39608614450 e Release 39608677913, ambos falhos em 21/10/2026.

Em 25/10/2026, esse teste também passou 1/1 em MariaDB 11.4.4 descartável no
Windows, loopback `127.0.0.1:33128`, usando .NET 10 Release. O processo de teste
foi encerrado e a porta descartável fechou; a instância operacional em 3306 não
foi consultada. Essa evidência não substitui o job Ubuntu hospedado, ainda sem
execução confirmada para esta alteração.
# Revalidação do save concorrente de imagens — 2026-10-26 05:59 BRT

Em Windows/.NET 10 Release, três testes MariaDB descartáveis passaram: refreshes concorrentes do mesmo item preservam um conjunto completo de imagens, metadados e imagens podem ser persistidos concorrentemente, e `SaveImagesAsync` espera pelo advisory lock mantido por outra sessão. Execução em MariaDB 11.4.13 loopback `127.0.0.1:33099`; o serviço foi iniciado com `--skip-grant-tables`, portanto autenticação não foi validada. As instâncias MariaDB e MongoDB de teste foram encerradas; os endpoints operacionais não foram consultados.

### Plano seletivo de consulta de imagens — 2026-10-26

`ServerPersistenceMariaDbTests` passou 22/22 em Windows/.NET 10 Release com MariaDB 11.4.4 descartável em `127.0.0.1:33159`, configurada com `max_connections=256`. O teste `SaveImagesAsync_UsesCompositeItemImageIndexForSelectiveLookup` adiciona 2.048 linhas não relacionadas e confirma via `EXPLAIN` o índice `IX_BaseItemImageInfos_ItemId_ImageType`, com estimativa de no máximo quatro linhas. A execução inicial completa excedeu o limite padrão de conexões; a repetição com capacidade ajustada passou. Serviço operacional na porta 3306 não foi usado; o servidor descartável foi encerrado. Isso valida o plano do cenário, não benchmark ou carga de produção.

### Persistência funcional sem índice de imagens — 2026-10-26

`ServerPersistenceMariaDbTests` passou 23/23 em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável em `127.0.0.1:33159` (`max_connections=256`). O novo caso remove a FK e o índice composto somente no schema aleatório do fixture, mantém 2.048 imagens de itens inexistentes e confirma `EXPLAIN type=ALL` sem chave. `SaveImagesAsync` ainda persistiu o conjunto esperado de duas imagens. A estimativa de linhas não atingiu o limite arbitrário de 2.048; o teste usa `type=ALL` e chave nula como critério determinístico. Porta 3306 não acessada; processo de teste encerrado. Isso prova comportamento funcional e plano de varredura, não latência ou carga de produção.

## Revalidação concorrente da persistência de imagens — 2026-10-26 15:57 BRT

O log anexado tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao incidente já registrado. Ele não fornece PID/assembly nem desfecho dos retries. `ItemPersistenceServiceTests` passou 35/35 em Windows/.NET 10 Release. `ServerPersistenceMariaDbTests` passou 24 aprovados, 1 ignorado e 0 falhas/25 contra MariaDB 11.4.4 descartável, porta 33219. Foram executados os testes concorrentes entre `SaveImagesAsync`/`SaveItems`, múltiplos stripes e `DeleteItem`. O único skip requer marcadores do harness de restart, aprovado separadamente 1/1. A instância foi criada com datadir e processo exclusivos, validada por PID/executável/datadir e encerrada; porta 33219 fechada. MariaDB operacional 3306 não acessado.

Build produziu avisos existentes `xUnit1051` e `SA1402`, sem falhas. Nenhum código de runtime mudou nesta reexecução. O resultado valida o checkout local, não o runtime que gerou o log nem produção. CI hospedada, runtime instalado e produção seguem pendentes; homologação formal 2/7 (28,6%), sem crédito ou release. Mapa técnico conferido: nenhum módulo, API, classe, dependência ou fluxo mudou. Ver [[Sessões/2026-10-26-MulletaFlix-t56-concurrent-image-persistence-rerun-1557]].

## Regressão ampla do servidor — 2026-10-26 16:10 BRT

dotnet test da solution em Windows/.NET 10 Release terminou exit 0 na repetição no-build/no-restore: 5.379 aprovados, 96 ignorados, 0 falhas/5.475 em 15 projetos. O filtro FullyQualifiedName!~Integration excluiu os testes dos dois projetos Integration; integrações MariaDB/Mongo não recebem crédito. A execução fresh anterior retornou exit 1 e a causa não foi isolada. Esta evidência é do checkout local; não prova runtime/produção nem CI hospedada. Formal 2/7, sem release. Ver [[Sessões/2026-10-26-MulletaFlix-server-regression-1610]].

O contrato do status de homologação passou 292/292 e o JSON parseou; a porta local 4173 não tinha listener, portanto o HTML servido/renderizado não foi visualmente verificado. Sem publicação remota.

## Regressão compilada — 2026-10-26 16:23 BRT

Repetição fresh dotnet test MulletaFlix.sln --configuration Release --no-restore --filter FullyQualifiedName!~Integration terminou exit 0. Server.Implementations.Tests passou 1.693/1.786 (93 ignorados); Api.Tests 789/792 (3 ignorados). Os projetos Integration tiveram zero casos selecionados pelo filtro. Uma execução fresh anterior havia retornado exit 1 sem diagnóstico preservado; este rerun encerrou com sucesso. Evidência Windows/local; MariaDB/Mongo opt-in, Linux/macOS, CI hospedada, runtime e produção continuam pendentes. Formal 2/7, sem release. Ver [[Sessões/2026-10-26-MulletaFlix-server-regression-compiled-1623]].

## Rechecagem do log repetido de deadlock — 2026-10-26 16:28 BRT

O anexo SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` é byte a byte igual aos registros anteriores. `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` recompilou em Windows/.NET 10 Release e passou 50/50. Nesta sessão a integração MariaDB não foi executada; o processo operacional MariaDB em 3306 foi apenas identificado e não acessado. O assembly do servidor não existe no caminho esperado `C:\Program Files\MulletaFlix\Server\Jellyfin.Server.Implementations.dll`; portanto não foi possível correlacionar o log com o runtime atualmente instalado. Fonte local segue com stripe por item e `GET_LOCK` antes da transação. Nenhuma alteração de runtime; formal 2/7, sem crédito ou release. Ver [[Sessões/2026-10-26-MulletaFlix-image-deadlock-log-recheck-1628]].

CI remota verificada nesta rechecagem: últimos runs listados em 21/10/2026 no SHA d51f4f38 falharam; jobs de build do servidor e validação da release não reportam steps. Não prova falha do checkout atual nem motivo do runner.

## Identidade do runtime nos retries de gravação de imagens — 2026-10-26 18:32 BRT

O aviso de retry de SaveImagesAsync agora inclui os campos estruturados ProcessId e AssemblyVersion, além de ItemId e tentativa. A mudança permite identificar em ocorrências futuras qual processo/versão está emitindo deadlocks e falhas transitórias; não muda o retry nem comprova qual binário produziu o log repetido anterior. ItemPersistenceServiceTests passou 35/35 em Windows/.NET 10 Release, incluindo asserção dos campos de identidade; dotnet format style --verify-no-changes passou nos dois arquivos. MariaDB operacional não foi acessado. Sem Linux/CI hospedada/runtime instalado/produção, sem release. Homologação formal 2/7 (28,6%), sem crédito.

## Rechecagem do anexo de deadlock — 2026-10-26 19:53 BRT

O anexo recebido nesta sessão tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico ao log já registrado. Ele mostra concorrência em `DELETE BaseItemImageInfos` para um `ItemId` e deadlock MariaDB 1213, com retries; o status HTTP 200 citado no cabeçalho não comprova commit. O texto não contém os campos estruturados de PID e versão que a fonte atual registra nos avisos de retry. A DLL instalada consultada reporta 12.1.35, enquanto o checkout declara 12.1.32; sem PID/hash do processo, não é possível atribuir o log à instalação nem afirmar que a correção está ativa em produção. Banco operacional não acessado.

Regressão ACL incidental desta sessão: `BackupRestoreDirectoryTests` Release passou 39, ignorou 10 e falhou 0/49 em Windows/.NET 10. A tentativa de rejeitar `CreateDirectories` em todo namespace parent foi revertida porque também rejeitava ancestrais padrão durante `OpenRoot`; a política existente ainda barra criação de arquivos/substituição no pai, e a transação valida a ACL da pasta final antes de gravar. A possibilidade de negação de serviço por corrida de criação de diretório permanece sem reprodução determinística; não foi demonstrada gravação fora do destino.

Nenhuma alteração de runtime, release, banco ou produção. Auditoria técnica global continua 5.465/5.493; homologação formal 2/7 (28,6%), sem crédito. Mapa técnico conferido; nenhuma estrutura mudou. Próximo passo: correlacionar novo log com PID/AssemblyVersion emitidos pela versão que contém a instrumentação e validar o binário implantado por hash antes de concluir a correção em produção.

## Reexecução focal do log de deadlock — 2026-10-27 05:23 BRT

O anexo repetido tem SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2. O teste SaveImagesAsync_WaitsForSameItemPersistenceLockBeforeOpeningDatabaseContext passou 1/1 em Windows/.NET 10 Release. Integração MariaDB não executada: configuração opt-in, Docker e WSL indisponíveis. MariaDB operacional não acessado. Esta evidência cobre o lock em processo, não o fluxo concorrente contra MariaDB nem o runtime/produção.

## Reenvio idêntico do log de deadlock — 2026-10-27 05:31 BRT

O anexo tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual aos registros anteriores. Repete deadlocks 1213 durante `DELETE BaseItemImageInfos` para o mesmo ItemId e avisos de retry, mas não fornece PID/AssemblyVersion nem o resultado final. O status HTTP 200 citado no início não confirma commit. A fonte local contém stripe por ItemId e lock advisory MariaDB; o processo do log não foi identificado. Sem alteração de runtime ou acesso ao banco operacional.
