# Handover para próxima IA — servidor e frontend web

Atualizado em 03/10/2026. Objetivo: continuar o roadmap de evolução do servidor e web. A lista detalhada, evidências por mudança e decisões técnicas ficam em [`docs/roadmap-evolucao-tecnologica-servidor-e-web.md`](docs/roadmap-evolucao-tecnologica-servidor-e-web.md); este handover é o mapa de retomada.

## Resumo executivo

- O roadmap está **em andamento**; há diversas tarefas ativas. **Não criar nem publicar release** enquanto todas não estiverem implementadas e validadas.
- Branch observada: `main`; HEAD: `27972568648b` (`git rev-parse --short=12 HEAD`). Nenhuma alteração desta tarefa foi commitada, empacotada ou publicada.
- Worktree está muito sujo, com alterações concorrentes de servidor, web, Android e anexos. São trabalho do usuário/outros fluxos: preservar, não reverter, limpar, sobrescrever ou incluir indiscriminadamente.
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
- **Leitor de livros (03/10):** `BookReaderController.GetEpub`/`GetStatus` agora consultam itens no escopo do usuário autenticado; usuário desconhecido falha fechado e livro invisível não chega ao conversor. Quatro testes cobrem os dois endpoints, caso permitido e identidade ausente. A suíte API Release passou **296/296** nesta continuação; `dotnet format whitespace --verify-no-changes` e busca de `IsVisibleStandalone(null)` nos controllers passaram (0 ocorrências). Sem E2E HTTP/servidor real; T6.1 segue parcial.
- **Episódios (03/10):** `TvShowsController.GetEpisodes` escopa consultas de `seasonId`, número da temporada e série completa ao usuário autenticado. Três regressões cobrem a sobrecarga segura em cada ramo. Suíte `Jellyfin.Api.Tests` Release passou **299/299**; format e busca de consultas de `seriesId`/`seasonId.Value` sem usuário passaram. Sem E2E HTTP/fuzzing; auditoria T6.1 continua aberta.
- **Escopo de parentId na API (03/10):** `RequestHelpers.GetParentItem` aplica lookup visível ao usuário e falha fechado se um `userId` não vazio não resolver; preserva root e API keys conforme contratos existentes. Integrado às duas rotas Filters e às sete ações de listagem em `Genres`, `Artists` (2), `Items`, `MusicGenres`, `Studios` e `Years`; pai não resolvido retorna `404`, usuário desconhecido retorna `401` em Filters. Testes de helper/filtros passaram **9/9**, testes parametrizados dos sete handlers **7/7**; suíte `Jellyfin.Api.Tests` Release passou **315/315**. Format e `git diff --check` passaram; busca final encontrou zero chamadas diretas restantes ao helper não escopado por `parentId`. Sem E2E HTTP/fuzzing/servidor real; T6.1 continua parcial.
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

Os testes `Jellyfin.Server.Implementations.Tests` 777/777 e os números anteriores de `Jellyfin.Api.Tests` continuam sendo evidência histórica. Na continuação atual (03/10), `dotnet test tests/Jellyfin.Api.Tests/Jellyfin.Api.Tests.csproj -c Release --no-restore` passou **315/315** após escopo em BookReader, episódios e pais de consulta. `dotnet format whitespace --no-restore --verify-no-changes` passou nos arquivos C# alterados e `git diff --check` passou (avisos de conversão LF/CRLF do Git, sem erro). Não houve E2E HTTP nem servidor real.

## Próximas tarefas abertas — fonte de verdade: checkboxes do roadmap

### Servidor / operação / dados

- [ ] **T0.1, T0.3, T0.4:** completar baseline sob carga representativa, cenários de recuperação Windows/Linux e séries suficientes para definir budgets sem números arbitrários.
- [ ] **T1:** validar collector/retention/alertas, agregados e índices com volume real; confirmar operação remota dos jobs. Código e testes locais não bastam.
- [ ] **T3.2, T3.6:** testar playback + prefetch, interrupção/reinício, troca/cancelamento, parte ausente e disco cheio com serviço Nebula isolado operacional. Unidade determinística não comprova fluxo real.
- [ ] **T4.2:** fechar política de consistência, criptografia, retenção/rotação, integridade e remoção remota. Decisões potencialmente destrutivas exigem autorização/decisão do operador; não inventar política.
- [ ] **T4.3:** executar restore integral Mongo em instância descartável explicitamente isolada. O teste requer `MULLETAFLIX_TEST_MONGODB_CONNECTION_STRING` fixada ao opt-in local `127.0.0.1:27099`; não usar Mongo desconhecido `27017`, Supabase nem produção.
- [ ] **T4.4:** completar painel acionável (última/próxima execução, destino, conteúdo/tamanho, integridade/erros e ação protegida de restore/teste) e validar comportamento real.
- [ ] **T5.1, T5.2, T5.4, T5.6:** medir pool/consultas, otimizar só com plano/benchmark, avaliar cache apenas se justificado e testar carga/degradação.
- [ ] **T6.1–T6.5:** auditoria endpoint a endpoint de autenticação/autorização; limites seletivos; canonicalização, symlinks e TOCTOU em Windows/Linux; segredos/logs/transporte/dependências; defaults seguros. O roadmap registra compatibilidades/rotas legadas que precisam de decisão, não remoção implícita.
  - Próximo bloco de auditoria T6.1: investigar controladores que carregam entidades por IDs em rotas de streaming/sessão, diagnósticos e ações elevadas; verificar compatibilidade, principal API key versus usuário, cobertura de autorização cruzada e fuzzing antes do aceite integral.
- [ ] **T8 operacional:** reconciliação reversível item-a-item de livros, nomes, metadados, capa e sidecars NFO; gerar preview/backup, preservar metadados/capas manuais e validar refresh real. IA continua fora do ciclo.

### Frontend web

- [ ] **W1.1:** terminar loading/vazio/erro/offline/degradado/sucesso em home, busca, detalhes, solicitações e gestão; validar visualmente e no servidor/browser real.
- [ ] **W1.2:** E2E autenticado: autocomplete/indexação, solicitar, grids pendentes/incluídos, confirmação e prioridade; desenhar correlação segura da solicitação até item/posição Nebula.
- [ ] **W1.3, W1.5:** setas/carrosséis/resultados e teclado/controle remoto/foco/responsividade; consolidar tokens/componentes sem reescrita global.
- [ ] **W2.1–W2.3:** auditoria WCAG 2.2 AA, teclado/controle remoto/leitor de tela, axe + Playwright nas rotas e validação manual. W2.4 está concluído.
- [ ] **W3.1–W3.6:** Web Vitals, budgets/lazy load medidos, posters/backdrops, cache TanStack, benchmark de virtualização e redes/CPU/dispositivos lentos.
- [ ] **W4.1–W4.5:** Playwright de fluxos críticos, snapshots humanos revisados por viewport, interações/paginação, quality gate hospedado e política de flaky tests sem retries ilimitados.

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

1. Confirmar estado atual do workspace e ler documentação/instruções acima; este arquivo e os números de teste são um snapshot de 03/10/2026, não um substituto das fontes vivas.
2. W1.1 recebeu loading acessível compartilhado para seções assíncronas, mas ainda exige inspeção visual real e cobertura das rotas/estados restantes. Continuar a partir dos checkboxes e registros recentes do roadmap.
3. Não mudar serviço de produção nem dados; usar testes isolados. Se uma dependência de ambiente for necessária, registrar o bloqueio concreto e avançar em outras tarefas seguras.
4. Rerodar os gates da fatia, revisar regressões e twins, registrar resultados nos dois documentos. Reconsultar os checkboxes antes de considerar qualquer frente concluída.
