# Testes de integração MariaDB

Os testes de integração que criam ou removem schemas só usam MariaDB quando
`MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` está configurada. Sem a variável,
os casos que dependem do banco são ignorados; nenhum endpoint padrão é sondado.

O valor é validado antes de qualquer operação destrutiva. São aceitos apenas um
host loopback (`127.0.0.1`, `::1` ou `localhost`) e uma porta diferente de `3306`.
O nome do banco é removido da conexão administrativa; cada suíte cria e remove
apenas seus próprios schemas descartáveis. O fixture web consome a instância
fornecida e não inicia nem encerra serviços MariaDB. Nunca aponte a variável para
produção nem para um serviço compartilhado.

Exemplo PowerShell para uma instância MariaDB descartável local em `33099`:

```powershell
$env:MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING = "Server=127.0.0.1;Port=33099;User ID=test;Password=<senha-local>;CharSet=utf8mb4;"
dotnet test tests/IntroSkipper.Integration.Tests/IntroSkipper.Integration.Tests.csproj --configuration Release
Remove-Item Env:MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING
```

As suítes de Intro Skipper, persistência, migração SQLite→MariaDB, restore de
backup e integração HTTP do servidor usam essa configuração. Inicie antes uma
instância descartável MariaDB local e execute, por exemplo:

```powershell
$env:MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING = "Server=127.0.0.1;Port=33099;User ID=test;Password=<senha-local>;CharSet=utf8mb4;"
dotnet test tests/Jellyfin.Server.Integration.Tests/Jellyfin.Server.Integration.Tests.csproj --configuration Release
Remove-Item Env:MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING
```

A senha deve ser fornecida somente no ambiente local do processo; não a grave
em arquivos versionados ou no vault.
