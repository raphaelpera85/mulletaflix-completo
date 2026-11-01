# Auditoria do fluxo de capas e imagens

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

## Rótulos de idioma em minúsculas para qualquer locale — 2026-10-28

- `TmdbUtils.AdjustImageLanguage` agora retorna códigos de idioma em minúsculas em todos os casos, inclusive códigos regionais herdados do locale solicitado (`pt-br`, `en-us`, `zh-hant`). O formato em maiúsculas exigido por TMDb permanece somente no parâmetro de consulta; esta mudança afeta o rótulo retornado para cada imagem.
- Regressão focal cobre locale regional para inglês, francês e chinês, idioma base maiúsculo e idioma não localizado. Nenhum idioma foi codificado como preferência fixa.
- O mapa técnico foi conferido; a mudança permanece no fluxo existente de conversão de resultados TMDb, sem novo módulo, classe, API ou dependência.
- Não testado contra TMDb ao vivo nem visualmente na produção nesta sessão. Refresh de imagens já gravadas continua necessário para reaplicar rótulos/seleção de candidatos após implantação.

## Reauditoria do CDN e runtime de imagens — 2026-10-28

- Teste focal Release de `RemoteImagePayloadValidatorTests` e `ProviderManagerRemoteImageValidationTests`: 10/10 aprovados. Isso revalida o fluxo em memória/test doubles, não o provider completo contra API real.
- Uma URL pública de imagem do CDN TMDb respondeu HTTP 200, `image/jpeg`, 154.888 bytes e assinatura JPEG válida (`FF-D8-FF`). O teste confirma entrega do CDN para esse recurso, não seleção de idioma ou refresh feito pelo servidor.
- `GET http://127.0.0.1:8096/System/Info/Public` respondeu HTTP 200 com versão instalada 12.1.43, enquanto a fonte/release atual é 12.1.45. Portanto o runtime observado ainda não valida as correções recentes. Nenhum item de mídia foi atualizado.
- Pendente: instalar a versão candidata em janela segura, executar refresh de um item de teste e verificar resposta, arquivo, `ImageInfos`/`ImageTags` e renderização. Homologação formal permanece 2/7; esta evidência não concede crédito formal.

## Cobertura de idiomas base e regionais — 2026-10-28

- A busca TMDb passou a enviar a preferência regional e também o código ISO 639-1 base (`fr-CA,fr,null,en`), preservando imagens sem idioma e inglês como fallback. Os itens da biblioteca continuam usando o locale preferido efetivo da mídia; a lista é compartilhada pelos providers de filme, série, temporada, episódio e coleção.
- A documentação da API TMDb define `include_image_language` como lista separada por vírgulas de códigos ISO 639-1; a consulta somente pela variante regional podia omitir imagens classificadas apenas com o idioma base.
- Teste Release focal `TmdbUtilsTests`: 27/27 aprovados para pt-BR, francês, japonês, chinês tradicional, espanhol latino-americano, árabe, inglês regional e locale vazio. A suíte Providers ampla foi interrompida após mais de um minuto sem saída; nenhum resultado dela é contabilizado.
- O mapa técnico foi conferido. A alteração usa `TmdbUtils.GetImageLanguagesParam` e os providers já existentes; não adiciona módulos, APIs, dependências ou fluxos.
- Não foi chamado o TMDb autenticado, nem realizado refresh, download ou verificação visual em mídia/runtime. Capas existentes exigem refresh após instalação; homologação formal permanece pendente.
# Proveniência da seleção manual — 2026-11-01

- O endpoint `POST Items/{itemId}/RemoteImages/Download` recebia a URL escolhida e persistia a imagem, mas descartava o `ProviderName` já enviado pela interface e o idioma associado ao candidato. `BaseItem.SetImagePath` limpa esses campos quando substitui uma arte.
- O endpoint agora aceita `providerName` opcional; quando informado, consulta novamente o provider para localizar a URL exata entre todos os idiomas e salva `ProviderName`/`Language` no `ItemImageInfo` antes da atualização do repositório. Clientes antigos sem o parâmetro preservam o comportamento anterior.
- Se o provider não devolver mais a URL, a imagem continua sendo salva, mas sem inventar proveniência. Isso mantém compatibilidade e impede atribuir idioma/provider incorretos.
- `RemoteImageControllerTests`: 8/8 em Release, cobrindo `Movie` e `Series` nos locales `pt-br`, `en-us` e `zh-hant`, URL ausente no provider e cliente legado sem `providerName`. Suíte `Jellyfin.Api.Tests`: 805 aprovados, 3 ignorados, 0 falhas/808.
- Limites: teste usa provider/mock e não verifica TMDb/Fanart ao vivo, metadados históricos em MariaDB, browser nem produção. Categorias de animação são atendidas pelos tipos `Movie`/`Series` já suportados.
## Comparação com Jellyfin upstream: seleção de imagens por idioma — 2026-11-01

Consulta realizada em `master` do Jellyfin em 2026-11-01; como `master` é móvel, esta comparação não representa uma versão/tag específica.

- `ProviderManager.GetAvailableRemoteImages` consulta os image providers habilitados e considera `item.GetPreferredMetadataLanguage()`. Com `IncludeAllLanguages=false`, o Jellyfin mantém imagens sem idioma, no idioma exato configurado e em inglês; cada provider ordena suas candidatas pela preferência. O endpoint/listagem manual pode incluir todos os idiomas quando a opção é ativada.
- O refresh automático de `ItemImageProvider` chama essa listagem com `IncludeAllLanguages=true` para preservar fallback. Em seguida aplica opções de tipo, dimensões mínimas e limites de imagens; imagens singulares usam a primeira candidata que consegue salvar, e backdrops são múltiplos. `ImageSaver` centraliza a persistência, respeita a configuração de metadados locais e registra o caminho no item.
- No TMDb upstream atual, `TmdbMovieImageProvider` e `TmdbSeriesImageProvider` buscam imagens sem passar idioma de imagem (`null`) e têm TODO explícito para restringir pela opção “All Languages”. Isso deixa o catálogo amplo e transfere a seleção para a ordenação posterior. Há também um relato upstream recente de imagens em inglês sendo priorizadas sobre espanhol latino; ele é um relato de bug, não prova de defeito em toda versão ou instalação.
- No fork MulletFlix, `ItemImageProvider.GetPreferredImageLanguage` resolve idioma/região do item, pais, pastas/biblioteca e configuração do servidor; a ordenação por preferência é global entre providers e os tipos `Movie` e `Series` usam o mesmo fluxo (inclui filmes e séries de animação). `TmdbUtils.GetImageLanguagesParam` solicita a variante regional, idioma-base, imagens sem idioma (`null`) e inglês como fallback. Essa chamada direcionada difere do `null` amplo observado nos image providers TMDb do upstream atual.
- A política observada é preferência de idioma primeiro e fallback depois, não exclusão dos demais idiomas: inglês segue disponível quando não existe imagem adequada no idioma solicitado. Candidatos sem idioma também continuam elegíveis. As buscas manuais de imagens podem listar alternativas em outras línguas.

Fontes primárias consultadas: [ProviderManager](https://github.com/jellyfin/jellyfin/blob/master/MediaBrowser.Providers/Manager/ProviderManager.cs), [ItemImageProvider](https://github.com/jellyfin/jellyfin/blob/master/MediaBrowser.Providers/Manager/ItemImageProvider.cs), [TMDb Movie image provider](https://github.com/jellyfin/jellyfin/blob/master/MediaBrowser.Providers/Plugins/Tmdb/Movies/TmdbMovieImageProvider.cs), [TMDb Series image provider](https://github.com/jellyfin/jellyfin/blob/master/MediaBrowser.Providers/Plugins/Tmdb/TV/TmdbSeriesImageProvider.cs), [ImageSaver](https://github.com/jellyfin/jellyfin/blob/master/MediaBrowser.Providers/Manager/ImageSaver.cs) e [Jellyfin Setup Wizard](https://github.com/jellyfin/jellyfin.org/blob/master/docs/general/post-install/setup-wizard.md). Referência contextual: [relato de idioma de imagens TMDb](https://github.com/jellyfin/jellyfin/issues/18347).

Escopo desta sessão: leitura de documentação/código upstream e comparação estática com os arquivos locais. Nenhum código, imagem, item, configuração ou runtime foi alterado; testes de software e provedores externos não foram executados. O comportamento instalado em produção e a renderização de títulos reais não foram revalidados.

## Revalidação do idioma/fallback de imagens — 2026-11-01 03:09 BRT

- Testes focais Release/.NET 10 de `TmdbUtilsTests`, ordenação `ProviderManager`, idioma de biblioteca/servidor, `Movie`/`Series`, backdrop, logo, thumb e fallback: 48/48 aprovados.
- Suíte `Jellyfin.Providers.Tests` Release completa, excluindo `DramaFindsLiveIntegrationTests` por chamar API externa: 849 aprovados, 12 ignorados, 0 falhas/861. Os casos live restantes também permaneceram ignorados por opt-in.
- Evidência confirma o comportamento dos testes isolados, não seleção/download em TMDb/Fanart real, atualização de imagens existentes, renderização ou produção. Nenhum runtime ou mídia foi alterado.
