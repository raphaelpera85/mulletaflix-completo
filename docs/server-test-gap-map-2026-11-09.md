# Mapa de testes e gaps do servidor — 2026-11-09

## Evidência atual

| Área | Inventário local | Validação conhecida | Estado |
|---|---:|---|---|
| .NET servidor | 17 projetos de teste; 682 arquivos C# de teste | Release: 0 erros; regressão filtrada anterior: 6.028 aprovados, 101 ignorados, 0 falhas | Parcial |
| NebulaPython | 40 arquivos `test_*.py` | 727 aprovados, 2 ignorados, 0 falhas; Ruff: 309 findings | Parcial |
| Web | 367 arquivos com padrão `test`/`spec` no checkout, incluindo dependências e artefatos | Vitest: 430/430; build/typecheck aprovados; Playwright autenticado/catalogação ainda parcial | Parcial |
| Portal | Contrato do portal | 379/379; JSON válido | Verde local |
| Integração .NET | `Jellyfin.Server.Integration.Tests` e categorias externas | MariaDB/Mongo descartáveis já cobertos em rodadas anteriores; integrações reais continuam sem aceite | Parcial |
| Android | Testes JVM existentes; instrumentado depende de dispositivo | `adb devices` sem dispositivo; instrumentado bloqueado | Bloqueado |
| Linux/POSIX | Testes condicionais e contratos de segurança | WSL/distribuição indisponível; branches POSIX permanecem ignoradas | Bloqueado |
| Runtime instalado | Smoke público e restore realizados anteriormente | Login/catálogo/playback autenticados não aceitos; source novo não promovido | Bloqueado |

## Gaps classificados

### P0 — observabilidade e catálogo autenticado

1. Materialização de mídia sintética após scan: há histórico de `MovieCount=0`, `SeriesCount=0` e `items=[]` em execuções do stage. O diagnóstico precisa separar scanner, persistência, consulta autenticada e timing.
2. Contrato HTTP de `GET /Items/Counts`: os testes atuais cobrem autorização e `GET /Items`, mas não validam o payload de contagens em uma chamada autenticada.
3. Playback HLS/multiusuário no runtime instalado: teste local de stage não equivale a aceite autenticado persistente.

### P1 — integrações e persistência

1. Mongo/Supabase/Telegram/FTP reais permanecem fora do aceite por credenciais e produção.
2. Casos `RequiresMongo`, Linux/POSIX, ACLs, symlink, bind mount e recuperação de processo precisam de CI/host compatível; skips não contam como aprovados.
3. Backup Nebula → Supabase possui TDD para configuração ausente, mas falta execução de integração real autorizada.

### P1 — qualidade estática com risco comportamental

- Ruff continua em 309 findings. `BLE001`, `S110`, `RUF059`, `UP045`, `UP006`, `UP041`, `UP024` e demais regras semânticas devem ser triadas em fatias TDD; não aplicar autofix global.
- Os imports `I001` e o `B023` conhecido do worker foram zerados nos alvos já tratados.

### P2 — cobertura de contrato

- Expandir contratos HTTP de endpoints que atualmente têm apenas teste de status.
- Priorizar invariantes observáveis: payload não nulo, contagens não negativas, autorização, isolamento por usuário e comportamento de erro.
- Não fabricar testes de integração com mídia de produção; usar fixtures sintéticas descartáveis.

## Próximo ciclo TDD

1. RED/GREEN de `Items/Counts` autenticado no `Jellyfin.Server.Integration.Tests`, sem depender de mídia real.
2. Regressão do projeto de integração e build Release.
3. Adicionar o resultado ao `TODO-AUDITORIA.md`, portal e Obsidian.
4. Em seguida, mapear o fluxo de materialização com timeouts HTTP explícitos e evidência de cada chamada, sem alterar runtime instalado.

## Limites de aceite

Este mapa não transforma contagem de arquivos ou testes passados em percentual de cobertura de linhas. A homologação global continua **PARTIAL** enquanto runtime autenticado, playback, Linux/POSIX, Android instrumentado, CI hospedada e integrações reais permanecerem sem evidência.
