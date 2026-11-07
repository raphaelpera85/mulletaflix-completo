# Testes de integração ao vivo dos provedores

Os testes live consultam sites públicos e podem demorar, falhar por indisponibilidade externa ou retornar catálogos diferentes. A regressão local e a CI não os executam por padrão.

Para executá-los deliberadamente no PowerShell:

```powershell
$env:MULLETAFLIX_RUN_LIVE_PROVIDER_TESTS = '1'
dotnet test tests/Jellyfin.Providers.Tests/Jellyfin.Providers.Tests.csproj --configuration Release --filter 'FullyQualifiedName~LiveIntegrationTests'
```

O opt-in precisa ter o valor exato `1`. Sem a variável, os 13 testes live em DramaBox, GoodShort, NetShort, DramaFinds e NartoDrama são reportados como ignorados antes de criar clientes HTTP. Os testes unitários continuam cobrindo respostas capturadas e comportamento dos parsers sem rede pública.

Não configure o opt-in na validação padrão da release. A execução live é uma verificação diagnóstica separada e não substitui testes com fixtures, MariaDB descartável, CI ou homologação de produção.
