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
   - ganho esperado: muito alto se ainda existir scan ou sort caro.
2. P1 - Fechar `NextUp`:
   - validar o impacto do novo indice em `UserData`;
   - revisar se `GetNextUpSeriesKeys` ainda materializa mais do que precisa.
   - ganho esperado: alto em bibliotecas com muitas series.
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
2. `UpdateOrInsertItemsCore` ainda precisa manter o padrao de lookup O(1) em todas as estruturas internas.
3. O pipeline de reconhecimento de midia e download de metadata ainda faz trabalho demais por item e precisa de mais batching, cache e menos I/O.
4. `BaseItemRepository` ainda possui trechos com materializacao antecipada em consultas quentes.

### P1

1. `MariaDbProcessManager` faz bootstrap com polling bloqueante.
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
- [ ] Validar se a pagina `NFO` segue funcional mesmo quando a carga de usuarios falha.
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

### 28/10/2026 — Retry de backup de usuários e refresh Nebula

- O backup Supabase de usuários MulletaFlix reutiliza `SendSupabaseBatchWithRetryAsync` para até três tentativas idempotentes (merge por `normalized_username`); a falha de migração `PGRST205` mantém mensagem específica.
- `NebulaDirectoryRefreshQueue` reprograma em memória o diretório que falhou após 30 segundos; refreshes seguintes continuam sendo atendidos. Testes determinísticos cobrem HTTP 500→sucesso e refresh temporariamente falho→sucesso.
- Mudança de fluxo nas classes existentes, sem novo módulo, API pública ou dependência. Retry da fila não é persistente após reinício; o scan recuperatório segue limitado a 30 minutos. Focal Windows/.NET 10 Debug: 7/7 aprovados; GitHub CI/PR, serviços reais e produção pendentes. Formal 2/7, sem release.

### 28/10/2026 — Fila durável para refresh VFS do Nebula

- Corrigida retentativa anterior que era só em memória e não sobrevivia a reinícios; além disso, o callback de rclone engolia exceções. Refreshes agora ficam registrados na coleção `nebula_directory_refreshes`, são reidratados no scan periódico e removidos após confirmação do rclone. Falta de readiness/HTTP/resultado rclone não é mais sucesso silencioso.
- Teste de normalização rejeita raiz invisível e traversal; teste `NebulaDirectoryRefreshMongoTests` valida upsert idempotente, sobrevivência entre instâncias e remoção após refresh confirmado, mas requer Mongo opt-in.
- Suíte Release Windows/.NET 10 `FullyQualifiedName~Nebula`: 716 aprovados, 26 ignorados, 0 falhas/742. Mongo descartável, CI hospedada, runtime e produção não foram validados. Estrutura: nova coleção Mongo operacional; sem endpoint ou pacote novo. Formal 2/7, sem release.

### 28/10/2026 — Backup da fila durável de refresh VFS

- O ciclo Supabase nativo agora sincroniza a coleção operacional `nebula_directory_refreshes` como snapshot por `directory_path`: faz upsert dos caminhos ainda pendentes, remove do espelho remoto entradas concluídas e reidrata os caminhos ao restaurar Mongo. Os caminhos restaurados passam pela mesma normalização e validação de raiz visível.
- O SQL de configuração Supabase cria a tabela com chave primária e RLS restrito à `service_role`. Restauração continua mesclada; marcadores remotos são idempotentes.
- Não incluir `nebula_admission` nem `operation_replays`: leases e chaves replay são estado efêmero e podem recriar bloqueios obsoletos. Schema remoto real ainda requer instalação/validação.
- Verificação focal Release: 57 aprovados, 4 ignorados, 0 falhas; com Mongo descartável opt-in, a suíte ampla `FullyQualifiedName~Nebula` passou 744/744 sem ignorados. Migration Supabase `20261028125319` aplicada e conferida: tabela vazia, RLS ativo, policy exclusiva `service_role`, sem privilégios `anon`/`authenticated`, acesso DML `service_role` permitido. CI hospedada, runtime instalado e comportamento end-to-end do backup usando chave de serviço continuam sem validação.

- 28/10/2026: migration `20261028130514_supabase_advisor_cleanup` fixou o `search_path` de `public.set_nebula_updated_at` e removeu o índice duplicado de usernames e o índice `nebula_files.parent` sem scans. C# e setup Python não recriam o índice `parent`; unicidade de `normalized_username` permanece garantida. Advisors Supabase security/performance ficaram sem findings. Validação local do DDL C# e do schema CLI Python passou 1/1 cada.
