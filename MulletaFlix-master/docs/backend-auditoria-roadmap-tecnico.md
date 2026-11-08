## Mapeamento completo das partes não homologadas, runtime isolado (`18096`/`33188`) e suítes Playwright/a11y — 2026-11-04 00:30 BRT

Mapeadas todas as partes ainda não homologadas do sistema (`server` 2/7 áreas formais = 28,6%, `android` 10/21 áreas = 47,6% + 47 itens abertos em `TODO-APP.md`, `web` em monitoramento `W1..W4`, e os 79 itens abertos do roadmap: 7 `in_progress` e 72 `pending`) e executadas as homologações locais pendentes de runtime isolado e Playwright/a11y sem tocar a produção (`127.0.0.1:8096`), o MariaDB operacional (`127.0.0.1:3306`) ou o MongoDB operacional (`127.0.0.1:27017`):

1. **Desbloqueio de instância isolada do servidor lado a lado com produção (`MariaDbProcessManager` e `SecureConnectionProcessManager`)**:
   - `Jellyfin.Server/Helpers/MariaDbProcessManager.cs`: adicionado `ResolveStartupMutexNames(bool requireEmbeddedProcess, int port)` para usar `"Global\\MulletaFlix.EmbeddedMariaDb.Port.{port}"` e `"Local\\MulletaFlix.EmbeddedMariaDb.Port.{port}"` quando `port != 3306`, preservando `"Global\\MulletaFlix.EmbeddedMariaDb"` para a porta padrão `3306` de produção. Resolve o bloqueio registrado anteriormente em `server-metrics-runtime-smoke-mariadb-lock-2026-11-03-2335`.
   - `Jellyfin.Server/Helpers/SecureConnectionProcessManager.cs`: adicionado `ShouldSkipForIsolatedTestEnvironment(string? e2eTestMode, string? disableExternalBootstrap)` para suprimir inicialização de Nginx/DuckDNS quando `MFLX_E2E_TEST_MODE=true` ou `MFLX_DISABLE_EXTERNAL_BOOTSTRAP=true`.
   - `tests/Jellyfin.Server.Tests/Observability/MetricsLabelRedactionTests.cs`: adicionados testes unitários cobrindo `ResolveStartupMutexNames` e `ShouldSkipForIsolatedTestEnvironment`; `MetricsLabelRedactionTests` passou **14/14** em Release.
2. **Smoke live do servidor isolado (`127.0.0.1:18096` + MariaDB embarcado `127.0.0.1:33188`) e prova live de redaction de `/metrics`**:
   - `MulletaFlix.dll` (`12.1.46`) iniciado em diretório temporário com cadeia de ancestrais confiável para `BackupRestoreDirectory.OpenRoot`, MariaDB embarcado na porta `33188` (`mulletaflix_smoke`) e HTTP na porta `18096` após `CoreInitialisation` e `PostInitialization`.
   - `GET http://127.0.0.1:18096/System/Info/Public` respondeu HTTP 200 (`Version=12.1.46`, `ServerName=Mulletaflix`).
   - `GET http://127.0.0.1:18096/health` respondeu HTTP 200 (`Healthy`).
   - `GET http://127.0.0.1:18096/metrics` respondeu HTTP 200 (**326 linhas / 28.317 bytes**) com **0 ocorrências** de `mysqlconnector`, `pool_name`, `User ID`, `Server=` ou `33188`.
   - Instância isolada e `mariadbd.exe` da porta `33188` encerrados e diretório temporário removido (`Port 18096 listening: False`, `Port 33188 listening: False`).
3. **Homologação Playwright E2E, acessibilidade WCAG 2.2 AA (`axe-core`) e bundle budget em `MulletaFlix-web-master`**:
   - Corrigido `MulletaFlix-web-master/vite.config.ts`: `htmlPlugin.resolveId` retorna `{ id: source, external: true }` durante `options?.scan` (evitando erro do esbuild dependency scanner em `*.template.html`), `optimizeDeps.entries` inclui fixtures Playwright e `resolve.dedupe` unifica `react`, `react-dom`, `@emotion/react` e `@emotion/styled`.
   - `npx playwright test -c playwright.a11y.config.ts` passou **3/3** (`28-backup-coverage.spec.ts` em `390px`/`1280px` e `29-accessibility.spec.ts` WCAG 2.2 AA com `axe-core` nos estados `success`, `loading`, `error` e `empty` em `390px`/`1280px`).
   - `npx playwright test -c playwright.scrollbuttons.config.ts` passou **2/2** (`24-scrollbuttons.spec.ts` navegação por teclado, `:focus-visible`, LTR/RTL e estado `disabled`).
   - `npx playwright test -c playwright.requests.config.ts` passou **8/8** (`25-myrequests.spec.ts` em `390px`/`1280px`, `26-request-autocomplete.spec.ts` teclado/debounce/stale response, `27-nebula-navigation.spec.ts` em `390px`/`1280px`, `28-backup-coverage.spec.ts` em `390px`/`1280px`).
   - `npm run verify:build` passou (**1.929 artefatos válidos**, payload inicial **294,3 KB gzip** dentro do limite de **420 KB gzip**); `tsc --noEmit` **0 erros**; Vitest **76 arquivos / 421 testes aprovados**.

## Homologação local integrada: MongoDB descartável, MariaDB descartável, LocalMetadata, NebulaPython e Web — 2026-11-03 23:45 BRT

Executada a homologação local integrada das frentes anteriormente pendentes no host Windows sem tocar o servidor de produção (`127.0.0.1:8096`), o MariaDB operacional (`127.0.0.1:3306`) ou o MongoDB operacional (`127.0.0.1:27017`):

1. **MongoDB 8.3 descartável (`127.0.0.1:27099`) + Python 3.10 locked venv (`Category=RequiresMongo`)**:
   - Corrigida asserção obsoleta de cursor UUID com `%22` em `tests/Jellyfin.Server.Implementations.Tests/Nebula/NebulaSupabaseRestoreMongoTests.cs` (linhas 177-179) para validar `id=gt.{appUserId:D}` conforme produzido por `NebulaSupabaseSyncService.FetchSupabaseRowsPagedAsync`.
   - `dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-build --filter "Category=RequiresMongo"` passou **38 aprovados, 0 ignorados, 0 falhas/38** (`NebulaQueueConcurrencyMongoTests` **31/31** com cross-initializer C#/Python, `NebulaSupabaseRestoreMongoTests` **2/2**, `NebulaSupabaseBackupBotTokensMongoTests` **2/2**, `NebulaPlaybackRecoveryMongoTests` **1/1**, `MongoTopologyCapabilityTests` **2/2**).
2. **MariaDB 11.4.4 descartável (`127.0.0.1:33189`)**:
   - Corrigido `tests/IntroSkipper.Integration.Tests/BackupRestoreMariaDbTests.cs` (`CreateDisposableConnection`, linhas 61-63, 402-404, 972-973) para usar `MariaDbTestEnvironment.SkipUnlessConfigured()` e encaminhar `disposableInstanceConfirmed: true` a `MariaDbTestEnvironment.Validate(connectionString, disposableInstanceConfirmed: true)`.
   - `IntroSkipper.Integration.Tests` passou **96 aprovados, 0 ignorados, 0 falhas/96** (incluindo `BackupRestoreMariaDbTests` **4/4**, `SaveImagesAsync_RetriesAfterMariaDbProcessRestartDuringDelete` **1/1** com kill e restart do `mysqld.exe` descartável bloqueado em trigger `BEFORE DELETE`, `ServerPersistenceMariaDbTests`, `IntroSkipperMariaDbTests`, `SqliteToMariaDbMigrationTests` e `MariaDbTestConnectionTests`).
   - Suítes MariaDB de `Users` em `Jellyfin.Server.Implementations.Tests` passaram **64 aprovados, 1 ignorado, 0 falhas/65** (o único skip requer `lower_case_table_names == 0` em filesystem Linux case-sensitive).
   - `MariaDbProcessManagerLifecycleTests` em `Jellyfin.Server.Tests` passou **3 aprovados, 0 ignorados, 0 falhas/3**.
   - `Jellyfin.Server.Integration.Tests` executada por classe isolada com `mysqldump.exe` no `PATH` passou **146 aprovados, 0 ignorados, 0 falhas/146 casos únicos** em todas as 26 classes HTTP/WebSocket/OpenAPI/Backup (incluindo `ImageControllerProfileUploadTests` **1/1**, `StartupBackupRecoveryTests` **1/1**, `LibraryControllerTests` **17/17**, `UserLibraryControllerTests` **16/16**, `VideosControllerTests` **13/13**, `AudioControllerTests` **12/12**, `DashboardControllerTests` **12/12**, `UserControllerTests` **11/11**).
   - Instâncias descartáveis `27099` e `33189` foram encerradas e seus diretórios temporários removidos ao final (`Port 27099 listening: False`, `Port 33189 listening: False`).
3. **Correções adicionais em `MediaBrowser.LocalMetadata` (TDD RED/GREEN)**:
   - `MediaBrowser.LocalMetadata/Images/EpisodeLocalImageProvider.cs` (linha 45): `d.Name.Equals("metadata", StringComparison.OrdinalIgnoreCase)` em vez de `StringComparison.Ordinal`, descobrindo miniaturas de episódios em subpastas `Metadata`/`METADATA`.
   - `MediaBrowser.LocalMetadata/Images/LocalImageProvider.cs` (linhas 127 e 166): normalização `(i.Extension ?? string.Empty).ToLowerInvariant()` antes de `Array.IndexOf(BaseItem.SupportedImageExtensions, ...)`, evitando que extensões em maiúsculas (`poster.JPG`) recebam índice `-1` e ordenem antes de `poster.png`.
   - Dois novos testes unitários adicionados em `LibraryManagerImageConversionTests`, que passou **6/6**.
4. **Suítes NebulaPython, Packaging, Release e Frontend Web**:
   - `Tools/NebulaPython` (`pytest` via venv Python 3.10 locked): **726 aprovados, 2 ignorados (POSIX), 0 falhas**.
   - Packaging PowerShell (`MulletaFlix-packaging-master/scripts/tests/*.Tests.ps1`): **5/5 aprovados**.
   - Release Node (`node --test tools/release/tests/*.test.mjs`): **33/33 aprovados**.
   - Frontend `MulletaFlix-web-master`: `npm test -- --run` **76 arquivos / 421 testes aprovados**; `npm run build:check` (`tsc --noEmit`) **0 erros**.

## Auditoria comparativa Jellyfin: metadados locais, subpastas de imagens, TMDb e conversão lazy — 2026-11-03 22:12 BRT

Comparação read-only com Jellyfin upstream (`MediaBrowser.Providers`, `Emby.Server.Implementations`, `Jellyfin.Api`) identificou e corrigiu via TDD (RED/GREEN) quatro grupos de defeitos no tratamento de informações de mídia, imagens e capas:

1. **`MetadataService.RefreshWithProviders` (`MediaBrowser.Providers/Manager/MetadataService.cs`)**: `localItem.Images` agora é mesclado antes de `localItem.RemoteImages`, dando precedência a arquivos locais sobre URLs remotas do provider local; e os tipos existentes em `item.ImageInfos` são fotografados antes do loop `localItem.RemoteImages`, impedindo que o primeiro backdrop salvo bloqueie o segundo e demais backdrops locais quando `!options.IsReplacingImage(ImageType.Backdrop)`.
2. **`ItemImageProvider.MergeImages` (`MediaBrowser.Providers/Manager/ItemImageProvider.cs`)**: substituído `item.ContainingFolderPath.Contains(Path.GetDirectoryName(image.FileInfo.FullName))` pelo helper `IsImageStoredWithMedia(item, image)`, reconhecendo imagens na pasta da mídia e em subpastas (`extrafanart`, `extrathumbs`, `metadata`) e eliminando falsos positivos com caminhos relativos (`""`) ou pastas irmãs de mesmo prefixo (`C:\Media\Movie` vs `C:\Media\Movies\Sample`).
3. **Provedores TMDb (`MediaBrowser.Providers/Plugins/Tmdb/*`)**: `TmdbPersonImageProvider`, `TmdbMovieProvider`, `TmdbSeriesProvider`, `TmdbSeasonProvider`, `TmdbEpisodeProvider`, `TmdbBoxSetProvider` e `TmdbPersonProvider` agora validam IDs TMDb com `int.TryParse(..., NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0` antes de resolver idioma ou chamar `_tmdbClientManager` (evitando `FormatException` e permitindo fallback para IMDb/TVDB/busca por nome). `TmdbMovieProvider.GetSearchResults` passa `searchInfo.MetadataLanguage` (e não `GetImageLanguagesParam`) a `FindByExternalIdAsync` e faz fallback para TVDB/nome quando `FindByExternalIdAsync` retorna lista vazia (`Count == 0`), alinhado a `TmdbSeriesProvider.GetSearchResults`. Corrigida precedência do operador `??` na capacidade inicial de `List<RemoteImageInfo>` em `TmdbMovieImageProvider`, `TmdbSeriesImageProvider` e `TmdbBoxSetImageProvider`.
4. **Conversão lazy de image stubs remotos (`Emby.Server.Implementations/Library/LibraryManager.cs` e `Jellyfin.Api/Controllers/ImageController.cs`)**: `LibraryManager.ConvertImageToLocal` agora tenta todas as URLs separadas por `|` mesmo quando uma URL candidata falha com erro HTTP transitório (`5xx`/rede) ou `InvalidDataException`, e só remove o image stub quando todas as URLs falham permanentemente (`404`/`403`/`410`/payload inválido), relançando a falha transitória caso contrário. `ImageController.GetImageInternal` captura `InvalidOperationException` e `HttpRequestException` na conversão lazy, tenta `TryRecoverMissingBookPrimaryImageAsync` na primeira tentativa e retorna `204 NoContent` em vez de erro 500 não tratado.

Evidência Windows/.NET 10 Release: 13 novos casos RED falharam antes das correções e passaram em GREEN; `Jellyfin.Providers.Tests` **944 aprovados, 13 ignorados, 0 falhas/957**; `Jellyfin.Server.Implementations.Tests` **1.755 aprovados, 95 ignorados, 0 falhas/1.850**; `Jellyfin.Api.Tests` **831 aprovados, 3 ignorados, 0 falhas/834**; build Release `MulletaFlix.sln` **0 erros**; probe read-only `127.0.0.1:8096` confirmou `/health` e `/System/Info/Public` HTTP 200 na versão instalada `12.1.45`. Runtime instalado não contém as correções da working tree; homologação formal permanece 2/7 (28,6%).

## Probe pós-pacote 12.1.46 — 2026-11-03 22:25 BRT

O probe read-only confirmou que o pacote local `12.1.46` ainda não foi aplicado. O processo instalado continua no PID `24732`, versão **12.1.45**. `/health`, `/System/Info/Public` e `/Users/Public` responderam HTTP 200. `/metrics` respondeu HTTP 200 com **477.526 bytes** e ainda publicou `pool_name` em 158 ocorrências, `User ID` em 158, `Server=` em 158 e host em 160. Nenhum restart, instalação ou mutação ocorreu durante o probe.

## Pacote local de release 12.1.46 — 2026-11-03 22:17 BRT

Pacote local criado a partir da working tree para homologação controlada. Build Release serial da solução terminou com **0 erros e 4 avisos**. ZIP de atualização: `dist/mulletaflix-update-win-x64.zip`, **128.092.167 bytes**, SHA-256 `B9DA1ED28ED281A8FD89A28EC885B0BD167D61CBF49BEE148E932795D88C70B6`, 2.524 entradas; inclui `MulletaFlix.dll`, `apply-update.ps1`, `Tools/NebulaPython/stream_service.py` e web client. Instalador NSIS: `dist/mulletaflix_12.1.46_windows-x64.exe`, **415.470.877 bytes**, SHA-256 `2937C7D3958C75EA156FB381CF5B69CD93E4D677AAE06521F0902E503268734B`. Nenhum commit, push, publicação ou alteração no runtime instalado foi executado. O pacote representa o estado atual inteiro da working tree, que possui alterações amplas não commitadas.

## Revalidação read-only do runtime e inventário de SaveImage — 2026-11-03 19:42 BRT

O runtime instalado continua ativo na versão **12.1.45**. `/health`, `/System/Info/Public` e `/Users/Public` responderam HTTP 200; `/Items?Limit=1` respondeu HTTP 401 sem credencial. `/metrics` respondeu HTTP 200 com **339.050 bytes** e ainda publicou `pool_name` em 186 ocorrências, `User ID` em 186, `Server=` em 186 e host em 188; valores foram redigidos. Inventário read-only enumerou callers de `SaveImage`, `SetImageSource`, `RemoteImageInfo` e `ProviderName`, sem novo gap confirmado. A working tree continua sem crédito no runtime.

## Regressão ampla API — 2026-11-03 19:32 BRT

`Jellyfin.Api.Tests` passou **830 aprovados, 3 ignorados, 0 falhas/833 casos** em Release serial com Integration excluído. Os três ignorados são cenários Linux/ACL e preservação de arquivo diante de alteração concorrente; não contam como aprovação. O resultado cobre controllers, autorização, imagens remotas, persistência de imagem, cancelamento HTTP e rotas administrativas.

## Regressão ampla Server Implementations — 2026-11-03 18:43 BRT

`Jellyfin.Server.Implementations.Tests` passou **1.753 aprovados, 95 ignorados, 0 falhas/1.848 casos** em Release serial com Integration excluído. O resultado cobre persistência, biblioteca, usuários, restore e Nebula dentro da working tree; ignorados permanecem fora da aprovação. Comparação read-only com Jellyfin upstream foi revisada para `ProviderManager`, `ItemImageProvider` e `MetadataService`; divergências locais continuam deliberadas e cobertas por testes.

## Contorno seguro MariaDB e locks — 2026-11-03 18:02 BRT

`ItemPersistenceServiceTests` passou **39/39** em Release serial, cobrindo stripes, locks de item/parent/owner e classificação de deadlocks/conflitos transitórios. Para integração, o harness exige conexão explícita a MariaDB descartável em loopback e porta não padrão, com confirmação `I_ACKNOWLEDGE_TESTS_CREATE_AND_DROP_SCHEMAS`. A máquina não tem variáveis configuradas nem binaries standalone conhecidos. Proxy para `3306` foi recusado porque burlaria o fail-closed e poderia mutar a instância operacional. Próximo contorno seguro: iniciar instância descartável por Docker/Podman/WSL ou binário standalone, sem tocar MariaDB de produção.

## Revalidação ampla Providers — 2026-11-03 17:57 BRT

A suíte serial `Jellyfin.Providers.Tests` sem Integration passou **931/931**, com **0 falhas e 0 ignorados**. O resultado fortalece a evidência local de providers, idioma, fallback, proveniência e metadata, mas não valida o runtime instalado, TMDb real, item persistido ou capa renderizada.

## Revalidação provider/fallback e probe autenticado — 2026-11-03 17:54 BRT

A consulta Graphify no grafo existente encontrou 65.443 nós relacionados a providers, idioma, fallback e proveniência. A leitura do código confirmou que TMDb preserva o idioma real por imagem, converte `xx` em sem idioma e mantém `ProviderName`; `GetImageLanguagesParam` produz idioma preferido + `null` + inglês sem duplicação. Filtro Release focal passou **84/84**, com build concluído em **0 erros e 19 avisos existentes**. Runtime instalado respondeu `/Items` com HTTP 401 sem credencial e `/Users/Public` com HTTP 200 e lista vazia. Não houve instalação, restart, refresh ou mutação.

## Redaction local de métricas — 2026-11-03 17:44 BRT

Auditoria confirmou `Jellyfin.Server/Startup.cs` conectando `MetricsLabelRedactor.ShouldPublishInstrument` ao `Prometheus.MeterAdapter`, removendo o meter `MySqlConnector` do endpoint público. `MetricsLabelRedactionTests` passou **8/8** em Release. O runtime instalado `12.1.45` continua anterior à correção e ainda expõe `pool_name`, `User ID`, `Server=` e host em `/metrics`; nenhuma instalação ou mutação de produção ocorreu.

## Probe read-only do runtime instalado — 2026-11-03 17:33 BRT

Revalidado `MulletaFlix.exe` instalado, versão **12.1.45**, PID **24732**. `/health` e `/System/Info/Public` responderam HTTP **200**. `/metrics` respondeu HTTP **200** com **363.566 bytes** e ainda publicou `pool_name` em **158** ocorrências, `User ID` em **158**, `Server=` em **158** e `127.0.0.1` em **160**; valores foram redigidos. O binário instalado continua sem crédito pelas correções da working tree. Não houve instalação, restart, refresh, mutação, commit, push ou release. Repetir após instalação autorizada permanece pendente.

## Fallback após identificação por provider — 2026-11-03 16:12 BRT

Auditoria comparativa encontrou filtragem destrutiva em `MetadataService.ExecuteRemoteProviders`: `SearchProviderName` removia todos os providers exceto o selecionado. Em `ItemLookupController.ApplySearchCriteria`, isso podia deixar o item sem metadata quando o provider escolhido retornava vazio ou falhava. A correção preserva todos os providers e ordena o selecionado primeiro; a prioridade do resultado identificado permanece, mas providers seguintes podem preencher metadata ausente. TDD RED reproduziu `OtherProvider` não chamado. GREEN: `RefreshWithProviders_IdentifySearchProvider_PrioritizesSelectedProvider` e `RefreshWithProviders_IdentifySelectedProviderWithoutMetadata_FallsBackToOtherProvider`. Evidência: focais **2/2**, `MetadataServiceTests` **50/50**, `Jellyfin.Providers.Tests` sem Integration **928/928**, regressão serial **5.555 aprovados, 98 ignorados, 0 falhas/5.653 casos**, build Release **0 erros e 594 avisos existentes**. Comparação read-only com Jellyfin upstream confirmou ordenação do provider selecionado sem remover os demais. Runtime instalado, providers remotos reais, CI hospedada e Linux/POSIX continuam sem aceite.

## Contrato de falha em providers remotos — 2026-11-03 15:55 BRT

Auditoria identificou que `ProviderManager.GetImages` registrava exceção de `IRemoteImageProvider.GetImages` e retornava `Enumerable.Empty<RemoteImageInfo>`. Em refresh, isso podia parecer ausência limpa de candidatos e permitir avanço indevido de `DateLastRefreshed`. TDD RED: `GetAvailableRemoteImages_SelectedProviderFailureIsNotReportedAsEmpty` falhou porque a falha era convertida em vazio. GREEN: `GetImages` retorna imagens e erro separadamente; `GetAvailableRemoteImages` lança `AggregateException` somente quando todos os providers falham e preserva resultados de providers saudáveis. `ItemImageProvider.RefreshImages` converte falha total em `RefreshResult.Failures`, sem atualizar refresh; cancelamento continua relançado. Evidência: ProviderManager focal 2/2; ItemImageProvider focal 1/1; `Jellyfin.Providers.Tests` sem Integration 927/927; regressão serial atual 5.554 aprovados, 98 ignorados, 0 falhas/5.652 casos; build Release 0 erros e 594 avisos existentes; `git diff --check` passou. Execução 5.549/5.647 foi superseded por execução atual após build; execução 82/82 cobriu somente último TRX por sobrescrita de nome. Correção apenas na working tree; runtime instalado e providers reais continuam sem aceite.

## Proveniência em imagens dinâmicas — 2026-11-03 15:37 BRT

Auditoria read-only encontrou perda de `ProviderName` em imagens produzidas por `IDynamicImageProvider`. O fluxo HTTP criava `ItemImageInfo` sem provider; fluxos stream e arquivo delegavam a `SaveImage` e também deixavam a imagem sem proveniência. Isso impedia classificação posterior por provider para stale-image replacement, pruning e proteção de ownership. TDD RED alterou os testes HTTP e stream para exigir `MockDynamicProvider`; ambos falharam com `null`. GREEN gravou `provider.Name` no item HTTP e aplicou o mesmo valor à imagem salva nos caminhos stream/arquivo. O teste de caminho de arquivo também foi fortalecido. Testes focais passaram 3/3; `Jellyfin.Providers.Tests` sem Integration passou 924/924; build Release serial passou com 0 erros e 20 avisos existentes; `git diff --check` passou. Correção somente na working tree; runtime instalado 12.1.45, providers remotos reais, CI hospedada, Linux/POSIX e produção continuam sem aceite.

## Proveniência em image stubs remotos — 2026-11-03 15:18 BRT

`ItemImageProvider.SaveImageStub` copiava `RemoteImageInfo.ProviderName` e `Language`, mas não passava o `IRemoteImageProvider` atual a `SetImageSource`. Quando o provider retornava `ProviderName` vazio, a imagem remota ficava sem origem. TDD RED/GREEN: `RefreshImages_RemoteStubWithoutProviderName_UsesRemoteProviderName` falhou com `null` e passou após `SaveImageStub` receber o provider nos caminhos singular e múltiplo; `SetImageSource` aplica `provider.Name` como fallback. `Jellyfin.Providers.Tests` Release passou 924 aprovados, 13 ignorados, 0 falhas/937. O upstream Jellyfin mantém o stub sem este fallback; a correção local reforça a proveniência já aplicada no download local. Tentativa concorrente encontrou lock de DLL em `testhost`; build Release serial posterior passou com 0 erros e 565 avisos existentes. Runtime instalado 12.1.45 não contém a mudança.

## Revisão adversarial do cancelamento Live TV — 2026-11-03 15:18 BRT

A revisão encontrou três gaps no lote anterior. `RecordingsManager` capturava `OperationCanceledException` no callback assíncrono como erro genérico; `SaveSeriesNfoAsync` e `SaveVideoNfoAsync` não recebiam o token; e a troca para quatro parâmetros não mantinha o método público de três parâmetros para callers binários antigos. TDD RED reproduziu a ausência do overload e o cancelamento durante carregamento da configuração NFO. GREEN adicionou o overload compatível, propagou o token às duas rotinas NFO, verificou cancelamento antes da geração e após carregar configuração, e registrou cancelamento normal em nível debug. `RecordingsMetadataManagerTests` passou 4/4; `Jellyfin.LiveTv.Tests` passou 84/84; build Release passou com 0 erros e 565 avisos existentes; regressão serial completa sem Integration passou 5.551 aprovados, 98 ignorados, 0 falhas/5.649 casos em 15 assemblies. Runtime instalado 12.1.45 não contém a correção.

## Cancelamento nas imagens de gravações Live TV — 2026-11-03 15:18 BRT

`RecordingsMetadataManager.SaveRecordingMetadata` era o caller de produção restante que convertia capas remotas sem receber o token da gravação. TDD RED reproduziu a ausência da sobrecarga com `CancellationToken`; GREEN adicionou parâmetro opcional, propagou `recordingInfo.CancellationTokenSource.Token` desde `RecordingsManager`, encaminhou-o a todas as conversões de imagem e relança `OperationCanceledException` nos ramos de imagem em vez de registrá-la como falha comum. `RecordingsMetadataManagerTests` passou 82/82; build Release da solução terminou com 0 erros e 565 avisos existentes; `git diff --check` passou. Busca de twins por `ConvertImageToLocal` encontrou os callers de produção com token; usos sem token restantes estão em testes de compatibilidade. Runtime instalado 12.1.45 não contém a mudança; nenhuma mutação de produção foi executada.

## Cancelamento em atualização de imagens — 2026-11-03 14:38 BRT

`ILibraryManager.UpdateImagesAsync` não recebia token, embora `ImageController`, `BaseItem.RefreshMetadata`, `Folder` e `UpdateItemsAsync` já operassem em fluxos canceláveis. TDD RED reproduziu a ausência de contrato; GREEN adicionou `CancellationToken` opcional a `UpdateImagesAsync` e `RunMetadataSavers`. `LibraryManager` agora encaminha o token a `ConvertImageToLocal` e `IItemPersistenceService.SaveImagesAsync`, verificando cancelamento antes de cada imagem. `ImageControllerIndexTests` passou 3/3; `LibraryManagerImageConversionTests` passou 2/2; `Jellyfin.Api.Tests` sem Integration passou 830 aprovados, 3 ignorados, 0 falhas/833; regressão serial sem Integration terminou exit 0 com 5.545 aprovados, 98 ignorados, 0 falhas/5.643 casos observados; build Release serial terminou com 0 erros e 600 avisos existentes. O agregado histórico 5.642/5.740 foi preservado sem misturar denominadores. Callers sem token preservam compatibilidade pelo default. Runtime instalado 12.1.45 não contém a correção; nenhuma mutação de produção foi executada.

## Cancelamento em `ConvertImageToLocal` — 2026-11-03 14:20 BRT

O teste novo de conversão revelou que a interface `ILibraryManager.ConvertImageToLocal` não aceitava o token necessário. Após a correção, `LibraryManager` encaminha `CancellationToken` a `ProviderManager.SaveImage` e `UpdateToRepositoryAsync`; `ImageController` e `GuideManager` encaminham seus tokens disponíveis. `LibraryManagerImageConversionTests` passou 2/2 e o build Release da solução terminou com 0 erros e 600 avisos existentes. Callers sem token continuam compatíveis pelo parâmetro default; isso não prova cancelamento nesses fluxos. Runtime instalado 12.1.45 não contém a mudança.

## Probe read-only de produção e comparação upstream — 2026-11-03 14:04 BRT

Runtime instalado `12.1.45` confirmou `/health`, `/System/Info/Public` e `/metrics` HTTP 200; processo `24732`. MariaDB empacotado `11.4.4`, processo `8792`, escuta em `127.0.0.1:3306`. O endpoint `/metrics` ainda publica `pool_name` em **158** linhas, além de ocorrências de `User ID`, `Server=` e `127.0.0.1`; valores foram redigidos.

Consulta read-only encontrou 0 transações ativas e três conexões de aplicação ociosas. `SHOW ENGINE INNODB STATUS` confirmou o deadlock do log: `INSERT` em lote de `BaseItemImageInfos` contra `DELETE ... WHERE ItemId = ...`, disputando `IX_BaseItemImageInfos_ItemId_ImageType`; a transação de inserção foi revertida. A working tree atual já usa stripes locais, lock MariaDB e retry, mas o binário instalado não contém as correções.

Comparação read-only com Jellyfin upstream: o upstream mantém a substituição `DELETE` + `INSERT` sem a serialização/retry local. O fluxo upstream de imagens poda stale somente após novos downloads; a proteção local permanece compatível com refresh parcial. Nenhuma mutação de produção foi executada.

## Log de produção: deadlocks e starvation — 2026-11-03 13:59 BRT

O recorte de produção (5.981 linhas; `07:10:15.464`–`11:28:56.252`) confirma um incidente operacional de persistência: 182 deadlocks MariaDB, 124 comandos falhos e 96 retries de imagens, predominantemente no `DELETE BaseItemImageInfos`, além de 29 retries de itens. O log também registra 21 heartbeats atrasados do Kestrel, máximo de 1m14,232s, 59 colisões de arquivo de imagem em uso, 16 falhas de metadata saver por `#sql-temptable-*` ausente e um timeout de conexão. O recorte termina durante retry, sem prova do resultado final; não creditar como resolvido.

O código atual já serializa o item em memória e usa lock de banco em `ItemPersistenceService.SaveImagesAsync`, mas o log prova que isso não fecha o problema em produção. Próxima evidência exigida: `SHOW ENGINE INNODB STATUS`, lista de transações/conexões ativas e correlação por process/assembly version. Nenhuma alteração foi aplicada no runtime.

## Cancelamento em ItemLookup e regressão sistêmica — 2026-11-03 13:45 BRT

`ItemLookupController` passava `CancellationToken.None` em oito endpoints de busca remota e no fluxo `ApplySearchCriteria`, incluindo refresh, fallback de imagem e persistência. TDD RED/GREEN adicionou três testes de propagação; GREEN usa `HttpContext.RequestAborted` em todos esses pontos. Busca de twins encontrou zero ocorrências de `CancellationToken.None` nos controllers `ItemLookupController.cs` e `RemoteImageController.cs`. API sem Integration passou 828 aprovados, 3 ignorados, 0 falhas/831; build Release passou 0 erros; regressão serial sem Integration passou 5.642 aprovados, 98 ignorados, 0 falhas/5.740. Runtime instalado 12.1.45 ainda não contém a correção.

## Runtime instalado: `/metrics` ainda expõe `pool_name` — 2026-11-03 13:34 BRT

Probe read-only contra `C:\\Program Files\\MulletaFlix\\Server\\MulletaFlix.exe` confirmou versão `12.1.45`, PID `24732`, `/health` HTTP 200 `Healthy` e `/System/Info/Public` HTTP 200. `/metrics` respondeu HTTP 200 e publicou `pool_name` em **130 linhas** do MySqlConnector; valores foram redigidos e não foram persistidos. A working tree contém `MetricsLabelRedactor`, validado por `MetricsLabelRedactionTests` **8/8** em Release, mas o runtime instalado ainda não contém essa correção. Não houve mutação de produção. O aceite exige instalação autorizada seguida de novo probe read-only.

## Cancelamento HTTP em imagens remotas e regressão sistêmica — 2026-11-03 13:20 BRT

`RemoteImageController` usava `CancellationToken.None` ao consultar imagens remotas, impedindo que o cancelamento do request interrompesse o provider. O controller agora encaminha `HttpContext.RequestAborted` para `GetAvailableRemoteImages`, `SaveImage` e `UpdateToRepositoryAsync` nos fluxos `GetRemoteImages` e `DownloadRemoteImage`. TDD RED/GREEN: testes focais de cancelamento 2/2; `RemoteImageControllerTests` passou; `Jellyfin.Api.Tests` sem Integration passou 826 aprovados, 3 ignorados, 0 falhas/829. Build Release passou 0 erros; regressão serial sem Integration passou 5.640 aprovados, 98 ignorados, 0 falhas/5.738; portal 361/361. `TWINS: searched CancellationToken.None em RemoteImageController.cs` - found 0 outros sites. Runtime instalado 12.1.45, provider remoto real, CI/Linux, instalação persistente e produção permanecem pendentes.

## Seleção exclusiva do provider identificado e regressão sistêmica — 2026-11-03 11:28 BRT

`MetadataService.RefreshWithProviders` agora restringe a execução ao provider indicado por `MetadataRefreshOptions.SearchResult.SearchProviderName`; antes, a identificação executava toda a lista e permitia que provider diferente sobrescrevesse o resultado escolhido. TDD RED/GREEN: `RefreshWithProviders_IdentifySearchProvider_OnlyRunsSelectedProvider` passou 1/1 após RED; `MetadataServiceTests` passou 49/49; `Jellyfin.Providers.Tests` sem Integration passou 921/921. Regressão serial sem Integration passou 5.537 aprovados, 98 ignorados, 0 falhas/5.635; build Release passou com 0 erros e 593 avisos existentes. Jellyfin upstream foi consultado somente leitura: `ProviderManager` filtra buscas pelo provider identificado, enquanto `MetadataService` prioriza sem restringir; a restrição local é deliberada e aguarda validação com providers reais. Revalidação adicional do caso anteriormente observado como falha `SaveRemoteResultImages_SingularFailureRecoveredByNextProvider_DoesNotRemainFailed` passou **1/1** isoladamente; a falha não reproduziu após a serialização atual. Isso reduz o risco de regressão no fallback de imagem, mas não substitui validação com providers remotos reais. Runtime instalado 12.1.45 não foi alterado.

## Proteção contra referências compartilhadas, fallback inglês e regressão sistêmica — 2026-11-03 10:47 BRT

A revisão adversarial encontrou e o TDD corrigiu quatro gaps no `ItemImageProvider`: prune podia retirar stale sem replacement, fallback inglês dependia do índice zero, exclusão física não consultava referências em outros tipos/entidades e o branch de stream dinâmico não persistia `ProviderName`. `PruneImages` agora verifica o item inteiro e `ILibraryManager.GetItemList` com todos os tipos de imagem antes de apagar; sem índice ou diante de erro, faz somente detach. `IOException` vira warning e não interrompe a remoção da referência. O fallback inglês avalia qualquer stale não-inglês e o prune parcial prioriza esses stale. TDD: referências compartilhadas 1/1, `IOException` 1/1, fallback inglês multi-backdrop 1/1, stream dinâmico 1/1; classe 85/85; Providers sem Integration 920/920; build Release 0 erros/593 avisos; regressão serial 5.536 aprovados, 98 ignorados, 0 falhas/5.634 casos. Runtime instalado permanece sem as correções da working tree e não recebe crédito formal.

## Seleção localizada, limpeza, prune parcial e preferência global — 2026-11-03 10:00 BRT

`ItemImageProvider` agora propaga `preferredLanguage` até `RefreshFromProvider`, resolve preferência global para item vazio, prioriza candidato localizado com `MatchesLanguage`, remove arquivo local somente quando a imagem substituída mantém `ProviderName` remoto e pruna somente tantos backdrops obsoletos quanto replacements realmente baixados durante refresh localizado. Backdrop manual sem proveniência continua protegido; `RemoveImages` segue apenas removendo referências. TDD: preferência local 1/1, preferência global em item vazio 1/1, fallback após MIME inválido 1/1, limpeza remota/preservação manual 1/1, prune parcial 1/1, classe 81/81, `Jellyfin.Providers.Tests` 916 aprovados/13 ignorados. Build Release 0 erros/593 avisos; regressão 5.532 aprovados, 98 ignorados, 0 falhas/5.630 casos. Fallback entre múltiplos providers e runtime instalado permanecem pendentes.

## Cobertura adicional de backdrops — 2026-11-03 07:59 BRT

O teste `RefreshImages_ReplacesMultipleStaleRemoteBackdropsBeforePruning` cobre dois backdrops remotos obsoletos, um manual e limite `1`; passou 1/1. A classe `ItemImageProviderTests` passou 75/75. A revisão adversarial também identificou mutação de estado estático nos testes `ImageSaverTests`; a classe foi colocada na coleção não paralela `LibraryManagerTests`, e o filtro passou 5/5.

## Substituição de backdrop obsoleto com backdrop manual — 2026-11-03 07:53 BRT

`ItemImageProvider` agora reserva capacidade temporária para cada backdrop remoto obsoleto antes do prune. RED reproduziu que limite `1` + remoto `en` obsoleto + backdrop manual não baixava o candidato `pt`; GREEN passou com limite 1 e 2, mantendo manual + remoto localizado. `ItemImageProviderTests` 74/74; Providers sem Integration 909/909 após reexecução; build Release 0 erros e 20 avisos existentes. A ordem sem idioma primeiro continua igual ao Jellyfin upstream e fica pendente de homologação funcional com catálogo real.

## Regressão serial sem Integration — 2026-11-03 07:41 BRT

`dotnet test MulletaFlix.sln --no-restore --no-build --configuration Release --filter 'FullyQualifiedName!~Integration' -m:1` passou com **5.523 aprovados, 98 ignorados, 0 falhas, 5.621 casos**. Providers passou 908/908; Server.Implementations passou 1.752 com 95 ignorados. Os skips dependem de Linux/macOS, Mongo/MariaDB opt-in ou fixtures externos; não contam como homologação.

## Runtime instalado — probe read-only 2026-11-03 07:30 BRT

O runtime instalado permanece em `C:\Program Files\MulletaFlix\Server\MulletaFlix.exe`, versão 12.1.45, PID 24732. `/health` respondeu HTTP 200 com `Healthy`; `/System/Info/Public` respondeu HTTP 200 com `Version=12.1.45`. Probe não alterou produção e não valida correções presentes apenas na working tree.

## Retry de ImageSaver com múltiplos caminhos e streams non-seekable — 2026-11-03 07:25 BRT

`ImageSaver` calculava dois caminhos compatíveis para backdrop duplicado, mas um único `retryPath` interno. A condição `paths.Length == retryPaths.Length` deixava `retryPath` nulo e convertia falha no diretório compatível em erro final. RED reproduziu `IOException` ao ocupar `extrafanart` com arquivo; GREEN mudou a seleção para `retryPaths.Length > i`, usando fallback por índice existente. Segundo RED mostrou que bufferizar stream non-seekable sem reposicionar o `MemoryStream` salvava zero bytes; GREEN define `Position = 0` após `CopyToAsync`. `ImageSaverTests` passou 5/5. Jellyfin upstream ainda possui a condição antiga; as correções locais cobrem cenários confirmados sem ampliar API.

## Fallback de idioma preferido em overview e tagline — 2026-11-03 06:54 BRT

Comparação read-only com Jellyfin upstream mostrou que o refresh mantém marcadores locais para `overviewIsFallback` e `taglineIsFallback`: um resultado em idioma não preferido pode preencher o campo, mas um provider posterior no idioma preferido deve substituí-lo. O fluxo local não tinha essa proteção. RED reproduziu dois providers retornando inglês seguido de `pt-BR`, mantendo o texto inglês. GREEN adicionou o rastreamento por campo em `ExecuteRemoteProviders` e comparação exata/regional (`pt`/`pt-BR`), com teste `RefreshWithProviders_PrefersPreferredLanguageOverEarlierFallback` passando 1/1 e classe `MetadataServiceTests` 48/48. Imagens e múltiplos backdrops permanecem separados.

## Reexecução MariaDB concorrente — resultado preservado — 2026-11-03 06:54 BRT

Uma execução duplicada do script de persistência MariaDB, concorrente com outra execução do mesmo cenário, terminou com 21 aprovados, 13 falhas e 1 ignorado em 35 casos. A falha ocorreu na resolução do serviço `Contexts` durante `InitializeAsync` (`ServerPersistenceMariaDbTests.cs:96/136`); não foi promovida como regressão funcional. A execução limpa subsequente passou 34 aprovados, 1 ignorado, 0 falhas/35. O caso ignorado continua opt-in por reinício do processo MariaDB. O listener operacional 3306 não foi tocado; sem processo scratch remanescente. Formal 2/7.

## Auditoria contínua — preservação de idioma regional explícito — 2026-11-03 06:47 BRT

`BaseItem.GetPreferredMetadataLanguage` convertia globalmente `pt-PT` para `pt-BR` quando o país era BR. A comparação com Jellyfin upstream e com `TmdbUtils` mostrou que a conversão deve ficar no adaptador do provider/API que exige ISO 639-1, não no modelo compartilhado. TDD RED/GREEN: `GetPreferredMetadataLanguage_PreservesExplicitRegionalLanguage` passou 1/1 após remover a normalização; `BaseItemTests` passou 13/13; `Jellyfin.Providers.Tests` sem `Integration` passou 905/905; build Release terminou com 0 erros. Runtime instalado 12.1.45 não foi alterado; homologação formal continua 2/7.

## Auditoria contínua — IDs TMDb inválidos em providers de imagens — 2026-11-03 06:29 BRT

`TmdbSeriesImageProvider`, `TmdbSeasonImageProvider`, `TmdbEpisodeImageProvider` e `TmdbBoxSetImageProvider` agora validam `ProviderId` com `int.TryParse` antes de resolver idioma ou consultar TMDb. A auditoria TDD reproduziu quatro `FormatException` com `not-a-number`; GREEN passou 4/4. Filtro combinado de imagem/TMDb passou 36/36; regressão `Jellyfin.Providers.Tests` sem `Integration` passou 905/905 após isolar uma falha transitória de `AudioResolverTests`; `git diff --check` passou. Build Release da solução passou serialmente com 0 erros e 20 avisos existentes. Busca de twins não encontrou `Convert.ToInt32` remanescente em `*ImageProvider.cs` no módulo TMDb. Providers de metadata não-imagem seguem fora deste slice; runtime instalado 12.1.45 não foi alterado. Homologação formal permanece 2/7.

## Auditoria contínua — proveniência em imagens dinâmicas — 2026-11-03 05:17 BRT

`ItemImageProvider` agora persiste `provider.Name` quando provider dinâmico entrega URL HTTP ou arquivo local. TDD 2/2; filtro de imagens 73/73; Providers 901 aprovados/13 ignorados/0 falhas; build Release 0 erros/1 aviso XML. Runtime instalado não foi alterado; homologação formal segue 2/7.

## Auditoria contínua — retry seguro de imagem — 2026-11-03 04:56 BRT

`ImageSaver` agora reinicia stream seekable em zero antes do retry e bufferiza stream não seekable quando `retryPaths` existem. TDD `ImageSaverTests` 3/3; `Jellyfin.Providers.Tests` 899 aprovados/13 ignorados/0 falhas; build Release 0 erros/1 aviso XML. Runtime instalado 12.1.45 não foi alterado; homologação formal segue 2/7.

## Auditoria contínua — proveniência na conversão local — 2026-11-03 04:46 BRT

`LibraryManager.ConvertImageToLocal` agora captura e restaura `ProviderName`/`Language` após `SaveImage`, porque `BaseItem.SetImagePath` limpa esses campos durante a troca de URL por arquivo local. Teste TDD 1/1; `Jellyfin.Server.Implementations.Tests` passou 1.752 aprovados, 95 ignorados, 0 falhas/1.847; build Release 0 erros/4 avisos XML. Runtime instalado 12.1.45 não foi alterado; homologação formal segue 2/7.

## Auditoria contínua — índice de imagem na rota — 2026-11-03 04:33 BRT

`ImageController.SetItemImageByIndex` descartava `imageIndex` e enviava `null` ao `IProviderManager.SaveImage`. Correção TDD encaminha o índice recebido na rota; `ImageControllerIndexTests` passou 1/1 e filtro `ImageController` passou 53/53. Busca de twins encontrou somente `SetItemImage`, endpoint sem índice que mantém `null` intencionalmente. Build Release passou com 0 erros e 4 avisos XML preexistentes. Homologação formal segue 2/7; runtime instalado 12.1.45 não foi alterado.

## Auditoria contínua de imagens e metadados — 2026-11-03 04:18 BRT

Comparação read-only com Jellyfin upstream e TDD local corrigiram dois pontos: `MetadataService.RefreshMetadata` só avança `DateLastRefreshed` quando metadata ou imagem tentam busca real; `ImageProcessor` não colide cache entre `PercentPlayed` e `UnwatchedCount`, usando marcadores separados e versão 4. Providers passou 898 aprovados/13 ignorados/0 falhas; focal API/imagens 3/3; build Release 0 erros/3 avisos XML preexistentes. O inventário de callers ainda mantém bypasses de provenance em `RemoteImageController`, `ItemLookupController`, conversão lazy e `LibraryManager`; eles continuam pendentes para testes TDD separados. Runtime instalado 12.1.45 respondeu somente a probes read-only e não contém a working tree. Homologação formal 2/7.

## Revalidação após retomada do Codex — 2026-11-02 22:25 BRT

O checkout atual contém retry da unidade transacional completa, em contexto novo, para capítulos, streams e pessoas. A errata de 21:35 em `TODO-AUDITORIA.md` descreve o estado anterior às alterações de 21:42–21:58; não representa mais o código atual. `ItemRepositoryTransientRetryTests` foi reexecutado em Windows/.NET 10 Release: 17 aprovados, 0 ignorados/falhas, exit 0, aproximadamente 12 s incluindo build. A integração MariaDB descartável 1/1 às 22:18 é evidência anterior, não repetida nesta sessão.

Pesquisa oficial: MariaDB informa que erro 1205 reverte a instrução que aguardou por padrão (a transação inteira somente com `innodb_rollback_on_timeout`); por isso o retry deve descartar contexto/transação e repetir a unidade completa. EF Core alerta que retries com transação explícita devem envolver todas as operações da unidade repetível e que falha no commit pode deixar resultado ambíguo. O código só repete códigos de lock 1205/1213; resultado ambíguo por falha de conexão/commit continua fora da política. Fontes: https://mariadb.com/docs/server/server-usage/storage-engines/innodb/innodb-system-variables e https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency. Sem mudanças de módulo, classe, API, dependência ou fluxo de produção; formal 2/7, sem release. `last_verified: 2026-11-02`.

# Retries transacionais de capítulos e streams — 2026-11-02 21:42 BRT

## Evidência de lock wait timeout MariaDB real — 2026-11-02 22:18 BRT

O teste opt-in `ServerPersistenceMariaDbTests.SaveChaptersAsync_RetriesAfterRealMariaDbLockTimeout` comprovou o retry do caminho de substituição assíncrona em MariaDB 11.4.4 descartável. O lock concorrente gerou 1205; o teste observou o warning, liberou o lock e verificou o novo capítulo persistido. `innodb_lock_wait_timeout=1` é aplicado somente às conexões do factory de teste por `DbConnectionInterceptor.ConnectionOpenedAsync`; o global foi conferido em 50 s antes e depois. Resultados: cenário real 1/1 e simulação SQLite das três classes 17/17. Não comprova produção, Linux, CI hospedada, runtime instalado, carga, nem caminho síncrono dos streams. O CI executa o cenário no job MariaDB dedicado. Técnica compatível com o escopo de sessão documentado pelo MariaDB e o hook oficial de conexão assíncrona do EF Core: https://mariadb.com/docs/server/server-usage/storage-engines/innodb/innodb-system-variables ; https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectionopenedasync?view=efcore-10.0 . `last_verified: 2026-11-02`.

Implementados em `ChapterRepository.SaveChapters/SaveChaptersAsync` e `MediaStreamRepository.SaveMediaStreams/SaveMediaStreamsAsync`: cada tentativa cria contexto e transação novos, repete somente em MariaDB 1205/1213, no máximo três tentativas com pausas de 150/300 ms, e registra warning. Os construtores originais foram preservados; overloads com logger permitem observabilidade via DI. Os retries envolvem a substituição inteira e não mascaram falhas permanentes. Testes SQLite com falhas MySqlException injetadas passaram 10/10 (incluem rollback, limite e não-retry de falha permanente); formatter Release e build do projeto de integração passaram. MariaDB real, CI Linux, runtime instalado e produção não foram validados.

Pendência detectada na mesma comparação: `PeopleRepository.UpdatePeople` ainda não repete a transação no checkout, apesar de seção anterior afirmá-lo. Corrigir a memória e tratar esse fluxo em tarefa separada. Homologação formal segue 2/7 (28,6%), sem crédito; nenhuma release.

# Errata: retries de transações em capítulos e streams — 2026-11-02 21:35 BRT

A conferência do checkout atual (branch main, HEAD fa1289b686bf7130315b6f36ab417064f054e8c9) contradiz afirmações anteriores no roadmap e no vault. `ChapterRepository.SaveChapters`, `ChapterRepository.SaveChaptersAsync`, `MediaStreamRepository.SaveMediaStreams` e `MediaStreamRepository.SaveMediaStreamsAsync` executam DELETE + INSERT em transação sem retry para MariaDB 1205/1213. Callers confirmados: `ChapterManager.SaveChapters` (síncrono); `FFProbeVideoInfo` e `AudioFileProber` usam os caminhos assíncronos. Não há testes desses repositórios no checkout atual. O retry configurado em `ItemPersistenceService` não é aplicado automaticamente a esses contextos.

Registros anteriores que afirmam retries implementados (roadmap, ficha e sessões de 02/11) não são corroborados pelo código e pelos worktrees atuais; tratá-los como desatualizados. A hipótese de que mudanças de código/teste tenham se perdido numa sessão interrompida não foi comprovada. Nenhum código foi alterado nesta auditoria; testes não executados. Próximo passo: reconstruir teste de repetição integral com contexto/transação novos e validar 1205/1213 em fixture, seguido de MariaDB descartável opt-in. Sem crédito de homologação ou release. Fonte de técnica: https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency — retry precisa envolver a transação completa.

# NextUp MariaDB: tentativa de catálogo sintético ampliado — 2026-11-02 21:28 BRT

Tentativa local de acrescentar 2.000 episódios de séries fora do filtro ao teste `GetNextUpEpisodesBatch_SelectsEarliestUnplayedEpisodeOnMariaDb`. O fixture alterado não produziu evidência válida: a asserção de contagem revelou 2.020 registros-base, a expectativa fixa de `r_rows=19` não se manteve e a filtragem do JSON por `table_name=BaseItems` não encontrou nós. As alterações experimentais foram revertidas; nenhuma lógica de produção mudou. Não foi obtido tempo válido do plano ampliado. O teste pequeno continua com a evidência anterior, sem revalidação nesta sessão.

O método oficial `ANALYZE FORMAT=JSON` executa a consulta e reporta contadores reais de linhas, loops e duração. A documentação do EF Core alerta que bases pequenas não demonstram desempenho em escala. Fontes: https://mariadb.com/docs/server/reference/sql-statements/administrative-sql-statements/analyze-and-explain-statements/analyze-format-json e https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying. Próximo passo: entender a estrutura real dos nós do plano antes de retomar este teste. Homologação formal 2/7 (28,6%), sem crédito/release. Mapa técnico conferido; módulos, contratos e fluxo de produção inalterados.

# Atualização do driver MariaDB e advisory GHSA-473q-m89c-ghf8 — 2026-11-02 21:09 BRT

`MySqlConnector` 2.4.0 era resolvido transitivamente pelo Pomelo 9.0.0 nos dois consumidores do servidor. Atualizado para 2.6.1, versão corrigida do advisory, com versionamento central e referências diretas em `Jellyfin.Database.Implementations` e `MulletaFlix.Plugin.IntroSkipper`. Pomelo 9.0.0 documenta o intervalo suportado como MySqlConnector >=2.4.0, EF Core 9 e .NET 8+; runtime do servidor usa MariaDB 11.4.2. A atualização não troca o provider nem altera strings/configurações da conexão.

O advisory aplica-se a `SslMode=VerifyCA` ou `VerifyFull`, sem `SslCa`, e com senha não vazia. A revisão dos caminhos locais não encontrou essa configuração; a exposição em produção não foi comprovada. Verificação Windows/.NET 10 Release: `Jellyfin.Server.Implementations.Tests` 1746 aprovados/95 ignorados/0 falhas; `IntroSkipper.Integration.Tests` 22/53/0. Restore completo dos grafos de teste, `dotnet list package --vulnerable --include-transitive` sem vulnerabilidades nos dois grafos verificados. Testes MariaDB opt-in, Linux, CI hospedada, runtime instalado e produção permanecem pendentes. Homologação formal 2/7 (28,6%), sem crédito.

## MariaDB descartável: mutex de teste separado da instância operacional — 2026-11-02 21:18 BRT

O teste `GetNextUpEpisodesBatch_SelectsEarliestUnplayedEpisodeOnMariaDb` falhou antes de iniciar seu datadir temporário porque a instância MulletaFlix operacional mantinha o mutex global por todo o ciclo de vida. O cenário usava porta dinâmica e diretório temporário exclusivos; a trava global impedia a integração sem proteger um recurso compartilhado.

Quando `requireEmbeddedProcess` é `true` (uso interno atual: testes de integração), `MariaDbProcessManager` usa um mutex de teste próprio. O caminho público de produção mantém os nomes globais existentes. A mensagem de timeout agora descreve a liberação do lock de ciclo de vida.

Windows/.NET 10 Release: a integração NextUp passou 1/1 em 9,5 s e `MariaDbStartupMutexTests` passou 4/4 em 156 ms. O ciclo start/stop/start passou 1/1 em 11 s. A primeira execução anterior à correção falhou em 45,8 s no timeout do mutex. O servidor instalado e seu listener 3306 permaneceram ativos; os testes usaram portas diferentes de 3306, datadirs temporários e limparam os datadirs. Build direcionado passou com dois avisos SA1201 preexistentes. Linux/CI hospedada e runtime instalado não foram validados. Fixture NextUp ainda tem 19 episódios; medir catálogo representativo permanece pendente. Homologação formal 2/7 (28,6%), sem crédito ou release.

# Restore — reexecução FullSystemBackup em Windows — 2026-10-27 17:07 BRT

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~FullSystemBackup"` concluiu exit 0: 142 aprovados, 16 ignorados, 0 falhas/158 em Windows/.NET 10 Release. TRX classifica 14 skips Linux (dirfd/O_NOFOLLOW/statx, ACL POSIX, symlink, bind/nested mount e identidade de mount) e 2 macOS. A evidência atualiza a reexecução anterior 141/16/0/157.

Isto confirma apenas o comportamento Windows. Os cenários Linux e macOS não foram executados; CI hospedada, runtime instalado e produção permanecem pendentes. Restore P0 e homologação formal 2/7 (28,6%) continuam abertos; sem crédito/release. Mapa técnico conferido, sem mudança estrutural.

# Scheduler de biblioteca — cleanup e descarte concorrentes — 2026-10-27 16:24 BRT

`LimitedConcurrencyLibraryScheduler` sincroniza snapshot e remoção de `_taskRunners` com `_taskLock`; a saída do worker remove a chave registrada e lê o canal pelo token vinculado cancelado no cleanup. Uma barreira mantém novos enqueues suspensos até todos os workers ociosos encerrarem. `DisposeAsync` espera enqueues já admitidos, fecha o canal, cancela e aguarda workers, e chamadas simultâneas compartilham a conclusão. Cancelamento tolera fonte já descartada. Testes cobrem backpressure, cleanup real de 65 s, enqueue durante callbacks de cleanup e descarte concorrente.

Windows/.NET 10 Release: `Jellyfin.Controller.Tests` 69/69 e regressão serial da solução 17 TRX, 5.477 aprovados, 98 `NotExecuted`, 0 falhas; em seguida, após o guard final pós-dispose, a classe do scheduler passou 4/4. Portal Node 359/359. Formatter direcionado, revisão estática independente e `git diff --check` passaram. Assemblies de Integration foram filtrados. CI hospedada, Linux, runtime instalado e produção não foram validados. Formal 2/7 (28,6%), sem crédito ou release.

Revalidação no checkout atual, 27/10/2026 16:30 BRT: `LimitedConcurrencyLibrarySchedulerTests` passou 4/4 em Windows/.NET 10 Release. Resultado focal confirma o estado pós-guard; não substitui a regressão ampla nem fecha CI/Linux/runtime/produção.

# Restore — arquivo agendado aplicado antes da inicialização — 2026-10-27 14:50 BRT

`ScheduledRestore_RestoresArchiveBeforeStartupInitialization` cobre o caminho entre `ScheduleRestoreAndRestartServer`, `IHostApplicationLifetime.StopApplication`, `Program.CaptureRestartState` (usado pelo `StartServer`) e os helpers de recovery executados pelo startup seguinte. O teste usa um archive e diretórios locais isolados, captura os campos de reinício que o entry point transfere do host e usa uma nova instância de `BackupService` para simular o serviço do host seguinte; comprova conteúdo restaurado, migração/inicialização adiadas e journal removido. A limpeza do teste restaura os valores estáticos que encontrou antes.

Validação Windows/.NET 10 Release: teste focal passou 1/1; `dotnet format whitespace --verify-no-changes` e portal Node passaram (358/358). Não executa `Program.Main`, relançamento real de processo, Linux/ACL, CI hospedada, runtime instalado ou produção. Formal 2/7 (28,6%), sem crédito/release. A estrutura técnica foi conferida e atualizada: `Program` contém o helper interno `CaptureRestartState` chamado pelo loop de `StartServer`; sem novo módulo, dependência ou API pública.

# Restore: handle aberto impede rename da raiz Windows — 2026-10-27 09:09 BRT

Adicionado teste adversarial Windows: `BackupRestoreDirectory.OpenRoot` mantém o handle sem `FileShareDelete`; `Directory.Move` não consegue renomear a raiz enquanto a autoridade está viva, e a mesma operação passa depois do descarte. `BackupRestoreDirectoryTests` Release passou 44/55 (11 ignorados, 0 falhas); os ignorados POSIX/Linux não são validação Linux.

Isto confirma apenas a proteção Windows contra detach/rename da raiz aberta. Não testa concorrência entre operações relativas, troca de descendente, ACL POSIX ou corrida real em Linux. O restore P0, CI hospedada, runtime instalado e produção continuam abertos; formal 2/7, sem crédito/release.

# Nebula — encerramento do accept loop do lease host — 2026-10-27 09:00 BRT

A regressão ampla Release encontrou `ObjectDisposedException` no `AcceptLoopAsync` quando `DisposeAsync` cancela o token e fecha `TcpListener` durante `AcceptTcpClientAsync`. O loop agora trata a exceção de socket descartado sob o mesmo guard de cancelamento já usado para `SocketException` e `OperationCanceledException`; falhas fora do shutdown continuam propagando.

Validação Windows/.NET 10 Release: `NebulaPlaybackCacheTests` 60/60; o caso `PythonCacheLeaseProtectsPlaybackCacheUntilReleased` repetiu 10/10; `Jellyfin.Server.Implementations.Tests` 1.735 aprovados, 95 ignorados, 0 falhas. Na regressão serial da solução, 16 dos 17 TRX iniciais ficaram sem falhas; substituído o resultado inicial da implementação pelo rerun completo: 5.456 aprovados, 98 `NotExecuted`, 0 falhas/5.554 resultados. O filtro excluiu métodos contendo `Integration`; os dois assemblies Integration não tiveram casos selecionados.

WSL e Docker não estão disponíveis no host. A execução local não comprova CI Linux, runtime instalado ou produção. Auditoria distinta 5.465/5.493 e homologação formal 2/7 (28,6%) permanecem inalteradas; sem release.

# Revalidação do mutex e do ciclo MariaDB — 2026-10-27 08:48 BRT

`MariaDbStartupMutexTests` + `MariaDbProcessManagerLifecycleTests` passaram 5/5 em Windows/.NET 10 Release: quatro testes do lease (liberação após continuação assíncrona, timeout limitado, cancelamento e mutex abandonado) e o teste real start-stop-start do MariaDB empacotado. O ciclo usou porta dinâmica e diretório de dados exclusivo; uma linha persistiu após o segundo start. Pós-teste: PID 31236 é o único `mysqld`, escutando no 3306 operacional; nenhum diretório temporário `MfxMariaDbLifecycle-*` restou. Não foi aberto contato com 3306 pelo teste. CI hospedada, Linux, runtime instalado e produção não validados. Formal 2/7, sem crédito/release.

# Regressão ampla do checkout .NET — 2026-10-27 07:27 BRT

`dotnet test MulletaFlix.sln --configuration Release --no-restore --no-build --filter 'FullyQualifiedName!~Integration' --verbosity quiet -m:1 --results-directory <TEMP> --logger trx` concluiu com exit 0. A agregação dos 17 TRX deu 5.450 aprovados, 98 `NotExecuted` e 0 falhas em 5.548 resultados brutos; 15 assemblies tiveram casos e os dois assemblies Integration tiveram zero selecionados pelo filtro. O projeto `Jellyfin.Server.Implementations.Tests` passou 1.729, ignorou 95 e falhou 0/1.824.

Os 98 ignorados foram classificados pelos TRX: 38 MongoDB opt-in, 32 MariaDB opt-in, 25 plataforma/SO, 2 benchmarks e 1 `/dev/shm`. A soma bruta não é o agregado distinto do portal: as execuções opt-in são registradas à parte e as contagens se sobrepõem. Mantêm-se auditoria distinta 5.465/5.493 e homologação formal 2/7 (28,6%). CI hospedada segue bloqueada por cobrança; Linux/ACL, runtime instalado e produção pendentes. Sem alteração de runtime ou release.

# Escrita concorrente de imagens e bloqueio externo da CI — 2026-10-27 07:11 BRT

Adicionada a regressão `SaveItems_ConcurrentWithDeleteItemForSameItem_CompletesWithoutOrphanedImages`, cobrindo substituição de imagens e exclusão concorrentes para o mesmo item. O teste passou 1/1 em MariaDB 11.4.4 descartável; a classe completa `ServerPersistenceMariaDbTests` passou 26, ignorou 1 caso dependente de reinício externo e teve 0 falhas/27 quando executada com `max-connections=500`. A primeira execução com o limite padrão do datadir temporário teve 7 falhas por `Too many connections`; não é tratada como resultado válido da aplicação.

A API de anotações do GitHub confirmou que o run CI `39608614450` não iniciou jobs porque a conta está bloqueada por uma questão de cobrança (`The job was not started because your account is locked due to a billing issue.`). Os 13 jobs ficaram sem runner/steps; o preflight da release falhou e Linux/publicação foram skipped. A correção depende da conta GitHub e está fora do repositório. Sem alteração de runtime: homologação formal 2/7, auditoria 5.465/5.493, nenhuma release. CI hospedada, Linux/ACL, runtime instalado e produção permanecem pendentes.

# Regressão permanente do lifecycle MariaDB embarcado — 2026-10-26 21:52 BRT

O teste MariaDbProcessManagerLifecycleTests.StartStopStart_UsesTheSameIsolatedDataDirectory executa o gerente real com runtime MariaDB 11.4.4, porta livre e DataPath exclusivo em Temp; start, stop, segundo start e stop passaram 1/1 localmente em Windows/.NET 10 Release. O teste limpa o processo e o datadir próprios e não acessa o listener operacional.

A CI Windows prepara os mesmos binários ao lado do assembly e exige exatamente um resultado Passed no TRX. Contrato de CI 16/16, suíte Jellyfin.Server.Tests 86 aprovados, 1 ignorado sem runtime stageado e 0 falhas/87; contrato do portal 312/312. O caso ignorado na suíte ampla foi executado separadamente com runtime e passou. A hospedada ainda não executou.

Sem alteração de runtime de produção nem release. Auditoria agregada continua 5.465/5.493; homologação formal 2/7 (28,6%). Permanecem pendentes CI hospedada, runtime instalado, schema sob carga e produção. Mapa técnico conferido: foi incluído um overload interno de guarda na classe `MariaDbProcessManager`, além do teste e da CI; não há nova API pública, dependência ou fluxo normal de runtime.

## Boot integral após replay de restore interrompido — 2026-10-27 06:32 BRT

`StartupBackupRecoveryTests.CreateHost_ReplaysInterruptedRestoreBeforeMigrationsAndCompletesStartup` exercita o gate do `Program` com `BackupService` real: prepara archive/journal e instalação parcial, confirma conteúdo restaurado e journal removido antes da primeira migração e então inicia o host com migrations concretas, `InitializeServices`, startup tasks e requisição HTTP a `/System/Info/Public`. Windows/.NET 10 Release passou 1/1 contra MariaDB 11.4.4 descartável (PID 37464, porta 49372); o processo terminou graciosamente e a porta fechou. Build passou com zero avisos/erros, MariaDbTestConnectionTests passou 10/10 e contrato CI MariaDB 4/4. O harness agora exige confirmação separada de que a instância é descartável e a CI só configura o opt-in dentro do job com container MariaDB. Um primeiro run falhou por `mysqldump` ausente no PATH; a repetição passou com os binários MariaDB no PATH. Dois diretórios descartáveis permaneceram em `%TEMP%` após bloqueio da remoção recursiva. Linux/ACL, CI hospedada, runtime instalado e produção não foram verificados; homologação formal segue 2/7, auditoria 5.465/5.493, sem crédito/release.
# Reenvio idêntico do deadlock; nenhuma instância de servidor ativa — 2026-10-26 20:50 BRT

O arquivo recebido tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao log de 23/10. Ele registra SQL `DELETE BaseItemImageInfos` repetido para o mesmo ItemId, deadlock MariaDB 1213 e retries do `SaveImagesAsync`; o trecho não contém o desfecho. O `Status Code 200` no cabeçalho é resposta HTTP e não prova commit no banco. O aviso de heartbeat Kestrel ocorre na mesma janela, mas não prova causalidade.

Inspeção read-only de serviços/processos não encontrou processo MulletaFlix/Jellyfin ativo no host; somente o proxy Nginx estava ativo. Nenhum banco foi acessado e nenhum serviço foi iniciado/reiniciado. Sem PID/assembly/version no log, a falha histórica não pode ser atribuída ao assembly instalado 12.1.35 ou ao checkout. O arquivo DLL instalado inspecionado anteriormente diverge da fonte local, mas não identifica o emissor do log. Não fazer release/downgrade com o checkout atual: está 8 commits atrás de `origin/main`, com 224 caminhos locais modificados/não rastreados. Auditoria agregada 5.465/5.493; homologação formal 2/7 (28,6%), sem crédito. CI hospedada, runtime ativo e produção pendentes.

# Deadlock de imagens: runtime instalado diverge do checkout — 2026-10-26 13:43 BRT
- [x] Inspeção somente leitura: DLL instalada MulletaFlix.Server.Implementations 12.1.35, SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`; marcador `ItemPersistenceDatabaseLock` ausente. Checkout principal declara 12.1.32 e inclui o tipo/lock.
- [x] Não havia listener 8096 nem processo MulletaFlix/Jellyfin ativo nesta verificação.
- [x] Portal e contrato de teste atualizados. Formal segue 2/7 (28,6%), sem crédito.
- [ ] A ausência de marcador não prova o comportamento completo do binário. Log antigo não tem PID/assembly/versão e não pode ser correlacionado.
- [ ] Checkout 12.1.32 é inferior ao instalado 12.1.35; não publicar downgrade. Exige candidato superior validado e runtime de produção para confirmar.

# Regressão ampla do servidor em Windows/.NET 10 — 2026-10-26 13:40 BRT
- [x] `MulletaFlix.sln` Release, sem build/restore, filtro `FullyQualifiedName!~Integration`: 17 TRX, 5.378 Passed, 96 NotExecuted, 0 Failed (5.474 resultados), exit 0.
- [x] Classificação dos 96 skips por mensagem TRX: 37 MongoDB opt-in, 32 MariaDB opt-in, 24 plataforma/SO, 2 benchmarks e 1 dependente de `/dev/shm`.
- [x] Portal atualizado com a classificação; homologação formal preservada em 2/7 (28,6%), sem crédito.
- [ ] Os 96 NotExecuted não são aprovações; testes Integration foram filtrados. CI Linux hospedada, runtime instalado e produção não foram validados nesta rodada.
- [ ] Sem alteração de runtime; não criar release com esta evidência isolada.

# MariaDB: mutex de startup respeita ownership de thread — 2026-10-26 20:05 BRT

`StartMariaDbAsync` mantinha um `Mutex` adquirido por uma thread e chamava `ReleaseMutex` após awaits em `StopMariaDbAsync`, onde a continuação poderia executar em outra thread. O lifecycle agora usa uma thread proprietária dedicada: ela cria, adquire, mantém e libera o mutex no mesmo thread. A aquisição tem timeout também no mutex fallback, aceita cancelamento e recupera mutex abandonado. O mutex de sistema foi preservado para manter exclusão entre processos e recuperação após crash.

Validação Windows/.NET 10 Release: quatro testes direcionados passaram (inclui descarte concorrente, continuação assíncrona, timeout, cancelamento e mutex abandonado); `Jellyfin.Server.Tests` passou 86/86; contrato local do portal 301/301. `dotnet format whitespace` direcionado nos projetos de servidor e testes e `git diff --check` passaram. Isso valida a fonte e os testes locais, não o runtime instalado ou produção. CI hospedada não foi verificada. Formal permanece 2/7 (28,6%) e nenhuma release foi publicada.

# Auditoria Tecnica do Backend MulletaFlix

# Smoke do lifecycle real do MariaDB embarcado — 2026-10-26 21:45 BRT

Um smoke temporário invocou `MariaDbProcessManager.StartMariaDbAsync` e
`StopMariaDbAsync` usando os binários MariaDB locais, uma porta efêmera e um
`DataPath` exclusivo em Temp. O mesmo datadir passou por start a frio, stop
gracioso, segundo start e novo stop (1/1). O processo terminou e os dados
temporários foram removidos/verificados. A suíte permanente
`MariaDbStartupMutexTests` passou 4/4 em Windows/.NET 10 Release.

O teste temporário foi removido após a execução; nenhuma fonte de produção foi
alterada. A porta operacional 3306 e o banco de produção não foram acessados.
Isto não valida o runtime instalado completo, CI hospedada, upgrade de schema
sob carga ou produção. Homologação formal permanece 2/7 (28,6%), sem crédito
nem release. Mapa técnico conferido; estrutura inalterada.

## Contrato dos testes privilegiados de restore Linux — 2026-10-26 21:40 BRT

O contrato da CI agora exige os dez cenários de fronteira do restore no passo
Linux executado com `sudo`, confere que cada cenário está no filtro e que a
validação TRX rejeita casos ausentes ou com resultado diferente de `Passed`.
Contrato focal passou 2/2; conjunto de contratos CI 15/15; portal 310/310.
WSL/Docker não estão disponíveis neste host. Isto protege a configuração, mas
não é execução Linux; restore P0, CI hospedada, runtime e produção seguem sem
evidência nesta sessão. Formal 2/7, sem crédito nem release.

## Contrato CI da serialização dos testes do servidor — 2026-10-26 21:38 BRT

O job de contratos do workflow não executava `ci-test-runner-serialization.test.mjs`,
apesar do arquivo verificar que a regressão da solução usa `-m:1`. O contrato foi
adicionado à lista do job. Os seis contratos envolvidos passaram 14/14 no Node
local. Isso valida a definição do workflow, não um run hospedado. Os gates Linux
privilegiados de restore, CI hospedada, runtime instalado e produção seguem sem
evidência nesta sessão; homologação formal 2/7 (28,6%), sem crédito ou release.

## Refresh localizado para imagens remotas sem idioma — 2026-10-26 21:35 BRT

O refresh identifica como elegível para correção de idioma a imagem que mantém
`ProviderName`, mas tem `Language` nulo/vazio, e consulta a língua preferida do
item/servidor. A regra preserva arte sem provedor. Testes locais em Windows/.NET
10 Release: 4 casos direcionados e 47 testes de refresh aprovados. Imagens
legadas sem qualquer proveniência seguem sem reparo automático; TMDb real,
produção, CI hospedada e runtime instalado continuam pendentes. Homologação
formal 2/7 (28,6%); sem crédito nem release.

# Regressão serial da solução e crash do testhost — 2026-10-26 20:33 BRT

- [x] `MulletaFlix.sln` Release, sem restore/build, filtro `FullyQualifiedName!~Integration`, `-m:1`: 15 assemblies com casos, 5.401 aprovados, 97 ignorados, 0 falhas/5.498; exit 0. Dois assemblies Integration não tiveram casos selecionados.
- [x] API passou isolada 792/795 (3 ignorados). Execução anterior sem `-m:1` encerrou o testhost com CLR `0x80131506`/`-1073741819`; causa ainda desconhecida.
- [x] Classificação dos 97 ignorados no TRX: 38 requerem Mongo opt-in, 32 MariaDB opt-in, 25 plataforma/SO e 2 benchmarks. A CI configura jobs dedicados, mas o último run público não executou steps.
- [x] CI unit-test agora usa `-m:1`; contrato específico passou 1/1. Contrato do portal 304/304.
- [ ] CI hospedada não iniciou jobs; runtime instalado 12.1.35 difere do checkout 12.1.32 e produção não foi validada.
- Auditoria agregada mantida em 5.465/5.493; homologação formal 2/7 (28,6%), sem crédito. Nenhum banco operacional/runtime alterado; sem release.

# Nebula: aliases físicos ainda geram identidades distintas — 2026-10-26 19:31 BRT

Auditoria somente de fonte e testes, sem MongoDB: `NebulaMongoContext.NormalizeQueueIdentity` aplica `Path.GetFullPath` e normalização lexical; `Tools/NebulaPython/main.py::queue_identity` usa `abspath`/`normcase`. Nenhum resolve symlink/junction antes de persistir ou consultar `queue_identity`. Os testes cobrem caixa legada, backfill, colisão lexical e inicializadores concorrentes, mas não aliases físicos. Portanto dois caminhos para o mesmo arquivo podem criar registros distintos, inclusive entre os produtores C# e Python.

Não trocar a identidade existente por identidade física sem definir comportamento para caminhos inexistentes, UNC e caminhos cujo destino muda; o índice unique sparse pode rejeitar aliases que colidam e abortar o startup. Próximo gate: especificar semântica comum C#/Python, testar alias para arquivo existente em filesystem temporário e executar inventário somente leitura de colisões antes de planejar qualquer migração. Nenhum registro foi lido ou alterado em Mongo operacional. Sem mudança de código, módulo ou API; formal 2/7 (28,6%), sem crédito/release.

# Instaladores de plugins: verificação e extração pelo mesmo handle — 2026-10-26 13:08 BRT

`InstallationManager` e o bootstrap de `PluginManager` agora leem o checksum legado e entregam o mesmo `FileStream` ao `BoundedZipArchiveExtractor`. Isso remove a janela de troca do `package.zip` por pathname entre a verificação e a extração. Nenhum módulo, API ou dependência foi acrescentado.

Windows/.NET 10 Release: `InstallationManagerTests` + `BootstrapPluginCatalogTests` + `BoundedZipArchiveExtractorTests` 31/31; contrato do portal 279/279. O checksum MD5 é requisito de compatibilidade do manifesto legado e não prova autenticidade. Não houve download/instalação de pacote real, Linux/CI, runtime instalado ou produção; formal segue 2/7 (28,6%), sem crédito/release.

# Updates do servidor: validação SHA-256 e handle único — 2026-10-26 13:04 BRT

Os fluxos de atualização automática (`ServerUpdateTask`) e do Centro de Atualizações (`UpdateInfoController`) exigem digest SHA-256 válido dos metadados GitHub ou do manifesto customizado. Cada fluxo verifica e extrai pelo mesmo handle de arquivo, evitando que uma substituição do pathname `package.zip` entre as duas operações troque os bytes consumidos. O Centro de Atualizações só reaproveita estado em memória quando versão e digest correspondem ao metadata atual. O DTO carrega o digest; estado persistido inclui `VerifiedChecksum`, e estados anteriores sem digest equivalente não são restaurados como `ReadyToApply`.

Validação Windows/.NET 10 Release: `BoundedZipArchiveExtractorTests` + `ServerUpdateTaskTests` 20/20; `UpdateInfoControllerTests` 8/8; contrato do portal 278/278; `dotnet format whitespace` direcionado passou. O metadata público de `v12.1.35` foi consultado e todos os três assets expõem `sha256:`; isso não substitui comparação do pacote real. Nenhuma assinatura do publicador foi verificada: o digest é confiado via HTTPS. Não houve instalação HTTP integrada, Linux/CI do checkout, runtime instalado ou produção; homologação formal segue 2/7 (28,6%), sem crédito/release.

## Persistência de imagens: regressão MariaDB reexecutada — 2026-10-26 12:43 BRT

O anexo recebido tem o mesmo SHA-256 dos registros anteriores e repete os deadlocks 1213 em `DELETE BaseItemImageInfos` para um mesmo item. Contra MariaDB 11.4.13 descartável, `ServerPersistenceMariaDbTests` passou 19/19; `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 49/49 em Windows/.NET 10 Release. A instância efêmera e a conta temporária foram encerradas/removidas. Isso valida a fonte local, não identifica PID/assembly nem demonstra qual versão produziu o log. Nenhuma alteração de runtime/release; formal segue 2/7, sem crédito. Linux, CI remota, runtime instalado e produção pendentes.

## Nebula: backfill idempotente de identidades ausentes — 2026-10-26 09:38 BRT

Na inicialização, C# (`NebulaMongoContext.EnsureIndexesAsync`) e Python (`setup_database_indexes`) agora identificam arquivos legados sem `queue_identity` e a calculam a partir do `local_path` não branco. A leitura/escrita é limitada a lotes de 500 e cada update exige que o campo ainda esteja ausente, permitindo retomada. Caminhos vazios ou de espaços são ignorados. A chave única sparse continua protegendo inserções; colisões de caminhos que normalizam para a mesma chave falham fechadas e nenhum registro é removido.

Validação: `NebulaQueueConcurrencyMongoTests` Release passou 29/29 em MongoDB 8.3.7 descartável; testes NebulaPython completos passaram 723, com 2 skips POSIX; contrato portal 270/270; formatador C# focado e `git diff --check` passaram. Instância temporária encerrada. A execução não mediu startup no volume real nem acessou Mongo operacional. Duplicatas legadas normalizadas podem impedir inicialização após atualização e exigem inventário/backup antes da instalação. Inicializações C#/Python concorrentes, CI Linux, runtime e produção seguem pendentes. Sem release; homologação formal permanece 2/7 (28,6%), sem crédito.

## Nebula: inicializadores C# e Python concorrentes — 2026-10-26 09:53 BRT

Adicionado teste de integração que semeia um arquivo legado sem `queue_identity` e inicia `NebulaMongoContext.EnsureIndexesAsync` e `setup_database_indexes` simultaneamente no mesmo banco aleatório. O teste valida saída do processo Python, identidade calculada e unicidade do registro. O job Mongo da CI instala Python 3.10 e as dependências runtime bloqueadas para executar essa cobertura; o caso continua opt-in local, exigindo somente Mongo descartável em `127.0.0.1:27099` e `NEBULA_TEST_PYTHON`.

Validação local Windows/.NET 10 Release: o teste cruzado passou 1/1 com MongoDB 8.3.7 descartável; o processo Mongo foi encerrado e a porta fechou. A pasta temporária de dados permaneceu no Temp porque o shell recusou a remoção recursiva. Compilação do projeto de teste passou. Não houve execução do workflow remoto, acesso ao Mongo operacional, medição de startup nem instalação/release. Homologação formal permanece 2/7 (28,6%), sem crédito.

## Lock de imagens: revalidação MariaDB isolada — 2026-10-26 08:57 BRT

Na worktree local `codex/server-image-deadlock-next` (HEAD `d51f4f38`, tag `v12.1.35`), a classe `ServerPersistenceMariaDbTests` Release/.NET 10 passou 15/15 contra MariaDB 11.4.13 descartável em loopback `127.0.0.1:33099`. A classe cobre o advisory lock entre duas sessões e `SaveImagesAsync` aguardando outra sessão; o teste `DeleteItem_ConcurrentWithImageRefreshes_CompletesWithoutDeadlock` passou 10/10 em execuções adicionais. `ItemPersistenceServiceTests` + `ItemPersistenceDatabaseLockTests` passou 43/43. Processo encerrou normalmente; processo MariaDB ausente e porta fechada após o teste. O cenário prova comportamento local, não identifica o binário/processo do log de produção. Worktree continua com mudanças staged/unstaged e alterações não relacionadas; sem commit/release/instalação. Formal 2/7, sem crédito.

## Lock de imagens: regressão unitária na base instalada, integração pendente — 2026-10-26 08:47 BRT

Na worktree local `codex/server-image-deadlock-next` (HEAD `d51f4f38`, tag `v12.1.35`), o lock ausente no tag está presente como mudança não commitada. Build Release/.NET 10 gerou DLL 12.1.35 com marcador estático; `ItemPersistenceServiceTests` + `ItemPersistenceDatabaseLockTests` passou 43/43. Isso valida processo de unidade local, não sincronização entre sessões MariaDB. O teste `DeleteItem_ConcurrentWithImageRefreshes_CompletesWithoutDeadlock` existente modela 12 refreshes e delete, mas não rodou: 33099 sem listener, MariaDB/container runtime indisponível. Worktree tem mudanças sobrepostas/staged e temas alheios; sem integração/publicação. O log segue sem PID/assembly para correlação. Homologação 2/7, sem crédito.

## Deadlock de imagens: artefato instalado e checkout local não correspondem — 2026-10-26 08:42 BRT

Leitura estática local: DLL instalada em `C:\Program Files\MulletaFlix\Server` reporta 12.1.35 e não contém o texto de metadados `ItemPersistenceDatabaseLock`; DLL Release do checkout reporta 12.1.32, hash distinto e contém o marcador. A busca de marcador é evidência binária, não prova de qual assembly estava carregado no instante do log. O trecho MariaDB não inclui data completa/PID/assembly. O teste existente `DeleteItem_ConcurrentWithImageRefreshes_CompletesWithoutDeadlock` usa 12 `SaveImagesAsync` para um ItemId e deleção concorrente, mas não foi reexecutado: listener 33099 e runtime MariaDB descartável indisponíveis nesta sessão. Como a build local é numericamente inferior, não instalar nem anunciar como release; reconciliar fonte/tag/versão e obter evidência correlacionada do processo antes de atribuir a causa. Homologação formal 2/7, sem crédito.

## Nebula: falha do índice único não pode ser ignorada — 2026-10-26 08:34 BRT

`setup_database_indexes` suprimia também a falha ao criar o índice único sparse de `queue_identity`. Se valores duplicados já existirem, MongoDB recusa o índice; continuar a inicialização deixa a deduplicação sem sua garantia. O tratamento foi removido para esse índice: a exceção chega ao `main()`, que encerra os bots e aborta antes de subir o FTP. Um teste exige propagação de `DuplicateKeyError`. `py_compile` passou em Python 3.10; a suíte completa na venv isolada oficial passou 721 testes, com 2 skips POSIX. Estado Mongo real, runtime instalado e produção não foram verificados. Não remover registros automaticamente; inventariar e resolver duplicatas com backup e procedimento controlado antes de instalar a correção. Homologação formal 2/7, sem crédito/release.

## Nebula: compatibilidade de caixa da identidade da fila no Windows — 2026-10-26 09:20 BRT

Os produtores C# e Python agora persistem a identidade lexical em maiúsculas no Windows e consultam também a grafia minúscula legada antes de inserir. Teste do scanner confirma a query Python; teste Mongo C# semeia uma identidade legada e valida que a segunda inserção não cria novo documento. `NebulaQueueConcurrencyMongoTests`: 27/27 sem skips, MongoDB 8.3.7 descartável; NebulaPython completo: 722 aprovados, 2 skips POSIX. A resolução de symlink/junction permanece fora deste ajuste; não houve inventário nem mesclagem no Mongo operacional. T2.2 continua parcial.

## Capas localizadas: regressão de seleção e MariaDB pendente — 2026-10-26 08:12 BRT

`ProviderManagerImageProviderTests|TmdbMovieImageProviderFlowTests|ItemImageProviderTests`
passou 65/65 em Windows/.NET 10 Release, sem skips/falhas e usando fixture local,
sem chamar TMDb externo. Isso revalida a seleção por idioma, fallback inglês e
fluxo de refresh já implementados; não comprova atualização da arte persistida
no servidor instalado nem renderização de *Invasão Zumbi 2*. O teste concorrente
MariaDB de lote entre stripes segue presente, mas não foi reexecutado porque este
host não tem binário MariaDB, runtime de container ou listener de teste. Formal
2/7 (28,6%), sem crédito; runtime, produção e integração MariaDB atual pendentes.

## Nebula: reconciliação da nota de lease/heartbeat — 2026-10-26 07:31 BRT

Corrigida a nota antiga de T2.1 que descrevia lease/fencing como ausentes. O código e a seção T2.2 já registram lease por tentativa, heartbeat, renovação ao persistir progresso, recuperação por expiração e fencing por dono/token, com compatibilidade para documentos legados sem lease. Em 26/10, a fonte passou a distinguir `retry_wait` de falha terminal e honrar `retry_after`/`next_retry_at` nos fluxos C# e Python, incluindo leitura de dados legados. A reconciliação local de ack Telegram também está implementada: busca UUID/parte/SHA-256, retoma sem reenvio quando encontra a parte e adia o retry se o histórico é inconclusivo. Testes locais prévios cobrem esses caminhos; não provam integração real ou exactly-once. A identidade de caminho sob aliases e a validação real de Telegram/Mongo/runtime/produção permanecem pendentes.

## Trickplay: admissão em modo NonBlocking sob saturação — 2026-10-26 07:24 BRT

`TrickplayProviderTests` Release passou 3/3 em Windows/.NET 10. O teste de saturação observa oito refreshes em background e confirma que a chamada seguinte permanece aguardando até um slot ficar livre. Isso limita o trabalho destacado, mas pode propagar backpressure à varredura; não prova atraso em biblioteca real. T6.2 e a medição representativa seguem pendentes. Sem alteração de runtime, crédito formal ou release.

## Implementations com bancos descartáveis — 2026-10-26 07:02 BRT

Na branch local baseada em `d51f4f38`, a regressão Release de
`Jellyfin.Server.Implementations.Tests` passou 1.516/1.581, com 65 ignorados e
zero falhas. O TRX classifica os skips: 31 MariaDB opt-in, 25 MongoDB opt-in,
5 Unix-only, 2 benchmarks, 1 dependente de `/dev/shm` e 1 de bits ACL Unix não
aplicáveis ao Windows.

Os opt-ins MariaDB focais passaram: `UserManagerNormalizedUsernameTests` e
`BillingDatabaseTests`, 30/31 (um skip exige `lower_case_table_names=0`);
`ServerPersistenceMariaDbTests`, 7/7; `ItemPersistenceServiceTests`, 24/24.
Os 25 casos Mongo de topologia, filas, recuperação de playback e backup/restore
passaram 25/25. MariaDB 11.4.13 e MongoDB 8.3.7 usaram portas descartáveis
33099/27099, bind loopback e datadirs isolados; processos encerrados e portas
fechadas. MongoDB operacional 27017 não foi acessado.

Esta evidência valida a branch de trabalho, que ainda contém alterações locais
pendentes; não identifica o binário instalado nem substitui CI Linux,
homologação runtime/produção ou aceite formal. O gate permanece 2/7 (28,6%),
sem crédito/release.

## Regressão ampla .NET sem integrações — 2026-10-25 21:07 BRT

Após build Release em Windows/.NET 10, `MulletaFlix.sln` foi repetida sem build com `FullyQualifiedName!~Integration`: 15 projetos, 5.291 aprovados, 80 ignorados, 0 falhas/5.371 resultados; ambos os comandos encerraram com exit 0. O filtro exclui integrações; os ignorados incluem casos opt-in/plataforma e não são aprovações. Portanto, não prova MariaDB/MongoDB, Linux/macOS, CI hospedada, runtime instalado ou produção. O portal registra a rodada como evento separado e conserva a regressão mais abrangente anterior com MariaDB descartável (5.505/5.554, 49 ignorados). Homologação formal permanece 2/7 (28,6%), sem crédito ou release. Mapa técnico conferido: nenhuma alteração em módulos, APIs, dependências ou fluxo de runtime.

## Reauditoria do lifecycle de restore por handles — 2026-10-25 18:15 BRT

Revisão atual da fonte e auditoria independente não encontraram mutação pathname-based restante dentro de `FileSystemRestoreTransaction`; staging, instalação, rollback, replay e cleanup usam operações relativas a diretórios abertos. Checks de pathname escolhem uma âncora antes de `OpenRoot`, que percorre componentes sem seguir links. `FullSystemBackup` Release Windows/.NET 10 passou 108, ignorou 11 e falhou 0/119. Isso atualiza o estado da fonte em relação às descrições históricas que precedem a migração por handles.

O restore P0 não está fechado: Linux/syscalls, ACL/filesystem efetivos, CI hospedada, runtime/produção e teste determinístico de corrida permanecem sem evidência. Sem crédito formal (2/7); sem release, instalação, commit, push ou publicação remota. Mapa técnico conferido; fluxo existente sem classe, API, dependência ou módulo novo.

## Nebula: descoberta de raízes e status real da fila — 2026-10-25 18:04 BRT

O contador emitido pelo worker era do título atual, mas o manager publicava `QueueCount` e “Fila de download preparada”, apesar do downloader processar os candidatos sequencialmente. O status agora mostra candidatos elegíveis do título e mantém a contagem de fila em zero até haver fila real. Falha ao enumerar uma raiz emite `[STRM-SCAN-ROOT-ERROR]` e o manager a apresenta como erro, não como título vazio. O texto de `MONGO-READY` também foi corrigido: `refresh_cache` prepara cache de duplicidade em memória; não prova criação de índices físicos Mongo nem scan completo.

Verificações: `NebulaDownloaderStartupTests` 31/31; `Jellyfin.Server.Implementations.Tests` 1.628 aprovados, 77 ignorados, 0 falhas/1.705; suíte completa NebulaPython 702/702; revisão adversarial Cavecrew sem achados; `dotnet format style --verify-no-changes` passou nos dois arquivos C# tocados. Ruff não estava instalado na venv e nenhuma dependência foi adicionada. O MariaDB não foi necessário para esta alteração. Validação de runtime instalado, raiz inacessível real, CI hospedada e produção continua pendente. Homologação formal continua 2/7 (28,6%), sem crédito; nenhuma release, instalação ou publicação remota. Mapa técnico conferido; fluxo de status alterado entre worker Python e `NebulaFtpManager`, sem módulo, API ou dependência nova.

## Reexecução após build e log de deadlock repetido — 2026-10-25 17:48 BRT

O anexo tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico ao incidente já registrado. Traz 12 comandos `DELETE BaseItemImageInfos`, 11 deadlocks InnoDB 1213, 12 avisos de retry e um heartbeat Kestrel de 2,35 s. HTTP 200 antecedente não demonstra commit; não há PID/assembly, resultado final dos retries ou estado InnoDB.

Após o build Release anterior, a regressão local `MulletaFlix.sln` com `--no-build --no-restore --filter 'FullyQualifiedName!~LiveIntegrationTests'` terminou com exit 0: 5.305 aprovados, 259 ignorados, 0 falhas/5.564 resultados em 17 assemblies. O ambiente de teste MariaDB descartável não está configurado; integrações opt-in permanecem ignoradas. A fonte local usa stripe intra-processo e advisory `GET_LOCK` em `SaveImagesAsync`, mas a regressão do checkout não identifica qual binário produziu o log nem comprova runtime instalado/produção. Homologação formal permanece 2/7 (28,6%), sem crédito; sem banco operacional, instalação, release ou publicação remota. Mapa técnico conferido; topologia e contratos não mudaram.

## Regressão integral Release e Newtonsoft seguro — 2026-10-25 09:00 BRT

A regressão Release de `MulletaFlix.sln` falhou inicialmente em `InsecureDeserializationAuditTests`: o guard detectava qualquer `JsonConvert.DeserializeObject` e não reconhecia as configurações seguras já passadas pelo novo snapshot do IntroSkipper. A desserialização é limitada a `BackupSnapshot` e agora configura explicitamente `TypeNameHandling.None`; o teste permite somente esse callsite e verifica que a configuração explícita permaneça, além de continuar rejeitando outros callsites não auditados e `TypeNameHandling.All/Auto`.

Validação: os dois testes focais `InsecureDeserializationAuditTests` passaram 2/2. Regressão completa em Windows/.NET 10 Release, `--no-build --no-restore`, filtro `FullyQualifiedName!~LiveIntegrationTests`: 5.298 aprovados, 256 `NotExecuted`, 0 falhas (5.554 resultados, 17 assemblies), exit code 0. TRX por assembly preservado nos diretórios `TestResults` locais. Os ignorados incluem integração MariaDB/MongoDB opt-in e casos Linux/macOS; provedores live foram excluídos. A homologação formal permanece 2/7 (28,6%), sem crédito. Sem validação de runtime instalado, produção, CI remota, instalação, release ou publicação remota. Mapa técnico conferido; sem alteração de arquitetura, módulos, APIs ou dependências.

## CI para scripts de empacotamento Linux — 2026-10-23

- `ci.yml` agora inclui job `linux-packaging-script-tests` em `ubuntu-latest`, com permissões mínimas (`contents: read`), checkout sem persistir credenciais, validação `bash -n` do instalador/atualizador e execução das três fixtures Bash já existentes.
- O contrato Node valida separadamente que cada fixture é executada na etapa correspondente, não apenas mencionada na sintaxe. Revisão adversarial levou à restrição do token e ao reforço do contrato.
- Contratos CI locais: 8/8. Em Git Bash no Windows, `bash -n` e as três fixtures passaram; isso não substitui runner Ubuntu. CI hospedada/remota ainda não foi observada. Sem mudança de runtime, API, módulo ou dependência; sem instalação, produção ou release. Homologação formal permanece 2/7 (28,6%), sem crédito por esta evidência local.
- Mapa técnico e estrutura conferidos em 2026-10-23: alteração apenas no fluxo de CI/infraestrutura de testes, sem mudança da topologia de runtime. Ver registro da sessão do vault `2026-10-23-MulletaFlix-linux-packaging-ci-coverage`.

## Sync M3U preserva mídias após falha transitória de validação — 2026-10-23

- Auditoria encontrou risco no sync manual em modo `download`: entradas filtradas como offline antes da geração dos arquivos eram tratadas como removidas da M3U, permitindo apagar mídia já baixada e salvar manifesto parcial. Uma playlist vazia com manifesto anterior também podia disparar remoção integral.
- Correção local: o plano de limpeza distingue validação incompleta/lista vazia suspeita de lista válida; preserva o manifesto anterior e não remove entradas nesse ciclo. Uma execução completa posterior volta a remover somente os itens ausentes. O caminho `force` também preserva o manifesto se a validação estiver incompleta.
- Testes adicionados cobrem preservação em filtro parcial, playlist vazia com manifesto anterior, limpeza quando a validação é completa e proteção com `force`. Suíte `Jellyfin.Providers.Tests` Release: 862 aprovados, 13 ignorados, 0 falhas. Regressão da solução Release pós-correção saiu 0 com Integration filtrado; agregado truncado, sem totais consolidados. Sync com origem real, instalação e produção não executados.
- Auditoria paralela read-only também encontrou riscos P1 em claim de upload Nebula preso em `staging` após exceção e em TOCTOU de restore. São itens separados, permanecem pendentes e não foram alterados nesta correção.
- Mapa técnico conferido em 2026-10-23: helper interno `MidiaStorageOnlineManifestCleanup` novo no módulo de sync; não houve API pública, dependência ou mudança de topologia. Ficha/mapa do vault atualizados.

## Username como componente de diretório de perfil — 2026-10-23

- Confirmado: `UserManager` permitia os nomes exatos `.` e `..`; `ImageController` e `AvatarService` derivavam pastas sob `UserConfigurationDirectoryPath` diretamente do username. Corrigido no working tree: validação central recusa os dois nomes e `FileSystemHelper.GetUserConfigurationDirectoryPath` falha fechado para nome não seguro/fora da raiz; consumidores nativos e GetAvatar migrados para o helper.
- Testes adicionados para traversal, caminhos absolutos, separadores, link estático, alias de ponto final do Windows e username normal. Release Windows/.NET 10: `FileSystemHelperPathSafetyTests` + `AvatarControllerUserAvatarAuthorizationTests` 44/44; `UserManagerTests` 16/16; `git diff --check` passou. Linux, runtime instalado e produção não executados. TOCTOU contra alteração concorrente de ancestrais permanece separado e não é considerado resolvido.
- Estrutura do mapa técnico conferida em 2026-10-23: mudança reutiliza `FileSystemHelper` e altera seus consumidores em `ImageController` e `AvatarService`; atualizar o mapa para documentar o novo helper após validação.

## Regressão de teste — catálogo padrão de classificações — 2026-10-23

- A regressão da solução identificou que `GetParentalRatings_Default_Success` esperava os 56 aliases do catálogo US, mas configurava apenas a cultura de interface; `ServerConfiguration.MetadataCountryCode` tem padrão explícito `BR`. A implementação corretamente retornava os 42 aliases brasileiros, incluindo `Livre` com score 0.
- Ajuste limitado aos testes: o caso de 56 entradas configura `MetadataCountryCode = "US"`; um teste adicional confirma país BR padrão, 42 aliases e a classificação `Livre`. Não houve alteração da lógica/runtime de produção.
- `LocalizationManagerTests` Release passou 67/67. Regressão ampla `dotnet test MulletaFlix.sln --configuration Release --no-restore --filter "FullyQualifiedName!~Integration"` passou 5.173, 69 ignorados, 0 falhas (5.242 resultados). Os ignorados não são aprovações; integrações foram excluídas pelo filtro. Sem CI remota, Linux, MariaDB, instalação ou produção.
- Homologação formal continua 1/7 (14,3%). Sem release: mudança somente em teste, TOCTOU/ACL ainda P0 e checkout continua sujo/8 commits atrás de `origin/main`.

## Addendum — auditoria adversarial da janela TOCTOU no restore — 2026-10-22

- O replay/journal foi adicionado no working tree desde o snapshot base deste roadmap; consultar `FullSystemBackup/BackupRestoreRecoveryJournal.cs`, `BackupService.RecoverInterruptedRestoreAsync` e `Program.StartApp`. O teste de regressão filtrado `FullSystemBackup` recompilou em Release e passou 50/50 nesta sessão.
- O restore ainda usa caminhos absolutos para criar diretórios, staging, backup e substituição após checar reparse points. Link estático é recusado, mas troca concorrente não foi coberta; a lacuna TOCTOU técnica permanece aberta. O endpoint HTTP exige papel Administrator e o caminho fica no diretório de backup; o instalador declara ACL restritiva, sem validação da ACL efetiva instalada nesta sessão. Não foi demonstrado vetor remoto; exposição local é condicional às permissões reais e controle dos diretórios ancestrais.
- Revalidação adversarial em 2026-10-22: teste novo cria symlink estático no `.tmp` determinístico de replay e comprova que o alvo externo fica intacto; teste focal 1/1 e `FullSystemBackup` Release 56/56. Isto não simula troca concorrente e não fecha a janela TOCTOU.
- Pesquisa das APIs candidatas recomenda estudo multiplataforma de `openat2`/dirfd no Linux e uma alternativa Windows com suporte/compatibilidade demonstrável. `NtCreateFile` consta em `winternl.h`, mas a Microsoft classifica APIs de `winternl.h` como internas e sujeitas a mudanças; não foi implementada.
- Pendentes: teste adversarial determinístico sem escrita externa; matriz Windows/Linux; kill/restart real com MariaDB e storage descartáveis; FKs pré-validadas; consistência temporal; integração; homologação formal. Nenhuma produção ou MariaDB operacional tocada; sem release.
- Revalidação em 2026-10-22: `ServiceDataAcl.Tests.ps1` passou em fixture descartável, cobrindo grants amplos herdados/explicitos, herança dos filhos e sentinel fora da raiz; isso não prova ACL de instalação. `ProtectedApiSurfacesHttpTests` passou 2/2, com HTTP 401 anônimo e 403 para usuário comum nas rotas de restore. O risco TOCTOU permanece aberto.
- Revalidação do vínculo do ZIP em 2026-10-22: recovery compara SHA-256/tamanho do stream que o `ZipArchive` consumirá, e não somente uma nova abertura do pathname. `FullSystemBackup` 58/58 Release; timestamp legado não participa da identidade. Alteração pós-hash no mesmo inode, snapshot privado, lease interprocesso, operações de destino path-based e matriz Linux continuam pendentes; gate formal não recebe crédito.
- Revalidação local em 2026-10-22: suíte `FullSystemBackup` Release repetida no checkout atual, 61/61; `BackupRestoreMariaDbTests` teve 2 aprovados e 2 ignorados sem MariaDB descartável. Revisão independente confirmou que staging, instalação, rollback e limpeza continuam path-based após verificações reparse e que não há teste de troca concorrente determinístico; não implementar mitigação BCL incompleta. TOCTOU, crash/replay MariaDB, matriz Linux e ACL efetiva permanecem P0; homologação formal 1/7, sem release/instalação/produção.
- Revalidação do contrato FK em 2026-10-22: novo teste percorre os cinco modelos EF, confirma 39 FKs no total e 38 FKs associadas a entidades exportadas, cada qual com plano de validação. Teste focal 1/1 e `FullSystemBackup` Release 62/62; build do assembly de testes teve 0 erros e 303 avisos de analisadores. Evidência in-memory não substitui MariaDB; esta sessão não validou runtime, produção, Linux ou CI remota. Homologação formal segue 1/7; sem release.

## Addendum — journal/replay de restore após crash — 2026-10-22

- Implementado localmente: journal durável fora dos roots restaurados registra identidade SHA-256 do ZIP; startup valida e reaplica o mesmo arquivo antes de inicializar o host principal. Sidecars de filesystem são determinísticos por ID de operação e destino, permitindo retomar uma troca interrompida; arquivo ausente/alterado ou journal inválido falha fechado.
- Verificação: `BackupServiceTests` 41/41; conjunto `Jellyfin.Server.Implementations.Tests` 1.541 aprovados, 66 ignorados, 0 falhas. Inclui conflito `.bak` + `.absent` com falha fechada e preservação do journal; formatação C# e `git diff --check` aprovados.
- Limite: testes simulam estados do disco, não kill/restart real; listener de setup inicia antes do ponto de recovery. MariaDB descartável, CI remota, runtime instalado, produção e aceite formal não executados. Formal 1/7 (14,3%); sem release/instalação.

### Revalidação focal do journal — 2026-10-22 09:58 (-03)

- `dotnet test MulletaFlix-master/tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~FullSystemBackup --logger 'console;verbosity=minimal'` recompilou os projetos dependentes e passou 50/50, zero ignorados/falhas.
- A reexecução confirma os testes unitários atuais, não um encerramento/reinício real do processo. Nenhum código de runtime foi alterado; crash/restart com MariaDB descartável, risco TOCTOU, FK prévia, consistência temporal, CI remota, instalação e aceite formal continuam pendentes. Homologação formal 1/7 (14,3%); sem release ou produção.
- O mapa técnico foi conferido e continua descrevendo o fluxo atual: `BackupRestoreRecoveryJournal`, `BackupService.RecoverInterruptedRestoreAsync` e recuperação em `Program.StartApp`; sem mudança estrutural nesta sessão.

## Addendum — teste MariaDB de restore multi-contexto — 2026-10-22

- O teste integrado executado em MariaDB 11.4.4 isolada encontrou `ObjectDisposedException` ao reutilizar um contexto pooled após ele ter sido reconfigurado para compartilhar a conexão do restore.
- As fábricas dos contextos Movies, Series, Channels e Books passaram a ser não-pooladas; os demais contextos permanecem inalterados. Com isso, o restore ainda usa conexão/transação comum, mas não devolve contexto mutado a um pool.
- Validação: `BackupRestoreMariaDbTests` 3/3 e `FullSystemBackup` 46/46. A instância descartável foi encerrada; a instância operacional da porta 3306 não foi usada.
- Pendências: teste real kill/restart em storage/database descartáveis, TOCTOU, validação prévia de FK, consistência temporal do snapshot, CI remota e aceite formal. Sem instalação, release ou produção.

## Nebula — validação de recuperação após aceite Telegram e falha Mongo — 2026-10-21

- Implementação Python integrada à árvore principal: journal local atômico registra intenção antes do POST; confirmações guardam UUID, número da parte e SHA-256; retomadas conciliam o histórico MTProto em todos os bots antes de reenviar. Checkpoints Mongo legados são atualizados quando tamanho/data da fonte permitem validar a identidade. Resultados inconclusivos e falhas do journal são adiados com retry automático, preservando o stage.
- O scanner recupera leases Mongo vencidas no mesmo processo e mantém o fencing do worker antigo. O pacote Windows e o pacote Linux incluem upload_checkpoint.py; o verificador Windows compara SHA-256 fonte/stage.
- Validação executada nesta continuação: Python 3.10, py_compile e suíte NebulaPython 681/681; cópia Windows Pester 1/1; teste de integridade do stage Windows aceitou pacote correspondente e rejeitou helper ausente/desatualizado; contratos Node 4/4; teste de cópia Linux passou; git diff --check passou.
- A ausência em buscas MTProto é observada por até 31 s antes de autorizar retry; falha/inconclusão da busca nunca vira ausência. Ainda existe risco de duplicata se o Telegram aceitar a mensagem e a busca não a encontrar após essa janela. Exactly-once não é garantido; nenhum Telegram, Mongo operacional ou runtime instalado foi acessado nesta validação.
- Sem CI remota, instalação, produção, publicação do servidor ou release. Homologação formal continua 2/7 (28,6%); testes locais não recebem crédito adicional nos gates. Próximo gate: runtime instalado e smoke operacional controlado, além de CI/plataformas e aceite formal pendentes.

## Nebula — recuperar claim do scanner antes da fila — 2026-10-23

- `queued_mongo_scanner` podia deixar um registro preso em `staging`: qualquer exceção após o claim e antes da fila era apenas registrada, enquanto as iterações futuras buscavam somente `queued`. Se o arquivo continuasse no stage, `staging_scanner` também preservava esse status sem reenfileirar.
- Correção local: exceção pré-fila tenta retornar o documento a `queued` condicionalmente pelo `_id` e `status=staging`, com `next_retry_at` em 60 segundos. O claim seguinte respeita o prazo e limpa `failed_reason`/`next_retry_at`. Documento sem arquivo continua exigindo recuperação manual. Se Mongo falhar durante a própria atualização compensatória, status pode permanecer `staging`; log registra esse segundo erro.
- Testes adicionados cobrem requeue após erro transitório, respeito ao prazo, limpeza dos campos de retry e corrida em que outro worker já moveu o documento a `uploading`. Focados 16/16 e suíte completa Python 3.14.7 688/688; `py_compile` e `git diff --check` passaram (somente avisos de conversão CRLF/LF).
- Sem API, classe, dependência ou topologia nova; fluxo interno existente `queued_mongo_scanner` e seus testes foram alterados. Não houve Mongo operacional, Telegram, runtime instalado, produção ou release. Homologação formal permanece 2/7 (28,6%) sem crédito local.

## Intro Skipper — reanálise de STRM alterado e parâmetro EF — 2026-10-18

- O log de `A Era de Miyu - S01E06.strm` registra `AnalysisScheduler` falhando no passe `ChangedItems`: EF Core não conseguiu avaliar `ReadOnlySpan<AnalysisMode>` no predicado de `ResetItemsForReanalysisAsync`. Os avisos Chromaprint `returned 0 points` são mensagens separadas e não causam essa exceção.
- A consulta materializava os modos em array e usava `Contains` dentro de duas expressões EF. O código foi alterado para usar um `IReadOnlySet<AnalysisMode>` capturado como parâmetro local em ambos os predicados; os IDs dos itens já usam conjunto.
- Regressão MariaDB em `tests/IntroSkipper.Integration.Tests/IntroSkipperMariaDbTests.cs` cobre dois resets seletivos: `Credits` remove somente segmento/registro daquele modo; `Introduction` preserva o segmento manual e o automático correspondente e limpa apenas o marcador analisado desse modo. Outro item permanece inalterado. Suíte completa `IntroSkipper.Integration.Tests` Release recompilada passou 38/38, 0 ignorados, contra MariaDB descartável em loopback; revisão adversarial final sem achados.
- Sem endpoint, módulo, dependência ou componente novo; mudança no fluxo de manutenção/consulta existente do `IntroSkipperDatabase`. Não houve alteração em instalação ou banco produtivo. A versão do plugin/runtime que gerou o log ainda precisa ser comparada com a fonte e validada após uma release.
- Revalidação em 18/10/2026 15:32 (-03): solução Release recompilada e suíte repetida contra MariaDB descartável 11.4.4 em `127.0.0.1:33681`: 38/38 aprovados, 0 ignorados. O novo log recebido às 14:54 contém a mesma exceção; sem versão do plugin no log, não prova se é binário antigo nem se correção foi instalada. Nenhuma produção ou banco operacional foi acessado.

## Gate de compatibilidade do worker STRM no pacote — 2026-10-18 11:14 (-03)

- Evidência: runtime instalado tem `strm_downloader.py` datado de 08/10/2026 e sem `[STRM-WORKER-MONGO-READY]`/`[STRM-SCAN-STARTED]`; fonte atual possui o marcador exigido pelo manager. A divergência é compatível com falha/timeout ao iniciar o Downloader, mas não há no log anexado chamada manual nem stdout para confirmar causa exata.
- `Assert-StageIntegrity.ps1` agora verifica o contrato de prontidão Mongo na fonte antes de aceitar o artefato; hash da fonte e do staged continua sendo comparado. Teste PowerShell cobre pacote válido, cópia obsoleta e ausência do contrato. C# focal `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests`: 47/47; teste de integridade de stage passou; `git diff --check` limpo.
- Limite operacional: produção permanece sem alteração; processo/listeners atuais preservados. A API local indisponível impede observação via endpoint; instalação e smoke test controlados ainda necessários para confirmar a execução real. Sem release até o gate global de homologação e primeiro download controlado.
- Mapa conferido: fluxo existente `NebulaFtpManager` → worker Python e pipeline de empacotamento; nenhum componente, API, dependência ou topologia runtime nova.

## Rollback preserva sincronizador Supabase preexistente no Nebula — 2026-10-18

- Em falha do `StartEnvioAsync`, o catch descartava `_supabaseSyncService` mesmo quando a promoção do runtime reutilizava um serviço iniciado anteriormente. A propriedade agora é rastreada por tentativa; só recurso criado pela tentativa atual é limpo.
- Testes C# focados startup/HostedService/cleanup 57/57; suíte completa de Implementations 1.496 aprovados, 63 ignorados por dependências externas, 0 falhas. A suíte não cobre o runtime Python/Telegram real.
- Mapa técnico: componentes e endpoints inalterados; apenas semântica de rollback interno do manager. Produção e release não validadas.
- Homologação publicada no portal Git commit `9e1484aada74ff807ef757719b8f4d7a598be019`; deployment Vercel `READY`, endpoint público HTTP 200.

Escopo: backend `MulletaFlix-master`, com foco em performance, manutencao, bootstrap, persistencia, consultas e cobertura de testes.

## Autorização de endpoints administrativos do GetAvatar — 2026-10-17

- Corrigido controle de acesso em seis endpoints que já documentavam operação “admin only”: upload, delete, remover avatar de usuário, validar, limpar órfãos e consultar status. Antes, `LocalAccessOrRequiresElevation` permitia papel `User` em IP local; agora exigem `Administrator`.
- Regressão HTTP em `TestServer`: todos os seis endpoints retornam 401 sem autenticação e 403 para papel `User` (12/12). Suíte API Release: 697/697. Build Release do plugin: 0 avisos/0 erros.
- Ainda pendente: verificar ação bem-sucedida com admin por endpoint em ambiente controlado, incluir na release final e confirmar instalação. Não houve chamada privilegiada nem mudança de produção.
- Fontes: `MulletaFlix.Plugin.GetAvatar/Controllers/AvatarController.cs`, `tests/Jellyfin.Api.Tests/Controllers/NebulaAuthorizationHttpTests.cs`, `docs/avatar-admin-authorization.md`.

## Privacidade da configuração de branding — 2026-10-17

- `GET /Branding/Configuration` continua público para opções visuais, mas só inclui `IntroPath` para papel `Administrator`; anônimos e usuários comuns recebem `null`, impedindo divulgação do caminho físico do servidor.
- O DTO mantém a propriedade e o endpoint administrativo continua podendo ler o caminho. Testes unitários de controller cobrem retorno público sem caminho e leitura por administrador; o teste de integração público existente foi ignorado pelo runner nesta execução, então não conta como validação HTTP.
- Fonte: `Jellyfin.Api/Controllers/BrandingController.cs`; regressão: `tests/Jellyfin.Api.Tests/Controllers/BrandingControllerPrivacyTests.cs`.

## Persistência de imagens sob timeout MariaDB — 2026-10-17

- O log observado em produção mostrou `SaveImagesAsync` falhando após `Connect Timeout expired`, com `IOException` no stream de leitura. O classificador de falhas transitórias agora reconhece mensagens explícitas de timeout, `IOException` e continua percorrendo a cadeia interna quando um `MySqlException` não for transitório por si só.
- `SaveImagesAsync` já possui até três tentativas com backoff curto; esta mudança faz o erro de conexão do log alcançar esse retry, evitando perder a persistência das referências de imagem por uma falha de rede temporária.
- Testes de regressão cobrem o texto `Connect Timeout expired` e exceções de I/O. Isso não prova download de arte/capa do provider nem validação contra MariaDB de produção; após a release será necessário confirmar as imagens na interface e revisar o log.
- Fontes: `Jellyfin.Server.Implementations/Item/ItemPersistenceService.cs` e `tests/Jellyfin.Server.Implementations.Tests/Item/ItemPersistenceServiceTests.cs`.

## Padrao De Codificacao

- Toda documentacao e todo codigo devem ser mantidos em `utf-8`.
- O repositório ja reforca isso em [`.editorconfig`](D:/Users/Raphael/Documents/Projetos/mulletaflix/MulletaFlix-master/.editorconfig) com `charset = utf-8`.
- Novos arquivos de texto, markdown e codigo devem seguir o mesmo padrao.

## Handoff Entre IAs

Se esta conversa ficar sem tokens e for necessario continuar em outra IA, a proxima instancia deve receber este contexto minimo:

- O projeto principal e o backend do `MulletaFlix-master`.
- A sprint atual e `Sprint 5 - Home, detalhe e refresh sob demanda`.
- O que ja foi concluido inclui `UserManager`, `ItemPersistenceService`, `BaseItemRepository.Querying`, `MetadataService`, `ProviderManager`, `MediaInfoResolver`, `FFProbeVideoInfo` e partes de `ProbeProvider`.
- O que ja foi corrigido nesta rodada inclui o erro de `requestIdleCallback` no `imageLoader`, que estava bloqueando a renderizacao da home e da pagina de detalhe.
- O que ainda falta nesta rodada inclui medir o impacto do refresh sob demanda em background, validar no navegador o carregamento de metadados/imagens, revisar a pagina de dispositivos do dashboard sob carga e continuar os cortes de performance em reconhecimento de midia e download de metadata.
- Toda a documentacao e todo o codigo devem continuar em `utf-8`.
- Os testes de providers já foram executados com sucesso e devem ser mantidos como referencia de regressao.
- A cada tarefa concluida, a documentacao deve ser atualizada com:
  - a tarefa realizada;
  - as proximas tarefas da sprint;
  - o status atual da sprint;
  - qualquer risco ou bloqueio novo.
- Esse update de documentacao deve acontecer antes de trocar de IA, para que outra instancia possa continuar sem perder contexto.
- Se houver troca de IA de ida e volta, a documentacao deve servir como fonte unica do estado atual do trabalho.
- Sempre marcar o que foi concluido com `[x]` e o que falta com `[ ]`, para que outra IA saiba exatamente onde continuar.

## Sprint Atual

- Sprint atual: `Sprint 5 - Home, detalhe e refresh sob demanda`
- Status da sprint atual: `em andamento`
- Sprints concluídas: `Sprint 0 - Baseline e instrumentacao`, `Sprint 1 - Consultas quentes e midia`, `Sprint 2 - Persistencia e escrita`, `Sprint 3 - Startup e bootstrap` e `Sprint 4 - Manutenibilidade estrutural`
- Sprints pendentes: `Sprint 5 - Home, detalhe e refresh sob demanda`

## Status Atual

- [x] Auditoria tecnica inicial concluida.
- [x] Otimizacoes de `UserManager`, `ItemPersistenceService`, `BaseItemRepository.Querying`, `MetadataService`, `ProviderManager`, `MediaInfoResolver` e `FFProbeVideoInfo` aplicadas.
- [x] Suíte de testes de providers validada com sucesso.
- [x] Sprint 1 concluída com sucesso (incluindo fechamento de `ProbeProvider.HasChanged` e cortes de performance).
- [x] Sprint 2 concluída com sucesso (refatorações de batching, remoção de queries N+1, otimização de `DeleteItem` e `SaveImagesAsync` no `ItemPersistenceService`).
- [x] Sprint 3 concluída com sucesso (portabilidade e bootstrap do MariaDB assíncrono, remoção de polling de porta bloqueante e fast-path de schema bypass no `UserManager`).
- [x] Sprint 4 concluída com sucesso (refatoração e decomposição de `UserManager.cs` em `UserAuthenticationService.cs` e `PasswordResetService.cs`).
- [x] Auditoria de segurança completa (skill `find-bugs`) executada com sucesso.
  - Resultado: 1 vulnerabilidade corrigida (injeção de parâmetros URL no `OpenLibraryProvider.cs`).
  - Correção: aplicação de `Uri.EscapeDataString` nos parâmetros ISBN e OLID interpolados nas URLs de consulta à API externa do OpenLibrary.
  - Build verde (0 erros) e testes unitários/providers aprovados (4/4 OpenLibrary, 560/560 Implementations).
  - Testes de integração com falhas pré-existentes (401 Unauthorized no `AuthHelper`) não relacionadas à alteração.

- [x] Corrigido o fluxo de detalhe para revalidar metadados e imagens sob demanda antes de montar o DTO em `GetItem`.
- [x] Criado `OnDemandMetadataRefreshPolicy` para centralizar a decisao de refresh por completude e recencia.
- [x] Ajustado `GetLatestMedia` para tentar completar itens incompletos ou desatualizados antes de montar os cards da home.
- [x] Teste unitario da politica de refresh aprovado com 3 casos de regressao.
- [x] Revisao do fluxo de frontend da home e da pagina de detalhe feita; nao apareceu bug obvio de fetch, entao o ajuste principal ficou no backend.
- [x] Corrigido o `GetLatestTvShowItems` para agrupar por `SeriesId`, nao por `SeriesName`, e adicionado indice composto `Type, SeriesId, DateCreated`.
- [x] Adicionado indice em `UserData` para o `NextUp` (`UserId, Played, LastPlayedDate`).
- [x] Evitado leitura de arquivo para `PrimaryImageAspectRatio` quando `ItemImageInfo.Width/Height` ja estao persistidos.
- [ ] Medir o impacto real de latencia do refresh sob demanda na home e no detalhe.
- [ ] Validar no navegador se as artes, overview e metadados reaparecem nas midias recentemente adicionadas.

## Todo Prioritario Por Ganho

1. P0 - Medir a home real com banco grande:
   - comparar `LatestMedia` de filmes e series com `EXPLAIN ANALYZE`;
   - confirmar se o custo saiu de SQL e ficou em DTO/imagem.
   - [x] Adicionar medição MariaDB descartável dos dois ramos: filmes com 10.000 versões sintéticas (100 chaves x 100; ANALYZE 10.001 linhas/9,562 ms; endpoint 369,929 / 65,830 / 19,720 ms) e séries com 10.000 episódios (100 séries x 100, no mesmo acervo com os filmes; ANALYZE 20.101 linhas/13,504 ms; endpoint 60,406 / 21,906 / 23,016 ms). O teste exige milhares de linhas no plano para garantir a captura da consulta candidata, não da consulta de carga final.
   - [ ] Repetir em snapshot anonimizado/fixture representativa da biblioteca real e medir memória e custo do DTO/imagem; não ajustar índice nem algoritmo apenas com a fixture sintética.
   - ganho esperado: muito alto se ainda existir scan ou sort caro.
2. P1 - Fechar `NextUp`:
   - [x] Revisar a materialização: `GetNextUpSeriesKeys` projeta apenas as chaves no banco e aplica o limite antes de retornar; o lote seguinte pode não ter limite geral.
   - [x] Confirmar em `GetNextUpEpisodesBatch` que episódios assistidos, candidatos não assistidos e (quando habilitado) datas de reprodução são materializados integralmente e agrupados/ordenados em memória para todas as chaves recebidas.
   - [x] Substituir listas completas, agrupamentos e ordenações por acumulação em passagem única, retendo somente o candidato selecionado por série para episódios assistidos, não assistidos e rewatch; teste diferencial validou ordenação estável com chaves nulas/empatadas (NextUpQueryOptimizationTests 8/8 em Windows/.NET 10 Release).
   - [x] Verificar contrato de paginação: `EnableTotalRecordCount` é true por padrão e `GetResult` materializa toda a sequência antes de aplicar `StartIndex`/`Limit` quando a contagem está habilitada.
   - [x] Implementar ranking relacional compatível com MariaDB nos candidatos não assistidos e no rewatch, preservando especiais, posição nullable, contagem e paginação; MariaDB/Pomelo descartável confirma tradução e semântica em fixture.
   - [ ] Medir planos, linhas examinadas, latência e memória antes/depois com dados representativos; fixture pequena não prova benefício de produção. Índice existente `(Type, SeriesPresentationUniqueKey, ParentIndexNumber, IndexNumber)` não deve ser alterado sem plano que demonstre necessidade.
   - ganho esperado: alto em bibliotecas com muitas séries e episódios.
3. P2 - Cortar custo de DTO/imagem:
   - revisar `DtoService` e `UpdateImagesAsync` para evitar trabalho repetido em items recentes;
   - medir se o gargalo restante esta em blurhash, dimensoes ou carga de imagem.
   - ganho esperado: medio.
4. P3 - Refino de home no cliente:
   - manter as imagens fora da viewport em lazy load;
   - evitar refetch desnecessario em navegacao entre paginas.
   - ganho esperado: baixo a medio, mas barato de manter.

## Skills utilizadas nesta auditoria

Estas skills foram selecionadas porque cobrem revisao tecnica, arquitetura, simplificacao e execucao orientada a sprint.

- `codex-fable5`: leitura orientada a evidencias, validacao e fechamento rastreavel.
- `code-review-and-quality`: revisao de corretude, arquitetura, seguranca, legibilidade e performance.
- `code-review-excellence`: estruturar achados por severidade, impacto e recomendacao.
- `dotnet-backend`: orientar ASP.NET Core, EF Core, async, cache e background services.
- `dotnet-architect`: avaliar limites de arquitetura, coesao de modulos e risco sistico.
- `dotnet-backend-patterns`: validar padroes de API, data access, DI, cache e resiliencia.
- `code-simplifier`: reduzir complexidade sem mudar comportamento.
- `backend-development-feature-development`: organizar trabalho em sprints, entregaveis e validacao.
- `caveman`: sintetizar e manter foco em execucao pragmatica.
- `cavecrew`: delegacao curta e objetiva para investigacao pontual.

## Entregas ja validadas

As alteracoes abaixo ja foram aplicadas e validadas com `dotnet test` no backend:

- [x] `Jellyfin.Server.Implementations/Users/UserManager.cs`
  - lock por username para evitar serializacao indevida entre logins diferentes;
  - lock por user id quando o usuario ja existe.
- [x] `Jellyfin.Server.Implementations/Item/ItemPersistenceService.cs`
  - uso de `Dictionary` e `HashSet` para reduzir buscas repetidas;
  - menor custo em atualizacao de itens com muitos relacionamentos.
- [x] `Jellyfin.Server.Implementations/Item/BaseItemRepository.Querying.cs`
  - hot path de `latest TV shows` reduzido para materializar menos dados;
  - ajuste para evitar falha de compatibilidade com EF Core no filtro recente.
- [x] `MediaBrowser.Providers/Manager/MetadataService.cs`
  - menos enumeracoes repetidas em selecao de providers.
- [x] `MediaBrowser.Providers/Manager/ProviderManager.cs`
  - materializacao e particionamento simplificados no caminho quente.
- [x] `MediaBrowser.Providers/MediaInfo/MediaInfoResolver.cs`
  - parse e filtragem dos sidecars em um unico passe.
- [x] `MediaBrowser.Providers/MediaInfo/FFProbeVideoInfo.cs`
  - cache de sidecars reaproveitado e probes de DVD executados em paralelo.
- [x] `MediaBrowser.Providers/MediaInfo/ProbeProvider.cs`
  - comparacao de sidecars reduzida com helper mais barato.
- [x] Testes de regressao adicionados e aprovados para esses caminhos.

## Achados priorizados

### P0

1. `ItemPersistenceService` concentra save, delete, deduplicacao, relacoes e limpeza em um fluxo grande.
2. `UpdateOrInsertItemsCore` mantém lookup O(1) para comparar providers e campos de metadados bloqueados: conjuntos hash são construídos uma vez por comparação e consultados durante uma varredura linear. Testes focais e regressão da suíte `Jellyfin.Server.Implementations.Tests` passaram em Windows/.NET 10 Release em 26/10/2026. Sem benchmark antes/depois; confirmar ganhos medidos e execução Linux/CI continua pendente.
3. O pipeline de reconhecimento de midia e download de metadata ainda faz trabalho demais por item e precisa de mais batching, cache e menos I/O.
4. `GetLatestItemList` agora mantém os IDs de álbuns com faixas correspondentes em subconsulta distinta; teste funcional Release passou. Ainda faltam plano/carga MariaDB. Deduplicação de filmes/séries recentes transmite somente chaves projetadas e para ao obter o limite de cards; pode percorrer muitos candidatos quando a duplicidade é alta ou o limite inexiste.

### P1

1. `MariaDbProcessManager`: bootstrap/readiness/shutdown assíncronos confirmados; `Program` aguarda o ciclo. `MariaDbProcessManagerLifecycleTests.StartStopStart_UsesTheSameIsolatedDataDirectory` passou 1/1 em Windows/.NET 10 Release em 27/10. Wrapper público síncrono `StopMariaDb` permanece sem chamadores internos; manter compatibilidade até confirmar consumidores externos. Linux, CI hospedada e runtime instalado pendentes.
2. `UserManager` ainda concentra responsabilidades demais para manutencao sustentavel.
3. Os caminhos de refresh e provider ainda podem evitar downloads repetidos e refreshs redundantes.

### P2

1. Classes grandes precisam de extracao gradual de helpers.
2. Rotinas auxiliares de consulta e bootstrap precisam de contratos mais claros.

## Plano tecnico por sprint

### Sprint 0 - Baseline e instrumentacao

Objetivo: medir antes de otimizar.

Tarefas:

- [x] Identificar endpoints e jobs mais caros.
- [x] Medir tempo de query, memoria e startup.
- [x] Registrar baseline para listagem, next up, save item e bootstrap.

Concluido:

- [x] Sprint 0 fechado como baseline documental e de medicao.

Pendente:

- [ ] Repetir as mediciones apos as proximas sprints para comparar ganhos reais.

Skills:

- `codex-fable5`
- `code-review-and-quality`
- `code-review-excellence`
- `dotnet-architect`
- `dotnet-backend`

Prioridade: P0.

Ganho esperado:

- Nao entrega ganho direto de producao.
- Reduz risco de regressao e define linha de base.

### Sprint 1 - Consultas quentes e midia

Objetivo: reduzir latencia e memoria em listagens, reconhecimento de midia e metadata.

Tarefas:

- [x] Revisar `BaseItemRepository.Querying.cs` para adiar materializacao.
- [x] Manter filtros e ordenacao no banco pelo maior tempo possivel.
- [x] Revisar `MediaInfoResolver` para reduzir probes e leituras de caminho.
- [x] Revisar `MetadataService` e `ProviderManager` para diminuir downloads repetidos.
- [x] Adicionar testes para regressao de paginacao, ordenacao e descoberta de midia.
- [x] Reduzir tempo de download de metadata com cache por item e deduplicacao de requests.
- [x] Reduzir o custo de reconhecimento de midia com menos probes redundantes.

Concluido:

- [x] `MediaInfoResolver`, `MetadataService`, `ProviderManager`, `FFProbeVideoInfo` e `ProbeProvider` receberam o primeiro corte de performance.
- [x] Testes focados de providers fecharam em verde.
- [x] `ProbeProvider.HasChanged` recebeu o corte seguro e foi validado com teste dedicado.

Pendente:

- [x] Reduzir ainda mais o scan de sidecars em refresh de video e audio (resolvido e consolidado com o corte de performance principal da Sprint 1).

Proxima tarefa:

- [x] Iniciar a Sprint 2 - Persistencia e escrita (concluída!).

Skills:

- `dotnet-backend`
- `dotnet-backend-patterns`
- `dotnet-architect`
- `code-simplifier`
- `code-review-excellence`
- `backend-development-feature-development`

Prioridade: P0.

Ganho esperado:

- Latencia p95 de listagens: melhora estimada de 15% a 35%.
- Consumo de memoria: reducao estimada de 10% a 25%.
- Reconhecimento de midia: melhora estimada de 10% a 30%.
- Download de metadata: melhora estimada de 15% a 40%.

### Sprint 2 - Persistencia e escrita

Objetivo: reduzir custo de save/delete e melhorar persistencia em lote.

Tarefas:

- [x] Quebrar `UpdateOrInsertItemsCore` em etapas menores.
- [x] Trocar buscas repetidas por dicionarios e estruturas indexadas.
- [x] Reduzir chamadas sincrono-sobre-async.
- [x] Revisar `DeleteItem` para diminuir `Any`, `Contains` e `ToList` repetidos.
- [x] Reduzir regravacao desnecessaria de metadata e imagens.
- [x] Reforcar cobertura de `ItemPersistenceServiceTests`.

Concluido:

- [x] Refatorações e batching do `ItemPersistenceService` aplicados e validados com testes (remoção de queries N+1, otimização de `DeleteItem` e `UpdateOrInsertItemsCore`, e conversão de `.SaveChangesAsync().GetAwaiter().GetResult()` em `SaveChanges()`).
- [x] Implementação de verificação de alterações para Providers, Imagens (em `SaveImagesAsync` e `SaveBaseItemEntities`) e LockedFields, eliminando regravações redundantes no banco de dados.

Pendente:

- [ ] Nenhuma (Sprint 2 totalmente concluída).

Skills:

- `dotnet-backend`
- `dotnet-backend-patterns`
- `code-simplifier`
- `code-review-and-quality`
- `code-review-excellence`

Prioridade: P0.

Ganho esperado:

- Tempo de save em lotes: melhora estimada de 20% a 40%.
- Menos lock contention e menor chance de travamento sob carga concorrente.
- Refresh de metadata: melhora estimada de 10% a 25%.

### Sprint 3 - Startup e bootstrap

Objetivo: melhorar boot e reduzir acoplamento operacional.

Tarefas:

- [x] Remover polling bloqueante de `MariaDbProcessManager`.
- [x] Separar inicializacao de processo e criacao de schema.
- [x] Tornar bootstrap de DB externo e local mais explicito.
- [x] Revisar `UserManager` para nao bloquear caminhos frequentes.
- [x] Cobrir boot com DB pre-existente e schema ausente.

Concluido:

- [x] Conversão da inicialização do MariaDB portátil para fluxos totalmente assíncronos (`StartMariaDbAsync`, `InitializeDatabaseSchemasAsync`, `WaitForPortAsync`) e await real.
- [x] Otimização da concorrência de logins de usuários inexistentes via hashing MD5 de username para a chave de lock, evitando serialização de requests.
- [x] Implementação do fast-path bypass de validação de schemas em consultas síncronas (`GetUsers`, `GetUsersIds`, `GetUserById`, `GetUserByName`) no `UserManager`.
- [x] Validação completa de toda a suíte de testes (560/560 testes aprovados, incluindo os testes de concorrência de autenticação que anteriormente expiravam).

Pendente:

- [ ] Nenhuma (Sprint 3 totalmente concluída).

Skills:

- `dotnet-architect`
- `dotnet-backend`
- `dotnet-backend-patterns`
- `backend-development-feature-development`
- `code-review-and-quality`
- `code-review-excellence`

Prioridade: P1.

Ganho esperado:

- Startup: melhora estimada de 10% a 30%.
- Menor risco de falha intermitente no boot.

### Sprint 4 - Manutenibilidade estrutural

Objetivo: reduzir tamanho de classes e facilitar evolucao.

- [x] Extrair helpers de `UserManager`.
- [x] Separar autenticacao, reset de senha e inicializacao.
- [ ] Revisar classes grandes de midia e provider.
- [x] Padronizar nomes, contratos e retornos.
- [x] Remover logica morta, comentarios obsoletos e duplicacao.

Concluido:

- [x] Extração de `UserAuthenticationService` contendo a lógica de validação de licenças, login local/provedores e restrição de acesso.
- [x] Extração de `PasswordResetService` contendo a lógica de esquecimento de senha e verificação de PIN de redefinição.
- [x] Refatoração de `UserManager.cs` delegando responsabilidades a esses novos serviços internos, reduzindo o arquivo original.
- [x] Compilação e testes validados com sucesso (560/560 testes passando).

Pendente:

- [ ] Revisar classes grandes de midia e provider (planejado para sprints futuras de manutenção de código).

Skills:

- `code-simplifier`
- `dotnet-architect`
- `code-review-excellence`
- `code-review-and-quality`
- `cavecrew`
- `caveman`

Prioridade: P2.

Ganho esperado:

- Ganho direto de performance: baixo, 0% a 10%.
- Ganho forte em manutencao e velocidade de entrega futura.

### Sprint 5 - Home, detalhe e refresh sob demanda

Objetivo: corrigir a exibicao de midias recentes, metadados e imagens sem perder o controle de performance.

Tarefas:

- [x] Auditar o fluxo da home e da pagina de detalhe no backend e no frontend.
- [x] Centralizar a decisao de refresh sob demanda em `OnDemandMetadataRefreshPolicy`.
- [x] Revalidar metadados e imagens no `GetItem` antes de montar o DTO.
- [x] Revalidar itens incompletos ou desatualizados no `GetLatestMedia`.
- [x] Criar teste unitario da politica de refresh.
- [x] Enfileirar o refresh sob demanda em background com `MulletaFlixJobQueue` para remover custo da request.
- [x] Reduzir o custo do `GetLatestTvShowItems` trocando o carregamento final de entidades para `AsSplitQuery` por ids materializados.

Concluido:

- [x] O backend agora tenta completar itens sem overview ou imagem primaria antes de responder o detalhe.
- [x] O fluxo de cards recentes tambem ganhou refresh sob demanda para melhorar a chance de mostrar arte e metadados atualizados.
- [x] A validacao do frontend nao mostrou bug obvio no fetch; o problema principal ficou concentrado no backend.
- [x] O refresh sob demanda saiu da request path e passou a ser processado em background com deduplicacao curta por item.
- [x] Corrigido o bug de `requestIdleCallback` em `imageLoader`, que interrompia `lazyChildren()` e impedia home e detalhe de terminar de renderizar.
- [x] Blindado o `reload()` da pagina de detalhe com `finally` para liberar o loading mesmo se alguma etapa falhar.
- [x] Otimizado o carrossel da home para pedir imagens menos pesadas em listas `overflow`, reduzindo o custo de download e decode das thumbs.
- [x] Corrigido o carregamento da pagina de dispositivos do dashboard para nao bloquear a renderizacao em `useUsersDetails`.
- [x] Otimizado o caminho de series recentemente adicionadas no backend (`GetLatestTvShowItems`) para evitar `SingleQuery` com multiplas colecoes e reduzir a latencia da home.
- [x] Ajustado o frontend da home para `tvshows` pedir `Backdrop` e voltar a priorizar `Thumb`, reduzindo fallback lento de imagem nas series recentemente adicionadas.

Proximas tarefas:

- [ ] Validar no servidor instalado se a secao de series recentemente adicionadas voltou ao mesmo patamar de filmes.
- [ ] Medir a diferenca real de tempo da home com e sem varredura/refresh de metadata em andamento.
- [x] O backend de devices passou a tolerar referencia de usuario ausente sem derrubar a lista inteira.
- [x] Teste de regressao `DeviceManagerTests` aprovado.
- [x] Build de producao do frontend validado com sucesso.
- [x] Corrigido o carregamento da pagina `Atividade` do dashboard para nao bloquear a tabela em `useUsersDetails`.
- [x] Build de producao do frontend validado novamente apos o ajuste de `Atividade`.
- [x] Corrigida a pagina `NFO` do dashboard para nao depender de usuarios como bloqueio de carregamento.
- [x] Corrigida a persistencia de `BaseItemProvider`, `LockedFields`, `Images` e `TrailerTypes` para nao anexar o grafo completo com entidades duplicadas durante refresh de metadados.
- [x] Teste de regressao `ItemPersistenceServiceTests` aprovado.
- [x] A gravacao de `ItemValues` passou a usar `INSERT IGNORE` para eliminar conflitos recorrentes de indice unico durante refresh concorrente.
- [x] Validacao da frente `ItemPersistenceService` aprovada em `Jellyfin.Server.Implementations.Tests`.

Pendente:

- [ ] Medir o impacto de tempo de resposta da home e do detalhe com o refresh novo.
- [ ] Validar a experiencia final no navegador com midias novas, metadados e imagens.
- [ ] Medir o impacto do ajuste da pagina de dispositivos apos remover a dependencia de usuarios.
- [ ] Medir o impacto do ajuste da pagina de `Atividade` apos remover a dependencia de usuarios.
- [x] Validar que a pagina `NFO` segue funcional quando a carga de usuarios falha; `nfo.test.tsx` cobre configuração disponível com `useUsers` em erro, formulário presente e opção `Nenhum`.
- [ ] Confirmar nos logs que o refresh de metadados nao volta a acusar `BaseItemProvider` duplicado nem repetir erro ao persistir `TrailerTypes`.
- [ ] Confirmar em stage que o erro `Duplicate entry '3-Sky Atlantic' for key 'IX_ItemValues_Type_Value'` nao volta a aparecer sob carga.
- [ ] Medir o impacto da troca para `INSERT IGNORE` na taxa de sucesso do refresh concorrente.
- [ ] Se a home continuar vazia, instrumentar `itemsContainer.refreshItems()` para registrar falhas de query e diferenciar erro de API de lista realmente vazia.
- [ ] Fechar a proxima sprint apenas depois de confirmar no navegador que `Minha midia`, `recentemente adicionadas` e a tela de detalhe carregam sem erro de console.

Skills:

- `dotnet-backend`
- `dotnet-architect`
- `dotnet-backend-patterns`
- `code-review-and-quality`
- `code-review-excellence`
- `backend-development-feature-development`
- `caveman`
- `cavecrew`

Prioridade: P0.

Ganho esperado:

- Melhor chance de carregar arte, overview e metadados nas primeiras respostas.
- Reducao de chamados de suporte por item sem imagem ou sem metadata na tela.
- Impacto de performance agora tende a ser menor na request e maior no processamento em segundo plano.
- A fila existente foi reutilizada para evitar duplicar infraestrutura de background.
- A pagina de dispositivos deve aparecer mais cedo no dashboard e continuar funcional mesmo com usuarios antigos removidos do banco.

## Ordem recomendada

1. Sprint 0
2. Sprint 1
3. Sprint 2
4. Sprint 3
5. Sprint 4
6. Sprint 5

## Prioridade consolidada

1. `P0`: consultas quentes, reconhecimento de midia, download de metadata, refresh sob demanda da home/detalhe e persistencia em lote.
2. `P1`: startup, bootstrap e reducao de bloqueios.
3. `P2`: refatoracao estrutural e simplificacao de classes grandes.

## Validacao recomendada

- `dotnet test` focado em `Jellyfin.Server.Implementations.Tests`.
- Casos prioritarios:
  - `ItemPersistenceServiceTests`
  - `NextUpQueryOptimizationTests`
  - `BaseItemRepositoryTests`
  - `BaseItemRepositoryLatestTvShowTests`
  - `UserManagerTests`
  - `UserManagerNormalizedUsernameTests`
  - `UserManagerAuthenticationLockTests`
- Benchmark local para:
  - listagem paginada
  - latest/next up
  - save/delete em lote
  - startup com MariaDB embutido e com DB ja existente
  - reconhecimento de midia
  - download de metadata

## Risco residual

Mesmo com as correcoes aplicadas, parte do custo pode continuar por causa de dados legados, schema historico e compatibilidade com comportamento antigo.

Para evitar falso ganho, cada sprint deve fechar com medicao antes/depois.

### 13/10/2026 — Persistência resiliente das referências de imagens (v12.1.20)

- O log de produção registrou deadlock MariaDB `1213` durante `ItemPersistenceService.SaveImagesAsync`, além de timeouts posteriores do banco. A mídia e os metadados apareciam, mas a gravação de `BaseItemImageInfos` falhava.
- Antes da correção, `SaveImagesAsync` apagava as referências antigas e inseria as novas sem uma transação explícita. Uma falha na inserção podia deixar o item sem referências de imagem.
- A operação agora exclui e insere dentro da mesma transação e repete até três vezes deadlocks/timeout de lock e falhas de conexão transitórias, cada tentativa usando um novo contexto e atraso progressivo. Erros de duplicidade e chave estrangeira não entram no retry.
- A busca por gravações de `BaseItemImageInfos` encontrou uma rotina análoga em `UpdateOrInsertItemsCore`; ela já é executada dentro de uma transação. A rotina de exclusão de itens não reinsere imagens.
- Build Release de `Jellyfin.Server.Implementations` aprovado. `ItemPersistenceServiceTests`: 23 aprovados. Suíte completa: 1.419 aprovados, 60 ignorados e 16 falhas não relacionadas em `NebulaDownloaderEngineTests`, devido a mocks de `NebulaMongoContext` sealed.
- Limite: o log não confirma se o provedor remoto chegou a baixar a imagem; o ID do erro de persistência não coincide com o ID do item mostrado no screenshot. A correção protege a persistência das referências; é necessário revalidar uma mídia afetada depois de atualizar e atualizar seus metadados. Falhas de geração FFmpeg do Trickplay são outro fluxo.

### 24/10/2026 — Reforço do teste de exclusão de versão órfã

- Revisão adversarial mostrou que o teste inicial usava atraso fixo e segurava somente o `SemaphoreSlim` local; isso não provava que a operação chegou ao bloqueio nem cobria a advisory lock compartilhada entre sessões.
- O teste MariaDB agora mantém a advisory lock da stripe órfã em uma sessão, inicia `SaveItems` em outra e consulta `PROCESSLIST` filtrado pelo schema descartável até observar `GET_LOCK` em espera. Esse sinal remove a corrida do atraso fixo e valida coordenação entre sessões.
- O teste foi recompilado em Release, mas a execução foi ignorada porque a conexão descartável MariaDB não está configurada. Não valida processos OS separados, runtime instalado, produção ou causa do log 1213. Homologação formal continua 2/7 (28,6%), sem crédito.

### 16/10/2026 — Monitoramento Nebula restrito às pastas configuradas (v12.1.21)

- O downloader agora deriva suas fontes exclusivamente de `MonitorPaths`; caminhos inexistentes, vazios ou raízes do filesystem são rejeitados e caminhos equivalentes são normalizados/deduplicados. Não injeta mais `D:\midias` nem `D:\midias2` automaticamente.
- A seleção de staging não aceita a raiz de um volume e falha explicitamente se nenhum candidato válido restar, em vez de tentar gravar nela ou silenciosamente usar o primeiro caminho inválido.
- `NebulaFtpManager` valida as fontes antes de inicializar dependências e só informa downloader ativo quando o motor realmente iniciou.
- Validação: suíte final Implementations 1.437 aprovados, 60 ignorados, 0 falhas; `NebulaDownloaderEngineTests` 43/43; portal 31/31; build Release `Jellyfin.Server` 0 avisos/erros. Gate agregado anterior passou 4.384/4.449 (65 ignorados), excluindo `LiveIntegration`; repetição agregada final ficou sem progresso no testhost ASP.NET por mais de cinco minutos e foi interrompida (inconclusiva). Nenhum teste de reprodução/upload em produção foi executado.

### 17/10/2026 — Falha de deleção de imagem de usuário não pode retornar sucesso

- `ImageController.DeleteUserImage` usava `System.IO.File.Delete` diretamente e, ao capturar `IOException`, apenas registrava o erro; ainda limpava a referência persistida e retornava 204. Isso podia deixar arquivo e banco inconsistentes e informar sucesso indevido.
- A operação agora usa o `IFileSystem` injetado. Em `IOException`, retorna 500 e mantém a referência; em sucesso continua limpando-a e retornando 204. O contrato da API declara também a resposta 500.
- Regressão TDD: 2 testes focados passaram (falha mantém referência/retorna 500; sucesso chama exclusão e limpa referência). `Jellyfin.Api.Tests` Release: 679/679. A busca global por `catch (IOException)` com log seguido de `ClearProfileImageAsync`/`NoContent()` encontrou apenas este local e foi corrigida.
- Essa correção não altera módulos, endpoints, dependências ou fluxo de runtime, mas ainda precisa ser incluída em pacote/release. Isso é separado do download Nebula: a instalação ativa segue 12.1.21, não havia worker no snapshot e o primeiro download real permanece sem homologação.

### 17/10/2026 — Corrida entre sync Supabase (backup/restore) e uploads Nebula

- `NebulaSupabaseSyncService` serializa backup e restore no `_backupGate`; ambos agora adquirem também `NebulaMongoContext.AcquireExclusiveSyncAdmissionAsync`, que reserva `nebula_admission`, bloqueia novas admissões C#/Python e drena leases ativos antes de ler ou substituir estado Mongo. A API foi renomeada para refletir o escopo de sincronização exclusiva, não somente backup.
- A regressão Mongo `Restore_ReservesExclusiveAdmissionUntilActiveUploadCompletes` mantém um upload lease ativo e comprova que o restore não envia requisições ao Supabase até o lease ser liberado; após a liberação, o restore termina. Executada contra MongoDB descartável em `127.0.0.1:27099`; testes focados restore + concorrência passaram 20/20, sem ignorados/falhas.
- Limite: o gate só coordena processos que compartilham o mesmo Mongo/database `ftp` e usam versões que implementam o protocolo; isso não valida backup/restore remoto real nem instalação produtiva. A instância 12.1.21 continua sem essa evidência.
- Evidências: `Jellyfin.Server.Implementations/Nebula/NebulaSupabaseSyncService.cs`, `NebulaMongoContext.cs`, `tests/Jellyfin.Server.Implementations.Tests/Nebula/NebulaSupabaseRestoreMongoTests.cs` e `NebulaQueueConcurrencyMongoTests.cs`. Mapa técnico atualizado e release 12.1.24 ainda não publicada.

### 17/10/2026 — Revalidação do gate de backup e cobertura Mongo real

- Reinspeção confirmou que `_backupGate` em `NebulaSupabaseSyncService` serializa chamadas de backup/restauração entre si; não coordena os quatro caminhos de admissão de upload: três `find_one_and_update` nos workers large/parallel/small de `Tools/NebulaPython/main.py` e a criação/claim em `NebulaUploadEngine`/`NebulaMongoContext`.
- `NebulaQueueConcurrencyMongoTests` Release foi primeiro executado sem opt-in Mongo e todos os 16 casos foram ignorados; depois, com MongoDB 8.3.7 exclusivo em `127.0.0.1:27099` e banco de teste isolado, passou 16/16, 0 falhas, 0 ignorados. A instância temporária foi encerrada e a porta deixou de escutar; o processo Mongo alheio em outras portas não foi tocado.
- A integração demonstra que as regressões existentes funcionam contra Mongo real, mas não contém caso de interleaving backup-ocioso → claim de upload; não é validação da barreira ainda ausente. Sem evidência de perda permanente e sem modificação de produção.

### 17/10/2026 — Revalidação do download Nebula que não inicia

- Confirmado novamente: runtime local responde 12.1.21 e executa script antigo (SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`); fonte e stage 12.1.22 têm hash `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F` e contêm correção de readiness/varredura incremental por título.
- `NebulaDownloaderStartupTests`: 10/10; suíte Python: 581/581; integridade de stage passou. Esses testes não comprovam transferência Telegram real. Nenhum worker, instalação ou configuração de produção foi alterado. `gh auth status` não encontra sessão autenticada; release não publicada.
- Próximo gate: autenticar publicação GitHub, reconstruir/publicar pacote suportado, instalar em janela controlada e observar primeiro item real. Não declarar correção em produção antes dessa evidência.

### 17/10/2026 — Homologação ampla da solução do servidor

- `dotnet test MulletaFlix.sln --configuration Release --no-build --no-restore --filter "FullyQualifiedName!~Integration"` concluiu sem falhas nos projetos determinísticos reportados. `Jellyfin.Server.Implementations.Tests`: 1.452 aprovados, 60 ignorados (1.512); `Jellyfin.Providers.Tests`: 775/775; `Jellyfin.Api.Tests`: 679/679; `Jellyfin.Server.Tests`: 73 aprovados/1 ignorado; Controller 63/63; Naming 681/681; Model 684/684; Networking 124/124; MediaEncoding 71/71; LiveTv 75/75; XbmcMetadata 39/39; Common 31/31; HLS 18/18; Extensions 144/144; Keyframes 3/3. O filtro excluiu os projetos cujo assembly é `*.Integration.Tests`; testes de banco sem serviços opt-in também foram ignorados.
- A primeira execução usou `Category!=LiveIntegration` e incluiu indevidamente `DramaFindsLiveIntegrationTests`, que acessa a internet e falhou porque a API agora retorna resultados à consulta que o teste esperava vazia. Reexecução com o filtro por `FullyQualifiedName` passou na suíte isolada de Providers. A checagem live permanece não-gating e sua expectativa de payload precisa ser atualizada antes de executá-la como homologação externa.
- NebulaPython com Python 3.10: 581/581. Portal: 35/35. Uma chamada inicial de pytest a partir da raiz do monorepo falhou na coleta por `sys.path`; repetida no diretório `Tools/NebulaPython` passou. Isso é erro de invocação, não falha de teste. Status publicado no portal em `main`, commit final `770bf8b455ee3c9de737478045e1628b869f39e3`; Vercel Production `dpl_CPufMKYbJ6SHR7JLSxFLrboFU3aV` `READY`, JSON público HTTP 200 confirma evento `server-wide-validation-2026-10-17-0420` e evidência 35/35.
- Limites: validação local, sem instalação/restart, sem worker Nebula, transferência Telegram, dados de produção, testes de integração completos ou homologação visual. A instalação permanece em 12.1.21 enquanto fonte/stage estão em 12.1.22; não produzir nem anunciar release até completar os gates ainda abertos do roadmap e o smoke test controlado.
- Mapa técnico consultado; não houve mudança de módulos, APIs, classes, dependências ou fluxo produtivo nesta sessão.

### 17/10/2026 — Fila de download Nebula e admissão compartilhada Mongo

- `MediaValidator.refresh_cache` não limitava tentativas com falha: uma falha durante o cursor deixava `last_cache_time` antigo, e a checagem de cada próximo título repetia a consulta completa. Foi adicionado `last_cache_attempt_time` separado para respeitar o intervalo de 120 s sem descartar o último snapshot válido; cursor com `max_time_ms(15000)` limita consulta quando suportado pelo driver.
- Criada a barreira `nebula_admission` no Mongo, com `NebulaMongoAdmissionLease` no C# e `tools/mongo_admission.py` no Python. O backup Supabase reserva atomicamente o gate, drena leases de upload existentes e mantém novos uploads aguardando; upload workers (legacy, parallel e small) e `NebulaUploadEngine` adquirem/renovam/liberam leases. O resultado exige que os dois runtimes implantados compartilhem Mongo/configuração.
- Testes: NebulaPython completo 584/584; testes focados de startup e Supabase .NET Release 52/52; os dois novos testes de concorrência `RequiresMongo` compilam, mas foram ignorados nesta sessão por falta de Mongo de teste. Sem garantia de integração até executar os casos com Mongo real.
- Evidência operacional limitada: `C:\ProgramData\MulletaFlix\nebula\nebula.log` não é atualizado desde 11/10/2026; não permite diagnóstico atual do worker. A instalação responde 12.1.21 e a fonte é 12.1.22. Nenhum worker, configuração, instalação, restart ou mídia alterados; produção e primeiro download não foram validados.
- Mapa técnico atualizado na sessão correspondente; release e implantação permanecem pendentes.

### 17/10/2026 — Promoção Nebula de streaming para modo completo

- `StartEnvioAsync` agora serializa o startup e revalida o modo existente: um worker `Somente Streaming` não é aceito como Envio completo. Para engine nativa, listeners anteriores são liberados antes da recriação; o serviço Supabase é reutilizado durante a transição.
- O host de lease do cache Python também é descartado se o processo não existir. Testes Release focados de startup: 39 aprovados, 0 falhas/ignorados.
- `pytest` do helper de mount indisponível neste Python (módulo não instalado), portanto não contado como aprovação. O log confirma WinError 10048 em 2121 e retries de mount, mas não identifica o processo proprietário. Nenhum processo foi encerrado; sem instalação/restart nem transferência real. Homologação de produção pendente.
- Mapa técnico conferido em 17/10/2026: fluxo interno de startup em `NebulaFtpManager` atualizado; sem nova API, dependência ou classe runtime.

### 18/10/2026 — Reuso baseado em runtime vivo no Nebula

- O log histórico confirma modo somente streaming e falha posterior de bind `127.0.0.1:2121` (WinError 10048), mas não confirma chamada/readiness do Downloader STRM. `StartEnvioAsync` agora decide reuso/promoção pela saúde do host/processo sob `_envioLock`, não pela flag lógica isolada; isso evita tentar criar um segundo runtime quando o handle permanece vivo e corrige a flag após reuso.
- Regressão testa a decisão de reuso usando runtime vivo, modo atual e modo pedido. Testes Release de `NebulaDownloaderStartupTests`, `NebulaHostedServiceConfigurationTests` e `NebulaSupabaseSyncTests`: 95/95, 0 ignorados/falhas; build incluiu warnings preexistentes em outros testes/analyzers. Retry do cursor Supabase tem cobertura de 500→200, 503 após três tentativas e 401 sem retry.
- Mapa: mesmos módulos, classes públicas, APIs e dependências; alterada a condição interna do fluxo de startup `NebulaFtpManager.StartEnvioAsync`. Runtime de serviço completo concorrente não foi reproduzido; instalação atual, resolução de listener 2121 e transferência real permanecem pendentes. Sem acesso/alteração de produção e sem release.

### 17/10/2026 — Cobertura unitária da lease Mongo de admissão

- Adicionado `NebulaMongoAdmissionLeaseTests.cs` cobrindo cancelamento quando o heartbeat perde a lease ou lança exceção, mais descarte idempotente que interrompe o heartbeat e libera a lease uma vez.
- Evidência .NET Release: teste focado 3/3; projeto `Jellyfin.Server.Implementations.Tests` completo 1.455 aprovados, 62 ignorados, 0 falhas. NebulaPython 3.10: 584/584.
- Os casos não exercitam Mongo real nem transferência Telegram. Runtime continua 12.1.21 e candidato/stage completo, instalação e primeiro download seguem pendentes. Não é aprovação para release.
- Mapa técnico consultado em 17/10/2026; a alteração desta rodada é somente teste, sem mudança adicional em módulos, APIs, classes runtime, dependências ou fluxo produtivo.

### 18/10/2026 04:42 (-03) — Promoção Python do streaming para Envio mantinha porta FTP ocupada (T2.1)

- Evidência do log: worker `main.py` iniciou em Somente Streaming; uma inicialização posterior de Envio falhou ao vincular FTP `127.0.0.1:2121` com WinError 10048. No código, a rotina de transição só parava o worker anterior quando `UsePythonEngine=false`; na configuração Python, ele continuava ocupando 2121 enquanto um novo processo era iniciado.
- Correção: parar o worker Python existente também na promoção stream-only → Envio completo, antes de iniciar o modo full; no runtime nativo, continuar liberando os listeners anteriores. Adicionado teste de condição da transição.
- Evidência: testes Nebula de startup/hosted/mount C# `Release` 49/49; ciclo de vida/health/mount Python 3.10 16/16; portal independente 80/80 e espelho 65/65; `git diff --check` aprovado. Uma coleta Python inicial falhou por não incluir o diretório de runtime no `PYTHONPATH`, repetida corretamente e aprovada.
- Limite: produção continua em 12.1.21; PID que possui listeners foi preservado. Não houve restart, instalação, mount ou download real. O código fonte corrige a causa de transição, mas operação e release continuam sem homologação. Sem alteração estrutural de módulos, APIs, classes ou dependências.
- Homologação do portal publicada em `Mulletaflix-Portal`, commit `e2ce7ee663372d2e0635d57f29ac6f307c280c41`; GET público HTTP 200 confirmou o evento `in_progress`.

### 18/10/2026 08:12 (-03) — Admissão de backup aguarda upload, integração cruzada pendente

- Revisão atual da fonte: `NebulaSupabaseSyncService` reserva admissão exclusiva antes do backup; `NebulaMongoContext` bloqueia novos uploads e drena leases com `expires_at > now`. Python renova lease em heartbeat de 30 s e a expiração configurada é de 5 min. Isso torna plausível uma espera recorrente durante upload ativo, mas o log anexado não permite saber se a lease tem proprietário ativo.
- NebulaPython 3.10 completo 592/592. Três testes C# reais de Mongo foram compilados e ignorados porque o endpoint de teste `127.0.0.1:27099` não está disponível. Existe cobertura separada C#/fake, mas não interleaving C#↔Python no mesmo Mongo descartável. Evitar usar Mongo de produção; falta ambiente isolado para fechar T4.2.
- Estrutura revisada em `NebulaSupabaseSyncService`, `NebulaMongoContext`, `NebulaMongoAdmissionLease` e `Tools/NebulaPython/tools/mongo_admission.py`: sem novos módulos, APIs, classes ou dependências nesta auditoria.

### 18/10/2026 11:26 (-03) — Revalidação ampla de testes, gates operacionais continuam abertos

- Execuções Release concluídas nesta rodada: `Jellyfin.Server.Implementations.Tests` 1.498 aprovados, 63 ignorados, 0 falhas; `Jellyfin.Api.Tests` 730/730; `Jellyfin.Providers.Tests` 790/790; NebulaPython 3.10 592/592. Gate de integridade do pacote PowerShell aprovado; portal homologação 102/102.
- Os 63 testes ignorados em Implementations incluem cenários dependentes de serviços externos/ambiente e não contam como validação. Não há Mongo isolado disponível em `127.0.0.1:27099`; nenhum banco de produção foi usado. Não foram iniciados worker, upload/download, instalação ou restart.
- Fato operacional ainda pendente: API local `8096` estava indisponível na última leitura; listeners antigos `2121/2123` foram preservados. O log anexado é histórico e não comprova StartDownloader, readiness, scan ou transferência.
- Resultado: suites determinísticas locais aprovadas; homologação global e release não aprovadas. Permanecem pendentes CI remoto, integrações com Mongo/Supabase em ambiente isolado e smoke test controlado do downloader instalado.
- Mapa técnico consultado nesta rodada; não houve alteração de módulos, APIs, classes, dependências ou fluxo runtime nesta atualização de evidências.

### 18/10/2026 11:29 (-03) — Anexo de startup confirma adiamentos e conflitos, não scan/download STRM

- O log anexado tem 516 linhas. Backup inicial Supabase completou: 255 arquivos, 7 usuários FTP e 28 tokens; usuários do aplicativo não incluídos. Depois aparecem 248 mensagens de adiamento do backup automático por uploads Telegram ativos, embora o processo Python registre “somente streaming” e feeder/fila/workers de upload desativados. Isso sugere leases/atividade de outro processo ou runtime antigo; o log não identifica proprietário nem lease Mongo.
- O mesmo anexo registra duas colisões WinError 10048 em `127.0.0.1:2121` e 14 saídas do helper de montagem porque outro processo já tinha o lock; não confirma N: saudável. Só existe mensagem de scanner de pastas de stage; não há `StartDownloader`, readiness Mongo ou scan STRM de biblioteca.
- Busca na fonte atual não encontra a mensagem literal de adiamento de 30 s. O log é de versão/build anterior ou de componente distinto da fonte atual. Não é seguro inferir qual lease ou processo causou a espera nem encerrar o worker, pois pode servir streams.
- Testes locais desta sessão: Implementations 1.498 aprovados/63 ignorados; API 730/730; Providers 790/790; NebulaPython 592/592. Eles não reproduzem a situação operacional do anexo. Sem mudança de código ou produção, sem download real e sem release; próximo passo é coletar build/versão instalada e telemetria de leases em janela controlada.
- Mapa técnico conferido; arquitetura inalterada nesta análise.

### Decisão do usuário sobre resultado ambíguo do Telegram — 2026-10-21

- Preferência confirmada: manter retentativas automáticas quando o resultado do envio Telegram for ambíguo.
- A decisão não elimina o risco de duplicata após aceite remoto sem checkpoint Mongo. Implementação local agora combina retry automático com journal e reconciliação antes de reenviar; retry não equivale a entrega exactly-once.
- Nenhum upload ou Telegram real foi acessado nesta validação. Execução simulada e limites estão registrados nesta seção e em docs/AUDIT-EVIDENCE.md.
# Mídia STRM — contenção de M3U/manifesto — 2026-10-22

- `MidiaStorageOnlinePathGuard` é compartilhado pelo controller manual e `MidiaStorageOnlineSyncTask`: rejeita caminhos vazios, absolutos, malformados e traversal, canonicaliza destinos dentro da raiz configurada e recusa reparse points na cadeia da raiz e nos componentes existentes abaixo dela.
- O cleanup do manifesto valida cada chave antes de remover arquivo e diretórios vazios. A migração de caminhos legados usa a mesma validação.
- Escritas `.strm` gravam em temporário irmão criado exclusivamente e substituem por rename; downloads usam temporários aleatórios `CreateNew` e rename de substituição. Isso evita seguir hard links já colocados no nome final/temporário.
- Limite explícito: validação de pathname e abertura/remoção ainda são operações distintas; a condição de corrida local TOCTOU não está fechada. Não é homologação de ACL, instalação, runtime ou produção.

## Serialização dos syncs manual e agendado — 2026-10-23

- `MidiaStorageOnlineController.Sync` e `MidiaStorageOnlineSyncTask` adquirem o mesmo lease assíncrono, indexado pelo caminho canonicalizado de `sync_manifest.json`, antes de ler o manifesto e materializar arquivos; o lease cobre a gravação final e é liberado também em cancelamento/exceção.
- Raízes de manifesto distintas podem sincronizar em paralelo. Para a mesma raiz, um sync aguarda o outro, evitando que snapshots simultâneos sobrescrevam entradas no manifesto ou disputem os mesmos arquivos.
- O lease tem espera limitada a 15 minutos; timeout encerra a tentativa em espera, não o sync ativo. O manifesto é salvo em arquivo irmão temporário e substituído atomicamente; falha é propagada e impede marcar a execução como sucesso.
- `MidiaStorageOnlineSyncGateTests` cobre exclusão por caminho equivalente, independência entre raízes, cancelamento e timeout. Build Providers Release passou sem avisos/erros; testes focais passaram 69/69 e a suíte Providers sem Integration 858/858. Ver [[Sessões/2026-10-23-MulletaFlix-media-sync-concurrency]].
- Limites: lease é intra-processo; processos independentes ainda não coordenam a escrita. Timeout de espera não garante prioridade FIFO. A revisão não exercitou duas sincronizações completas contra biblioteca real, instalação ou produção, e não fecha TOCTOU de operações por pathname.
- Estrutura/fluxo mudaram com novo helper interno; sem endpoint, dependência, DI ou alteração de contrato HTTP. Evidência: `MediaBrowser.Providers/Plugins/MidiaStorageOnline/MidiaStorageOnlineSyncGate.cs`, `MidiaStorageOnlineController.cs` e `ScheduledTasks/MidiaStorageOnlineSyncTask.cs`.

### Revalidação de timeout e persistência do manifesto — 2026-10-23

- Após revisão, o gate limita espera a 15 minutos; não cancela o sync ativo. A gravação do manifesto usa substituição atômica, e falhas são propagadas para que os fluxos manual/agendado registrem falha em vez de sucesso.
- Testes focais Release (gate, path guard, TMDb) 69/69; suíte Providers sem Integration 858/858; build `MediaBrowser.Providers` Release 0 avisos/0 erros; `git diff --check` passou.
- A espera limitada não promete FIFO; coordenação entre processos, sync concorrente real, instalação, Linux, CI remota e produção continuam sem validação.

## Restore: temporário preexistente ligado por hard link — 2026-10-22

- `FileSystemRestoreTransaction.ApplyEntryAsync` descarta o staging sidecar `.tmp` que pode ter sobrado de interrupção e cria novamente com `FileMode.CreateNew`. ZIP já verificado pelo journal segue como fonte de verdade do replay.
- Regressão prepara journal válido, arquivo sentinel fora da raiz e hard link no caminho determinístico `.tmp`; recuperação completa sem alterar sentinel e instala conteúdo do backup. `FullSystemBackup` Release passou 55/55.
- Isso evita truncamento pelo nome `.tmp` preexistente, mas não resolve operações absolutas nem troca concorrente de diretórios; Linux, MariaDB, runtime, ACL efetiva e produção não validados.

## Upload de letras — proteção contra hard link no destino — 2026-10-22

- `LyricManager.TrySaveToFiles` agora escreve em arquivo temporário irmão com `CreateNew`, fecha o stream e substitui o destino por rename. A gravação deixa de abrir/truncar o inode que outro nome pode referenciar por hard link.
- Regressão `TrySaveToFiles_ReplacesHardLinkWithoutTruncatingItsOtherTarget` verifica que o conteúdo do nome vinculado original permanece intacto e que o destino recebe a nova letra.
- A troca por pathname não fecha TOCTOU contra ator local que possa modificar a pasta entre criação e rename. A substituição de arquivo também pode alterar permissões/ACL explícitas associadas ao inode antigo; política e preservação de metadados de segurança requerem validação adicional. Nenhuma instalação/produção/release.
- Verificação em Windows/.NET 10 Release: `LyricManagerPathSafetyTests` 21/21; suíte `Jellyfin.Providers.Tests` 847/847, sem ignorados/falhas. O caso de hard link é ignorado apenas se o filesystem/host negar sua criação.
### Restore: identidade do arquivo aberto — 2026-10-22

- `BackupRestoreRecoveryJournal.VerifyArchiveAsync(entry, stream)` compara
  tamanho/SHA-256 do stream aberto e restaura sua posição. O restore normal e o
  replay após interrupção usam essa verificação antes das mutações; restore novo
  também confirma que o pathname externo ainda corresponde ao journal.
- Testes cobrem pathname A→B, stream divergente, timestamp alterado com bytes
  iguais e posição preservada. Rebuild final `FullSystemBackup` Release passou
  58/58; format check focado passou nos dois arquivos de runtime e no teste.
  A verificação ampla dos projetos sinaliza arquivos não relacionados do
  worktree fora do formato; `git diff --check` passou.
- Escopo limitado à época: alteração do mesmo inode após hash, snapshot
  privado e operações por pathname da árvore de destino ainda mantêm TOCTOU.
  O lease interprocesso foi adicionado depois, como barreira parcial documentada
  abaixo. Sem homologação Linux/MariaDB/produção; formal 1/7, sem release.
  Ver `docs/backup-coverage-audit.md`.

## Restore: lease single-writer — 2026-10-22

- O journal mantém um arquivo de lease estável aberto com `FileShare.None`. Os dois entrypoints de restore adquirem a exclusão antes de ler o journal e a mantêm até concluir replay/restore; o gate em memória continua protegendo a instância.
- `FullSystemBackup` Release 61/61 em Windows/.NET 10; format checks focados e `git diff --check` passaram. Regressões verificam contenda entre processos e aquisição antes de leitura do journal.
- A garantia é limitada a processos cooperativos no mesmo caminho e filesystem. Troca adversarial do diretório/arquivo, ACL efetiva, Linux/mounts e ordem completa de inicialização do host ainda não foram verificados. Não fecha TOCTOU nas operações da árvore de destino. Formal 1/7; sem instalação, produção, CI remota ou release.

## Reauditoria do restore — regressão focada — 2026-10-24

Na `main` local suja (`64a33dcf`), a suíte `FullSystemBackup` Release passou 88, ignorou 9 testes de plataforma e falhou 0/97 em Windows/.NET 10. O teste confirma regressão funcional existente, não a fronteira de segurança. A transação ainda usa pathname em apply/rollback/cleanup; `BackupRestoreDirectory` está parcialmente integrado ao journal, sem validação da ACL efetiva de roots/ancestrais/journal/ZIP. P0 aberto, homologação formal 2/7 sem crédito; sem Linux nativo, restore real, instalação, produção ou release. Ver `docs/backup-coverage-audit.md`.

## Restore — falha de cleanup mantém journal — 2026-10-24

`FileSystemRestoreTransaction.Commit` não marca o commit nem permite apagar o journal quando cleanup de sidecars falha. O serviço preserva a transação e solicita restart/replay, inclusive restore sem banco relacional; rollback só permite excluir o journal se rollback e limpeza pré-commit terminaram. Teste Windows bloqueia deleção do backup aberto e comprova replay posterior. `FullSystemBackup` 89 aprovados, 9 ignorados, 0 falhas/98; foco repetido 3/3; portal atualizado e seus testes 90/90. Isso evita perder o ponto de recuperação por falha de cleanup, mas não fecha TOCTOU/ACL: integração por handles, ACL efetiva e Linux nativo seguem P0 pendente.

## Journal sem bootstrap por pathname — 2026-10-24

`BackupRestoreRecoveryJournal.AcquireOperationLease` e `WriteAsync` agora abrem diretamente a raiz `ProgramDataPath` já criada pelo bootstrap de aplicação, sem executar `Directory.CreateDirectory` por pathname antes da autoridade por handles. Raiz ausente falha fechado e não é criada pelo journal. `FullSystemBackup` Release passou 91/100 em Windows/.NET 10, com 9 testes Linux/macOS ignorados e zero falhas. Isso remove uma mutação anterior à abertura segura; não corrige pathname na transação, verificação de ACL nem races integradas. Homologação formal 2/7, sem crédito; P0 continua aberto.

## Reauditoria do lifecycle por handles — 2026-10-24

Revisão independente e leitura da fonte confirmam que `BackupRestoreDirectory` ainda não participa de apply, rollback, cleanup ou replay; `FileSystemRestoreTransaction` continua pathname-based. O helper não valida ACL efetiva e faltam operações atômicas/consulta de estado para sidecars e rollback. `FullSystemBackup` Release reexecutado: 91 aprovados, 9 ignorados de plataforma, 0 falhas/100. WSL/Linux nativo indisponível neste host. Próxima entrega precisa tratar transação inteira e política de ACL como unidade, com testes de corrida em Windows e Linux; sem crédito formal, runtime ou release.

## Restore — precedência de erro e lease — 2026-10-24

A validação da solução detectou uma falha de contrato quando o ZIP e a raiz ProgramData estavam ambos ausentes. `RestoreBackupInternalAsync` mantém a aquisição do lease antes de examinar o backup; se a abertura da raiz falhar com erro nativo 2/3 e o ZIP solicitado também não existir, converte para `FileNotFoundException`. Não converte erros de contenda/compartilhamento nem recuperação pendente. Testes focais: ausência do backup 1/1 e lease ocupado 2/2. Solução Release: 5.260 aprovados, 261 ignorados, zero falhas/5.521 no Windows/.NET 10. O mapa técnico foi conferido e permanece estruturalmente igual: mudou apenas o tratamento interno do fluxo privado de restore, sem novo tipo, API, dependência ou serviço. Homologação formal 2/7 sem crédito; TOCTOU/ACL P0, Linux nativo e validação de runtime/produção seguem pendentes. Nenhuma release ou publicação remota.

### 24/10/2026 — Lock da exclusão de versões locais órfãs

- `SaveLinkedChildren` pode remover `BaseItemEntity` de uma versão local que deixou de pertencer ao vídeo principal. A exclusão relacional também remove imagens ligadas ao item; antes, o lote só travava IDs de entrada, pai e owner, não necessariamente o próprio órfão.
- A rotina agora devolve as stripes faltantes antes de concluir o lote. `UpdateOrInsertItems` descarta a transação ainda não confirmada, libera as stripes atuais e repete com todas as stripes distintas em ordem crescente. Isso coordena a remoção com `SaveImagesAsync`, `SaveItems` e a exclusão explícita.
- Regressão MariaDB `SaveItems_WaitsForOrphanedLocalAlternateVersionPersistenceLock` usa uma stripe retida para confirmar que a remoção aguarda o mesmo item; o projeto compilou, mas o teste foi ignorado localmente sem conexão descartável.
- Release Windows/.NET 10: `ItemPersistenceServiceTests` 31/31; assembly `Jellyfin.Server.Implementations.Tests` 1.609 aprovados, 76 ignorados, zero falhas/1.685. O log anexado não identifica processo/binário e não prova que esse caminho causou os deletes observados. Banco de produção, runtime instalado e CI remota não foram consultados.
- Sem nova classe pública, módulo, endpoint, dependência ou DI. O fluxo interno da persistência existente mudou. Formal 2/7 (28,6%), sem crédito; sem instalação, produção ou release.

Atualização 24/10/2026 07:07 BRT: MariaDB descartável 4/4; trigger de `ActivityLogs` injeta falha de INSERT no restore e confirma rollback do DB preservando configuração e registros live. Suíte completa Server.Implementations Release recompilada 1.598 aprovados/75 ignorados/0 falhas (1.673); `FullSystemBackup` 91 aprovados/9 ignorados/0 falhas. Mapa conferido: nenhuma classe, API, dependência ou topologia de runtime mudou; apenas fixture de teste. ACL, cenários gerais de falha/crash de arquivos, TOCTOU integrado e Linux nativo continuam pendentes; formal 2/7 sem crédito e sem release.
# Releitura de log de persistência de imagens — 2026-10-24 12:00 BRT

O log recebido repete MariaDB 1213 em operações concorrentes `DELETE BaseItemImageInfos` para um mesmo `ItemId`, retries e alerta posterior de atraso do heartbeat Kestrel. A linha HTTP 200 antecedente não confirma o commit dessas alterações.

No checkout atual, `ItemPersistenceService.SaveImagesAsync` usa lock em processo por faixa e `ItemPersistenceDatabaseLock.AcquireAsync` mantém conexão MariaDB com advisory `GET_LOCK` durante a transação. Isso é evidência sobre a fonte local, não sobre o binário que produziu o log. O anexo não informa PID nem versão; runtime antigo ou writer fora dessa coordenação seguem hipóteses. Não se acessou o banco ativo, não se alterou runtime nem se gerou release nesta revalidação. O worktree continua divergente do remoto; não publicar como release integral.

Validação de portal: 109/109 testes; JSON parseável; `git diff --check` sem erros, com avisos de finais de linha. Homologação formal continua 2/7 (28,6%).

## Reconciliação da rotina FixDates — 2026-10-24

Leitura do anexo 14e0dd32 confirma novamente deadlocks MariaDB 1213 em DELETEs concorrentes de `BaseItemImageInfos` para um mesmo `ItemId`; o status HTTP 200 antecedente não comprova commit. A memória do projeto e o mapa afirmavam que `FixDates` adquiria as 16 advisory stripes, mas essa proteção não estava presente na fonte deste checkout. A rotina agora adquire as 16 stripes compartilhadas antes de ler/atualizar as datas e mantém o lock até `SaveChangesAsync`; a coordenação cobre sessões MariaDB que usam o mesmo helper e schema.

Adicionada regressão MariaDB que segura a stripe 15 em outra sessão e confirma `FixDates` aguardando o lock; incluída no job Linux MariaDB e protegida por contrato Node. `ItemPersistenceDatabaseLockTests` Release passou 9/9; o teste de integração compila mas foi ignorado sem `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`; contrato CI passou 2/2. Isso não prova causa definitiva do log, execução multiprocesso, outro escritor externo, runtime instalado ou produção. Nenhum banco ativo foi acessado e nenhuma release foi criada. Homologação formal permanece 2/7 (28,6%), sem crédito.
# Regressão Nebula Python Windows — 2026-10-24 12:05 BRT

`Tools/NebulaPython/tests` passou 694/694 em Windows com Python 3.10, versão que corresponde à matriz Windows do CI. O teste rodou em venv temporário, com runtime instalado do `requirements.lock` por hashes e requisitos de teste já declarados; `pytest` não usou cache e bytecode foi desativado. Nenhum MongoDB, Telegram, Supabase, runtime operacional ou produção foi acessado. Não foi gerado relatório de cobertura nem executado Linux/macOS. A tentativa de remover a venv temporária foi recusada pela ferramenta; não houve tentativa de contornar essa proteção. O fluxo Nebula não mudou; esta verificação não resolve o restore P0 nem dá crédito formal (2/7). Portal local registra o resultado.

## Revalidação do anexo de deadlock de imagens — 2026-10-24 12:12 BRT

O anexo recebido nesta sessão tem timestamp 23/10/2026 20:36 e repete `DELETE BaseItemImageInfos` concorrente para o mesmo `ItemId`, MariaDB 1213, retries e heartbeat Kestrel atrasado. A linha HTTP 200 antecedente não comprova commit da gravação. A fonte local atual usa lock por stripe e `GET_LOCK` MariaDB durante a transação; `ItemPersistenceServiceTests` Release foi reexecutado e passou 31/31. Contrato do portal passou 111/111, JSON parseável e `git diff --check` passou nos arquivos versionados tocados. Como o log não identifica PID/assembly e nenhum runtime/MariaDB foi consultado, não se conclui que produção contenha a correção nem se descarta outro writer. Sem alteração de código ou topologia; o mapa técnico foi conferido. Formal 2/7, sem crédito; restore ACL/TOCTOU P0 permanece aberto. Ver sessão do vault `2026-10-24-MulletaFlix-image-deadlock-log-recheck-1212`.

## Restore P0 — reauditoria independente de ACL e lifecycle — 2026-10-24 12:17 BRT

Duas auditorias independentes e conferência direta da fonte confirmam que `BackupRestoreDirectory` não governa `FileSystemRestoreTransaction`: os cinco roots são aplicados sequencialmente e cada target/sidecar é validado e mutado por pathname; rollback, replay e cleanup seguem a mesma transação. O helper é usado pelo journal/lease, mas não consulta ACL/owner efetiva. ZIP e identidade/replay ainda têm consultas/reaberturas pathname-based. Não foram encontrados testes ACL/owner ou fail-closed. Isso contradiz o contrato adotado (service + root/admins; shared/inconclusivo fail-closed), portanto P0 continua aberto. Revisão estática apenas; sem execução Linux/Windows ACL adversarial, restore, runtime ou produção. Sem alteração de código/topologia; mapa técnico conferido. Portal testado em 112 casos após registro. Ver `docs/backup-coverage-audit.md`, a decisão `2026-10-24-restore-acl-trust-boundary` e sessão do vault `2026-10-24-MulletaFlix-restore-p0-independent-crosscheck-1217`.

## Deadlock de imagens — regressão concorrente MariaDB — 2026-10-24 12:20 BRT

O anexo recebido continua sendo o mesmo log de 23/10/2026 20:36: `DELETE BaseItemImageInfos` concorrente para o mesmo item, deadlock 1213 e heartbeat Kestrel atrasado; não revela PID/assembly nem identifica o runtime. Revisão detectou teste concorrente de 12 refreshes já existente; o teste redundante de 16 foi removido e substituído por cenário que retém `GET_LOCK` em outra sessão enquanto chama `SaveImagesAsync`. O teste compila em Release, mas foi ignorado por falta de `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`. `ItemPersistenceServiceTests` passou 31/31. Sem MariaDB real de teste, isso ainda não prova a execução do caminho. Sem acesso à produção. Formal 2/7 sem crédito; mapa técnico conferido e sem mudança estrutural. Ver evento do portal `server-image-deadlock-regression-test-2026-10-24-1220`.

## CI MariaDB Linux — executar teste do advisory lock de imagens — 2026-10-24 12:26 BRT

Não existe runtime MariaDB local, serviço, Docker/Podman ou WSL configurado; o teste de integração não pode ser exercitado localmente sem instalar infraestrutura. O workflow `.github/workflows/ci.yml` já sobe MariaDB 11.4 isolado no runner Linux; o filtro passou a incluir `SaveImagesAsync_WaitsForDatabaseAdvisoryLockHeldByAnotherSession`, preservando o cenário de refreshes concorrentes e restore crash recovery. O teste-contrato `ci-mariadb-linux-coverage.test.mjs` agora exige essa inclusão e passou 2/2. O workflow remoto ainda não rodou; isso não é evidência de resultado MariaDB. Formal 2/7 sem crédito, runtime/produção não acessados, release não criada. Sem mudança na topologia técnica do servidor. Ver evento `server-image-advisory-lock-ci-2026-10-24-1226` no portal.

## Revalidação local do deadlock de imagens — 2026-10-24 12:58 BRT

O teste focal `ItemPersistenceServiceTests` foi recompilado em Release e passou 31/31. Três testes de integração que exercitam refreshes concorrentes e `GET_LOCK` em outra sessão foram selecionados, mas ignorados porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` está ausente; nenhum banco foi acessado. O host não apresenta serviço/cliente MariaDB detectável. A fonte local contém a serialização por stripe e advisory lock MariaDB; não há evidência de que o runtime que gerou o log use essa versão. Contrato do portal passou 118/118 e o JSON parseou. Próximo gate é CI/MariaDB descartável e identificação de versão/assembly do servidor. Sem mudança de runtime, módulos, APIs ou dependências. Mapa técnico conferido e estrutura inalterada; homologação formal 2/7 sem crédito, sem release ou validação de produção. Ver evento do portal `server-image-deadlock-local-revalidation-2026-10-24-1258` e sessão do vault.

## Revalidação local do deadlock de imagens — 2026-10-24 12:58 BRT

O teste focal `ItemPersistenceServiceTests` foi recompilado em Release e passou 31/31. Três testes de integração que exercitam refreshes concorrentes e `GET_LOCK` em outra sessão foram selecionados, mas ignorados porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` está ausente; nenhum banco foi acessado. O host não apresenta serviço/cliente MariaDB detectável. A fonte local contém a serialização por stripe e advisory lock MariaDB; não há evidência de que o runtime que gerou o log use essa versão. Próximo gate é CI/MariaDB descartável e identificação de versão/assembly do servidor. Sem mudança de runtime, módulos, APIs ou dependências. Mapa técnico conferido e estrutura inalterada; homologação formal 2/7 sem crédito, sem release ou validação de produção. Ver evento do portal `server-image-deadlock-local-revalidation-2026-10-24-1258` e sessão do vault.

## Regressão completa da solução — 2026-10-24 13:10 BRT

`dotnet test MulletaFlix.sln --configuration Release --no-restore --verbosity minimal -m:1` terminou com exit code 0 em Windows/.NET 10: 5.272 aprovados, 267 ignorados, 0 falhas, 5.539 resultados em 17 projetos. Ignorados incluem integrações de bancos opt-in, cenários Linux/macOS e providers live. Isso não valida runtime instalado, produção, ACL efetiva nem restore adversarial Linux. `FileSystemRestoreTransaction` ainda executa apply/rollback/replay/cleanup por pathname; P0 de restore permanece aberto. Homologação formal segue 2/7 (28,6%), sem crédito. Mapa técnico conferido e estrutura inalterada; sem release/publicação remota. Ver evento `server-full-solution-regression-2026-10-24-1310` no portal.

## Restore ACL — pesquisa de APIs nativas — 2026-10-24 13:15 BRT

`GetSecurityInfo` exige `READ_CONTROL` no handle Windows. `acl_get_fd` no Linux depende de libacl e pode retornar `ENOTSUP`; `fstatfs` não prova universalmente localidade. A abertura relativa existente usa `NtCreateFile`, API interna sujeita a mudanças. Nenhum código de runtime/dependência foi alterado; pesquisa documental não valida ACL efetiva. `FileSystemRestoreTransaction` continua pathname-based, P0 aberto e formal 2/7 sem crédito. Ver `docs/backup-coverage-audit.md`, decisão e sessão `2026-10-24-MulletaFlix-restore-acl-native-api-research-1315`.
Snapshot NuGet de 25/10/2026: 48/48 projetos .NET verificados com `--vulnerable --include-transitive`, 0 vulnerabilidades conhecidas e 0 erros; Jellyfin.Api.Fuzz precisou de restore individual. Ver `dotnet-dependency-security-audit.md`. Não houve upgrade; resultado não certifica runtime/produção nem dados de advisory futuros.

## Rechecagem do anexo de deadlock e regressão local — 2026-10-25 22:48 BRT

O anexo tem o mesmo SHA-256 do log de 23/10/2026 (53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2): DELETEs concorrentes de BaseItemImageInfos para o mesmo item, MariaDB 1213 e heartbeat Kestrel atrasado; o HTTP 200 antecedente não prova commit. A fonte atual continua usando stripe local por item, advisory GET_LOCK MariaDB e transação em SaveImagesAsync. A regressão Release de MulletaFlix.sln terminou exit 0: 5.209 aprovados, 83 ignorados, 0 falhas/5.292 resultados em 17 assemblies, com filtro FullyQualifiedName!~Integration; integração não foi executada nesta rodada. Isso não prova qual binário produziu o log nem valida runtime ou produção. PID/assembly, CI hospedada, MariaDB descartável nesta rodada e produção seguem pendentes. Mapa técnico conferido; nenhuma mudança estrutural. Homologação formal continua 2/7 (28,6%), sem crédito. Sem release.

## Rechecagem focal do deadlock de imagens — 2026-10-26 00:06 BRT

O anexo tem SHA-256 idêntico ao incidente já documentado. ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests passou 49/49 em Windows/.NET 10 Release; portal homologação 222/222. Fonte local possui lock por stripe e GET_LOCK, mas integração concorrente MariaDB, runtime emissor e produção continuam sem evidência. Homologação formal 2/7 (28,6%), sem crédito ou release. Ver docs/image-metadata-flow-audit.md e a sessão de 26/10 no vault.

\r\n
## Retentativa Nebula com campos de prazo coexistentes — 2026-10-26 07:58 BRT

Cobertura adicionada para documentos com retry_after e next_retry_at simultâneos: consumidores aguardam o prazo mais distante. NebulaPython completo passou 721 (2 ignorados); Category=RequiresMongo passou 32/32 em MongoDB 8.3.7 isolado; contrato do portal 260/260. Esta rodada alterou apenas testes. Não valida worker/runtime instalado, mídia/Telegram real, Linux, CI hospedada ou produção; homologação formal 2/7, sem crédito/release. Mapa conferido, sem mudança estrutural.

## Regressão completa da solução — 2026-10-26 08:08 BRT

dotnet test MulletaFlix.sln --configuration Release --no-restore --verbosity minimal -m:1 terminou com exit code 0 em Windows/.NET 10. Resultados visíveis: Server.Implementations 1.686 aprovados/86 ignorados; API 787/3; Providers 878/13; os demais projetos que reportaram resultado também terminaram sem falhas. O CLI não forneceu total global confiável na saída truncada; não somar parcialmente. Ignorados incluem Linux/macOS, Mongo/MariaDB opt-in, provedores live e benchmarks. Isso não valida Linux/CI hospedada, DB integrados, runtime instalado ou produção. Homologação formal 2/7 (28,6%), sem crédito/release.

## Persistência de imagens: estresse MariaDB — 2026-10-26 09:02 BRT

Regressão concorrente passou em MariaDB 11.4.13 descartável: classe `ServerPersistenceMariaDbTests` 15/15, repetição do cenário refresh/delete 10/10 e testes unitários de persistência/lock 43/43. O banco temporário encerrou; porta 33099 fechada e nenhum deadlock/1213 no log dessa instância. Isso valida o candidato local Windows/.NET 10, não runtime/produção. O anexo do incidente continua sem PID/assembly; a DLL instalada 12.1.35 não tem o marcador estático do lock, sem evidência suficiente para correlacioná-la. Formal 2/7; sem release. Portal contratual 267/267.

## Nebula C#: falha fechada do índice único da fila — 2026-10-26 09:11 BRT

A auditoria comparativa encontrou que `NebulaMongoContext.EnsureIndexesAsync` ainda capturava qualquer falha ao criar `queue_identity_1`, embora o scanner Python já propagasse o erro. O catch genérico foi removido. O único caller de `EnsureIndexesAsync`, `NebulaFtpManager.StartAsync`, trata a exceção e encerra a inicialização antes de subir o FTP. Novo teste semeia duas identidades iguais e confirma que a exceção não é ocultada. `NebulaQueueConcurrencyMongoTests` passou 26/26 contra MongoDB 8.3.7 descartável isolado em 127.0.0.1:27099; o teste focal passou 1/1. Instância encerrada e porta fechada. Não acessamos o Mongo operacional.

Risco operacional: duplicatas preexistentes podem impedir o índice e bloquear a inicialização após atualização. Elas ainda precisam ser inventariadas com backup e plano explícito; nada foi removido automaticamente. Sem CI Linux, runtime instalado ou produção; homologação formal 2/7 (28,6%), sem crédito ou release.


## Rechecagem do anexo de deadlock — 2026-10-26 16:28 BRT

O anexo tem SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2, igual aos logs anteriores. A suíte focal ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests recompilou e passou 50/50. A fonte local contém stripe e GET_LOCK antes da transação. Nenhum MariaDB de teste foi usado nesta sessão; o operacional em 3306 não foi acessado. O assembly do servidor está ausente no caminho esperado em Program Files, e o log não tem PID/assembly; runtime causador não identificado. Sem mudança de runtime/release ou crédito formal; homologação segue 2/7. Mapa técnico conferido; estrutura inalterada. Ver [[Sessões/2026-10-26-MulletaFlix-image-deadlock-log-recheck-1628]].

## Regressão ampla da solução — 2026-10-26 18:46 BRT
- [x] `MulletaFlix.sln` Release, sem restore, filtro `FullyQualifiedName!~Integration`: exit code 0 em Windows/.NET 10.
- [x] Resultados individuais confirmados na saída: Server.Implementations 1.705/1.798 (93 ignorados), Providers 881/881, Extensions 144/144, MediaEncoding.Hls 18/18, MediaEncoding.Keyframes 3/3, LiveTv 75/75, Common 31/31, MediaEncoding 71/71 e Naming 681/681.
- [ ] Runner truncou a saída; agregado global não recuperado. Não somar parciais nem converter skips em aprovações. Integrações filtradas; DBs opt-in e plataformas externas pendentes.
- [ ] Log do deadlock repete SHA-256 já conhecido: sem PID/assembly/versão e sem confirmação de commit. Persistência MariaDB integrada, Linux/CI hospedada, runtime e produção seguem pendentes.
- [ ] Homologação formal continua 2/7 (28,6%), sem crédito. Nenhuma release.

## Regressão ampla do servidor — agregado TRX reconciliado — 2026-10-26 18:52 BRT

Para recuperar o agregado confiável, a suíte foi repetida com `--results-directory` temporário e logger TRX por projeto. Execução terminou exit code 0: 17 TRX, **5.397 Passed, 96 NotExecuted, 0 Failed, 5.493 resultados**. Os 96 skips foram classificados pelas mensagens registradas: 37 MongoDB opt-in, 32 MariaDB opt-in, 24 plataforma/SO, 2 benchmarks e 1 dependente de `/dev/shm`. O contrato do portal passou novamente 297/297. A métrica automatizada do portal agora reflete esse agregado local; ela é distinta da homologação formal, que permanece 2/7 (28,6%). Integrações foram filtradas e os skips não contam como aprovações. A evidência é do checkout local sujo em Windows/.NET 10; não valida branch remoto, Linux/CI hospedada, runtime instalado ou produção. Nenhuma release foi criada.

## Revalidação do contrato do portal — 2026-10-26 18:56 BRT

Ao atualizar `testAudit`, a suíte apontou asserções acopladas ao agregado anterior. Os checks que leem o estado atual foram atualizados para 5.397/5.493, mantendo os eventos históricos intactos e a homologação formal separada; um teste garante a nova contagem TRX e que o gate formal permanece 2/7. O contrato passou **298/298**, com zero falhas. `git diff --check` passou; Git emitiu somente avisos de normalização CRLF/LF em arquivos já alterados. Arquivo atualizado: `portal-site/tests/homologacao.test.mjs`.

## Regressão MongoDB opt-in e correção do ambiente de CI — 2026-10-26 18:58 BRT

A suíte `Category=RequiresMongo` foi executada em MongoDB 8.3.7 descartável, bind apenas em `127.0.0.1:27099`. A primeira execução teve 36/37; o caso C#/Python falhou porque `NEBULA_TEST_PYTHON` apontava para o interpretador global e o guard do servidor corretamente exigiu venv. Repetido com a venv Nebula isolada e dependências travadas, o caso cruzado passou 1/1 e a suíte completa passou **37/37, zero skips/falhas**. O processo descartável foi encerrado e a porta 27099 fechou; a instância operacional em 27017 não foi usada.

Corrigi `.github/workflows/ci.yml`: o job Mongo agora cria venv sob `runner.temp`, instala `requirements.lock` usando `--require-hashes` dentro da venv e fornece esse Python ao teste cruzado. `tools/release/tests/ci-mongo-coverage.test.mjs` agora verifica a venv e a instalação isolada. Teste contratual do workflow passou 1/1. Regressão ampla anterior mais 37 cenários Mongo reexecutados: 5.434 aprovados, 59 NotExecuted, 0 falhas/5.493 casos; os 59 incluem 32 MariaDB opt-in, 24 plataforma/SO, 2 benchmarks e 1 `/dev/shm`. O portal registra as execuções separadamente e explica o agregado reconciliado. A imagem local Mongo 8.3.7 difere da imagem 7.0 configurada na CI, cuja execução remota ainda falta. Formal 2/7 (28,6%), sem crédito; sem runtime/release/produção. Mapa conferido, sem mudança estrutural.

## Validação MariaDB local — testes opt-in e filtro de CI — 2026-10-26 19:03 BRT

Uma instância MariaDB 11.4.4 descartável foi iniciada com datadir temporária e bind restrito a `127.0.0.1:33099`. `Jellyfin.Server.Implementations.Tests` completo: 1.736 aprovados, 62 ignorados, 0 falhas/1.798; 31 dos 32 casos antes ignorados por MariaDB foram executados e passaram. O único restante exige Linux com filesystem case-sensitive.

O filtro usado pelo job MariaDB da CI em `IntroSkipper.Integration.Tests` passou 17/17, abrangendo crash recovery, schema dedicado, backup participant e concorrência/locks na persistência de imagens. O daemon de teste foi encerrado depois de validar PID, binário e datadir; porta 33099 fechada. Processo operacional na 3306 permaneceu PID 31236, sem conexão ou alteração.

Combinando resultados de execuções locais anteriores: regressão ampla 5.397/5.493, Mongo `RequiresMongo` 37/37 e MariaDB 31 dos 32 skips reconciliados: 5.465 aprovados, 28 NotExecuted, 0 falhas de 5.493 casos distintos. Os 17 testes IntroSkipper são evidência suplementar fora do denominador. CI Linux hospedada, caso Linux case-sensitive, runtime instalado e produção seguem pendentes. Portal atualizado; 99,5% é métrica de automação acumulada, não homologação formal; gate formal permanece 2/7 (28,6%). Sem release.

Revalidação final do registro 19:03: a suíte do contrato `portal-site/tests/homologacao.test.mjs` passou **298/298**, incluindo o evento MariaDB e a distinção entre a métrica técnica (99,5%) e homologação formal (2/7). `git diff --check` passou sem erros; apenas avisos de normalização CRLF/LF em arquivos sujos. Portas verificadas após shutdown: 33099 e 27099 fechadas; apenas os listeners operacionais 3306 PID 31236 e 27017 PID 4700 continuam presentes e intocados.

## Cobertura NuGet do projeto de recovery — 2026-10-26 19:19 BRT

O contrato `ci-nuget-audit-coverage.test.mjs` revelou que `tests/Jellyfin.Server.StartupRecoveryProbe/Jellyfin.Server.StartupRecoveryProbe.csproj` estava fora da solução e também fora da lista explícita do job de auditoria. Adicionado ao loop `Audit NuGet dependencies outside solution`; o job faz restore tratando NU1900–NU1905 como erros e lista dependências vulneráveis incluindo transitivas. Testes contratuais de release passaram 29/29 depois da correção. Restore local do probe passou e `dotnet list ... --vulnerable --include-transitive --no-restore` não encontrou pacote vulnerável nas fontes atuais. `ProgramStartupRecoveryTests` passou 8/8. Nenhuma mudança de runtime/API. CI remota ainda não executou por bloqueio de billing da conta GitHub; Linux hospedado permanece sem evidência. Formal 2/7, sem crédito. Correção incluída no rascunho `docs/releases/12.1.32.md`; nenhuma release publicada.

## Auditoria NuGet local completa — 2026-10-26 19:22 BRT

A auditoria `dotnet list MulletaFlix.sln package --vulnerable --include-transitive` restaurou e analisou os 43 projetos na solução; todos os resultados reportados indicaram zero pacotes vulneráveis segundo o feed NuGet.org consultado nesta data. Os seis projetos fora da solução que a CI enumera também passaram por restore com NU1900–NU1905 como erros e auditoria transitive; zero vulnerabilidades nos seis, exit 0. Essa execução fecha somente a verificação local de dependências, não o gate Linux/hospedado. O bloqueio de billing impede CI remota. Formal 2/7, sem crédito. Portal: evento `server-nuget-full-local-audit-2026-10-26-1922`.

## Suíte completa Nebula Python — 2026-10-26 19:24 BRT

Execução em Windows com venv isolada CPython 3.10.11, correspondente ao runtime trancado do projeto: `pytest tests --cov=.`, 723 aprovados, 2 ignorados, zero falhas em 26,73 s; cobertura geral do runtime testado 91%. Os dois skips são as branches de sincronização POSIX após rename, indisponíveis no Windows. Os testes Windows `MoveFileExW` passaram. Uma tentativa anterior com Python global foi interrompida pelo guard de segurança antes da coleta; não instalou dependências globais. Coverage XML foi salvo em Temp. Ubuntu CI permanece sem execução por bloqueio de billing; evidência não altera agregado .NET nem homologação formal 2/7. Portal evento `server-nebula-python-310-full-suite-2026-10-26-1924`.

## Nebula: atualizar a identidade quando muda o caminho local — 2026-10-26 19:43 BRT

Conferidos os writers C# (`NebulaMongoContext`, `NebulaStagingWatcher`) e Python (`main.py`, `ftp/pathio.py`). Atualizações/recovery que alteram `local_path` também gravam `queue_identity`; novos itens de staging preenchem os dois. Python focal passou 112/112 em CPython 3.10.11 isolado. O teste C# de recuperação foi selecionado, mas ignorado sem MongoDB descartável; sem prova integrada nesta rodada. Aliases físicos e inventário read-only continuam pendentes. Não usar esse resultado para homologação formal, release ou produção.

## MariaDB startup mutex: ownership cruza o lifecycle assíncrono — 2026-10-26

`StartMariaDbAsync` chama `AcquireStartupMutex` antes do primeiro `await` e armazena o `Mutex` estático até `StopMariaDbAsync`. O fluxo passa por `WaitForDatabaseAsync`, inicialização de MariaDB e inicialização de schema; a liberação ocorre no `finally` de `StopMariaDbAsync`, após operações assíncronas. Com `ConfigureAwait(false)`, a continuação não preserva a thread de aquisição. A documentação Microsoft confirma que `ReleaseMutex` exige a thread proprietária e que outra thread recebe `ApplicationException`.

Classificação: **P1, confirmado estaticamente**. A exceção de desligamento/rollback é consequência direta se a continuação ocorre em outra thread. A ocorrência em runtime não foi capturada nesta sessão. O mutex é mantido por todo o tempo de vida do serviço, portanto a espera de 45 segundos em outro processo também merece teste de startup concorrente. Próxima tarefa: cobrir liberação entre threads no fluxo real, preservar semântica de mutex abandonado após crash e remover espera indefinida do fallback sem criar lock que fique preso após encerramento do processo. Fonte: `Jellyfin.Server/Helpers/MariaDbProcessManager.cs:45,184-213,373-458`; caller `Jellyfin.Server/Program.cs:198,221`. Fonte externa: https://learn.microsoft.com/en-us/dotnet/standard/threading/mutexes.

Não houve alteração de runtime, configuração ou dependências. Linux não se aplica ao binário `mysqld.exe`; o runtime de produção Windows não foi exercitado. Homologação formal permanece 2/7 (28,6%), sem crédito.

## CI hospedada: falha antes dos steps — 2026-10-26 20:39 BRT
- Run CI `39608614450` no commit remoto `d51f4f38`: 13 jobs concluídos em 3–4 s com failure e sem steps reportados.
- Release `39608677913`: preflight falhou sem steps; Linux e publicação skipped.
- API pública não apresentou run posterior a 21/10 entre os cinco mais recentes. A causa continua desconhecida; não demonstra falha de compilação/testes do código.
- `main` local está 8 commits atrás de `origin/main`, com 224 caminhos alterados/não rastreados; CI hospedada não validou o estado atual. Sem release, sem crédito formal (2/7).

## Logs da CI ausentes no armazenamento remoto — 2026-10-26 20:42 BRT
A API de logs retornou 404 `BlobNotFound` para job do servidor 119251247983 e job de preflight 119251465001. Os jobs falharam sem steps; a resposta não oferece causa nem comprova falha de compilação/teste. Necessário novo run que registre steps/logs para validar checkout atualizado. Formal 2/7, sem crédito/release.

## Regressão local Release com build serial — 2026-10-26 20:46 BRT
- Comando serial `dotnet test` da solution em Release e `--no-restore`, incluindo build, terminou exit 0.
- 17 TRX: 5.401 Passed, 97 Skipped, 0 Failed/5.498. 15 assemblies tiveram casos; dois projetos Integration foram selecionados sem testes pelo filtro.
- TRX confirmou 38 skips RequiresMongo, 32 RequiresMariaDB, 25 dependentes de SO/filesystem e 2 benchmarks.
- Evidência é do checkout local `64a33dcf`, dirty e 8 commits atrás de `origin/main`; é repetição de casos já contabilizados. Não validar CI hospedada, Linux, runtime ou produção com este resultado. Sem crédito ou release; formal 2/7.

## Auditoria dos writers de BaseItemImageInfos — 2026-10-26 20:53 BRT

Leitura somente da fonte e diffs locais encontrou três caminhos de escrita em `ItemPersistenceService`: exclusão em `DeleteItemsUnderLocks`, substituição por `SaveImagesAsync` e gravação de imagens por `SaveBaseItemEntities` dentro de `UpdateOrInsertItemsCore`. Os caminhos do serviço adquirem stripes locais e locks advisory MariaDB antes da transação. O escritor adicional é `FixBaseItemImageInfos`, em `20250620180000_FixDates.cs`; adquire os 16 locks advisory e percorre as imagens em lotes. O workflow `Program` executa `MigrateStepAsync(CoreInitialisation)` antes de `appHost.InitializeServices`, ordenando essa migração de startup antes dos serviços que recebem persistência da biblioteca. Em provider sem advisory locks, não há lock compartilhado visível nesse método; a proteção depende da ordem serial de startup. Não foi encontrado writer de runtime sem lock. Revisão estática; nenhum banco/runtime/produção acessado e nenhum código alterado. O contrato do portal passou 304/304, sem skips/falhas. A correlação do log histórico permanece impossível sem PID/assembly. Métricas 5.465/5.493 e 2/7 formais permanecem; sem release.

## Reenvio do log de deadlock e auditoria do processo — 2026-10-26 20:50 BRT

O anexo tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao log de 23/10. Registra MariaDB 1213 em `DELETE BaseItemImageInfos` concorrente e retries de `SaveImagesAsync`, sem desfecho no trecho. HTTP 200 não prova commit; o alerta de heartbeat na mesma janela não demonstra causalidade.

Não havia processo MulletaFlix/Jellyfin ativo na inspeção local; nenhum banco ou serviço foi tocado. Sem PID/assembly/version, não é possível correlacionar a ocorrência à DLL instalada 12.1.35 ou ao checkout. Auditoria técnica 5.465/5.493 e homologação formal 2/7 (28,6%) permanecem. Checkout 8 commits atrás com 224 caminhos modificados/não rastreados; sem release. Ver evento portal `server-image-deadlock-repeat-no-process-2026-10-26-2050`.
# Nebula: teste do lifecycle full-upload — 2026-10-26 21:23 BRT

`test_main_lifecycle` parametrizado cobre streaming-only e modo full-upload.
No modo completo valida cleanup de duplicados, restauração antes do scanner e
criação dos workers grandes/pequenos. `test_stats_reporter` confere também a
identidade normalizada `queue_identity` ao re-enfileirar um registro falho.
Revisão Cavecrew encontrou uma asserção ausente para garantir que streaming-only
não execute o cleanup; corrigida antes do rerun final. Suite completa Nebula:
724 passed, 2 skipped POSIX, CPython 3.10.11 isolado. O teste mocka Mongo,
Telegram e workers; integração de processo e confirmação do upload persistido
continua pendente. Ver sessão `2026-10-26-MulletaFlix-nebula-full-mode-startup-audit-2123`.
# Reauditoria do deadlock de imagens e ACL do restore — 2026-10-26 21:29 BRT

O anexo recebido tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao log já auditado; não acrescenta PID/assembly, estado final das retentativas ou evidência sobre o runtime emissor. A fonte atual mantém lock local por item e advisory `GET_LOCK` MariaDB antes da transação de imagens. Revisão Cavecrew não encontrou lacuna de sincronização se todos os processos usarem a mesma versão. Runtime e produção continuam sem identificação/validação.

A revisão da exceção Windows de `BackupRestoreAclValidator` foi comparada à emenda da decisão ACL de 24/10: pais do namespace podem conceder somente criação de subdiretório; criação de arquivos e substituição continuam rejeitadas, assim como qualquer criação concedida no destino final. Não reverti a exceção porque ela é parte da decisão documentada e evita negar restores em raízes com essa ACL herdada. `BackupRestoreDirectoryTests` Release passou 39/49; 10 testes Linux/macOS ignorados, nenhum teste falhou. Linux nativo, CI hospedada, runtime instalado e produção seguem pendentes. Mapa conferido e sem alteração estrutural. Homologação formal permanece 2/7 (28,6%), sem crédito ou release.
# Regressão Release ampla local — 2026-10-26 22:06 BRT

A solução passou em Windows/.NET 10 Release, execução serial sem rebuild, com
filtro `FullyQualifiedName!~Integration`: 15 assemblies, 5.403 Passed, 98
Skipped, 0 Failed (5.501 selecionados; exit code 0). Dois assemblies de
integração não tiveram testes que correspondessem ao filtro. Skips permanecem
fora da contagem de aprovação. O lifecycle MariaDB foi ignorado nesta execução
ampla por runtime não stageado; evidência focal prévia cobre 1/1 com runtime
MariaDB descartável. Isso não equivale a homologação formal, não altera o
agregado de auditoria e não valida CI hospedada, Linux nativo, runtime instalado
ou produção. Formal permanece 2/7; sem release.

O log anexado nesta sessão tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico ao incidente previamente documentado de deadlock em `BaseItemImageInfos`. Não contém identidade do processo/assembly nem desfecho dos retries, portanto não identifica o runtime emissor nem demonstra que a causa foi resolvida.
# MariaDB descartável: regressão de usuário e billing — 2026-10-26 22:11 BRT

`UserManagerNormalizedUsernameTests|BillingDatabaseTests` passou 31/32 em
Windows/.NET 10 Release. O único skip requer `lower_case_table_names=0` e fica
para Linux. MariaDB 11.4.4 temporária usou porta dinâmica 60327, bind exclusivo
127.0.0.1 e datadir descartável; processo finalizado e porta fechada. O listener
operacional PID 31236 em 3306 não foi acessado. Esta execução complementar não
altera o agregado 5.465/5.493 nem a homologação formal 2/7 (28,6%); Linux,
CI hospedada, runtime instalado e produção permanecem pendentes. Sem release.

## Reauditoria do mutex de startup MariaDB — 2026-10-26 22:43 BRT

MariaDbStartupMutexTests passou 4/4 em Windows/.NET 10 Release: release na thread proprietária, timeout, cancelamento e mutex abandonado. MariaDbProcessManagerLifecycleTests foi ignorado nesta rodada porque mysqld.exe, mysql_install_db.exe e mysqladmin.exe não estavam staged ao lado do assembly. Há smoke start-stop-start 1/1 anterior registrado em T5.6; não foi reexecutado nesta sessão. A falha P1 de liberação entre threads está corrigida em fonte/testes; validação do runtime instalado, CI hospedada e produção continuam pendentes. Estrutura técnica inalterada; sem crédito formal (2/7) ou release.

## Smoke MariaDB start-stop-start com runtime 11.4.4 — 2026-10-26 22:47 BRT

`MariaDbProcessManagerLifecycleTests.StartStopStart_UsesTheSameIsolatedDataDirectory` passou 1/1 em Windows/.NET 10 Release. Runtime `mysqld.exe` reporta MariaDB 11.4.4; teste criou datadir temporário e porta dinâmica, inseriu linha, parou e reiniciou o processo e confirmou a linha persistida. O banco operacional não foi acessado ou parado. O teste usou o runtime instalado localmente; não foi comparado hash/proveniência com o ZIP baixado pela CI. Não valida o pacote instalado do servidor, CI hospedada nem produção. Homologação formal 2/7; sem release.

### Revalidação CI hospedada — 2026-10-27 02:25 BRT

A consulta das annotations dos check runs públicos 119251248109 (Server build & unit tests) e 119251465001 (Validate source tag before packaging) confirmou que os jobs não iniciaram porque a conta GitHub está bloqueada por pendência de cobrança. Runner ID 0, sem steps; logs não disponíveis. Os runs são do commit remoto d51f4f38; o checkout atual 64a33dcf está oito commits atrás. Isso é bloqueio administrativo, não falha de teste do código. Regressões locais não substituem CI hospedada. Homologação formal segue 2/7; release sem validação.
# Rotas públicas MVC declaradas e log de deadlock revalidado — 2026-10-27 04:08 BRT

O contrato HTTP `EveryDiscoveredMvcRouteDeclaresAuthorizationIntent` agora exige que cada rota MVC declare `[Authorize]` ou `[AllowAnonymous]`. Foram identificados como públicos, conforme o comportamento já existente, endpoints de login/QuickConnect, status público, sincronização de hora, imagens, splashscreen e segmentos HLS legados. Esses métodos receberam `[AllowAnonymous]` explicitamente; nenhum endpoint protegido foi aberto por esta alteração. `ProtectedApiSurfacesHttpTests` passou 7/7 em Windows/.NET 10 Release.

O log recebido tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico aos anexos anteriores. Registra vários workers tentando `DELETE BaseItemImageInfos` do mesmo item e deadlocks MariaDB 1213; Kestrel também reporta heartbeat atrasado. Este arquivo não inclui PID/versão nem desfecho dos retries. Inspeção atual read-only: nenhum processo MulletaFlix/Jellyfin está ativo; DLL instalada 12.1.35, timestamp 21/10, enquanto o checkout mantém alterações locais, inclusive o lock/serialização de `SaveImagesAsync`. Isso não prova qual binário emitiu o log nem que a correção esteja instalada. MariaDB operacional não foi consultado.

Não houve alteração adicional na correção do deadlock. Portal foi atualizado localmente; auditoria agregada permanece 5.465/5.493 e homologação formal 2/7 (28,6%), sem crédito. Sem release, commit, push, restart ou implantação. CI remota, runtime atual e produção permanecem pendentes.
## Cobertura dinâmica de autorização inclui o plugin IntroSkipper — 2026-10-27 04:11 BRT

Revisão do composition root `ApiServiceCollectionExtensions` confirmou que plugins fornecem suas assemblies ao `ApplicationPartManager`. O TestServer de autorização não carregava `IntroSkipper`, deixando sete controladores do servidor fora do contrato. `Jellyfin.Api.Tests` agora referencia o plugin e adiciona sua assembly às aplicações de teste; os contratos dinâmicos de intenção de acesso e `RequiresElevation` incluem os endpoints do plugin junto das assemblies API, GetAvatar e MediaBrowser.Providers.

`ProtectedApiSurfacesHttpTests` passou 7/7 em Windows/.NET 10 Release. Foi descoberta e fechada uma lacuna de cobertura; nenhuma rota, permissão ou implementação de produção mudou. A compilação mostrou avisos de analyzer já existentes em outros testes. Formal 2/7 (28,6%), auditoria agregada 5.465/5.493, sem crédito. Sem Linux/CI hospedada/runtime/produção, release, commit ou push. Mapa conferido; nenhuma mudança de módulo ou dependência de runtime.

## Restore P0 — travessia de destino ancorada na raiz aberta — 2026-10-27 04:18 BRT

`FileSystemRestoreTransaction.OpenTargetDirectoryCore` agora abre cada componente do destino relativo ao handle da raiz configurada. Diretórios previamente armazenados em cache são reabertos a partir do pai ancorado e comparados pela identidade do handle; divergência falha fechado. Isso remove o fallback para abrir o caminho-alvo a partir do ancestral nominal arbitrário e impede que o cache reutilize um handle de diretório diferente da árvore autorizada. `BackupRestoreDirectory.IsSameDirectoryAs` compara identidade por volume/File ID no Windows e dispositivo/inode no Linux.

`BackupRestoreDirectoryTests` + `BackupServiceTests` Release/.NET 10 no Windows: 128 aprovados, 16 ignorados, 0 falhas/144. Ignorados incluem ACL/symlink/mount Linux e macOS; por isso o comportamento POSIX e as ACLs reais não foram executados. O gate P0 segue aberto para execução privilegiada Linux, races de rename/unlink durante mutação, ACL efetiva/runtime, CI hospedada e produção. Homologação formal 2/7 (28,6%), agregado 5.465/5.493, sem crédito; sem release.
## 2026-10-27 04:27 BRT — Restore: revisão adversarial e gates locais

Regressão `FullSystemBackup` Windows/.NET 10 Release passou 137/153, com 16 ignorados por plataforma/macOS e zero falhas. A revisão da exceção de owner TrustedInstaller não encontrou bug: ela está isolada da lista usada para aceitar ACEs de escrita, e o teste Windows já rejeita grant ao TrustedInstaller. Contrato do portal fortalecido para exigir igualdade entre os 14 filtros Linux privilegiados e os 14 resultados esperados no TRX. Pendentes Linux nativo/CI hospedada, ACL efetiva, corridas adversariais, runtime e produção; restore P0 permanece aberto.
# Regressão Windows e contrato do gate privilegiado Linux — 2026-10-27 04:27 BRT

`FullSystemBackup` Release no Windows/.NET 10 passou 137, ignorou 16 casos por plataforma e falhou 0/153. Revisão do teste `ValidateTrustedWriteBoundary_RejectsTrustedInstallerWriteGrantOnWindows` confirmou que TrustedInstaller pode ser owner da âncora, mas não recebe grant de escrita. O contrato dedicado `ci-test-runner-serialization.test.mjs` passou 2/2 e valida os 14 nomes do filtro Linux, TRX esperado e métodos xUnit. O teste do portal verifica três casos representativos e passou 332/332. Isso valida o contrato estático, não execução Linux. Restore P0 permanece aberto; sem alteração de runtime ou release. Mapa técnico conferido; estrutura inalterada.

## Rechecagem read-only do runtime instalado — 2026-10-27 05:37 BRT

A consulta local encontrou nenhum processo MulletaFlix/Jellyfin em execução; o serviço `MulletaFlixNginx` está ativo. A DLL instalada `MulletaFlix.Server.Implementations.dll` tem versão de arquivo/produto 12.1.35, tamanho 1.774.080 bytes e data 21/10/2026 10:15:18 -03. Isto confirma apenas o estado dos arquivos/serviços no instante da consulta; não correlaciona o processo histórico do deadlock e não valida a versão de trabalho 12.1.32. Nenhum serviço ou banco foi iniciado, parado ou consultado.

## Regressão focal — deadlock na persistência de imagens — 2026-10-27 05:41 BRT

`ItemPersistenceServiceTests` e `ItemPersistenceDatabaseLockTests` passaram 52/52 em Windows/.NET 10 Release. O log anexado repete o incidente já conhecido (SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`), com deadlock 1213 em `BaseItemImageInfos`, sem identificação do binário ou desfecho terminal. A validação unitária não substitui concorrência MariaDB. O teste integrado não foi executado: não há connection string descartável configurada e existe `mysqld` ativo não identificado, sem acesso nesta auditoria. Formal 2/7; agregado 5.465/5.493, sem crédito. Sem mudança ou release.

## Regressão ampla e falha de visibilidade de migração corrigida — 2026-10-27 05:55 BRT

A execução inicial de `dotnet test MulletaFlix.sln -c Release --no-restore` encontrou CS1061 no teste `FixDatesImageMigration_WaitsForAllImageAdvisoryLocks`: `FixDates.FixBaseItemImageInfos` é interno e `IntroSkipper.Integration.Tests` não tinha `InternalsVisibleTo` no assembly `MulletaFlix`. O método não foi tornado público. Adicionada apenas a declaração de friend assembly necessária ao teste existente.

Após a correção, `dotnet test MulletaFlix.sln --configuration Release --no-restore` terminou com exit code 0. `Jellyfin.Server.Implementations.Tests`: 1.729 aprovados, 95 ignorados, 0 falhas; `Jellyfin.Api.Tests`: 809 aprovados, 3 ignorados; `Jellyfin.Providers.Tests`: 884 aprovados, 13 ignorados. Execução focada `IntroSkipper.Integration.Tests`: 20 aprovados, 48 ignorados, 0 falhas. Os 48 casos MariaDB foram ignorados porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não estava configurada; nenhuma instância ativa foi acessada. Ignorados de plataforma, opt-in Mongo e benchmarks permanecem sem crédito.

Auditoria agregada preservada em 5.465/5.493, homologação formal 2/7 (28,6%), sem crédito. Linux, runtime instalado, CI hospedada e produção seguem pendentes. Sem release. O mapa foi conferido: mudança limitada à visibilidade de teste do assembly, sem API pública, módulo ou dependência nova.

## Visibilidade interna do teste de migração e regressão total — 2026-10-27 05:55 BRT

A compilação da solução revelou um teste MariaDB novo chamando `FixDates.FixBaseItemImageInfos`, mas o assembly de teste não tinha acesso ao método internal do servidor. `Jellyfin.Server/Properties/AssemblyInfo.cs` agora declara `InternalsVisibleTo("IntroSkipper.Integration.Tests")`; a API continua internal e nenhuma dependência de runtime foi adicionada.

A solução Release terminou exit code 0. `Jellyfin.Server.Implementations.Tests`: 1.729 aprovados, 95 ignorados, 0 falhas; API 809 aprovados/3 ignorados; Providers 884 aprovados/13 ignorados. `IntroSkipper.Integration.Tests` focal: 20 aprovados, 48 ignorados, 0 falhas. Os skips MariaDB refletem ausência de connection string descartável; não se acessou o `mysqld` ativo. Homologação formal 2/7 e auditoria 5.465/5.493 sem crédito. Mapa técnico conferido; sem mudança estrutural. Ver evento portal `server-full-solution-retest-integration-internals-2026-10-27-0555`.

Portal de homologação local atualizado e validado com `node --test portal-site/tests/homologacao.test.mjs` (334/334). JSON preserva 2/7 formal e 5.465/5.493 auditados. `git diff --check` terminou com exit code 0; avisos apenas de LF/CRLF.

## Gate restore seguido do boot — nota histórica de 2026-10-27 06:07 BRT (superada às 06:32)

Registro histórico: a revisão inicial identificou que recovery e inicialização estavam cobertos separadamente. A lacuna foi fechada localmente pelo teste ponta a ponta descrito no início deste documento: replay do archive/journal antes das migrações, `InitializeServices`, startup tasks e resposta HTTP usando MariaDB descartável. O gate funcional local está concluído; permanecem pendentes Linux/ACL, CI hospedada, runtime instalado e produção. Não confundir o fechamento desse fluxo de startup com o gate independente do filesystem Linux.

## 2026-10-27 — isolamento final do schema de teste

O host usa schema IntroSkipper randômico por teste e o remove no teardown. A fixture MariaDB do plugin remove o schema fixo durante teardown e em falha de inicialização. O nome padrão de produção permanece `mulletaflix_introskipper`.

Evidência: build final de Jellyfin.Server.Integration.Tests Release 0 warnings/0 errors; StartupBackupRecoveryTests 1/1; IntroSkipperMariaDbTests 12/12, schema fixo ausente; CI contract 4/4; portal 334/334. Windows/.NET 10/MariaDB descartável; Linux/ACL, CI hospedada, runtime e produção pendentes. Formal 2/7, sem crédito/release. Ver sessão 2026-10-27-MulletaFlix-full-startup-after-restore-0626.

## Regressão de imagens TMDb e restore Windows — 2026-10-27 06:52 BRT

O fluxo de imagem pede `include_image_language=pt,null,en` para pt-BR e conserva idioma/provedor da imagem selecionada. `RemoteImagePayloadValidatorTests` + `TmdbUtilsTests`: 41/41; fixture HTTP localizada: 1/1; `Jellyfin.Providers.Tests` completo: 884 aprovados, 13 integrações externas ignoradas, 0 falhas/897. A fixture não chama TMDb real nem persiste em MariaDB.

`BackupRestoreDirectoryTests` no Windows/.NET 10 Release passou 129, ignorou 16 cenários específicos de Linux/macOS e teve 0 falhas/145. WSL não está instalado e Docker não foi encontrado; portanto a ACL/rename Linux e o gate hospedado permanecem abertos. O boot integral após restore, já exercitado contra MariaDB descartável, está documentado acima; não confundir com a validação do filesystem Linux.

O log MariaDB reenviado em 27/10 é byte a byte idêntico ao SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`: repete `DELETE BaseItemImageInfos`, deadlock 1213 e retries, sem PID/AssemblyVersion nem resultado terminal. O status HTTP 200 e o aviso de heartbeat Kestrel não demonstram commit nem causalidade. Fonte local contém stripe por ItemId, `GET_LOCK` e retry; evidência histórica não identifica o binário emissor e não confirma correção no runtime de produção.

Checkout permanece 8 commits atrás de `origin/main`, com alterações locais preexistentes; nenhuma sincronização, release ou publicação remota nesta auditoria. Homologação formal 2/7 e agregado 5.465/5.493 permanecem inalterados.
# Divergência da correção de persistência com origin/main — 2026-10-27 07:32 BRT

`git fetch origin main` confirmou que `origin/main` está em `d51f4f38` / v12.1.35,
oito commits à frente do checkout principal. A release remota executa `DELETE
BaseItemImageInfos` sem `ItemPersistenceDatabaseLock` e mantém o log intermediário
de retry sem PID/AssemblyVersion; o helper não existe nesse commit. A árvore
principal tem o helper como arquivo não rastreado e alterações extensas no
serviço/testes, então não foram mescladas, commitadas ou publicadas. O anexo
reenviado tem o mesmo SHA-256 do log já auditado e não identifica processo nem
resultado dos retries. A worktree de correção baseada na v12.1.35 passou o filtro
`ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` 43/43 em Windows/.NET
10 Release; o build gerou avisos de estilo/documentação. Próximo passo: reconciliar
e revisar a correção numa árvore isolada e executar testes MariaDB descartáveis; CI
hospedada continua bloqueada pela cobrança. Formal 2/7 e auditoria 5.465/5.493
sem alteração. Linux/ACL, runtime e produção pendentes.
# NextUp — critérios de segurança para reduzir candidatos — 2026-10-27 08:09 BRT

Revisão dos chamadores confirma que o fluxo geral fornece todas as chaves de séries a `GetNextUpBatched`; `GetResult` conta o conjunto completo antes de aplicar `StartIndex` e `Limit`. `GetNextUpEpisodesBatch` filtra acesso, episódios especiais, virtuais, reproduzidos e rewatch e seleciona por temporada/episódio com a ordem estável já coberta no teste diferencial.

Não truncar séries ou episódios antes de formar o resultado e contar. Uma alternativa relacional precisa definir desempate para posições e datas repetidas, preservar nullable e todos os filtros, e demonstrar a tradução/ordem no MariaDB. A fonte atual materializa as linhas candidatas; plano, latência e memória continuam sem medição. Auditoria estática apenas, sem mudança de código, SQL ou homologação formal (2/7).

# Corrida ao reconstruir as tags herdadas — 2026-10-27 09:40 BRT

`UpdateInheritedValues` executava um `ExecuteDelete` global em `ItemValuesMap`/`InheritedTags` enquanto `SaveItems` podia inserir linhas para itens. O cleanup agora adquire as 16 stripes locais, em ordem crescente, e os advisory locks MariaDB correspondentes antes de abrir a transação. A implementação usa o protocolo de locks já existente; não cria APIs nem dependências.

Validação: regressão unitária focada 1/1; `ItemPersistenceServiceTests` 38/38 em Windows/.NET 10 Release; contrato do job CI MariaDB/Linux 4/4; formatter whitespace passou; `git diff --check` passou. A integração MariaDB adicionada ao filtro de CI foi ignorada localmente por ausência da variável de conexão descartável. CI hospedada segue sem execução recuperável; runtime instalado/produção não foram verificados. Formal permanece 2/7; não alterar o agregado nem publicar release.

## Pessoas órfãs após exclusão de item — correção local 2026-10-27

MariaDB 11.4.4 descartável confirmou que `DeleteItemsUnderLocks` apagava primeiro `BaseItems`; `ON DELETE CASCADE` então removia `PeopleBaseItemMap` antes da busca dos IDs de pessoas. A limpeza não conseguia excluir as linhas órfãs de `Peoples`. O serviço agora captura os IDs antes do cascade e remove pessoas sem referências após a exclusão. `DeleteItem_RemovesPeopleRowsMadeOrphanByDeletedItem` falhou antes e passou depois (1/1).

Validação adicional: `Jellyfin.Server.Implementations.Tests` Windows/.NET 10 Release 1.738 aprovados, 95 ignorados, 0 falhas; contrato do filtro CI MariaDB/Linux 4/4. O teste está incluído no job, mas CI hospedada, runtime instalado e produção não foram validados. A hipótese distinta de interleaving com `PeopleRepository.UpdatePeople` permanece aberta. Formal 2/7; sem crédito/release.
# Persistência de pessoas: exclusão concorrente com UpdatePeople — 2026-10-27

`PeopleRepository.UpdatePeople` agora segue o protocolo de persistência por item: adquire a stripe local, depois o advisory lock MariaDB correspondente, verifica se `BaseItems` ainda contém o item e só então abre a transação de mapeamentos. Isso serializa a atualização com `ItemPersistenceService.DeleteItem` e evita inserir pessoas órfãs quando a exclusão venceu a corrida. `DeleteItemsUnderLocks` continua capturando `PeopleId` antes do cascade FK para limpar órfãos preexistentes.

Validação local Windows/.NET 10 Release: teste MariaDB da corrida e regressão sequencial passaram 2/2 contra MariaDB 11.4.4 descartável; `ItemPersistenceServiceTests` passou 39/39; contrato do filtro CI Linux MariaDB passou 4/4. O caso de corrida foi adicionado ao job Linux, mas CI hospedada, Linux real, runtime instalado e produção não foram validados. Homologação formal permanece 2/7 (28,6%); sem release. Estrutura de módulos/APIs permanece igual.

## NextUp: desempate estável de séries — 2026-10-27 10:24 BRT

`NextUpService.GetNextUpSeriesKeys` agora ordena pela chave da série depois de `LastPlayedDate` decrescente e antes de `Limit`. Quando várias séries têm a mesma data, o conjunto retornado deixa de depender da ordem implícita do banco. `NextUpQueryOptimizationTests` passou 8/8 em Windows/.NET 10 Release. Foi adicionado `ServerPersistenceMariaDbTests.GetNextUpSeriesKeys_WithEqualLastPlayedDates_AppliesStableTieBreakBeforeLimit`, que executa a consulta de produção com empate e limite contra MariaDB, e sua seleção foi incluída no job Linux MariaDB e no contrato do workflow. Neste host o projeto compilou, mas o teste de integração foi ignorado por não haver endpoint descartável; Docker não está disponível. Portanto a tradução Pomelo/MariaDB ainda não foi observada localmente e a CI hospedada segue pendente. Runtime e produção sem validação; formal 2/7, sem release.

## If-None-Match — listas, weak validators e precedência — 2026-10-27 10:49 BRT

`ConditionalRequestExtensions.IfNoneMatchMatches` compartilha a análise de ETags nas rotas de imagem e legenda. Agora trata listas, `W/"..."` com comparação fraca e `*`, conforme RFC 9110. Na resposta de imagem, `If-Modified-Since` só é consultado quando `If-None-Match` está ausente. O teste público `ImageControllerSplashscreenTests.GetSplashscreen_NonMatchingIfNoneMatchTakesPrecedenceOverIfModifiedSince` confirma no controller que ETag presente sem correspondência não permite 304 pela data. O filtro `ConditionalRequestExtensionsTests|ImageControllerSplashscreenTests|ImageControllerUserImageAuthorizationTests|SubtitleControllerTests` passou 44/44 em Windows/.NET 10 Release. Os padrões literais equivalentes foram buscados em `ImageController` e `SubtitleController`; ambos usam agora o helper. Build gerou avisos preexistentes em outros pontos dos projetos. Runtime, CI hospedada e produção continuam pendentes; formal 2/7, sem release.

## NextUp — selecionar episódios não assistidos no SQL — 2026-10-27 11:24 BRT

`NextUpService.GetNextUpEpisodesBatch` seleciona a primeira posição não assistida por série no banco via agrupamento ordenado, que Pomelo/MariaDB traduz para `ROW_NUMBER()` com filtro `<= 1`. O predicado de corte usa apenas o episódio assistido mais recente segundo a ordenação anterior; quando temporada ou episódio desse registro é nulo, mantém-se a regra anterior de não aplicar corte por posição. O teste MariaDB descartável cobre associação por série, último assistido, rewatch, séries sem histórico, especiais, virtuais, posições nulas e ranking SQL; passou 1/1. Cavecrew encontrou a regressão de posição parcial antes da validação final.

Esta mudança reduz candidatos não assistidos transferidos do MariaDB, mas não prova redução de linhas examinadas, latência ou memória. Rewatch ainda enumera registros em memória; `EXPLAIN/ANALYZE`, benchmark com biblioteca representativa, CI hospedada, runtime instalado e produção permanecem pendentes. A suíte `Jellyfin.Server.Implementations.Tests` passou 1.739, teve 95 ignorados por ambiente e 0 falhas em Windows/.NET 10 Release. Homologação formal 2/7 (28,6%), sem crédito ou release. Mapa técnico conferido: classe e fluxo existentes; módulos, APIs e dependências inalterados.

## NextUp — plano observado e redução no fixture — 2026-10-27 11:45–11:48 BRT

O teste de integração captura o SQL ranqueado executado pelo `NextUpService` e chama `ANALYZE FORMAT=JSON` na mesma instância MariaDB descartável, com parâmetros idênticos. No fixture de cinco séries e 16 linhas de episódios, MariaDB examinou 16 linhas da tabela base e o derived result materializou quatro vencedores; o query block reportou 0,455 ms. Plano usou índices por chave de série e a chave primária de `UserData`. A asserção agora exige saída ranqueada inferior às linhas examinadas.

Na revisão, o teste deixou de depender dos aliases internos (`b`, `<derived2>`) do JSON do MariaDB e passou a ignorar `r_rows` nulos. A execução repetida passou 1/1 e observou contagens incluindo 4 e 16, com 0,429 ms no query block. São medições de fixture pequeno e cache aquecido; não medem catálogo real, variância, memória ou benefício ponta a ponta. Volume representativo, comparação controlada, rewatch relacional, CI hospedada, runtime e produção seguem pendentes. Sem crédito formal/release (2/7). Mapa técnico conferido; teste apenas, sem mudança estrutural.

## NextUp — desempate determinístico no rewatch — 2026-10-27 12:01 BRT

`GetNextUpEpisodesBatch` escolhe agora o item de rewatch por `LastPlayedDate`; datas iguais usam a maior temporada/episódio e, persistindo empate, o maior `Guid`. MariaDB descartável passou 1/1 cobrindo data igual entre posições distintas, empate de posição com IDs distintos, timestamp nulo frente a timestamp válido e posição nula frente a posição válida. `NextUpQueryOptimizationTests` passou 8/8. Uma primeira ampliação do fixture tentou inserir um usuário já existente; o seed foi corrigido para consultar o usuário no contexto descartável.

Isto torna o resultado determinístico, mas não reduz as linhas lidas pelo ramo de rewatch. Testes Linux/CI hospedada, runtime e produção continuam pendentes; formal 2/7, sem release. Mapa conferido: nenhum módulo, API ou dependência foi adicionado; alterada lógica interna da classe existente.

## NextUp — seleção SQL do ramo de rewatch — 2026-10-27 12:30 BRT

O ramo de rewatch agora agrupa por série no banco, ordena por `LastPlayedDate`, temporada, episódio e ID, projeta somente o ID vencedor e carrega as entidades vencedoras pela chave. A projeção de entidade dentro de `GroupBy(...).First()` causava `EmptyProjectionMember` na enumeração EF/Pomelo; a seleção escalar elimina essa falha de tradução. A comparação de posição mantém a regra anterior: sem temporada ou episódio no último reproduzido, não corta candidatos por posição.

`GetNextUpEpisodesBatch_SelectsEarliestUnplayedEpisodeOnMariaDb` passou 1/1 contra MariaDB 11.4.4 descartável após incluir uma série com episódio reproduzido S1E5, candidato não reproduzido sem posição e S1E6; o escolhido foi S1E6. Fixture total: 6 séries/19 episódios. Também cobre desempates, datas/posições nulas, ranking não assistido e rewatch. `NextUpQueryOptimizationTests` passou 8/8; `dotnet format whitespace ... --verify-no-changes --no-restore` e `git diff --check` passaram. Cavecrew encontrou a ausência desse caso no fixture; a leitura adversarial do predicado não correspondeu ao comportamento real do `Any` negado, confirmado pelo teste.

A validação confirma tradução e semântica do fixture, não ganho de latência/memória nem plano do ramo de rewatch. Benchmark com catálogo representativo, CI hospedada, Linux, runtime instalado e produção continuam pendentes. Homologação formal 2/7 (28,6%), sem crédito/release. O mapa técnico foi conferido; nenhuma API, classe pública, dependência ou módulo mudou.

## NextUp — plano ANALYZE do rewatch — 2026-10-27 12:52 BRT

O teste captura novamente a consulta SQL executada pelo ramo de rewatch e exige `LastPlayedDate` e `ROW_NUMBER() OVER` no comando antes de chamar `ANALYZE FORMAT=JSON`, evitando analisar por engano uma consulta ranqueada anterior. MariaDB 11.4.4 descartável passou 1/1; no fixture de seis séries e 19 episódios, o plano reportou 0,568 ms no query block e linhas observadas 2, 12, 1, 1, 3.666666667, 0.666666667, 5, 0.666666667, 1, 12, 1, 1, 3.666666667, 0.666666667, 5, 0.666666667. Fixture pequena/cache aquecido; não é benchmark representativo nem prova de ganho. `NextUpQueryOptimizationTests` já havia passado 8/8; formatter passou nesta rodada. Linux/CI hospedada, catálogo real, runtime e produção continuam pendentes; formal 2/7, sem crédito/release.
# Revalidação do log duplicado de deadlock em imagens — 2026-10-27 13:32 BRT

O anexo recebido tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual aos registros anteriores. Ele repete o erro MariaDB 1213 no `DELETE BaseItemImageInfos` e retries concorrentes, sem PID, versão do assembly ou resultado terminal. O `HTTP 200` do pedido não confirma commit das referências de imagem.

O checkout atual serializa `SaveImagesAsync` por stripe local do item e mantém um advisory `GET_LOCK` MariaDB durante a transação. O filtro `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 54/54 em Windows/.NET 10 Release nesta sessão. A validação MariaDB descartável 1/1 já registrada não foi repetida. O log não permite identificar o binário emissor nem confirmar se as imagens persistiram; runtime instalado, CI hospedada e produção seguem pendentes. A homologação formal permanece 2/7 (28,6%), sem crédito ou release.
# Cache HLS — framing da chave contra colisões — 2026-10-27 13:40 BRT

`StreamStateCache.BuildCacheKey` codifica cada string por comprimento seguido do conteúdo (e representa `null` separadamente). Antes, campos eram concatenados com `|` sem escape; como os validadores aceitam esse caractere em `Container` e `SegmentContainer`, `mkv|mp4` + `ts` e `mkv` + `mp4|ts` podiam compartilhar a mesma chave e reutilizar o `StreamState` de uma seleção de encode diferente. A regressão `BuildCacheKey_SeparatesContainerValuesContainingKeyDelimiter` cobre esse par.

Validação Windows/.NET 10 Release: `StreamStateCacheTests` 18/18, `DynamicHlsControllerTests` 7/7, contrato do portal 351/351, `dotnet format whitespace --verify-no-changes`, validação JSON e `git diff --check` passaram. Isso confirma a chave no nível da função e a regressão de controle; requisições HTTP HLS simultâneas, cliente real, runtime instalado, CI hospedada e produção não foram validados. Homologação formal permanece 2/7 (28,6%), sem crédito/release. Mapa conferido: nenhuma API, dependência, módulo ou caller novo; permanece o caller de `DynamicHlsController`.
# Backup/restore — reauditoria da exclusão entre operações — 2026-10-27 13:45 BRT

O semáforo de `RateLimitMiddleware` serializa somente a admissão HTTP de `POST /Backup/Create`, `/Backup/Restore` e `/Backup/RestorePointInTime`; seu `finally` libera o semáforo após a resposta. A exclusão do trabalho real está no `BackupService.TryBeginOperation`, que usa `Interlocked.CompareExchange` para manter uma operação ativa durante o backup/restauração. `ScheduleRestoreAndRestartServer` conserva o gate enquanto prepara o encerramento; `RestoreBackupInternalAsync` o libera no `finally`.

Revalidação Windows/.NET 10 Release: `ConcurrentRestoreRequests_AreRejectedWithoutBlockingPlayback` 2/2; `CreateBackupAsync_RejectsConcurrentOperationAndReleasesGateAfterFailure` 1/1. O primeiro verifica admissão concorrente nos dois endpoints de restore e que GET de playback continua; o segundo verifica restore e scheduling rejeitados durante backup ativo e liberação do gate após falha. A direção restore-ativo contra novo backup, o restart real, carga, Linux, CI hospedada, runtime e produção não foram testados. Isso não completa T6.2 nem altera homologação formal (2/7); sem release. Mapa conferido: middleware, controller e service existentes; nenhuma API, dependência ou módulo alterado.
## Backup/restore — exclusão nas duas direções — 2026-10-27 14:00 BRT

Adicionada regressão em `BackupServiceTests` que pausa o restore no callback de criação do sidecar do arquivo existente e confirma que um backup concorrente recebe `BackupOperationInProgressException`. Após liberar o restore, o teste confirma a conclusão e o conteúdo restaurado. O helper da fixture passou a expor esse callback de restore e os chamadores existentes foram renomeados para refletir seu uso; produção não mudou.

Windows/.NET 10 Release: os três testes focais de gate passaram 3/3, incluindo backup ativo rejeitando restore/scheduling, restore ativo rejeitando backup e liberação do gate após falha. Isso não valida o ciclo real de reinício após scheduling, CI hospedada, Linux, runtime instalado, carga, nem produção. Homologação formal permanece 2/7 (28,6%), sem crédito ou release.

## Backup/restore — suíte completa no HEAD atual — 2026-10-27 14:04 BRT

No HEAD `fa1289b`, a execução `dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~FullSystemBackup' --verbosity quiet` passou 141 testes, ignorou 16 e falhou 0/157 em Windows/.NET 10 Release. Os skips são limitações de plataforma/privilégio e não contam como execução Linux.

Isso revalida a suíte de backup/restore na árvore atual, mas não executa o ciclo end-to-end de agendamento + encerramento + novo processo + startup, nem ACL/TOCTOU Linux, CI hospedada, runtime instalado ou produção. Homologação formal permanece 2/7 (28,6%), sem crédito ou release.

## Regressão ampla da solução — HEAD fa1289b — 2026-10-27 14:25 BRT

Execução serial Windows/.NET 10 Release, sem recompilar, com filtro `FullyQualifiedName!~Integration`: exit code 0. Foram gerados e reconciliados 17 TRX independentes: 5.473 `Passed`, 98 `NotExecuted`, 0 falhas, 5.571 resultados. O filtro exclui casos cujo nome completo contém `Integration`; os 98 `NotExecuted` não são aprovações. Integrações MariaDB/MongoDB opt-in, Linux/ACL, CI hospedada, runtime instalado e produção não foram validados. Homologação formal permanece 2/7 (28,6%), sem crédito/release.

## Restore — sinal de encerramento após agendamento — 2026-10-27 14:34 BRT

O teste `ScheduleRestoreAndRestartServer_SetsRestorePathAndKeepsOperationsBlocked` agora espera o callback de `IHostApplicationLifetime.StopApplication()` e confirma uma chamada depois do atraso de 500 ms. Mantém também as verificações de `RestoreBackupPath`, `ShouldRestart`, notificação de reinício e bloqueio de operações concorrentes.

Windows/.NET 10 Release: teste focal passou 1/1. A verificação cobre o sinal de shutdown no `BackupService`, não o encerramento e relançamento de um processo nem a restauração realizada no boot seguinte. O teste do ciclo completo continua pendente, assim como CI hospedada, Linux/ACL, runtime instalado e produção; formal 2/7, sem crédito/release. Mapa técnico conferido: somente teste e documentação mudaram; módulos, classes/API de produção, dependências e fluxo permanecem iguais.

## Restore — sinal de encerramento após agendamento — 2026-10-27 14:34 BRT

O teste `ScheduleRestoreAndRestartServer_SetsRestorePathAndKeepsOperationsBlocked` agora espera o callback de `IHostApplicationLifetime.StopApplication()` e confirma uma chamada depois do atraso de 500 ms. Mantém as verificações de `RestoreBackupPath`, `ShouldRestart`, notificação de reinício e bloqueio de operações concorrentes.

Windows/.NET 10 Release: teste focal passou 1/1. A verificação cobre o sinal de shutdown no `BackupService`, não o encerramento e relançamento de um processo nem a restauração realizada no boot seguinte. O teste do ciclo completo continua pendente, assim como CI hospedada, Linux/ACL, runtime instalado e produção; formal 2/7, sem crédito/release. Mapa técnico conferido: somente teste e documentação mudaram; módulos, classes/API de produção, dependências e fluxo permanecem iguais.
# LatestMedia — consulta ainda sem medição representativa — 2026-10-27 16:36 BRT

Auditoria estática do caminho `UserLibraryController.GetLatestMedia` → `LibraryManager.GetLatestItemList` → `BaseItemRepository.GetLatestItemList`: filmes e séries ordenam/projetam candidatos no SQL, mas fazem a deduplicação por `PresentationUniqueKey`/`SeriesId` após `AsEnumerable()`, limitando somente depois de obter chaves distintas. O fluxo evita DTOs para todos os candidatos, mas não limita no SQL as linhas candidatas examinadas/transferidas.

Não há evidência de volume ou latência em banco real. A hipótese é custo proporcional a duplicatas por chave e precisa ser medida em MariaDB descartável com população representativa antes de propor mudança, preservando ordem estável e semântica de versões. Nesta sessão não havia conexão de teste MariaDB configurada; nenhuma consulta ou benchmark foi executado. Homologação formal continua 2/7 (28,6%), auditoria distinta 5.465/5.493; sem crédito/release.

`BaseItemRepositoryLatestTvShowTests` passou 3/3 em Windows/.NET 10 Release. Esses casos exercitam semântica de seleção de séries/álbuns, não medem execução SQL nem desempenho; `GetLatestItemList_Movies` ainda não tem teste focal identificado nesta busca.

## LatestMedia — observação read-only do runtime de produção — 2026-10-27 17:01 BRT

Após o operador iniciar o servidor, `GET /System/Info/Public` respondeu 200 e identificou MulletaFlix 12.1.35. `GET /metrics` respondeu 200; uma leitura local levou 35,603 ms. Os histogramas cumulativos expostos no scrape tinham somente `db_context=users`: 76 comandos de leitura (74,329 ms acumulados; média 0,978 ms), 7 comandos de schema (2,762 ms; média 0,395 ms) e 28 aberturas de conexão (14,168 ms; média 0,506 ms). São métricas agregadas do processo, sem atribuição por controller/rota.

`Startup` também usa `UseHttpMetrics`, e o scrape `/metrics` expõe `http_request_duration_seconds`/`http_requests_received_total` com labels `controller`, `action` e `endpoint` para outras rotas observadas. O snapshot não contém série para `Items/Latest`; a chamada anônima respondeu 401 e não forneceu duração autenticada. Portanto ainda não há amostra da rota nem evidência sobre o acervo real, DTOs/imagens ou memória. Nenhum dado foi alterado e não foi executado SQL/ANALYZE contra produção. Próximo passo: após login no navegador já aberto, capturar uma carga normal de `/Items/Latest` e comparar o header `X-Response-Time-ms`/métricas por endpoint com histogramas agregados DB. Homologação formal permanece 2/7 (28,6%); sem crédito/release. Mapa técnico conferido e complementado com a instrumentação HTTP existente.


## LatestMedia em produção — rechecagem read-only — 2026-10-27 17:10 BRT

Cinco requisições locais por endpoint: /System/Info/Public, /metrics e /web/ retornaram 200. Medianas do tempo total cliente local: 1,87 ms, 11,38 ms e 1,87 ms. A primeira chamada foi incluída; os valores não isolam tempo de servidor e não representam acesso remoto. GET /Items/Latest?Limit=5 anônimo retorna 401. Chrome permanece no formulário de login, sem sessão válida; endpoint protegido e acervo não puderam ser medidos. Nenhuma leitura/escrita de banco operacional foi feita e nenhum dado foi alterado.


## Restore Linux gate — ambiente e branch rechecados — 2026-10-27 17:12 BRT

Rechecagem do host: wsl.exe --list --quiet não listou distribuição; Docker e Podman não estão instalados/disponíveis. git fetch origin concluiu sem erro e manteve origin/main em d51f4f38; checkout main fa1289b6 segue 1 commit à frente e 8 atrás. Há alterações locais não commitadas em Program.cs, scheduler e testes que também diferem do upstream; não houve merge/rebase para preservar esse trabalho. Consulta GitHub do commit remoto devolveu lista vazia de status checks; isso não comprova execução CI Linux nem causa do run falho previamente observado. Restore P0, Linux/ACL, CI hospedada, runtime/produção continuam sem validação; formal 2/7, sem crédito/release.


## LatestMedia — métricas atuais sem série da rota — 2026-10-27 17:13 BRT

GET /System/Info/Public confirmou MulletaFlix 12.1.35. GET /metrics respondeu 200, porém filtro de http_request_duration_seconds e http_requests_received_total por Latest/UserLibrary não encontrou séries. Não há amostra para estimar duração da rota; requer navegação autenticada e telemetria emitida pelo caminho. Nenhum banco foi consultado ou alterado. Homologação formal 2/7, sem crédito/release.


## Restore agendado — segunda passagem do loop de startup — 2026-10-27 17:16 BRT

O teste ScheduledRestore_RestoresArchiveBeforeStartupInitialization agora verifica que ContinueAfterBackupRecoveryAsync limpa _restoreFromBackup e que a próxima chamada do gate, com nova instância de BackupService no mesmo processo, continua para migração e inicialização em vez de reaplicar o archive. O teste focal passou 1/1 em Windows/.NET 10 Release; FullSystemBackup passou 142 aprovados, 16 ignorados por plataforma e 0 falhas/158. O primeiro filtro FQN incorreto selecionou zero testes; a repetição com filtro pelo nome do teste executou e passou. Não houve mudança na lógica de produção. Linux/ACL, CI hospedada, runtime e host operacional continuam pendentes; formal 2/7, sem crédito/release.


## Restore agendado — semântica de restart reconciliada — 2026-10-27 17:18 BRT

A inspeção de Program.StartApp confirma que o reinício agendado não relança o executável: um do/while chama StartServer, e quando _restartOnShutdown está ativo a aplicação chama _setupServer.StopAsync/RunAsync antes de iterar novamente no mesmo processo. O teste ScheduledRestore_RestoresArchiveBeforeStartupInitialization cobre duas invocações do gate com serviços de teste, mas não executa Program.Main/StartApp nem o ciclo real com host operacional. Registros antigos que descreviam um “processo novo” foram corrigidos nas entradas ativas; evidência histórica não foi reescrita. Linux/ACL, CI, runtime/produção pendentes; formal 2/7, sem crédito/release.
# Nebula — runtime em diretório de dados e paginação Supabase (27/10/2026)

O log de execução apontou `WinError 5` ao criar `Tools/NebulaPython/.venv` em `Program Files`, mas a exceção era absorvida e o gerenciador ainda tentava iniciar os processos. O bootstrap agora instala runtime e marker em `CommonApplicationPaths.DataPath/Tools/NebulaPython`, passa o diretório de dados ao script de instalação e propaga erro de dependência para impedir worker em estado incompleto. `FindPythonExe` consulta o novo marker e o caminho antigo para compatibilidade de instalações existentes.

O paginador Supabase passou a enviar GUID como UUID sem aspas, corrigindo `22P02`; a reconciliação de `nebula_users` filtra chaves de login nulas e vazias para não invalidar lotes por dados legados. Não houve exclusão de dados. Windows/.NET 10: testes focais Supabase 3/3, restart loop 1/1; verificação de integridade do pacote aprovada. Instalador instalado, CI hospedada, Linux e reteste produtivo pendentes. Formal 2/7, sem crédito ou release.

### Revalidação Nebula runtime — 27/10/2026
O marker Python em `DataPath/Tools/NebulaPython/python.path` tem precedência sobre o marker legado em `AppContext.BaseDirectory`, mantendo compatibilidade como fallback. O instalador sem `MULLETAFLIX_DATA_PATH` usa `<ProgramData>/MulletaFlix/Server/data`, alinhado a `BaseApplicationPaths.DataPath`. Teste focado 1/1 e integridade do staging aprovados. O log recebido depois do patch reproduz o caminho antigo e falha Supabase durante reconciliação; não comprova execução da versão corrigida. Publicação bloqueada pela divergência da branch e mudanças locais; CI, pacote final e produção pendentes. Formal 2/7.

### Correlação Supabase do incidente Nebula — 27/10/2026
Logs do trace de produção mostram agente `MulletaFlix-Server/12.0`, paginação `nebula_users` sem os filtros adicionados na fonte local e, em requisição subsequente do mesmo trace, cursor UUID citado em `mulletaflix_users` com PostgREST 400/22P02. O schema remoto tem PK `nebula_users.login` e 7 linhas. O teste `FetchSupabaseRowsPagedAsync_PreservesNebulaLoginFiltersWhenPaging` cobre os filtros em ambas as páginas; os três testes focais de cursor/filtros passaram 3/3. Evidência remota sugere binário anterior ao patch; runtime pós-patch não foi confirmado. Advisor Supabase reporta search_path mutável na função `set_nebula_updated_at` e índice duplicado na tabela de usuários; alterações de banco não executadas.
Validação do patch em base limpa: aplicado sem conflito a `origin/main` d51f4f38 em `C:\mf`; regressão Windows/.NET 10 Nebula 84/84 e `Assert-StageIntegrity.Tests.ps1` aprovados. Isso não substitui a pipeline completa, validação Linux nem reteste do runtime em produção. Nenhuma release/tag criada.

## Reauditoria do scheduler e Nebula — 2026-10-27 22:07 BRT

No working tree compartilhado, os testes focais dos diffs locais passaram: LimitedConcurrencyLibrarySchedulerTests 4/4 (Windows/.NET 10 Release; ciclo incluiu a janela de cleanup de 65 s) e NebulaDownloaderStartupTests + NebulaSupabaseSyncTests 100/100 (Windows/.NET 10 Release). Revisão adversarial independente não encontrou problema concreto no diff Nebula. No scheduler, o descarte espera _enqueueCounter chegar a zero antes de cancelar os runners; portanto, um callback admitido que não conclua pode atrasar o descarte indefinidamente. A API atual não fornece token de cancelamento do scheduler ao callback, e nenhum teste demonstrou esse travamento com trabalho não cooperativo; tratar como limite de desligamento gracioso para decisão/validação futura, sem mudar contrato nesta sessão.

Estes testes focalizados não são regressão completa nem validação Linux, CI hospedada, runtime instalado ou produção. Os diffs já existentes permanecem sem commit e sem release. A homologação formal continua em 2/7 (28,6%). Mapa técnico conferido: sem mudança estrutural.

## Scheduler — propagação de cancelamento em varreduras — 2026-10-27 22:21 BRT

`ILimitedConcurrencyLibraryScheduler.Enqueue` agora entrega um `CancellationToken` ao callback; `Folder` o propaga às operações de validação e atualização de metadados. A escrita no canal limitado usa token ligado ao cancelamento do chamador e ao descarte, para não continuar admitindo itens após shutdown. Falhas lançadas por callbacks de cancelamento são registradas e não interrompem o restante da limpeza.

Windows/.NET 10 Release: classe `LimitedConcurrencyLibrarySchedulerTests` 6/6; suíte `Jellyfin.Controller.Tests` 71/71; `git diff --check` passou. Formatter direcionado reportou ordenação de imports já existente em `Folder.cs` e não concluiu a segunda execução antes de ser interrompido; verificação de formatação completa permanece pendente. Callbacks que ignoram o token ainda podem estender o descarte gracioso; não foi validado com runtime real, Linux, CI hospedada ou produção. Checkout principal divergente e sujo; nenhum commit, push ou release efetuado. Homologação formal 2/7 (28,6%).

Revalidação em branch limpa sobre `origin/main` (22:35 BRT): scheduler 12/12, Controller 75/75 e caller-cancel com canal cheio 1/1. Commits `0813ca64` e `69d81ca4` publicados em `codex/scheduler-callback-cancellation`. PR bloqueado por integração GitHub 403 e ausência de sessão `gh`; sem merge/release. Formatter completo, Linux, runtime e produção permanecem pendentes.

## Scheduler — cancelamento de workers no shutdown gracioso — 2026-11-02

Auditoria do diff local encontrou regressão: `LimitedConcurrencyLibraryScheduler.Worker` vinculava o token do worker apenas ao seu lifetime local, removendo o vínculo pré-existente com `IHostApplicationLifetime.ApplicationStopping`. Um teste reproduziu a falha: após sinalizar shutdown, workers ociosos não encerravam em 5 s. O token do worker agora também observa `ApplicationStopping`. A classe `LimitedConcurrencyLibrarySchedulerTests` passou 7/7 e `Jellyfin.Controller.Tests` passou 72/72, Windows/.NET 10 Release; o contrato do portal passou 360/360. Treze verificações do portal exigiam mês fixo e foram atualizadas para validar formato ISO com `-03:00`; a primeira execução falhou nessas verificações até o ajuste. `dotnet restore` levou 32,85 s e reportou aviso NU1902 moderado para MySqlConnector 2.4.0, dependência existente. CI hospedada, Linux, runtime instalado e produção não validados. Homologação formal permanece 2/7, sem crédito; nenhuma release criada. Mapa técnico conferido; fluxo de cancelamento atualizado. Ver sessão do vault `2026-11-02-MulletaFlix-scheduler-host-shutdown-cancellation-2055`.
### Retry transacional em atualização de elenco — 2026-11-02

`PeopleRepository.UpdatePeople` agora repete a substituição integral após deadlock ou lock wait timeout MariaDB (1213/1205), usando contexto e transação novos, até três tentativas e espera curta crescente. O lock do item permanece adquirido entre tentativas; falhas permanentes continuam propagando. Os testes usam interceptor SQLite para simular os códigos sem serviço externo. A abordagem segue a recomendação do EF Core de repetir a unidade transacional inteira.

Revisão adversarial identificou que os testes iniciais injetavam a falha antes das gravações: `TransactionStarting` em People e o comando `DELETE` interceptado antes de executar em Chapters/MediaStreams. Os cenários foram unificados para injetar o erro transitório em `TransactionCommitting`/`TransactionCommittingAsync`, depois das escritas, e conferir o estado anterior após rollback e retry. `ItemRepositoryTransientRetryTests`: 17/17 aprovados em Windows/.NET 10; formatter e `git diff --check` aprovados. A técnica usa callbacks oficiais do interceptor transacional e atomicidade de transação do EF Core ([interceptores](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors), [transações](https://learn.microsoft.com/en-us/ef/core/saving/transactions)).

Ainda falta MariaDB descartável com lock real, Linux/CI hospedada e runtime/produção. Formal 2/7; não conta como homologação ou release.
### 02/11/2026 — URLs sensíveis nos logs Live TV/IPTV (T6.4 parcial)

- `TunerHostManager` redige URLs de sintonizadores em logs de descoberta e alteração, e formata a exceção de descoberta depois de remover URLs remotas.
- `IptvOrgEpgSynchronizer` e `XmlTvListingsProvider` não passam mais a exceção bruta ao logger nos fluxos de tuner/EPG/XMLTV; `LiveTvUrlRedactor.RedactText` sanitiza URLs embutidas no texto da exceção enquanto preserva o restante do diagnóstico.
- Testes Release Windows/.NET 10: Live TV focal 19/19 e Providers focal 11/11; formatter e diff-check passaram. Warning SA1507 no `LiveTvManager.cs` não foi causado por estes arquivos.
- Limite: auditoria de outros `ILogger` e sinks do servidor, Linux, runtime instalado e produção ainda pendente; nenhuma release/instalação.


### 02/11/2026 — provedores externos e imagens Live TV (T6.4 parcial)

- URLs de falha/sucesso e exceções em NartoDrama, GoodShort e YouTube agora passam pelo redator de URL já existente no assembly Providers; GuideManager usa o redator de Live TV.
- Teste do YouTube simula URL .strm com credenciais e API key sintéticas, força falha HTTP e confirma logs sem os valores. Providers focal: 62/62; Live TV focal: 15/15.
- A pesquisa estática ainda mostra sinks de URL em outros provedores e ItemImageProvider; T6.4 não está concluído. Linux, runtime instalado e produção não foram testados.

### 02/11/2026 — redação compartilhada de URLs em imagens e provedores (T6.4 parcial)

- O helper interno `RemoteUrlLogRedactor` foi movido para `MediaBrowser.Providers/Helpers` para atender provedores independentes de MidiaStorageOnline.
- `ItemImageProvider`, `ListenBrainzLabsClient` e `DramaBoxClient` redigem URLs e exceções antes do logger; erros de refresh de imagem também removem URLs sensíveis.
- Providers focal: 150/150 em Windows/.NET 10 Release; `dotnet format whitespace --verify-no-changes` e `git diff --check` passaram. A busca ampliada localizou logs de exceção ainda a revisar em ShortMax e no tratamento de erro de imagens do YouTube; varredura não fecha T6.4.
- T6.4 continua parcial: formatos/sinks fora do padrão e demais módulos do servidor precisam de auditoria; Linux, runtime instalado e produção pendentes. Nenhuma instalação ou release.

### 02/11/2026 — nova varredura de exceções de rede dos providers (T6.4 parcial)

- Redação aplicada em ShortMax, nos três caminhos remotos de YouTube metadata, YouTube Images, ListenBrainz Similar, TMDb Similar Movie/TV e JSON inválido de GoodShort.
- Testes focais existentes + novos: Providers 152/152 em Windows/.NET 10 Release; formatter focal aprovado. `git diff --check` aprovado no escopo.
- A pesquisa literal de `_logger.Log*(ex, ...)` em `MediaBrowser.Providers/Plugins` ainda encontra somente dois usos com contexto local (leitura de índice e persistência local); outras formas de log, camadas de transportes e módulos do servidor não estão cobertos por essa busca.
- Microsoft Learn recomenda redaction pelo pipeline com classificação e `Microsoft.Extensions.Telemetry`; dependência/compatibilidade não avaliadas e nenhuma adoção feita. Linux, runtime instalado, CI remota e produção pendentes.
### Continuação T6.4 — exceções em fronteiras comuns de providers e Live TV (2026-11-02 23:56 BRT)

Providers/Live TV agora sanitizam texto de exceção antes do logger em `ProviderManager`, `MetadataService`, OpenLibrary, letras e legendas, `GuideManager` e chamadas de imagem/headend da Schedules Direct. Mensagens de erro de refresh originadas por providers também removem URLs. A chamada HTTP da Schedules Direct mantém host redigido, status HTTP e código de serviço; o corpo bruto, que pode ecoar dados, deixou de ser registrado. Nenhuma requisição ou metadado persistido foi alterado.

Verificações Windows/.NET 10 Release: `Jellyfin.Providers.Tests` 893 aprovados, 13 ignorados, 0 falhas/906; `Jellyfin.LiveTv.Tests` 81/81; `git diff --check` aprovado. `dotnet format whitespace --verify-no-changes` nos arquivos alterados teve verificação parcial: a ferramenta parou em `CHARSET` nos arquivos preexistentes `LyricManager.cs` e `SchedulesDirect.cs`; os demais arquivos selecionados passaram. A busca estática ainda encontra outros logs com exceção bruta fora dos pontos confirmados; T6.4 permanece parcial.

Após fechamento forçado informado pelo usuário, não havia processo de build/test `dotnet` nem processo `git`/`gh` ativo na inspeção. Duas instâncias `Nebula` e duas rotinas `clean_already_sent.py` estavam ativas e não foram encerradas. O histórico do processo já fechado não permite identificar a execução que ficou presa por cinco horas.

Sem release, instalação, publicação remota ou crédito formal. Homologação permanece 2/7 (28,6%). O mapa técnico foi conferido e atualizado: fluxos de log foram ampliados em classes existentes; sem novo módulo, API ou dependência.
## Auditoria contínua — persistência MariaDB de imagens — 2026-11-03 05:41 BRT

MariaDB 11.4.4 descartável foi iniciado em `127.0.0.1:51588` com datadir temporário e `max-connections=500`, sem tocar a instância operacional na porta 3306. O filtro focal de imagens passou 11 aprovados, 1 ignorado e 0 falhas; a classe `ServerPersistenceMariaDbTests` passou 34 aprovados, 1 ignorado e 0 falhas em 35 casos. A execução inicial revelou falha de fixture no teste `GetNextUpSeriesKeys_WithEqualLastPlayedDates_AppliesStableTieBreakBeforeLimit`, causada por `UserData` sem `User` correspondente; o fixture foi corrigido, o teste isolado passou 1/1 e a classe completa foi reexecutada com sucesso. Cleanup confirmou porta liberada e datadir removido. Build Release da solução terminou com 0 erros e 603 avisos existentes. Nenhum código de produção ou ambiente operacional foi alterado. A evidência técnica de persistência de imagens está concluída localmente; CI hospedada, Linux, instalação persistente, runtime corrigido e produção continuam pendentes.
# Atualização de homologação — 2026-11-03 12:22 BRT

- Fallback de capa de livro agora persiste `ProviderName`/`Language` após `SaveImage` via `UpdateToRepositoryAsync`; testes focais **2/2**.
- Imagens de `IDynamicImageProvider` são extrações locais (EPUB, áudio, vídeo ou imagem embutida) e não recebem `ItemImageInfo.ProviderName`, reservado para origem remota; testes dinâmicos **4/4**.
- A preferência global em item vazio passou **1/1** após correção da fixture de teste; o ordenamento singular de produção não foi alterado.
- Providers sem Integration **921/921**; API sem Integration **823/823 aplicáveis**; regressão serial **5.537/5.635**, com 98 ignorados; build Release sem erros.
- Status permanece parcial: runtime instalado 12.1.45 não contém essas correções e gates reais/CI/Linux/produção continuam pendentes.
## 2026-11-03 12:43 BRT — download manual de imagem remota e isolamento API

- Corrigido `RemoteImageController.DownloadRemoteImage`: o endpoint agora resolve o `RemoteImageInfo` pelo URL/tipo, preserva `ProviderName` e `Language` no `ItemImageInfo` salvo e mantém `UpdateToRepositoryAsync`.
- TDD RED/GREEN: `RemoteImageControllerTests.DownloadRemoteImage_PersistsSelectedRemoteImageProvenance` passou 1/1; filtro combinado dos testes que mutam `BaseItem.LibraryManager` passou 5/5.
- Corrida de estado estático foi isolada na coleção não paralela `LibraryManagerTests` para quatro classes API.
- API sem Integration: 824 aprovados, 3 ignorados, 0 falhas/827. Build Release: 0 erros, 576 avisos existentes.
- Aceite permanece parcial: runtime 12.1.45 não contém working tree; integração real/repositório, providers remotos, CI/Linux, instalação e produção continuam pendentes.
## Fallback não-HTTP de imagens e regressão serial — 2026-11-03 12:57 BRT

`ItemImageProvider.DownloadImage` e `DownloadMultiImages` agora distinguem cancelamento de falha comum: `OperationCanceledException` continua propagada; exceções não-HTTP viram falha do candidato, warning redigido e permitem tentar o próximo provider/imagem. O teste `RefreshImages_SingularNonHttpFailureContinuesWithNextCandidate` passou 1/1 após RED; `Jellyfin.Providers.Tests` sem Integration passou 923/923; regressão serial sem Integration passou 5.638 aprovados, 98 ignorados, 0 falhas/5.736; build Release passou com 0 erros. Runtime 12.1.45 não foi alterado.
