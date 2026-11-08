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

## Exclusão segura de imagens de perfil GetAvatar — 2026-10-27

- `AvatarService` apagava imagens por caminho após uma checagem textual, e permitia caminhos aninhados. A substituição de avatar, remoção e limpeza de órfãos agora usam `AvatarFileStore` para validar um arquivo direto no diretório e excluí-lo relativamente ao handle aberto; paths externos/nested e links são mantidos.
- Testes Windows/.NET 10 Release: `AvatarControllerUserAvatarAuthorizationTests` + `AvatarFileStoreTests` 40/40 aprovados com 3 casos Linux ignorados; `Jellyfin.Api.Tests` 765 aprovados, 3 ignorados e 0 falhas. Teste de troca concorrente do nome validado no Windows; o cenário Linux aguarda CI de release.
- Release `v12.1.41` preparada com essa correção. ACL observada na instalação local contém uma ACE de capability AppContainer não identificada com escrita total; não é evidência de exploração, mas mantém a validação de ACL aberta. Formal 2/7, sem crédito; produção não foi alterada.

## Scheduler de biblioteca aguarda workers durante limpeza — 2026-10-27

- A limpeza periódica removia os workers do registro antes de aguardar somente os callbacks de cancelamento. Um novo `Enqueue` podia iniciar outra leva enquanto as tasks anteriores ainda encerravam; `DisposeAsync` também podia deixar de observar esses workers.
- O fluxo agora conserva os runners registrados até o `finally` de cada worker e aguarda cancelamento mais conclusão da task interna na limpeza e no descarte explícito. Teste focal `CleanupWaitsForWorkerTaskAfterCancellationCallbacksComplete` cobre um worker bloqueado após observar cancelamento.
- Sem classe pública, API, dependência, módulo ou novo fluxo externo. `LimitedConcurrencyLibrarySchedulerTests` passou 10/10 e `Jellyfin.Controller.Tests` 73/73 em Release; Windows e Linux x64 foram empacotados e publicados na release [v12.1.40](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/v12.1.40), commit de código `ab03abb7`. A homologação do portal foi atualizada; aceite formal permanece 2/7 (28,6%). Produção não foi alterada.
- Mapa técnico conferido: `LimitedConcurrencyLibraryScheduler` e seus callers permanecem no módulo Controller; o fluxo interno de limpeza/Dispose agora aguarda a saída real dos workers. Nenhuma mudança de topologia ou dependência.

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
### 27/10/2026 — Shutdown seguro do scheduler de biblioteca

- `LimitedConcurrencyLibraryScheduler.Enqueue` usa `ApplicationStopping` para cancelar `WriteAsync` no canal limitado e a espera pela conclusão do workset; evita que uma fila cheia bloqueie o shutdown após os workers pararem.
- O `DisposeAsync` explícito continua drenando enqueues já admitidos antes de concluir o canal. Descarte reentrante solicitado por callback do próprio scheduler falha rápido para evitar ciclo de espera.
- Testes de regressão incluídos em `tests/Jellyfin.Controller.Tests/LibraryTaskScheduler/LimitedConcurrencyLibrarySchedulerTests.cs`.
- Dependências do frontend atualizadas no `MulletaFlix-web-master/package.json` e lockfile: DOMPurify 3.4.17, Vitest/coverage 5.0.5 e Undici transitivo corrigido; auditoria npm sem vulnerabilidades.
- Mapa técnico conferido: mesma classe, singleton e caller `Folder.RunTasks`; sem novo módulo/API/dependência. Versões de dependências existentes foram corrigidas.
