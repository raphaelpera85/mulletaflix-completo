# MulletaFlix Server v12.1.16

Esta versão corrige falhas de upload Telegram e reduz trabalho repetido durante a inicialização do Nebula.

### Correções

- **Upload rejeitado com HTTP 413:** o runtime Python enviava partes de 64 MiB ao endpoint padrão `api.telegram.org/sendDocument`, que rejeitava cada bot e acabava fazendo falhar a mídia inteira. O tamanho efetivo agora fica limitado a 45 MiB (com margem para o multipart), inclusive para configurações antigas acima do limite. A interface e a validação da API também passam a aceitar somente 1–45 MiB.
- **Inicialização do Nebula:** as migrações de Novelas e Animações materializavam separadamente a coleção `ftp.files` inteira. Agora compartilham a mesma leitura se a primeira migração não move registros; a coleção só é lida novamente quando é necessário refletir mudanças da primeira migração.
- **Autenticação FTP em documentos legados:** o pacote inclui a desserialização defensiva de permissões antigas em formato string e entradas ACL inválidas, evitando que o dispatcher FTP encerre a sessão com `TypeError` e mantendo permissões externas fechadas. A correção é coberta por testes de regressão.

### Validação

- `dotnet test MulletaFlix.sln -c Release --no-restore --verbosity quiet`: aprovado, exit code 0.
- `Jellyfin.Server.Implementations.Tests`: 1.374 aprovados, 55 ignorados, 0 falhas.
- `Jellyfin.Api.Tests`: 664 aprovados, 0 falhas (inclui rejeição de configuração de bloco acima de 45 MiB).
- `Jellyfin.Server.Integration.Tests`: 140 aprovados, 3 ignorados, 0 falhas.
- Nebula Python: 563 aprovados, 0 falhas; os testes de transporte Telegram são simulados, sem envio para bots reais.

### Artefatos

- `mulletaflix-update-win-x64.zip` — atualização Windows.
- `mulletaflix_12.1.16_windows-x64.exe` — instalador Windows x64.
- `mulletaflix_12.1.16_linux-x64.tar.gz` — pacote Linux x64.
