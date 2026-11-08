# Revalidação integrada da preferência TMDB — 2026-10-27 08:45 BRT

## Rechecagem do log idêntico — 2026-10-27 12:52 BRT

O anexo voltou com o SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual às cópias anteriores. São deadlocks MariaDB 1213 no `DELETE BaseItemImageInfos` para o mesmo ItemId; o HTTP 200 inicial não confirma commit, e o trecho não traz PID, AssemblyVersion, relatório InnoDB nem o resultado final das tentativas.

A DLL instalada foi verificada novamente: versão 12.1.35 e SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`. A fonte local 12.1.32 coordena as gravações por stripe e `GET_LOCK`, mas isso não atribui o log ao runtime nem comprova a correção na instalação. `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 20/20 em Release; os três testes de integração MariaDB selecionados foram ignorados porque a conexão descartável não foi configurada nesta rodada. O teste de concorrência MariaDB anterior passou 1/1 em 27/10 às 08:19 e está registrado separadamente. Banco operacional não acessado; instalação, CI e produção não validados. Formal 2/7, sem crédito/release.

Em Windows/.NET 10 Release, os testes focados `TmdbUtilsTests`, `ItemImageProviderTests`, `ProviderManagerImageProviderTests`, `MetadataServiceTests` e `TmdbMovieImageProviderFlowTests` passaram 144/144. A suíte integral `Jellyfin.Providers.Tests` passou com 884 aprovados, 13 integrações externas ignoradas e 0 falhas/897. Isso revalida a seleção pt-BR/pt, o inglês como fallback, o filtro por tipo e o fluxo agregado/refresh conforme os fixtures locais. Nenhuma requisição TMDb/CDN foi feita; não se inspecionou nem atualizou *Invasão Zumbi 2* ou outras imagens persistidas. Imagens legadas sem `ProviderName` continuam sem substituição automática para proteger capas manuais. Runtime instalado, interface visual, CI e produção não foram verificados; formal 2/7, sem crédito/release.

# Revalidação do log repetido — 2026-10-26 23:02 BRT

O anexo tem SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2, idêntico ao incidente anterior. O trecho mostra erros MariaDB 1213 em DELETEs de BaseItemImageInfos, várias tentativas para o mesmo ItemId e um aviso de atraso no heartbeat Kestrel; não contém PID/assembly, relatório InnoDB nem resultado final dos retries. O HTTP 200 inicial pertence a outra operação.

No checkout atual, SaveImagesAsync aguarda a stripe estática do ItemId antes de abrir contexto e toma advisory GET_LOCK MariaDB antes da transação; UpdateOrInsertItems e DeleteItem compartilham a coordenação. Os testes unitários ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests passaram 52/52 em Windows/.NET 10 Release. Não executam concorrência MariaDB real e não provam o runtime instalado. O alerta de heartbeat não demonstra causalidade. Agregado 5.465/5.493, formal 2/7 (28,6%), sem crédito/release. Runtime emissor, CI atual, teste MariaDB nesta sessão e produção seguem pendentes. Ver sessão vault 2026-10-26-MulletaFlix-image-deadlock-duplicate-rerun-2302.

# Auditoria do fluxo de capas e imagens

## Verificação somente leitura do runtime instalado — 2026-10-26 20:16 BRT

Foi lida a DLL `C:\Program Files\MulletaFlix\Server\MulletaFlix.Server.Implementations.dll`: versão de arquivo/produto 12.1.35, SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`. Uma busca de strings UTF-8/UTF-16 não encontrou `mfx:item:` nem `assembly version`. O checkout declara 12.1.32 e a fonte contém esses marcadores no lock advisory e na mensagem de retry, logo o arquivo instalado diverge do checkout auditado.

Não havia processo MulletaFlix/Jellyfin ativo; o serviço `MulletaFlixNginx` estava ativo. Nenhum banco foi acessado, processo iniciado, serviço alterado ou arquivo substituído. A inspeção não prova que esta DLL produziu o log de 23/10 nem se os retries terminaram; a correlação histórica permanece aberta. Correção não instalada, nenhuma release nesta rodada. Homologação formal 2/7 (28,6%); sem crédito.

## Reenvio idêntico do deadlock e regressão focal — 2026-10-26 20:14 BRT

O anexo tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao incidente de 23/10. Ele repete MariaDB 1213 durante `DELETE BaseItemImageInfos` concorrente para o mesmo item, mas não inclui PID, versão do assembly, relatório InnoDB nem o resultado final das retentativas. A resposta HTTP 200 anterior é independente e não comprova commit.

`ItemPersistenceServiceTests` e `ItemPersistenceDatabaseLockTests` passaram 50/50 em Windows/.NET 10 Release nesta rodada. A fonte atual serializa a gravação por stripe do item no processo e por advisory lock MariaDB entre instâncias participantes. Este resultado valida os testes unitários da fonte atual; não atribui o log a um binário, não prova que o retry concluiu e não valida produção. O evento foi atualizado no portal; agregado técnico permanece 5.465/5.493 e homologação formal 2/7 (28,6%), sem crédito ou release.

## Reexecução independente MariaDB — 2026-10-26 13:15 BRT

Após a revalidação registrada às 12:43, `ServerPersistenceMariaDbTests` foi executado novamente nesta sessão e passou 19/19 em Windows/.NET 10 Release contra a instância MariaDB 11.4.13 descartável em `127.0.0.1:33099`. A instância foi encerrada pelo cliente admin; verificação confirmou a porta fechada. `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` já havia passado 49/49 na mesma rodada anterior. O anexo continua idêntico (SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`). Esta nova execução confirma novamente a fonte local, mas não identifica PID/assembly do incidente nem valida runtime instalado, Linux, CI remota atual ou produção. Contrato local do portal passou 279/279. Homologação formal 2/7 (28,6%), sem crédito; nenhuma release.

## Revalidação MariaDB — 2026-10-26 12:43 BRT

O anexo desta rodada tem o mesmo SHA-256 (`53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`) dos logs já auditados. `ServerPersistenceMariaDbTests` passou 19/19 contra MariaDB 11.4.13 descartável em loopback `127.0.0.1:33099`; `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 49/49, ambos Release/.NET 10. A instância e a conta temporária foram encerradas/removidas; a porta ficou fechada. A validação prova a fonte local, mas o log não identifica PID/assembly nem correlaciona o incidente com o runtime instalado. Sem alteração de runtime, instalação ou release; homologação formal continua 2/7 (28,6%), sem crédito. Linux, CI remota do checkout e produção continuam pendentes.

## Reenvio idêntico do deadlock — 2026-10-26 10:11 BRT

O anexo recebido nesta conferência tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico aos registros anteriores. Repete deadlock MariaDB 1213 em `DELETE BaseItemImageInfos`; não informa PID/assembly, desfecho da última tentativa nem relatório InnoDB.

A evidência integrada posterior já registrada em [[Sessões/2026-10-26-MulletaFlix-image-lock-mariadb-stress-0902]] passou `ServerPersistenceMariaDbTests` 15/15 em MariaDB 11.4.13 descartável e o cenário `DeleteItem_ConcurrentWithImageRefreshes_CompletesWithoutDeadlock` 10/10 adicionais; os testes unitários passaram 43/43. Esta conferência apenas validou a identidade do anexo; não repetiu os testes de banco. O log não pode ser atribuído à DLL instalada. Runtime, CI Linux e produção continuam sem confirmação; homologação formal permanece 2/7 (28,6%), sem crédito ou release.

## Reenvio idêntico do deadlock — 2026-10-26 08:26 BRT

O anexo desta rodada tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual aos registros anteriores. Repete `DELETE BaseItemImageInfos` concorrentes e erro MariaDB 1213 para o mesmo item; não inclui PID/assembly, término das tentativas nem relatório InnoDB.

Revisão dos escritores no checkout encontrou as mutações de `BaseItemImageInfos` concentradas em `ItemPersistenceService`: `SaveImagesAsync`, `SaveItems`, `DeleteItem` e exclusões em cascata. Os caminhos explícitos compartilham as stripes de `GetItemPersistenceLock`; a proteção MariaDB usa `ItemPersistenceDatabaseLock`. `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` recompilado em Windows/.NET 10 Release passou 49/49. Isso prova o comportamento da fonte local, não identifica o binário do log nem valida MariaDB, runtime instalado ou produção. A fonte permanece apenas na working tree; sem release/instalação. Homologação formal permanece 2/7 (28,6%), sem crédito.

## Lock pai revalidado em cenários concorrentes — 2026-10-26 06:49 BRT

Na branch de auditoria baseada em `d51f4f38`, os testes focais
`ServerPersistenceMariaDbTests` passaram 7/7 em Release contra MariaDB 11.4.13
descartável e autenticada em `127.0.0.1:33099`. Dois casos mantiveram a linha
`BaseItems` bloqueada em uma sessão e verificaram que `SaveImagesAsync` espera
quando concorre com `SaveItems` ou `DeleteItem`; após liberar o lock, cada fluxo
terminou e deixou estado consistente. A implementação usa `SELECT ... FOR
UPDATE` nas linhas pai para sincronizar escritores entre processos, além dos
locks já existentes no processo e advisory MariaDB.

O banco de teste foi encerrado com shutdown autenticado e a porta 33099 fechou.
O log reportado continua sem PID/assembly, resultado final das tentativas ou
correlação com runtime; esses testes não reproduzem a carga de produção nem
provam que o servidor instalado contém esta alteração. CI Linux, runtime,
produção e homologação formal 2/7 (28,6%) seguem pendentes. O contrato local da
página de homologação passou 256/256, e `ItemPersistenceServiceTests` passou
24/24 em Release, cobrindo retries transitórios e falhas permanentes. Nenhuma
release foi criada.

## Concorrência de imagens reexecutada — 2026-10-26 06:41 BRT

MariaDB 11.4.13 descartável, com autenticação e bind somente em `127.0.0.1:33099`,
passou cinco testes focais Release: refreshes simultâneos do mesmo item, gravação
combinada de metadados/imagens, batch entre lock stripes, espera por advisory lock
de outra sessão e exclusão concorrente com refresh. O teste novo de 24 itens em
múltiplas stripes passou. O processo foi encerrado e a porta fechada.

Os cenários não reproduziram o deadlock histórico. O log segue sem PID/assembly,
versão carregada e resultado final dos retries; CI hospedada do checkout, runtime
instalado e produção continuam pendentes. Isso não credita homologação formal
(2/7; 28,6%) e não permite declarar a correção instalada.

## Concorrência e restore em MariaDB descartável — 2026-10-26 05:18 BRT

Foi iniciada uma instância MariaDB 11.4.13 com datadir novo, bind somente em `127.0.0.1:33099` e credencial aleatória temporária. O filtro de integração MariaDB configurado no workflow CI rodou em Release: `IntroSkipper.Integration.Tests` passou 16/16, sem skips ou falhas. A seleção incluiu refresh de imagens concorrente, gravação simultânea de metadados/imagens, locks consultivos entre conexões e cenários de restore/backup.

O servidor descartável foi encerrado e a porta 33099 ficou sem listener. A ferramenta recusou a remoção recursiva do datadir novo em TEMP, que foi preservado; nenhum processo permaneceu ativo. Isso valida concorrência na fonte local contra MariaDB Windows, não identifica o processo do log histórico nem prova runtime/produção. DLL instalada segue 12.1.35; Linux/CI hospedada e formal 2/7 (28,6%) permanecem pendentes, sem crédito.

## Repetição do anexo e estado instalado — 2026-10-26 05:07 BRT

O arquivo recebido tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico ao incidente já auditado. Não traz PID/assembly, resultado final dos retries ou relatório InnoDB. Uma leitura local confirmou ausência de processo MulletaFlix/Jellyfin em execução; a DLL instalada permanece na versão 12.1.35, SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`. Isso não associa a DLL ao evento histórico nem demonstra correção em produção. Sem acesso ao banco operacional, instalação ou nova release; homologação formal 2/7 (28,6%), sem crédito.

## Regressão de integração com MariaDB descartável — 2026-10-26 01:01 BRT

Para validar os testes de integração que antes eram opt-in, foi baixado o ZIP oficial MariaDB 11.4.13, conferido pelo SHA-256 publicado, e criado um datadir descartável isolado. A instância ouviu somente em `127.0.0.1:33099`; a suíte `ServerPersistenceMariaDbTests` passou 17/17 e `IntroSkipper.Integration.Tests` completa passou 57/57 em Windows/.NET Release. O servidor foi encerrado e a porta fechou. A pasta temporária específica permanece porque a ferramenta bloqueou a remoção recursiva; não contém dados operacionais e está fora do checkout.

Isso confirma os testes contra MariaDB real 11.4.13, não correlaciona o deadlock observado ao processo/assembly de produção nem valida o banco operacional, instalação, Linux ou CI hospedada. O aviso de deadlock recebido continua sem PID/assembly e sem relatório InnoDB. Nenhuma alteração de runtime foi feita; homologação formal continua 2/7 (28,6%), sem crédito ou release.

## Rechecagem do deadlock e do binário local — 2026-10-26 00:50 BRT

O anexo desta sessão tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao incidente anterior. O checkout contém coordenação por stripe local e `GET_LOCK` MariaDB em `SaveImagesAsync`, `SaveItems` e `DeleteItem`; revisão independente não encontrou outro escritor conhecido sem lock. Isso não identifica a versão que produziu o log nem elimina writer externo.

Leitura da DLL em `C:\Program Files\MulletaFlix\Server\MulletaFlix.Server.Implementations.dll`: versão 12.1.35, SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`; a busca literal não encontrou `ItemPersistenceDatabaseLock` nem `mfx:item:`. A DLL local não foi correlacionada ao processo do log. Testes locais `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests`: 49/49 em Windows/.NET 10 Release. Nenhum MariaDB foi acessado: connection string descartável ausente, porta 33099 sem listener e runtime MariaDB/container não instalado. Sem consulta operacional, relatório InnoDB, instalação ou produção validada; homologação formal segue 2/7 (28,6%), sem crédito ou release.

## Revalidação de regressão Release e anexo repetido — 2026-10-25 23:38 BRT

O anexo recebido nesta continuação tem novamente SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`: mesmo evento conhecido, sem PID/assembly, desfecho dos retries ou relatório InnoDB. A DLL instalada foi rechecada por leitura: versão 12.1.35, SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`; marcadores `ItemPersistenceDatabaseLock` e `mfx:item:` ausentes. O working tree contém stripe por item, lock `GET_LOCK` MariaDB e transação antes dos writes, mas não há evidência para atribuir o log a essa DLL.

Verificação Windows/.NET 10 Release desta sessão: `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` 49/49; `Jellyfin.Server.Implementations.Tests` 1.647 aprovados, 77 ignorados, 0 falhas/1.724. Uma execução anterior interrompida após ~64 s foi inconclusiva; a repetição completa terminou em 1m40s. Contrato portal 218/218, JSON válido e `git diff --check` passaram após a atualização. Nenhuma integração MariaDB foi executada (opt-in ausente), nem banco operacional consultado. Isto valida a suíte local, não concorrência entre processos, DLL instalada, runtime em execução ou produção. Homologação formal permanece 2/7 (28,6%), sem crédito ou release.

## Testes da resposta ambígua de GET_LOCK — 2026-10-25 21:23 BRT

Foram adicionados testes comportamentais para os dois fluxos de `ItemPersistenceDatabaseLock`. O provider Pomelo MySQL ativa a guarda de produção; a conexão fake registra a aquisição do nome de lock e lança exceção como se a resposta de `GET_LOCK` tivesse se perdido. `Acquire` e `AcquireAsync` então precisam enviar `RELEASE_LOCK` para o mesmo nome e deixar nenhum lock registrado como retido. A suíte completa `Jellyfin.Server.Implementations.Tests` passou 1.641, ignorou 77 e falhou 0/1.718 em Windows/.NET 10 Release. O formatador passou e a revisão independente não achou defeitos.

O fake não valida semântica do servidor MariaDB, desconexão de rede, pool real, Linux, CI hospedada ou produção. Essas verificações permanecem pendentes; homologação formal continua 2/7 (28,6%), sem crédito.

## Limpeza do lock advisory após resposta síncrona ambígua — 2026-10-25 21:15 BRT

`ItemPersistenceDatabaseLock.Acquire` síncrono agora libera, na falha, até o lock corrente além dos locks confirmados. O servidor MariaDB pode ter concluído `GET_LOCK` mesmo se a resposta se perder; tentar `RELEASE_LOCK` também para o nome ainda não confirmado é seguro: retorna `0` se a sessão não o detém e `NULL` se o lock não existe. O caminho assíncrono usa o mesmo cálculo para manter paridade.

Verificação Windows/.NET 10 Release: `ItemPersistenceDatabaseLockTests|ItemPersistenceServiceTests` 44/44; `Jellyfin.Server.Implementations.Tests` 1.639 aprovados, 77 ignorados, 0 falhas/1.716; `dotnet format style --verify-no-changes` passou nos arquivos alterados. Os ignorados incluem opt-ins MongoDB e casos condicionais de plataforma/ambiente. Não houve MariaDB descartável, acesso ao banco operacional, CI hospedada, runtime instalado ou produção. O anexo histórico de deadlock não informa PID/assembly e não foi correlacionado à fonte local. Homologação formal continua 2/7 (28,6%), sem crédito; sem release ou instalação.

## Preservação de arquivos compartilhados ao remover referências — 2026-10-25 20:54 BRT

`BaseItem.DeleteImageAsync` e `ItemImageProvider.PruneImages` agora removem e persistem/desanexam somente a referência da imagem; nenhum dos dois fluxos apaga os bytes locais. A busca de twins encontrou os dois escritores de exclusão física relevantes, e ambos foram tratados. A regra falha fechada porque não existe coordenação atômica global que prove que outro item não aponta para o mesmo caminho.

O custo conhecido é acumular arquivos sem referência. Não foi implementado um coletor de órfãos; cleanup seguro exigirá coordenação global ou varredura offline com revalidação. Arquivos previamente apagados não podem ser recuperados por esta alteração. Testes: `BaseItemTests` 12/12; suíte `Jellyfin.Controller.Tests` 65/65; `ImageController` 50/50; `ItemImageProviderTests` 63/63; suíte `Jellyfin.Providers.Tests` 868 aprovados, 13 ignorados por opt-ins de integração, 0 falhas; `dotnet format style --verify-no-changes` passou.

Isto não corrige o deadlock MariaDB 1213 do anexo: o SHA-256 do arquivo recebido (`53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`) é idêntico ao já auditado, sem PID/assembly ou desfecho. Sem runtime instalado, Linux nativo, MariaDB de integração ou produção validados; homologação formal 2/7 (28,6%), sem crédito e sem release.

## Preservação da imagem anterior durante troca — 2026-10-25

O fluxo foi alterado para não apagar o arquivo anterior dentro de `ImageSaver.SaveImage`: a gravação do arquivo e a persistência posterior da linha `BaseItemImageInfos` são operações separadas, então manter os bytes antigos evita referência quebrada caso o commit falhe. Se o destino calculado for igual ao caminho ainda referenciado no objeto, `ImageSaver` usa um nome irmão único para não sobrescrever esse arquivo antes do commit. `LibraryManager.UpdateImagesAsync` mantém a persistência existente e não executa limpeza concorrente.

A proposta anterior de consultar referências e apagar os caminhos substituídos após o commit foi descartada na revisão: escritores de itens diferentes podem compartilhar caminho e uma associação pode ocorrer entre a consulta e o `DeleteFile`. Portanto, imagens antigas/sem referência podem permanecer órfãs no disco; remoção segura requer coordenação global de caminhos ou limpeza offline revalidada, fora desta alteração. A descrição histórica de `BaseItem.DeleteImageAsync` nesta seção foi superada pela política registrada em “Preservação de arquivos compartilhados ao remover referências”, às 20:54 BRT.

Verificações locais: `ImageSaverTests` 1/1; `ItemPersistenceServiceTests` 31/31; `MetadataServiceTests.SaveRemoteResultImages` 12/12; `ServerPersistenceMariaDbTests.SaveImagesAsync_ReplacesImageRowsAndPreservesSharedPaths` compilou, mas foi ignorado por ausência de connection string MariaDB descartável; portal 197/197. Sem validação de runtime instalado, Linux/CI hospedada, mídia real ou produção. Homologação formal segue 2/7 (28,6%), sem crédito.

## Remoção explícita de imagem — 2026-10-25

Esta descrição é histórica e foi superada em 25/10/2026 às 20:54 BRT: `BaseItem.DeleteImageAsync` não chama mais `FileSystem.DeleteFile`; a referência continua sendo restaurada se a persistência falhar. Os dois endpoints de `ImageController` permanecem no mesmo método.

Limites históricos: testes usam `UpdateToRepositoryAsync` sobrescrito e `IFileSystem` mockado; não simulam transação MariaDB real nem bytes reais. A exclusão de bytes compartilhados foi removida na atualização de 20:54 BRT. Cleanup recuperável continua inexistente. O cancelamento continua `CancellationToken.None`, conforme assinatura existente. Não valida deadlock 1213, runtime instalado, Linux/CI hospedada ou produção; formal 2/7 (28,6%), sem crédito.

## Risco residual na substituição de imagem — 2026-10-25

Rastreamento de `ImageSaver.SaveImage` mostra que o arquivo antigo pode ser apagado após gravar o novo e alterar o caminho em memória, enquanto `BaseItemImageInfos` é persistido depois, numa transação MariaDB separada. Se essa persistência falhar, existe uma janela em que o banco ainda referencia o arquivo antigo já removido. Isso é risco comprovado pela ordem do código, não ocorrência confirmada em produção.

O teste MariaDB `RefreshImages_PersistsSelectedPortugueseCandidateToMariaDb` simula `ProviderManager.SaveImage`; não integra `ImageSaver` nem injeta falha de banco depois da substituição. Próximo passo: desenhar uma confirmação/cleanup transacional que preserve a imagem antiga até a referência nova ser durável e cobrir rollback sem deixar arquivo ou referência órfã. Não alterado nesta sessão.

## Rechecagem da DLL instalada — 2026-10-25 11:06 BRT

O anexo tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao log já analisado: 12 comandos `DELETE BaseItemImageInfos`, deadlocks InnoDB 1213 e retries para o mesmo item. A linha HTTP 200 antecede os deletes e não comprova commit; PID/assembly, desfecho dos retries e relatório InnoDB não aparecem.

`ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 40/40 em Windows/.NET 10 Release. A fonte local contém stripe intra-processo e advisory lock MariaDB. Leitura somente do arquivo instalado `C:\Program Files\MulletaFlix\Server\MulletaFlix.Server.Implementations.dll` encontrou versão 12.1.35, SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`, sem o marcador ASCII `mfx:item:`; `SharedVersion.cs` no checkout declara 12.1.32. A divergência não prova qual assembly produziu o log. O processo `mysqld` estava ativo, mas não houve conexão ao banco; nenhum servidor MulletaFlix foi iniciado.

Homologação formal permanece 2/7 (28,6%), sem crédito. Sem integração MariaDB nesta sessão, instalação, release, commit, push ou publicação remota. Próximo gate: correlacionar PID/assembly do runtime emissor e validar concorrência em MariaDB descartável e no runtime alvo antes de afirmar resolução.

## Teste ponta a ponta seleção localizada → MariaDB — 2026-10-25 10:56 BRT

Adicionado `RefreshImages_PersistsSelectedPortugueseCandidateToMariaDb` à fixture descartável existente. O teste passa um candidato `pt` por `ItemImageProvider.RefreshImages`, verifica `ProviderName`/idioma no item e persiste os campos consultando a linha MariaDB real. A resposta HTTP e `SaveImage` são simulados: isto não valida bytes/arquivo de imagem nem TMDb real. O projeto de integração agora referencia `MediaBrowser.Providers`, e o job Linux/MariaDB seleciona o caso. O projeto compilou em Release; a execução local foi ignorada porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada. Os dois testes unitários de seleção localizada passaram; o contrato estático CI MariaDB 2/2, a suíte do portal 197/197 e `dotnet format style --verify-no-changes` passaram. Nenhuma imagem real/TMDb, runtime instalado, Linux hospedado ou produção foi acessado; homologação formal continua 2/7 (28,6%), sem crédito.

## Reenvio idêntico do deadlock e regressão focal — 2026-10-25 10:13 BRT

O novo anexo tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao log já auditado: 12 `DELETE BaseItemImageInfos`, 11 deadlocks MariaDB 1213 e retries, sem PID/assembly, desfecho final ou relatório InnoDB. A regressão focal `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` foi recompilada e passou 40/40 em Windows/.NET 10 Release. É evidência apenas do checkout local; `HEAD` ainda não contém o helper de locks. Não foi executada integração MariaDB nesta sessão nem acessado banco operacional. A instância emissora e o runtime permanecem sem correlação; homologação formal 2/7 (28,6%), sem crédito.

## Rechecagem do anexo duplicado e dos testes — 2026-10-25 07:58 BRT

O anexo tem o mesmo SHA-256 do log já auditado; não acrescenta PID/assembly nem resultado final das tentativas. A regressão unitária `ItemPersistenceServiceTests` recompilada em Windows/.NET 10 Release passou 31/31. A repetição isolada de `SaveImagesAsync_ConcurrentRefreshesForSameItem` foi selecionada e ignorada por ausência da conexão MariaDB descartável; o resultado anterior de `ServerPersistenceMariaDbTests` 14/14 permanece evidência separada, não foi repetido nesta sessão. O código do checkout contém stripe intra-processo e advisory lock MariaDB, enquanto `HEAD` não possui o helper; logo isso não comprova correção no binário do incidente. Nenhum banco operacional foi acessado. Homologação formal permanece 2/7 (28,6%), sem crédito; runtime do log e validação de instalação continuam pendentes.

## Revalidação do log repetido — 2026-10-25 05:12 BRT

O anexo repete 12 `DELETE BaseItemImageInfos` para o mesmo item, 11 deadlocks MariaDB 1213, retries e heartbeat Kestrel atrasado. HTTP 200 antecedente não comprova commit; o trecho não informa PID/assembly/versão nem desfecho final. Auditoria dos caminhos mutáveis na fonte local encontrou três exclusões explícitas, uma exclusão em cascata e a migração que apenas atualiza imagens; todos compartilham stripes e advisory locks MariaDB. `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` recompilado em Release/.NET 10 passou 40/40. Isso valida o checkout local, não o binário do log. Banco operacional não foi acessado; causa em produção segue sem correlação conclusiva. Homologação formal permanece 2/7 (28,6%), sem crédito ou release.

## Regressão ampla e reanálise do log repetido — 2026-10-25 04:56 BRT

O anexo contém 12 `DELETE BaseItemImageInfos` para o mesmo item, 11 erros MariaDB 1213 (deadlock), 12 avisos de retry e um heartbeat Kestrel de 2,35 s. O trecho não inclui PID, assembly/versão, `SHOW ENGINE INNODB STATUS` nem o desfecho da persistência; o HTTP 200 anterior não comprova commit e o heartbeat não demonstra relação causal. O código local atual protege `SaveImagesAsync` com stripe local e, em MariaDB, advisory `GET_LOCK`; isso não confirma qual versão produziu o log nem se todos os writers concorrentes cooperam.

Na árvore principal `main` em 25/10, suíte `MulletaFlix.sln` Release/.NET 10 passou 5.287, marcou 256 `NotExecuted`, falhou 0/5.543 em 17 assemblies; `LiveIntegrationTests` foi excluído. Focal `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests`: 40/40. Pytest Nebula não coletou: `main.py` aborta com runtime venv ausente; nenhuma dependência foi instalada no Python global. O único listener MariaDB 3306 é operacional e não foi acessado.

Conclusão: a regressão local é verde, mas o incidente operacional permanece sem causa confirmada. Próximo passo é correlacionar PID/versão/assembly do processo emissor e coletar evidência InnoDB/processlist enquanto ocorre; validar o assembly instalado e os writers ativos. Sem runtime/produção/CI Linux/NebulaPython comprovados, sem release e sem crédito para homologação formal (2/7, 28,6%).

## Regressão MariaDB cross-session após corrigir fixture — 2026-10-25 03:51 BRT

`ServerPersistenceMariaDbTests` passou 15/15, 0 ignorados/falhas, em MariaDB 11.4.4 descartável no loopback `127.0.0.1:33135`, Windows/.NET 10 Release. A primeira rodada expôs falha de fixture em `CleanupUserDataTask_WaitsForThePlaceholderAdvisoryLock`: inseria `UserData` sem a linha pai `User`. A correção é somente no teste: cria um usuário com ID correspondente e salva usuário e dados juntos; a suíte repetida passou. O MariaDB temporário foi encerrado e as portas fechadas; os quatro datadirs temporários foram removidos após validar os caminhos e que nenhum processo os usava. O DB operacional 3306 não foi acessado. Esta evidência valida a candidata local contra MariaDB, não CI Linux, runtime de produção ou associação do assembly ao log. Homologação formal continua 2/7 (28,6%), sem crédito.

## Revalidação da candidata sobre a base remota — 2026-10-25 03:44 BRT

O worktree `C:\Users\Raphael\.codex\worktrees\mfx-server-audit-20261024` está no commit remoto/tag `v12.1.35` (`d51f4f38`) e branch candidata `codex/server-image-deadlock-12.1.36`, com 21 caminhos modificados ou não rastreados. A suíte focal `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 40/40 em Windows/.NET 10 Release. Isso valida os casos unitários presentes nessa árvore, não um pacote publicado. A checagem de listeners encontrou apenas MariaDB em `127.0.0.1:3306`; nenhum teste foi conectado a esse serviço e MariaDB descartável cross-session não foi executado nesta rodada. DLL/runtime instalado, CI Linux e PID/assembly do log seguem sem correlação. Homologação formal continua 2/7 (28,6%), sem crédito.

## Reconciliação do artefato instalado — 2026-10-25 03:37 BRT

O anexo reenvia o incidente já visto: vários `DELETE BaseItemImageInfos` concorrentes para o mesmo item, deadlock MariaDB 1213 e retry; HTTP 200 antecedente não prova commit. No host local, a DLL instalada em `C:\Program Files\MulletaFlix\Server\MulletaFlix.Server.Implementations.dll` é 12.1.35 (SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`) e não contém o literal UTF-16 `mfx:item:`. Não havia processo MulletaFlix/Jellyfin ativo, logo não foi possível identificar assembly carregado nem associar essa DLL ao processo histórico do log.

A fonte local `SharedVersion.cs` declara 12.1.32; a DLL candidata Debug também é 12.1.32 e contém o literal do lock. `SaveImagesAsync_WaitsForSameItemPersistenceLockBeforeOpeningDatabaseContext` passou 1/1 em Debug, mas só valida espera pela trava intra-processo e não usa MariaDB. `git ls-remote` confirmou v12.1.35 como tag remota mais recente; checkout `main` em `64a33dcf`, `origin/main` em `d51f4f38`, com 154 caminhos modificados. Isso evidencia divergência entre artefatos e forte hipótese de instalação sem a correção; não prova qual assembly produziu o log. Banco operacional não foi acessado, e nenhum processo foi reiniciado ou atualizado. Homologação formal segue 2/7 (28,6%), sem crédito; release bloqueada até reconciliar mudanças na base atual, validar MariaDB descartável e limpar/revisar a árvore.

## Revalidação do log e da CI remota — 2026-10-24 21:19 BRT

O novo anexo repete `DELETE BaseItemImageInfos` concorrente, deadlock MariaDB 1213 e heartbeat Kestrel prolongado. HTTP 200 anterior não confirma commit. `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` foi recompilado em Windows/.NET 10 Release e passou 40/40. O registro MariaDB descartável 4/4 da sessão 20:54 é evidência anterior, não reexecutada agora. A API pública do GitHub retornou CI run 39608614450 e release run 39608677913 como `failure`; os 13 jobs da CI não têm runner (`runner_id=0`) nem steps, portanto nenhum teste remoto foi demonstrado. `origin/main` em `d51f4f38` não contém `ItemPersistenceDatabaseLock`; o checkout principal `64a33dcf` permanece 8 commits atrás e com alterações preexistentes. PID/assembly do log, runtime e produção seguem sem correlação. Homologação formal permanece 2/7 (28,6%), sem crédito; sem release ou publicação remota.

## Cleanup de advisory lock síncrono ambíguo — 2026-10-24 19:11 BRT

O `Acquire` síncrono liberava somente locks confirmados antes de uma exceção. Se o MariaDB obtivesse `GET_LOCK` mas a resposta se perdesse, o lock corrente podia permanecer preso à conexão. O cleanup sync/async agora compartilha `GetPotentiallyAcquiredLockCount` (`min(acquired + 1, requested)`). RED por helper ausente; após correção, focal passou 12/12. Solução candidata Release passou 5.142, ignorou 242, falhou 0/5.384 em 17 assemblies; `DramaFindsLiveIntegrationTests` foi excluído. MariaDB real, runtime e produção pendentes; sem integração/release.

## Suíte completa da solução candidata — 2026-10-24 19:00 BRT

Na worktree candidata `codex/server-image-deadlock-next`, baseada em `origin/main`/v12.1.35 SHA `d51f4f38`, a solução Release/.NET 10 passou 5.139 testes, ignorou 242 e falhou 0 de 5.381, em 17 assemblies; a execução fez restore e excluiu apenas `DramaFindsLiveIntegrationTests`, que chama API externa ao vivo. Dentro dela, foco de persistência passou 40/40 e `Jellyfin.Server.Implementations.Tests` passou 1.532, ignorou 65 e falhou 0 (1.597; 83 s). É evidência de regressão local, não integração à branch publicada. MariaDB não foi reexecutado porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` está ausente; 12/12 + 4/4 anteriores são históricos. Nenhuma conexão ao DB ativo, instalação ou release. O log permanece sem PID/assembly, CI/runtime/produção seguem sem validação e homologação formal permanece 2/7 (28,6%), sem crédito.

## Inventário estático de gravações de imagem — 2026-10-24 19:00 BRT

Busca nas fontes do servidor encontrou quatro pontos de alteração de `BaseItemImageInfos`: `SaveImagesCoreAsync`, `SaveBaseItemEntities` chamado sob `UpdateOrInsertItemsCore`, `DeleteItemsUnderLocks` e `FixBaseItemImageInfos`. Os três primeiros adquirem os stripes locais e o advisory lock MariaDB; FixDates adquire todos os 16 stripes no banco. Isso confirma cobertura estática dos call sites diretos encontrados, não prova concorrência real, ausência de caminhos indiretos de SQL/reflection ou integração em runtime. O teste MariaDB continua pendente.

## Reconciliação da branch pública com o patch candidato — 2026-10-24 18:35 BRT

Correção importante à leitura das entradas anteriores: `origin/main`/v12.1.35 (SHA `d51f4f38`) não contém `ItemPersistenceDatabaseLock` nem seus callers. A trava e os testes MariaDB existem numa árvore candidata local baseada nesse SHA, ainda sem commit/merge; o checkout primário está em `64a33dcf` (v12.1.31), oito commits atrás e com alterações preexistentes. Portanto, as verificações 40/40 deste checkout validam código local, não a release publicada. O teste descartável MariaDB 12/12 e foco 4/4 foi registrado numa sessão anterior, não reexecutado agora. Nenhuma conexão ao banco ativo, instalação ou publicação foi feita. A identificação do binário do log continua pendente.

## Revalidação do anexo repetido — 2026-10-24 18:23 BRT

O novo anexo repete o padrão já registrado: DELETEs concorrentes em `BaseItemImageInfos` para o mesmo item, deadlocks MariaDB 1213, retries e um heartbeat Kestrel de 2,35 s. O log não traz PID/assembly; HTTP 200 anterior não confirma commit. `SaveImagesAsync` no checkout usa stripe local e advisory `GET_LOCK`, compartilhados com `SaveItems`/`DeleteItem`; os testes focais Release continuam 40/40. Essa verificação não prova que o processo do log carregava a correção nem valida a instância MariaDB. Não tocamos no banco ativo. O trabalho desta sessão avançou o gate P0 separado de restore ACL, não a causa operacional desse log.

## Teste cross-session para exclusão de itens — 2026-10-25

A auditoria confirmou que `ItemPersistenceDatabaseLock.GetLockNames` deriva o nome do lock somente do nome do schema e do índice da stripe; conexões e processos diferentes no mesmo servidor/schema convergem ao mesmo lock. `SaveImagesAsync` já tem um teste que espera por um lock detido por outra sessão. Havia, porém, apenas teste de concorrência `DeleteItem` versus refresh dentro da mesma instância do serviço, que também é protegido pelo semáforo local. Foi adicionado `DeleteItem_WaitsForDatabaseAdvisoryLockHeldByAnotherSession` para cobrir a exclusão bloqueada por outra sessão e verificar ausência das linhas depois da liberação.

Build/teste Release do projeto `IntroSkipper.Integration.Tests` compilou com sucesso. Os três casos selecionados de advisory lock (`ItemPersistenceAdvisoryLock_BlocksAnotherDatabaseSession`, `SaveImagesAsync_WaitsForDatabaseAdvisoryLockHeldByAnotherSession` e o novo teste de exclusão) foram descobertos, mas ignorados porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada. O novo `DeleteItem_WaitsForDatabaseAdvisoryLockHeldByAnotherSession` foi incluído no filtro do job Ubuntu `user-mariadb-linux-tests`, que usa MariaDB descartável; contrato local foi atualizado, mas a execução hospedada ainda está pendente. Sem mudança em runtime, arquitetura ou dependências. O log anexado não contém PID/versão e não comprova que a instância ativa esteja usando a candidata. Formal 2/7, sem crédito; sem release ou publicação.

## Deadlock MariaDB: reexecução focal e isolamento do listener — 2026-10-24 15:38 BRT

`ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` foi recompilado e executado em Release no Windows/.NET 10: 40 aprovados, 0 ignorados, 0 falhas. A variável `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada. A checagem local encontrou um `mysqld.exe` do servidor MulletaFlix escutando em 3306; nenhuma conexão foi aberta, pois não foi possível comprovar que seja descartável. Os testes MariaDB de concorrência permanecem sem execução. O portal local registra o limite e não concede crédito; sua suíte passou 133/133, e JSON, JavaScript e `git diff --check` foram validados. O log anexado repete `1213 Deadlock` em exclusões de `BaseItemImageInfos`, mas não identifica PID/assembly nem comprova presença da trava na instância. Homologação formal permanece 2/7 (28,6%), sem crédito; sem alteração runtime, instalação ou release.

## Refresh troca capa conhecida em idioma não preferido por fallback inglês — 2026-10-23

- **Defeito confirmado:** o refresh só autorizava substituir imagem remota conhecida quando havia candidata no idioma preferido. Uma capa com proveniência `ko` (ou outro idioma terceiro) permanecia mesmo quando havia candidata em inglês e nenhuma em pt-BR/pt.
- **Correção local:** o tipo de imagem pode ser substituído se houver candidata pt-BR/pt; na ausência dela, pode usar candidata `en`, mas somente quando a imagem atual conhecida não é inglesa. Capa inglesa existente, imagem manual e imagem legada sem proveniência continuam preservadas.
- **Verificação:** `RefreshImages` passou 55/55 e o conjunto correlato `ItemImageProviderTests|ProviderManagerImageProviderTests|TmdbUtilsTests` passou 94/94 em Release no Windows/.NET 10. Nenhuma chamada real a TMDb/Fanart, refresh visual, runtime instalado, produção ou release foi feita.
- **Limites:** a correção se aplica ao refresh automático de imagens com proveniência conhecida; não identifica idioma em arquivos legados. A disponibilidade de arte inglesa continua dependente do provider para o título.

## Reauditoria do filtro de idioma da galeria — 2026-10-23

- **Evidência de código:** `TmdbClientManager.GetMovieImagesAsync` pede `pt,null,en` sem filtro restritivo de `language`; `ProviderManager.GetAvailableRemoteImages` aplica idioma preferido entre provedores e só expõe inglês/neutro como fallback quando não há imagem localizada. `RemoteImageController` usa `includeAllLanguages=false` por padrão. O seletor da galeria web só envia `IncludeAllLanguages=true` quando a caixa “Todos os idiomas” está marcada.
- **Confirmação externa:** a documentação oficial do TMDb diz que `language` filtra imagens e que `include_image_language` deve incluir idiomas adicionais; o servidor envia o idioma preferido, `null` e inglês. Fontes: [referência de imagens de filmes](https://developer.themoviedb.org/reference/movie-images) e [guia de idiomas de imagem](https://developer.themoviedb.org/docs/image-languages).
- **Conclusão limitada:** não foi encontrada falha determinística na seleção automática pelo código. A galeria manual pode listar inglês quando “Todos os idiomas” está marcado ou quando não há arte pt-BR/pt. A captura da galeria não prova qual imagem foi persistida nem que exista arte portuguesa para o título.
- **Pendência:** ainda precisa confirmar a caixa de idioma e executar refresh/renderização de *Invasão Zumbi 2: Península* no servidor instalado; sem isso, causa específica continua sem reprodução. Não houve alteração de código, consulta autenticada ao TMDb, gravação de imagens ou release.

## Rejeitar imagem remota sem decoder compatível — 2026-10-23

- **Causa confirmada:** o validador aceitava payload arbitrário com MIME `image/*` se o encoder estivesse desativado ou não suportasse o formato. Os testes antigos afirmavam essa aceitação para SVG/TIFF e encoder ausente.
- **Correção local:** payload remoto só passa ao `ImageSaver` depois de validação completa pelo decoder configurado. Formatos sem decoder retornam HTTP 415; bytes inválidos em formatos suportados retornam HTTP 422. O limite de 64 MiB e a limpeza do staging permanecem ativos; `ItemImageProvider` trata `HttpRequestException` e tenta o próximo candidato. Imagem previamente persistida não é substituída por payload rejeitado.
- **Verificação:** regressões focadas `RemoteImagePayloadValidatorTests`, `ProviderManagerTests` e `ItemImageProviderRefreshTests` passaram 85/85; a suíte `Jellyfin.Providers.Tests` excluindo `Integration` passou 844/844. Execução sem filtro foi abortada pelo watchdog após 746 aprovações no teste `DramaBoxLiveIntegrationTests.RebuildIndex_FindsTheRealCatalogueAndMatchesRealLibraryTitles`; total geral desconhecido. Esse teste faz crawl real de catálogo externo e não foi reexecutado isoladamente.
- **Limites:** sem provider/CDN real, runtime instalado, refresh visual de *Invasão Zumbi 2*, Linux, instalação ou produção. SVG e formatos não suportados pelo decoder são rejeitados para que o provedor procure alternativa. Homologação formal não recebe crédito por teste local; sem release.

## Refresh aplica preferência global efetiva às capas remotas antigas — 2026-10-23

- **Defeito reproduzido:** quando item e opções da biblioteca não definiam idioma, o refresh considerava vazia a preferência para detectar capa remota desatualizada, embora a busca do `ProviderManager` herdasse pt-BR da configuração global. Assim, uma capa em inglês conhecida podia impedir a consulta/substituição localizada.
- **Correção local:** `ItemImageProvider` agora resolve o idioma efetivo por `BaseItem.GetPreferredMetadataLanguage()` quando há imagem singular com provedor e idioma conhecidos; a chamada permanece lazy para não exigir o contexto global nos casos sem proveniência remota. Isso mantém capas manuais e antigas sem proveniência fora da substituição automática. Inglês continua fallback quando não há candidato pt/pt-BR.
- **Regressão:** o teste com configuração global pt-BR falhou antes da mudança e passou depois. Suíte focada `ItemImageProviderTests.RefreshImages` passou 40/40; seleção/provedores/TMDb (`ItemImageProviderTests|ProviderManagerImageProviderTests|TmdbUtilsTests`) passou 86/86 em Release no Windows/.NET 10.
- **Limites:** não houve chamada ao TMDb, refresh visual de *Invasão Zumbi 2*, alteração de imagens persistidas, instalação ou teste em produção. A fonte/testes deste checkout estão em uma árvore local ampla e suja, 8 commits atrás de `origin/main`; nenhuma release foi criada. Homologação formal permanece 1/7 (14,3%).

## Seleção automática respeita o idioma em todos os provedores — 2026-10-22

- **Causa confirmada:** o refresh automático consultava um provedor por vez e solicitava todos os idiomas; inglês de um provedor podia ser gravado antes de português oferecido por outro. Além disso, a filtragem global por idioma podia descartar um backdrop em inglês por existir apenas um poster em português.
- **Correção local:** `ItemImageProvider` agora busca candidatos remotos em uma chamada agregada, com idiomas preferidos, e encaminha cada imagem ao provedor correspondente. `ProviderManager` aplica fallback por `ImageType`: quando existe candidato pt/pt-BR para aquele tipo, remove outros idiomas; para tipos sem correspondência, preserva alternativas/fallback inglês. Consultas explícitas de todos os idiomas continuam sem filtro.
- **Regressões:** `ProviderManagerTests` valida filtro agregado entre provedores e por tipo (75/75); `ItemImageProviderTests.RefreshImages` valida que o refresh agrega uma consulta e encaminha o candidato localizado ao provider correto (36/36). `GetAvailableRemoteImages` passou 4/4; fallback de backdrop inglês após poster pt é coberto separadamente. Nome de provider ausente/vazio é normalizado para permitir despacho.
- **Limites:** sem consulta autenticada ao TMDb, refresh visual de *Invasão Zumbi 2: Península*, instalação ou validação em produção. Arte persistida não é substituída automaticamente; após instalar, os títulos afetados precisam de refresh/substituição. Fanart pode ser plugin externo e não foi encontrado neste checkout.

## Proveniência persistida e refresh localizado seguro — 2026-10-22

- `ItemImageInfo` e `BaseItemImageInfo` agora carregam `ProviderName` e `Language`; `BaseItemMapper` mantém esses campos ao mapear domínio↔MariaDB. A migration `AddRemoteImageProvenance` adiciona colunas nullable com limites de 255/32 caracteres, sem classificar nem alterar imagens antigas.
- `ItemImageProvider` grava proveniência em imagens remotas baixadas ou registradas como stub. No refresh normal, uma imagem singular só é elegível para correção automática quando os dois campos de origem/idioma estão presentes e existe uma candidata no idioma preferido para o mesmo tipo; idioma inglês continua intacto se a candidata localizada não existir. Imagens antigas sem proveniência permanecem intocadas.
- `MetadataService.SaveRemoteResultImages` também grava provedor e idioma do resultado na imagem persistida, para que novos downloads pelo fluxo de metadados participem do refresh seguro futuro.
- Testes focais Windows/.NET 10 Release: `ItemImageProviderTests|ProviderManagerImageProviderTests|MetadataServiceTests` (103/103, incluindo persistência do provedor/idioma pelo fluxo de metadados, preferência, fallback, arte sem proveniência e limpeza de origem ao substituir manualmente); `BaseItemMapperImageTests` (1/1). Build `Jellyfin.Server.Implementations` Debug passou. A migration EF foi gerada e o script de migração compilou; nenhuma base MariaDB foi atualizada.
- **Limites:** imagens anteriores à migration mantêm `ProviderName`/`Language` nulos e não são inferidas por URL/nome: refresh automático seguro delas continua indisponível até uma substituição explícita pelo administrador. Não houve TMDb ao vivo, teste visual/produção nem update de bibliotecas. Homologação formal continua sem crédito local.

## Fallback automático restrito a inglês — 2026-10-22

- A política declarada pelo usuário é pt-BR primeiro e inglês como fallback. O filtro automático anterior retornava qualquer idioma quando não existia arte portuguesa para um tipo; ordenar inglês acima de outros idiomas não impedia uma capa coreana, espanhola ou japonesa de ser escolhida.
- `ProviderManager.GetAvailableRemoteImages` agora mantém somente pt/pt-BR quando houver arte localizada para o tipo. Isso exclui também candidatos sem idioma nesse tipo: como o refresh despacha por ordem de provider, uma imagem não rotulada poderia ser gravada antes da portuguesa. Se não houver, mantém inglês e imagens sem idioma. A opção manual `IncludeAllLanguages=true` continua sem filtro.
- Regressões cobrem candidato `ko` e não rotulado concorrendo com português, além de candidatos `ko`, `en` e sem idioma no fallback. `GetAvailableRemoteImages_*` passou 4/4 e o grupo completo `ProviderManagerTests` passou 75/75 em Release; `dotnet format style ... --verify-no-changes` e `git diff --check` passaram.
- Sem chamadas reais ao TMDb/Fanart, refresh, inspeção visual, runtime instalado ou produção. Imagens persistidas não mudam automaticamente; arte atual requer refresh/substituição. Fanart externo permanece fora da cobertura confirmada.

### Revisão adversarial e decisão sobre imagens sem idioma — 2026-10-22

- Revisão independente observou que imagens sem idioma podem ser neutras, como fundos sem texto, e sugeriu mantê-las junto de pt. A inspeção do fluxo mostrou que ordenar o resultado agregado não basta: `ItemImageProvider` despacha por provedor e a ordem configurada pode fazer a imagem neutra ser salva antes da portuguesa. Mantivemos o filtro estrito quando existe pt/pt-BR para aquele tipo; neutras são fallback apenas quando não há imagem localizada. Essa decisão prioriza consistência de idioma da capa sobre disponibilidade de fundos neutros.
- O fallback de backdrop em inglês continua coberto separadamente quando não há backdrop pt. `IncludeAllLanguages=true` permanece como opção manual para consultar todos.
- Sem acesso autenticado à API, plugin Fanart externo, refresh visual ou runtime de produção. Artes já gravadas não mudam por esta alteração.

## Preferência pt-BR aceita imagens TMDb/Fanart marcadas apenas como pt — 2026-10-22

- **Causa confirmada no filtro local:** `ProviderManager` comparava `PreferredMetadataLanguage` por igualdade textual. `pt-BR` e `pt` são idiomas equivalentes para este filtro de imagens, mas eram tratados como diferentes; em `IncludeAllLanguages=false` a arte `pt` podia ser removida e o ordenamento favorecia inglês.
- **Correção local:** `MediaBrowser.Model.Extensions.EnumerableExtensions.MatchesLanguage` compara códigos ISO 639-1 e variantes regionais. O `ProviderManager` mantém `pt`/`pt-BR`, prioriza a correspondência exata, depois idioma-base, inglês, sem idioma e outras línguas. O inglês continua como fallback.
- **Regressões:** `EnumerableExtensionsTests` 7/7; `ProviderManagerTests` 73/73, incluindo filtro com `pt-BR`, arte `pt`, fallback `en`, sem idioma e exclusão de idioma não solicitado. Não houve chamada externa.
- **Limites:** a imagem de *Invasão Zumbi 2* ainda precisa de consulta autenticada/refresh e conferência visual para confirmar que TMDb oferece arte `pt`. A grade do screenshot mistura candidatos Fanart e TMDb; Fanart não foi localizado no código versionado e pode ser plugin externo. Imagens já persistidas exigem refresh após release; nenhuma release/instalação/produção foi feita nesta sessão.

## Reconsulta completa de idiomas de arte TMDb — 2026-10-22

- **Causa confirmada:** os endpoints de imagens de filmes eram chamados com `include_image_language` vazio. Remover o filtro não solicita automaticamente todos os idiomas: a documentação TMDb orienta informar idiomas adicionais explicitamente. A API também não suporta variantes regionais no filtro de imagens; `pt-BR` precisa ser enviado como `pt`. Por isso, a correção anterior liberava a filtragem local, mas a resposta remota ainda podia conter apenas inglês/sem idioma.
- **Correção local:** `TmdbClientManager` consulta `/configuration/languages`, mantém a lista em cache e solicita todas as línguas ISO 639-1 suportadas para imagens de filmes. `TmdbUtils` coloca primeiro português (pt), idioma original e inglês, deduplica códigos e inclui `null`; `ProviderManager` continua ordenando candidatos pela preferência do servidor. Se a lista de configuração falhar, a consulta usa idioma preferido, original, inglês e `null`, com fallback cacheado por cinco minutos.
- **Teste local:** `TmdbUtilsTests` verifica `pt-BR` → `pt`, inclusão de idioma original, catálogo completo, deduplicação e fallback. O teste existente do `ProviderManager` verifica que `pt-BR` ordena antes de inglês.
- **Limites:** sem chamada autenticada ao TMDb, refresh ou inspeção visual de *Invasão Zumbi 2*, runtime instalado, produção ou release. A alteração afeta novas consultas. Capas já persistidas não mudam sem atualização/substituição de imagens. A política de todos os idiomas foi aplicada ao provider de imagens de filmes; demais providers devem ser validados separadamente. Opções Fanart seguem externas/não encontradas neste checkout.
- **Referências primárias:** [TMDb image languages](https://developer.themoviedb.org/docs/image-languages), [TMDb movie images](https://developer.themoviedb.org/reference/movie-images), documentação local TMDbLib 3.0.0 em `%USERPROFILE%\.nuget\packages\tmdblib\3.0.0\lib\net10.0\TMDbLib.xml`.

## Idioma das imagens TMDb — 2026-10-22

- **Revisão de causa em 2026-10-22:** a hipótese anterior de que omitir filtros retornaria todos os idiomas não era correta. A documentação oficial TMDb orienta enviar `include_image_language` explicitamente. Remover o filtro só permite defaults da API e não garante que posters PT estejam na resposta. A captura de *Invasão Zumbi 2* lista opções `en`, mas não identifica qual imagem foi persistida.
- **Correção local para filmes:** `TmdbClientManager` consulta `/configuration/languages` (cacheado), transforma códigos para ISO 639-1, prioriza idioma preferido do servidor (pt-BR → pt), idioma original e en, acrescenta demais códigos disponíveis e `null`, e usa a lista na chamada de imagens de filme e na chave de cache. Se a consulta de idiomas falhar, usa pt/original/en/null com fallback cacheado por cinco minutos. `ProviderManager` continua ordenando candidatas pela preferência local.
- **Outros escopos ainda não cobertos pela consulta explícita:** `TmdbSeriesImageProvider` e `TmdbSeasonImageProvider` ainda usam consultas sem lista explícita de idiomas; coleções passam `null`; episódios preservam o fluxo de detalhe/ordem de exibição. Esta correção não declara séries, temporadas, coleções, episódios nem o provedor Fanart resolvidos.
- **Verificação local nesta continuação:** filtro Release `FullyQualifiedName~TmdbUtilsTests|FullyQualifiedName~ProviderManagerImageProviderTests` passou 22/22, 0 ignorados/falhas; `git diff --check` passou. Sem captura HTTP automatizada do parâmetro, chamada real/autenticada TMDb, refresh do item, instalação ou produção.
- **Limites:** imagens já persistidas não mudam automaticamente; após instalar release corrigida, será necessário atualizar/substituir artes dos títulos afetados. Fanart não foi encontrado neste checkout. Sem release nesta tarefa.
- A captura também mostra candidatos do provedor Fanart. Não foi encontrada implementação Fanart neste checkout pesquisado; a origem pode ser um plugin externo/não versionado. Portanto esta correção cobre somente providers TMDb e não confirma nem corrige o comportamento das opções Fanart.
- Referências: [TMDb image languages](https://developer.themoviedb.org/docs/image-languages) explica `include_image_language` e ausência de variantes regionais; [TMDb movie images](https://developer.themoviedb.org/reference/movie-images) documenta o endpoint. O manager existente ganhou consulta/cache de configuração; sem endpoint HTTP novo do MulletaFlix, dependência ou serviço.
- Homologação no portal publicada em `Mulletaflix-Portal:main`, commit `8472e0f5d2ea0b0d10717a3c8ef49b2cebca65fd`; contratos passaram 231/231, JSON válido, Vercel Production `READY` no deployment `dpl_A7AvwcjaZ7YbhdKcsV3xGpJz9nrS`, e `/homologacao-status.json` canônico confirmou HTTP 200/evento. Homologação formal continua 1/7 (14,3%); sem release do servidor.

## Disponibilidade real de pôster pt-BR — 2026-10-22

- Reexecutado em Windows/.NET 10 Release: `dotnet test tests/Jellyfin.Providers.Tests/Jellyfin.Providers.Tests.csproj --no-restore --configuration Release --filter "FullyQualifiedName~TmdbUtilsTests|FullyQualifiedName~ProviderManagerImageProviderTests"` passou 22/22, 0 ignorados/falhas. Os testes cobrem `pt-BR` → `pt`, ordenação/filtro e fallback inglês quando a fonte não disponibiliza imagem preferida; não fazem requisição ao TMDb real.
- A captura de *Invasão Zumbi 2: Península* mostra cartões marcados `en`, mas não mostra o estado do checkbox “Todos os idiomas”; portanto não identifica se o filtro estava ligado nem prova ausência de candidatos `pt` na resposta. A implementação consultada pede explicitamente `pt` junto ao catálogo de idiomas; o seletor de imagem pode conservar inglês quando não há candidato localizado. A documentação oficial do TMDb confirma que `include_image_language` limita idiomas de imagens, não traduz ou cria artes ausentes.
- Pesquisa pública encontrou pôsteres brasileiros em páginas editoriais (Jovem Nerd e AdoroCinema), fora dos provedores TMDb/Fanart identificados na captura. Isso demonstra existência de arte localizada externa, mas não prova que seja licenciada/tecnicamente disponível para integração; nenhum scraping, download ou import foi feito.
- Conclusão factual: consulta e ranking locais passam os testes, mas disponibilidade/cobertura por título e arte atualmente persistida continuam sem validação. Decisão do usuário em 2026-10-22: manter arte inglesa como fallback quando não houver pt-BR; arte localizada continua tendo prioridade. Avaliar fonte adicional somente com API/licença e contrato estáveis. A regra atual é compatível com a decisão, sem alteração de runtime nesta sessão. Para concluir a causa do screenshot, observar uma consulta real com `IncludeAllLanguages=false` e os candidatos retornados para o item identificado.
- Sem código, runtime, biblioteca, instalação, produção ou release alterados. Homologação formal servidor permanece 1/7 (14,3%). Referências: [TMDb movie images](https://developer.themoviedb.org/reference/movie-images), [Jovem Nerd](https://jovemnerd.com.br/noticias/filmes/invasao-zumbi-2-peninsula-ganha-dois-novos-posteres), [AdoroCinema](https://www.adorocinema.com/filmes/filme-266875/fotos/).

## Cobertura explícita de idiomas nos providers de TV — 2026-10-22

- **Correção na fonte:** `TmdbClientManager` centraliza a consulta/cache de `/configuration/languages` e envia `include_image_language` nas imagens de séries, temporadas e episódios, com prioridade pt-BR→pt, idioma original, en, todos os idiomas suportados e `null`. Falha ao consultar o catálogo mantém fallback preferred/original/en/null por cinco minutos.
- As chaves de cache de séries e temporadas agora incluem a lista de idiomas; episódios usam a chave já parametrizada por idioma, lista, país e ordem. Providers de temporada/episódio herdam o idioma original da série. Overloads antigos continuam usando a mesma busca abrangente, para não regredir chamadores internos.
- Regressões Release: `TmdbUtilsTests` 25/25; compilação do projeto `Jellyfin.Providers` ocorreu como parte dos testes. Três casos novos provam que alterações na lista linguística isolam o cache de séries, temporada e episódio. Avisos de estilo preexistentes foram emitidos em outros arquivos.
- Coleções continuam sem `include_image_language`: a assinatura local de TMDbLib para coleção só aceita `language`, e `null` é o parâmetro que evita limitar o conjunto. Sem chamada autenticada/live TMDb, refresh ou inspeção visual de mídias; artes já persistidas podem precisar ser atualizadas. Não houve instalação/produção nem release. Homologação formal continua 1/7 (14,3%).

Data: 2026-10-18

## Sintoma

Títulos recebem metadados textuais, mas parte dos posters permanece ausente (placeholder azul); também há relato de capas sendo substituídas por outra.

## Fluxo verificado

1. Os metadata providers retornam `MetadataResult.RemoteImages` junto dos metadados.
2. `MetadataService.RefreshWithProviders` busca resultados dos providers em paralelo, mas os mescla e salva imagens em ordem, sequencialmente.
3. `SaveRemoteResultImages` filtra imagens por tipo e existência de `ImageInfo`, faz download e encaminha para `ProviderManager.SaveImage`.
4. `ImageSaver` escolhe o destino físico, escreve o arquivo e só então atualiza o caminho registrado no item.
5. Em uma etapa separada, `ItemImageProvider.RefreshImages` busca artes adicionais. `BaseItem.ValidateImages` remove referências locais a arquivos inexistentes antes da atualização normal.

## Achados comprovados na fonte

- **Sobrescrita de imagem singular (corrigida na fonte):** `SaveRemoteResultImages` e a importação de `RemoteImages` de provedores locais agora compartilham o conjunto de tipos singulares já salvos. Tentam candidatas em ordem e param na primeira gravação bem-sucedida, inclusive entre provedores; tentam a próxima se a anterior falhar. A exceção preexistente permanece: fonte identificada pode substituir uma capa se `IsReplacingImage` foi explicitamente solicitado. Tipos que aceitam múltiplas imagens continuam salvando todas. Antes da correção, múltiplas entradas `Primary` podiam gravar no mesmo destino (índice 0); OpenLibrary possui caminhos que agregam mais de uma capa `Primary`.
- **Falha de arte refletida no resultado:** falhas singulares ficam pendentes até terminar a sequência local/remota e não marcam o refresh como falho se outra fonte salvar a capa. Falhas finais de download remoto incrementam `RefreshResult.Failures`; falhas de imagens múltiplas permanecem parciais por candidata. O cancelamento solicitado é propagado e logs não expõem URLs potencialmente assinadas. Metadados textuais válidos continuam podendo ser mesclados independentemente da capa.
- **Caminho alternativo de imagens:** `ItemImageProvider` é executado separadamente para providers de imagem. Falha HTTP ali incrementa `Failures`, mas resposta não-2xx interrompe tentativas restantes daquele provider. Sem log e estado de `ImageInfos` por título, não dá para estabelecer se esse fallback foi acionado ou por que cada arquivo de imagem está ausente.

## Evidência e limites

- Caminhos examinados: `MediaBrowser.Providers/Manager/MetadataService.cs`, `ProviderManager.cs`, `ImageSaver.cs`, `ItemImageProvider.cs`, `MediaBrowser.Controller/Entities/BaseItem.cs` e providers de livros OpenLibrary.
- Não havia log de produção atual, registros de `ImageInfos`, resposta de endpoint de imagem ou amostra dos arquivos em disco associada aos títulos do screenshot. Portanto a relação com todos os placeholders exibidos é hipótese compatível, não causa de produção demonstrada.
- Nenhum item, arquivo de imagem, serviço, biblioteca ou estado produtivo foi modificado nesta auditoria.

## Próximas verificações/correções propostas

- **Implementado na fonte:** salvamento singular limitado à primeira candidata bem-sucedida no refresh inteiro, incluindo provedores locais e remotos; falhas só são consolidadas depois dos fallbacks; a fonte identificada pode substituir uma vez quando isso foi solicitado explicitamente, sem aceitar outras capas Primary do mesmo resultado; sem limitar tipos múltiplos.
- **Regressões adicionadas:** duas capas `Primary` não se sobrescrevem; fallback dentro do provider e entre providers; fonte identificada ainda substitui capa quando pedido explicitamente; falha final de todas as capas `Primary` fica visível; `IOException` local é coberto; múltiplos `Backdrop` continuam sendo salvos; estado compartilhado incompleto é rejeitado.
- Capturar para um título afetado: provider e URL sem credencial, status HTTP/content-type, destino calculado, existência/tamanho do arquivo após gravação, `ImageInfos`/`ImageTags` e status do endpoint de imagem. Não registrar tokens ou URLs assinadas.
- Homologação isolada com item real ainda necessária para confirmar arquivo salvo, `ImageInfos`/`ImageTags`, endpoint e renderização visual. Não houve correção de itens em produção; o screenshot continua sem causa produtiva individual confirmada.

## Validação geral da sessão

- Regressões focadas do `MetadataServiceTests`: 37/37 aprovadas em Release; suíte integral `Jellyfin.Providers.Tests`: 798/798 aprovada em Release (reexecutada após o último ajuste, 3m23s).
- O log anexado em 18/10 para `A Era de Miyu - S01E06.strm` contém avisos do Intro Skipper (“Chromaprint returned 0 points”) e falha do passe `ChangedItems`, com EF Core/LINQ tentando avaliar `ReadOnlySpan<AnalysisMode>`. Não contém erro de download/carregamento de imagem; é pendência separada do plugin, não validada nem corrigida aqui.
- Não declarar resultado agregado de solução completa nesta sessão: sem total consolidado final verificável. Testes locais não provam arquivo de imagem real, endpoint ou renderização em produção. Sem evidência para atribuir todos os placeholders do screenshot ao defeito de fonte.

## Fallback global de idioma para a galeria remota — 2026-10-22

- **Causa reproduzida por teste:** o `ProviderManager` filtrava cada provedor individualmente e deixava inglês como fallback de cada um. Assim, artes `en` do Fanart/TMDb continuavam na galeria padrão mesmo quando outro provedor já oferecia uma arte `pt`/`pt-BR`.
- **Correção local:** a decisão agora ocorre depois de combinar os resultados de todos os provedores. Se existir ao menos uma imagem no idioma preferido, a consulta padrão mantém imagens no idioma preferido e imagens sem idioma (sem texto), removendo opções inglesas e de outras línguas. Se não houver arte no idioma preferido, inglês continua disponível como fallback. `IncludeAllLanguages=true` continua incluindo todas as línguas.
- **Testes:** três testes Release focais passaram (todos os idiomas; português em um provedor suprime inglês de outro; fallback inglês quando não há português) e o grupo `ProviderManagerTests` passou 74/74. `dotnet format ... --verify-no-changes` e `git diff --check` passaram. A suíte ampla Providers foi iniciada, mas interrompida após permanecer sem resultado; não conta como aprovada.
- **Limites:** sem chave/chamada autenticada TMDb, refresh nem conferência visual de *Invasão Zumbi 2: Península*. A captura mostra candidatos em inglês, não prova qual arquivo foi persistido. Imagens já salvas não são substituídas por esta alteração até atualização/substituição da arte. Fanart pode ser plugin externo.

## Retry de falhas HTTP de imagens — 2026-10-19

- `SaveRemoteResultImages` agora repete apenas falhas transitórias de transporte (sem status HTTP), 408, 429 e 5xx. Erros permanentes como 404 seguem imediatamente para a próxima candidata, sem consumir as tentativas de retry.
- Regressões focalizadas: 44/44 aprovadas em `MetadataServiceTests` Release, incluindo retries transitórios e fallback após 404. Na suíte Providers completa, 801/802 passaram; a única falha foi `DramaFindsLiveIntegrationTests.SearchAndDetail_WorkAgainstTheRealApi`, uma asserção contra API ao vivo não relacionada à alteração de imagens. A validação global não está totalmente verde.
- Auditoria de duplicatas: busca por outros `HttpRequestException` usados em loops de retry não encontrou outro local equivalente; `ItemImageProvider` contém catches separados que não implementam retry.
- O ajuste não prova que imagens específicas em produção foram baixadas/renderizadas; homologação visual com mídia real continua pendente.

## Fallback nos loops alternativos de imagens — 2026-10-19

- `ItemImageProvider.DownloadImage` (imagens singulares) e `DownloadMultiImages` (backdrops) são fluxos separados de `MetadataService.SaveRemoteResultImages`. Antes desta correção, HTTP não-2xx inesperado e `HttpRequestException` interrompiam o loop no primeiro candidato em ambos os fluxos.
- Ambos agora tentam as URLs seguintes. Primary representa uma única capa: falha transitória não é mantida se alternativa salva. Backdrop contém imagens distintas: falha parcial continua contabilizada mesmo quando as demais são baixadas.
- Regressões Release: 51/51 em `ItemImageProviderTests`; cenário novo cobre Primary e Backdrop para HTTP 502 e `HttpRequestException`, confirmando que a URL seguinte é tentada e salva e que falha de Backdrop permanece visível como parcial. Teste existente mantém cobertura de falha sem alternativa. A compilação em Release mostrou avisos StyleCop preexistentes noutros arquivos de teste.
- Twin search pelo antigo `skipping all remaining requests`: nenhum outro loop remoto desse provider mantém o `break` após resposta não-2xx. Isso valida lógica unitária; ainda não prova gravação física, `ImageInfos`/`ImageTags`, endpoint ou renderização em produção. Nenhum servidor/serviço foi instalado ou reiniciado.

## Revalidação local das regressões de imagens — 2026-10-20

- `dotnet test tests/Jellyfin.Providers.Tests/Jellyfin.Providers.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~MetadataServiceTests|FullyQualifiedName~ItemImageProviderTests'` passou: 95 aprovados, 0 ignorados/falhas.
- `dotnet test tests/Jellyfin.Providers.Tests/Jellyfin.Providers.Tests.csproj -c Release --no-restore --no-build --filter 'FullyQualifiedName!~DramaFindsLiveIntegrationTests'` passou: 805 aprovados, 0 ignorados/falhas, 3m27s. O teste que depende da API real foi excluído deliberadamente; o resultado não representa toda a suíte Providers.
- Nenhum arquivo de mídia, registro `ImageInfos`/`ImageTags`, endpoint, biblioteca, instalação ou produção foi acessado/modificado. Os testes provam fallback/regra de persistência sob mocks, não download físico ou renderização de capa real.
- Próximo gate: executar refresh numa biblioteca isolada com um título autorizado e verificar resposta HTTP, arquivo/tamanho, `ImageInfos`/`ImageTags` e imagem renderizada. Não inferir que os placeholders de produção foram corrigidos sem essa evidência. Sem alteração runtime, sem release.

## Validação dos bytes de imagens remotas — 2026-10-21

- **Correção implementada:** novo `RemoteImagePayloadValidator` é chamado pelos três overloads de `ProviderManager.SaveImage(BaseItem, ...)`, compartilhados pelos fluxos de `MetadataService` e `ItemImageProvider`. A resposta precisa ser staged antes de chegar ao `ImageSaver`; decoder compatível exige decodificação completa, e payload inválido lança `HttpRequestException` para manter o fallback de candidato.
- **Limites e compatibilidade:** tamanho máximo de 64 MiB sempre aplicado; decoder limita a 16.777.216 pixels; duas decodificações e quatro downloads URL em paralelo; cache de URL usa o buffer de `MemoryStream` sem `ToArray()` e só mantém respostas até 8 MiB. Sem encoder ou formato não suportado, os bytes ainda são staged e limitados, mas não validados por pixels. SVG é ignorado pelo decoder e permanece sem validação de conteúdo.
- **Proteção de integridade:** teste de ponta a ponta pelo `ProviderManager` confirma que JPEG inválido não altera arquivo nem `ImageInfos` preexistentes. Staging é removido ao fechar `FileStream` com `DeleteOnClose`, inclusive depois do salvamento.
- **Testes:** novas regressões validam JPEG válido/inválido, fallback de decoder/formatos não suportados, bytes acima do limite, limpeza de staging, falha ao criar diretório temporário e limite por `Content-Length`. Os testes focais atuais passaram 177/177; a suíte Providers excluindo a integração externa ao vivo passou 817/817. O build Release do `Jellyfin.Server` passou com 0 erros e um aviso CS0419 preexistente.
- **Limites de homologação:** testes usam encoder simulado e arquivo de fixture; nenhuma CDN/provider, biblioteca, runtime instalado, produção ou mídia real foi exercitada. Formatos SVG e sem suporte a decoder não são decodificados. O workflow do GitHub ainda precisa rodar após a publicação; em 12.1.26 a conta estava bloqueada por cobrança.

## Idioma das imagens TMDb e abertura concorrente do staging — 2026-10-21

- **Causa comprovada para capas em idioma inesperado:** `TmdbMovieImageProvider`, `TmdbSeriesImageProvider`, `TmdbSeasonImageProvider`, `TmdbEpisodeImageProvider` e `TmdbBoxSetImageProvider` tinham idioma/região disponíveis, mas consultavam TMDb com parâmetros de idioma/imagens nulos. O filtro posterior do `ProviderManager` não conseguia escolher imagens preferidas que a API nem havia sido instruída a priorizar.
- **Correção na fonte:** os cinco providers agora passam idioma e país preferidos, mais a lista de imagens preferidas + sem idioma + inglês. O cache de filmes, séries, temporadas, episódios e coleções inclui idioma de texto normalizado, idiomas de imagem e país; a mesma entidade não reutiliza a resposta montada para outra região.
- **Falha de download confirmada pelo log de execução:** `RemoteImagePayloadValidator.PrepareAsync` falhou ao reabrir o JPG staged com `IOException` de compartilhamento do Windows. O bloqueio por qual processo não ficou identificado; atribuí-lo ao antivírus/indexador é hipótese, não fato. A abertura agora repete somente os códigos de compartilhamento/bloqueio 32/33, em esperas limitadas de 50/100/200 ms; outros erros continuam falhando imediatamente.
- **Regressões:** imagem TMDb pt-BR com fallbacks; chaves de cache distintas por país/idiomas; abertura após bloqueio exclusivo temporário do Windows; validação e fallback prévios do staging. `TmdbUtilsTests`, `RemoteImagePayloadValidatorTests` e `ProviderManagerRemoteImageValidationTests` passaram 28/28; suíte `Jellyfin.Providers.Tests` sem `LiveIntegrationTests` passou 808/808 em Release.
- **Twin search:** a busca pelos métodos de obtenção de imagem encontrou cinco providers com idioma de imagem nulo (todos corrigidos). `TmdbPersonImageProvider` usa `GetPersonAsync`, sem parâmetro de idioma de imagem nesta abstração, portanto não reproduz o mesmo defeito de capas/telas de títulos. Não foi encontrado outro re-open de staging remoto com política de retry concorrente.
- **Limites:** testes não chamam a API real do TMDb, não reprocessam capas já salvas e não verificam mídia renderizada. Títulos com arte antiga precisarão de atualização/refresh após implantação; configuração efetiva de idioma/região por servidor/biblioteca/item permanece a fonte de preferência. A correção ainda não foi instalada nem observada em produção nesta sessão.
