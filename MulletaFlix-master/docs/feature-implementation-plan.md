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

## 1. Badge "atualização disponível" no dashboard

**Estado:** backend pronto (`UpdateInfoController.GetUpdateInfo`); falta o frontend.

**Objetivo:** mostrar um badge/aviso no dashboard quando `UpdateAvailable=true`, sem exigir entrar na página "Centro de atualizações".

**Tarefas:**
1. Criar hook `useServerUpdateInfo` já existente (reusar `apps/dashboard/features/updates/api/useServerUpdateInfo.ts`).
2. Adicionar badge no drawer (ServerDrawerSection) ou header do dashboard.
3. Exibir "nova versão X disponível" com link para `/dashboard/updates`.

**Verificação:** `npm run build:check` exit 0.

---

## 2. Persistência de preferências do leitor

**Estado:** o `BookPlayer` tem `theme` (dark/sepia/light) e `fontSize`, mas são resetados a cada abertura.

**Objetivo:** salvar tema e fonte por usuário e restaurar ao abrir o livro.

**Tarefas:**
1. Identificar o mecanismo de preferências por usuário já existente (ex.: `userSettings` ou `DisplayPreferences`).
2. Persistir `theme`/`fontSize` ao alternar.
3. Restaurar no `play()` / construtor.

**Verificação:** `npm run build:check` exit 0.

---

## 3. Webhook de eventos

**Estado:** `IEventManager` + `EventManager` já publicam eventos (playback, licença, etc.).

**Objetivo:** notificar Discord/Telegram em eventos configuráveis (playback iniciado, item adicionado, erro de transcoding).

**Tarefas:**
1. Modelo `WebhookConfig` + endpoint de configuração (admin).
2. Consumidor de eventos `WebhookNotifier` (padrão `UserLicenseChangedLogger`).
3. HTTP POST com payload JSON, com retry básico.

**Verificação:** build 0 erros + testes unitários do serializador/config.

---

## 4. Gráficos no relatório de playback

**Estado:** `playback-reports` tem tabela + estatísticas agregadas; sem visualização.

**Objetivo:** adicionar gráficos leves (linha de horas por dia, top mídias).

**Tarefas:**
1. Escolher lib leve de chart (ou SVG manual para evitar dependência).
2. Gráfico de linha (horas/dia) e barra (top itens) na página.

**Verificação:** `npm run build:check` exit 0.

---

## 5. "Continue assistindo/lendo" na home

**Estado: JÁ IMPLEMENTADO** — verificado nesta sessão. A home do usuário (`apps/experimental`) já tem as seções `Resume` (vídeo), `ResumeAudio` e `ResumeBook` em `components/homesections/homesections.ts`, e `sections/resume.ts` já trata `Book` com shape de retrato. O backend `ItemsController.GetResumeItems` usa `IsResumable=true` + filtro `MediaTypes`. O leitor de livros já reporta progresso (`Events.trigger(this, 'pause')` no `relocated`). Nada a fazer — remover do backlog.

---

## Risco residual

- Features 3–5 dependem de contexto de biblioteca que ainda não auditei por completo; cada uma exige leitura do código antes de implementar.
- Features visuais (badge, gráficos, shelf) só são 100% validadas com servidor rodando; o gate mínimo é build/test.
