# Autorização administrativa do GetAvatar

Os endpoints `Upload`, `Delete/{avatarId}`, `RemoveUserAvatar`, `ValidateAvatars`, `CleanupOrphans` e `AvatarStatus` são operações administrativas. Cada ação declara explicitamente o papel `Administrator`; acesso local de rede, sozinho, não autoriza essas operações.

`tests/Jellyfin.Api.Tests/Controllers/NebulaAuthorizationHttpTests.cs` executa os endpoints reais sob `TestServer`. A matriz verifica que chamadas anônimas recebem HTTP 401 e chamadas com papel `User` recebem HTTP 403. Esses testes não executam operações privilegiadas nem substituem a homologação da release.

`tests/Jellyfin.Api.Tests/Controllers/GetAvatarAdminAuthorizationTests.cs` também exercita as seis rotas através do pipeline HTTP com papel `Administrator`, em diretório temporário isolado: upload sem arquivo retorna 400, exclusão de avatar inexistente 404, remoção com GUID inválido 400, validação/limpeza/status 200. A combinação de negação e acesso autorizado passou em 26 casos junto com `NebulaAuthorizationHttpTests`; a suíte completa `Jellyfin.Api.Tests` passou 706/706 em Release em 17/10/2026. As respostas 200 provam entrada na action, não efeitos funcionais com arquivos/usuários reais. Sem instalação ou validação de produção; permanece necessário homologar uma operação admin controlada na próxima release.

## Leitura de avatar individual e privacidade — 2026-10-18

`GET /GetAvatar/UserAvatar/{userId}` aplica visibilidade do alvo, não apenas o conhecimento do GUID. Anônimos só recebem avatares de usuários visíveis segundo as regras de hidden/disabled, setup, dispositivo, rede local e acesso remoto; usuário autenticado pode ver seu próprio avatar, administrador e API key podem consultar avatares ocultos, e outro usuário autenticado só pode consultar alvo público. Alvo ausente ou negado retorna 404 para evitar expor existência. Resposta de imagem define `Cache-Control: no-store`.

`AvatarControllerUserAvatarAuthorizationTests` invoca a action/controller em configuração e arquivos temporários; cobre avatar público em rede local, remote access, device access, owner/admin/API key, usuário oculto/desabilitado, wizard incompleto e alvo inexistente. Portanto, esses 11 casos validam a lógica da action, mas não o pipeline HTTP completo para `UserAvatar`. `GetAvatarAdminAuthorizationTests` e `NebulaAuthorizationHttpTests` passam pelo TestServer para as rotas administrativas e têm gates separados. Revalidação focal Release em 18/10: 37/37 casos combinando as três classes; a suíte `Jellyfin.Api.Tests` completa já passou 729/729 em execução anterior. Isso valida a fonte local, não instalação ou clientes externos; não houve validação de produção.

## Revalidação do roadmap — 2026-10-18 07:15 (-03)

O item de auditoria está corrigido na fonte local e registrado como T6.1.24. Ficam em aberto o pipeline HTTP completo de `UserAvatar`, compatibilidade de clientes e instalação/produção (T6.1.23 e T6.1 global). Regressão focal Release: 37/37. Nenhuma release foi criada nesta rechecagem.

## Persistência transacional do catálogo de avatares — 2026-10-18

`AvatarService.SaveAvatarAsync` valida a instância/configuração do plugin antes de criar arquivo. Se a gravação da imagem ou do XML de configuração falhar, remove a entrada recém-adicionada em memória e tenta apagar o arquivo parcial; erro de limpeza é registrado sem ocultar a falha original. Isso impede que uploads/instalações de pacotes deixem arquivos órfãos invisíveis no catálogo quando a persistência do catálogo falha.

Regressão em `AvatarControllerUserAvatarAuthorizationTests.SaveAvatarAsync_WhenConfigurationPersistenceFails_RollsBackFileAndCatalogEntry` força `IXmlSerializer.SerializeToFile` a lançar `IOException` e confirma a exceção, preservação do avatar preexistente e ausência do novo arquivo. Classe focal em Release: 12/12; suíte completa `Jellyfin.Api.Tests` Release: 730/730, sem ignorados/falhas. Nenhuma instalação ou validação em produção foi feita; esta evidência cobre somente execução local e fixtures/diretórios temporários.
