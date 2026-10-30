# Autorização administrativa do GetAvatar

## Estado atual — implementação por handle — 2026-10-20

Os arquivos do catálogo GetAvatar agora são acessados através de `AvatarFileStore`, ancorado a um handle do diretório aberto no construtor. `AvatarService.OpenAvatarRead` retorna um stream já aberto e validado; os endpoints e a cópia para o perfil não reabrem o caminho por nome. A criação usa criação exclusiva relativa ao diretório. A exclusão persiste primeiro a remoção do catálogo/mapeamentos e só então remove o arquivo; se a persistência falhar, restaura listas e snapshot do XML e mantém o arquivo. Se a exclusão posterior falhar, a remoção já persistida é mantida e um aviso registra possível arquivo órfão.

No Windows, abertura e exclusão usam handles relativos com `NtCreateFile`/`SetFileInformationByHandle`. No Linux, `openat`/`statx` operam relativamente ao descritor raiz; a máscara exigida inclui `STATX_MODE`, e a raiz precisa ser diretório real, pertencer ao UID efetivo do servidor e não permitir escrita por grupo/outros. A leitura abre primeiro o inode por `O_PATH|O_NOFOLLOW`, confirma tipo/identidade e reabre os dados por `/proc/self/fd`; portanto, procfs é requisito. A exclusão revalida a identidade após a persistência e chama `unlinkat` por nome, ainda não atômico em relação ao descritor do arquivo. Há uma janela entre essa última revalidação e `unlinkat` para processos com o mesmo UID efetivo que consigam modificar a pasta. Linux não foi executado nesta máquina; três testes Linux foram ignorados no Windows. macOS não tem implementação e é deliberadamente recusado. Não declarar mitigação completa de corrida nem compatibilidade Linux até validar ABI, libc, procfs e arquivos especiais em runtime Linux.

Evidência local de 2026-10-20: build `Jellyfin.Api.Tests` Release para `win-x64` passou; execução focal `AvatarControllerUserAvatarAuthorizationTests` + `AvatarFileStoreTests` passou 38, ignorou 3 testes Linux e teve zero falhas (41 total). O relatório amplo existente agrega 17 TRX: 5.070 aprovados, 68 `NotExecuted`, zero falhas/5.138; `Jellyfin.Api.Tests` contabilizou 763 aprovados e 3 ignorados, e os dois projetos Integration selecionaram zero casos pelo filtro. Um build `--no-restore --no-incremental` sem RID falhou ao não resolver o código gerado de `MimeTypes`; o build Windows `-r win-x64 --no-restore` passou. Esses resultados não equivalem a Linux/MariaDB/HTTP integrado. Cobertura não substitui Linux/macOS, armazenamento real, instalação ou produção. A homologação continua em andamento e nenhuma release foi criada.

## Histórico de auditoria — antes da implementação por handle

## Auditoria de acesso por handle — 2026-10-20

Revalidei os consumidores do caminho obtido por `GetAvatarPath`: os dois endpoints de imagem do `AvatarController` chamam `File.Exists` e depois `File.OpenRead` pelo caminho; `SetUserAvatarCoreAsync` também abre o caminho retornado com `File.OpenRead`. `DeleteAvatar` valida reparse/containment e em seguida chama `File.Delete` pelo caminho. Essas sequências mantêm uma janela TOCTOU se um processo capaz de alterar a árvore substituir um componente após a validação. `IsPathWithinRoot` continua útil contra traversal e symlinks estáveis, mas não é uma operação ancorada a handle.

Reexecutei `AvatarControllerUserAvatarAuthorizationTests` em Release: 32/32 passaram, sem ignorados. Isso confirma casos atuais de autorização e containment, mas não demonstra resistência a rename/junction concorrente. A documentação oficial do Windows recomenda `FILE_FLAG_OPEN_REPARSE_POINT` ao abrir objetos reparse; isso isoladamente não resolve todos os ancestrais nem fornece uma implementação Linux/macOS equivalente. Implementação multiplataforma ancorada em handles/descritores e testes concorrentes permanecem pendentes. Nenhuma alteração runtime ou release nesta auditoria.

### Auditoria de estratégia nativa multiplataforma — 2026-10-20 00:12 (-03)

A revisão das APIs primárias detalhou que `File.OpenHandle(path)` não oferece resolução relativa à raiz nem opção gerenciada de não seguir reparse points em todos os componentes. No Windows, o candidato é caminhar componente por componente com `NtCreateFile`, `OBJECT_ATTRIBUTES.RootDirectory` apontando ao handle do diretório pai e `OBJ_DONT_REPARSE`; manter os handles ancestrais abertos e abrir/ler/excluir o objeto final pelo handle, nunca validar e depois voltar a `File.OpenRead(path)`/`File.Delete(path)`. `FILE_SHARE_DELETE` e diferenças entre filesystem/SMB precisam de política e testes explícitos. `SetFileInformationByHandle` marca o objeto aberto, mas não prova que a entrada ainda esteja ligada ao nome esperado após rename concorrente.

No Linux 5.6+, `openat2` com `RESOLVE_BENEATH | RESOLVE_NO_SYMLINKS` pode restringir a resolução relativa; para kernels anteriores, a alternativa é abrir cada componente com `openat`/`O_NOFOLLOW` mantendo os descritores, sem fallback por pathname. `unlinkat` relativo ao diretório pai remove a entrada daquele diretório. O número de syscall `openat2` depende da ABI e não deve ser fixado sem despacho correto. Para macOS, XNU tem flags de resolução sem symlink/abaixo da raiz, mas suporte e ABI da versão mínima precisam ser comprovados via SDK e execução real antes de habilitar a estratégia.

Fontes: [NtCreateFile](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntcreatefile), [OBJECT_ATTRIBUTES](https://learn.microsoft.com/en-us/windows/win32/api/ntdef/ns-ntdef-_object_attributes), [SetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle), [Linux openat2(2)](https://man7.org/linux/man-pages/man2/openat2.2.html), [Linux open(2)](https://man7.org/linux/man-pages/man2/open.2.html), [Linux unlinkat(2)](https://man7.org/linux/man-pages/man2/unlinkat.2.html), [XNU fcntl.h](https://github.com/apple/darwin-xnu/blob/main/bsd/sys/fcntl.h).

Resultado: desenho e limites documentados; nenhum runtime/teste foi alterado ou executado nesta rodada. Não alegar mitigação. Próximo gate segue sendo implementar um primitive seguro por OS (ou bloquear operação onde não suportado), validar a semântica de rename/link do objeto excluído e construir testes concorrentes com sentinela externa nas plataformas suportadas.

### Reexecução da suíte focada — 2026-10-20 00:18 (-03)

`AvatarControllerUserAvatarAuthorizationTests` foi executada novamente em Windows/.NET 10 e passou 32/32, sem skips ou falhas. O WSL não está instalado nesta máquina; esta evidência não cobre Linux/macOS nem corridas de rename/symlink. Nenhuma mudança runtime, instalação ou release ocorreu. O resultado serve apenas como revalidação dos cenários atuais de autorização e containment estável; TOCTOU permanece aberto.

## Rollback não regrava arquivo de perfil externo — 2026-10-19 21:42 (-03)

Antes de substituir uma foto, `AvatarService.SetUserAvatarAsync` captura os arquivos que pode precisar restaurar. O caminho da foto anterior vem do registro do usuário e não deve ser presumido como pertencente à pasta dele. O snapshot agora inclui a foto anterior e o destino somente quando `FileSystemHelper.IsPathWithinRoot` confirma containment sem componentes reparse existentes. Com isso, uma falha no provider não regrava bytes num arquivo externo que tenha sido alterado durante a operação. A reconciliação do vínculo persistido continua usando o fluxo de rollback já existente.

`SetUserAvatarAsync_WhenSaveFails_DoesNotRestoreExternalProfileFileSnapshot` falhou antes da correção porque o rollback repôs `[1, 2, 3]` sobre bytes externos concorrentes `[91, 92, 93]`; após a alteração, a classe focal passou 28/28 e `Jellyfin.Api.Tests` Release passou 753/753. Esses testes usam arquivo temporário e mocks. Eles não fecham TOCTOU entre a checagem do caminho e a operação de arquivo, nem substituem MariaDB integrada, runtime instalado ou teste de produção.

Homologação atualizada no portal em `Mulletaflix-Portal:main`, commit `398e3a17522f4b0b1c983b3d77bd7537db7185e5`. Contrato do portal 154/154; JS, JSON e diff check passaram. O JSON público retornou HTTP 200 com o evento novo. Nenhuma GitHub Release do servidor foi criada; gates de runtime, produção e TOCTOU continuam abertos.

## Segurança de limpeza do caminho de perfil — 2026-10-19 21:04 (-03)

Revisão adversarial achou que casing distinto no Windows podia apagar o arquivo ativo e que containment lexical permitia limpeza por dentro de junction/symlink. `MediaBrowser.Controller.IO.FileSystemHelper` agora centraliza `PathsEqual` (comparação normalizada e sensível a case só em sistemas case-sensitive) e `IsPathWithinRoot` (containment relativo, rejeita reparse points em todos os componentes existentes). Upload nativo e GetAvatar usam os dois helpers antes de apagar perfil antigo.

Regressões: caminho por symlink rejeitado, irmão com prefixo parecido rejeitado, igualdade case-aware, GetAvatar não apaga perfil de mesmo caminho com casing diferente no Windows, e falha ao salvar mapping preserva arquivo antigo de nome único. Focal: 45/45; `Jellyfin.Api.Tests` Release: 749/749, zero falhas/skips. Suíte Server Integration: 3 aprovados/142 ignorados (145), pois MariaDB descartável não foi configurada; teste de upload HTTP/MariaDB segue não executado.

Limite remanescente da revisão adversarial: existe janela TOCTOU entre checar reparse points e excluir pelo caminho, caso processo com permissão para modificar a pasta do usuário troque um diretório por symlink/junction nesse intervalo. Os testes cobrem links estáveis, não substituição concorrente. Registrar como gate de hardening/ACL antes de afirmar segurança completa. Sem runtime/produção/release.

### Investigação de exclusão ancorada em handle — 2026-10-19 21:11 (-03)

A leitura da documentação oficial confirma opções técnicas, mas não altera o estado do risco. No Windows, `CreateFileW` pode abrir o objeto sem seguir o reparse point final e `SetFileInformationByHandle` pode marcar para exclusão o objeto associado ao handle. No Linux, `openat` com `O_NOFOLLOW` e `unlinkat` permite operar relativamente a um descritor de diretório aberto. Portanto, repetir checagens de caminho gerenciado não fecha a janela TOCTOU; uma correção precisa de implementação nativa ancorada e testes concorrentes.

Nenhuma primitiva foi adicionada nesta investigação. O comportamento atual continua como descrito acima. Ainda faltam implementar e exercitar o fluxo em Windows/Linux, comprovar que a pasta pai configurada não pode ser renomeada/substituída por identidade não confiável, e decidir a política para macOS antes de declarar hardening multiplataforma. Sem instalação/runtime/produção/release.

## GetAvatar limpa arquivo nativo anterior após substituição bem-sucedida — 2026-10-19 20:52 (-03)

`AvatarService.SetUserAvatarAsync` salva o avatar selecionado pelo plugin no caminho canônico `profile.<ext>`. Após atualizar `ProfileImage` e persistir o mapeamento do plugin com sucesso, remove o arquivo nativo anterior se for diferente do novo caminho e resolver dentro da pasta de configuração do usuário. Falhas/ambiguidade na exclusão são logadas sem invalidar a associação recém-persistida.

`AvatarControllerUserAvatarAuthorizationTests`: 24/24; `Jellyfin.Api.Tests` Release completa: 745/745, sem skips/falhas. Regressão observada RED antes do cleanup e GREEN depois. `ImageControllerProfileUploadTests.PostUserImage_ReplacesPersistedImageAndRemovesOldFile` compilou e foi descoberto, mas ficou ignorado por ausência de `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`; upload HTTP com persistência MariaDB permanece pendente. Sem instalação/runtime/produção; nenhuma release criada.

## Upload nativo de avatar preserva imagem anterior após falha — 2026-10-19 20:38 (-03)

`ImageController.PostUserImage` agora salva o novo arquivo em caminho exclusivo antes de remover o vínculo existente. Depois do save, a rota relê e reautoriza o usuário sob o lock compartilhado, remove a relação antiga e persiste a nova. Se o save falhar, o vínculo anterior permanece. Se a persistência falhar depois da remoção, a rota relê o estado e tenta recriar a relação anterior com `ImageInfo` de chave nova. Se o commit da nova imagem ocorreu apesar de uma exceção posterior, mantém o arquivo e retorna sucesso. Arquivos de substituição são limpos somente dentro da pasta de configuração do usuário; falha/estado ambíguo mantém arquivos para evitar referência quebrada.

Regressões em `ImageControllerUserImageAuthorizationTests`: save novo acontece antes de limpar o antigo; falha do provider preserva o vínculo e limpa o arquivo parcial; falha ao persistir o novo vínculo restaura caminho e `LastModified`. Três casos focais Release passaram; `Jellyfin.Api.Tests` Release passou 744/744, sem skips/falhas. Testes usam managers/provider simulados; EF/MariaDB, HTTP integrado, runtime instalado e produção não foram exercitados. Sem release.

## Lock compartilhado de mutações de imagem de perfil — 2026-10-19 20:22 (-03)

Foi confirmado que seleção/remoção via GetAvatar e upload/exclusão via `Jellyfin.Api.Controllers.ImageController` alteram o mesmo vínculo `User.ProfileImage`. O gate anterior era privado ao plugin e não coordenava as rotas nativas. `IUserManager.AcquireProfileImageUpdateLockAsync(Guid)` agora fornece um lease assíncrono por usuário implementado por `UserManager` com `AsyncKeyedLocker<Guid>`. GetAvatar (seleção, remoção e reparo de mapping obsoleto) e as rotas nativas de upload/exclusão adquirem o mesmo lease; a API nativa relê e reautoriza o usuário dentro do gate antes de mutar.

Regressões: API focal 40/40; `UserManagerAuthenticationLockTests` 9/9, incluindo bloqueio do mesmo usuário e não bloqueio de usuários distintos; suíte completa `Jellyfin.Api.Tests` 742/742; suíte `Jellyfin.Server.Implementations.Tests` 1.508 aprovados, 64 ignorados por gates de ambiente/opt-in e 0 falhas; build Release do GetAvatar 0 avisos/0 erros. Os caminhos não foram exercitados com runtime instalado nem chamadas HTTP concorrentes reais; as mutações em testes usam managers simulados, portanto MariaDB/EF e produção continuam sem validação. Nenhuma release foi criada.

## Sincronização dos escritores da configuração — 2026-10-19

`Plugin.AcquireConfigurationLock` oferece um escopo `IDisposable` sobre um monitor privado, e `SaveConfiguration`/`UpdateConfiguration` adquirem o mesmo monitor. `AvatarController` e os caminhos de escrita/leitura que compartilham coleções de `PluginConfiguration` também entram nesse escopo. Isso faz o snapshot e a restauração do XML em `SaveUserAvatarMapping` ficarem serializados com os demais escritores internos, evitando que um rollback antigo reverta uma atualização mais recente.

`SetUserAvatarAsync_WhenImageSaveFails_PreservesConcurrentConfigurationWrite` usa uma barreira na gravação da imagem: enquanto a operação está suspensa, grava outra configuração no XML; então força a falha de imagem e confirma que o conteúdo concorrente permanece. O teste foi observado falhando antes da correção e passou depois. `AvatarControllerUserAvatarAuthorizationTests` Release final: 20/20; build Release do plugin: 0 avisos/0 erros. Isso cobre concorrência entre caminhos internos do plugin, não gravações externas diretas ao XML, EF real de `ImageInfo`, pipeline HTTP de `UserAvatar` ou runtime instalado. Nenhuma produção ou release foi alterada.

### Persistência da imagem de perfil após rollback — 2026-10-19

`UserProfileImagePersistenceTests.ReplacingDeletedProfileImage_PersistsNewImageThroughDetachedUserUpdate` executa o modelo real `UsersDbContext` com EF Core InMemory: salva `User` + `ImageInfo`, remove a imagem antiga, atribui uma nova imagem com identidade zero, chama `DbContext.Update(user)` e relê o vínculo em outro contexto. Release: 1/1. Também foi adicionado `ServerPersistenceMariaDbTests.UserProfileImage_CanBeDeletedAndRecreatedWithGeneratedIdentity` para executar o mesmo contrato na MariaDB isolada do projeto; o build passou, mas a execução ficou ignorada porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não estava configurada. Logo, a persistência EF com o provider InMemory foi observada; as restrições/identidade do provider MariaDB continuam sem execução real. O pipeline HTTP `UserAvatar`, instalação e produção permanecem pendentes. Ver [[Sessões/2026-10-19-MulletaFlix-getavatar-imageinfo-persistence]].

Os endpoints `Upload`, `Delete/{avatarId}`, `RemoveUserAvatar`, `ValidateAvatars`, `CleanupOrphans` e `AvatarStatus` são operações administrativas. Cada ação declara explicitamente o papel `Administrator`; acesso local de rede, sozinho, não autoriza essas operações.

`tests/Jellyfin.Api.Tests/Controllers/NebulaAuthorizationHttpTests.cs` executa os endpoints reais sob `TestServer`. A matriz verifica que chamadas anônimas recebem HTTP 401 e chamadas com papel `User` recebem HTTP 403. Esses testes não executam operações privilegiadas nem substituem a homologação da release.

`tests/Jellyfin.Api.Tests/Controllers/GetAvatarAdminAuthorizationTests.cs` também exercita as seis rotas através do pipeline HTTP com papel `Administrator`, em diretório temporário isolado: upload sem arquivo retorna 400, exclusão de avatar inexistente 404, remoção com GUID inválido 400, validação/limpeza/status 200. A combinação de negação e acesso autorizado passou em 26 casos junto com `NebulaAuthorizationHttpTests`; a suíte completa `Jellyfin.Api.Tests` passou 706/706 em Release em 17/10/2026. As respostas 200 provam entrada na action, não efeitos funcionais com arquivos/usuários reais. Sem instalação ou validação de produção; permanece necessário homologar uma operação admin controlada na próxima release.

## Leitura de avatar individual e privacidade — 2026-10-18

`GET /GetAvatar/UserAvatar/{userId}` aplica visibilidade do alvo, não apenas o conhecimento do GUID. Anônimos só recebem avatares de usuários visíveis segundo as regras de hidden/disabled, setup, dispositivo, rede local e acesso remoto; usuário autenticado pode ver seu próprio avatar, administrador e API key podem consultar avatares ocultos, e outro usuário autenticado só pode consultar alvo público. Alvo ausente ou negado retorna 404 para evitar expor existência. Resposta de imagem define `Cache-Control: no-store`.

`AvatarControllerUserAvatarAuthorizationTests` invoca a action/controller em configuração e arquivos temporários; cobre avatar público em rede local, remote access, device access, owner/admin/API key, usuário oculto/desabilitado, wizard incompleto e alvo inexistente. Portanto, esses 11 casos validam a lógica da action, mas não o pipeline HTTP completo para `UserAvatar`. `GetAvatarAdminAuthorizationTests` e `NebulaAuthorizationHttpTests` passam pelo TestServer para as rotas administrativas e têm gates separados. Revalidação focal Release em 18/10: 37/37 casos combinando as três classes; a suíte `Jellyfin.Api.Tests` completa já passou 729/729 em execução anterior. Isso valida a fonte local, não instalação ou clientes externos; não houve validação de produção.

### Regressão do endpoint UserAvatar pelo pipeline HTTP — 2026-10-19

`AvatarControllerUserAvatarAuthorizationTests.UserAvatarRoute_ReturnsImageThroughHttpPipelineWithoutCaching` hospeda a action real em `TestServer` e faz GET em `/GetAvatar/UserAvatar/{userId}`. Confirma resolução de rota, resposta HTTP 200, content type `image/png`, bytes da imagem, `Cache-Control: no-store` e consulta ao usuário esperado. A classe focal Release passou 21/21. Isso fecha o gate do pipeline HTTP para o caso público local; não valida rede externa, comportamento de cliente instalado, MariaDB relacional nem produção.

## Revalidação do roadmap — 2026-10-18 07:15 (-03)

O item de auditoria está corrigido na fonte local e registrado como T6.1.24. Ficam em aberto o pipeline HTTP completo de `UserAvatar`, compatibilidade de clientes e instalação/produção (T6.1.23 e T6.1 global). Regressão focal Release: 37/37. Nenhuma release foi criada nesta rechecagem.

## Persistência transacional do catálogo de avatares — 2026-10-18

`AvatarService.SaveAvatarAsync` valida a instância/configuração do plugin antes de criar arquivo. Se a gravação da imagem ou do XML de configuração falhar, remove a entrada recém-adicionada em memória e tenta apagar o arquivo parcial; erro de limpeza é registrado sem ocultar a falha original. Isso impede que uploads/instalações de pacotes deixem arquivos órfãos invisíveis no catálogo quando a persistência do catálogo falha.

Regressão em `AvatarControllerUserAvatarAuthorizationTests.SaveAvatarAsync_WhenConfigurationPersistenceFails_RollsBackFileAndCatalogEntry` força `IXmlSerializer.SerializeToFile` a lançar `IOException` e confirma a exceção, preservação do avatar preexistente e ausência do novo arquivo. Classe focal em Release: 12/12; suíte completa `Jellyfin.Api.Tests` Release: 730/730, sem ignorados/falhas. Nenhuma instalação ou validação em produção foi feita; esta evidência cobre somente execução local e fixtures/diretórios temporários.

## Seleção de avatar com rollback transacional — 2026-10-19

`AvatarService.SetUserAvatarAsync` agora captura os bytes da foto anterior, o arquivo XML da configuração e o estado dos mapeamentos antes da substituição. Se salvar imagem, atualizar o usuário ou persistir a configuração XML falhar, restaura best-effort o perfil, arquivo e XML anteriores; os arquivos são recuperados por temporário no mesmo diretório e substituição por rename, sem reserializar um XML possivelmente truncado. Erros de rollback são registrados sem substituir a exceção original. Como `ClearProfileImageAsync` remove a linha antiga do banco, o perfil restaurado usa nova entidade `ImageInfo` (Id zero), para que `UpdateUserAsync` insira a relação novamente. `AssignRandomAvatarAsync` deixou de remover previamente o mapeamento, preservando o estado para rollback.

Testes focais em `AvatarControllerUserAvatarAuthorizationTests` cobrem falha ao limpar perfil existente (sem emitir update duplicado), falha de persistência XML na atribuição direta (incluindo truncamento parcial do arquivo) e aleatória, além da regressão de catálogo já existente. Classe focal Release: 15/15 aprovada. A persistência EF real do vínculo recriado não é exercitada por essa fixture de API e segue como limite de validação; não foi feita instalação nem teste em produção.

### Limites encontrados na revisão independente

A revisão adversarial confirmou dois riscos ainda não eliminados: (1) o snapshot/restauração direta de `ConfigurationFilePath` não usa o mesmo lock privado de `BasePluginOfT.SaveConfiguration`, então uma gravação concorrente entre snapshot e rollback pode ser sobrescrita; (2) a flag de exclusão só é marcada após `ClearProfileImageAsync` retornar, portanto uma exceção depois do commit mas antes do retorno pode deixar estado ambíguo. O caso testado de falha na limpeza ocorre antes de qualquer mutação e não cobre esse cenário de commit incerto. Além disso, o fixture usa `IUserManager` simulado; recriação, unicidade e leitura de `ImageInfo` ainda precisam de teste com EF/contexto real descartável. Por esses motivos a correção é candidata e não deve ser tratada como homologada para produção.

## Revalidação de falha ambígua ao limpar perfil — 2026-10-19

`SetUserAvatarAsync` agora marca a tentativa de remoção antes de chamar `ClearProfileImageAsync`. Se a chamada lançar, consulta novamente o usuário persistido: recria `ImageInfo` com `Id` zero somente quando o usuário ainda existe e a imagem realmente foi removida; se o perfil ainda existe ou o usuário foi excluído, não tenta inserir uma relação duplicada nem recriar o usuário. Os bytes do arquivo original continuam restaurados pelo rollback já existente.

Regressões em `AvatarControllerUserAvatarAuthorizationTests` cobrem falha antes da mutação, exceção após a remoção persistida e usuário ausente na consulta de reconciliação. Classe focada Release: 17/17. A suíte completa `Jellyfin.Api.Tests` teve uma falha inicial em `LibraryBackgroundOperationGateTests.AddMediaPath_WhenRefreshFails_RestartsMonitorAndReleasesAdmission`; o teste isolado passou 1/1 e a repetição da suíte completa passou 735/735, sem ignorados. A validação ainda usa `IUserManager` simulado; `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não estava configurada.

Permanecem abertos: sincronização do snapshot/XML com gravações concorrentes e teste da relação `ImageInfo` usando EF/MariaDB descartável. Nenhuma instalação, produção ou GitHub Release foi alterada.

## Serialização das alterações de perfil GetAvatar — 2026-10-19

`AvatarService` agora usa o mesmo `UserAvatarUpdateLock` para `SetUserAvatarAsync`, `RemoveUserAvatarAsync` e a limpeza de perfil associada a um mapeamento de avatar obsoleto em `ValidateUserAvatarsAsync`. A validação reconsulta o mapeamento sob o lock de configuração antes de limpar o perfil; se a seleção já mudou, preserva a imagem e o novo mapeamento. Isso evita que operações internas concorrentes deixem uma foto removida com um mapeamento novo, ou apaguem uma seleção feita durante a validação.

`RemoveUserAvatarAsync_WaitsForConcurrentProfileAssignment` e `ValidateUserAvatarsAsync_WaitsForConcurrentProfileAssignmentWhenMappingIsStale` usam barreiras determinísticas. O primeiro teste falhou antes da correção e ambos passaram depois. `AvatarControllerUserAvatarAuthorizationTests`: 23/23 Release; `Jellyfin.Api.Tests`: 741/741 Release, sem ignorados/falhas; build Release do plugin: 0 avisos/0 erros.

Limite: o gate é interno ao GetAvatar. `Jellyfin.Api.Controllers.ImageController` ainda tem rotas nativas de upload e exclusão que manipulam `ProfileImage` sem este semáforo; a coordenação entre essas rotas e GetAvatar permanece pendente. Testes usam managers simulados para a concorrência; EF/MariaDB, instalação, runtime e produção não foram validados. Nenhuma release foi criada.

## Exclusão de perfil restrita à pasta do usuário — 2026-10-19

`ImageController.DeleteUserImage` agora verifica o caminho da foto contra a pasta de configuração do usuário antes de apagar. Se estiver fora, retorna HTTP 500, preserva arquivo e referência no banco e registra aviso. `AvatarService.RemoveUserAvatarAsync` não apaga arquivo externo, mas ainda remove a associação/mapeamento; a limpeza de órfãos valida a pasta individual sob `DataPath/users` e cada arquivo antes de `File.Delete`, ignorando caminhos externos e diretórios com reparse point.

Regressões RED/GREEN: exclusão nativa de caminho externo (sem apagar/limpar referência), remoção via GetAvatar sem apagar arquivo externo e limpeza sem seguir diretório de usuário symlinkado. Foco Release: 30/30; suíte completa `Jellyfin.Api.Tests`: 752/752, zero falhas/ignorados.

Limite de segurança ainda aberto: validação por caminho e remoção subsequente deixam TOCTOU se um processo autorizado a modificar a pasta trocar um componente por symlink/junction entre as operações. Não alegar hardening completo; exclusão ancorada em handle/descritor, ACL e teste concorrente multiplataforma continuam pendentes. Sem runtime instalado, produção ou Release.

## Pesquisa de exclusão ancorada em handle/descritor — 2026-10-19

Revisão das fontes primárias confirmou dois blocos de mecanismo, mas ainda não uma implementação pronta para produção. No Linux, `openat(2)` ancora resolução relativa em um descritor de diretório; `O_NOFOLLOW` protege apenas o componente final de cada chamada, então a caminhada de todos os diretórios precisa abrir e validar cada componente. `unlinkat(2)` remove a entrada relativa ao descritor e, se o nome tiver sido trocado por symlink, remove o link em si. Não foram testados nesta sessão o comportamento do filesystem real, as constantes por arquitetura/OS nem a concorrência.

No Windows, `CreateFileW` com `FILE_FLAG_OPEN_REPARSE_POINT` abre o reparse point em vez de seguir seu destino; `SetFileInformationByHandle` requer acesso `DELETE` no handle para marcar o objeto aberto para remoção. Isso ancora a operação ao objeto final aberto, mas as fontes consultadas não provam, por si só, que uma caminhada de caminho Win32 impede rename concorrente dos diretórios ancestrais ou garante que o objeto permaneça dentro da raiz autorizada entre validação e delete. Não usar o caminho como se essa prova existisse.

Decisão: manter a proteção TOCTOU como risco aberto. Próxima implementação deve usar handles para validar cada ancestral e o arquivo final, definir quais ACLs/compartilhamentos são aceitos, falhar sem excluir quando o filesystem não der as garantias necessárias e testar rename/junction/symlink concorrente em Windows e Linux. Definir e testar também política macOS antes de habilitar exclusão ancorada nessa plataforma. Fontes: [CreateFileW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew), [SetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle), [open(2)](https://man7.org/linux/man-pages/man2/open.2.html), [unlinkat(2)](https://man7.org/linux/man-pages/man2/unlinkat.2.html). Nenhum código runtime foi alterado nem testado nesta pesquisa.

### Permissões dos diretórios de perfil — 2026-10-19 21:56 (-03)

As duas rotas runtime criam a pasta de perfil com `Directory.CreateDirectory(userDataPath)` sem modo Unix/ACL explícitos; a raiz é `ConfigurationDirectoryPath/users`. A restrição `0600` existente alcança arquivos de configuração Unix, não essa árvore. O instalador Debian define `/etc/jellyfin` como `jellyfin:adm` e `0750` só quando ausente ou ainda pertencente a root; o instalador Windows habilita herança e concede Modify às identidades NetworkService/SYSTEM/LocalService, mas não comprova por código que todos os ACEs herdados ou configurações customizadas negam escrita aos demais. Isso é evidência de políticas do pacote, não de ACL/modo efetivos em produção.

`Directory.CreateDirectory` possui overloads com `UnixFileMode` e `DirectorySecurity`; o overload Windows aplica ACL durante a criação, mas não muda diretório preexistente. Hardening precisa tratar criação e upgrade, inspecionar principals efetivos e recusar exclusão se o pai puder ser alterado por terceiro. A revalidação dos testes de containment/exclusão passou 6/6 Release; nenhum desses casos verifica permissões ou corrida de rename. Fontes: [Directory.CreateDirectory](https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.createdirectory?view=net-10.0), [Windows ACL inheritance](https://learn.microsoft.com/en-us/windows/security/identity-protection/access-control/access-control), [chmod(1)](https://www.man7.org/linux/man-pages/man1/chmod.1.html). TOCTOU continua aberto.

### Observação read-only do runtime instalado — 2026-10-19 22:02 (-03)

O processo MulletaFlix instalado ativo reporta versão 12.1.21 e usa `--datadir` sob `%LOCALAPPDATA%`; a instância observada roda interativamente, sem serviço `MulletaFlixServer` registrado. A árvore ativa `config/users` contém 29 diretórios de perfil. Nas ACLs efetivas examinadas, BUILTIN Users/Everyone não têm ACE de escrita; há um SID local sem conta local correspondente com `ReadAndExecute`, e um Capability SID não resolvido com `FullControl` herdado somente para objetos diretório (sem flags de propagação). A chave local `AllCachedCapabilities` estava ausente, logo a capacidade específica não pôde ser identificada. A documentação Microsoft diz que Capability SIDs não resolvem para nomes e alerta para não removê-los sem avaliação; não modificar essas ACLs.

Foi encontrada outra árvore sob ProgramData com ACE herdada de `BUILTIN\Users:Write`, mas ela não corresponde ao `--datadir` do processo ativo; o conteúdo não foi examinado e nada foi alterado. Não atribuir essa árvore à instância ativa nem removê-la sem confirmar proprietário/uso.

Conclusão limitada: não se observou permissão de escrita para o grupo geral Users na árvore ativa; a presença de um Capability SID com FullControl e o limite de identidade compartilhada (processos sob a mesma conta interativa) impedem afirmar isolamento completo. A árvore ProgramData separada requer triagem de instalação/ACL. TOCTOU permanece aberto. Fontes: [Microsoft — Capability SIDs](https://learn.microsoft.com/en-us/troubleshoot/windows-server/windows-security/sids-not-resolve-into-friendly-names), [Microsoft — AppContainer tokens and ACLs](https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer).

## Containment do caminho do catálogo de avatares — 2026-10-19 23:47 (-03)

`AvatarInfo.FileName` é dado persistido na configuração e pode conter um caminho relativo com `..` ou um caminho absoluto. `AvatarService.GetAvatarPath` agora retorna `null` para caminhos fora de `_avatarDirectory`; `DeleteAvatar` retorna `false` e mantém arquivo, item de catálogo e associações do usuário sem mutação quando containment falha. Ambos validam com `FileSystemHelper.IsPathWithinRoot` antes de acessar o arquivo. Busca global pelo padrão `Path.Combine(_avatarDirectory, avatar.FileName)` encontrou exatamente dois pontos: `GetAvatarPath` e `DeleteAvatar`, ambos guardados pela validação.

As regressões `GetAvatarPath_WhenConfiguredFileNameEscapesAvatarDirectory_ReturnsNull` e `DeleteAvatar_WhenConfiguredFileNameEscapesAvatarDirectory_PreservesOutsideFileAndCatalogEntry` falharam antes do hardening (incluindo perda da associação em memória) e passaram depois: 2/2 Release. `Jellyfin.Api.Tests` passou 755/755. Gate da solução `dotnet test MulletaFlix.sln -c Release --no-restore --filter 'FullyQualifiedName!~Integration'` passou com 5.062 aprovados, 65 ignorados, 0 falhas em 5.127 resultados agregados de 17 TRX; os dois projetos Integration selecionaram zero casos. NebulaPython Python 3.10: 646/646.

Essa validação fecha traversal estável no nome configurado, mas não a corrida TOCTOU entre validação por caminho e operação de arquivo. ACL efetiva, corrida com rename/junction/symlink, MariaDB/HTTP integrado, runtime instalado, produção e release não foram validados. Avisos XML/StyleCop/analyzers continuam presentes no build.

### Symlink no arquivo final do catálogo — 2026-10-19 23:58 (-03)

Revisão da implementação confirmou que `FileSystemHelper.IsPathWithinRoot` rejeita componentes reparse existentes em todo o caminho, inclusive o arquivo final. O teste preexistente cobria somente symlink em diretório pai. Foram acrescentadas regressões no nível de `AvatarService`: leitura de `GetAvatarPath` e exclusão de `DeleteAvatar` recusam um arquivo do catálogo que é symlink para fora; o teste de exclusão confirma que link, alvo externo, entrada e mapping permanecem intactos. Ambos executaram sem skip no Windows desta sessão. Não houve alteração de comportamento de produção nesta rodada.

O gate da solução Release (`FullyQualifiedName!~Integration`) foi reexecutado após os novos testes: 5.064 aprovados, 65 ignorados, 0 falhas em 5.129 resultados/17 TRX; API 757/757. Os dois projetos Integration não selecionaram casos. TOCTOU por troca concorrente posterior à checagem permanece aberto; o teste não prova segurança sob corrida. Também seguem pendentes ACL efetiva, integração HTTP/MariaDB, Mongo opt-in, CI remota, runtime instalado e produção.
