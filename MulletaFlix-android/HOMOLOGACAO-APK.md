# Homologação formal — MulletaFlix Android

**Data da revisão:** 05/10/2026
**Estado:** em andamento — aceite final não concedido
**Escopo:** somente aplicativo Android (APK), seu transporte e testes; nenhum componente do servidor foi alterado e nenhuma release foi criada/publicada.

## Decisão de homologação

O APK ainda **não está homologado para aceite final**. Há 11 áreas concluídas com evidência técnica local, 1 em validação e 9 pendentes entre as 21 áreas do contrato público `portal-site/homologacao-status.json`. Nove gates dependem de servidor/conteúdo reais, dispositivos físicos, receiver ou assinatura/instalação de produção. Os estados “validado localmente” não significam compatibilidade universal nem aprovação da release.

## Evidência automatizada desta rodada

- `./gradlew testDebugUnitTest --no-daemon --no-parallel --console=plain`: BUILD SUCCESSFUL; relatórios Gradle atuais totalizam 1.399 testes aprovados, 0 falhas/erros e 0 ignorados.
- `:core:api:connectedDebugAndroidTest` — `CleartextAwareMediaDataSourceHttpsTest`: 1/1 aprovado no AVD `MulletaflixApi35` (API 35). Exercitou leitura real via Media3/OkHttp sobre HTTPS com servidor TLS de teste e certificado confiável. O wrapper confirmou QEMU na GPU NVIDIA e encerrou o AVD.
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
| APK-H12 | Transporte seguro: HTTPS remoto e HTTP restrito à LAN | Reexecução seletiva: 552 testes JVM aprovados nos módulos `core:common`, `core:api`, `app` e `feature:player`; Media3/player instrumentado 5/5 e Media3 HTTPS com TLS 1/1 registrados. Retrofit bloqueia HTTP público antes de consultar token; testes de redirecionamento LAN/público existem. | Faltam Coil com HTTP público direto e HTTPS remoto, transferência real do DownloadManager em LAN/HTTPS, HTTPS/redirecionamentos de legenda e sanitização/testes de credenciais adicionais (`password`, `auth`, userinfo, `Cookie`, `Proxy-Authorization`). A configuração Android-base ainda permite cleartext para LAN dinâmica; a proteção depende dos guards em cada cliente, não é bloqueio global do SO. |

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

A matriz completa, com evidência por área e estado renderizado no portal, está em `portal-site/homologacao-status.json` e na página pública `/homologacao`. Os 21 IDs são a unidade de homologação funcional. Os 266 checkboxes do backlog (`TODO-APP.md`) são tarefas de engenharia distintas e não devem ser convertidos em percentual de aceite.

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

`TODO-APP.md` permanece a fonte linha a linha para implementação e histórico de releases. Do início do arquivo até imediatamente antes do cabeçalho “Funcionalidades APK validadas localmente, ainda não publicadas”, há 219 checkboxes marcadas e 47 abertas (266 no total). A contagem exclui as checklists históricas/arquivadas depois desse ponto e mede backlog de engenharia, não a cobertura formal nem o percentual de aceite.

## Fechamento

Para concluir, executar e registrar os nove gates abertos, revisar todas as evidências e obter aprovação explícita do responsável. Não criar ou publicar release como parte desta homologação. As notas/artefatos de release ficam para uma solicitação futura e devem corresponder apenas a mudanças realmente testadas.
