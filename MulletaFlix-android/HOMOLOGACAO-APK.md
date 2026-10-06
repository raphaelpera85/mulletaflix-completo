# Homologação formal — MulletaFlix Android

**Data da revisão:** 05/10/2026
**Estado:** em andamento — aceite final não concedido
**Escopo:** somente aplicativo Android (APK), seu transporte e testes; nenhum componente do servidor foi alterado e nenhuma release foi criada/publicada.

## Decisão de homologação

O APK ainda **não está homologado para aceite final**. Há 11 áreas concluídas com evidência técnica local, 1 em validação e 9 pendentes entre as 21 áreas do contrato público `portal-site/homologacao-status.json`. Nove gates dependem de servidor/conteúdo reais, dispositivos físicos, receiver ou assinatura/instalação de produção. Os estados “validado localmente” não significam compatibilidade universal nem aprovação da release.

## Evidência automatizada desta rodada

- `./gradlew testDebugUnitTest --no-daemon --no-parallel --console=plain`: BUILD SUCCESSFUL; relatórios Gradle atuais totalizam 1.400 testes aprovados, 0 falhas/erros e 0 ignorados.
- `:core:api:connectedDebugAndroidTest` — `CleartextAwareMediaDataSourceHttpsTest`: 1/1 aprovado no AVD `MulletaflixApi35` (API 35). Exercitou leitura real via Media3/OkHttp sobre HTTPS com servidor TLS de teste e certificado confiável. O wrapper confirmou QEMU na GPU NVIDIA e encerrou o AVD.
- `:app:connectedDebugAndroidTest` — `CoilHttpsArtworkIntegrationTest`: 2/2 aprovados no AVD PHONE API 35. Coil baixou e decodificou PNG HTTPS com certificado de teste confiável pelo `buildAuthenticatedImageClient`, conferiu o token de sessão e bloqueou redirect HTTPS→HTTP público sem emitir segunda requisição. QEMU confirmado na GPU NVIDIA RTX 3050; wrapper encerrou o AVD.
- `:app:connectedDebugAndroidTest` — `AndroidCleartextPolicyIntegrationTest`: 1/1 aprovado no AVD PHONE API 35. Verificou a configuração Android efetivamente empacotada: domínio público e subdomínio MulletaFlix negados por HTTP e IP LAN dinâmico permitido; verificou também o guard Kotlin para HTTPS, HTTP LAN e rejeição de IP/domínio público. Sem requisição externa; wrapper encerrou o AVD.
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
| APK-H12 | Transporte seguro: HTTPS remoto e HTTP restrito à LAN | Suíte JVM registrada: 1.400 testes aprovados; Media3/player instrumentado 5/5, leitura Media3 HTTPS via TLS de teste 1/1, DownloadManager HTTP LAN 1/1 e HTTPS remoto 1/1, ExoPlayer com legenda externa HTTPS decodificada 1/1 e redirect de legenda HTTPS→HTTP público bloqueado 1/1; Coil HTTPS instrumentado 1/1 e redirect Coil HTTPS→HTTP público bloqueado 1/1. Downloads validam 64 KiB no cache isolado e redirect público HTTP falho sem bytes extras. Retrofit bloqueia HTTP público antes do token; Coil nega URL HTTP público direto antes de ler credenciais; artwork HTTP LAN tem round-trip testado. Redirects cross-origin removem credenciais reconhecidas e preservam cursores/assinaturas de destino. | Android-base ainda permite cleartext para LAN dinâmica; guards protegem os clientes, sem bloqueio global do SO. É necessário fechar a política do sistema e restante da matriz antes do aceite formal. |

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
| APK-H21 | Leitura de EPUB/CBZ servidos pela instância real | Validar API autenticada, MIME/respostas, arquivos reais e retomada nos dispositivos-alvo. |

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

O HTTP em cleartext necessário à LAN permanece uma exceção arquitetural que requer guards em todos os transportes do aplicativo. O sucesso do teste HTTPS de Media3 sozinho não fecha os cenários públicos HTTP, redirects, Retrofit, imagens, legendas e downloads. A política Android de base ainda admite cleartext para suportar endereços privados dinâmicos; não anunciar bloqueio global do SO.

## Inventário do backlog

`TODO-APP.md` permanece a fonte linha a linha para implementação e histórico de releases. Do início do arquivo até imediatamente antes do cabeçalho “Funcionalidades APK validadas localmente, ainda não publicadas”, há 227 checkboxes marcadas e 48 abertas (275 no total). A contagem exclui as checklists históricas/arquivadas depois desse ponto e mede backlog de engenharia, não a cobertura formal nem o percentual de aceite.

## Evidência adicionada nesta rodada

- Integração instrumentada da Biblioteca: catálogo salvo pelo `LibraryCatalogCacheRepositoryImpl` no DataStore, restauração pela tela real após recriação de composição/ViewModel sem rede, prévia offline e atualização após retorno da conectividade. O servidor e a mídia são fakes; não houve encerramento do processo Android. APK-H04 continua com evidência técnica local, sem mudar seu aceite formal ou a contagem de áreas.
- Verificação local: `:feature:library:connectedDebugAndroidTest` (teste focal 1/1 aprovado); `:feature:library:testDebugUnitTest :feature:library:lintDebug` (`BUILD SUCCESSFUL`). AVD PHONE API 35 executado via wrapper, QEMU na NVIDIA RTX 3050, emulador encerrado ao final.

## Fechamento

Para concluir, executar e registrar os nove gates abertos, revisar todas as evidências e obter aprovação explícita do responsável. Não criar ou publicar release como parte desta homologação. As notas/artefatos de release ficam para uma solicitação futura e devem corresponder apenas a mudanças realmente testadas.
