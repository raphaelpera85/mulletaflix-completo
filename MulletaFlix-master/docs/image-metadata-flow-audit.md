## Revalidação do ImageSaver e publicação do portal — 2026-11-01 15:55 BRT

`ImageSaverTests` em Windows/.NET 10 Release: 4 aprovados, 1 ignorado, 0 falhas/5. O caso de falha durante a cópia preserva o arquivo antigo e limpa o temporário. O teste Unix retorna cedo em Windows (resultado contado como aprovado), e o teste de exclusão não-Windows foi ignorado; nenhum deles comprova escrita POSIX ou rooted. O writer ainda promove por pathname e não há cobertura de symlink/rename concorrente. Nenhum runtime foi alterado.

Portal local e espelho: contratos 450/450 e 109/109. O lote 10/10 foi publicado em `main` (`f33689f`, `f3e5609`); o JSON público respondeu HTTP 200 com `generatedAt=2026-11-01T15:55:46-03:00` e 730 eventos. Homologação formal 2/7, sem crédito de release.

## Auditoria de fronteira para gravação rooted — 2026-11-01 15:49 BRT

`BackupRestoreDirectory` não pode ser consumida diretamente por `ImageSaver`: ela é interna a `Jellyfin.Server.Implementations` e aplica política de ACL orientada a restore. `MediaBrowser.Providers` tem referências de camada a `MediaBrowser.Controller` e `MediaBrowser.Model`. A validação Linux do nome-fonte antes do rename ainda não tem teste que substitua o arquivo concorrencialmente; portanto essa classe não serve como evidência de escrita segura para imagens.

`BackupRestoreDirectoryTests` em Windows/.NET 10 Release: 47 aprovados, 11 ignorados por plataforma, 0 falhas/58. Os casos ignorados dependem de Linux e não foram validados nesta sessão. Nenhum código runtime foi alterado. O `ImageSaver` continua baseado em pathname; gravador rooted compartilhado, preservação de ACL/modo, fallback e atomicidade precisam ser projetados e testados em Windows e Linux antes de migrar qualquer caller.

## Comparação do fluxo de idioma com Jellyfin upstream — 2026-11-01

- O `TmdbMovieImageProvider` do Jellyfin upstream ainda pede imagens com parâmetros de idioma nulos e mantém um TODO para expor o controle de `All Languages` à tela. Fonte: [arquivo upstream](https://github.com/jellyfin/jellyfin/blob/master/MediaBrowser.Providers/Plugins/Tmdb/Movies/TmdbMovieImageProvider.cs), linhas 555–560.
- Neste fork, `ILanguageAwareRemoteImageProvider` recebe idioma/região; `ProviderManager` normaliza e ordena idioma exato, idioma-base e fallbacks. O refresh automático consulta todos os idiomas disponíveis e escolhe pela ordenação antes do download.
- Revalidação focal Windows/.NET 10 Release: `TmdbUtilsTests`, `ProviderManagerImageProviderTests` e `ItemImageProviderRefreshTests`, 36/36 aprovados.
- Não foi reproduzida falha na ordenação. Falta teste direto do contrato TMDb de filme e série desde os parâmetros de consulta até a conversão da resposta; API real, mídia, imagens renderizadas e produção não foram verificadas. Sem alteração de runtime.

# Auditoria do fluxo de capas e imagens

## Cache HTTP da AudioDB — 2026-11-01 16:43 BRT

`AudioDbArtistProvider.DownloadArtistInfo` e `AudioDbAlbumProvider.DownloadInfo` abriam `artist.json`/`album.json` diretamente com `FileMode.Create`. As respostas agora são lidas com `ResponseHeadersRead`, verificadas por status e copiadas a temporário exclusivo no mesmo diretório; só após concluir a cópia o cache é promovido. Cancelamento, erro de transporte/cópia e status HTTP não exitoso preservam o arquivo anterior.

`AudioDbCacheFileTests` e `AudioDbCachePathSafetyTests`: 10/10 aprovados; `Jellyfin.Providers.Tests`: 930 aprovados, 14 ignorados, 0 falhas/944 em Windows/.NET 10 Release. Nenhuma requisição AudioDB real, banco, catálogo de produção ou Linux foi usado. Sem mudança de API/dependência; mapa técnico conferido e estruturalmente igual. Homologação formal 2/7, sem crédito ou release; portal local 5/10 do lote.

## Consulta TMDb respeita IncludeAllLanguages — 2026-11-01

`ProviderManager.GetAvailableRemoteImages` já desliga o filtro local de idiomas quando `RemoteImageQuery.IncludeAllLanguages` está ativado. Porém, os providers TMDb de filmes, séries, temporadas, episódios e coleções ainda precisam omitir os filtros no pedido ao TMDb. A interface language-aware entrega essa opção ao provider; um método default mantém implementadores antigos compatíveis. No modo todos, os cinco providers passam strings vazias para omitir `language` e `include_image_language`: na TMDbLib 3.0.0, `null` pode reativar defaults em alguns métodos, enquanto valores vazios não viram parâmetros. No modo padrão, continua solicitando locale regional, idioma base, imagens sem texto e inglês.

Verificação Windows/.NET 10 Release: testes focais `TmdbUtilsTests` + `ProviderManagerImageProviderTests` 36/36; suíte `Jellyfin.Providers.Tests` 923 aprovados, 14 ignorados, 0 falhas/937. Os ignorados são integrações/precondições existentes. Não houve chamadas live ao TMDb, refresh com mídia real, verificação de arte já persistida, renderização, Linux, runtime ou produção. Dados de imagem existentes não mudam automaticamente.

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

## Locale global nas consultas de imagem — 2026-11-01 08:01 BRT

- `ProviderManager` resolve idioma/país do servidor e encaminha ambos por `ILanguageAwareRemoteImageProvider`; providers antigos mantêm o overload original. Os seis image providers TMDb (Movie, Series, Season, Episode, BoxSet e Person) usam o locale recebido ao consultar imagens.
- `ItemImageProvider` mantém a preferência global como primeira opção e a preferência de item/biblioteca como fallback. `NormalizeLanguageTag` trata separadores e país em qualquer locale, sem anexar país a tags que já tenham subtags. Inglês, imagem sem idioma e demais resultados continuam no ranking de fallback.
- Verificação local Windows/.NET 10 Release: `Jellyfin.Model.Tests` 699/699; `Jellyfin.Providers.Tests` 887 aprovados, 13 integrações externas ignoradas e 0 falhas (900 total).
- Limites: nenhum request TMDb/Fanart, refresh/visualização de biblioteca real, Linux ou produção. Providers externos que não implementam `ILanguageAwareRemoteImageProvider` não recebem locale na requisição; imagens já persistidas não são substituídas por refresh normal.
## Limite da raiz na limpeza de imagem anterior — corrigido em 2026-11-01

A auditoria inicial encontrou `currentImagePath.Contains(InternalMetadataPath)`, que aceitava lexicalmente a pasta irmã `metadata-backup` como descendente de `metadata`. A correção atual substituiu essa decisão por `RootedFileSystem.TryDeleteFile` com raízes explícitas; no Windows, o teste do caller confirma que a pasta irmã permanece intacta. A exclusão só é suportada com proteção handle-relative no Windows; Linux/macOS falham fechados e preservam a imagem antiga. Nenhum arquivo de produção foi alterado durante a reprodução inicial.

Essa correção trata apenas a exclusão da imagem anterior. Não protege a gravação da imagem e não fecha corridas em outras operações por pathname.

## Exclusão handle-relative de imagem anterior — 2026-11-01

`ImageSaver` agora tenta remover a imagem anterior somente sob raízes permitidas: `ContainingFolderPath` quando a gravação local está habilitada e `InternalMetadataPath` para fallback interno. `MediaBrowser.Controller.IO.RootedFileSystem.TryDeleteFile` calcula componentes relativos e executa exclusão handle-relative somente no Windows; mantém abertos todos os handles dos diretórios ancestrais até concluir a exclusão e não segue reparse points descendentes. Em Linux e macOS, lança `PlatformNotSupportedException`; `ImageSaver` captura e registra aviso, preservando a imagem anterior até existir proteção verificada contra troca/renomeação concorrente da árvore. Caminhos fora da raiz e alvos ausentes também não são removidos. A limpeza adicional de diretórios vazios de episódio foi removida para não reintroduzir exclusão por pathname.

Windows/.NET 10 Release: testes focais `RootedFileSystemTests` 4 aprovados, 1 ignorado (5 total); `Jellyfin.Providers.Tests` passou 900 aprovados, 13 integrações externas ignoradas, 0 falhas (913 total). O teste cobre caminho com dois níveis de diretórios, além de descendente, pasta irmã `metadata-backup`, traversal, link de raiz configurada, link de arquivo e bloqueio de diretório simbólico descendente. Em 2026-11-01 foi adicionado `ImageSaverTests` para verificar diretamente o caller: imagem antiga sob a raiz é removida após salvar a nova e pasta irmã `metadata-backup` é preservada. No Windows, 2 cenários passaram; o caso não-Windows foi ignorado neste host. A suíte Providers então passou 902 testes, ignorou 14 integrações opcionais e não teve falhas (916 total). Linux/macOS, runtime instalado e produção não foram verificados; a limpeza antiga nessas plataformas fica desabilitada, portanto arquivos substituídos podem permanecer. No Windows, leitores que neguem compartilhamento de exclusão também podem deixar a imagem antiga; falha é registrada como aviso e não desfaz a nova referência. A escrita da imagem continua pathname-based e faz parte de outra lacuna de auditoria. Homologação formal segue 2/7, sem crédito.
## TMDb: preservar região real da imagem — 2026-11-01

TMDb devolve idioma (`iso_639_1`) e região (`iso_3166_1`) separadamente. O fluxo anterior ignorava a região e promovia qualquer imagem `pt` para o locale solicitado `pt-BR`; uma arte `pt-PT` podia assim ser rotulada como brasileira e ganhar pela avaliação. `TmdbClientManager.ConvertToRemoteImageInfo` agora passa ambos os campos a `TmdbUtils.GetImageLanguage`. O helper preserva tags genéricas em pedidos genéricos, usa o locale solicitado quando a região da imagem coincide e conserva região divergente (por exemplo, `pt-PT` em consulta `pt-BR`). Imagens `xx` permanecem sem idioma.

A conversão é central para imagens TMDb usadas por filmes, séries, temporadas, episódios e coleções; a ordenação compartilhada prioriza locale exato antes de variante regional, inglês e sem idioma. A mesma classe de imagens também inclui animações modeladas como `Series`. Baseado no upstream Jellyfin PR [#18150](https://github.com/jellyfin/jellyfin/pull/18150), commit `1d3ee4d`, mesclado em `release-12.z`.

Validação local Windows/.NET 10 Release: `TmdbUtilsTests` 32/32; `Jellyfin.Model.Tests` ordenação de idiomas 8/8; suíte `Jellyfin.Providers.Tests` 907 aprovados, 14 ignorados, 0 falhas/921 total. Esta rodada não consulta TMDb, não refaz imagens existentes e não valida catálogo/produção, outros provedores (Fanart/TVDB) ou runtime Linux. Imagens já persistidas exigem refresh após instalação. Sem crédito de homologação formal (2/7), sem release publicada.

## Substituição de imagens já existentes — 2026-11-01 16:11 BRT

O refresh comum mantém a capa existente e pode encerrar sem consultar providers. Quando o administrador usa `FullRefresh` com `ReplaceAllImages=true`, o fluxo consulta todos os idiomas e ordena a preferência configurada antes do inglês; o novo teste cobre a troca de pôster inglês persistido por `pt-BR`. Filtro Windows/.NET 10 Release `ItemImageProviderTests|ProviderManagerImageProviderTests|TmdbUtilsTests`: 116 aprovados, 0 ignorados, 0 falhas. O teste usa providers HTTP simulados, não faz chamada ao TMDb. Não foi introduzida substituição automática para evitar trocar arte administrada/local por uma escolha remota; a atualização de itens existentes requer refresh explícito de imagens. Homologação formal 2/7, sem crédito ou release.


## Salvamento atômico de imagem — 2026-11-01 10:36 BRT

`ImageSaver.SaveImageToLocation` grava primeiro em arquivo temporário irmão, copia integralmente a origem e só então promove. Se a cópia falhar/cancelar, o destino anterior permanece intacto e o temporário é removido. Para destino existente, usa `File.Replace`: preserva ACL do arquivo no Windows; no Unix reaplica os bits de modo existentes. Se a promoção falhar, tenta restaurar atributos originais e mantém a exceção original. Novas imagens usam criação sem sobrescrever (`File.Move`), e a limpeza de temporário é registrada quando falha.

Validação Windows/.NET 10 Release: `ImageSaverTests` 4 aprovados, 1 ignorado; suíte Providers sem `DramaFindsLiveIntegrationTests`: 909 aprovados, 13 ignorados, 0 falhas/922. O teste de modo Unix não executa neste host. ACLs Unix estendidas por arquivo não são copiadas; espera-se herança do diretório. Integração GitHub confirmou que os 16 jobs do run 41283703952 não iniciaram por bloqueio de cobrança; os 16 logs por job retornaram `BlobNotFound`/404, sem steps executados. CLI `gh` local permanece sem autenticação. Sem instalação, mídia ou produção; candidata 12.1.46, homologação formal 2/7.

## Revalidação da exclusão confinada do ImageSaver — 2026-11-01 11:10 BRT

Windows/.NET 10 Release: `ImageSaverTests` 4 aprovados, 1 ignorado/5; `RootedFileSystemTests` 4 aprovados, 1 ignorado/5. O teste de integração do caller confirma que `metadata-backup` permanece intacta e que a imagem anterior sob a raiz permitida é removida após a nova gravação. O teste fail-closed não-Windows e o teste de `RootedFileSystem` não-Windows foram ignorados; o cenário de modo Unix retorna sem executar neste host Windows. Linux/macOS continuam fail-closed e podem reter imagens antigas; esses runtimes não foram exercitados. Sem alteração de runtime nesta revalidação, sem crédito formal (2/7) e sem release publicada. Ver `docs/releases/12.1.46.md` e a sessão `2026-11-01-MulletaFlix-imagesaver-rooted-revalidation-1110.md`.

## Auditoria da escrita de imagens por pathname — 2026-11-01 11:18 BRT

O fluxo compartilhado `ImageSaver.SaveImageToLocation` ainda cria o diretório, inspeciona o destino, grava o temporário e promove por pathname. A promoção atômica protege contra conteúdo parcial, mas não ancora a resolução à raiz configurada: symlink/junction descendente ou troca de diretório entre operações pode redirecionar temporário e destino. Avaliação estática; nenhum exploit foi reproduzido.

Os destinos de item e retries convergem nesse writer. `ProviderManager.SaveImage(Stream, mimeType, path)` também encaminha caminho arbitrário; o único caller explícito encontrado é `MulletaFlix.Plugin.GetAvatar.Services.AvatarService`, que deriva caminho sob `UserConfigurationDirectoryPath` e aplica containment lexical antes da gravação. A verificação não elimina troca concorrente. `RootedFileSystem` cobre somente exclusão no Windows.

Revalidação Windows/.NET 10 Release: `ImageSaverTests` 4 aprovados, 1 ignorado; `RootedFileSystemTests` 4 aprovados, 1 ignorado. Esses testes cobrem persistência/substituição e exclusão, não symlink ou troca de pathname durante escrita. Nenhuma mudança runtime. Próxima ação: primitiva handle-relative de criação/substituição com testes adversariais Windows e Linux, preservando permissões/ACLs antes de migrar callers.

## Reauditoria do writer de imagens — 2026-11-01 13:53 BRT

Os overloads de item de `ProviderManager.SaveImage` usados por `MetadataService` e `ItemImageProvider` convergem em `ImageSaver.SaveImageToLocation`; foram encontrados 13 ingressos de produção (5 stream, 2 caminho e 6 URL). O overload explícito `ProviderManager.SaveImage(Stream, mimeType, path)` também chega ao mesmo writer; `AvatarService` e `ImageController` são seus dois callers diretos e passam caminhos de perfil. O contrato de caminho explícito não carrega raiz confiável.

O writer cria diretório por pathname, abre temporário irmão por pathname e promove com `File.Replace` ou `File.Move`. Isso preserva atomicidade contra falha de cópia, mas não confina a operação contra troca concorrente de diretórios/links. `RootedFileSystem` oferece somente exclusão, Windows-only; não pode ser reaproveitado como writer seguro. Não foi encontrada implementação cross-platform já disponível que preserve atomicidade e evite troca da árvore. A mudança segura exige desenhar e testar uma primitiva de gravação rooted antes de migrar `ImageSaver` e o caller de avatar.

Esta sessão confirmou o call graph e as limitações por leitura estática; não executou testes, não mudou código ou mídia e não validou Linux, runtime instalado ou produção. Homologação formal permanece 2/7, sem crédito.

## Revalidação: comparação de imagens com Jellyfin upstream — 2026-11-01 12:08 BRT

O fluxo upstream separa descoberta, ordenação e persistência. `ProviderManager.GetAvailableRemoteImages` reúne resultados dos providers e ordena por idioma preferido; o refresh de `ItemImageProvider` pode pedir todas as línguas para manter fallbacks e tenta a primeira candidata elegível para imagens singulares. O código upstream TMDb consultado nesta data ainda solicita o catálogo amplo em alguns image providers; a preferência final depende da ordenação de `RemoteImageInfo` e dos metadados de idioma retornados. `master` é móvel e não identifica uma release específica.

No fork, `ProviderManager` resolve locale global/item/biblioteca, aplica ranking global e `RemoteImageController` devolve a sequência sem reordenar. Os providers TMDb language-aware também pedem a língua/região preferida com variantes sem idioma e inglês de fallback. Os testes focados reexecutados em Windows/.NET 10 Release passaram: `ProviderManagerTests.GetAvailableRemoteImages`: 3/3; `ItemImageProviderTests.RefreshImages_Remote`: 26/26; `TmdbUtilsTests`: 32/32; `RemoteImageControllerTests`: 8/8.

Limites confirmados: providers externos que não implementam `ILanguageAwareRemoteImageProvider` não recebem a preferência na própria requisição; o caminho legado `MetadataService.SaveRemoteResultImages` recebe tuplas URL/tipo sem idioma e não pode reordená-las com segurança sem preservar a proveniência; refresh comum mantém uma imagem singular já existente salvo substituição explícita. Assim, arte inglesa antiga pode continuar até refresh com substituição/ação explícita. Nenhuma alteração de runtime nesta sessão.

Fontes primárias: [Jellyfin ProviderManager](https://github.com/jellyfin/jellyfin/blob/master/MediaBrowser.Providers/Manager/ProviderManager.cs), [Jellyfin ItemImageProvider](https://github.com/jellyfin/jellyfin/blob/master/MediaBrowser.Providers/Manager/ItemImageProvider.cs), [Jellyfin MetadataService](https://github.com/jellyfin/jellyfin/blob/master/MediaBrowser.Providers/Manager/MetadataService.cs), [TMDb image utilities](https://github.com/jellyfin/jellyfin/blob/master/MediaBrowser.Providers/Plugins/Tmdb/TmdbUtils.cs) e [TMDb client manager](https://github.com/jellyfin/jellyfin/blob/master/MediaBrowser.Providers/Plugins/Tmdb/TmdbClientManager.cs).

Não verificados nesta sessão: API TMDb/Fanart ao vivo, providers externos, imagens reais já persistidas, renderização visual, Linux, runtime instalado ou produção. A validação comprova lógica local sob testes, não a correção visual do catálogo de produção.

## Proveniência de idioma nas imagens do fluxo legado — 2026-11-01 12:18 BRT

O contrato `MetadataResult.RemoteImages` contém apenas `(Url, ImageType)`. O parser NFO cria esses pares a partir de URLs de `<thumb>`/imagens locais e `BaseVideoNfoProvider` os copia. OpenLibrary também usa esse contrato para capas remotas; sua conversão para `RemoteImageInfo` define provedor, URL e tipo, mas não idioma. `MetadataService.SaveRemoteResultImages` consome essas URLs durante o refresh de metadados e salva a primeira imagem elegível, antes do fluxo independente de seleção `ItemImageProvider`. Portanto, esses candidatos não podem ser ordenados por idioma nessa etapa.

`MetadataResult.ResultLanguage` descreve o idioma dos metadados textuais do título, não o idioma comprovado da arte. Não deve ser copiado para uma capa como substituto de proveniência. NFO não oferece esse campo por imagem e o identificador/URL de capa do OpenLibrary não comprova o idioma impresso. O contrato público é usado por produtores e consumidores existentes; trocá-lo por `RemoteImageInfo` seria uma alteração incompatível sem informação de idioma disponível nas fontes examinadas.

Decisão: manter o contrato legado e não inventar idioma. Para corrigir esta limitação com evidência, uma fonte precisa fornecer idioma por imagem; somente então se pode adicionar um caminho compatível e ordená-lo antes do salvamento. Os provedores comuns de imagem (`IRemoteImageProvider`) continuam cobertos pelo ordenamento compartilhado e pelo locale regional do TMDb.

Verificação local Windows/.NET 10 Release em 2026-11-01: `Jellyfin.Model.Tests` com `FullyQualifiedName~EnumerableExtensionsTests` passou 21/21; `Jellyfin.Providers.Tests` filtrados para preferência de idioma no `ProviderManager`, locale do `ItemImageProvider` e idioma/fallback TMDb passaram 23/23. A revisão é de código e testes; sem chamadas live aos provedores, refresh de livros/mídias, execução Linux, renderização ou produção. Sem alteração de runtime, release ou crédito formal (2/7). O mapa técnico foi conferido e permanece estruturalmente igual.
## Consulta TMDb respeita `IncludeAllLanguages` — 2026-11-01

`ProviderManager` já ignorava a filtragem local quando `RemoteImageQuery.IncludeAllLanguages` estava ativo, mas os providers TMDb de filmes, séries, temporadas, episódios e coleções continuavam limitando a resposta da API ao conjunto de idioma/região. O overload language-aware agora recebe a opção; em modo todos, TMDb é consultado sem `include_image_language`. Em modo padrão, preserva-se locale/região, idioma base, imagens sem texto e inglês. O overload default mantém compatibilidade com providers/plugins existentes. Foco 33/33; suíte Providers 920 aprovados, 14 ignorados, 0 falhas/934. Sem chamada live ou refresh de itens já persistidos.

## Cache de lista de estúdios e fronteira de links no Nebula — 2026-11-01 16:31 BRT

`StudiosImageProvider.EnsureList` gravava a resposta HTTP diretamente no índice local com `FileMode.Create`, truncando conteúdo anterior antes do fim da transferência. Agora grava em temporário exclusivo no mesmo diretório e só promove após cópia completa; falha/cancelamento preserva a versão existente e remove o temporário. Testes focais cobrem resposta interrompida e promoção concluída.

A auditoria também encontrou que a enumeração recursiva de `MonitorPaths` podia alcançar mídia por reparse points/symlinks fora da raiz, enquanto o downloader contém fluxos que removem origem duplicada ou concluída. Raízes configuradas com reparse points agora são rejeitadas; varreduras recursivas ignoram reparse points; limpadores percorrem apenas diretórios normais e removem diretórios vazios sem exclusão recursiva. Testes cobrem raiz linkada e arquivo externo sob subdiretório linkado. A proteção é contra links presentes durante a enumeração: a sequência ainda usa caminhos e não elimina uma troca concorrente de diretório entre validação e uso. Junctions Windows e a corrida destrutiva não foram testados; o hardening rooted continua pendente.

Validação local Windows/.NET 10 Release: `NebulaPathSafetyTests|NebulaDownloaderEngineTests` 51/51; `StudiosImageProviderTests` 3/3; `Jellyfin.Providers.Tests` 927 aprovados, 14 ignorados, 0 falhas/941. `dotnet format style --verify-no-changes` passou para os arquivos modificados. Não inspecionados links em produção, fluxos de upload separados, Linux, runtime instalado ou transferência real. Alteração interna sem novo módulo, API pública, dependência ou componente; mapa técnico conferido e permanece estruturalmente igual. Homologação formal 2/7, sem crédito ou release.
