# MulletaFlix — Plano de Novas Funcionalidades

> Ordenado por esforço × valor. Cada feature termina com verificação real (build/test), nunca só com resumo.
> Estado: gerado após concluir as Roadmaps #1–#11 + leitor de livros.

---

## Ordem de execução

1. **Badge "atualização disponível" no dashboard** — aproveita o endpoint `GET /System/UpdateInfo` (Roadmap #11).
2. **Persistência de preferências do leitor** (tema + tamanho de fonte por usuário) — o leitor já tem os estados, mas não persistem.
3. **Webhook de eventos** (Discord/Telegram) — encaixa no `IEventManager` já usado.
4. **Gráficos no relatório de playback** — hoje é só tabela.
5. **"Continue assistindo/lendo" na home** — shelf com dados de resume/PlaybackReport.

---

## 1. Badge "atualização disponível" no dashboard — ✅ IMPLEMENTADO

**Estado (reconciliado em 05/10):** já implementado e commitado. `UpdateAvailableIndicator.tsx` (ícone com `Badge` "dot" + tooltip + link) está conectado via `UpdateAvailableIndicatorContainer.tsx` (reusa `useServerUpdateInfo`, sem polling duplicado) tanto na toolbar do header (`AppLayout.tsx`) quanto no item "Centro de atualizações" do `ServerDrawerSection.tsx`.

**Verificação (evidência real, 05/10):** `npx tsc --noEmit -p tsconfig.json` → exit 0; `npx vitest run UpdateAvailableIndicator.test.tsx` → 4/4 passando.

**Limitação:** sem validação visual manual com servidor real rodando nesta sessão.

---

## 2. Persistência de preferências do leitor — ✅ IMPLEMENTADO

**Estado:** `BookPlayer.theme`/`fontSize` já eram persistidos via `userSettings.bookPlayerTheme()`/`bookPlayerFontSize()` (localStorage com fallback para `DisplayPreferences` do servidor), restaurados no construtor do plugin. Esta rodada endureceu a implementação existente: extraiu a lógica de resolução para funções puras testáveis (`resolveBookPlayerTheme`/`resolveBookPlayerFontSize` em `src/plugins/bookPlayer/plugin.ts`) e blindou `appSettings.get`/`set` (`src/scripts/settings/appSettings.ts`) com try/catch para que um `localStorage` corrompido/indisponível nunca quebre o leitor (fallback seguro para o valor padrão).

**Mecanismo de persistência:** `localStorage` via `appSettings`/`userSettings` (padrão já estabelecido no projeto para preferências de usuário; mesma store usada por `enableCinemaMode`, `customCss`, etc.), com sincronização best-effort para `DisplayPreferences` do servidor (`CustomPrefs`) quando a sessão do usuário está carregada.

**Arquivos alterados:**
- `MulletaFlix-web-master/src/scripts/settings/appSettings.ts` — `get`/`set` agora toleram exceções de `localStorage` (storage corrompido/indisponível/quota), retornando `null`/no-op em vez de propagar o erro.
- `MulletaFlix-web-master/src/plugins/bookPlayer/plugin.ts` — lógica de restauração de tema/fonte extraída para `resolveBookPlayerTheme`/`resolveBookPlayerFontSize`, com fallback seguro para valores ausentes ou inválidos/corrompidos.
- `MulletaFlix-web-master/src/plugins/bookPlayer/plugin.test.ts` (novo) — 12 testes Vitest cobrindo: preferência salva e recarregada corretamente, fallback para padrão quando nada foi salvo, e fallback seguro quando o valor salvo (ou o próprio `localStorage`) está inválido/corrompido.

**Verificação (evidência real, executada nesta rodada):**
- `npx tsc --noEmit -p tsconfig.json` → exit 0.
- `npx eslint src/plugins/bookPlayer/plugin.ts src/plugins/bookPlayer/plugin.test.ts src/scripts/settings/appSettings.ts` → exit 0 (1 warning pré-existente, não relacionado, em linha fora do diff).
- `npx vitest run --config vite.config.ts src/plugins/bookPlayer src/scripts/settings` → 1 test file, 12 tests, todos passando.

**Limitação:** sem servidor rodando nesta sessão, não houve validação visual manual no leitor real; a cobertura é via testes automatizados (unitário/integração com `localStorage` real do jsdom) exercitando exatamente o contrato usado pelo construtor do `BookPlayer`.

---

## 3. Webhook de eventos — PARCIAL (pedido original atendido; versão rica incompleta)

**Estado (reconciliado em 05/10):** o pedido original ("notificar Discord/Telegram") **já está implementado**: `PlaybackWebhookNotifier.cs` (`Jellyfin.Server.Implementations/Events/Consumers/Session/`) consome `PlaybackStartEventArgs`/`PlaybackStopEventArgs` via `IEventConsumer` e faz POST JSON para URLs configuradas por `MulletaFlix_WEBHOOK_URL`/`MulletaFlix_WEBHOOK_EVENTS` (env vars), com teste em `PlaybackWebhookNotifierTests.cs`.

**Gap real restante:** existe uma versão mais rica, só como scaffolding nunca finalizado — `WebhookConfiguration`/`WebhookEndpointConfiguration`/`WebhookEventTypes` (`MediaBrowser.Model/Configuration/WebhookConfiguration.cs`, com suporte a múltiplos endpoints nomeados + `ItemAdded` além de playback), `WebhookConfigurationStore`/`WebhookConfigurationFactory` (`Jellyfin.Server/Configuration/Webhook/`) e a interface `IWebhookDispatcher` (`MediaBrowser.Controller/Webhooks/`) — **sem nenhuma implementação concreta do dispatcher, sem controller de API admin e sem UI no frontend**. `ItemAdded` nunca é disparado por ninguém.

**Próximo passo se quiser a versão rica:** implementar `IWebhookDispatcher`, um controller `WebhooksController` (CRUD de `WebhookEndpointConfiguration`) e a tela admin equivalente; migrar `PlaybackWebhookNotifier` para usar a config em vez de env vars.

**Verificação:** nenhuma mudança de código nesta reconciliação, apenas leitura/confirmação do estado real via busca no repositório.

---

## 4. Gráficos no relatório de playback — ✅ IMPLEMENTADO

**Estado:** `playback-reports` já tinha tabela + estatísticas agregadas (contadores e tabelas de Top Users/Top Items), mas nenhuma visualização gráfica. A página já importava `BarChart`/`LineChart` de um componente `PlaybackCharts` e os renderizava dentro do painel "📊 Stats" (toggle já existente ao lado dos filtros), mas esse componente não tinha nenhum teste cobrindo render com dados mock nem o caso de dados vazios.

**Decisão de biblioteca:** nenhuma lib de gráficos (recharts, chart.js, @mui/x-charts, victory, nivo) está no `package.json` do projeto. Em vez de adicionar uma dependência nova, mantive a implementação já existente: **SVG manual** (`BarChart`/`LineChart` em `src/apps/dashboard/features/playback/components/PlaybackCharts.tsx`), que usa `useTheme()` do MUI para cores e não requer bundle adicional — consistente com a tarefa 1 do plano original ("Escolher lib leve de chart (ou SVG manual para evitar dependência)").

**Gráficos exibidos (dentro do toggle "📊 Stats" da página, acima da tabela de Top Items/Top Users, mantendo a tabela principal intacta):**
- `LineChart` com `stats.PlaysByDate` — série temporal de volume de reproduções por dia.
- `BarChart` com `stats.PlaysByItemType` — distribuição de reproduções por tipo de mídia (Movie/Episode/Audio/etc.).
- Ambos retornam `null` (sem renderizar `<svg>`) quando o dicionário de dados está vazio, em vez de quebrar.

**Arquivos criados nesta rodada:**
- `MulletaFlix-web-master/src/apps/dashboard/features/playback/components/PlaybackCharts.test.tsx` (novo) — 5 testes Vitest: `BarChart` renderiza uma barra por entrada com dados mock de distribuição por tipo de mídia; `BarChart` não quebra e não renderiza nada com dados vazios; `LineChart` renderiza a polyline/pontos da série temporal com dados mock; `LineChart` ordena as entradas cronologicamente independente da ordem de entrada; `LineChart` não quebra e não renderiza nada com dados vazios.

**Arquivos não modificados (já implementados em rodada anterior, fora do escopo de alteração desta sessão):** `src/apps/dashboard/routes/playback-reports/index.tsx` e `src/apps/dashboard/features/playback/components/PlaybackCharts.tsx` — apenas lidos/verificados, sem necessidade de alteração.

**Verificação (evidência real, executada nesta rodada):**
- `npx tsc --noEmit -p tsconfig.json` → exit 0.
- `npx eslint src/apps/dashboard/features/playback/components/PlaybackCharts.tsx src/apps/dashboard/features/playback/components/PlaybackCharts.test.tsx src/apps/dashboard/routes/playback-reports/index.tsx` → exit 0 (0 erros, 0 warnings).
- `npx vitest run --config vite.config.ts src/apps/dashboard/features/playback/components/PlaybackCharts.test.tsx` → 1 test file, 5 tests, todos passando, exit 0.

**Limitação:** sem servidor rodando nesta sessão, não houve validação visual manual no dashboard real; a cobertura é via teste automatizado que renderiza os componentes de gráfico com `renderToStaticMarkup` (padrão já usado em outros testes de componente do projeto, ex. `BackupCoverageSummary.test.tsx`) e inspeciona o SVG/markup gerado.

---

## 5. "Continue assistindo/lendo" na home

**Estado: JÁ IMPLEMENTADO** — verificado nesta sessão. A home do usuário (`apps/experimental`) já tem as seções `Resume` (vídeo), `ResumeAudio` e `ResumeBook` em `components/homesections/homesections.ts`, e `sections/resume.ts` já trata `Book` com shape de retrato. O backend `ItemsController.GetResumeItems` usa `IsResumable=true` + filtro `MediaTypes`. O leitor de livros já reporta progresso (`Events.trigger(this, 'pause')` no `relocated`). Nada a fazer — remover do backlog.

---

## Risco residual

- Features 3–5 dependem de contexto de biblioteca que ainda não auditei por completo; cada uma exige leitura do código antes de implementar.
- Features visuais (badge, gráficos, shelf) só são 100% validadas com servidor rodando; o gate mínimo é build/test.
