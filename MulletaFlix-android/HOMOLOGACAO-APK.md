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
- `node --test portal-site/tests/homologacao.test.mjs`: 3/3 aprovados.
- `.\tools\Validate-PortalSitemap.ps1`: 7 URLs públicas cobertas; nenhuma página nova foi criada.

## Áreas com evidência técnica local registrada

1. Arquitetura modular e infraestrutura do cliente.
2. Autenticação, sessão e troca/isolamento de conta e servidor.
3. Home, navegação e recuperação de conteúdo.
4. Bibliotecas, paginação, capas, filtros e ordenação.
5. Detalhes de mídia, temporadas e episódios.
6. Player, faixas, legendas e controles.
7. Downloads e reprodução offline.
8. Leitura EPUB/CBZ.
9. TV ao vivo e EPG.
10. SyncPlay e continuidade de sessão.
11. UX adaptativa, tablet, Android TV e controle remoto.
12. Transporte de rede, sujeito ao gate específico abaixo.

Essas classificações agregam evidências locais preexistentes registradas em `TODO-APP.md`, mais os testes executados nesta rodada. Cada limite (p.ex., falta de servidor, receiver ou aparelho físico) está explicitado na matriz pública; não inferir aceite manual de uma caixa marcada.

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
