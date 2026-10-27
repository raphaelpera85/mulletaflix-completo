# Trickplay: backpressure para refreshes não bloqueantes

`TrickplayProvider` deixa no máximo oito refreshes no modo `NonBlocking` detached ao mesmo tempo. Ele adquire uma vaga antes de iniciar `ObserveBackgroundRefreshAsync`; quando todas estão ocupadas, o próprio `FetchAsync` aguarda de forma cancelável. Assim, o scheduler de biblioteca mantém as chamadas adicionais na sua fila limitada e pode aplicar backpressure ao scan, em vez de criar um Task detached por item. Mídias e chamadas `replace` não são descartadas ou coalescidas.

As vagas são liberadas em `finally`, inclusive após falha ou cancelamento de refresh. Se o token do scan for cancelado enquanto espera a vaga, o provider retorna `ItemUpdateType.None`, preservando o contrato de cancelamento não bloqueante.

`TrickplayProviderTests` Release passou 3/3 em Windows/.NET 10 e formatter direcionado passou. A regressão confirma oito chamadas em background e bloqueio da nona até uma vaga ser liberada. O número oito é fixo e ainda precisa ser avaliado com uma biblioteca representativa. Linux, runtime instalado e produção não foram validados; sem release ou crédito à homologação formal (2/7, 28,6%).
