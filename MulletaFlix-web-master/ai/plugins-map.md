# Mapa de Plugins — MulletaFlix Web

Diretório: `src/plugins/` — registro via `import.meta.glob` em `src/components/pluginManager.ts`.

## Legenda de tipos

| Tipo | Significado |
|---|---|
| `MediaPlayer` | Player de mídia (vídeo, áudio, livros, PDF, etc.) |
| `Screensaver` | Screensaver de inatividade |
| `PreplayIntercept` | Intercepta antes da reprodução (validação/avisos) |
| `SyncPlay` | Sincronização de playback entre dispositivos |

## Plugins existentes (14)

| Plugin | id | Tipo | Prioridade | Status |
|---|---|---|---|---|
| HTML Video Player | `htmlvideoplayer` | MediaPlayer | 1 | ✅ estável |
| HTML Audio Player | `htmlaudioplayer` | MediaPlayer | 1 | ✅ estável |
| YouTube Player | `youtubeplayer` | MediaPlayer | 1 | ✅ estável |
| Chromecast Player | `chromecast` | MediaPlayer | - | ✅ estável (não-local) |
| Session/Remote Player | `remoteplayer` | MediaPlayer | - | ✅ estável (não-local) |
| Book Player | `bookplayer` | MediaPlayer | 1 | ✅ estável |
| PDF Player | `pdfplayer` | MediaPlayer | 1 | ✅ estável |
| Comics Player | `comicsplayer` | MediaPlayer | 1 | ✅ estável |
| Photo Player | `photoplayer` | MediaPlayer | 1 | ✅ estável |
| SyncPlay | `syncplay` | SyncPlay | 1 | ⚠️ precisa refactor (nota no código: não deveria ter playback manager independente) |
| Backdrop Screensaver | `backdropscreensaver` | Screensaver | - | ✅ estável (não anônimo) |
| Logo Screensaver | `logoscreensaver` | Screensaver | - | ✅ estável (anônimo) |
| Play Access Validation | `playaccessvalidation` | PreplayIntercept | order -2 | ✅ estável |
| Experimental Warnings | `expirementalplaybackwarnings` | PreplayIntercept | - | ✅ estável (id com typo histórico — candidato a correção) |

## Pontos de melhoria identificados

1. **SyncPlay** — refactor para não ter playback manager independente (nota do próprio código).
2. **Experimental Warnings** — id `expirementalplaybackwarnings` tem typo; corrigir exige cuidado para não quebrar estado salvo (candidato a alias).
3. **Backdrop Screensaver** — `supportsAnonymous = false`; avaliar se deve suportar anônimos.
4. **Novos plugins candidatos**:
   - Screensaver com relógio/estatísticas do servidor
   - Player de música com visualização (album art + equalizer)
   - Intercept de "pré-roll/trailer" antes de filmes (cinema mode já existe parcialmente)
   - Widget de clima/notícias para screensaver

## Regras ao modificar plugins

- Nunca mudar `id` sem alias de compatibilidade.
- Manter `priority` coerente (players de plugin têm prioridade 1).
- Validar com `npm run build:check` + `npm test`.
- Atualizar este mapa após qualquer mudança.
