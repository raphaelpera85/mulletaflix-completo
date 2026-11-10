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

## Evidência adicionada em 2026-11-09 07:36 BRT

- `ItemsControllerTests.GetItems_WithValidUserAndRecursiveQuery_ReturnsItemsAndForwardsQuery`: **1/1** focal e **22/22** na classe.
- O RED inicial revelou uma dependência legítima do caminho `Folder.GetItems` em `BaseItem.LibraryManager`; o teste final usa somente fixture sintética, restaura o estado estático e valida `Recursive`, usuário, `EnableTotalRecordCount` e DTO materializado pelo controller.
- A cobertura continua unitária; o endpoint HTTP real, MariaDB descartável, materialização persistente, runtime instalado e playback permanecem sem aceite.

## Próximo ciclo TDD

1. Corrigir o bloqueio de composição DI de `BackupService` observado no harness de integração e garantir limpeza idempotente do banco de teste após falha de inicialização.
2. Reexecutar o contrato HTTP autenticado com MariaDB descartável em porta não padrão, sem usar `3306` nem mídia de produção.
3. Mapear o fluxo de materialização com timeouts HTTP explícitos e evidência de cada chamada, sem alterar runtime instalado.
4. Depois, separar o aceite de playback HLS/multiusuário do aceite de catálogo.

## Evidência adicionada em 2026-11-09 23:46 BRT

- `ItemCountServiceTests.GetItemCounts_GroupsKnownTypesAndPreservesTotal`: **1/1** focal.
- A primeira execução RED revelou que a fixture mockava o locking sem chamar o callback real de `SaveChanges`; a correção ficou restrita ao teste.
- MariaDB 11.4.4 descartável foi inicializado em `127.0.0.1:33188` com datadir efêmero; as variáveis de confirmação foram usadas somente no processo da execução e não ficaram persistidas no ambiente.
- `Jellyfin.Server.Integration.Tests` com filtro `ItemsControllerTests`: **0/6 aprovados, 6 falhas**. Falhas observadas: DI ambígua para `BackupService` e banco de teste já existente após falha de setup.
- O MariaDB descartável foi encerrado, o datadir removido e nenhuma instância scratch permaneceu; `3306` não foi acessado.

## Limites de aceite

Este mapa não transforma contagem de arquivos ou testes passados em percentual de cobertura de linhas. A homologação global continua **PARTIAL** enquanto runtime autenticado, playback, Linux/POSIX, Android instrumentado, CI hospedada e integrações reais permanecerem sem evidência.
