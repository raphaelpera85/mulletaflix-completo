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

## Revalidação da seleção pt-BR e fallback inglês — 2026-10-22

- O fluxo de refresh agrega resultados remotos antes de escolher a imagem, filtra por `ImageType` e prefere a língua configurada. `MatchesLanguage` faz `pt-BR` corresponder a `pt`; inglês permanece candidato apenas quando não há imagem no idioma preferido para aquele tipo.
- `dotnet test tests/Jellyfin.Providers.Tests/Jellyfin.Providers.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~Manager.ProviderManagerTests|FullyQualifiedName~Manager.ItemImageProviderTests|FullyQualifiedName~TmdbUtilsTests'` passou 154/154; `Jellyfin.Model.Tests` com `EnumerableExtensionsTests` passou 7/7.
- Isso valida ranking/filtro com fixtures, não a resposta real do TMDb nem o pôster persistido de *Invasão Zumbi 2*. Capas existentes podem requerer substituição/FullRefresh após instalar versão corrigida. Decisão do usuário: manter inglês como fallback. Fanart não foi localizada no checkout; homologação formal permanece 1/7, sem crédito por testes unitários.

## Filtro de idiomas de imagens TMDb — 2026-10-22

- **Decisão confirmada pelo usuário:** usar o idioma preferido do servidor e manter inglês como fallback; não incluir arte em todos os idiomas nem forçar o idioma original do filme.
- **Correção local:** consultas de imagens de filmes, séries, temporadas e episódios enviam `include_image_language` como idioma preferido ISO 639-1, `null` e `en` (por exemplo, `pt-BR` vira `pt,null,en`). Isso evita opções de idiomas não solicitados e remove a chamada extra a `/configuration/languages`, que não era necessária para aplicar a política escolhida.
- **Regressões:** `TmdbUtilsTests` e `ProviderManagerImageProviderTests` passaram 25/25 em Release, cobrindo a lista `pt,null,en`, exclusão de outros idiomas e a seleção de português com fallback inglês. `dotnet format whitespace` e `git diff --check` passaram.
- **Limites:** não houve chamada real ao TMDb nem refresh/conferência visual de *Invasão Zumbi 2*; arte já persistida requer substituição/refresh depois da instalação. Fanart não foi localizada no checkout e pode pertencer a plugin externo. Sem instalação, produção ou release.

## Default pt-BR/BR para novas configurações — 2026-10-22

- `ServerConfiguration` agora inicia novas configurações em `pt-BR`/`BR`; consultas TMDb para imagens preservam `null` e inglês como fallback (`pt,null,en`). Preferências já persistidas não são migradas e capas existentes não são substituídas por este default.
- Testes focais Release: `TmdbUtilsTests`, `ProviderManagerImageProviderTests` e `ItemImageProviderRefreshTests`, 26 aprovados, 0 falhas/ignorados. `git diff --check` passou.
- Sem chamada TMDb ao vivo, instalação, biblioteca ou produção; o teste valida apenas configuração e seleção local. Homologação formal permanece 1/7 (14,3%); nenhuma release criada.

### Reexecução focal — 2026-10-22

- `dotnet test tests/Jellyfin.Providers.Tests/Jellyfin.Providers.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Manager.ProviderManagerTests|FullyQualifiedName~Manager.ItemImageProviderTests|FullyQualifiedName~TmdbUtilsTests"`: 154/154 aprovados, 0 ignorados/falhas. A compilação emitiu avisos StyleCop em arquivos já existentes; não foram alterados nesta revalidação.
- O resultado confirma o filtro/ranking local e inglês como fallback; não confirma se o checkbox “Todos os idiomas” estava ativo na captura, nem a resposta do TMDb, refresh visual ou arquivo persistido do título exemplo. Sem mudança de runtime, instalação ou produção.
- Escopo inclui o fallback limitado a preferred/original/en/null quando a chamada de catálogo de idiomas falha; nesse caso, a abrangência total depende de nova consulta após o cache de falha. Próximo passo para H06: validar uma consulta real autenticada isolada do item e comparar os candidatos antes/depois de refresh, sem scraping/import externo.

## Deduplicação do fallback inglês — 2026-10-22

`TmdbUtils.GetImageLanguagesParam` compara agora o idioma ISO 639-1 da preferência com `en`, evitando `en,null,en` para preferências regionais como `en-US`. Preferência `pt-BR` continua emitindo `pt,null,en`; nenhum idioma adicional é incluído. Testes focais Release: Providers 72/72 e Server.Implementations 4/4, incluindo contrato de migration que verifica as duas colunas nullable e rollback limitado a elas. O teste confirma operações EF; não executa SQL contra MariaDB. `dotnet ef migrations script` não pôde rodar nesta sessão porque `dotnet-ef` não está restaurado; variável de banco MariaDB descartável ausente. Sem TMDb ao vivo, migration aplicada, instalação, mídia real ou produção. Homologação formal permanece 1/7 (14,3%).

## Reexecução da validação focada — 2026-10-23

- Decisão mantida: português configurado como preferência; inglês aceito somente como fallback quando não existe arte em português para o tipo de imagem. A busca TMDb usa `pt,null,en` para pt-BR; imagens manuais/legadas sem proveniência conhecida não são substituídas automaticamente.
- Testes Release em Windows/.NET 10: `TmdbUtilsTests|ProviderManagerImageProviderTests|ItemImageProviderRefreshTests|MetadataServiceTests` passaram 72/72, sem ignorados ou falhas.
- Nenhuma chamada TMDb ao vivo, refresh visual de *Invasão Zumbi 2*, migration aplicada, instalação ou validação em produção nesta sessão. Portanto, o resultado confirma o contrato local, não a correção visual de todos os títulos. Homologação formal permanece 1/7 (14,3%); sem release de servidor.

## Revalidação da regra de idioma visual — 2026-10-22

Em Release, os filtros `ProviderManagerTests|ItemImageProviderTests|TmdbUtilsTests` passaram 160/160 e `EnumerableExtensionsTests` 7/7. Isso confirma seleção agregada de candidatos, preferência pt/pt-BR por tipo de arte, inglês como fallback quando não há português e correspondência pt-BR→pt. A execução não consultou TMDb, não atualizou imagem persistida nem validou visualmente *Invasão Zumbi 2*. Preferências já persistidas e imagens locais/manuais não são alteradas automaticamente; refresh seguro da imagem remota elegível continua necessário após a versão corrigida. O andamento foi publicado no portal: `Mulletaflix-Portal:main` commit `29db87f69a353e351b36d1bfcf7260caea8973a1`; Vercel Production `READY` e endpoint público HTTP 200 confirmou o evento. Homologação formal permanece 1/7 (14,3%); nenhum release ou deploy do servidor foi realizado.

### Reexecução focal após confirmação do fallback — 2026-10-23

O usuário confirmou manter inglês como fallback. `TmdbUtilsTests|ProviderManagerImageProviderTests` passou 27/27 em Windows/.NET 10 Release. Isso valida somente os contratos locais de idiomas/ranking; não consulta TMDb, não inspeciona a arte persistida de *Invasão Zumbi 2* e não prova que haja pôster português para cada item. Arte já persistida não é substituída em massa; refresh controlado do item e comparação da resposta/capa escolhida seguem pendentes. Nenhuma alteração de runtime, instalação, produção ou release; homologação formal continua 1/7 (14,3%).

## Idioma vazio em configuração legada — 2026-10-23

- **Causa reproduzível:** quando `PreferredMetadataLanguage` vinha vazio de configuração legada, `TmdbUtils.GetImageLanguagesParam` produzia `null,en`; a consulta deixava de solicitar arte portuguesa apesar da preferência de produto pt-BR. A consulta pública ao endpoint de imagens TMDb do filme *Peninsula* (ID 581392), com `include_image_language=pt,null,en`, retornou 6 posters, 2 backdrops e 1 logo em português, além de inglês/neutro. A página de posters do TMDb também lista seis imagens Portuguese.
- **Correção:** `ServerConfiguration.PreferredMetadataLanguage` normaliza apenas valores vazios/espaços para `pt-BR`; idiomas explicitamente definidos são preservados. Inglês continua fallback. Capa já persistida não é migrada/alterada automaticamente.
- **Testes Release Windows/.NET 10:** `ServerConfiguration_*` passou 6/6, incluindo XML legado vazio/espaços; `TmdbUtilsTests|ProviderManagerImageProviderTests|ItemImageProviderRefreshTests|MetadataServiceTests` passou 77/77. Revisão independente não encontrou problema. `git diff --check` passou.
- **Limites:** endpoint público confirma disponibilidade TMDb, não a resposta do runtime instalado nem arquivo/capa persistidos. Não houve refresh visual, produção, instalação ou release. Homologação formal não recebe crédito; servidor segue 2/7 (28,6%). Próxima validação: instalar em homologação isolada, consultar a galeria padrão sem “Todos os idiomas”, atualizar um item de teste e conferir idioma/proveniência/renderização.

### Revalidação posterior da preferência pt-BR — 2026-10-23

`TmdbUtilsTests|ProviderManagerImageProviderTests` passou 32/32 em Windows/.NET 10 Release. Mantém pt-BR/pt primeiro e inglês como fallback local. Esta rodada não alterou código de imagens e não consultou TMDb; capa salva, refresh visual de *Invasão Zumbi 2*, instalação e produção não foram verificados. O status formal permanece 2/7 (28,6%); sem release.

## Refresh de backdrops remotos no idioma preferido — 2026-10-23

- **Causa confirmada no código:** `RefreshImages` detectava arte remota desatualizada somente em `_singularImages`; backdrops em inglês não acionavam a consulta localizada nem eram marcados para substituição.
- **Correção local:** backdrops com provedor e idioma conhecidos entram na detecção. Ao existir candidato no idioma preferido, o refresh adiciona a nova imagem antes de remover apenas backdrops remotos obsoletos; arte manual e backdrops sem proveniência são preservados. Sem candidato localizado, o fallback inglês existente permanece.
- **Regressão:** `RefreshImages_ReplacesStaleRemoteBackdropAndPreservesManualBackdrop` valida troca en→pt e preservação da imagem manual. `ItemImageProviderTests` passou 60/60 em Windows/.NET 10; `git diff --check` passou.
- **Limites:** não houve chamada real ao TMDb, refresh/inspeção visual de *Invasão Zumbi 2*, instalação, produção ou release. Homologação formal permanece 2/7 (28,6%), sem crédito; alteração no checkout servidor ainda não foi publicada.

## Fallback inglês após falha do candidato localizado — 2026-10-23

- **Causa:** a consulta padrão do `ProviderManager` remove inglês quando qualquer provedor retorna arte em português. Isso é correto para a galeria manual, mas impedia o `ItemImageProvider` de tentar inglês quando o download pt falhava.
- **Correção local:** somente o refresh automático pede candidatos de todos os idiomas e depois conserva idioma preferido, inglês e arte sem idioma. Provedores com arte preferida são tentados antes dos que só têm fallback. O filtro da galeria manual permanece inalterado.
- **Teste de regressão:** `RefreshImages_FailedPreferredLanguageImage_UsesEnglishFromAnotherProvider` prova que falha HTTP no pôster pt permite usar pôster en de outro provedor. `ItemImageProviderTests|ProviderManagerTests` passou 137/137 em Windows/.NET 10 Release; `dotnet format --verify-no-changes` e `git diff --check` passaram.
- **Limites:** não houve TMDb real, refresh/renderização de *Invasão Zumbi 2*, instalação, produção nem release. Imagens já persistidas não são regravadas automaticamente. Homologação formal permanece 2/7 (28,6%), sem crédito; servidor segue em checkout local sujo e 8 commits atrás.

### Reexecução adicional — 2026-10-23

`dotnet test tests/Jellyfin.Providers.Tests/Jellyfin.Providers.Tests.csproj --no-restore --configuration Release --filter FullyQualifiedName~TmdbUtilsTests` passou 32/32 em Windows/.NET 10. Confirma apenas os contratos unitários de normalização do idioma e fallback; não consultou TMDb nem demonstrou a capa apresentada na interface. Também foi esclarecido o rótulo de cobertura na cópia de homologação do monorepo para distinguir checklist do roadmap de homologação formal; o portal público já apresenta essa distinção. Nenhuma release/runtime/produção foi alterada. O mapa técnico foi conferido e a estrutura permaneceu igual.

## Proveniência do idioma por imagem remota — 2026-10-23

`MetadataResult.ResultLanguage` identifica o idioma do resultado textual do provider, não o idioma de cada imagem retornada. O modelo `RemoteImages` contém URL e tipo, sem idioma individual; por isso `SaveRemoteResultImages` não deve propagar o idioma do texto para todas as artes. A gravação agora deixa `ItemImageInfo.Language` nulo quando a imagem não tem proveniência própria, evitando classificar uma capa possivelmente inglesa como pt-BR e impedir refresh localizado posterior. ProviderName continua registrado. Regressão `SaveRemoteResultImages_DoesNotUseMetadataLanguageAsImageLanguage`; `MetadataServiceTests` passou 45/45 em Windows/.NET 10 Release. Imagens existentes não foram reclassificadas; TMDb ao vivo, refresh visual, runtime instalado e produção não foram verificados. Sem crédito de homologação ou release.

## Revalidação do deadlock MariaDB em imagens — 2026-10-24 14:05 BRT

O anexo repete falhas 1213 em `DELETE BaseItemImageInfos` concorrentes para o mesmo `ItemId`, mas não identifica PID nem assembly carregado. No checkout, `SaveImagesAsync` usa stripe local + `GET_LOCK` MariaDB, enquanto `SaveItems` e `DeleteItem` coordenam as gravações relacionadas; `ItemPersistenceDatabaseLock.cs` continua não rastreado e a implementação não existe em `origin/main`. `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 40/40 Release Windows/.NET 10. O teste MariaDB concorrente foi compilado e ignorado por falta de configuração opt-in; não se tocou na porta ativa 3306. Logo, o log ainda não comprova a correção em runtime.

Na mesma sessão, `FullSystemBackup` teve uma falha transitória em teste de limpeza que usa `FileSystemWatcher` (evento de `.bak` entregue após remoção); repetido sozinho passou 1/1. Não houve alteração no código nem evidência suficiente para marcar a regressão de teste corrigida. Restore segue pathname-based, o helper ACL não está integrado e o P0 continua aberto. Sem release/produção. Portal atualizado localmente; homologação formal 2/7 (28,6%), sem crédito. Ver [[Sessões/2026-10-24-MulletaFlix-image-deadlock-and-restore-test-rerun-1405]].

## Rechecagem de log repetido do deadlock MariaDB — 2026-10-24 14:27 BRT

O novo anexo repete o padrão já investigado: `DELETE BaseItemImageInfos` concorrentes, MariaDB 1213, retries e heartbeat Kestrel longo. O `HTTP 200` antecedente não prova que a transação de persistência foi confirmada. A fonte local coordena `SaveImagesAsync`, `SaveItems` e `DeleteItem` com semaphore por stripe e advisory `GET_LOCK` mantido até a transação; `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` foi recompilado em Release e passou 40/40. Isso não identifica o assembly/PID do log nem garante que o runtime tenha essa fonte; auditoria anterior encontrou ausência do helper em `origin/main`. O teste concorrente MariaDB requer `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` e não foi executado nesta rodada; o banco ativo não foi acessado. Sem mudança de código/runtime. Formal 2/7 (28,6%), sem crédito; sem release ou publicação remota. Restore ACL/TOCTOU P0 permanece aberto. Ver sessão `2026-10-24-MulletaFlix-image-deadlock-recheck-1427`.

## Auditoria dos escritores e divergência remota — 2026-10-24 14:30 BRT

O inventário de `BaseItemImageInfos` no código local encontrou quatro caminhos mutáveis: `SaveImagesAsync`, `SaveItems/UpdateOrInsertItems`, `DeleteItem` e a rotina de migração `FixDates`. Os três caminhos de `ItemPersistenceService` usam `ItemPersistenceDatabaseLock` e as mesmas stripes em processo; `FixDates` toma as 16 stripes MariaDB. A `main` remota foi verificada no SHA `d51f4f38d2b4148ab49394c001567ad273bd57aa` e ainda não contém o helper nem as chamadas; não há processo escutando em 8096 no momento da verificação. Isso sustenta divergência entre o checkout protegido e a base remota, mas não identifica o processo histórico do log.

`ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests`: 40/40. Contrato da CI MariaDB: 2/2. Os três testes reais selecionados de concorrência MariaDB compilaram em Release e foram ignorados por ausência de `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`; Docker/Podman não estão instalados e a porta ativa 3306 não foi acessada. A CI Linux já declara MariaDB 11.4 e seleciona os cenários de refresh e lock entre sessões, mas seu resultado remoto não foi consultado. Sem mudança de código/runtime; release continua bloqueada pela divergência, pela falta de execução MariaDB nesta rodada e pelo restore ACL/TOCTOU P0. Homologação formal 2/7, sem crédito. Ver sessão `2026-10-24-MulletaFlix-image-deadlock-remote-gap-audit-1430`.
## Revalidação do log MariaDB 1213 — 2026-10-24 14:45 BRT

O anexo recebido repete erros `1213 Deadlock found` em `DELETE BaseItemImageInfos` para o mesmo `ItemId`, em chamadas concorrentes, seguidos de retentativas. A resposta HTTP 200 anterior não comprova commit da persistência. A fonte local em `ItemPersistenceService.SaveImagesAsync` serializa as operações por stripe em processo e mantém `ItemPersistenceDatabaseLock` (`GET_LOCK`) durante a transação; `SaveItems` e `DeleteItem` também compartilham as stripes e advisory locks. Isso descreve o checkout, não identifica o código carregado pelo processo do log. `FixDates` é rotina de migração e não indica um writer recorrente em runtime. Sem PID/assembly no log, a causa operacional segue sem correlação conclusiva; processo antigo/misto ou outro writer são hipóteses.

Nesta revalidação: `ItemPersistenceServiceTests` Release passou 31/31; os dois testes MariaDB selecionados (refreshes concorrentes e lock mantido por outra sessão) foram ignorados por ausência de `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`. Nenhum banco ativo ou produção foi consultado. A correção não está reconciliada com `origin/main` segundo a auditoria anterior; não marcar produção corrigida nem criar release com o checkout divergente. Próximo gate: MariaDB descartável/CI com resultado observado e identificação do assembly runtime. Homologação formal permanece 2/7 (28,6%), sem crédito.

## Verificação atual do runtime do deadlock — 2026-10-24 15:06 BRT

Reconsulta read-only confirma `origin/main` no SHA `d51f4f38d2b4148ab49394c001567ad273bd57aa`; o arquivo remoto `ItemPersistenceService.cs` faz `ExecuteDeleteAsync` em `BaseItemImageInfos` sem stripe/advisory lock. O assembly presente em `C:\Program Files\MulletaFlix\Server` reporta versão `12.1.35.0` e timestamp 21/10/2026; isso identifica o arquivo instalado, não o assembly carregado pelo processo do log. Testes focados locais Release passaram 40/40; MariaDB descartável não estava configurado e não foi acessado. Runtime/produção seguem sem correlação conclusiva; sem release.

## Rechecagem do log anexado — 2026-10-24 15:09 BRT

O novo trecho de log, datado 23/10/2026 20:35 BRT, repete vários `DELETE BaseItemImageInfos` falhos em paralelo para o mesmo item; há várias mensagens de retry da tentativa 2 e heartbeat do Kestrel atrasado. O `HTTP 200` antecedente é de outra operação e não confirma commit dessas transações. O log não traz PID/assembly nem uma conclusão bem-sucedida do retry; portanto, confirma recorrência observada, mas não qual código/runtime executou. A rechecagem read-only de 15:06 permanece: a fonte em `origin/main` não contém o lock local atual. Nenhum banco ativo foi consultado; sem release/instalação e homologação formal continua 2/7 (28,6%), sem crédito.

## Inventário de escritores do estado de imagens — 2026-10-24 15:10 BRT

Busca em todo `MulletaFlix-master` excluindo testes e migrations encontrou mutações da tabela em `ItemPersistenceService`: exclusão de `DeleteItem` (linha 267), delete/insert de `SaveImagesCoreAsync` (390/395) e delete/add de `UpdateOrInsertItemsCore` (860/863). A rotina `FixDates.FixBaseItemImageInfos` (123-155) também persiste datas via `SaveChangesAsync`. No checkout local, os três caminhos de `ItemPersistenceService` usam locks locais por stripe e advisory locks MariaDB; `FixDates` adquire somente as 16 stripes advisory no banco, sem semaphore local. `GET_LOCK` usa o mesmo schema e conexão durante a transação. O wrapper `SaveItems` chama `UpdateOrInsertItems`, que adquire stripes por todos os IDs antes de abrir o contexto e transacionar. Não encontramos outro escritor de runtime na busca. Isso é evidência de fonte local, não prova de runtime. `origin/main` ainda não contém o helper; por isso o log pode ser de binário anterior, mas PID/assembly continuam desconhecidos.

## CI MariaDB remota — 2026-10-24 15:12 BRT

O workflow local define serviço descartável MariaDB 11.4 e inclui os testes de concorrência de imagens em `.github/workflows/ci.yml`. A execução pública do CI para SHA `d51f4f38d2b4148ab49394c001567ad273bd57aa` (run `39608614450`) terminou `failure`; o job `User schema MariaDB Linux (case-sensitive)` também falhou, mas a API retornou `steps: []` e o download dos logs respondeu 404. Portanto, nenhum resultado dos testes MariaDB pode ser inferido dessa execução, nem a causa do erro. CI remoto deve ser repetido/diagnosticado antes de considerar o gate validado; homologação permanece 2/7.

## Execução local segura do gate MariaDB — 2026-10-24 15:14 BRT

`MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` estava ausente; `MariaDbTestEnvironment` e a fixture retornam sem conectar quando ausente. O projeto `IntroSkipper.Integration.Tests` compilou em Release (com warning MSB3277 de conflito `Microsoft.Extensions.DependencyModel` 9.0/10.0; o build escolheu 9.0 como primário). A execução focada `ServerPersistenceMariaDbTests` terminou exit 0, com 12 ignorados, 0 aprovados e 0 falhas; confirma descoberta/skip, não valida persistência nem concorrência. `dotnet list package --include-transitive` mostra DependencyModel 10.0.0 na árvore, mas não identificou quem trouxe a referência 9.0; conflito fica pendente de rastreamento.

## Escopo de EF Design no plugin — 2026-10-24 15:17 BRT

Assets do teste localizaram a referência 9.0 em `Microsoft.EntityFrameworkCore.Design/9.0.0`, que o plugin propagava porque seu `PackageReference` não declarava `PrivateAssets`. `Jellyfin.Server.csproj` e `Jellyfin.Database.Implementations.csproj` já mantêm EF Design privado. Apliquei o mesmo escopo em `MulletaFlix.Plugin.IntroSkipper.csproj`; plugin compila, a suíte de integração Release compila com `-warnaserror:MSB3277` e nenhum warning desse ID aparece. Build total terminou exit 0, ainda com 1.389 outros avisos existentes. O teste focado termina exit 0, 12 ignorados, 0 aprovados, 0 falhas porque MariaDB opt-in não está configurado; não é validação de concorrência.

## Rechecagem de deadlock e identificação de build/runtime — 2026-10-24 15:50 BRT

O log de 23/10 contém 12 falhas de `DELETE BaseItemImageInfos` para o mesmo `ItemId` em aproximadamente 9 ms; 11 exceções são deadlock 1213, 12 logs indicam retry e o Kestrel reporta heartbeat de 2,35 s. O HTTP 200 na primeira linha é de outra operação e não confirma persistência. O log não identifica PID nem assembly.

Leitura local, sem iniciar servidor ou consultar MariaDB: a DLL instalada é `MulletaFlix.Server.Implementations` 12.1.35, SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`, e não contém o marcador UTF-16 `mfx:item:` do advisory lock atual. A DLL recém-compilada do checkout é 12.1.32, SHA-256 `7E36006D7E2001FDBD768161A0EF7C24EE405BD8C0A46A51237BC7200FFC9DFD`; o código atual contém `ItemPersistenceDatabaseLock`. Isso prova divergência entre artefatos lidos, mas não vincula o log histórico à DLL instalada. `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 40/40 em Release Windows/.NET 10. O teste real MariaDB opt-in continua sem execução; listener 3306 não foi usado.

Próximo gate: reconciliar a correção com a base/versionamento da release e executar testes cross-session contra MariaDB descartável ou CI confiável. Sem release, instalação, produção ou crédito de homologação; formal 2/7 (28,6%).

## MariaDB descartável para concorrência de imagens — 2026-10-24 16:01 BRT

MariaDB 11.4.4 foi iniciado com datadir separado e bind somente em `127.0.0.1:33099`; a instância operacional em 3306 não foi conectada. Os três testes focados de refresh concorrente, advisory lock entre sessões e delete concorrente com refresh passaram 3/3. A classe `ServerPersistenceMariaDbTests` passou 12/12, 0 ignorados e 0 falhas. A primeira execução encontrou fixture inválida em `DeleteExpiredPlaceholderUserDataAsync_DeletesOnlyExpiredDetachedRows`: o teste criava `UserData` apontando a um `UserId` ausente. A fixture agora insere um usuário isolado antes dos dados; classe completa reexecutada com sucesso.

O `mysqld` descartável foi encerrado e a porta 33099 ficou fechada. O diretório temporário de teste permaneceu porque a política da ferramenta recusou a remoção recursiva; não há processo usando-o. Isso prova comportamento do checkout Windows/.NET 10 contra MariaDB local, não identifica o assembly do log, nem valida instalação, produção ou CI remota. DLL instalada 12.1.35 e build local 12.1.32 continuam divergentes conforme a auditoria 15:50; nenhum crédito formal adicional (2/7, 28,6%) e nenhuma release.

## Reauditoria do fluxo de locks — 2026-10-24 16:38 BRT

O log anexado repete o deadlock MariaDB 1213 durante `DELETE BaseItemImageInfos`; não inclui PID/assembly. Releitura dos quatro writers corrigiu uma imprecisão deste documento: `SaveImagesAsync`, `SaveItems` e `DeleteItem` usam stripes locais e locks advisory MariaDB; a rotina de migração `FixDates` usa somente as 16 stripes advisory no banco, sem semaphore local. Os advisory locks são adquiridos em ordem crescente; não foi encontrado ciclo de aquisição entre esses quatro caminhos. Isso não exclui writers externos nem explica o evento histórico.

`ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests`: 40/40 aprovados em Windows/.NET 10 Release. A validação MariaDB descartável já registrada às 16:01 permanece 3/3 concorrência e 12/12 classe completa, com instância encerrada e porta operacional 3306 intocada. Sem novo teste contra runtime/produção ou CI remota. DLL instalada 12.1.35 e checkout 12.1.32 permanecem não reconciliados; formal 2/7 (28,6%), sem crédito ou release. Mapa técnico conferido; estrutura/fluxo de produção inalterados nesta reauditoria.

## Reexecução focal após novo log — 2026-10-24 17:18 BRT

O anexo repete erros MariaDB 1213 em `DELETE BaseItemImageInfos` concorrentes, retries e heartbeat Kestrel de 2,35 s; o HTTP 200 anterior não comprova commit. Reexecutei `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` em Release Windows/.NET 10: 40 aprovados, 0 ignorados, 0 falhas. A fonte local contém `ItemPersistenceDatabaseLock`, mas não havia processo MulletaFlix ativo e o log não informa PID/assembly nem transação concorrente. Não se correlacionou o log a binário/runtime, nem se acessou banco operacional ou produção. Sem alteração de código; causa-raiz permanece não confirmada, sem release e sem crédito de homologação (2/7, 28,6%).

## Rechecagem do log e gates atuais — 2026-10-24 18:05 BRT

O anexo continua mostrando operações concorrentes de `DELETE BaseItemImageInfos` para o mesmo `ItemId`, deadlocks InnoDB 1213 e retries. O trecho termina sem demonstrar o resultado da última tentativa; o HTTP 200 inicial é de uma operação anterior. `SaveImagesAsync_WaitsForSameItemPersistenceLockBeforeOpeningDatabaseContext` passou 1/1 em Release Windows/.NET 10. O teste MariaDB `SaveImagesAsync_ConcurrentRefreshesForSameItem_PersistOneCompleteImageSet` foi ignorado porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada; nenhum banco foi acessado nesta rodada. Uma execução anterior de MariaDB descartável (11.4.4, 127.0.0.1:33099) passou três cenários de concorrência e a classe 12/12, registrada às 16:01, mas não correlaciona o assembly ao log operacional. A API pública do GitHub ainda lista a execução CI main 39608614450 como falha no SHA `d51f4f38d2b4148ab49394c001567ad273bd57aa`; não apareceu run posterior nessa consulta. O erro pode ser originado por binário sem o lock ou por writer concorrente não cooperante, mas isso permanece hipótese: o anexo não tem PID/assembly nem transação InnoDB. Causa-raiz/produção não confirmadas; formal 2/7 (28,6%), sem crédito e sem release.

## Auditoria dos jobs da CI — 2026-10-24 18:08 BRT

Consulta adicional da execução pública `39608614450`: os 13 jobs terminaram `failure`; todos exibem `runner_id=0`, `runner_name` vazio, `steps=[]` e duração de aproximadamente 3–4 segundos, em matrizes Linux, Windows e macOS. Os logs recuperados para jobs Nebula/Mongo, Server build e MariaDB Linux retornam 404 `BlobNotFound`. O padrão indica falha antes da execução de passos no runner e favorece investigar alocação/orquestração do GitHub Actions; não determina causa específica, não valida nem invalida código/testes do servidor. Nenhum rerun foi iniciado. Gate remoto Linux segue sem evidência executada; homologação formal 2/7 (28,6%), sem crédito.

## Gate de empacotamento da release v12.1.35 — 2026-10-24 18:09 BRT

O workflow público de release `39608677913` para o SHA `d51f4f38d2b4148ab49394c001567ad273bd57aa` também não executou steps. `Validate source tag before packaging` terminou failure após 3 s com `runner_id=0` e nome vazio; log 404 `BlobNotFound`. `Build Linux x64 package` e `Build and publish release` foram skipped. Assim, esse run não gerou/publicou assets; isso não demonstra ausência de release v12.1.35 criada por outro fluxo ou manualmente. A falha de runner/orquestração é compatível com o CI principal, mas causa exata segue não observável. Sem rerun, escrita remota, pacote ou release produzida por esse workflow.

## Revalidação após novo log de deadlock — 2026-10-24 18:13 BRT

O anexo repete deadlocks MariaDB 1213 durante `DELETE BaseItemImageInfos` concorrentes para o mesmo item, seguidos de retries. O HTTP 200 anterior é de outra operação e não comprova commit. A fonte local coordena `SaveImagesAsync`, `SaveItems` e `DeleteItem` por stripes locais e advisory `GET_LOCK`; a suíte `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` foi recompilada e passou 40/40 em Release/.NET 10 nesta execução. Isso valida o checkout, não identifica o PID/assembly que produziu o log e não valida runtime/produção. Nenhuma conexão foi feita ao banco ativo. Integração MariaDB descartável não foi executada nesta rodada; CI remota permanece sem evidência acessível. Homologação formal 2/7 (28,6%), sem crédito; sem release/publicação remota.

## Refresh de idioma/provedor e regressão MariaDB — 2026-10-24 20:54 BRT

O anexo mais recente demonstra concorrência de `DELETE BaseItemImageInfos` para o mesmo `ItemId` e deadlocks MariaDB `1213`; não inclui PID/assembly, portanto não identifica o binário que executou o erro. No checkout principal, a revisão independente confirmou que os escritores conhecidos compartilham stripes/advisory locks e encontrou uma lacuna distinta: `SaveImagesCoreAsync` podia considerar imagens iguais sem comparar `ProviderName`/`Language`, e o pareamento por `FirstOrDefault` não consumia duplicatas. A comparação agora consome cada linha correspondente e inclui os campos persistidos, mantendo a substituição já transacional.

`ItemPersistenceDatabaseLockTests`: 9/9 em Windows/.NET 10 Release. MariaDB 11.4.4 descartável, datadir próprio e loopback `127.0.0.1:33099`: quatro testes passaram (refreshes concorrentes, `GET_LOCK` entre sessões, delete concorrente com refresh e alteração persistida de provedor/idioma). O processo descartável foi encerrado; a pasta foi preservada quando o ambiente recusou a remoção recursiva. A instância operacional em 3306 permaneceu em execução e não foi conectada. Isso valida o checkout/testes, não o runtime do log, produção, CI remota ou release. Homologação formal continua 2/7 (28,6%), sem crédito; checkout segue 8 commits atrás com alterações preexistentes.

## Revalidação do log repetido — 2026-10-24 21:25 BRT

O anexo repetido mantém o padrão de `DELETE BaseItemImageInfos` com deadlock MariaDB 1213, seguido de retry; resposta HTTP 200 anterior não confirma persistência. Reexecutei `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` em Windows/.NET 10 Release: 40 aprovados, 0 ignorados, 0 falhas. `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada, então integração MariaDB não foi executada nesta sessão. O arquivo não informa PID/assembly; não há correlação com runtime nem evidência de produção corrigida. Não houve alteração de código ou release. Homologação formal permanece 2/7 (28,6%), sem crédito.

## Gate MariaDB isolado e revisão do CA1416 — 2026-10-24 21:55 BRT

`MariaDbTestEnvironment` exige loopback em porta não padrão e recusa 3306. A variável `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada; não foi encontrado executável MariaDB em PATH/Program Files. Foi observado `mysqld` escutando em `127.0.0.1:3306` com conexões existentes. Não conectei nem alterei essa instância; os testes de integração MariaDB continuam pendentes até haver serviço descartável isolado.

O teste focado `ServerConfigurationManagerFilePermissionsTests` terminou exit 0 em Windows/.NET 10: 1 aprovado, 1 ignorado, 0 falhas. O caso Unix-only chama `File.GetUnixFileMode` após `Assert.SkipWhen(OperatingSystem.IsWindows())`; o `CA1416` é aviso de análise nesta plataforma, sem falha reproduzida. Isso não valida Linux/ACL, MariaDB real, runtime nem produção. Homologação formal permanece 2/7 (28,6%), sem crédito.

## Revalidação do anexo de deadlock — 2026-10-24 22:12 BRT

O anexo repete `DELETE BaseItemImageInfos` concorrentes para o mesmo item, deadlock InnoDB 1213 e retries. O HTTP 200 antecedente não comprova commit, e o trecho não inclui PID/assembly nem o resultado final das tentativas. Em Windows/.NET 10 Release, `ItemPersistenceServiceTests` passou 31/31; a combinação `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 40/40, sem skips/falhas. Contratos do portal passaram 162/162. O teste de integração MariaDB não foi executado nesta rodada; nenhum banco ou runtime operacional foi acessado. A evidência confirma o comportamento observado no log e a regressão do checkout, mas não correlaciona o emissor ao código atual nem confirma correção em produção. Homologação formal permanece 2/7 (28,6%), sem crédito; sem release ou publicação remota.

## Reconciliação de runtime local e fonte — 2026-10-24 22:33 BRT

Inspeção somente leitura encontrou o processo do servidor local iniciado em 24/10 às 19:15; executável e módulos `MulletaFlix.Server.Implementations.dll`/`MediaBrowser.Providers.dll` no caminho carregado reportam versão 12.1.35 e timestamps de 21/10. A fonte em `SharedVersion.cs` declara 12.1.32; `ItemPersistenceDatabaseLock.cs` e seus testes estão apenas no working tree, sem integração/commit, enquanto `ItemPersistenceService.cs` está alterado localmente. Regressão focal atual `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 40/40 em Windows/.NET 10 Release. MariaDB descartável não foi executado; não houve acesso ao banco operacional, restart, instalação ou release. Versões dos arquivos em disco e estado do processo são evidência local, não certificação de produção nem prova de correlação com o log sem PID. Homologação formal permanece 2/7 (28,6%), sem crédito.

## Integração de persistência no MariaDB descartável — 2026-10-24 22:46 BRT

Inicializei MariaDB 11.4.4 com datadir novo e isolado, sem arquivo de configuração externo, em `127.0.0.1:33099`. A suíte completa `ServerPersistenceMariaDbTests` passou 13/13, sem ignorados ou falhas, em 6 min 11 s; os testes criaram e removeram schemas `mfx_it_*`. A instância foi encerrada normalmente e o processo de teste não permaneceu ativo. A remoção do datadir em TEMP foi bloqueada pelo terminal, então ele foi preservado. A porta operacional 3306 não foi usada. Isso valida a fonte local e os cenários de integração; não resolve a divergência entre runtime local 12.1.35 e fonte 12.1.32, não associa o log a um PID/assembly e não comprova produção/CI/release. Homologação formal segue 2/7 (28,6%), sem crédito.

## Revalidação do anexo na candidata v12.1.35 — 2026-10-24 23:00 BRT

O log anexado registra `DELETE BaseItemImageInfos` concorrentes para o mesmo `ItemId`, deadlock InnoDB 1213 e retries na tentativa 2; o HTTP 200 na primeira linha é de outra operação e não comprova persistência. Também há heartbeat atrasado do Kestrel, compatível com pressão de concorrência, mas insuficiente para atribuir starvation como causa raiz.

Validei a worktree candidata baseada em `origin/main` SHA `d51f4f38d2b4148ab49394c001567ad273bd57aa`, fonte v12.1.35, sem integrar ou alterar as mudanças existentes: `ServerPersistenceMariaDbTests` passou 12/12, 0 ignorados/falhas, em MariaDB 11.4.4 isolado em `127.0.0.1:33099`; `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 43/43 em Release/.NET 10. MariaDB descartável encerrou normalmente e a porta fechou. A instância operacional em 3306 não foi usada. A worktree continua suja/não integrada; os testes não identificam o PID/assembly que produziu o log, não validam runtime/produção nem CI remota. Formal 2/7 (28,6%), sem crédito; sem instalação ou release.
## Rechecagem de CI e scan de dependências — 2026-10-25 02:42 BRT

A API GitHub consultada nesta sessão ainda lista como runs mais recentes CI `39608614450` e release `39608677913`, de 21/10/2026, ambos com falha. Os 13 jobs da CI retornaram `steps=null`; downloads de logs amostrados retornaram HTTP 404 `BlobNotFound`. Na release, a validação de tag falhou e empacotamento Linux/publicação foram pulados. A causa não foi identificada e nenhum rerun foi acionado. O comando `dotnet list ... package --vulnerable --include-transitive --no-restore` não forneceu saída útil e foi interrompido; não há conclusão de vulnerabilidade. Contratos do portal passaram 169/169. Formal segue 2/7 (28,6%), sem crédito; produção, CI executada em runner e scan conclusivo permanecem pendentes.

## Regressão global e novo recorte do log de deadlock — 2026-10-25 03:11 BRT

Recontagem dos 17 arquivos TRX da regressão `MulletaFlix.sln` Release (`--no-build --no-restore`, execução serial, filtro `FullyQualifiedName!~LiveIntegrationTests`) confirmou 5.287 `Passed`, 255 `NotExecuted`, 0 `Failed`, total 5.542; processo terminou com exit code 0. Os testes Nebula passaram 697/697 em Python 3.10.11 isolado. Uma tentativa de pytest feita com diretório de trabalho na raiz falhou durante a coleta por imports relativos ao projeto; repetida a partir de `Tools/NebulaPython` passou.

O novo anexo registra 12 comandos `DELETE BaseItemImageInfos` falhos e repetidos erros MariaDB 1213 no mesmo item, seguidos de retries; Kestrel também reporta heartbeat atrasado 2,35 s. A fonte local já serializa `SaveImagesAsync` com stripe intra-processo e advisory lock MariaDB, mas o checkout declara 12.1.32 e o log não fornece PID/versão. Assim, não se atribui o log à fonte atual nem se afirma correção em produção. O teste não acessou o banco operacional. Checkout permanece preexistente e sujo; venv descartável de teste ficou em `Tools/NebulaPython/.build/nebula-regression-20261025-0309` após o terminal bloquear sua remoção.

Homologação formal segue 2/7 (28,6%), sem crédito. Não valida runtime instalado, Linux, CI remota, provedores live nem DB operacional; nenhuma release/instalação/publicação ocorreu. O mapa foi conferido contra `ItemPersistenceService.SaveImagesAsync`, `ItemPersistenceDatabaseLock`, os 17 projetos .NET e o runtime de testes Nebula; sem alteração de módulos, APIs, dependências ou arquitetura nesta rodada.

## Reexecução das regressões de persistência em MariaDB descartável — 2026-10-25 07:36 BRT

Para fechar o gate que ficou ignorado na rechecagem anterior, inicializei MariaDB 11.4.4 com datadir exclusivo e porta loopback não padrão `33217`, sem registrar serviço Windows. O filtro Release `FullyQualifiedName~ServerPersistenceMariaDbTests` passou 14/14, sem skips/falhas, em 24 s. A classe cobre refresh de imagens, alterações de provider/idioma, exclusão concorrente e locks entre sessões. A instância descartável encerrou e a porta 33217 fechou; a configuração de teste apontou somente para 33217, não para o listener em 3306. O datadir temporário gerado não foi removido porque a proteção do terminal bloqueou remoção recursiva.

O resultado valida o checkout atual, não o binário que emitiu o log nem CI Linux/produção. O contrato local do portal passou 184/184; JSON, sintaxe e diff checks passaram. `origin/main` foi consultado novamente e segue em `d51f4f38d2b4148ab49394c001567ad273bd57aa`, sem o helper local; correção ainda não integrada à branch remota. PID/assembly do incidente e estado final dos retries continuam desconhecidos. Homologação formal permanece 2/7 (28,6%), sem crédito.

## Rechecagem independente do anexo MariaDB — 2026-10-25 07:31 BRT

O anexo atual (`SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, 252 linhas) contém 12 `DELETE BaseItemImageInfos`, 11 erros MariaDB 1213, 12 retries “attempt 2” e um heartbeat Kestrel de 2,35 s. O HTTP 200 inicial precede as falhas e não demonstra commit. O log não contém PID/assembly, estado final dos retries ou relatório InnoDB; não identifica a instância que executou a chamada.

Inventário da fonte atual encontrou somente `ItemPersistenceService` e `FixDates` como escritores de produção dessa tabela. Os caminhos do serviço envolvem `ItemPersistenceDatabaseLock`; a migração também obtém as 16 advisory stripes em MariaDB. O teste CI Linux/MariaDB descartável já seleciona três regressões de concorrência/locks. `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 40/40; as três integrações MariaDB selecionadas foram realmente executadas pelo runner, porém ignoradas por falta de `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`. Contrato de seleção da CI passou 2/2.

Estado de distribuição: `origin/main` observado em `d51f4f38d2b4148ab49394c001567ad273bd57aa` não contém `ItemPersistenceDatabaseLock`; o checkout local `main` está em `64a33dcf` e o helper permanece não rastreado com alterações relacionadas não commitadas. Nesta máquina, serviço `MulletaFlixServer` não está registrado e a DLL não foi encontrada no caminho padrão consultado `C:\Program Files\MulletaFlix\Server\Jellyfin.Server.Implementations.dll`. Isso impede atribuir o log ao patch ou afirmar estado global de instalação. Nenhum DB operacional foi acessado; release, CI remota pós-patch e runtime continuam sem validação.

## Rechecagem do anexo repetido — 2026-10-25 07:18 BRT

O anexo recebido novamente contém erros MariaDB 1213 durante `DELETE BaseItemImageInfos` para o mesmo `ItemId`, seguido de novas tentativas do `SaveImagesAsync`; há também heartbeat do Kestrel atrasado em 2,35 s. O `Status Code 200` inicial antecede as falhas e não prova commit das alterações de imagem. O trecho não contém PID, versão/assembly, resultado final das tentativas nem relatório InnoDB, portanto não correlaciona o incidente com a fonte ou runtime local.

`ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 40/40 em Windows/.NET 10 Release. O teste MariaDB `SaveImagesAsync_ConcurrentRefreshesForSameItem` foi selecionado e ignorado por ausência de `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`; o banco operacional não foi consultado. O checkout já contém stripe intra-processo e advisory lock MariaDB para a gravação, mas isso não comprova que o processo do log esteja usando essa implementação. Nenhuma fonte runtime foi alterada nesta rechecagem; homologação formal permanece 2/7 (28,6%), sem crédito, sem instalação ou release.

No portal, a primeira execução da suíte detectou redação histórica da evidência IntroSkipper incompatível com as asserções contratuais. A redação foi alinhada aos fatos já registrados sem alterar os critérios; a suíte completa passou 181/181 na repetição.

## Rechecagem do log e DLL instalada — 2026-10-25 06:12 BRT

O anexo contém 12 comandos `DELETE BaseItemImageInfos`, 11 erros MariaDB 1213, 12 avisos de retry e um heartbeat Kestrel atrasado em 2,35 s. O `HTTP 200` inicial não é o resultado da transação que falhou; o trecho não traz PID/assembly/versão, `SHOW ENGINE INNODB STATUS` nem o resultado final das tentativas. Não permite fechar a causa operacional.

Reexecutei `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` em Windows/.NET 10 Release: 40 aprovados, 0 ignorados, 0 falhas. A DLL instalada no host foi lida sem iniciar o servidor: versão 12.1.35, SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`, sem o marcador do advisory lock `mfx:item:`. Nenhum processo MulletaFlix/Jellyfin estava ativo. A fonte local declara 12.1.32 e contém a proteção, mas o checkout `main` está sujo e não publicada; essa divergência não prova que a DLL observada produziu o log de 23/10. Nenhum banco foi conectado nesta sessão. Formal 2/7 (28,6%), sem crédito; sem instalação, restart, release ou publicação.

## Regressão cross-session de exclusão em MariaDB isolado — 2026-10-25 03:30 BRT

`DeleteItem_WaitsForDatabaseAdvisoryLockHeldByAnotherSession` passou 1/1 em MariaDB 11.4.4 descartável no Windows, loopback `127.0.0.1:33128`, .NET 10 Release. O listener do teste encerrou; a porta 3306 de produção permaneceu ativa e não foi consultada. O job MariaDB Linux já contém o teste, mas a execução hospedada segue pendente. Homologação formal permanece 2/7, sem crédito; runtime/produção e release não foram validados. Dois diretórios temporários descartáveis ficaram em TEMP porque a limpeza foi bloqueada pelo terminal.

## Regressão global e novo recorte do log de deadlock — 2026-10-25 03:11 BRT

Recontagem dos 17 arquivos TRX da regressão `MulletaFlix.sln` Release (`--no-build --no-restore`, execução serial, filtro `FullyQualifiedName!~LiveIntegrationTests`) confirmou 5.287 `Passed`, 255 `NotExecuted`, 0 `Failed`, total 5.542; processo terminou com exit code 0. Os testes Nebula passaram 697/697 em Python 3.10.11 isolado. Uma tentativa de pytest feita com diretório de trabalho na raiz falhou durante a coleta por imports relativos ao projeto; repetida a partir de `Tools/NebulaPython` passou.

O novo anexo registra 12 comandos `DELETE BaseItemImageInfos` falhos e repetidos erros MariaDB 1213 no mesmo item, seguidos de retries; Kestrel também reporta heartbeat atrasado 2,35 s. A fonte local já serializa `SaveImagesAsync` com stripe intra-processo e advisory lock MariaDB, mas o checkout declara 12.1.32 e o log não fornece PID/versão. Assim, não se atribui o log à fonte atual nem se afirma correção em produção. O teste não acessou o banco operacional. Checkout permanece preexistente e sujo; venv descartável de teste ficou em `Tools/NebulaPython/.build/nebula-regression-20261025-0309` após o terminal bloquear sua remoção.

Homologação formal segue 2/7 (28,6%), sem crédito. Não valida runtime instalado, Linux, CI remota, provedores live nem DB operacional; nenhuma release/instalação/publicação ocorreu. O mapa foi conferido contra `ItemPersistenceService.SaveImagesAsync`, `ItemPersistenceDatabaseLock`, os 17 projetos .NET e o runtime de testes Nebula; sem alteração de módulos, APIs, dependências ou arquitetura nesta rodada.

## Rechecagem do anexo idêntico e distribuição do lock — 2026-10-25 18:19 BRT

O arquivo recebido nesta sessão tem o mesmo SHA-256 já registrado (53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2). Repete 12 comandos DELETE BaseItemImageInfos, 11 deadlocks MariaDB 1213 e tentativas de retry. Não apresenta PID/assembly, desfecho das gravações nem diagnóstico InnoDB; o HTTP 200 antecedente não comprova commit.

A inspeção local confirmou que a fonte do checkout ainda usa stripe estática por item e advisory lock MariaDB. git ls-remote confirmou origin/main no commit d51f4f38d2b4148ab49394c001567ad273bd57aa; nesse commit, ItemPersistenceService não chama o helper e o arquivo ItemPersistenceDatabaseLock.cs não existe. O caminho padrão consultado para a DLL instalada não existe neste host, então não há assembly local para correlacionar. Isso confirma uma lacuna de distribuição entre checkout e branch remota, não prova qual binário produziu o incidente.

Nenhum teste foi reexecutado nesta rechecagem; os resultados anteriores (focais 40/40 e MariaDB descartável 14/14) permanecem evidência do checkout, não de produção. Nenhum banco foi acessado. Sem mudança de código, release, instalação ou publicação. Homologação formal segue 2/7 (28,6%), sem crédito. Próximo passo: reconciliar a árvore local com a base remota, revisar o conjunto de alterações antes de publicar e correlacionar PID/assembly + estado InnoDB durante uma ocorrência.

Contrato do portal atualizado após esta anotação passou 197/197; JSON válido, JavaScript sintaticamente válido e git diff --check sem erros (apenas aviso LF/CRLF).

## Rechecagem do binário instalado e testes focados — 2026-10-25 20:32 BRT

O anexo desta sessão repete o SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`. A fonte local examinada serializa `SaveImagesAsync` com stripe por item e `ItemPersistenceDatabaseLock`/`GET_LOCK` MariaDB antes da transação. `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 40/40 em Windows/.NET 10 Release. Dois testes de integração MariaDB para concorrência foram descobertos e ignorados, pois `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada.

A DLL no caminho padrão de instalação é versão 12.1.35 e não contém os literais `ItemPersistenceDatabaseLock` ou `mfx:item:`; nenhum processo MulletaFlix/Jellyfin estava ativo. Isso mostra apenas que o arquivo local consultado não carrega a proteção examinada na fonte; não identifica o assembly que emitiu o log histórico nem prova causa do incidente. Nenhum banco operacional foi acessado. Homologação formal permanece 2/7 (28,6%), sem crédito; sem alteração de runtime, instalação ou release. Próximo passo: reproduzir em MariaDB descartável e capturar PID/assembly, `SHOW ENGINE INNODB STATUS` e resultado final durante uma nova ocorrência.

O contrato local do portal passou 200/200; o JSON e o JavaScript passaram validação sintática, e `git diff --check` terminou sem erros (com avisos LF/CRLF do Git).

## Concorrência entre SaveItems e SaveImagesAsync — 2026-10-25

A cobertura anterior concorria refreshes de imagem entre si, mas não executava simultaneamente a regravação de metadados (`SaveItems`) e o refresh de imagens para o mesmo item. Foi acrescentado `SaveItemsAndSaveImagesAsync_ConcurrentWritesForSameItem_PersistMetadataAndCompleteImageSet`, que sincroniza o início dos dois fluxos, verifica término dentro de 30 segundos e confirma o nome atualizado e o conjunto íntegro de imagens (tipo, path, provider e idioma). O mesmo método foi incluído no filtro MariaDB do workflow CI.

O teste compilou em Windows/.NET 10 Release, mas foi ignorado por ausência de `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`; não houve execução em MariaDB, acesso ao banco operacional, CI hospedada ou produção. A lacuna de integração continua aberta até execução contra MariaDB descartável e runner hospedado observável. Homologação formal continua 2/7 (28,6%), sem crédito.

## Rechecagem do log repetido — 2026-10-25 21:29 BRT

O anexo recebido novamente tem o SHA-256 já registrado (`53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`): 12 `DELETE`s em `BaseItemImageInfos` para o mesmo item, 11 respostas MariaDB 1213 e retries; a primeira linha HTTP 200 não comprova o commit. A fonte local mantém serialização por stripe e advisory `GET_LOCK`. Testes unitários/reais de provider `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passaram 46/46 em Windows/.NET 10 Release. Sem `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`, integração cross-session não foi executada. O log não identifica PID/assembly nem o resultado final; não confirma runtime nem produção.

Na reauditoria paralela do restore, `BackupRestoreDirectoryTests` passou 36/44, com 8 casos de outras plataformas ignorados. A política de namespace atual permite criação de subpastas nos pais e valida mais estritamente o alvo; negar `FILE_APPEND_DATA` em todos os ancestrais bloqueou testes em caminhos Windows existentes e foi revertido. Não houve alteração permanente de código, instalação, release ou acesso ao banco operacional. Homologação formal continua 2/7 (28,6%), sem crédito.

## Rechecagem do mesmo log e da DLL instalada — 2026-10-25 22:32 BRT

O anexo recebido tem o mesmo SHA-256 do incidente já registrado (`53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`). A DLL no caminho padrão local é 12.1.35; o checkout declara 12.1.32. A tag 12.1.35 não contém o lock de faixa e o advisory `GET_LOCK` presentes na fonte local. Isso confirma divergência entre a versão instalada consultada e a proteção local, mas não identifica o processo que emitiu o log nem prova igualdade do arquivo com o artefato publicado.

`ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 46/46 em Windows/.NET 10 Release. A integração MariaDB os dois testes foram descobertos e ignorados porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` está ausente (0 aprovados, 2 ignorados); o banco operacional não foi acessado. O log não traz PID/assembly, desfecho das gravações ou relatório InnoDB. A correção permanece apenas no checkout; sem release, instalação ou crédito formal. Formal 2/7 (28,6%).

## Cobertura do orçamento de retry — 2026-10-25 22:37 BRT

Adicionadas regressões unitárias para `SaveImagesAsync`: erros transitórios MariaDB 1205 e 1213 devem propagar a exceção após exatamente três tentativas; erro permanente deve parar após uma. Os três casos focados passaram; `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 49/49 em Windows/.NET 10 Release e `dotnet format style --verify-no-changes` passou. Apenas o arquivo de testes mudou no código; o comportamento de runtime não foi alterado.

A cobertura unitária não substitui a integração: os dois cenários MariaDB continuam sem execução com banco descartável nesta sessão. Não houve acesso ao banco operacional nem validação de DLL/runtime/produção. Homologação formal permanece 2/7 (28,6%), sem crédito ou release. Ver [[Sessões/2026-10-25-MulletaFlix-image-retry-budget-tests-2237]].

## Refresh localizado com proveniência — 2026-10-25 22:44 BRT

Adicionada regressão local em `ItemImageProviderRefreshTests`: candidata de capa em português é baixada por provider simulado e seu `ProviderName`/`Language` permanecem na imagem após `RefreshImages`. `ItemImageProviderTests` passou 64/64 e `ProviderManagerTests` 76/76 em Windows/.NET 10 Release; formatter passou. Nenhuma API externa foi chamada.

O `SaveImage` do teste é mockado. A persistência EF/MariaDB permanece coberta separadamente por teste opt-in; essa integração não foi executada porque não há conexão descartável disponível. Não afirmar que o arquivo foi escrito ou que o banco persistiu a proveniência. Formal 2/7 (28,6%), sem crédito/release. Ver [[Sessões/2026-10-25-MulletaFlix-localized-image-refresh-provenance-2244]].

## Rechecagem focal do anexo duplicado — 2026-10-25 22:53 BRT

O anexo repetiu o SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` e não acrescenta dados de PID/assembly ou resultado final. `SaveImagesAsync_WaitsForSameItemPersistenceLockBeforeOpeningDatabaseContext` passou 1/1 em Windows/.NET 10 Release. Esse teste confirma a espera pelo lock local, não a identidade do runtime nem a serialização entre processos MariaDB. A integração MariaDB descartável não foi executada. Sem mudança de runtime ou release; homologação formal continua 2/7 (28,6%), sem crédito.
## Rechecagem repetida do anexo — 2026-10-26 00:05 BRT

O anexo recebido nesta sessão tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, exatamente igual ao incidente já analisado. Repete 12 `DELETE BaseItemImageInfos`, 11 deadlocks MariaDB 1213, retries e heartbeat Kestrel de 2,35 s; não inclui PID/assembly nem desfecho das gravações. A linha HTTP 200 antecedente não prova commit.

`ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 49/49 em Windows/.NET 10 Release no checkout atual. Isso verifica regras unitárias e locks simulados, não concorrência real em MariaDB nem qual binário produziu o log. Nenhuma integração MariaDB, CI hospedada, runtime instalado ou produção foi validada; sem alteração de runtime ou release.

## Persistência localizada integrada em MariaDB descartável — 2026-10-26 01:45 BRT

MariaDB 11.4.13 isolado em `127.0.0.1:33099`: `ServerPersistenceMariaDbTests` passou 17/17 e `IntroSkipper.Integration.Tests` 57/57 em Windows/.NET 10 Release. A regressão exercita TMDb localizado → refresh → `ImageSaver` real → bytes/caminho → metadados EF/MariaDB (`ProviderName`/`Language`). Servidor de teste encerrado e porta fechada. Datadir temporário mantido após a ferramenta bloquear remoção recursiva. Sem execução Linux hospedada, correlação do binário do log, validação runtime/produção ou crédito formal; homologação permanece 2/7 (28,6%). Ver [[Sessões/2026-10-26-MulletaFlix-localized-image-mariadb-flow-wiring-0126]].

## Reenvio idêntico do log de deadlock — 2026-10-26 03:57 BRT

O anexo desta sessão tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, byte-idêntico ao log já auditado. Repetiu MariaDB 1213 em `DELETE BaseItemImageInfos` para o item do recorte; o HTTP 200 anterior não comprova commit. Não há PID, assembly, versão carregada ou desfecho das retentativas. O log não prova que a mitigação local `SemaphoreSlim` + `GET_LOCK` estava instalada. Nenhuma mudança na persistência de imagens nesta sessão; nenhum banco acessado. Produção permanece sem confirmação.

## Correlação do anexo repetido e revalidação focal — 2026-10-26 04:29 BRT

O anexo recebido tem novamente o SHA-256 já registrado (`53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`), sem PID, versão do processo, resultado final ou relatório InnoDB. A DLL instalada em `C:\Program Files\MulletaFlix\Server\MulletaFlix.Server.Implementations.dll` continua 12.1.35, com SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`, e não contém os marcadores literais `ItemPersistenceDatabaseLock`/`mfx:item:`. A fonte local coordena `SaveImagesAsync`, `SaveItems`, `DeleteItem` e `FixBaseItemImageInfos` por stripes e advisory locks MariaDB; o teste focal `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` foi reexecutado em Windows/.NET 10 Release: 49 aprovados, 0 ignorados/falhas.

Isso confirma divergência entre a fonte local e o arquivo instalado consultado, mas não identifica o processo do log nem prova que a DLL consultada o emitiu. O anexo é repetido, não evidência de uma nova falha após a correção; a causa operacional continua pendente até correlação PID/assembly e diagnóstico InnoDB. Nenhum banco operacional foi acessado. A árvore de trabalho está amplamente modificada e mistura diversas frentes; não foi criada nem publicada release. Homologação formal permanece 2/7 (28,6%), sem crédito.

## Revalidação do anexo repetido — 2026-10-26 04:55 BRT

O arquivo anexado nesta rodada mantém o SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual às ocorrências já registradas. A fonte atual foi relida: `SaveImagesAsync` serializa por stripe intra-processo e `ItemPersistenceDatabaseLock` adquire `GET_LOCK` MariaDB antes da transação; retries para códigos 1205/1213 permanecem limitados a três tentativas. `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 49/49 em Windows/.NET 10 Release; contrato da homologação passou 250/250.

Esta revalidação não correlaciona o erro ao processo instalado, não demonstra o resultado final das gravações e não substitui concorrência MariaDB. PID/assembly, relatório InnoDB, CI remota, runtime/produção continuam pendentes. Não houve mudança de runtime nem release; homologação formal permanece 2/7 (28,6%), sem crédito.

## Reenvio idêntico do log e regressão focal — 2026-10-26 07:35 BRT

O anexo recebido nesta sessão tem o mesmo SHA-256 do incidente já auditado (`53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`): 12 exclusões concorrentes de `BaseItemImageInfos` para o mesmo item, erros MariaDB 1213 e aviso de heartbeat Kestrel. A linha HTTP 200 antecede os deletes e não prova commit. O checkout atual serializa mutações por item com stripes locais e advisory locks MariaDB; a regressão `ItemPersistenceServiceTests` foi recompilada em Windows/.NET 10 Release e passou 34/34. Esta sessão não tinha MariaDB descartável configurado para repetir a integração. O log não identifica PID/assembly/versão e não prova que a correção esteja instalada; `HEAD` ainda não contém o helper local. Sem validação de produção, sem release e sem crédito de homologação formal (2/7, 28,6%).

## Revalidação concorrente da persistência de imagens — 2026-10-26 15:57 BRT

O log anexado tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao incidente já registrado. Ele não fornece PID/assembly nem desfecho dos retries. `ItemPersistenceServiceTests` passou 35/35 em Windows/.NET 10 Release. `ServerPersistenceMariaDbTests` passou 24 aprovados, 1 ignorado e 0 falhas/25 contra MariaDB 11.4.4 descartável, porta 33219. Foram executados os testes concorrentes entre `SaveImagesAsync`/`SaveItems`, múltiplos stripes e `DeleteItem`. O único skip requer marcadores do harness de restart, aprovado separadamente 1/1. A instância foi criada com datadir e processo exclusivos, validada por PID/executável/datadir e encerrada; porta 33219 fechada. MariaDB operacional 3306 não acessado.

Build produziu avisos existentes `xUnit1051` e `SA1402`, sem falhas. Nenhum código de runtime mudou nesta reexecução. O resultado valida o checkout local, não o runtime que gerou o log nem produção. CI hospedada, runtime instalado e produção seguem pendentes; homologação formal 2/7 (28,6%), sem crédito ou release. Mapa técnico conferido: nenhum módulo, API, classe, dependência ou fluxo mudou. Ver [[Sessões/2026-10-26-MulletaFlix-t56-concurrent-image-persistence-rerun-1557]].

## Regressão ampla e identidade do runtime — 2026-10-26 16:10 BRT

A regressão local Windows/.NET 10 Release passou 5.379/5.475, 96 ignorados e zero falhas no rerun sem build/restore; a tentativa fresh retornou exit 1 sem diagnóstico preservado. Filtro excluiu os projetos Integration. Revisão read-only encontrou os escritores de BaseItemImageInfos somente no ItemPersistenceService, e a rotina FixDates adquire as 16 advisory locks. DLL consultada instalada 12.1.35 difere do checkout 12.1.32; nenhum processo do servidor estava ativo sob Program Files/MulletaFlix. Isso não identifica o processo do log histórico nem valida runtime. Formal 2/7, sem release. Ver [[Sessões/2026-10-26-MulletaFlix-server-regression-1610]].

## Regressão compilada do servidor — 2026-10-26 16:23 BRT

A nova execução com build da solution Release terminou exit 0. Server.Implementations.Tests: 1.693 aprovados, 93 ignorados, 0 falhas; Api.Tests: 789 aprovados, 3 ignorados, 0 falhas. O filtro excluiu os testes dos dois projetos Integration. A execução fresh anterior exit 1 segue sem diagnóstico preservado; o rerun com build concluiu. Sem alteração de runtime, MariaDB/Mongo opt-in, CI hospedada ou produção. Ver [[Sessões/2026-10-26-MulletaFlix-server-regression-compiled-1623]].

## Reenvio do log idêntico — 2026-10-26 16:38 BRT

O anexo atual tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao incidente já documentado: deleções concorrentes de `BaseItemImageInfos` para o mesmo item, deadlocks 1213 e heartbeat atrasado. A resposta HTTP 200 precedente não comprova commit da persistência. `ItemPersistenceServiceTests` e `ItemPersistenceDatabaseLockTests` recompilaram e passaram 50/50 em Windows/.NET 10 Release. A fonte local já serializa por stripe e advisory lock MariaDB; como o log não contém PID/assembly/versão, não é possível atribuir a causa ao checkout ou afirmar que a correção esteja instalada. MariaDB de produção não foi acessada. Sem alteração de runtime/release; homologação 2/7.

## Regressão MariaDB de persistência — 2026-10-26 16:43 BRT

`ServerPersistenceMariaDbTests` recompilou e passou **24/25**; 1 skip é o caso de restart que exige marcadores específicos. Executou com MariaDB 11.4.4 isolado em loopback/porta 33167, grants habilitados e `max_connections=256`, cobrindo os fluxos concorrentes existentes de `SaveImagesAsync`, `SaveItems`, `DeleteItem`, locks advisory e recuperação de conexão. Duas tentativas iniciais expuseram requisitos do harness (não usar `--skip-grant-tables`; elevar o limite de conexões), não defeitos da aplicação; a repetição configurada passou. O processo MariaDB de teste foi encerrado, porta 33167 fechada; listener 3306/PID 31236 permaneceu intacto e sem conexão. Os diretórios descartáveis ficaram sob `%TEMP%` porque a ferramenta bloqueou a remoção recursiva. Carga prolongada, CI hospedada, runtime instalado e produção continuam pendentes. Homologação formal 2/7, sem release.

## Reenvio idêntico do log — 2026-10-26 18:20 BRT

O novo anexo tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual aos arquivos já auditados. Repete `DELETE BaseItemImageInfos` concorrentes para o mesmo item, deadlocks MariaDB 1213, retries e heartbeat Kestrel atrasado. O trecho termina durante outro retry: não comprova o resultado final nem identifica PID, assembly ou versão carregada. O HTTP 200 anterior não confirma commit. A fonte local mantém lock por faixa no processo e advisory `GET_LOCK` MariaDB; isso não demonstra que o binário do incidente contém a mitigação. Nenhuma alteração de runtime, teste ou banco nesta sessão; a integração MariaDB já registrada cobre o checkout, não este runtime. Sem nova release ou crédito formal; 2/7 continua sendo a última evidência registrada.

## Reenvio do log idêntico — 2026-10-26 19:27 BRT

O anexo tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual aos registros anteriores. Repete deadlocks MariaDB 1213 durante `DELETE BaseItemImageInfos` para o mesmo item, com retries; não inclui PID/assembly nem resultado final. O HTTP 200 antecedente não comprova commit. O checkout já contém stripe por item e advisory `GET_LOCK`, mas esta repetição não comprova qual runtime emitiu o log nem a correção em produção; nenhum banco operacional foi consultado.

Como validação independente, `bash -n` passou nos nove scripts shell do diretório `tools/release` em Git Bash/MSYS. WSL não tem distribuição instalada, então nenhum script Linux ou instalador foi executado. Contrato do portal passou 298/298. Sem mudança de runtime ou release; auditoria permanece 5.465/5.493 e homologação formal 2/7 (28,6%), sem crédito. Mapa conferido; estrutura inalterada.
## Imagens remotas legadas sem proveniência — 2026-10-26 21:23 BRT

O refresh automático só substitui imagem singular antiga quando a proveniência
remota (`ProviderName` e `Language`) é conhecida. Imagens sem esses campos são
preservadas porque podem ser arte manual. Isso protege substituições manuais,
mas deixa capas remotas antigas em idioma errado sem reparo automático. Não é
seguro inferir a autoria pelo nome ou caminho do arquivo. A correção exige
identificar imagens manuais e remotas legadas ou criar uma ação explícita de
recuperação que preserve a arte manual. TMDb/CDN, item real, runtime instalado
e produção não foram consultados nesta auditoria. Homologação formal 2/7; sem
release. Ver sessão
`2026-10-26-MulletaFlix-nebula-full-mode-startup-audit-2123`.

## Refresh de imagem remota sem idioma registrado — 2026-10-26 21:35 BRT

`ItemImageProvider.RefreshImages` agora trata arte com `ProviderName` conhecido,
mas `Language` nulo/vazio, como remota cuja língua precisa ser reavaliada. O
refresh usa a preferência pt-BR configurada e mantém inglês como fallback. A
resolução do idioma global também passa a ser acionada nesses registros. Arte
sem `ProviderName` continua preservada, pois pode ser manual. Isto não recupera
imagens antigas que perderam ambos os campos de proveniência; elas requerem
substituição administrativa explícita. Testes focais: 4/4; filtro completo do
refresh de imagens: 47/47 em Windows/.NET 10 Release. Nenhum TMDb/CDN, banco,
runtime instalado ou produção acessado. Formal permanece 2/7, sem crédito ou
release.

## Reenvio idêntico do log de deadlock — 2026-10-26 23:47 BRT

O anexo recebido nesta sessão tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual aos anexos anteriores. Repete deadlocks MariaDB 1213 no `DELETE BaseItemImageInfos` para o mesmo item, retries e heartbeat do Kestrel atrasado em 2,35 s. O HTTP 200 inicial não demonstra commit; o recorte termina antes do desfecho e não identifica PID/assembly emissor.

`ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` recompilou em Windows/.NET 10 Release: 52 aprovados, 0 ignorados, 0 falhas. A fonte local declara 12.1.32 e serializa gravações por stripe e advisory `GET_LOCK`; `origin/main` está em `d51f4f38`, oito commits à frente do checkout, sem a proteção. A DLL consultada instalada declara 12.1.35, SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`; contém o campo de identidade atual do retry, mas não os marcadores do lock. Nenhum processo do servidor estava ativo. Isso confirma divergência de distribuição, mas não atribui o log à DLL consultada nem confirma que o patch tenha chegado à produção. A integração MariaDB não foi executada nesta sessão, pois a configuração de teste está ausente; banco operacional não acessado. Sem mudança de código ou release. Homologação formal continua 2/7 (28,6%), sem crédito.

## Confirmação do log de deadlock idêntico — 2026-10-27 00:14 BRT

O arquivo recebido novamente tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico às ocorrências anteriores. Reconfirmei na instalação local a DLL `MulletaFlix.Server.Implementations.dll` 12.1.35, SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`, sem `ItemPersistenceDatabaseLock` nem `mfx:item:`. Nenhum processo do servidor estava ativo nesta leitura. A fonte deste checkout já possui stripe local e lock advisory MariaDB; o anexo não tem PID/versão nem resultado final dos retries, portanto não identifica o emissor nem prova a falha final. Banco operacional e integração MariaDB não foram acessados nesta sessão. Sem release ou confirmação de correção em produção.

## Revalidação do log repetido — 2026-10-27 02:58 BRT

O anexo desta sessão tem SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2, igual ao incidente já registrado. Ele repete deadlocks MariaDB 1213 no DELETE BaseItemImageInfos para o mesmo item e retentativas; não contém PID, AssemblyVersion nem resultado final. A linha HTTP 200 anterior não comprova commit. O checkout atual contém locks por faixa e advisory GET_LOCK, mas esta revalidação não executou MariaDB nem identificou o binário emissor. Nenhum código de produto, banco operacional, runtime ou instalação foi alterado; sem release.
## Revalidação do anexo — 2026-10-27 03:07 BRT

O arquivo recebido tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao incidente já registrado. O recorte repete deadlocks MariaDB 1213 durante `DELETE BaseItemImageInfos` para o mesmo item e logs de retry na tentativa 2; termina truncado e não informa resultado final, PID ou `AssemblyVersion`. O `HTTP 200` que antecede o erro não prova commit das imagens.

A fonte deste checkout contém lock por stripe de item e lock advisory MariaDB. A DLL instalada localmente continua na versão 12.1.35 e não contém `ItemPersistenceDatabaseLock` nem `mfx:item:`; nenhum processo MulletaFlix/Jellyfin estava ativo na verificação. Isso confirma divergência entre checkout e instalação local, mas não identifica qual binário emitiu o log. Testes focais em Windows/.NET 10 Release passaram 52/52. A integração MariaDB não foi executada porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada. Causa de produção e recuperação final das imagens continuam sem confirmação.
## Revalidação focal do log repetido — 2026-10-27 05:23 BRT

O anexo tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico ao incidente já auditado. Repete deadlocks MariaDB 1213 durante `DELETE BaseItemImageInfos` para o mesmo item; não contém PID/AssemblyVersion nem resultado final, e o HTTP 200 antecedente não comprova commit. O checkout serializa gravações de imagens por stripe de item e usa advisory lock para mutações MariaDB.

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~SaveImagesAsync_WaitsForSameItemPersistenceLockBeforeOpeningDatabaseContext` passou 1/1 em Windows/.NET 10. Integração MariaDB não foi executada: variável opt-in ausente, Docker ausente e WSL não instalado; serviço operacional não foi acessado. A revisão estática paralela do gate de restore Linux não encontrou novo achado, mas não executou os 14 testes privilegiados.

Sem alteração de runtime, banco ou instalação; homologação formal permanece 2/7 (28,6%), auditoria agregada 5.465/5.493, sem crédito ou release. Portal atualizado no checkout local, sem publicação remota. Mapa técnico conferido: nenhuma estrutura mudou. Ver [[Sessões/2026-10-27-MulletaFlix-image-deadlock-log-rerun-0523]].

## Reenvio do deadlock e regressão focal — 2026-10-27 05:41 BRT

O anexo recebido tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao log já registrado. Repete deadlocks MariaDB 1213 em `DELETE BaseItemImageInfos` para o mesmo item; não informa PID/AssemblyVersion nem o resultado final das retentativas. O HTTP 200 inicial não comprova commit dessas gravações.

`ItemPersistenceServiceTests` e `ItemPersistenceDatabaseLockTests` passaram 52/52 em Windows/.NET 10 Release (`dotnet test ... --configuration Release --no-restore --filter "FullyQualifiedName~ItemPersistenceServiceTests|FullyQualifiedName~ItemPersistenceDatabaseLockTests"`). Isso cobre a fonte local e simulações de retry/lock; não reproduz o deadlock em MariaDB real nem correlaciona o log ao runtime instalado. A integração foi omitida porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada e existe um `mysqld` ativo não identificado; ele não foi acessado. Nenhum serviço, banco operacional, instalação ou release foi alterado.

Homologação formal permanece 2/7 (28,6%) e auditoria 5.465/5.493, sem crédito. CI hospedada, runtime emissor, Linux e produção seguem pendentes. Mapa técnico conferido contra a persistência e seus testes; módulos, APIs e dependências não mudaram.

## Log de deadlock reenviado e correção de órfãos — 2026-10-27 10:03 BRT

O anexo desta sessão tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual aos anteriores. Ele registra deadlocks MariaDB 1213 repetidos no `DELETE BaseItemImageInfos` e retries sem PID/AssemblyVersion. A mensagem antiga não corresponde ao template atual de retry, que registra PID e versão; isto indica código/runtime diferente ou anterior, sem identificar qual binário produziu o log. O HTTP 200 antecedente não confirma commit, e o alerta de heartbeat Kestrel não prova causalidade.

O código atual mantém stripe por item, advisory `GET_LOCK` e retry limitado; nenhuma mudança foi feita nessa lógica nesta sessão. O runtime instalado/produção não foi consultado. Separadamente, foi corrigido e reproduzido em MariaDB descartável o vazamento de linhas `Peoples` órfãs após `ON DELETE CASCADE`; evidência e validação estão no roadmap técnico e no evento de homologação. Formal 2/7, sem crédito/release.

## Rechecagem do log MariaDB repetido — 2026-10-27 05:59 BRT

O novo anexo tem o mesmo SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` do incidente anterior. Ele repete o deadlock 1213 no `DELETE BaseItemImageInfos` para o mesmo item e não contém PID, versão do assembly ou resultado final. A mensagem de retry não corresponde à mensagem da fonte atual, que inclui PID e versão. A fonte atual usa stripe local por item e `GET_LOCK` no MariaDB; testes unitários `ItemPersistenceServiceTests` passaram 37/37 em Release. Isso não prova a versão executada na instalação nem reproduz o caso concorrente no MariaDB. Formal 2/7 e auditoria 5.465/5.493 permanecem sem crédito.
## MariaDB real descartável — regressão de persistência — 2026-10-27 06:07 BRT

`ServerPersistenceMariaDbTests` passou 25/26 em Windows/.NET 10 Release: 25 aprovados, 1 ignorado (restart exige marcadores do harness), 0 falhas. Executado com MariaDB 11.4.4, PID 35200, porta dinâmica 49393 e datadir exclusivo; shutdown gracioso confirmou porta fechada. A instância operacional 3306/PID 31236 não foi acessada. O restart isolado já tem evidência anterior 1/1, mas esta suíte não reproduziu esse cenário. Auditoria agregada e homologação formal permanecem inalteradas.
## Regressão concorrente em MariaDB descartável — 2026-10-27 08:19 BRT

O anexo reenviado tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico às cópias anteriores. Repete o deadlock MariaDB 1213 no `DELETE BaseItemImageInfos` para o mesmo item; o recorte não informa PID/AssemblyVersion ou resultado final, e o HTTP 200 antecedente não confirma commit.

`SaveImagesAsync_ConcurrentRefreshesForSameItem_PersistOneCompleteImageSet` passou 1/1 em Windows/.NET 10 Release com MariaDB 11.4.4 isolado em loopback/porta 51588. A instância temporária foi encerrada. A fonte do checkout coordena a gravação com stripe local e advisory `GET_LOCK` por schema/stripe. A saída de retry do anexo não corresponde à mensagem atual que registra PID/versão, portanto não identifica a DLL que o emitiu. MariaDB operacional 3306/PID 31236 não foi acessado; nenhuma instalação ou runtime foi alterado.

Este resultado valida o teste e a fonte local sob concorrência, não identifica nem certifica o runtime emissor, CI hospedada ou produção. Homologação formal permanece 2/7 (28,6%), auditoria 5.465/5.493 sem crédito; sem release. Mapa técnico conferido: sem alteração de módulos, APIs, classes públicas ou dependências.
