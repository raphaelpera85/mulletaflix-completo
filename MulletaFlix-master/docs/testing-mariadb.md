# Testes de integração MariaDB

## Regressão local de inicialização — 2026-10-29

`MariaDbDataDirectoryInitializationTests` e `MariaDbStartupMutexLeaseTests` passaram 9/9 em Windows/.NET 10 Release; `Jellyfin.Server.Tests` completo passou 95/95. Esses são testes unitários de filesystem e mutex do gerenciador, sem instância MariaDB; não validam inicialização de daemon, migrations ou conexão real. Para integração com banco, use exclusivamente uma instância MariaDB descartável e `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` conforme as instruções abaixo. MariaDB é o único banco relacional operacional; nenhum servidor MySQL separado é usado.

`MariaDbFixtureCleanupTests` passou 5/5 e `JellyfinApplicationFactoryCleanupTests` passou 4/4. Simulam falhas de descarte/remoção e confirmam que a limpeza tenta todas as ações e preserva a exceção inicial. Não iniciam MariaDB; a variável de integração `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` estava ausente e nenhuma suíte de banco foi executada neste lote.

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

## Runtime LTS alinhado — 2026-10-28

Os empacotadores Windows/portátil e o serviço MariaDB do job de integração CI estão pinados em MariaDB 11.4.13. Validação isolada Windows/.NET 10: o ZIP oficial foi comparado ao SHA-256 publicado (`d62986d433eeebfde218560b276103831604a61e929e87f1a17f5aebd80257e2`), `SELECT VERSION()` retornou `11.4.13-MariaDB` e `IntroSkipper.Integration.Tests` passou 47/47 em `127.0.0.1:33159`. A instância temporária foi encerrada e a porta ficou fechada. A execução não valida CI/Linux, instalação, produção ou os pacotes completos. A configuração EF `MariaDbServerVersion(11.4.2)` continua a descrever compatibilidade de recursos da série MariaDB, não o patch empacotado.

### Regressão de persistência idempotente de imagens — 2026-10-29

Execute o teste integrado `ServerPersistenceMariaDbTests.SaveItems_RescanningUnchangedImages_PreservesRowsAndOnlyReplacesChangedMetadata` no projeto `tests/IntroSkipper.Integration.Tests` com `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` apontando para MariaDB descartável em loopback e porta não padrão. O cenário exige que uma revarredura idêntica preserve os IDs das linhas e que mudança de idioma substitua o metadado. Sem a variável, o caso deve ser ignorado; esse resultado não comprova o comportamento no banco.

Em 2026-10-29, o teste passou 1/1 em Windows/.NET 10 contra MariaDB 11.4.4 iniciado com datadir exclusivo sob Temp e porta loopback dinâmica. A instalação local do servidor não foi usada; a instância e o datadir temporários foram encerrados e removidos.

### Regressão de tipos de trailer durante varredura — 2026-10-29

`ServerPersistenceMariaDbTests.SaveItems_RescanningTrailerWithSameTypes_PreservesTheStoredTypeSet` valida a persistência real dos tipos de trailer durante revarredura. Em 2026-10-29, passou 1/1 contra MariaDB 11.4.4 descartável, iniciado em loopback e porta dinâmica não padrão. Triggers de auditoria confirmaram zero `INSERT`/`DELETE` quando os tipos equivalentes chegam em ordem invertida; após alterar a coleção, os triggers registraram gravações e o conjunto novo persistiu corretamente. O processo foi encerrado e a porta ficou fechada. A unidade `TrailerTypeCollectionsMatch` passou 1/1, `ItemPersistenceServiceTests` passou 29/29 em Release e `dotnet format whitespace --verify-no-changes` passou para o projeto de integração. Datadirs temporários de teste permanecem em Temp após a remoção recursiva ter sido bloqueada pela política local. MariaDB é a única base relacional operacional.

## Execução por fatias e regressão ampla — 2026-10-29

Durante mudanças de persistência, execute primeiro o projeto e filtro focais. Exemplo já validado:

```powershell
dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --no-restore -c Release --filter FullyQualifiedName~ItemPersistenceServiceTests
```

Para Nebula Python, use o venv do repositório e rode a partir do diretório do módulo:

```powershell
Set-Location Tools/NebulaPython
.venv/Scripts/python.exe -m pytest tests -q
```

Não use os skips de dependências externas como aprovação. Como gate amplo, a solução Release local concluiu sem falhas; os 29 testes integrados opt-in MariaDB foram ignorados na execução sem endpoint descartável. Nebula Python passou 672/672. A solução demorou vários minutos, com Providers levando 3m27s; as execuções focais de ItemPersistenceService foram inferiores a 10s. A decomposição de testes externos lentos deve ocorrer sem enfraquecer assertions nem transformar validação externa em mock no gate de homologação.

### Limite da execução opt-in MariaDB — 2026-10-29

A tentativa de rodar toda a suíte integrada com `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` foi bloqueada pela política local antes do início de `dotnet test`. Não houve aprovação nova; a instância descartável foi encerrada e sua porta dinâmica ficou fechada.

### Providers rápido sem integrações externas — 2026-10-29

Comando validado para ciclo local rápido:

```powershell
dotnet test tests/Jellyfin.Providers.Tests/Jellyfin.Providers.Tests.csproj --configuration Release --no-build --no-restore --filter 'FullyQualifiedName!~Integration'
```

Resultado: 823/823 aprovados em 6 segundos no Windows/.NET 10. O filtro parcial `FullyQualifiedName!~DramaFindsLiveIntegration` deixa outras classes `*LiveIntegrationTests` habilitadas; a execução lenta com esse filtro foi interrompida e não conta como resultado. A execução rápida exclui integrações externas e não valida esses serviços. A CI já usa a exclusão por `Integration`.

### Regressão segmentada após mudanças do servidor — 2026-10-29

Para evitar builds concorrentes no mesmo `obj/Release`, executar um projeto .NET por vez. Uma tentativa com quatro processos `dotnet test` simultâneos falhou com `CS2012` ao escrever `MediaBrowser.Controller.dll`; os testes seguintes foram reexecutados serialmente.

| Fatia | Resultado |
|---|---:|
| `Jellyfin.Server.Implementations.Tests` (`FullyQualifiedName!~Integration`) | 1.659 aprovados, 82 ignorados, 0 falhas |
| `Jellyfin.Api.Tests` | 774 aprovados, 3 ignorados, 0 falhas |
| `Jellyfin.Server.Tests` | 91 aprovados, 0 ignorados |
| `Jellyfin.Providers.Tests` (`FullyQualifiedName!~Integration`) | 823 aprovados, 0 ignorados |
| `Jellyfin.MediaEncoding.Tests` | 71 aprovados, 0 ignorados |
| `IntroSkipper.Integration.Tests` (sem endpoint opt-in) | 21 aprovados, 29 ignorados |
| Nebula Python 3.10 | 674 aprovados |
| Portal de homologação | 331 aprovados |
| contratos de release/packaging | 26 aprovados |

A execução da solução completa foi interrompida após ausência prolongada de saída e não produz um total agregado novo. O teste MediaEncoding passou isoladamente 71/71. Ignorados opt-in MariaDB/Mongo e específicos de Linux/macOS não contam como aprovados; esta rodada não acessou bancos instalados, produção ou serviços externos. MariaDB continua sendo a única base relacional operacional.

### Grupos duplicados de pessoas — 2026-10-29

Teste focal para executar contra uma instância MariaDB descartável configurada em `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`:

```powershell
dotnet test tests/IntroSkipper.Integration.Tests/IntroSkipper.Integration.Tests.csproj --no-restore --configuration Release --filter FullyQualifiedName~PeopleValidationTask_QueriesDuplicateGroupsOnceAcrossBatchBoundary
```

O teste sem endpoint MariaDB compilou, encontrou o caso e terminou como **ignorado**; não conta como aprovação. Com MariaDB, ele semeia 101 grupos duplicados no schema descartável compartilhado, executa a tarefa e exige uma única leitura do catálogo, uma linha por grupo após limpeza e remoção dos próprios dados de teste. A medição usa `DatabaseTelemetryMetrics`; o banco operacional nunca deve ser usado.

### Suíte HTTP de integração do servidor — 2026-10-29

`Jellyfin.Server.Integration.Tests` deve ser executado em job próprio com MariaDB
descartável. O fluxo rápido `FullyQualifiedName!~Integration` exclui esse
assembly; sem job dedicado, parte da inicialização HTTP, backup/recuperação e
upload de imagem não era exercitada pelo CI principal.

Ao habilitar a suíte com MariaDB local em 2026-10-29, a primeira execução
identificou um defeito real de isolamento: o fixture criava schema temporário do
IntroSkipper, mas o plugin usava o schema operacional fixo. O plugin agora só
aceita `MULLETAFLIX_TEST_INTROSKIPPER_DATABASE` quando o host está no ambiente
`Testing`, validando o prefixo `mfx_intro_test_` e os caracteres do identificador.
O host de teste define `Testing`; em outros ambientes, o servidor continua
usando `mulletaflix_introskipper`.

Validação local Windows/.NET 10 contra MariaDB 11.4.13 descartável:
`StartupBackupRecoveryTests` e `ImageControllerProfileUploadTests` passaram
2/2; a suíte completa passou 146/146 em 1m54s. A instância usou datadir novo
em `%TEMP%`, loopback `127.0.0.1:60307`, foi encerrada e a porta ficou fechada.
O datadir temporário foi preservado. `dotnet build` do projeto passou e o
contrato Node `ci-mariadb-linux-coverage.test.mjs` passou 2/2. A verificação
global de formatação do projeto aponta problemas de charset/imports em vários
arquivos existentes; não foi tomada como aprovada. A factory alterada foi
formatada focalmente.

O workflow principal agora tem o job `server-mariadb-integration-tests`, usando
MariaDB 11.4.13 e a suíte inteira `Jellyfin.Server.Integration.Tests`. O
contrato Node protege essa ligação. Execução Linux/CI hospedada continua
pendente e não foi inferida do teste local. MariaDB é o único banco relacional
operacional; `MySqlConnector`, `MySqlConnection` e o identificador histórico
do provider são nomes técnicos da camada de compatibilidade, não uso de
servidor MySQL.

### Limpeza verificável da fixture MariaDB — 2026-10-29

- A fixture de `Jellyfin.Server.Integration.Tests` agora tenta executar todos os `DROP DATABASE`/`DROP USER` mesmo se um falhar e reporta falha de teardown em vez de apenas registrá-la no console. Dispose do host e cleanup são tentados independentemente; erros do teste e da limpeza são preservados na ordem.
- `JellyfinApplicationFactoryCleanupTests` cobre a tentativa de todas as ações/SQL e a preservação de exceções; `StartupBackupRecoveryTests` cobre a falha de operação junto da falha de Dispose.
- Windows/.NET 10 Release, MariaDB 11.4.4 descartável em loopback `127.0.0.1:60416`: suíte de integração passou 151/151 em 1m55s. Após a execução, não restaram schemas `mulletaflix_test_*`/`mfx_intro_test_*` nem contas `mfx_test_*`; o processo parou e a porta fechou. Contrato Node MariaDB da CI: 2/2. A versão local 11.4.4 difere da CI configurada 11.4.13; execução hospedada/Linux ainda pendente.
- Sem alteração em produção, release ou gates formais; homologação formal permanece 2/7. MariaDB é a única base relacional operacional.

### Fixture IntroSkipper: isolamento por execução e espera de inicialização — 2026-10-29

Cada instância de `MariaDbFixture` recebe um schema `mfx_intro_test_<guid>` próprio e remove somente esse schema. Após a inicialização hospedada com limite de startup, o fixture aguarda as gates compartilhadas de segmento e cache concluírem antes de permitir o teardown; isso evita remover um schema ainda em uso quando o warm-up ultrapassa o limite da inicialização do servidor. As ações de Dispose, DROP e restauração do override de ambiente são independentes; testes verificam que todas são tentadas e que as falhas são retornadas na ordem.

Validação final: `IntroSkipper.Integration.Tests` 55 aprovados, 1 ignorado, 0 falhas/56 em Windows/.NET 10 Release com MariaDB 11.4.4 descartável em loopback `127.0.0.1:60419`, autenticação normal (35s). Após a suíte, zero schemas `mfx_intro_test_*`, zero contas `mfx_test_*`, processo encerrado e porta fechada. O formatter focal passou. A CI usa MariaDB 11.4.13; paridade exata de versão, execução Linux hospedada e produção permanecem pendentes. O datadir temporário foi preservado. Não houve release nem mudança de produção; homologação formal 2/7 e agregado 5.291/5.376 não foram alterados.

## Validação do inicializador do datadir — 2026-10-29

Jellyfin.Server.Tests/Helpers/MariaDbDataDirectoryInitializationTests.cs cobre quatro casos: diretório ausente/vazio exige inicialização, falta do inicializador lança FileNotFoundException clara, inicializador presente é aceito e diretório já contendo arquivos não requer inicialização. A suíte completa Jellyfin.Server.Tests passou 95/95 em Release/.NET 10. Não iniciou serviço MariaDB nem acessou dados operacionais.

### Execução completa da suíte de integração em CI — 2026-10-29

O job Ubuntu `full-system-recovery-exercise` agora executa o projeto inteiro `tests/IntroSkipper.Integration.Tests` com serviços MariaDB 11.4.13 e MongoDB 7.0 descartáveis; o filtro que selecionava apenas `RecoveryExercise_RestoresMariaDbMongoAndFilesFromIsolatedSources` foi removido. Um contrato de CI garante serviços, projeto, timeout e ausência de filtro. Foi adicionado `SaveImagesAsync_PersistsLocalizedImagesAndIsIdempotent`, cobrindo gravação de `pt-br`, preservação do ID no resave idêntico e substituição da imagem quando o idioma muda.

Validação local Windows/.NET 10 Release: `IntroSkipper.Integration.Tests` 26 aprovados, 31 ignorados, 0 falhas/57. Os casos MariaDB/Mongo foram ignorados por falta de serviços descartáveis nesta execução. Contratos do workflow Linux 3/3 e formatação focal aprovados. A execução Linux hospedada e o round-trip real deste novo caso continuam pendentes; não há crédito de homologação.

### Round-trip de tabelas de cobrança — 2026-10-29 05:25 BRT

`BackupRestoreMariaDbTests` foi ampliado para semear e validar `PricingPlans`, `DiscountCoupons`, `PaymentGatewayConfigs` e `PaymentTransactions` após restauração. As transações cobrem relação obrigatória com usuário/plano e os cenários `CouponId` preenchido e nulo; a configuração de gateway usa apenas valores sintéticos. A classe passou 4/5, 1 ignorado (caso opt-in MongoDB), 0 falhas em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável em `127.0.0.1:55871`; o processo encerrou normalmente. `dotnet format whitespace --verify-no-changes` no arquivo e `git diff --check` passaram. Nenhum banco operacional foi acessado.

A contagem central reconciliada agora é 22/38 tabelas com linha semeada e assertion pós-restore; 16 seguem sem round-trip de linha. As 7 tabelas auxiliares de domínio e as tabelas persistentes do participante IntroSkipper são acompanhamentos separados. A matriz ampla 5.291/5.376 não foi recalculada; homologação formal permanece 2/7, sem release. MariaDB é o único banco relacional operacional.

### Round-trip de item central e dados de usuário — 2026-10-29 05:28 BRT

O fixture agora também semeia `BaseItems` e `UserData` relacionados a um usuário e valida os campos do item e os dados de reprodução/favorito após o restore. `BackupRestoreMariaDbTests` passou 4/5, 1 ignorado por requerer MongoDB, 0 falhas em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável em `127.0.0.1:60104`; processo encerrou normalmente. `dotnet format whitespace --verify-no-changes` focal e `git diff --check` passaram. Nenhum banco operacional foi acessado.

A cobertura central reconciliada agora é 24/38 tabelas, com 14 pendentes; as seis novas desde o inventário anterior são as quatro tabelas de cobrança, `BaseItems` e `UserData`. A matriz ampla 5.291/5.376 não foi recalculada; homologação formal segue 2/7, sem release. MariaDB é o único banco relacional operacional.

### Round-trip de AttachmentStreamInfo — 2026-10-29 05:30 BRT

Acrescentada uma attachment de stream associada ao `BaseItemEntity` da fixture. Após restore, as assertions conferem chave composta (`ItemId`, `Index`), codec, codec tag, comentário, filename e MIME type; isso também exercita a relação por convenção EF, não explicitada no fluent config. `BackupRestoreMariaDbTests` passou 4/5, 1 ignorado (MongoDB), 0 falhas em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável em `127.0.0.1:60837`; processo encerrou normalmente. Format whitespace focal e `git diff --check` passaram. Nenhum banco operacional foi acessado.

Cobertura central agora 25/38, 13 pendentes. Esta rodada confirma o vínculo por convention no modelo configurado; não amplia homologação formal (2/7) nem recalcula matriz ampla (5.291/5.376). Sem release. MariaDB é o único banco relacional operacional.

### Round-trip de relações de metadados base do item — 2026-10-29 05:32 BRT

A fixture inclui `BaseItemProviders`, `BaseItemImageInfos`, `BaseItemMetadataFields` e `BaseItemTrailerTypes`, todas relacionadas ao `BaseItemEntity`. O restore é verificado por identificadores, provedor/valor, imagem/idioma/dimensões/blurhash, e tipos de metadado/trailer. `BackupRestoreMariaDbTests` passou 4/5, 1 ignorado por depender de MongoDB, 0 falhas em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável em `127.0.0.1:50413`; encerramento normal. Format whitespace focal e diff check passaram; nenhum banco operacional foi acessado.

Cobertura central: 29/38 tabelas com linhas e assertions pós-restore; 9 pendentes. Formal 2/7 e matriz ampla 5.291/5.376 permanecem inalterados; sem release. MariaDB é o único banco relacional operacional.

### Round-trip de capítulos, streams e keyframes — 2026-10-29 05:34 BRT

Adicionadas linhas dependentes de `BaseItems` para `Chapters`, `MediaStreamInfos` e `KeyframeData`. As assertions pós-restore cobrem chave composta/índice e imagem do capítulo; codec, idioma, canais, bitrate, resolução, perfil e cores do stream; e duração/lista de keyframes. `BackupRestoreMariaDbTests` passou 4/5, 1 ignorado por requerer MongoDB, 0 falhas em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável em `127.0.0.1:53605`; processo encerrou normalmente. Format whitespace focal e diff check passaram. Nenhum banco operacional foi acessado.

Cobertura central 32/38; 6 tabelas pendentes. Homologação formal 2/7, agregado 5.291/5.376 sem recálculo e sem release. MariaDB é o único banco relacional operacional.

### Cobertura de linha completa das 38 tabelas centrais — 2026-10-29 05:35 BRT

A fixture adiciona dados e asserções de restore para `AncestorIds`, `LinkedChildren`, `ItemValues`, `ItemValuesMap`, `Peoples` e `PeopleBaseItemMap`, com dois BaseItems e vínculos pai/filho, pessoa/obra e valor/obra. `BackupRestoreMariaDbTests` passou 4/5, 1 ignorado (caso que exige MongoDB), 0 falhas em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável em `127.0.0.1:57572`; instância encerrada normalmente. `dotnet format whitespace --verify-no-changes` focal e `git diff --check` passaram. Nenhum banco operacional foi acessado.

A auditoria da fixture agora encontrou dados e assertions para as 38/38 tabelas centrais declaradas no `JellyfinDbContext`; não há tabela central pendente neste inventário. Esse resultado não significa que todas as linhas de produção foram exercitadas, nem cobre outros participantes/bancos/arquivos. As tabelas auxiliares de domínio e IntroSkipper permanecem contagens independentes já registradas. Agregado 5.291/5.376 não recalculado; homologação formal 2/7 e sem release. MariaDB é o único banco relacional operacional.

### Regressão unitária do subsistema de backup/restore — 2026-10-29 05:37 BRT

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-build --no-restore --filter FullyQualifiedName~FullSystemBackup --verbosity quiet` passou 154, ignorou 16 por plataforma/ambiente, falhou 0 (170 casos) em Windows/.NET 10 Release. Os ignorados não contam como aprovados; a bateria não acessou banco operacional nem valida restore de todas as linhas de produção. Combinada ao round-trip em MariaDB descartável, esta é uma verificação unitária adjacente, não homologação total. Homologação formal segue 2/7, matriz 5.291/5.376 sem recálculo, sem release. MariaDB é o único banco relacional operacional.

### Suíte completa Server Implementations — 2026-10-29 05:39 BRT

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet` passou 1.659, ignorou 83 por requisitos/platforma/opt-in, falhou 0 (1.742 casos) em Windows/.NET 10 Release, 1m12s. Isto amplia a verificação do backup para o projeto inteiro, mas skips continuam pendentes e a suíte não valida todos os serviços, Linux, instalação ou produção. Nenhum banco operacional foi acessado nesta execução. Homologação formal 2/7, agregado 5.291/5.376 sem recálculo, sem release. MariaDB é o único banco relacional operacional.

### Matriz de testes da solução recompilada — 2026-10-29 05:44 BRT

`dotnet test MulletaFlix.sln --configuration Release --no-restore --filter FullyQualifiedName!~LiveIntegrationTests --verbosity quiet` recompilou os projetos e terminou com exit code 0; nenhum teste falhou. Vários projetos foram reportados individualmente pelo runner. Exemplos: Server.Tests 95/95; Server.Integration.Tests 8 aprovados/143 ignorados; IntroSkipper.Integration.Tests 26/57, 31 ignorados; Providers 824/824; API 793 aprovados/3 ignorados; Server Implementations 1.659 aprovados/83 ignorados. O runner não publicou soma global nesta execução e não será inferida; os skips opt-in/ambiente permanecem pendentes. O filtro excluiu `LiveIntegrationTests`; build reportou avisos de documentação/analisadores, sem erros. Sem acesso a bancos de produção ou runtime. Formal 2/7, matriz 5.291/5.376 não recalculada, sem release. MariaDB é o único banco relacional operacional.

### Lote de validação multi-componente — 2026-10-29 07:29 BRT

A regressão `dotnet test MulletaFlix.sln --configuration Release --no-restore --filter FullyQualifiedName!~LiveIntegrationTests --verbosity quiet` terminou com exit code 0 e sem falhas. O runner reportou resultados por projeto, sem total global: Server.Tests 95/95; Server.Implementations 1.659 aprovados/83 ignorados; API 793 aprovados/3 ignorados; Providers 824/824; Naming 681/681; outros projetos também emitiram resultados por assembly. O filtro excluiu `LiveIntegrationTests`; os skips ambientais continuam pendentes e não contam como aprovados.

A suíte Nebula passou 681/681 em 21,84 s. Web passou 425/425 testes em 78 arquivos; `npm run build:check`, `npm run build:production` e `npm run verify:build` passaram. Verificação de artefatos confirmou 1.927 arquivos e payload inicial 298,1 KB gzip, abaixo do limite de 420 KB. A build Vite emitiu avisos de dependências `use client` ignoradas e imports dinâmicos/estáticos; não foi observada violação do orçamento, então não houve alteração de divisão de chunks.

Portal e contratos release passaram 102/102 no checkout monorepo; contrato do portal independente passou 372/372. Testes PowerShell direcionados passaram 10/10; verificações de empacotamento executadas passaram e o teste DuckDNS parametrizado passou ao receber `-UpdaterPath` explícito. Dois testes que usavam `Should Throw` foram ajustados para afirmar as exceções concretas, passando 4/4 no Pester 3.4 instalado. Os builds dos três projetos C# com referências XML corrigidas passaram com zero avisos e zero erros.

Limitações: Android Gradle não iniciou porque Android SDK não está configurado; iOS não foi executado porque `swift` não está instalado no Windows. O teste de empacotamento APK que requer `local.properties`/SDK permanece pendente. Nenhuma conexão MariaDB descartável foi configurada, então integrações reais ao banco não foram executadas; banco operacional e produção não foram acessados. A saída .NET não fornece total global, que permanece sem recálculo. Nenhuma release foi criada; homologação formal segue 2/7. MariaDB é o único banco relacional operacional.

#### Testes dos harnesses Android e empacotamento — 2026-10-29 07:33 BRT

- `MulletaFlix-android/tools` passou 51/51 em Pester 3.4, sem emulador.
- Oito scripts isolados de teste de empacotamento Windows executados diretamente passaram, cobrindo rollback de atualização, validação de stage/artefato, ACL e recuperação de build interrompido. O contrato de saída do gerador Linux também passou em fixture local; isso não substitui build ou execução Linux.
- O teste de integração do empacotador Android e Gradle continuam bloqueados neste host pela ausência do SDK/`local.properties` válido. `swift test` e MariaDB de integração também não foram executados por ausência de macOS/Swift e connection string descartável.
- Evidência do portal atualizada em `main`, commit `2f687432287ac842d4670ae1ecc421d6f59ee64d`, SHA remoto confirmado; portal contract 372/372. Deployment externo não verificado.

### Integrações MariaDB executadas em instância descartável — 2026-10-29 10:06 BRT

Com MariaDB 11.4.4 em `127.0.0.1:51451`, usando datadir exclusivo em `%TEMP%` e a variável explícita `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`, foram executadas sequencialmente:

- `Jellyfin.Server.Integration.Tests`: 151 aprovados, 0 ignorados, 0 falhas; exit 0.
- `IntroSkipper.Integration.Tests`: 57 aprovados, 1 ignorado, 0 falhas; exit 0. O caso ignorado `RecoveryExercise_RestoresMariaDbMongoAndFilesFromIsolatedSources` requer MongoDB descartável.
- `Jellyfin.Server.Implementations.Tests`, filtrado a `UserManagerNormalizedUsernameTests|BillingDatabaseTests`: 30 aprovados, 1 ignorado, 0 falhas; exit 0. O caso ignorado `EnsureSchemaCreated_UsesEfMappedUsersTableWithBinaryUsernameCollations` requer Linux com `lower_case_table_names=0`.

Total do lote: 238 aprovados, 2 ignorados, 0 falhas. Os skips não são aprovados e não validam MongoDB nem Linux. O PID exclusivo da instância de teste foi encerrado e a porta 51451 ficou fechada; nenhum serviço/banco operacional foi acessado. O arquivo temporário de conexão teve seu conteúdo de credencial efêmera substituído por texto neutro. A política bloqueou a remoção recursiva do diretório de teste, portanto os dados e logs sem credencial permanecem em `%TEMP%`. Este lote não valida CI hospedada, Linux nativo, instalação, runtime instalado, produção ou E2E de mídia. Formal permanece 2/7; sem release.

O estado e os resultados foram publicados em `Mulletaflix-Portal/main`, commit `2d65b700b3034420ab80bfae3dcbe74b5d91f557`; o contrato do portal passou 380/380 e o SHA remoto foi conferido. Deployment Vercel não verificado.
### Ciclo de vida concorrente do MariaDB — 2026-10-29

`MariaDbStartupMutexLeaseTests` passou 13/13 e `Jellyfin.Server.Tests` passou 103/103 em Windows/.NET 10. Cobertura adicionada para serialização start/stop no mesmo processo, cancelamento aguardando a trava, cancelamento quando mutex está livre e recuperação da lease após timeout. O teste é determinístico e usa apenas primitivas locais; não iniciou nem conectou ao serviço MariaDB.

O exercício completo de recuperação MariaDB+MongoDB+arquivos continua pendente de execução com instâncias descartáveis. Os dois skips da rodada anterior (MongoDB descartável e Linux `lower_case_table_names=0`) permanecem pendentes, não aprovados.

O contrato do portal que expõe a homologação passou 381/381; publicação Git `main` commit `57e0ed7903e4ce09a3fee717177f531537344e20`, SHA remoto conferido. Deployment Vercel não verificado.

CI remota: run 40872202840 associado ao commit-base `e3425131826a8d0573267878b0766f910588b34c` mostra 16 jobs em failure, sem steps. Logs de jobs amostrados retornam 404. Não há prova de que o teste integrado tenha iniciado; causa da falha do runner não confirmada. Não contar como teste executado nem como falha do código.

Estado desta verificacao publicado no portal Git `main`, commit `71642809d9c482e27b66027734f5af82a541d81a`; contrato 382/382 e SHA remoto confirmado. Vercel nao foi verificado.

Anotações GitHub recuperadas para jobs Full recovery, Server build e Server integration confirmam bloqueio da conta por cobrança e que os jobs não iniciaram. Não usar a conclusão `failure` como resultado de teste; nenhum step executou.

### Regressão da solução .NET após serialização MariaDB — 2026-10-29 10:28 BRT

`dotnet test MulletaFlix.sln --configuration Release --no-build --no-restore --filter FullyQualifiedName!~LiveIntegrationTests --verbosity quiet` terminou com exit 0. Agregação dos resumos de 17 assemblies: 5.372 aprovados, 261 ignorados, 0 falhas/5.633. Destaques: `Jellyfin.Server.Tests` 103/103; `Jellyfin.Server.Implementations.Tests` 1.659 aprovados/83 ignorados; `Jellyfin.Server.Integration.Tests` 8 aprovados/143 ignorados; `IntroSkipper.Integration.Tests` 26 aprovados/32 ignorados. O filtro excluiu testes live; integrações MariaDB/Mongo continuam ignoradas sem serviços opt-in. Sem conexão a banco/serviço operacional. Esta regressão não comprova Linux, instalação ou produção e não concede gate formal. Homologação 2/7; sem release.

Portal Git `main`: commit `8227cc94d769ff4f832178497166fdc115302eff; teste contratual 383/383 e SHA remoto confirmado. CI hospedada segue bloqueada antes dos steps pela cobrança; deployment Vercel não verificado.
