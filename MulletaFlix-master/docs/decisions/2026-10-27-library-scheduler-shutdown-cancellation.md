# Cancelar enqueues no shutdown do host

Data: 2026-10-27

## Decisão

Usar `IHostApplicationLifetime.ApplicationStopping` como token de cancelamento das escritas no canal limitado e da espera pela conclusão do workset. O shutdown cancela operações que não podem mais avançar quando os workers são encerrados. O descarte explícito do scheduler ainda aguarda os enqueues admitidos terminarem antes de concluir o canal.

## Motivo

Com backpressure, `WriteAsync` sem cancelamento pode esperar indefinidamente quando `ApplicationStopping` encerra todos os leitores. `DisposeAsync` aguarda enqueues ativos, portanto fechar o canal somente depois dessa espera não resolve o bloqueio. Cancelar as duas esperas no token do host libera o enqueue, enquanto mantém o fluxo de descarte explícito gracioso.

## Evidência

- Fonte: `MediaBrowser.Controller/LibraryTaskScheduler/LimitedConcurrencyLibraryScheduler.cs`.
- Teste: `tests/Jellyfin.Controller.Tests/LibraryTaskScheduler/LimitedConcurrencyLibrarySchedulerTests.cs`.
- Release: `docs/releases/12.1.39.md`.
