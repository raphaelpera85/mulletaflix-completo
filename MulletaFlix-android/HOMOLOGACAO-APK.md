# Homologação formal — MulletaFlix Android

**Data da revisão:** 07/10/2026
**Estado:** em andamento — aceite final não concedido
**Escopo:** somente aplicativo Android (APK), seu transporte e testes; nenhum componente do servidor foi alterado e nenhuma release foi criada/publicada.

## Decisão de homologação

O APK ainda **não está homologado para aceite final**. Há 11 áreas concluídas com evidência técnica local, 1 em validação e 9 pendentes entre as 21 áreas do contrato público `portal-site/homologacao-status.json`. Nove gates dependem de servidor/conteúdo reais, dispositivos físicos, receiver ou assinatura/instalação de produção. Os estados “validado localmente” não significam compatibilidade universal nem aprovação da release.

## Evidência automatizada desta rodada

- `./gradlew.bat testDebugUnitTest :core:common:lintDebug :core:api:lintDebug :feature:item-detail:lintDebug :feature:player:lintDebug :app:lintDebug :app:assembleDebug --console=plain`: BUILD SUCCESSFUL em 07/10/2026; relatórios Gradle totalizam 1.426 testes aprovados, 0 falhas/erros e 0 ignorados. Inclui teste comportamental que prova que o guard rejeita proxy HTTP antes de enviar bytes da requisição e teste da configuração HTTPS-only para redirects do atualizador. `BookReaderContentsTest` também verifica cabeçalho EPUB `<span>` sem ação de navegação. `BookReaderPayloadPolicyTest` cobre recuperação de EPUB/CBZ órfão, retry após exclusão falsa/negada, preservação de arquivo fora do padrão e coexistência de livro ativo. A fixture representa arquivos sobreviventes de execução anterior, mas não mata/reinicia o processo Android.
- `:core:api:connectedDebugAndroidTest` — `CleartextAwareMediaDataSourceHttpsTest`: 1/1 aprovado no AVD `MulletaflixApi35` (API 35). Exercitou leitura real via Media3/OkHttp sobre HTTPS com servidor TLS de teste e certificado confiável. O wrapper confirmou QEMU na GPU NVIDIA e encerrou o AVD.
- `:app:connectedDebugAndroidTest` — `CoilHttpsArtworkIntegrationTest`: 2/2 aprovados no AVD PHONE API 35. Coil baixou e decodificou PNG HTTPS com certificado de teste confiável pelo `buildAuthenticatedImageClient`, conferiu o token de sessão e bloqueou redirect HTTPS→HTTP público sem emitir segunda requisição. QEMU confirmado na GPU NVIDIA RTX 3050; wrapper encerrou o AVD.
- `:app:connectedDebugAndroidTest` — `AndroidCleartextPolicyIntegrationTest`: 3/3 aprovados no AVD PHONE API 35. Verificou a configuração Android empacotada, permitiu peer calculado dentro do prefixo LAN ativo e negou IP privado fora das sub-redes locais; também concluiu requisição HTTP real pelo cliente protegido até um socket vinculado ao endereço da interface ativa. Não representa comunicação com outro dispositivo físico na mesma LAN, nem bloqueio global pelo SO; sem requisição externa. Wrapper confirmou QEMU na NVIDIA RTX 3050 e encerrou o AVD.
- `:feature:item-detail:connectedDebugAndroidTest` — `BookReaderHttpPolicyIntegrationTest`: 4/4 aprovados no AVD PHONE API 35. O adapter Readium abriu stream HTTP local, recusou URL pública e redirect de host local para HTTP público sem alcançar o destino, e preservou redirect local same-origin. DNS de teste foi fixado em loopback para eliminar tráfego externo. O wrapper confirmou QEMU na NVIDIA RTX 3050 e encerrou o AVD.
- Nesta rodada, `:core:api:connectedDebugAndroidTest` — `AppUpdateHttpsRedirectIntegrationTest`: 4/4 aprovados no AVD PHONE API 35. O atualizador bloqueou redirect HTTPS→HTTP loopback sem alcançar o destino, aceitou CDN HTTPS, removeu headers Authorization/Cookie/Proxy-Authorization e credenciais da URL em redirect cross-origin, retirou `access_token`, preservou cursor/assinatura do destino e encerrou a cadeia no limite de 20 redirects. DNS ficou preso ao loopback. Wrapper confirmou QEMU na NVIDIA RTX 3050 e encerrou o AVD.
- `:app:assembleDebugAndroidTest`: BUILD SUCCESSFUL. `DownloadManagerCleartextIntegrationTest`: 1/1 via `adb shell am instrument` no AVD PHONE API 35 dedicado, serial `emulator-5560`; download LAN de 64 KiB concluído e presente em cache temporário, redirect público terminou em falha sem aumento do cache. Banco SQLite e cache têm nomes temporários únicos; o wrapper validou o processo QEMU na NVIDIA RTX 3050 e encerrou apenas esse AVD. O emulador preexistente foi preservado.
- `:core:api:assembleDebugAndroidTest :core:api:lintDebug`: BUILD SUCCESSFUL. `DownloadManagerHttpsIntegrationTest`: 1/1 via `adb shell am instrument`; download remoto HTTPS com certificado TLS de teste confiável completou, confirmou 64 KiB e persistência no `SimpleCache` isolado. AVD PHONE API 35 dedicado usou GPU NVIDIA e foi encerrado pelo wrapper.
- `:core:api:assembleDebugAndroidTest :feature:player:lintDebug`: BUILD SUCCESSFUL. `ExternalSubtitleHttpsPlaybackIntegrationTest`: 1/1; ExoPlayer requisitou mídia e legenda num servidor TLS de teste, decodificou o cue SRT e não registrou erro. O teste usa a fábrica Media3 com a política de transporte de produção e certificado confiável de teste. AVD PHONE API 35 usou a NVIDIA RTX 3050 e foi encerrado pelo wrapper.
- `:core:api:lintDebug`, `:feature:player:lintDebug`, `:app:lintDebug`, `:core:api:compileDebugAndroidTestKotlin`, `:feature:player:compileDebugAndroidTestKotlin`, `:app:compileDebugAndroidTestKotlin` e `:app:assembleDebug`: BUILD SUCCESSFUL (740 tarefas; warnings de depreciação preexistentes nos testes de player).
- `node --test portal-site/tests/homologacao.test.mjs`: 4/4 aprovados.
- `.\tools\Validate-PortalSitemap.ps1`: 7 URLs públicas cobertas; nenhuma página nova foi criada.

## Áreas com evidência técnica local registrada — 11/21

- **APK-H01** — Arquitetura modular e infraestrutura do cliente.
- **APK-H02** — Autenticação, sessão e troca/isolamento de conta e servidor.
- **APK-H03** — Home, navegação e recuperação de conteúdo.
- **APK-H04** — Bibliotecas, paginação, capas, filtros e ordenação.
- **APK-H05** — Detalhes de mídia, temporadas e episódios.
- **APK-H06** — Player, faixas, legendas e controles.
- **APK-H07** — Downloads e reprodução offline.
- **APK-H08** — Leitura EPUB/CBZ.
- **APK-H09** — TV ao vivo e EPG.
- **APK-H10** — SyncPlay e continuidade de sessão.
- **APK-H11** — UX adaptativa, tablet, Android TV e controle remoto.

Essas classificações agregam evidências locais preexistentes registradas em `TODO-APP.md`, mais os testes executados nesta rodada. São validações técnicas locais; não significam aceite integrado, validação física ou certificação de produção.

## Em homologação — 1/21

| ID | Área | Evidência atual | Saída exigida |
|---|---|---|---|
| APK-H12 | Transporte seguro: HTTPS remoto e HTTP restrito à LAN | Suíte JVM registrada: 1.426 aprovados, 0 falhas/erros/ignorados; Media3/player instrumentado 5/5, leitura Media3 HTTPS via TLS de teste 1/1, DownloadManager HTTP LAN 1/1 e HTTPS remoto 1/1, ExoPlayer com legenda externa HTTPS decodificada 1/1 e redirect de legenda HTTPS→HTTP público bloqueado 1/1; Coil HTTPS instrumentado 1/1 e redirect Coil HTTPS→HTTP público bloqueado 1/1. Nesta rodada, `ExternalSubtitleMediaItemTest` instrumentado no PHONE API 35 passou 6/6; com sub-rede LAN controlada, o payload Cast mantém HTTPS e HTTP autorizado e omite HTTP público. Os testes da política de rede cobrem separadamente autorização pela sub-rede ativa. Downloads validam 64 KiB no cache isolado e redirect público HTTP falho sem bytes extras. Retrofit bloqueia HTTP público antes do token; Coil nega URL HTTP público direto antes de ler credenciais; artwork HTTP LAN tem round-trip testado. Os dois clientes do updater rejeitam redirect para HTTP; integração prova downgrade bloqueado, CDN HTTPS, remoção cross-origin de Authorization/Cookie/Proxy-Authorization e userinfo, remoção de `access_token`, preservação de cursor/assinatura de destino e limite de 20 redirects (4/4). Prefixos IPv4/IPv6, DNS local resolvido fora da sub-rede, requisição protegida pela interface do AVD, recusa de proxy e proteção do cliente Readium com redirects locais/públicos têm testes locais. | Android-base ainda permite cleartext para LAN dinâmica; guards protegem os clientes, sem bloqueio global do SO. O AVD não substitui teste entre dispositivos físicos na mesma LAN. É necessário fechar a política do sistema e restante da matriz antes do aceite formal. |

## Pendente — 9/21

| ID | Área | Saída exigida |
|---|---|---|
| APK-H13 | Conexão à instância e cenários de servidor real | Executar E2E autenticado com catálogo e mídia reais. |
| APK-H14 | Descoberta e troca automática entre LAN e internet | Validar descoberta na mesma LAN física e transição entre redes/endereços. |
| APK-H15 | Streaming real, retomada e seleção de idioma/legenda | Testar mídia real, seeks, retries, codecs, faixas e legendas servidas pelo servidor. |
| APK-H16 | Cast para receiver compatível | Testar receiver Chromecast/Web real, sessão e legendas externas. |
| APK-H17 | Downloads com falha, espaço e recuperação em aparelho | Testar espaço limitado, interrupção/reinício e limpeza de cache em dispositivos-alvo. |
| APK-H18 | Instalação, atualização e assinatura de produção | Conferir certificado oficial, instalação e atualização sobre o APK anterior; nenhum APK de produção foi gerado nesta homologação. |
| APK-H19 | Aceite em dispositivos físicos e matriz final | Validar celulares, tablets e TVs físicas, fabricantes, Wi-Fi e controle remoto. |
| APK-H20 | Aceite formal do responsável pelo produto | Fechar a matriz e registrar aprovação explícita do responsável. |
| APK-H21 | Leitura de EPUB/CBZ/PDF servidos pela instância real | APK local agora detecta PDF e CBZ por conteúdo quando o MIME é genérico; teste instrumentado PHONE valida PDF sintético, navegação, progresso e marcador persistidos/restaurados. Ainda falta validar API autenticada, arquivos/MIMEs reais e retomada nos dispositivos-alvo. Conversão de MOBI/AZW/TXT/HTML continua dependente do conversor do servidor. |

A matriz completa, com evidência por área e estado renderizado no portal, está em `portal-site/homologacao-status.json` e na página pública `/homologacao`. Os 21 IDs são a unidade de homologação funcional. Os checkboxes do backlog (`TODO-APP.md`) são tarefas de engenharia distintas e não devem ser convertidos em percentual de aceite.

## Gates ainda abertos

1. Conexão autenticada E2E ao servidor real com catálogo e mídia configurados.
2. Descoberta/troca automática LAN ↔ internet validada na mesma rede física.
3. Reprodução de conteúdo real, retomada/seeks, codecs, idiomas e legendas.
4. Cast ponta a ponta com receiver Chromecast/Web compatível e legendas externas.
5. Downloads em aparelho com pouco espaço, interrupção/reinício e limpeza de cache.
6. Aceite em TVs, tablets e celulares físicos com controles e redes diversos.
7. Validação de EPUB/CBZ servidos pela instância real, MIME/autenticação e retomada em dispositivos-alvo.
8. Verificação da assinatura/keystore vigente e compatibilidade de instalação/atualização de produção.
9. Aceite formal do responsável pelo produto após fechar toda a matriz.

O HTTP em cleartext necessário à LAN permanece uma exceção arquitetural que requer guards em todos os transportes do aplicativo. O sucesso dos testes focados ainda não prova comunicação entre dispositivos físicos nem fecha a matriz em dispositivos reais. A política Android de base ainda admite cleartext para suportar endereços privados dinâmicos; não anunciar bloqueio global do SO.

## Inventário do backlog

`TODO-APP.md` permanece a fonte linha a linha para implementação e histórico de releases. Do início do arquivo até imediatamente antes do cabeçalho “Funcionalidades APK validadas localmente, ainda não publicadas”, há 227 checkboxes marcadas e 48 abertas (275 no total). A contagem exclui as checklists históricas/arquivadas depois desse ponto e mede backlog de engenharia, não a cobertura formal nem o percentual de aceite.

## Evidência adicionada nesta rodada

- Cast e legendas externas: após converter o item em payload de receiver, somente sidecars aceitos pela política são exportados. `ExternalSubtitleMediaItemTest` cobre HTTPS, HTTP de uma sub-rede LAN controlada e HTTP público descartado; passou 6/6 no AVD PHONE API 35. Os testes de política de rede verificam separadamente a autorização por sub-rede ativa. A revisão independente confirmou que o converter padrão não reenvia configurações descartadas por caminho paralelo. Isso não valida redirects do receiver nem uma sessão Chromecast real; APK-H16 segue pendente.
- Sumário EPUB: ação acessível só aparece quando o EPUB contém TOC; preserva capítulos aninhados, expõe nível semântico e resolve navegação via `Publication.url(link)`. Grupos `<span>` sem destino são headings sem ação; links com filhos continuam navegáveis. O teste unitário focal e o teste instrumentado verificaram essa distinção. A fixture escolheu “Capítulo 2” e confirmou o texto exclusivo no DOM do WebView: `BookReaderScreenIntegrationTest` passou 1/1 em PHONE API 35 e 1/1 em TABLET API 35. Ambos AVDs foram abertos pelo wrapper, QEMU confirmado na NVIDIA RTX 3050 e encerrados ao fim. Isto valida navegação e semântica Compose, não TalkBack manual nem servidor/catálogo real; APK-H21 segue pendente.
- Quality Bar Android desta rodada: `testDebugUnitTest` global, compilação AndroidTest, `:feature:item-detail:lintDebug`, `:app:lintDebug` e `:app:assembleDebug`: `BUILD SUCCESSFUL`. Artefato local debug criado; não é APK de produção e não foi publicado.
- Integração instrumentada da Biblioteca: catálogo salvo pelo `LibraryCatalogCacheRepositoryImpl` no DataStore, restauração pela tela real após recriação de composição/ViewModel sem rede, prévia offline e atualização após retorno da conectividade. O servidor e a mídia são fakes; não houve encerramento do processo Android. APK-H04 continua com evidência técnica local, sem mudar seu aceite formal ou a contagem de áreas.
- Verificação local: `:feature:library:connectedDebugAndroidTest` (teste focal 1/1 aprovado); `:feature:library:testDebugUnitTest :feature:library:lintDebug` (`BUILD SUCCESSFUL`). AVD PHONE API 35 executado via wrapper, QEMU na NVIDIA RTX 3050, emulador encerrado ao final.
- Leitor EPUB: `BookReaderScreenIntegrationTest` percorre Compose → ViewModel → Retrofit → fixture HTTP local: recebe HTTP 503, exibe retry, abre EPUB válido, verifica TOC aninhado e nível semântico, escolhe “Capítulo 2” e encontra seu texto exclusivo no DOM do WebView. 1/1 aprovado em PHONE API 35 e 1/1 em TABLET API 35. Não usa servidor/catálogo real nem substitui avaliação manual de TalkBack; APK-H21 permanece pendente. Ambos AVDs usaram QEMU na NVIDIA RTX 3050 e foram encerrados pelo wrapper.
- Verificações focais do leitor: `:feature:item-detail:connectedDebugAndroidTest` com `expectedDeviceProfile=PHONE` e depois `TABLET`, classe `org.mulletaflix.feature.itemdetail.BookReaderScreenIntegrationTest`: ambos `BUILD SUCCESSFUL`, 1 teste, 0 falhas/erros/ignorados por perfil.

## Fechamento

Para concluir, executar e registrar os nove gates abertos, revisar todas as evidências e obter aprovação explícita do responsável. Não criar ou publicar release como parte desta homologação. As notas/artefatos de release ficam para uma solicitação futura e devem corresponder apenas a mudanças realmente testadas.
