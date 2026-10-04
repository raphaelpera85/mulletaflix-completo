# Handover para próxima IA — servidor e frontend web

Atualizado em 03/10/2026. Objetivo: continuar o roadmap de evolução do servidor e web. A lista detalhada, evidências por mudança e decisões técnicas ficam em [`docs/roadmap-evolucao-tecnologica-servidor-e-web.md`](docs/roadmap-evolucao-tecnologica-servidor-e-web.md); este handover é o mapa de retomada.

## Resumo executivo

- O roadmap está **em andamento**; há diversas tarefas ativas. **Não criar nem publicar release** enquanto todas não estiverem implementadas e validadas.
- Branch observada: `main`; HEAD atual na última conferência: `870bfc7a` (`Expand roadmap handover with authorization progress and open tasks`). O commit contém código e alterações locais anteriores, não é uma release oficial. O handover e roadmap têm alterações posteriores ainda não commitadas; preservar os diffs.
- Estado local observado nesta continuação: handover, roadmap e arquivos de `tools/release` foram modificados para auditoria/segurança DuckDNS. Também apareceram mudanças locais em arquivos Android e `artifacts/aptoide-listing/` no `git status`; não foram inspecionadas nem alteradas nesta tarefa, pois o escopo é servidor/web. Preserve-as e confirme propriedade antes de qualquer ação.
- Não foi criada/empacotada/publicada release nesta continuação. Nenhum push ou deploy/atualização do portal foi executado.
- Escopo desta conversa: servidor Windows/Linux e frontend web. Não desenvolver APK aqui. Curadoria de catálogo por IA está fora do escopo ativo.
- A versão `12.1.10` respondeu no endpoint público local conforme registro anterior; isso não demonstra funcionamento administrativo nem é validação de release atual.

## O que já foi feito (conforme código/histórico do roadmap)

- **Baseline e arquitetura:** mapa de fluxos concluído (T0.2); existe amostra inicial de produção e cenários determinísticos, mas faltam medições representativas/cross-platform (T0.1/T0.3/T0.4).
- **Operação/observabilidade:** OpenTelemetry, indicadores, logs/auditoria redigidos, health checks e diagnósticos/alertas implementados (T1.1–T1.5); ainda há validações operacionais/escala real pendentes.
- **Nebula:** máquina de estados, idempotência, prioridade observável, backpressure/fairness, recuperação, limpeza/startup e refresh da listagem virtual após upload foram tratados e marcados concluídos (T2.x). Isso não equivale a teste operacional com Nebula ligado.
- **Reprodução/cache:** contrato do cache, leases, single-flight e painel de diagnóstico implementados (T3.1, T3.3–T3.5). Foram adicionados cancelamento do pré-cache por geração e tratamento de resposta curta; ciclo completo com serviço Nebula operacional continua pendente (T3.2/T3.6).
- **Backup:** inventário e exercícios de recuperação documentados (T4.1/T4.5). Há testes isolados de partes do backup/restore, incluindo HTTP/InMemory e fixtures Mongo opt-in; restore Mongo real não foi executado por falta de instância descartável configurada (T4.3). Consistência/política e painel ainda não fecham o aceite (T4.2/T4.4).
- **Banco/cache:** consistência entre Mongo/MariaDB e avaliação de Change Streams documentadas (T5.3/T5.5); baseline, otimização baseada em planos, HybridCache condicionado a evidência e carga continuam pendentes.
- **Segurança/API:** correções e cobertura parcial incluem rate limiting, `Retry-After`, admissão de operações pesadas, telemetria, uploads e validação/canonicalização de alguns caminhos, symlink/junction, HLS e autorização. Ainda falta auditoria completa e cross-platform (T6.1–T6.5).
- **Transcodificação por dispositivo (03/10):** lookup/ping/kill autenticados passam a exigir correspondência `deviceId + playSessionId`; request usa claim autenticado e stop HLS recusa query divergente. Testes focalizados **7/7** (manager), **5/5** (HLS), API completa **333/333**; sem E2E HTTP ou servidor real, logo T6.1 parcial.
- **Caminho de cache (03/10):** API agora compara caminho salvo com solicitado e devolve erro em divergência; interface relê status após salvar, só confirma sucesso quando coincide e restaura o último valor confirmado em falha. Inclui regressão da resposta positiva sem persistência. Testes API **333/333**, configuração **32/32**, web **312/312**, build/check/verify e solução Release passaram. A tela da captura não foi reproduzida no servidor instalado; precisa validar após uma build futura.
- **Leitor de livros (03/10):** `BookReaderController.GetEpub`/`GetStatus` agora consultam itens no escopo do usuário autenticado; usuário desconhecido falha fechado e livro invisível não chega ao conversor. Quatro testes cobrem os dois endpoints, caso permitido e identidade ausente. A suíte API Release passou **296/296** nesta continuação; `dotnet format whitespace --verify-no-changes` e busca de `IsVisibleStandalone(null)` nos controllers passaram (0 ocorrências). Sem E2E HTTP/servidor real; T6.1 segue parcial.
- **Episódios (03/10):** `TvShowsController.GetEpisodes` escopa consultas de `seasonId`, número da temporada e série completa ao usuário autenticado. Três regressões cobrem a sobrecarga segura em cada ramo. Suíte `Jellyfin.Api.Tests` Release passou **299/299**; format e busca de consultas de `seriesId`/`seasonId.Value` sem usuário passaram. Sem E2E HTTP/fuzzing; auditoria T6.1 continua aberta.
- **Escopo de parentId na API (03/10):** `RequestHelpers.GetParentItem` aplica lookup visível ao usuário e falha fechado se um `userId` não vazio não resolver; preserva root e API keys conforme contratos existentes. Integrado às duas rotas Filters e às sete ações de listagem em `Genres`, `Artists` (2), `Items`, `MusicGenres`, `Studios` e `Years`; pai não resolvido retorna `404`, usuário desconhecido retorna `401` em Filters. Testes de helper/filtros passaram **9/9**, testes parametrizados dos sete handlers **7/7**; suíte `Jellyfin.Api.Tests` Release passou **315/315**. Format e `git diff --check` passaram; busca final encontrou zero chamadas diretas restantes ao helper não escopado por `parentId`. Sem E2E HTTP/fuzzing/servidor real; T6.1 continua parcial.
- **Sessões, capabilities e reports (03/10):** além dos sete comandos de controle remoto e duas operações de compartilhamento protegidos no commit `29d16914`, as rotas `Sessions/Capabilities`, `Sessions/Capabilities/Full` e `Sessions/Viewing` agora verificam a sessão com `ISessionManager.GetSessions`. `Viewing` valida `itemId` e escopa consulta pela biblioteca do usuário; API key mantém acesso administrativo existente; identidade comum não resolvida falha fechado. Teste focal cobre cenários permitidos/negados, API key, usuário desconhecido, sessão alheia, item invisível e GUID inválido. T6.1 continua parcial: falta E2E HTTP/fuzzing integrado, auditoria das demais ações elevadas e análise mais ampla das rotas legadas.
- **Programas de TV ao vivo (03/10):** POST agora aplica `RequestHelpers.GetUserId` ao `UserId` fornecido; GET/POST escopam `LibrarySeriesId` ao usuário e retornam 404 para série invisível em vez de remover o filtro. Usuário do principal que não resolve falha fechado com 401; o ramo `Guid.Empty` sem usuário foi preservado, mas API key não foi testada especificamente nesta fatia. Cinco regressões cobrem userId cruzado, identidade ausente em ambas as rotas, visibilidade em ambas e query da série visível. `LiveTvProgramsAuthorizationTests` passou **5/5**; suíte API completa passou **331/331**.
- **Transcodificação por dispositivo (03/10, T6.1 parcial):** ping, validação de método de playback, lookup do job e kill usam `deviceId + playSessionId`; o ID de dispositivo informado por request de streaming é substituído pelo claim autenticado e a rota HLS stop rejeita claim/query divergente. Testes cobrem colisão de sessão em dois dispositivos e request HLS forjado. `TranscodeManagerTests` **7/7**, HLS controller **5/5**, API completa depois **333/333**; build da solução Release **0 erros/370 avisos**. Sem E2E HTTP ou dois clientes reais.
- **Salvar caminho do cache (03/10, verificação de confirmação):** a tela agora faz POST, relê o endpoint de status e só notifica sucesso se o caminho confirmado corresponde ao solicitado; divergência apresenta erro em vez de toast falso. O controller compara o status após salvar e responde HTTP 500 se o valor não foi confirmado. Os testes existentes cobrem persistência atômica e rollback; `NamedConfigurationConcurrencyTests` **32/32**, teste de divergência está em `NebulaFtpControllerTests` dentro da suíte API **333/333**. Isso não reproduz a falha na instalação real; validação visual/HTTP após atualização futura continua necessária.
- **Skills desta fatia:** skills base do projeto (`gauntlet-loop`, `fable-method`, `fable-loop`, `fable-judge`, `backend-architect`, `frontend-design`, `caveman`, `cavecrew`, `data-engineer`) e especialistas `api-security-testing`/`csharp-testing` foram lidas e aplicadas. A revisão por subagente prevista pelo Fable Loop não pôde iniciar por limite de threads; foi feita revisão local adversarial de diff, contratos, endpoints semelhantes e testes. A próxima IA deve repetir revisão independente quando houver capacidade.
- **FFmpeg:** diagnóstico/capacidades/fallback/compatibilidade estão marcados concluídos (T7.x).
- **Catálogo/livros:** seleção ambígua no Open Library foi corrigida e há suporte/testes para persistência local de NFO/capas (T8). A reconciliação dos livros em disco e validação item-a-item de metadados/capas ainda não foram feitas; não alegar que os 1.640 itens inventariados estejam corrigidos.
- **Solicitações/web:** autocomplete com debounce e proteção contra respostas obsoletas, botão de solicitação junto a Favoritos, grids/estados administrativos e prioridade avançaram (W1.2 parcial); jornada real com servidor/sessão e correlação com a fila Nebula continuam pendentes.
- **Estados web:** falhas de carregamento em grades/detalhes não devem ser apresentadas como vazio; foram feitos tratamentos parciais e estados de feedback. `ConnectionErrorPage` tem retry manual e retry único ao evento `online` em `Unavailable`, com limpeza do listener e testes (W1.1 parcial).
- **Seções assíncronas (03/10):** `loadSectionItems` agora exibe/ anuncia `Carregando` enquanto a consulta aguarda, remove o status ao concluir e preserva estados existentes de vazio, erro/retry e `aria-busy`. Usado por seções de detalhes e grades por nome (W1.1 parcial).
- **Acessibilidade:** formulários de feedback com labels, validação inline e anúncios acessíveis estão concluídos (W2.4). Auditoria WCAG geral, operação completa por teclado/controle remoto e axe nas rotas permanecem parciais.
- Existem entregas anteriores de carrossel, controle remoto/teclado, axe, Playwright, Stylelint, quality gates e Nebula, registradas cronologicamente no roadmap. Não presumir que “implementado” signifique E2E, CI remoto ou produção validados.

## Evidências de teste registradas e desta continuação

Segundo o handover/registro anterior, os testes Release do servidor passaram: `Jellyfin.Api.Tests` **292/292** e `Jellyfin.Server.Implementations.Tests` **777/777**, além de format e `git diff --check`. Testes Mongo opt-in/isolados não executados devem continuar identificados como ignorados, não aprovados.

Nesta continuação, o gauntlet web passou novamente: Vitest **312/312 em 48 arquivos**, `npm run build:check`, ESLint focado em `asyncItemsSection.ts`/teste, `npm run build:production`, `npm run verify:build` (**1.903 arquivos; limite 1.536 KiB**) e `git diff --check`. O teste de loading foi observado falhando antes e passando após a implementação. Build mantém avisos Vite de `use client`/chunks e testes avisam sobre `getComputedStyle` do jsdom.

Os testes `Jellyfin.Server.Implementations.Tests` 777/777 e números anteriores de `Jellyfin.Api.Tests` continuam sendo evidência histórica. Nesta continuação: `TranscodeManagerTests` **7/7**, HLS controller **5/5**, `NamedConfigurationConcurrencyTests` **32/32**, `Jellyfin.Api.Tests` completa **333/333**, Vitest **312/312 em 48 arquivos**, `npm run build:check`, `npm run build:production`, `npm run verify:build` (**1.903 arquivos; máximo 1.536 KiB**), format check e `git diff --check` passaram (exit 0). `dotnet build MulletaFlix.sln -c Release --no-restore --verbosity quiet` passou com 0 erros e 370 warnings no conjunto da solução; avisos não foram auditados individualmente. Vite mantém avisos de `use client`/chunks e jsdom de `getComputedStyle`. Não houve E2E HTTP, fuzzing integrado nem servidor real.

## Próximas tarefas abertas — inventário completo do roadmap

O roadmap vinculado acima é a fonte de verdade e contém critérios/evidências detalhados. A lista abaixo expande todos os itens ainda abertos no snapshot de 03/10/2026; “feito” em código/testes locais não substitui validação operacional ou remota.

### Servidor e dados

- [ ] **T0.1 — Baseline:** completar medições representativas de boot, memória/disco, latências p50/p95, busca, tráfego comprimido, cache, volume e duração Nebula; incluir Linux e amostra temporal suficiente.
- [ ] **T0.3 — Cenários:** completar cenários de bibliotecas pequenas/grandes, fonte local/Telegram, falhas de banco/rede, cache cheio e Windows/Linux; testar recuperação integrada sem produção.
- [ ] **T0.4 — Budgets:** definir limites de regressão só depois das séries representativas de boot, busca, recursos, tráfego e cache.
- [ ] **T1 operacional — Observabilidade:** embora T1.1–T1.5 estejam marcados feitos, validar agregações/índices com volume real, histórico completo, collector remoto autenticado, retenção/alertas e execução dos dois jobs remotos.
- [ ] **T3.2 — Playback/prefetch real:** exercitar início da mídia enquanto o prefetch baixa partes, limites/cancelamento ao trocar/parar. A configuração do caminho agora só dá sucesso após confirmação no status, mas a tela ainda requer verificação na instalação real. Playback/preload ponta a ponta está bloqueado por decisão operacional anterior: Nebula está configurado fechado (`Enabled=false`); não o iniciar nem alterar sem autorização/contexto explícitos.
- [ ] **T3.6 — Falhas/cache:** testar rede lenta/interrompida, parte ausente, restart, cliente cancelado, mudança de caminho e disco cheio. Usar fixture/serviço isolado; combinar com T3.2 quando houver ambiente autorizado.
- [ ] **T4.2 — Política de backup:** definir criptografia/chave, retenção/rotação, verificação de integridade e se remoção local também apaga Supabase. Snapshot coordenado MongoDB+MariaDB requer janela de manutenção aprovada pelo operador.
- [ ] **T4.3 — Restore isolado:** criar/rodar restauração integral e validar contagens/consultas e RTO/RPO, incluindo arquivos remotos. Mongo de teste só com instância descartável explicitamente confirmada; fixture esperada usa `MULLETAFLIX_TEST_MONGODB_CONNECTION_STRING` em `127.0.0.1:27099`; nunca presumir `27017`, Supabase ou produção.
- [ ] **T4.4 — Painel de backup:** expor última/próxima execução, destino, conteúdo/tamanho, integridade/erros e ação protegida de teste/restore; mostrar resultado do exercício completo Mongo/arquivos.
- [ ] **T5.1 — Medição de banco:** consultas lentas, pool/espera, locks, índices e custo de varredura.
- [ ] **T5.2 — Otimização:** paginação/projeções/índices/limites apenas com planos e benchmarks observados.
- [ ] **T5.4 — HybridCache:** decidir por evidência; se útil, chavear por identidade/idioma/permissão, TTL e invalidação; nunca tornar resposta personalizada pública.
- [ ] **T5.6 — Carga/degradação:** pool esgotado, reinício de conexão, volume alto, índice ausente e migração/upgrade.
- [ ] **T6.1 — Autorização:** terminar auditoria ação por ação (solicitações, reports, uploads, Nebula, cache, backup, diagnósticos, rotas legadas), adicionar autorização cruzada/fuzzing e E2E HTTP isolado. Coberturas recentes em livro, episódios, filtros/`parentId`, sessões/reports, Live TV e escopo de transcodificação são parciais; playback/streaming e Live TV ainda precisam E2E. Reavaliar `ReportNowViewingItem` se surgir outro chamador fora do controller que aplica precondições.
- [ ] **T6.2 — Limites:** terminar rate limits/concorrência seletiva; auditar trabalhos assíncronos fora de HTTP, medir por usuário/worker, calibrar sob carga e validar telemetria no collector remoto.
- [ ] **T6.3 — Entradas/caminhos:** completar upload/extensões/tipos (lista permitida requer definição do operador), tamanho, traversal, canonicalização, symlink/TOCTOU e acesso por usuário em Windows/Linux.
- [ ] **T6.4 — Segredos/transporte:** fechar criptografia do arquivo de configuração e custódia da chave/rotação com decisão do operador; rever permissões, redação de logs, transporte e dependências vulneráveis.
- [ ] **T6.5 — Defaults seguros:** validar TLS/CORS/headers, exposição de endpoints internos e contas/admin; provar collector autenticado, retenção e rotação sem credenciais de produção no ambiente atual.
- [ ] **T8 operacional — Biblioteca de livros:** reconciliar item a item os livros no armazenamento com título/idioma, metadados, capa e NFO; gerar preview/backup reversível, preservar campos/capas manuais e validar refresh. IA continua fora do escopo ativo.

### Frontend web

- [ ] **W1.1 — Estados de página:** fechar loading/vazio/erro+retry/offline/degradado/sucesso em home, busca, detalhes, solicitações e gestão; seções de detalhes e grades por nome e `ConnectionErrorPage` já têm melhorias parciais; falta validação visual integrada/browser/servidor real e demais rotas.
- [ ] **W1.2 — Solicitação de mídia:** E2E autenticado de indexação/autocomplete, solicitar, fila pendente versus incluídos, indicação de inclusão, confirmação/prioridade; demonstrar correlação segura da solicitação à posição/item Nebula.
- [ ] **W1.3 — Carrosséis/resultados:** setas, disabled, teclado/controle remoto, foco, scroll e responsividade em resultados e títulos/atores.
- [ ] **W1.5 — UI:** harmonizar tokens e controles React/MUI/Jellyfin gradualmente (cabeçalho, botões, grids, diálogos, alertas/estados), sem reescrita global.
- [ ] **W2.1 — WCAG 2.2 AA:** contraste, foco, semântica, zoom/reflow, orientação e alvos de toque nas rotas principais; W2.4 formulários já concluído.
- [ ] **W2.2 — Navegação acessível:** auditoria manual teclado/controle remoto/leitor de tela, ordem/retorno de foco, Escape e ausência de armadilhas.
- [ ] **W2.3 — axe/Playwright:** automatizar páginas principais e completar revisão manual; auditoria atual cobre apenas rotas/cenários parciais.
- [ ] **W3.1 — Web Vitals:** medir LCP/INP/CLS/TTFB por rota em laboratório; dados reais somente opt-in/anônimos se aprovados.
- [ ] **W3.2 — Budgets:** analisar chunks/dependências e carregar rotas admin, PDF/EPUB e ferramentas sob demanda; fechar limites com medições.
- [ ] **W3.3 — Imagens:** dimensões, lazy loading, prioridade da imagem principal, placeholders e espaço reservado em posters/backdrops.
- [ ] **W3.4 — TanStack Query:** rever stale time/chaves/invalidação/cancelamento/retry e evitar refetches e respostas fora de ordem.
- [ ] **W3.5 — Virtualização:** benchmark antes de adotar; preservar teclado, acessibilidade e rolagem.
- [ ] **W3.6 — Condições lentas:** throttling de rede/CPU, telas pequenas/TV e controle remoto.
- [ ] **W4.1 — Playwright:** completar busca, detalhes, solicitação, reprodução, perfil e painel.
- [ ] **W4.2 — Visuais:** snapshots de celular/desktop/TV, navegador fixado e baseline revisado por pessoa.
- [ ] **W4.3 — Interações:** setas, scroll, foco, loading e paginação/carregamento incremental.
- [ ] **W4.4 — CI frontend:** manter build/check/test/lint/style/build de produção/verificação/Playwright no quality gate e confirmar execução hospedada verde com artefatos de falha.
- [ ] **W4.5 — Flaky tests:** traces/screenshots na primeira falha, workflow deve reprovar sem retries ilimitados; investigar avisos React `act(...)`/jsdom e confirmar comportamento no job Ubuntu remoto.

### Domínio público e HTTPS (nova frente T9; ainda não implementada)

- [ ] Continuar T9.1 auditando `configure-https.ps1`, `duckdns-update.ps1`, NSIS e as demais partes de `linux-install.sh` para ACL/chmod, escrita atômica, quoting/injeção, firewall, Nginx, rollback e idempotência. A existência dos scripts não significa que o recurso esteja pronto.
- [x] Parcial Linux: token não é aceito em argumento literal nem variável de ambiente; installer pede entrada oculta ou arquivo restrito. Updater não executa configuração, não põe token em argv/log e usa temporário privado para a URL exigida por DuckDNS. Teste mock **passou**; ver evidência no roadmap. Fluxo Windows segue com token em argv.
- [ ] Criar API administrativa protegida para estado/progresso e provisionamento/revalidação; segredo nunca pode voltar no DTO, URL ou log. Armazenar com ACL Windows ou modo 0600 Linux e escrita atômica.
- [ ] Implementar provisionamento/renovação suportados em Windows e Linux, validação DNS/portas, teste de configuração antes de ativar, rollback seguro e reexecução sem duplicar tarefas.
- [ ] Criar UI sob Avançado/Redes para DuckDNS, subdomínio, token mascarado, e-mail, status DNS/portas/certificado/proxy e revalidar; exigir confirmação antes de redirecionar HTTP→HTTPS. Documentar que operador precisa encaminhar TCP 80/443 no roteador.
- [ ] Testar com APIs/DNS/ACME simulados e VMs descartáveis Windows/Linux; não tocar DNS, firewall, roteador, certificados ou proxy da instalação real sem autorização explícita.

### Fechamento e release — só depois de zerar o roadmap ativo

- [ ] Revisar diff e executar Quality Bar em produção para mudanças de servidor/web.
- [ ] Escrever notas com somente mudanças implementadas e validadas.
- [ ] Somente então gerar artefatos de produção Windows (ZIP + EXE) e Linux aplicáveis; nenhuma release intermediária neste ciclo.
- [ ] Atualizar/publicar `portal-site` junto e conferir rotas públicas/versões/notas.
- [ ] Consultar API oficial GitHub Releases, validar versão estável e assets; o portal deve apontar para a release estável mais recente.

## Bloqueios e cuidados para quem assumir

1. A execução real do Nebula/playback depende de uma instância isolada autorizada; a instância/serviço do usuário não deve ser iniciado, parado ou alterado sem contexto/autorização explícitos. A navegação do painel não prova que upload está funcionando.
2. Não há Mongo descartável confirmado nesta sessão; não conectar a endereços presumidos. Credenciais Supabase/Telegram e produção não são fixtures de teste.
3. Algumas definições de retenção/remoção remota e proteção de segredos precisam de decisão do operador. Não apagar histórico/backups nem fazer migração destrutiva por suposição.
4. A correlação entre solicitação de usuário e item da fila Nebula ainda não está demonstrada; ActivityLog em texto livre não deve ser tratado como vínculo confiável.
5. Worktree tem muitas mudanças locais simultâneas. Começar com `git status --short`, identificar propriedade/diff, preservar anexos e arquivos Android; não rodar `reset`, `checkout`, limpeza ampla, nem sobrescrever mudanças.

## Regras obrigatórias para continuar

- Ler `AGENTS.md`, `.agents/rules/mulletaflix-conventions.md`, `.agents/rules/gauntlet-loop.md`, roadmap e skills aplicáveis integralmente antes de codificar. Skills base exigidas pelo projeto: `gauntlet-loop`, `fable-method`, `fable-loop`, `fable-judge`, `backend-architect`, `frontend-design`, `caveman`, `cavecrew`, `data-engineer`; selecionar e ler as especialistas da tarefa no roadmap.
- Trabalhar em fatias pequenas com Builder vs. Evaluator, critérios de aceite antes da implementação, teste vermelho quando adequado, revisão adversarial, busca de twins e evidência real de terminal com exit code 0. Atualizar roadmap e este handover com comandos, contagens, limitações e evidências.
- Não desenvolver APK nesta conversa. Não iniciar curadoria por IA.
- Não fazer commit/push, pacote ou release intermediária. Instruções do projeto exigem aguardar o fechamento de **todas** as tarefas ativas do roadmap. Só então iniciar release final de produção e sincronizar/publicar portal; notas devem refletir o diff validado, assets precisam ser conferidos pela API oficial.

## Retomada recomendada

1. Confirmar `git status --short`, branch e HEAD antes de agir; preservar os diffs de sessão/Live TV, testes, roadmap e handover, o teste novo e `artifacts/aptoide-listing/`. Não presumir que artefatos não rastreados pertençam à tarefa atual.
2. As fatias atuais passaram: transcode **7/7**, HLS controller **5/5**, config Nebula **32/32**, API Release **333/333**, web Vitest **312/312**, TypeScript/build produção/artefatos, format e diff check; solução Release compilou com 0 erros/370 warnings. Reexecutar se código for alterado.
3. Prosseguir T6.1 com E2E HTTP isolado para capabilities/full/viewing e programas Live TV; auditar endpoint a endpoint os outros métodos/ações elevadas e rotas legadas. Verificar chamadas de domínio fora do caminho HTTP; proteger `ReportNowViewingItem` se surgirem outros chamadores. Adicionar fuzzing/casos de identidade, sessão/item/série invisível, API key e sessão alheia.
4. Corrigir o caminho Windows NSIS/PowerShell que ainda passa token como argumento; auditar demais scripts T9 antes de reuso. Depois implementar em fatias pequenas API, persistência segura, provisionamento Windows/Linux e UI, cada uma com testes e revisão adversarial. Skills roteadas no roadmap: `api-security-testing`, `backend-architect`, `frontend-design`, `csharp-testing`; para scripts: `powershell-windows` e `linux-shell-scripting`; revalidar documentação oficial de Nginx/ACME. Decisões de produção/credenciais são do operador.
5. Continuar tarefas restantes T3/T4 e jornadas W1.1/W1.2 e demais itens; antes de cada fatia, ler as skills especialistas relevantes indicadas no roadmap.
6. Aplicar Builder vs. Evaluator, revisão adversarial independente quando disponível e busca de twins; atualizar roadmap/handover com comandos, exit codes, contagens e limites reais. Reconsultar todos os checkboxes antes de qualquer discussão de release.
