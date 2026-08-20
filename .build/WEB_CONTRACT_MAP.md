# MulletaFlix Web — Frontend → Backend Contract Mapping (2026-08-19)

Fonte: skill `frontend-backend-contract-mapping`, escaneamento de `MulletaFlix-web-master/`.

## 1. Clientes de API (dual-SDK — risco conhecido)

| Cliente | Arquivos | Uso |
|---------|----------|-----|
| `@jellyfin/sdk` (moderno, tipado) | **302 arquivos** | hooks React Query (dashboard, stable) |
| `jellyfin-apiclient` (legacy) | **139 arquivos** | login, sessão, playback, controllers antigos |

**Risco**: divergência de contratos entre os dois clientes; correção requer migração total para o SDK único.

## 2. Auth / sessão (estado-máquina)

- **Entry points**: `ConnectionRequired.tsx`, `ConnectionErrorPage.tsx`, `ServerContentPage.tsx`, rota `quickConnect/`
- **State source**: `Dashboard.onServerChanged(id, accessToken, apiClient, url)` (controller session/login)
- **Persistência**: `localStorage` — `autocastPlayerId` (única key direta encontrada); token/servidor persistidos via `jellyfin-apiclient` internamente (storage do SDK legacy)
- **Login**: `authenticateUserByName` (legacy) — endpoint `POST /Users/AuthenticateByName`
- **Quick Connect**: `getQuickConnect()` + `authenticateQuickConnect()` (legacy) — endpoints `/QuickConnect/Initiate`, `/QuickConnect/Connect`
- **Público**: `getPublicUsers()` — `GET /Users/Public`
- **System**: `getSystemInfo()` — `GET /System/Info` (4 usos; `/System/Info/Public` não rastreado — verificar)

## 3. Hooks API (React Query)

- `src/hooks/api/`: `libraryHooks/`, `liveTvHooks/`, `useDisplayPreferences.ts`, `useUser.ts`, `useUserViews.ts`, `videosHooks/`
- Outros: `useQuickConnect.ts`, `useSystemInfo.ts`, `useUsers.ts`, `useFetchItems.ts`, `useItem.ts`, `useConfiguration.ts`, `useNamedConfiguration.ts`, `useBrandingTheme.ts`, `useThemes.ts`, `useWebConfig.tsx`

## 4. Realtime

- **Nenhum WebSocket direto no `src/`** (grep vazio) — sessão de playback usa polling/eventos do legacy apiclient? **Verificar** — possível gap.

## 5. Config runtime

- `src/config.json` — contrato runtime: `includeCorsCredentials: false`, `multiserver: false`, temas (Apple TV, Blue Radiance, Dark...). **Não é build artifact** — mudanças aqui alteram comportamento sem rebuild.

## 6. Gaps / Assumptions

1. **Dual SDK** (302 vs 139) — maior risco de drift; migração é tarefa alta
2. **`/System/Info/Public` vs `/System/Info`** — não confirmado se a UI distingue reachability de auth (pitfall da skill)
3. **WebSocket ausente no src/** — confirmar se playback/live TV usa SSE/polling ou se há camada não rastreada
4. **Storage do token** — delegado ao apiclient legacy; migração para SDK moderno precisa de camada de persistência própria
5. **`multiserver: false`** — UI assume servidor único; se o server mudar para multi-server, todo o fluxo de conexão precisa ser revisto

## 7. Server dependencies (o que o backend precisa prover)

- `POST /Users/AuthenticateByName`, `GET /Users/Public`, `GET /System/Info`
- `/QuickConnect/Initiate` + `/QuickConnect/Connect`
- Todos os endpoints dos hooks: library, liveTv, displayPreferences, userViews, videos — **todos existem no backend** (suíte de integração verde confirma)

## 8. Hardening realizado nesta rodada

- `useQuickConnect.ts` — query passou a esperar `api` existir antes de disparar
- `useUsers.ts` — cache key agora inclui os parâmetros da busca
- `useFetchItems.ts` — cache keys de itens, Live TV e seções passaram a incluir `user.Id` para evitar colisão entre sessões
- `liveTvHooks/*` e `useGetDownload.ts` — chaves de cache passaram a ser separadas por usuário
- `filterdialog.ts` — contrato local mínimo para o cliente legado do filtro e proteção nula em controles do diálogo
