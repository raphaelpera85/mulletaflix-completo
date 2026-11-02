
## ProviderManager: fila de refresh — 2026-11-02

A posse do worker permanece ativa durante a execução dos lotes. Depois de cada lote, a fila e a flag são verificadas sob `_refreshQueueLock`; se outro item entrou, o worker continua. `Dispose` marca `_disposed` sob a mesma trava usada para obter o token do worker. Regra de cache de ignorados é limpa com log de falha sem rearmar ciclo. O contrato público e dependências não mudaram; o fluxo de ownership interno mudou. Testes focais 1/1 e suíte Providers 931 aprovados, 14 ignorados, 0 falhas/945 em Windows/.NET 10. A suíte usa uma janela de 100 ms e não equivale a stress exaustivo. Linux, CI hospedada, instalação e produção pendentes. Fonte oficial: https://learn.microsoft.com/en-us/dotnet/standard/collections/thread-safe/ .
