# Handover — evolução do servidor e web do MulletaFlix

> **Versão substituída:** para estado consolidado após 03/10/2026, use [`HANDOVER-PROXIMA-IA-SERVIDOR-WEB.md`](HANDOVER-PROXIMA-IA-SERVIDOR-WEB.md). Este documento mantém contexto histórico; o roadmap continua sendo a fonte de verdade.

Atualizado em 03/10/2026. Documento para outra IA continuar a tarefa no mesmo workspace. A fonte de verdade é o roadmap; este arquivo serve como mapa rápido de estado, pendências e limites.

**Estado no handoff:** atualizado em 03/10/2026. Roadmap ainda incompleto; nenhuma release intermediária foi criada. W2.4 concluído e validado. T6.3 recebeu correções adicionais para contenção de caminhos em letras e HLS legado; suítes finais Release passaram: API **292/292**, Providers **777/777**, além de format e `git diff --check`. W1.1 avançou em `ItemsView`, telas administrativas, detalhes legados, `itemsByName` e guardião de conexão: erros não são confundidos com vazio; tela `Unavailable` oferece retry manual e nova tentativa única quando a rede do navegador volta. Última suíte Vitest **311/311 (48 arquivos)**, `build:check`, ESLint focado, build de produção, verificação de 1.903 artefatos e `git diff --check` passaram. ESLint estrito de `itemDetails/index.ts` continua falhando com 48 findings legados de `any`/complexidade. Consulte roadmap e `git status --short` antes de editar.

## Objetivo e limites

Continuar a implementação do roadmap em `docs/roadmap-evolucao-tecnologica-servidor-e-web.md`, atualizando seus checkboxes e o registro de execução conforme cada entrega for validada.

- Escopo ativo: servidor Windows/Linux e frontend web distribuído com o servidor.
- Curadoria de catálogo por IA está explicitamente fora do escopo ativo; manter somente no backlog futuro.
- Não desenvolver nem alterar o APK nesta conversa. Se surgir uma melhoria exclusiva do APK, registrá-la para a conversa do APK e não a implementar aqui.
- Todos os artefatos oficiais devem ser de produção.
- **Não criar, empacotar nem publicar release intermediária.** Só iniciar a release final quando todo o escopo ativo do roadmap estiver implementado e validado. Então publicar servidor e portal em conjunto, respeitando as instruções atuais de `AGENTS.md` e do projeto. As notas devem refletir somente o diff validado.
- Versionamento do servidor: `MAJOR.MINOR.PATCH`, `PATCH` 0–99 e `MINOR` 0–9; ao exceder, promover a parte anterior e zerar as seguintes.

## Método e skills

Aplicar Builder vs. Evaluator (Gauntlet Loop) a toda alteração: critérios de aceite antes do código, testes reais, revisão adversarial e correção até passar. Consultar `.agents/rules/mulletaflix-conventions.md`, `.agents/rules/gauntlet-loop.md` e a seção de skills no roadmap.

Skills base fornecidas para este ciclo: `gauntlet-loop`, `fable-method`, `fable-loop`, `fable-judge`, `backend-architect`, `frontend-design`, `caveman`, `cavecrew`, `data-engineer`. Ler integralmente a skill aplicável no início de cada tarefa; selecionar as skills especialistas por tarefa na tabela do roadmap (por exemplo: `csharp-testing`, `react-testing`, `vitest-skill`, `playwright-skill`, `api-security-testing`, `database-architect`, `performance-engineer`, `accessibility-compliance-accessibility-audit`).

## Estado do repositório e operação

- Workspace: `D:\Users\Raphael\Documents\Projetos\mulletaflix`.
- Branch: `main`; HEAD observado: `b6f8562b` (`Fix local network permission retry and retry truncated playback fetches`).
- Há muitas alterações **não commitadas** do trabalho corrente e de outros fluxos. Preservar todas; não usar reset/checkout, não limpar arquivos desconhecidos e não incluir alterações Android numa release do servidor.
- A árvore contém mudanças do servidor em Nebula/playback, Open Library e metadados locais de livros; mudanças web em `UserFeedbackListPage`; documentação do roadmap; além de alterações e arquivos não rastreados Android, anexos e ferramentas de outro trabalho. Verificar `git status --short` imediatamente antes de editar/validar.
- Instância local respondeu em `http://localhost:8096/System/Info/Public`, produto MulletaFlix, versão observada `12.1.10`. Essa resposta pública não autentica nem comprova funcionamento de operações administrativas.
- A pasta `I:\Meu Drive\Livros` está acessível. Uma inspeção read-only preliminar contou 1.640 arquivos de livro; foram encontrados sidecars NFO e muitas imagens, mas **não foi verificada a correspondência individual entre cada livro, NFO e capa, nem executado refresh/backfill**. Não declarar catálogo de livros corrigido/validado em produção com base nesse inventário.
- Nenhuma release ou implantação foi feita por esta continuação.

## Progresso comprovado no roadmap

O roadmap é a fonte de verdade, incluindo evidências, limites e histórico por tarefa. Resumo das frentes:

- **T0 — baseline:** mapa de fluxos/dependências pronto; cenários e medições parciais. Ainda faltam amostra sob carga representativa, validação remota dos jobs e budgets sustentados por medições.
- **T1 — observabilidade/operação:** código para OpenTelemetry, indicadores, logs/auditoria, health checks, alertas e diagnósticos amplamente implementado/testado. Restam integração operacional com collector/retention, validação dos agregados/índices no volume real e auditorias explicitamente anotadas no roadmap.
- **T2 — Nebula:** máquina de estados, idempotência, prioridade, fairness/backpressure, recuperação, limpeza/startup e refresh da listagem virtual marcados concluídos no roadmap; não confundir testes unitários com validação operacional de produção.
- **T3 — reprodução/cache:** contrato, leases, single-flight e painel concluídos. T3.2 e T3.6 ainda parciais: pré-cache/troca/cancelamento e recuperação automatizada têm testes, mas falta reproduzir/parar o fluxo completo num Nebula operacional e confirmar o comportamento com o serviço ligado.
- **T4 — backup/recuperação:** inventário e diversos cenários de consistência/restore/painel cobertos; T4.2–T4.4 permanecem parciais e exigem política e validação abrangente. T4.5 está marcado concluído com os exercícios documentados; revisar evidências/plataformas indicadas no histórico antes de afirmar cobertura além delas.
- **T5 — bancos/cache:** fonte de verdade/topologia e avaliação de Change Streams documentadas. Ainda abertos: baseline de consultas/pool, otimização baseada em plano real, HybridCache somente se justificado e carga/degradação.
- **T6 — APIs/segurança:** existem correções e instrumentação de rate limiting/filas em vários endpoints/tarefas. Permanecem abertas auditorias completas de autenticação/autorização, rate limits seletivos, validação/canonicalização de caminhos, segredos/dependências/transporte e configurações seguras por padrão.
- **T7 — FFmpeg:** diagnóstico, capacidades, fallback, limites e matriz de compatibilidade marcados concluídos; conservar testes/evidências do roadmap.
- **T8 — catálogo/provedores:** relatório determinístico, validação de fontes e proveniência/campos travados marcados concluídos. Corrigida a seleção ambígua de livros no Open Library e preparado o salvamento de NFO/capas locais. Ainda falta reconciliação/refresh real e auditar a persistência lado a lado para os livros da biblioteca; não sobrescrever capas ou metadados manuais sem critério seguro.
- **W1 — experiência web:** estados e navegação principais evoluíram; solicitação de mídia tem autocomplete/indexação, filas separadas e prioridade parciais. W1.1 recebeu uma parcela validada para grades de solicitações pendentes/incluídas e reports administrativos; ainda não está completo nas demais rotas/estados. W1.2, W1.3 e W1.5 seguem abertos.
- **W2 — acessibilidade:** auditorias WCAG 2.2 AA, leitor de tela, operação completa por teclado/controle remoto e coverage axe em rotas principais seguem parciais. W2.4 (labels, validação inline e anúncios de feedback) está concluído; veja o histórico e evidências abaixo.
- **W3 — desempenho web:** medições Web Vitals, budgets, imagens, cache TanStack, benchmark para virtualização e rede/dispositivos lentos seguem abertos.
- **W4 — regressão web:** há testes Playwright, Stylelint e gate para flaky tests em progresso. Faltam cobertura de todos os fluxos críticos, snapshots revisados, paginação/interações adicionais, confirmação em CI hospedado e investigação dos warnings registrados.

## Alterações recentes e estado que exige retomada

### Atualização de handover — 03/10/2026 (T4.3, em andamento)

- Foi acrescentado `FullRestore_WritesAndQueriesApplicationUserFromIsolatedSupabaseFixture` em `NebulaSupabaseSyncTests.cs`: exercita o restore completo contra respostas HTTP fictícias e `UsersDbContext` InMemory, incluindo persistência e consulta do usuário restaurado, contagens, timestamp e duração. URL `.invalid` e chave fictícia; nenhuma chamada ou escrita Supabase real.
- Foi criado `NebulaSupabaseRestoreMongoTests.cs` para testar o caminho completo com Mongo isolado e fixtures HTTP, incluindo usuários FTP, tokens de bot e usuário do aplicativo. **Não executou nesta máquina** porque o Mongo opt-in não está configurado.
- Testes Mongo passaram a exigir `MULLETAFLIX_TEST_MONGODB_CONNECTION_STRING`, validado por `NebulaMongoTestConnection`: somente o endereço opt-in local `127.0.0.1:27099`, sem credenciais nem múltiplos hosts. A string entregue ao driver é fixa e define `directConnection=true`, impedindo a descoberta de membros de replica set remotos. Os testes usam nome de banco com GUID completo e removem apenas o banco gerado. O teste de topologia Change Streams também requer o opt-in; removida a sondagem automática de `127.0.0.1:27017`.
- Evidência local após essas correções: build Release dos testes exit 0 (**0 erros, 313 avisos**, incluindo analisadores; SA1117 corrigido); filtro focado sync/restore/Mongo exit 0: **27 aprovados, 14 ignorados, 0 falhas**. Suíte completa `Jellyfin.Server.Implementations.Tests`: **1.302 aprovados, 53 ignorados, 0 falhas, 1.355 total**. `dotnet format whitespace --no-restore --verify-no-changes` nos arquivos Nebula/cache e `git diff --check`/scan de whitespace passaram. A revisão adversarial final confirmou que os riscos de discovery remoto, probe da porta 27017 e forwarding de token foram corrigidos; observou como limites restantes o restore Mongo não executado e o risco operacional de um túnel configurado para remoto.
- A mesma compilação detectou CA2016 em `NebulaPlaybackCache`; `StartPrefetchWithHandle` agora encaminha o token do ciclo a `_storageGate.Wait(lifecycleCancellationToken)` e mantém a verificação pós-aquisição. Build e suíte completa passaram depois. Os testes determinísticos reportam `ElapsedSeconds` como output observável; não existe limite/RTO aprovado, portanto nenhum threshold arbitrário é usado.
- Ignorados que dependem do Mongo de teste opt-in não contam como restore Mongo validado. O restore completo HTTP/InMemory foi aprovado. Checagem read-only confirmou listener em `127.0.0.1:27017` (PID 4580), sem listener em `27099`; nenhum executável `mongod`, Docker ou Podman foi encontrado. Não conectamos ao processo de `27017`. Mesmo com `directConnection=true`, um túnel local poderia apontar para remoto; o operador deve garantir destino descartável/local antes de configurar opt-in.
- O serviço Mongo dedicado não está configurado; não conectar ao `mongod` não identificado do host nem usar produção. T4.3 continua parcial até executar exercícios isolados completos e fechar medidas de contagem e RTO/RPO.
- Alterações no worktree, não commitadas. Nenhum pacote, release, push, atualização do portal ou escrita em dados de produção nesta atualização.

### Atualização desta sessão — 03/10/2026 (W1.2, cobertura de debounce)

- A próxima fatia escolhida foi **W1.2 — fluxo ponta a ponta de solicitação de mídia na web**, por ser independente do Mongo de teste pendente e não exigir acesso a produção.
- Antes de editar, inspecionar o contrato e os testes já existentes; não reimplementar subtarefas já concluídas. Arquivos/localizações identificados: `MulletaFlix-web-master/src/apps/experimental/routes/myrequests.tsx`, `myrequests.test.tsx`, `src/hooks/api/useMediaRequests.ts` e seus testes, `src/components/userFeedback/userFeedback.ts` e testes, `src/controllers/hometab.ts`, `src/controllers/home.html`, `src/apps/experimental/components/AppToolbar/userViews/UserViewNav.tsx`.
- Já existem configurações/specs Playwright `playwright.requests.config.ts`, `tests/playwright/specs/25-myrequests.spec.ts`, `26-request-autocomplete.spec.ts` e fixture `tests/playwright/fixtures/myrequests.tsx`. Ler antes de decidir o gap; em particular, testar integração realista de autorização/visibilidade do botão junto a Favoritos, debounce/cancelamento, estados de indexação e confirmação, além das duas grids. Não presumir que a presença de specs comprova E2E de ponta a ponta.
- Skills para esta fatia: reler integralmente `gauntlet-loop`, `fable-method`, `react-testing`, `vitest-skill` e `playwright-skill`; ler `frontend-api-integration-patterns`/`frontend-data-contracts` se a investigação revelar problema no contrato API. Aplicar `cavecrew` para revisão adversarial se houver mudança de comportamento. Seguir também as instruções globais de skills do projeto.
- Critérios de aceite vêm do item W1.2 no roadmap: fluxo validado da interface ao endpoint, autorização correta, autocomplete sem resposta obsoleta após cancelamento, indicação de título já incluído, grids pendente/incluído e prioridade/posição visíveis, confirmação do envio e testes de regressão apropriados. Refinar após inspeção de código/specs.
- Nova cobertura E2E em `MulletaFlix-web-master/tests/playwright/specs/26-request-autocomplete.spec.ts` confirma que a busca substituída antes do debounce de 250 ms não chama o endpoint, e que uma resposta atrasada de busca antiga não substitui nem reaparece depois do resultado atual. O código de produção não precisou de alteração nesta fatia.
- `MulletaFlix-web-master/src/apps/experimental/components/AppToolbar/userViews/UserViewNav.test.tsx` monta o componente real da toolbar e confirma visibilidade da solicitação junto a Favoritos com usuário autenticado, abertura do diálogo pelo cliente API atual e ausência de ações de usuário sem identidade.
- Verificações desta fatia final: Playwright de autocomplete **2/2**, suíte `test:playwright:requests` **8/8**, Vitest **292/292 (45 arquivos)**, `npm run build:check`, ESLint dos dois specs e `git diff --check` passaram. `npm run build:production` e `npm run verify:build` (**1.903 arquivos; 1.536 KiB máximo por artefato**) passaram na mesma sessão imediatamente antes da adição somente de arquivo de teste; fonte de produção não mudou. Builds mantêm avisos Vite existentes para diretivas `use client` e imports/chunks.
- W1.2 continua parcial: testes de rede/autenticação são simulados e não validam sessão real, servidor instalado nem prioridade/filas Nebula em runtime. A tentativa de revisão delegada terminou sem parecer; inspeção manual verificou controle do gate assíncrono no spec e links MUI como âncoras ao lado do botão nativo. Nenhuma release parcial.

### Atualização desta sessão — 03/10/2026 (W2.4 concluído)

- Labels visíveis e associados para todos os campos em solicitação/reporte de reprodução. Formulários `novalidate` usam verificação de constraints com erros próximos dos campos, `aria-describedby`/`aria-invalid` e foco no primeiro erro. Título vazio, tipo/categoria não selecionados e ano fora de 1888–2200 recebem orientação localizada; os selects iniciam num prompt vazio para evitar envio de padrão acidental.
- Falha de API anuncia alerta no formulário, preserva valores e reabilita retry. Sucesso usa live region `role=status` fora do diálogo para continuar anunciado depois do fechamento.
- Validação final: teste focal **15/15**, Vitest **300/300 em 45 arquivos**, Playwright axe/WCAG 2.2 AA **3/3**, `npm run build:check`, ESLint dos dois arquivos TS, JSON en-us/pt-br válido, build de produção, verificador de artefatos **1.903 arquivos / 1.536 KiB máx.** e `git diff --check`, todos exit 0. Avisos existentes Vite/jsdom não foram introduzidos por esta tarefa.
- Limite: Playwright a11y não exercita esses diálogos; a cobertura de formulário é DOM unitário, sem sessão manual de leitor de tela. W2.4 foi marcado concluído no roadmap; W2.1–W2.3 continuam abertos. Nenhuma release/implantação; gate só abre depois de todas as melhorias ativas.

### Última fatia validada — 03/10/2026 (T6.3: letras e HLS legado)

- `LyricManager` agora só aceita extensões seguras formadas por caracteres ASCII alfanuméricos, `-` e `_`, preservando formatos customizados de plugins sem permitir separadores, ponto, dois-pontos ou controles. A contenção compara caminhos canonicalizados por `Path.GetRelativePath`; componentes existentes marcados como `ReparsePoint` são recusados, inclusive symlink pendente/dangling. Testes em `LyricManagerPathSafetyTests.cs` cobrem extensão válida/customizada, entradas maliciosas, limite raiz/prefixo e symlinks.
- `HlsSegmentController` agora valida os segmentos/extensões recebidos nas rotas legadas e restringe playlist/segmento a arquivo direto dentro da pasta de transcode, com contenção canonicalizada e rejeição de reparse points. Corrigido também o predicado invertido que impedia servir playlist `.m3u8` válida. Testes em `HlsSegmentControllerPathTests.cs` cobrem traversal para sibling com prefixo comum, symlink externo e playlist válida.
- Compatibilidade: não foi adicionada autenticação às rotas HLS legadas; comentários no código indicam necessidade para clientes Chrome/iOS que omitem query string. Isso preserva o contrato atual, mas deve continuar explicitamente no inventário de autorização T6.1.
- Evidência final após os últimos ajustes: `dotnet test tests/Jellyfin.Api.Tests/Jellyfin.Api.Tests.csproj -c Release --no-restore` **292/292**, exit 0; `dotnet test tests/Jellyfin.Providers.Tests/Jellyfin.Providers.Tests.csproj -c Release --no-restore` **777/777**, exit 0; `dotnet format whitespace` scoped aos quatro arquivos C# alterados/novos passou; `git diff --check` passou. Os testes de symlink/dangling symlink executaram no host (sem skips).
- Scan de verificações de containment por `StartsWith` encontrou só `MediaBrowser.Providers/Subtitles/SubtitleManager.cs:257-258`; a raiz inclui separador final e a extensão está em allowlist, então não reproduz o prefix escape corrigido aqui.
- Limite: não foi resolvido o risco TOCTOU entre validar path/reparse point e o acesso posterior ao arquivo; as rotas HLS legadas seguem sem auth por compatibilidade. Revisar primitives seguras Windows/Linux e documentar o contrato antes de afirmar T6.3 concluído. Não houve release, pacote, commit, push ou escrita em produção.
- Preservar todas as modificações existentes. O worktree inclui mudanças de servidor, web, Android, handovers e anexos; Android não pertence a este escopo.

### Servidor / livros

- `MediaBrowser.Controller/Entities/Book.cs`: livros locais habilitam salvamento de metadados local quando suportado.
- `MediaBrowser.XbmcMetadata/Savers/BookNfoSaver.cs` e teste novo `tests/Jellyfin.XbmcMetadata.Tests/Location/BookNfoLocationTests.cs`: NFO no mesmo diretório, com nome-base do livro; IDs ISBN/OpenLibrary exportados.
- `MediaBrowser.Providers/Manager/ImageSaver.cs` já salva localmente a imagem quando o item local permite. O formato/nome obedece à convenção configurada (por exemplo `poster`/`folder` ou convenção compatível e, em mixed folders, nome associado ao item). A verificação read-only não provou que todos os livros reais têm os dois sidecars corretos. Planejar teste/refresh autenticado ou mecanismo de backfill reversível, sem escrita em massa cega.
- `OpenLibraryProvider.cs` e seus testes: ranking por título normalizado e seleção de ISBN exato em meio a resultados fora de ordem. Histórico diz que o teste focado passou 10/10, mas suíte integral de Providers ficou parada e foi interrompida; revisão/validação integral continuam pendentes.

### T6.3 — validação de caminhos, fatia de 03/10/2026

- `LibraryStructureController.IsPathWithinRoot` agora usa canonicalização relativa e recusa `ReparsePoint` em componentes existentes abaixo da raiz; trata caminhos inválidos/inacessíveis como não autorizados. Testes reproduzem entrada com NUL e descendente através de symlink.
- Evidência atual: `dotnet test tests/Jellyfin.Api.Tests/Jellyfin.Api.Tests.csproj -c Release --no-restore` **275/275**; `dotnet format whitespace --verify-no-changes` para controller e teste; `git diff --check` passou. Warns no build são de comentários XML e analyzer xUnit em arquivos preexistentes diferentes.
- Limite aberto: TOCTOU entre checagem e `Directory.Move`; investigar primitivos seguros disponíveis em Windows/Linux antes de alegar proteção completa. T6.3 segue parcial.
- Continuação: rota aceita apenas nomes de segmento único sem `/`, `\\`, `.`/`..`, raiz absoluta ou NUL e repete as checagens depois de adquirir o gate. Evidência atualizada: suíte API Release **284/284**, ambos os `dotnet format whitespace --verify-no-changes` e `git diff --check` scoped passaram. O gate reduz concorrência entre chamadas do servidor, mas não elimina TOCTOU contra alterações locais do filesystem.

### T6.2 — admissão do upload de logs de cliente (03/10/2026)

- `/ClientLog/Document`: até 5 requisições/minuto por usuário/API key; `429` inclui `Retry-After`. Teste garante identidade separada por usuário apesar de IP compartilhado. `RateLimitMiddlewareTests` 51/51; suíte API completa 288/288 em Release. Cota não calibrada sob carga real; T6.2 continua parcial.

### Nebula / pré-cache de reprodução

- Alterações em `NebulaChunkedStream.cs`, `NebulaHttpStreamServer.cs`, `NebulaPlaybackCache.cs`, `NebulaPlaybackSessionMonitor.cs` e testes. Lease fica com leitores de playback; worker auxiliar recebe cancelamento do ciclo; cancelamentos são protegidos por identidade/generation.
- Histórico de 03/10: focused tests 6/6, filtros Nebula 63/63, suíte Implementations 1.300 aprovados/51 ignorados (1.351), build Release `Jellyfin.Server` 0 warnings/0 errors, format e diff check aprovados. Revisão adversarial encontrou e levou à correção de corrida.
- Histórico de 02/10: caminho de resposta curta no cache 56/56. T3.2/T3.6 ainda exigem teste com serviço operacional; não declarar playback perfeito.

### Web — última parcela concluída

- Arquivo editado: `MulletaFlix-web-master/src/apps/dashboard/routes/user-feedback/UserFeedbackListPage.tsx`.
- Teste recém-adicionado: `MulletaFlix-web-master/src/apps/dashboard/routes/user-feedback/UserFeedbackListPage.test.tsx` (não estava na primeira listagem do status, mas foi criado logo depois; confirme no `git status`).
- Resultado: estados vazios acessíveis nas grades de solicitações pendentes/incluídas e reports, sem confundir vazio com loading; skeleton, erros e retry preservados.
- Validado após revisão adversarial: teste focado **9/9**, suíte Vitest **290/290 (44 arquivos)**, `npm run build:check`, ESLint dos arquivos alterados, `npm run build:production`, `npm run verify:build` (**1.903 arquivos; limite 1.536 KiB**) e `git diff --check` passaram. Build ainda mostra avisos de dependências Vite sobre diretivas `use client` ignoradas. O revisor confirmou os retries positivo/negativo do catálogo e não encontrou findings restantes. Evidência registrada em W1.1 do roadmap; W1.1 permanece parcial nas outras rotas/estados.

### Parcela web mais recente — 03/10/2026 (W1.1; seções assíncronas de conteúdo)

- Em `MulletaFlix-web-master/src/apps/experimental/components/library/ItemsView.tsx`, o resultado ausente após falha da API era interpretado como sucesso vazio. Agora a página mostra alerta recuperável com retry; mantém cartões de uma resposta anterior se o refresh falha; usa contagem desconhecida (`—`) se não há dados; e só mostra “sem itens” após resposta vazia bem-sucedida.
- Testes novos em `ItemsView.test.tsx` cobrem falha inicial, acionamento de retry, snapshot anterior não vazio com falha, snapshot anterior vazio e resposta vazia válida. Um teste vermelho demonstrou o defeito e outro detectou texto `0` produzido por condição JSX; a condição final é booleana.
- Evidência final: Vitest **305/305 em 46 arquivos**, `npm run build:check`, `npm run build:production`, `npm run verify:build` (**1.903 artefatos; máximo 1.536 KiB**), ESLint nos dois arquivos e `git diff --check` passaram. Vite reportou avisos preexistentes `use client`/divisão de chunks; jsdom reporta warnings de `getComputedStyle` e fetch de config.
- Foi acrescentado `components/asyncItemsSection.ts` como tratamento comum para “A seguir”, temporadas/episódios, itens de álbum/playlist, especiais, partes adicionais, itens semelhantes e grades carregadas por `scripts/itemsByName.ts`. Em falha exibe `role=alert`, retry localizado e `aria-busy`; resposta vazia válida continua ocultando seção. O alerta anterior é removido ao repetir; outras seções carregadas não são removidas.
- Testes `asyncItemsSection.test.ts` cobrem falha visível seguida por retry bem-sucedido e resposta vazia sem erro. Twin scan encontrou e corrigiu a mesma falha em `scripts/itemsByName.ts`; a varredura final não encontrou outras ocorrências do padrão catch-oculta-seção.
- Evidência final: Vitest **307/307 em 47 arquivos**, `npm run build:check`, ESLint no helper/teste e `itemsByName.ts`, build de produção, `npm run verify:build` (**1.903 artefatos válidos, limite 1.536 KiB**) e `git diff --check` passaram. ESLint estrito de `controllers/itemDetails/index.ts` reportou 48 findings de `any` e complexidade no arquivo legado; Vite/jsdom também continuam com os avisos já observados.
- W1.1 segue parcial: faltam offline/degradado e validação visual/browser de todos os estados/rotas. Não houve revisão separada por subagente nem release/publicação. Sem release intermediária; curadoria IA segue fora do escopo ativo.

## Próximas tarefas — checklist completo do roadmap

O status detalhado e os critérios de aceite vivem no roadmap. A lista abaixo reproduz todas as tarefas de nível superior ainda abertas em 03/10/2026; subtarefas já concluídas continuam marcadas no roadmap.

### P0 — baseline, playback e recuperação

- [ ] **T0.1:** repetir baseline sob carga representativa; coletar duração/volume Nebula autenticados, footprint de dados, Linux e janela temporal suficiente.
- [ ] **T0.3:** confirmar execução remota dos jobs Windows/Linux publicados e completar cenários integrados de recuperação de banco/Telegram sem serviços ou credenciais de produção.
- [ ] **T0.4:** medir séries representativas de boot, busca, recursos, tráfego comprimido e cache antes de definir budgets globais.
- [ ] **T3.2:** validar playback real e prefetch integral/cancelamento em serviço Nebula operacional. **Dependência operacional conhecida:** usuário determinou Nebula desativado (`Enabled=false`); não ativar sem autorização.
- [ ] **T3.6:** completar recuperação integrada, incluindo reinício do host e fluxo de mídia, além dos testes unitários já existentes. Depende do mesmo Nebula operacional e decisão de ativação.
- [ ] **T4.2:** fechar política de snapshot consistente, criptografia/chave, retenção efetiva e remoção remota. **Não decidir por conta própria:** aguarda operador para esquema/local da chave, valores de retenção, política Supabase e janela de manutenção para coordenar MariaDB/MongoDB.
- [ ] **T4.3:** completar teste isolado de recuperação de todos os conteúdos/fontes remotas, incluindo MongoDB↔Supabase; medir contagens, consultas críticas e RTO/RPO. Fixture HTTP + EF InMemory passou; suíte Implementations passou com 1.302/53; 14 casos do filtro focado foram ignorados, incluindo restore Mongo completo. Configurar instância de teste comprovadamente isolada e executar; credencial de produção não deve ser usada.
- [ ] **T4.4:** completar painel com último/próximo backup, destino, conteúdo/tamanho, validação, erro e ação protegida; falta apresentar estado do exercício completo MongoDB/arquivos.

### P1 — banco, segurança e API

- [ ] **T5.1:** medir consultas caras, planos, pool, espera e locks em workload representativo.
- [ ] **T5.2:** otimizar paginação/projeção/índices somente conforme planos e medições reais.
- [ ] **T5.4:** avaliar HybridCache; implementar apenas se leitura repetida e invalidação segura forem comprovadas.
- [ ] **T5.6:** testar carga/degradação (pool esgotado, reconexão, volume alto, índice ausente, upgrade/migração).
- [ ] **T6.1:** auditoria completa de autenticação/autorização por endpoint, incluindo admin/Nebula/backups/reports.
- [ ] **T6.2:** rate limits e concorrência seletivos, calibrados sem afetar reprodução; verificar `429`/`Retry-After`.
- [ ] **T6.3:** validação/canonicalização de entradas e paths: traversal, symlink, extensão, tamanho e autorização.
- [ ] **T6.4:** segredos/logs/transporte: repouso, rotação, permissões, redação e dependências vulneráveis.
- [ ] **T6.5:** defaults seguros de rede/TLS/CORS/headers/contas/endpoints internos e diagnósticos.

### Web — experiência, acessibilidade, desempenho e regressão

- [ ] **W1.1:** completar padronização loading/vazio/erro-retry/offline/sucesso em home, busca, detalhes, solicitações e gestão. A última parcela cobriu só grades administrativas.
- [ ] **W1.2:** E2E da solicitação de mídia: botão autorizado ao lado de Favoritos, autocomplete com debounce/cancelamento, indexação, resultado já incluído, grids pendente/incluído, prioridade e confirmação.
- [ ] **W1.3:** corrigir e validar setas/carrosséis, busca, teclado/controle remoto, foco e responsividade.
- [ ] **W1.5:** consolidar tokens/componentes sem reescrita global; cabeçalho, botões, grids, diálogos, alertas e estados.
- [ ] **W2.1:** auditoria WCAG 2.2 AA completa (contraste, foco, semântica, labels, status, zoom/reflow, orientação e touch targets).
- [ ] **W2.2:** teclado/controle remoto: ordem e retorno de foco, setas, Escape, diálogos, atalhos e ausência de armadilhas.
- [ ] **W2.3:** axe + Playwright nas rotas principais e avaliação manual por teclado/leitor de tela; testes automáticos já cobrem parcelas.
- [x] **W2.4:** labels visíveis, validação inline com orientação de correção e anúncios acessíveis de sucesso/falha nos formulários de feedback; implementação e testes automatizados concluídos.
- [ ] **W3.1:** Web Vitals por rota (laboratório e RUM opt-in/anônimo somente se aprovado).
- [ ] **W3.2:** budgets de bundle/recursos e lazy loading de rotas/ferramentas pesadas, justificados por medição.
- [ ] **W3.3:** posters/backdrops: tamanho adequado, lazy loading, prioridade do hero, placeholder e espaço reservado.
- [ ] **W3.4:** cache TanStack: chaves, staleTime, invalidação, cancelamento, retry e ordenação de respostas.
- [ ] **W3.5:** benchmark para decidir virtualização; preservar acessibilidade e navegação.
- [ ] **W3.6:** rede/CPU/dispositivos lentos, telas pequenas, TV e controle remoto.
- [ ] **W4.1:** Playwright dos fluxos críticos (busca, detalhes, solicitações, reprodução, perfil e gestão).
- [ ] **W4.2:** snapshots visuais estáveis em celular/desktop/TV, com baseline humano revisado e navegador fixado.
- [ ] **W4.3:** interações/paginação: setas, scroll, foco, loading e “carregar mais”.
- [ ] **W4.4:** quality gate frontend completo em CI: build check, testes, ESLint/Stylelint, produção, artefato e Playwright.
- [ ] **W4.5:** política de flaky tests com trace/screenshot e sem retries ilimitados.

### Itens concluídos de nível superior

Conforme roadmap, estão marcados concluídos: **T0.2; T1.1–T1.5; T2.x; T3.1, T3.3–T3.5; T4.1 e T4.5; T5.3 e T5.5; T7.x; T8.x**. Isso não elimina subtarefas operacionais declaradas abertas no registro. W1 e W2–W4 têm trabalho parcial, mas nenhuma dessas frentes está concluída integralmente.

### Retomada sugerida

1. Antes de alterar qualquer coisa, reler `AGENTS.md`, convenções, Gauntlet e skills que se aplicam; comparar roadmap e `git status --short` para preservar alterações concorrentes.
2. Não ligar Nebula nem executar escrita em dados reais. Pode avançar em testes isolados/determinísticos enquanto a ativação continuar proibida.
3. Próxima fatia recomendada: continuar W1.1 cobrindo offline/degradado e estados nas demais rotas, em especial o grid web/admin renderizado em browser com sessão real. Em paralelo ou depois, continuar T6.3 investigando TOCTOU e T6.1 autorização por endpoint.
4. **W1.2 continua parcial, mas já avançou:** há testes Playwright de debounce e resposta obsoleta (2/2; suíte requests 8/8) e teste da toolbar real com identidade/API mockadas (Vitest 2/2; suíte 292/292). Falta integração ponta a ponta com sessão de teste real/servidor instalado, confirmar envio → “Minhas solicitações”, estados de inclusão e prioridade/filas Nebula. Os testes atuais não demonstram runtime real.
5. T4.3 permanece pendente até existir Mongo descartável explicitamente isolado. Não usar credenciais nem dados Supabase de produção. Os testes opt-in ignorados não contam como aprovados.
6. A verificação de livros (1.640 itens inventariados) é trabalho de dados real separado: falta correspondência individual de cada livro/capa/NFO e backfill/refresh. Exigir plano reversível, preview/backup e não usar IA neste ciclo.
7. Atualizar roadmap e este handover com comandos, resultados, exit codes e limitações após cada parcela validada.
8. Não criar pacote/release parcial. A release final só pode começar quando todas as tarefas ativas acima e subtarefas do roadmap estiverem concluídas e comprovadas, seguida de publicação do servidor/portal e validação da API oficial conforme `AGENTS.md`.

## Qualidade e comandos

Usar os scripts/targets reais listados no projeto, no mínimo:

- Servidor: testes focados por projeto e suítes afetadas; `dotnet build`/`dotnet test` em configuração `Release`; `dotnet format whitespace --verify-no-changes`; `git diff --check`.
- Web: teste Vitest focado e suíte relevante, `npm run lint:changed` ou ESLint/Stylelint aplicável, `npm run build:check`, `npm run build:production`, verificação do artefato e Playwright relevante.
- Guardar no roadmap os comandos exatos, contagens, exit codes, limitações e se foram local/remotos. Não incluir testes ignorados como aprovados.

## Antes de continuar

1. Rodar `git status --short` e preservar todas as alterações atuais; confirmar especialmente arquivos não rastreados e mudanças Android que não pertencem a esta conversa.
2. Ler `AGENTS.md`, convenções e skill relevante; não presumir que o resumo deste handover substitui a documentação fonte.
3. Inspecionar o estado atual da instância e testes; não iniciar processos de longa duração em paralelo sem acompanhar/terminar o handle.
4. Não fazer commit, push, publicar release ou alterar portal até o gate do roadmap ser satisfeito e a publicação final ser explicitamente a etapa atual.

## Atualização mais recente — 03/10/2026 (W1.1 parcial)

- `ConnectionErrorPage` tem retry manual localizado e retry automático único no evento `online`, somente no estado `ConnectionState.Unavailable`. `ConnectionRequired` liga o callback a reload integral para refazer o fluxo. Mismatch/upgrade não reagem ao evento. Listener usa `once` e é removido no unmount.
- `ConnectionErrorPage.test.tsx` cobre retry manual, retorno online, execução única mesmo com evento repetido, cleanup e ausência de retry em mismatch. TDD observou teste falhar antes do listener (0 callbacks). Validação final: teste focal **4/4**, Vitest **311/311 em 48 arquivos**, ESLint focado, `npm run build:check`, build de produção, `npm run verify:build` (**1.903 arquivos; limite 1.536 KiB**) e `git diff --check`, todos exit 0. Avisos Vite/jsdom conhecidos continuam.
- Limite conhecido: teste usa jsdom; não houve sessão real de navegador com rede/servidor MulletaFlix. Retry faz reload integral. W1.1 não está concluído.
- Roadmap e handover atualizados. Sem pacote/release, commit/push ou publicação do portal.

## Próximas tarefas recomendadas para a IA que assumir

1. **Retomar W1.1 (parcial):** o guardião tenta novamente uma vez quando ocorre `online`; completar estados offline/degradado em home, busca, detalhes, solicitações e gestão; validar rotas e recuperação visualmente em browser, sem depender apenas de mocks. Preservar os estados de erro parciais já corrigidos e cobrir sucesso/vazio/erro/loading.
2. **Fechar W1.2 (parcial):** executar uma jornada com sessão e servidor de teste reais para autocomplete/indexação, solicitar → aparecer em “Minhas solicitações”, separar pendentes/incluídos e confirmar prioridade. A posição na fila ainda não é correlacionada entre ActivityLog (texto livre) e caminho da fila Nebula; desenhar vínculo seguro antes de implementar. Specs atuais são simulados e não provam runtime real.
3. **T6.1–T6.5 (parciais):** concluir inventário endpoint-a-endpoint de autenticação/autorização; revisar cotas seletivas com carga; validar/canonicalizar caminhos, symlinks e TOCTOU em Windows/Linux; tratar segredos/logs/transporte/dependências e defaults de segurança. T6.3 ainda tem risco TOCTOU e rotas HLS legadas sem autenticação por compatibilidade, explicitamente registradas.
4. **T4.3 (parcial):** obter MongoDB descartável dedicado com opt-in local `127.0.0.1:27099` e exercitar restauração integral Mongo; testes ignorados por falta do Mongo não contam como aprovados. Nunca usar instância desconhecida em `27017`, Supabase/Telegram ou produção.
5. **T4.2/T4.4:** terminar política e validação integral de backups, escopo/cobertura/destino e restore isolado com relatório; confirmar ciclo de usuário/Supabase dentro da autorização já estabelecida. Não declarar o backup operacional só pelo estado “Conectado”.
6. **T3.2/T3.6:** executar o ciclo real de playback + pré-cache, troca/cancelamento e recuperação no serviço isolado operacional. Testes unitários determinísticos não comprovam playback com Nebula ligado.
7. **T5.1/T5.2/T5.4:** coletar baseline real de consultas/pool; otimizar apenas com explain/plano e benchmark; introduzir HybridCache/Change Streams só se medição e topologia justificarem; testar carga e degradação.
8. **T0/T1:** completar medições representativas, validação operacional remota de jobs/collector/retention/alertas e auditoria de agregados/índices no volume real.
9. **Catálogo de livros (T8 operacional):** inventário existente não prova pares corretos de NFO/capa para 1.640 livros. Planejar reconciliação/refresh com preview, backup e reversibilidade; conferir sidecars por item e não sobrescrever arte ou metadados manuais sem critério seguro. Curadoria por IA segue fora do ciclo ativo.
10. **W1.3/W1.5:** terminar carrosséis e resultados com controle remoto/modo nativo e servidor real; consolidar tokens/componentes sem reescrita geral.
11. **W2.1–W2.3:** auditoria WCAG 2.2 AA nas rotas principais, teclado/leitor de tela/controle remoto, axe/Playwright e avaliação manual. W2.4 está concluído.
12. **W3.1–W3.6:** Web Vitals por rota, budgets/lazy loading medidos, imagens, cache TanStack, benchmark de virtualização e testes em rede/CPU/dispositivos lentos/TV.
13. **W4.1–W4.5:** ampliar Playwright para fluxos críticos; baselines visuais revisados em desktop/celular/TV; interações/paginação; gate hospedado completo e política de flaky tests.
14. Em cada fatia: reler `AGENTS.md`, regras e skills aplicáveis; revisar diffs e twins; executar testes reais e quality gates adequados; registrar comandos, código de saída, contagens e limitações neste handover e no roadmap. Preservar o worktree sujo e alterações Android/anexos de outros trabalhos.
15. **Release:** gate permanece fechado. Só após todas as tarefas ativas e validações do roadmap estarem concluídas iniciar uma release final de produção; incluir notas correspondentes ao diff realmente validado, pacote servidor Windows ZIP + instalador, pacote Android somente conforme escopo autorizado, publicação separada, sincronização do portal e confirmação da API oficial de releases. Não publicar release intermediária.
