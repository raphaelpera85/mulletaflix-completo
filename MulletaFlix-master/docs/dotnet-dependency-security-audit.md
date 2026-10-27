# Auditoria de vulnerabilidades NuGet (.NET)

## Snapshot de 25/10/2026

`dotnet list <csproj> package --vulnerable --include-transitive --no-restore` foi executado individualmente para os 48 arquivos `.csproj` sob `MulletaFlix-master`, com até cinco processos em paralelo. Resultado observado: 48/48 exit 0, cada um reportou nenhum pacote vulnerável; 0 projetos com vulnerabilidade ou erro. A fonte NuGet identificada foi `https://api.nuget.org/v3/index.json`.

`fuzz/Jellyfin.Api.Fuzz/Jellyfin.Api.Fuzz.csproj` era o único sem `obj/project.assets.json`. Foi verificado com restore individual, que também restaurou suas referências de projeto; o projeto reportou nenhum pacote vulnerável. O scan cobre dependências NuGet diretas e transitivas conforme o grafo restaurado de cada projeto.

Este resultado é um snapshot do advisory data disponível na data acima. Não prova ausência de vulnerabilidades desconhecidas ou divulgadas depois, não analisa dependências não NuGet e não valida pacotes efetivamente carregados pelo runtime instalado. Nenhum upgrade foi aplicado e nenhum artefato SBOM foi gerado.

## Cobertura da CI — 25/10/2026

A solução contém 43 projetos; cinco projetos adicionais fora da solução agora são restaurados e auditados individualmente no workflow `.github/workflows/ci.yml`. O restore transforma NU1900–NU1905 relevantes (falha ao consultar fonte, severidades low–critical e fonte sem base de advisories) em erros, para evitar que indisponibilidade do audit ou advisory conhecido passe silenciosamente nos 48 projetos. O argumento `WarningsAsErrors` foi validado por restore local de um projeto externo. O teste de contrato `tools/release/tests/ci-nuget-audit-coverage.test.mjs` compara todos os `.csproj` da árvore com os dois caminhos de auditoria, verifica os comandos dentro do loop e impede lacunas. Também foi corrigida a capitalização do caminho `Tools/MulletaFlix.IntroSkipperMigration` na solução para filesystem case-sensitive.

Verificações locais desta alteração: os cinco projetos suplementares passaram `dotnet list ... --vulnerable --include-transitive --no-restore` (exit 0, sem advisory conhecido); o contrato novo passou 1/1 e os contratos relacionados ao CI passaram 12/12; `dotnet build MulletaFlix.sln --configuration Release --no-restore` terminou com exit 0, 0 erros e 493 avisos. O portal de homologação passou 179/179 antes da inclusão do evento desta sessão.

O workflow remoto ainda não foi executado nesta revisão. Os resultados locais não validam runner Linux nem substituem a auditoria hospedada. Revalidar no próximo CI observável e em cada release; manter a ressalva sobre advisories desconhecidos/futuros, dependências não NuGet e runtime instalado.
