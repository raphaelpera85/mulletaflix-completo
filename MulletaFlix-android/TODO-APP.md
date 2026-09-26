# MulletaFlix Android - Plano de Desenvolvimento & Checklist de Funcionalidades (TODO)

Este documento rastreia o status de implementação de todas as funcionalidades, módulos, telas e componentes do aplicativo oficial **MulletaFlix Android**.

## APK local v1.3.72 — Espaço por download e OSD de TV

### Notas do candidato

- Cada download mostra o espaço já armazenado no dispositivo e, quando conhecido, o tamanho total do arquivo.
- A fila pode ser ordenada por maior ou menor uso; itens sem tamanho medido ficam no fim e empates seguem o título.
- Durante a reprodução na Android TV, os controles do player desaparecem após 3 segundos e voltam com as setas/OK do controle remoto; em pausa, permanecem visíveis.
- Cancelar uma operação de SyncPlay libera o controle para uma nova tentativa.
- Mantém no APK candidato o Quick Connect e os formulários de solicitação de mídia/relato de reprodução da v1.3.71; o envio de feedback ainda depende das rotas correspondentes no servidor.

### Validação

- [x] Consulta à API confirmou a release de APK anterior `app-v1.3.70`; a tag `app-v1.3.71` não existe no GitHub.
- [x] Testes JVM cobrem ordenação, tamanhos ausentes/negativos, totais conhecidos, visibilidade do OSD na reprodução/pausa e teclas de revelação.
- [x] Teste Compose com relógio virtual confirma OSD oculto depois de 3 s reproduzindo e visível durante pausa.
- [x] Downloads instrumentados em telefone, tablet e TV: 10/10 em cada perfil, 0 falhas/ignorados.
- [x] Player instrumentado em Android TV: 31 cenários, 30 aprovados e 1 ignorado (Cast indisponível nesse perfil), 0 falhas.
- [x] Suíte JVM completa: 1.112 testes, 0 falhas, 0 erros, 0 ignorados; `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] APK local v1.3.72 (`versionCode=373`), pacote `org.mulletaflix.android`, assinatura v2 válida, 7.437.635 bytes, SHA-256 `143650D41584B67012B92046013B237FB07C889AF93B27E4FFF91771E0D25F97`.
- [ ] Não publicar até confirmar que os endpoints de feedback do servidor foram implantados; esta rodada mantém escopo somente APK.

### Correção local posterior — foco e temporizador do OSD na Android TV

- A tela do player agora recebe foco de controle remoto e cada tecla de navegação/seleção reinicia os 3 segundos de inatividade; controles não desaparecem no meio da navegação. Reprodução pausada continua mantendo o OSD aberto.
- Regressão Compose cobre o reinício do temporizador após interação; a suíte instrumentada do player na TV executou 32 testes, 31 aprovados e 1 ignorado (receptor Cast indisponível), sem falhas. Suíte JVM do módulo player aprovada.
- `:app:lintDebug` e `:app:assembleDebug`: `BUILD SUCCESSFUL`; emulador Android TV encerrado pelo wrapper.
- O APK/hash listado acima é o candidato anterior à correção e **não contém esta alteração**. Nenhuma nova versão/release foi criada nem publicada; gerar APK candidato atualizado exige revisar a versão/artefato antes da próxima release.

### Filtros avançados da Biblioteca — alteração local sem release

- A Biblioteca permite filtrar por gêneros, anos e classificação indicativa. Os critérios ficam em edição até confirmar; cancelar descarta alterações, limpar remove filtros ativos e anos inválidos impedem a aplicação.
- Os filtros são encaminhados à API no formato esperado pelo servidor: gêneros/classificações separados por `|` e anos por vírgula. Valores ativos são mantidos ao paginar e combinados com favoritos/assistidos.
- `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleDebug`: `BUILD SUCCESSFUL`; cobertura de filtros e contrato da API incluídos.
- Biblioteca instrumentada na Android TV: 20/20 testes, 0 ignorados e 0 falhas. Testes do diálogo no tablet: 5 cenários, 4 executados e aprovados; o caso exclusivo de D-pad foi corretamente ignorado nesse perfil.
- A versão oficial anterior continua sendo `v1.3.70`; nenhum version bump, APK de release ou publicação foi feito nesta alteração. O APK debug local não substitui nem atualiza o candidato de release acima.

## Release v1.3.71 — Quick Connect e feedback de mídia

### Notas do APK

- A contagem do Quick Connect agora usa prazo monotônico e considera o tempo das consultas de rede; ao atingir o prazo durante uma consulta pendente, a contagem some e a tela continua aguardando a resposta.
- A Home inclui atalho para solicitar filmes, séries e outros tipos de mídia; formulário valida ano e mantém falhas visíveis para nova tentativa.
- A tela de cada título permite reportar problema de reprodução; o relato inclui ID, categoria e descrição, com confirmação apenas após envio bem-sucedido.

### Validação

- [x] Release oficial anterior conferida pela API antes do bump: `app-v1.3.70`, um APK, 7.437.635 bytes, SHA-256 `535A911170AE519229E985C893697954BE336DABCA1E34DEE983265CEA6F91A5`; notas remotas conferidas.
- [x] Teste de regressão cobre consulta de autenticação que segue pendente após o prazo local; timer oculto até a resposta, e timeout neutro após retorno sem autorização.
- [x] Testes Compose de autenticação aprovados em celular, tablet e Android TV: 10/10 por AVD, 0 skips e 0 falhas; AVDs encerrados pelo wrapper.
- [x] Testes JVM de Home e detalhe verificam payload, remoção de espaços e bloqueio de envios duplicados; Quick Connect continua coberto.
- [x] Suíte JVM completa: 1.103 testes, 0 falhas, 0 erros, 0 ignorados; lint debug e build release concluídos.
- [x] Testes Compose de Home e detalhe aprovados em telefone, tablet e TV; casos exclusivos de TV foram ignorados nos outros perfis, sem falhas.
- [x] APK conferido: `versionName=1.3.71`, `versionCode=372`, pacote `org.mulletaflix.android`, assinatura v2 válida e igual à v1.3.70 (certificado SHA-256 `224F9A6BD12690E1114ACE649BBFA778D3E7E99DAE608FF711DDF9131E036273`), artefato `mulletaflix-app-v1.3.71.apk`, 7.437.635 bytes, SHA-256 `AC7D72B541466635616DFB62CA0684C554B4945D1FE82BE41CAFB68BBF62BAFD`.
- [ ] Release oficial APK-only: aguardar confirmação de que as rotas autenticadas de feedback estão implantadas no servidor; servidor ficou fora do escopo desta tarefa.

## Release v1.3.70 — Filtro de livros na busca

### Notas do APK

- A busca universal agora permite filtrar apenas livros; as sugestões também ficam limitadas a livros quando esse filtro está ativo.

### Validação

- [x] Release anterior conferida pela API do GitHub antes do bump: `app-v1.3.69`, um APK, 7.437.635 bytes, SHA-256 `7395E9450D7B080BEAE7A1199858638EA9B535CE3A883A223E8475A26BD597AC`; a tag `app-v1.3.70` não existia.
- [x] Teste unitário verifica envio de `Book` na busca e mantém apenas sugestões `Book` quando há tipos mistos.
- [x] Testes Compose instrumentados aprovados: toque/seleção em tablet; em Android TV, D-pad percorre de “Tudo” até “Livros” na fileira estreita com rolagem e ativa pelo centro. Cenário de foco de TV foi ignorado no perfil tablet; AVDs encerrados após os testes.
- [x] Suíte JVM completa: 1.099 testes, 0 falhas, 0 erros, 0 ignorados; `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] APK conferido: `versionName=1.3.70`, `versionCode=371`, 7.437.635 bytes, SHA-256 `535A911170AE519229E985C893697954BE336DABCA1E34DEE983265CEA6F91A5`; assinatura v2 válida, pacote `org.mulletaflix.android`, certificado igual à v1.3.69.
- [x] Release oficial [app-v1.3.70](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.3.70) publicada somente com o APK; API autenticada confirma asset único `mulletaflix-app-v1.3.70.apk`, 7.437.635 bytes, hash local/remoto `535A911170AE519229E985C893697954BE336DABCA1E34DEE983265CEA6F91A5` e notas idênticas ao item acima.

## Validação de feedback dependente do servidor

- [ ] Validar envio autenticado de `UserFeedback/MediaRequests` e `UserFeedback/PlaybackIssues` em um servidor que tenha esses endpoints implantados; o código do servidor está apenas no checkout local e não foi publicado nesta tarefa.
- [ ] Confirmar no painel do servidor que solicitações e relatos ficam registrados para administração.

## Release v1.3.69 — Retomar reprodução pela Home

### Notas do APK

- Na seção “Continuar Assistindo”, o botão “Retomar” abre o player diretamente na mídia com progresso salvo; tocar no restante do card continua abrindo os detalhes.
- “Retomar” só aparece para mídia não concluída com posição positiva e, quando a duração é conhecida, ainda abaixo do fim.

### Validação

- [x] Release anterior verificada pela API autenticada antes do bump: `app-v1.3.68`, um APK, 7.437.635 bytes, SHA-256 `36A02EA2089B68A37772A64E5394A2B8E013A5F51B9BC24F6FF2104034AF0CFA`; `app-v1.3.69` ainda não existe.
- [x] Suíte JVM completa: 1.098 testes, 0 falhas, 0 erros, 0 ignorados; `:app:lintDebug` passou.
- [x] Home instrumentada aprovada nos AVDs telefone e tablet (ações de toque/alvo mínimo) e Android TV (D-pad do card ao botão e ativação pelo centro); os três emuladores foram encerrados ao final.
- [x] `:app:assembleRelease` concluiu com `BUILD SUCCESSFUL`; APK validado com `versionName=1.3.69`, `versionCode=370`, 7.437.635 bytes, SHA-256 `7395E9450D7B080BEAE7A1199858638EA9B535CE3A883A223E8475A26BD597AC`; assinatura APK v2 válida e certificado igual ao da versão anterior.
- [x] Release oficial [app-v1.3.69](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.3.69) publicada somente com `mulletaflix-app-v1.3.69.apk`; API autenticada confirma asset único, tamanho/hash idênticos ao APK local e notas exatamente iguais aos dois itens acima.
- [x] APK v1.3.69 validado e release oficial APK-only conferida via API; notas remotas correspondem literalmente aos dois itens acima.

---

## Release v1.3.68 — Melhorias de perfil, rede local e controles de reprodução

### Notas do APK

- Preferências de idioma de áudio e legenda são isoladas por usuário e servidor; a escolha de uma reprodução antiga não sobrescreve a conta ativa, e ajustes legados do aparelho migram uma única vez para o primeiro perfil.
- Android TV verifica novas versões periodicamente enquanto o app está em primeiro plano; no celular, mantém a verificação ao voltar ao app. Verificações simultâneas são evitadas.
- A descoberta de servidor na rede local agrupa callbacks de conectividade e descarta buscas antigas antes de abrir sondagens, reduzindo buscas duplicadas.
- O player mantém um mini controle Cast fora da tela de reprodução, com estado do receptor, abrir controles, reproduzir/pausar e encerrar transmissão; PiP usa entrada automática quando compatível e oculta OSD e gestos enquanto ativo.
- Ações da barra superior na TV mantêm foco visual e respondem ao pressionamento central do controle remoto; durante carregamentos continuam alcançáveis sem iniciar pedidos duplicados.

### Validação

- [x] APK oficial anterior verificado antes do bump: `app-v1.3.67`, `versionName=1.3.67`, `versionCode=368`, um APK, 7.421.251 bytes, SHA-256 `CFC4356A359E2E3C92FB949838CBC10DACC1715762358B65892669FB6FD20991`; digest da API do GitHub confere.
- [x] `SettingsRepositoryAccountScopeTest`: isolamento usuário/servidor e migração do legado aprovados em Android TV e tablet (2/2 testes por dispositivo); AVDs encerrados após a execução.
- [x] Suíte JVM completa: 1.097 testes, 0 falhas, 0 erros, 0 ignorados; lint debug (repetição isolada após falha transitória FIR) e `assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Suítes Android instrumentadas dos 14 módulos concluídas em TV e tablet sem falhas; tablet: 188 testes, 20 pulos intencionais de cenários exclusivos de TV/controle remoto; os dois AVDs foram encerrados.
- [x] Suíte instrumentada completa dos 14 módulos no perfil de celular concluída com `BUILD SUCCESSFUL`; cenários exclusivos de TV/controle remoto foram omitidos pelo perfil e o AVD foi encerrado. A condição de TV ausente em um teste remoto da Home foi corrigida e a suíte integral repetida passou.
- [x] APK montado e conferido: `versionName=1.3.68`, `versionCode=369`, 7.437.635 bytes, SHA-256 `36A02EA2089B68A37772A64E5394A2B8E013A5F51B9BC24F6FF2104034AF0CFA`, assinatura v2 válida; certificado confere com o APK anterior.
- [x] Release oficial [app-v1.3.68](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.3.68) publicada somente com o APK; API autenticada confirma asset único `mulletaflix-app-v1.3.68.apk`, 7.437.635 bytes, digest remoto e SHA-256 local idênticos (`36A02EA2089B68A37772A64E5394A2B8E013A5F51B9BC24F6FF2104034AF0CFA`), tag/título corretos e notas idênticas aos cinco itens de melhorias listados acima.

## Release v1.3.67 — Fechamento confiável da resposta de atualização

### Notas oficiais do APK

- A consulta de versões do APK fecha a resposta HTTP também para status não exitosos; respostas de sucesso e erros durante leitura/análise do corpo liberam os recursos de rede.

### Validação

- [x] APK oficial anterior verificado por API e download antes do bump: `app-v1.3.66`, `versionName=1.3.66`, `versionCode=367`, um asset, 7.437.635 bytes, SHA-256 `98A677D690326F8A236D532FDDC3C99E5321042D79B4E40640348894E583904F`.
- [x] `:data:testDebugUnitTest`: 17 testes, 0 falhas; cobre fechamento da resposta em sucesso, HTTP 503 e corpo inválido.
- [x] `testDebugUnitTest`: 1.097 testes, 0 falhas, 0 erros, 0 ignorados.
- [x] `:app:lintDebug` e `:app:assembleRelease`: `BUILD SUCCESSFUL`; APK `dist/release-v1.3.67/mulletaflix-app-v1.3.67.apk`, `versionName=1.3.67`, `versionCode=368`, 7.421.251 bytes, SHA-256 `CFC4356A359E2E3C92FB949838CBC10DACC1715762358B65892669FB6FD20991`; assinatura v2 válida, certificado igual ao APK oficial v1.3.66.
- [x] Release oficial [app-v1.3.67](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.3.67) publicada somente com o APK; API/download confirmam asset único `mulletaflix-app-v1.3.67.apk`, 7.421.251 bytes, SHA-256 local e remoto idênticos (`CFC4356A359E2E3C92FB949838CBC10DACC1715762358B65892669FB6FD20991`), `versionName=1.3.67`, `versionCode=368`, assinatura v2 válida e notas oficiais idênticas às melhorias acima.

## Release v1.3.66 — PiP automático e transição alinhada ao vídeo

### Notas oficiais do APK

- No Android 12+, o sistema pode levar o vídeo automaticamente para PiP ao sair por gesto enquanto a reprodução está ativa e a preferência de PiP está ligada; pausado, desligado ou Android 11 e anteriores não ativam essa entrada automática. O fluxo manual de Home continua como fallback no Android 8–11.
- Os parâmetros de PiP atualizam o retângulo de origem junto com o layout visível do player para alinhar a animação ao vídeo.

### Validação

- [x] Release APK anterior consultada por API e download antes do bump: `app-v1.3.65`, `versionName=1.3.65`, `versionCode=366`, asset único de 7.421.255 bytes, SHA-256 `1C4D7591FC8161DA5205E85C58EE128C98649570584109B31CEC88956282D7FF`.
- [x] `testDebugUnitTest`: 1.094 testes, 0 falhas, 0 erros e 0 ignorados; `:app:lintDebug` concluído com `BUILD SUCCESSFUL`.
- [x] `PlayerPictureInPictureUiTest`: 1/1 aprovado em telefone Android 15, Android TV API 34 e tablet Android 15; AVDs iniciados pelo wrapper foram encerrados.
- [x] `:app:assembleRelease`: `BUILD SUCCESSFUL`; APK `dist/release-v1.3.66/mulletaflix-app-v1.3.66.apk`, `versionName=1.3.66`, `versionCode=367`, 7.437.635 bytes, SHA-256 `98A677D690326F8A236D532FDDC3C99E5321042D79B4E40640348894E583904F`; assinatura v2 válida e certificado igual ao APK oficial v1.3.65.
- [x] Release oficial [app-v1.3.66](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.3.66) publicada somente com o APK; API confirma asset único `mulletaflix-app-v1.3.66.apk`, 7.437.635 bytes, download com hash SHA-256 idêntico ao local (`98A677D690326F8A236D532FDDC3C99E5321042D79B4E40640348894E583904F`), `versionName=1.3.66`, `versionCode=367` e notas idênticas às melhorias acima.

## Release v1.3.65 — Player sem overlays no PiP

### Notas oficiais do APK

- Ao entrar em Picture-in-Picture, controles, avisos e demais overlays do player desaparecem; o vídeo e as legendas continuam visíveis. Gestos e o tratamento de Voltar do player deixam de interferir nesse modo.
- Ao sair do PiP, os controles reaparecem inclusive se a reprodução estiver pausada. A tela acompanha as transições do sistema e oculta os controles antecipadamente nos dispositivos compatíveis.

### Validação

- [x] Release anterior consultada pela API e APK baixado: `app-v1.3.64`, asset único de 7.421.247 bytes, SHA-256 `99DC636348AA0CED9FF8B6BE22C78D639EF082C40DEB012EC96FD0E8D6663EA2`.
- [x] `testDebugUnitTest`: 1.092 testes, 0 falhas, 0 erros e 0 ignorados; `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV (`MulletaflixTvApi34`) e tablet (`MulletaflixTabletApi35`): `:app:connectedDebugAndroidTest`, 14/14 em cada perfil; os AVDs iniciados pelo wrapper foram encerrados.
- [x] APK local `dist/release-v1.3.65/mulletaflix-app-v1.3.65.apk`: `versionName=1.3.65`, `versionCode=366`, 7.421.255 bytes, SHA-256 `1C4D7591FC8161DA5205E85C58EE128C98649570584109B31CEC88956282D7FF`; assinatura v2 e certificado iguais ao APK oficial anterior.
- [x] Release oficial [app-v1.3.65](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.3.65) publicada somente com o APK; API e download confirmam asset único, `versionName=1.3.65`, `versionCode=366`, 7.421.255 bytes, SHA-256 `1C4D7591FC8161DA5205E85C58EE128C98649570584109B31CEC88956282D7FF` e notas específicas iguais às melhorias acima.

## Release v1.3.64 — Recuperação de sessão Cast suspensa

### Notas oficiais do APK

- Uma desconexão Cast temporária agora mantém o mini player e o observador da sessão ativos, mostrando o estado de reconexão em vez de tratar a suspensão como encerramento.
- Os controles de reprodução ficam desativados enquanto o dispositivo Cast está desconectado e voltam ao reconectar; o encerramento/falha real continua limpando o mini player.

### Validação

- [x] Conferido o APK oficial anterior v1.3.63 via API e download: `versionCode=364`, 7.421.251 bytes, SHA-256 `D2AA84993155D2A2D69BBBE361A90B8D62E4EE68CB7509316BE472EF74380318`; release contém somente o APK e suas notas correspondem à versão publicada.
- [x] `:feature:player:testDebugUnitTest :app:compileDebugKotlin` e suíte completa `testDebugUnitTest`: `BUILD SUCCESSFUL`, 1.089 testes, 0 falhas, 0 erros e 0 ignorados.
- [x] `:app:lintDebug`: `BUILD SUCCESSFUL`.
- [x] Android TV (`MulletaflixTvApi34`) e tablet (`MulletaflixTabletApi35`): `:app:connectedDebugAndroidTest`, 14/14 em cada perfil; os AVDs iniciados pelos testes foram encerrados.
- [x] APK local `dist/release-v1.3.64/mulletaflix-app-v1.3.64.apk`: `versionName=1.3.64`, `versionCode=365`, 7.421.247 bytes, SHA-256 `99DC636348AA0CED9FF8B6BE22C78D639EF082C40DEB012EC96FD0E8D6663EA2`; manifesto e assinatura validados, certificado idêntico ao APK oficial anterior.
- [x] Release oficial [app-v1.3.64](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.3.64) publicada somente com o APK; API e download confirmam um asset, `versionName=1.3.64`, `versionCode=365`, 7.421.247 bytes, SHA-256 `99DC636348AA0CED9FF8B6BE22C78D639EF082C40DEB012EC96FD0E8D6663EA2`; notas oficiais formatadas e alinhadas aos dois itens acima.

## Release v1.3.63 — Continuidade da sessão Cast fora do player

### Notas oficiais do APK

- Corrigido o ciclo de vida do mini player Cast ao sair da tela do player: a observação da sessão e os eventos do controle remoto permanecem ativos enquanto a transmissão continuar, mesmo sem o serviço de MediaSession assumir a sessão.
- Ao terminar ou falhar a sessão Cast, o estado do mini player é limpo e os callbacks remotos são removidos.

### Validação

- [x] Conferido o APK oficial anterior v1.3.62 pela API de releases: asset único `mulletaflix-app-v1.3.62.apk`, `versionCode=363`, 7.421.251 bytes, SHA-256 `9FA09C38B5B8B5979BE59DF3AEE581D093B78C5F5B92478E8395724219762001`.
- [x] `testDebugUnitTest`: 1.087 testes, 0 falhas e 0 erros; `:app:lintDebug` e `:app:compileDebugKotlin` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV (`MulletaflixTvApi34`) e tablet (`MulletaflixTabletApi35`): `:app:connectedDebugAndroidTest`, 14/14 em cada perfil; os AVDs iniciados pelos testes foram encerrados.
- [x] APK local `dist/release-v1.3.63/mulletaflix-app-v1.3.63.apk`: `versionCode=364`, 7.421.251 bytes, SHA-256 `D2AA84993155D2A2D69BBBE361A90B8D62E4EE68CB7509316BE472EF74380318`; manifesto e assinatura v2 validados, certificado igual ao APK oficial anterior.
- [x] Release oficial [app-v1.3.63](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.3.63) publicada somente com o APK; API confirma um asset, tamanho/hash iguais ao artefato local e notas alinhadas às correções acima.

## Release v1.3.62 — Mini player global para sessões Cast

### Notas oficiais do APK

- Ao sair do player durante uma transmissão Cast, um mini player permanece nas telas autenticadas do aplicativo, mostrando título, dispositivo e estado da reprodução.
- O mini player permite reabrir os controles do título, pausar/retomar e encerrar a transmissão; desaparece ao encerrar a sessão e não cobre telas de login nem o player em tela cheia.
- Controles têm alvos acessíveis por toque e foco D-pad, com margens ampliadas para Android TV. O estado é atualizado por eventos do player, sem polling periódico.

### Validação

- [x] Conferido o APK oficial anterior v1.3.61: `versionCode=362`, 7.421.251 bytes, SHA-256 `D266739AC8C08B6158F2F27799BF54CD52A2FBB69BFDC569E460726BF8A85659`; tamanho e hash conferidos pela API e por download oficial do GitHub.
- [x] `testDebugUnitTest`: 1.085 testes, 0 falhas, 0 erros, 0 ignorados; `:app:lintDebug` concluído com `BUILD SUCCESSFUL`.
- [x] Android TV e tablet: `:app:connectedDebugAndroidTest`, 14/14 em cada perfil; os dois AVDs foram encerrados pelo wrapper.
- [x] APK v1.3.62: `dist/mulletaflix-app-v1.3.62.apk`, `versionCode=363`, 7.421.251 bytes, SHA-256 `9FA09C38B5B8B5979BE59DF3AEE581D093B78C5F5B92478E8395724219762001`; manifesto e assinatura v2 validados.
- [x] Release oficial `app-v1.3.62` publicada somente com o APK; API confirma asset único, tamanho/hash idênticos ao pacote local e notas que espelham as mudanças acima.

## Release v1.3.61 — Atualização automática na Android TV e estabilidade

### Notas oficiais do APK

- Na Android TV, a verificação de novas versões continua enquanto o aplicativo permanece em primeiro plano, com intervalo de 60 minutos; ao ir para segundo plano, ela pausa. Celulares continuam verificando ao voltar ao primeiro plano, sem polling periódico.
- Consultas simultâneas de atualização são evitadas. A atualização continua sendo oferecida para confirmação; a instalação depende do instalador do Android.
- A descoberta automática de servidor LAN aguarda 350 ms para agrupar notificações rápidas de rede e descarta varreduras substituídas antes de abrir sondas.
- Durante uma sessão Cast, o controle de qualidade local deixa de aparecer, pois não se aplica ao stream remoto.

### Validação

- [x] Conferido APK oficial anterior v1.3.60: `versionCode=361`, 7.421.251 bytes, SHA-256 `8CC9BF7CF733219838972D67EE3AAFA7FE166DCA22626DF014A0E59E6CD637FA`; tamanho e hash coincidem com a API e o download oficial do GitHub.
- [x] `testDebugUnitTest`: 1.082 testes, 0 falhas, 0 erros, 0 ignorados; `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: `:app:connectedDebugAndroidTest`, 14/14 aprovados; wrapper encerrou o AVD ao final.
- [x] APK v1.3.61: `dist/mulletaflix-app-v1.3.61.apk`, `versionCode=362`, 7.421.251 bytes, SHA-256 `D266739AC8C08B6158F2F27799BF54CD52A2FBB69BFDC569E460726BF8A85659`; manifesto validado e certificado igual ao APK oficial anterior.
- [x] Release oficial `app-v1.3.61` publicada somente com o APK; API confirma um único asset, tamanho, SHA-256 idêntico ao artefato local/baixado e notas específicas correspondentes às alterações.

## Release v1.3.60 — Atualização do stack estável do Compose

### Notas oficiais do APK

- Atualizada a Compose BOM de `2026.08.00` para `2026.09.00`, adotando o conjunto estável mais recente indicado pela documentação Android e mantendo as bibliotecas Compose alinhadas entre si.

### Validação

- [x] Conferido APK oficial anterior v1.3.59: `versionCode=360`, 7.421.251 bytes, SHA-256 `A1A96BE6C476F30F985311A9917F6514C6D0DC9D2056658A8EDB1D4499EED9E0`; tamanho e hash coincidem com a API e o download oficial do GitHub.
- [x] `testDebugUnitTest`: 1.080 testes, 0 falhas, 0 erros, 0 ignorados.
- [x] `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL` quando executados separadamente; uma primeira execução combinada falhou em lint instrumentado por ausência transitória de fonte gerada pelo KSP.
- [x] APK v1.3.60: `dist/release-v1.3.60/mulletaflix-app-v1.3.60.apk`, `versionCode=361`, 7.421.251 bytes, SHA-256 `8CC9BF7CF733219838972D67EE3AAFA7FE166DCA22626DF014A0E59E6CD637FA`; manifesto validado e certificado igual ao APK oficial anterior.
- [x] Release oficial `app-v1.3.60` publicada somente com o APK; API confirma asset único, tamanho, SHA-256 correspondente e notas específicas corrigidas/legíveis alinhadas ao artefato.

## Release v1.3.59 — Controle de qualidade consistente durante Cast

### Notas oficiais do APK

- O seletor de qualidade fica oculto durante a transmissão Cast, pois suas restrições atuam somente no player local; se uma sessão Cast iniciar com o menu aberto, ele também deixa de ser exibido.

### Validação

- [x] Conferido APK oficial anterior v1.3.58: `versionCode=359`, 7.421.251 bytes, SHA-256 `AF1A0C5C03F3D7B9017ECF66022FDFD648478652955D214B25CADD15A58950E8`; tamanho e hash coincidem com a API e o download oficial do GitHub.
- [x] `PlayerOptionsTest`: 21 testes, 0 falhas; suíte JVM completa: 1.080 testes, 0 falhas, 0 erros, 0 ignorados.
- [x] `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] APK v1.3.59: `dist/release-v1.3.59/mulletaflix-app-v1.3.59.apk`, `versionCode=360`, 7.421.251 bytes, SHA-256 `A1A96BE6C476F30F985311A9917F6514C6D0DC9D2056658A8EDB1D4499EED9E0`; manifesto e assinatura validados, certificado igual ao APK oficial anterior.
- [x] Release oficial `app-v1.3.59` publicada somente com o APK; API confirma asset único, tamanho, SHA-256 e notas correspondentes ao artefato.

## Release v1.3.58 — Reconexão LAN sem varreduras redundantes

### Notas oficiais do APK

- Descoberta e reconexão LAN agrupam mudanças rápidas de conectividade em uma janela de 350 ms e descartam varreduras substituídas antes de iniciar sondagens UDP, reduzindo trabalho redundante durante alternâncias de rede e retorno ao primeiro plano.

### Validação

- [x] Conferido APK oficial anterior v1.3.57: `versionCode=358`, 7.421.251 bytes, SHA-256 `2A03CD48096A76204B0CBDDB5BE075DF0C5BCDF885DFC6FCAFB9A0E0D133BD74`; hash coincide com a API do GitHub.
- [x] `testDebugUnitTest`: 1.267 testes, 0 falhas, 0 erros e 4 ignorados; `LanServerRecoveryPolicyTest` passou 21/21.
- [x] `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] APK v1.3.58: `dist/release-v1.3.58/mulletaflix-app-v1.3.58.apk`, `versionCode=359`, 7.421.251 bytes, SHA-256 `AF1A0C5C03F3D7B9017ECF66022FDFD648478652955D214B25CADD15A58950E8`; manifesto e assinatura validados, certificado igual ao APK anterior.
- [x] Release oficial `app-v1.3.58` publicada somente com o APK; API confirma asset único, tamanho, SHA-256 e notas correspondentes ao artefato.

## Release v1.3.57 — Melhorias de reprodução, biblioteca e UX para telas grandes

### Notas oficiais do APK

- Legendas embutidas agora podem ser selecionadas durante Cast com o seletor remoto Media3; controles de áudio indisponíveis no receiver padrão não são exibidos. Legendas externas não são oferecidas em Cast/offline.
- Reprodução local passa a aceitar sidecars de legenda externa com URLs resolvidas com segurança; adiciona opção independente para pular aberturas automaticamente, sem pular créditos.
- TV ao vivo atualiza o EPG ao retomar e mantém o estado de agendamento verificável enquanto o servidor propaga o timer.
- Quick Connect diferencia timeout local de expiração confirmada e permite recuperar-se de falhas transitórias.
- Controle remoto exibe progresso quando a sessão informa duração e não perde atualizações após comandos concorrentes.
- Busca e playlists preservam resultados durante paginação/retry, e a biblioteca expõe playlists do usuário.
- Downloads distinguem carregamento inicial de fila realmente vazia; Home separa reproduzir de detalhes e permite recuperar seções que falharam sem descartar o restante.
- Tablet/TV recebem índice alfabético focável para saltos na biblioteca.
- Cache offline durável e comandos SyncPlay mais consistentes preservam downloads e evitam comandos obsoletos.
- A troca de legenda em receiver Cast físico não foi verificada nesta rodada; a seleção foi coberta pelos testes do seletor Media3.

### Validação

- Baseline publicada verificada: v1.3.56, `versionCode=357`, SHA-256 `497CF96D4806C0159FFACCB0BA6FF2C8BAB1FA8D0D3E80060670ECC9AB06392C`.
- [x] `testDebugUnitTest`: 1.077 testes, 0 falhas/erros/skips; `:app:lintDebug` e `:app:assembleRelease` — `BUILD SUCCESSFUL`.
- [x] Android TV: suíte completa, 140 aprovados, 1 ignorado (alvo de toque não aplicável à TV), 0 falhas; após separar pressupostos por dispositivo, foco TV nos módulos Home, biblioteca, EPG e perfil passou 48/48.
- [x] Tablet: 121 testes instrumentados, 108 aprovados, 13 ignorados por serem verificações de foco remoto exclusivas de TV, 0 falhas; AVDs foram encerrados pelos wrappers.
- [x] APK v1.3.57: `dist/release-v1.3.57/mulletaflix-app-v1.3.57.apk`, `versionCode=358`, 7.421.251 bytes, SHA-256 `2A03CD48096A76204B0CBDDB5BE075DF0C5BCDF885DFC6FCAFB9A0E0D133BD74`; `aapt` confirmou versão/ID e `apksigner` confirmou assinatura, com certificado igual ao APK anterior.
- [x] Release oficial `app-v1.3.57` publicada somente com o APK; API confirma asset único, tamanho, SHA-256 e notas alinhadas ao artefato.

## Release v1.3.56 — Atualização confiável dos controles remotos

- [x] Após um comando remoto bem-sucedido, enfileirar uma consulta de atualização caso outra já esteja em andamento; polls periódicos concorrentes continuam coalescidos.
- [x] Teste de regressão reproduziu a atualização perdida antes da correção; `RemotePlaybackViewModelTest` (5/5).
- [x] `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` — `BUILD SUCCESSFUL`.
- [x] APK release: `dist/release-v1.3.56/mulletaflix-app-v1.3.56.apk`, `versionCode=357`, 7.421.251 bytes, SHA-256 `497CF96D4806C0159FFACCB0BA6FF2C8BAB1FA8D0D3E80060670ECC9AB06392C`; manifest validado com `aapt`, assinatura validada com `apksigner` e compatível com o APK v1.3.55 enviado anteriormente.
- [x] Release oficial `app-v1.3.56` publicada só com o APK; API confirma asset, SHA-256 e notas correspondentes a esta correção.
- [ ] Revisar release histórica `app-v1.3.54`: seu asset publicado tem SHA-256 `0AFFDEBDC32FA19A8964C659DD10552309E6550A716C1E713829234000B7B505` (igual ao APK local v1.3.53), enquanto o APK local v1.3.54 tem SHA-256 `9AD33AB2132E8FBF32C0E7F55AB799DC1704B365920A2ED6C461614C6524EC9C`; a release histórica não foi alterada.

## Trabalho APK — Faixas externas durante Cast (incluído em v1.3.57)

- [x] Em Cast/offline, excluir faixas externas das elegíveis para seleção e não anexar sidecars ao `MediaItem`; manter faixas externas na reprodução local online.
- [x] Testes JVM cobrem reprodução local, Cast, offline, stream embutido, tipo incorreto e ausência de preferência.
- [x] Incluído no APK v1.3.57; notas oficiais descrevem a política de legendas externas no Cast/offline e a validação automatizada.

## Trabalho APK — Pulo automático da abertura (incluído em v1.3.57)

- [x] Adicionar preferência independente “Pular abertura automaticamente”, opt-in/desligada por padrão e persistida no DataStore; manter separado o botão manual de pulo.
- [x] Buscar automaticamente apenas durante reprodução ativa e seekable, somente para segmentos/capítulos de abertura detectados; nunca pular créditos; evitar seeks repetidos.
- [x] Cobertura da política e persistência da configuração; Android TV: teste Compose do switch aprovado dentro de 12 testes instrumentados, 0 falhas.
- [x] `testDebugUnitTest`: 1.070 testes, 0 falhas/erros/skips; `:app:lintDebug` e `:app:assembleRelease` — `BUILD SUCCESSFUL`.
- [x] APK local sem bump/publicação: v1.3.55 (`versionCode=356`), 7.421.251 bytes, SHA-256 `1296828B8316E9EE2CBD9F62C090C8B6182DDBE28FC59307930373D4C35EA609`.
- [x] Incluído no APK v1.3.57; notas oficiais descrevem a preferência automática de abertura e explicitam que créditos não são pulados.

## Trabalho APK — Atualização imediata do EPG ao retomar (incluído em v1.3.57)

- [x] Recarregar o guia da TV ao vivo imediatamente quando o aplicativo retoma com o EPG aberto; manter atualização periódica apenas enquanto o guia estiver visível.
- [x] Serializar a leitura do EPG com a atualização de canais, evitando iniciar uma consulta que a recarga de canais cancelaria e repetiria.
- [x] Testes cobrem política de tela aberta/fechada, pausa/retomada (Android TV: `LiveTvRefreshEffectTest`, 3/3) e carga do guia adiada durante atualização de canais, com uma única consulta após estabilização.
- [x] `testDebugUnitTest`: 1.055 testes, 0 falhas/erros/skips; `:app:lintDebug`; `:app:assembleRelease` — todos `BUILD SUCCESSFUL`.
- [x] APK local de validação sem bump/publicação: v1.3.55 (`versionCode=356`), 7.421.251 bytes, SHA-256 `68988FE828EB4D2D2BF33D18E77172FC6DB14430C53E6131913F2EBB557B5361`.
- [x] Incluído no APK v1.3.57; notas oficiais descrevem a atualização do EPG ao retomar.

## Trabalho APK — Timeout honesto no Quick Connect (incluído em v1.3.57)

- [x] Separar término do prazo local da confirmação de expiração do servidor; HTTP 404 continua sendo expiração terminal.
- [x] Preservar retries após falha transitória e autenticação quando a consulta posterior confirma autorização.
- [x] Regressões cobrem 100 falhas transitórias, recuperação em polling posterior e expiração confirmada por HTTP 404.
- [x] Validar `testDebugUnitTest` (1.053 testes, 0 falhas/erros/skips), `:app:lintDebug` e `:app:assembleRelease`; todos concluídos com `BUILD SUCCESSFUL`.
- [x] Compose de autenticação: Android TV 9/9 e tablet 9/9; wrappers encerraram os AVDs.
- [x] APK local sem bump/publicação: v1.3.55 (`versionCode=356`), 7.421.251 bytes, SHA-256 `73E654F01C7E83D9739171C00EA8BB3B38DAF26C9497567C73CAA397EEEFAF7F`.
- [x] Incluído no APK v1.3.57; notas oficiais descrevem a diferença entre timeout local e expiração confirmada e a recuperação de falhas transitórias.

## Trabalho APK — Progresso da reprodução remota (incluído em v1.3.57)

- [x] Mostrar barra de progresso acessível e tempos decorrido/total nas sessões com duração conhecida; limitar posição exibida ao intervalo válido.
- [x] Preservar sessões sem duração (ex.: canais ao vivo), ocultando o progresso e mantendo controles atuais.
- [x] Validar `testDebugUnitTest` (1.050 testes, 0 falhas/erros/skips), `:app:lintDebug` e `:app:assembleRelease`; todos concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 8/8 testes instrumentados aprovados; o wrapper iniciou e encerrou o AVD.
- [x] APK local de validação sem bump/publicação: v1.3.55 (`versionCode=356`), 7.421.251 bytes, SHA-256 `1D2E8AC4B2E5AC7862F8F1719E642CEA68C9D3024BC0FF53C137C9A2D2B55121`.
- [x] Contrato HTTP de reprodução remota exercitado com Retrofit + MockWebServer: listagem/filtros, decodificação da sessão, seek e omissão do parâmetro de seek em Play/Pause (3/3); suíte completa `testDebugUnitTest` (1.069 testes, 0 falhas/erros/skips), `:app:lintDebug` e `:app:assembleDebug` — `BUILD SUCCESSFUL`.
- [x] Não perder atualização imediata após comando remoto durante uma consulta em andamento; polls periódicos concorrentes continuam coalescidos. Regressão `RemotePlaybackViewModelTest` (5/5); suíte `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleDebug` — `BUILD SUCCESSFUL`.
- [x] Incluído no APK v1.3.57; notas oficiais descrevem progresso remoto e atualização após comandos concorrentes.

## Trabalho APK — Retry da paginação da busca (incluído em v1.3.57)

- [x] Corrigir “Tentar” após falha de página para repetir o mesmo offset e anexar resultados sem substituir a primeira página.
- [x] Preservar o retry e a mensagem de falha quando o dispositivo estiver offline; adicionar testes para retry online e offline.
- [x] Validação: `testDebugUnitTest` (1.050 testes, 0 falhas/erros/skips), `:feature:search:connectedDebugAndroidTest` (11/11 na TV), `:app:lintDebug` e `:app:assembleRelease` — todos `BUILD SUCCESSFUL`; AVD encerrado pelo wrapper.
- [x] APK local de validação, sem bump/publicação: v1.3.55 (`versionCode=356`), 7.404.867 bytes, SHA-256 `4C1D58A2BBCD427F94D8B6C86BEB3F929F039C4C16E4D25F360F86FFADAF48AA`.
- [x] Incluído no APK v1.3.57; notas oficiais descrevem paginação/retry de busca e playlists, biblioteca de playlists e estados de loading/erro.

---

## Trabalho APK — Carregamento da fila offline (incluído em v1.3.57)

- [x] Distinguir fila não carregada de fila vazia; expirar snapshot compartilhado após coleta parada para não mostrar dados obsoletos ao retornar.
- [x] Mostrar progresso acessível antes do primeiro snapshot real do Media3; manter CTA “Explorar Catálogo” somente no estado vazio confirmado.
- [x] Cobrir snapshots atrasados na primeira abertura e retomada, política loading/vazio/conteúdo e transições Compose; CTA “Explorar Catálogo” funciona no estado vazio.
- [x] Gates APK: `testDebugUnitTest` (1.048 testes, 0 falhas/erros/skips), `:app:lintDebug`, `:app:assembleRelease`; todos `BUILD SUCCESSFUL`.
- [x] Android TV: 8/8 testes instrumentados aprovados; wrapper encerrou o AVD.
- [x] APK local de validação v1.3.55 (`versionCode=356`), 7.404.867 bytes, SHA-256 `66AACC03941830BE6EA4F78E173E6C2857B183F9C749000540BD42B3305A5943`.
- [x] Incluído no APK v1.3.57; notas oficiais descrevem a distinção entre fila inicial em carregamento e fila vazia confirmada.

---

## Trabalho APK — Confirmação de agendamento na TV ao vivo (incluído em v1.3.57)

- [x] Após agendar gravação, repetir consulta ao timer duas vezes com espera curta e limitada; estado local mantém “Agendado” durante propagação.
- [x] Se servidor não retornar ID, oferecer “Verificar” sem fechar/reabrir EPG; desabilitar Cancelar/Verificar offline e indicar confirmação pendente.
- [x] Invalidar consultas de timer após mudança de sessão, desconexão ou atualização concorrente; cancelar agendamento em trânsito ao trocar usuário; revalidar sessão e conectividade imediatamente antes de cancelar timer.
- [x] Testes cobrem falhas/atraso, teto de consultas, retry manual, respostas obsoletas, corrida de sessão/rede no cancelamento e ações focáveis/indisponíveis offline.
- [x] Gates APK sequenciais: `testDebugUnitTest` (1.046 testes, 0 falhas/erros/skips), `:app:lintDebug`, `:app:assembleRelease`; todos `BUILD SUCCESSFUL`.
- [x] Android TV: 13/13 testes instrumentados aprovados; AVD encerrado pelo wrapper.
- [x] APK local de validação v1.3.55 (`versionCode=356`), 7.404.867 bytes, SHA-256 `A26B12C791A7A86BA0007DDFF49E00E3E8799980DFF9C65F2219B5D2FAFC0CF7`.
- [x] Incluído no APK v1.3.57; notas oficiais descrevem EPG retomado e estado verificável de agendamento.

---

## Trabalho APK — Legendas externas no player (incluído em v1.3.57)

- [x] Preservar `DeliveryUrl` do DTO até o domínio e carregar legenda externa selecionada (SRT, VTT, ASS/SSA, TTML/DFXP) como sidecar Media3.
- [x] Ao alternar faixa, anexar só a legenda externa selecionada; remover sidecar ao voltar para faixa embutida/desativada. IDs exclusivos evitam colisões com faixas do container.
- [x] Resolver URLs relativas e da mesma origem com token atualizado; não enviar token para outra origem, rejeitar URLs externas com credenciais na URL e tratar URLs protocol-relative.
- [x] Ocultar faixas externas no Cast, pois a conversão padrão Media3 não transfere configurações sidecar ao receiver; respeitar indisponibilidade offline.
- [x] Gates APK sequenciais: `testDebugUnitTest` (1.066 testes, 0 falhas/erros/skips), `:app:lintDebug`, `:app:assembleRelease`; todos `BUILD SUCCESSFUL`.
- [x] Android TV: 27 testes instrumentados finalizados, 26 aprovados e 1 ignorado (alvo de toque Cast não aplicável à TV); wrapper encerra o AVD ao terminar.
- [x] APK local de validação v1.3.55 (`versionCode=356`), 7.404.867 bytes, SHA-256 `CE16D647748DD5C972DBA6FD38B841E1DFD6EE8459D0AD176078232C98D92385`.
- [x] `ExternalSubtitlePlaybackIntegrationTest` passou no emulador Android TV: Media3 buscou mídia e SRT por rotas HTTP independentes, decodificou o cue e o `SubtitleView` do `PlayerView` renderizou pixels durante playback.
- [x] Incluído no APK v1.3.57, com URLs seguras e sidecars locais. A seleção de faixa embutida foi coberta no seletor; fluxo Cast completo e receiver físico não foram validados e essa limitação consta nas notas oficiais.

---

## Trabalho APK — Biblioteca de playlists (incluído em v1.3.57)

- [x] Adicionar leitura paginada de itens de playlist via `Playlists/{playlistId}/Items`, sem mudança no servidor.
- [x] Expor “Minhas playlists” em Configurações, com capas, acesso aos detalhes e reprodução direta.
- [x] Cobrir mapeamento/API, validação de parâmetros, paginação, remoção de duplicados e estados vazios/carregamento/erro.
- [x] Android TV: 16/16 testes instrumentados aprovados; emulador encerrado pelo wrapper.
- [x] Gates APK: `testDebugUnitTest` (1.016 testes, 0 falhas/erros/skips), `:app:lintDebug`, `:app:assembleRelease`; `BUILD SUCCESSFUL`.
- [x] Artefato local de validação v1.3.55 (`versionCode=356`), 7.404.867 bytes, SHA-256 `F933134DE84E0ADCDF1B1F6DD95EB4DEE07CE883895D16DF6A6EE94CE80A7B54`.
- [x] Incluído no APK v1.3.57; notas oficiais descrevem a biblioteca de playlists.

## Trabalho APK — Paginação e retry de playlists e busca (incluído em v1.3.57)

- [x] Avançar os offsets de playlists e busca pela quantidade bruta recebida do servidor, não pela lista visível após deduplicação; busca conserva “Carregar mais” após páginas repetidas quando o total ainda indica resultados, permitindo chegar a itens posteriores sem paginação automática em loop.
- [x] Preservar títulos já carregados ao falhar página da playlist, repetir no mesmo offset e mostrar carregamento durante retry.
- [x] Cobrir duplicados, offsets, progresso, falha e retry: 92/92 testes unitários nos módulos `feature:item-detail` e `feature:search`.
- [x] Android TV: 17/17 testes instrumentados aprovados; wrapper encerrou o emulador.
- [x] Gates APK: `testDebugUnitTest` (1.062 testes, 0 falhas/erros/skips), `:app:lintDebug` e `:app:assembleRelease`; todos `BUILD SUCCESSFUL`.
- [x] APK local de validação v1.3.55 (`versionCode=356`), 7.421.251 bytes, SHA-256 `9A26ED1BE5D909D3BC7FB847E6702151EA016AC7E48DE5E813A5BE28E6389F75`.
- [x] Incluído no APK v1.3.57; notas oficiais descrevem paginação e retry de busca/playlists.

## Trabalho APK — Índice alfabético em tablet/TV (incluído em v1.3.57)

- [x] Adicionar trilho de salto por letras presentes na biblioteca, normalizando acentos do português.
- [x] Mostrar o índice somente em tablet/TV, com ordenação Nome ascendente e paginação concluída; saltos incluem cabeçalhos de erro/filtros.
- [x] Controles com alvo mínimo de 48 dp e descrição acessível; testes unitários cobrem agrupamento de letras e offsets.
- [x] Android TV: 16/16 testes instrumentados aprovados, incluindo salto real para letra com cabeçalhos; AVD fechado pelo wrapper.
- [x] Gates: `testDebugUnitTest` (1.019 testes, 0 falhas/erros), `:app:lintDebug` e `:app:assembleRelease` com `BUILD SUCCESSFUL`.
- [x] APK local de validação v1.3.55 (`versionCode=356`), 7.404.867 bytes, SHA-256 `2879055D68161D93D176B0E33883EBFBEBD7C4FE55DD741F0D7AA2D5F52A399B`.
- [x] Incluído no APK v1.3.57; notas oficiais descrevem o índice alfabético para tablet/TV.

## Trabalho APK — Reprodução da Home e atualização remota (incluído em v1.3.57)

- [x] Separar a ação “Reproduzir” do destaque da Home de “Mais informações”; o primeiro abre a rota do player e o segundo mantém a rota de detalhes.
- [x] Adicionar teste Compose das ações distintas; Home Android TV: 13/13 testes instrumentados aprovados.
- [x] Ao falhar a atualização das sessões remotas, limpar a lista desatualizada e manter erro acionável; teste unitário cobre falha após sessão previamente carregada.
- [x] Gates: `testDebugUnitTest` (1.020 testes, 0 falhas/erros), `:app:lintDebug` e `:app:assembleRelease` com `BUILD SUCCESSFUL`.
- [x] APK local de validação v1.3.55 (`versionCode=356`), 7.404.867 bytes, SHA-256 `EF55CB7221B6795B7BF115ED29E8541BE8EB2D7CE2D3ABAF3FE3D96C25774B76`.
- [x] Incluído no APK v1.3.57; notas oficiais descrevem a ação Reproduzir e a recuperação das atualizações remotas.

## Trabalho APK — Erros parciais da Home (incluído em v1.3.57)

- [x] Propagar falhas de Continuar Assistindo, Próximo Episódio e Minha Lista sem descartar as demais seções que carregaram.
- [x] Exibir erro com “Tentar novamente” no local da seção; não mostrar “Nenhum conteúdo” quando existe mídia ou erro de seção.
- [x] Cobrir os erros no caso de uso e ViewModel e a regra de Home realmente vazia com testes unitários.
- [x] Gates após as alterações APK: `testDebugUnitTest` (1.023 testes, 0 falhas/erros), `:app:lintDebug`, `:app:assembleRelease` — `BUILD SUCCESSFUL`; Android TV Home 13/13.
- [x] APK local de validação v1.3.55 (`versionCode=356`), 7.404.867 bytes, SHA-256 `7081D42AFCEC19DCE4EC3E6D80637FCC56FED8958A747EAFD982B854E83DB485`.
- [x] Incluído no APK v1.3.57; notas oficiais descrevem retry por seção da Home sem descartar conteúdo que carregou.

## Skills e metodologias do projeto

- [x] Gauntlet Loop: Builder vs. Evaluator; aceitar qualidade somente com evidências dos comandos reais do projeto.
- [x] Android/Clean Architecture: `android-dev`, `android-cli`, `android_ui_verification`, `android-clean-architecture`, `mobile-design`.
- [x] Revisão e agentes: `cavecrew` + agente especialista Android/QA, quando disponíveis no ambiente.
- [x] Fluxo de raciocínio: `fable-method`, `fable-loop`, `fable-judge`, `everything-claude-code`.
- [x] Economia de contexto/memória: `caveman`, `caveman-compress`, `caveman-learn`.
- [x] UI Android: Jetpack Compose, instrumentação em emuladores celular/tablet/Android TV, foco D-pad e acessibilidade.

---

## Trabalho APK v1.3.55 — Cache offline durável e comandos SyncPlay consistentes (incluído em v1.3.57)

- [x] Um comando SyncPlay novo invalida o comando agendado anterior; o player revalida sala, mídia e geração antes de aplicar.
- [x] Atualização da fila invalida comandos pendentes da faixa anterior.
- [x] Cache de downloads offline movido para diretório privado persistente; cache legado é migrado e pastas coexistentes são mescladas sem sobrescrita.
- [x] Conflitos entre arquivos mantêm as cópias intactas e a pasta antiga ativa; falhas de migração também preservam os downloads.
- [x] Player lê mídia baixada sem gravar streams não baixados no cache persistente.
- [x] Testes cobrem latest-wins, migração, coexistência, conflito, falha de migração e configuração de cache somente leitura.
- [x] Teste instrumentado no Android TV comprova leitura real de segmentos offline sem bytes upstream e stream sem persistência no cache.
- [x] Android TV: 20 testes instrumentados, 19 aprovados, 1 ignorado preexistente de touch target não aplicável à TV, 0 falhas; emulador encerrado automaticamente.
- [x] Gate da rodada anterior: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`; 980 testes, 0 falhas, 0 ignorados.
- [x] APK intermediário da rodada anterior: `dist/interim-v1.3.55/mulletaflix-app-v1.3.55.apk`, 7.372.099 bytes, SHA-256 `FD9ED53B0CEA115F7EA87E160D7CC0EF398164AD3E461DB0F241EF8C99EDAB10`. O pacote v1.3.55 preexistente foi verificado e preservado.
- [x] Gate desta rodada (factory extraída + teste instrumentado real): `testDebugUnitTest` (980 testes, 0 falhas, 0 ignorados), `:app:lintDebug`, `:app:assembleRelease` e suíte Android TV concluídos com `BUILD SUCCESSFUL`.
- [x] Incluído no APK v1.3.57; notas oficiais descrevem cache offline persistente e consistência dos comandos SyncPlay.

---

## Release v1.3.54 — Eventos SyncPlay limitados à sala ativa (APK)

- [x] O APK agora descarta comandos, atualizações da sala e da fila cujo `groupId` não corresponde à sala ativa; eventos de conexão/desconexão também são ignorados sem uma sala ativa.
- [x] Testes unitários cobrem eventos da sala correta, de outra sala e recebidos sem sala ativa.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] APK local: `dist/mulletaflix-app-v1.3.54.apk`, 7.372.095 bytes, SHA-256 `9AD33AB2132E8FBF32C0E7F55AB799DC1704B365920A2ED6C461614C6524EC9C`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.53 — Teste instrumentado para notificações do SyncPlay (APK)

- [x] O efeito de Snackbar do SyncPlay foi isolado em componente testável, mantendo a mensagem persistente nos controles da sala.
- [x] Teste instrumentado confirma no Android TV que eventos repetidos com a mesma mensagem continuam visíveis.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] APK local: `dist/mulletaflix-app-v1.3.53.apk`, 7.372.099 bytes, SHA-256 `0AFFDEBDC32FA19A8964C659DD10552309E6550A716C1E713829234000B7B505`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.52 — Feedback visual de eventos do SyncPlay (APK)

- [x] Eventos de conexão, desconexão, comandos, sala e fila agora geram notificações transitórias visíveis no APK.
- [x] Cada evento possui sequência própria, então eventos consecutivos com a mesma mensagem não são descartados pela UI.
- [x] O estado persistente continua mostrando o último evento nos controles da sala, enquanto a Snackbar fornece confirmação imediata.
- [x] Teste unitário cobre as mensagens de eventos em tempo real.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] APK local: `dist/mulletaflix-app-v1.3.52.apk`, 7.372.099 bytes, SHA-256 `1E01A68D925F1D0DD2C185C592FBE2CDC07D52EAE4E3F0D3295FA6368BEDAD99`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.51 — Estado consistente ao trocar automaticamente para a LAN (APK)

- [x] A descoberta LAN invalida usuários e Quick Connect do endpoint anterior antes de concluir a verificação do novo servidor.
- [x] O carregamento persistido do endpoint público é ignorado quando a descoberta já selecionou um endereço local diferente, evitando mistura de estados.
- [x] Teste unitário cobre a transição público → LAN e confirma que o seletor de usuários e Quick Connect não exibem dados antigos.
- [x] Android TV: 7/7 testes instrumentados de autenticação aprovados, sem skips ou falhas; emulador encerrado automaticamente.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] APK local: `dist/mulletaflix-app-v1.3.51.apk`, 7.369.451 bytes, SHA-256 `A7F9D2B526420D15757ACC62DD31622B67EE9A75A276874011A5601EBCF56461`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.50 — Cobertura do retry de disponibilidade do Quick Connect (APK)

- [x] O teste unitário do `AuthViewModel` cobre falha de `QuickConnect/Enabled`, mensagem acionável, retry e recuperação para disponibilidade confirmada.
- [x] A cobertura instrumentada continua validando os estados de carregamento, erro com retry e disponibilidade autorizada na Android TV.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] APK local: `dist/mulletaflix-app-v1.3.50.apk`, 7.369.451 bytes, SHA-256 `535524D2DAC96442DBE6B7441E88FE69BAA40B51E6ECF47611723BC9DAACAFA9`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.49 — Retry acionável para falha de disponibilidade do Quick Connect (APK)

- [x] Falhas de timeout/rede ao consultar `QuickConnect/Enabled` deixam de prender a tela indefinidamente em “Verificando”.
- [x] A tela exibe a mensagem acionável retornada pela política de conexão e o botão focável `Tentar novamente`.
- [x] O retry reinicia a verificação no servidor atualmente selecionado e invalida o estado anterior.
- [x] Teste instrumentado cobre a mensagem de erro e a ação de retry, além dos estados desconhecido e disponível.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 7/7 testes instrumentados de autenticação aprovados, sem skips ou falhas; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.49.apk`, 7.369.451 bytes, SHA-256 `A7596996CF623877C54A2581AAC5DD694BFA94AEA96CB4CE61E0E19111713E85`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.48 — Quick Connect aguarda a disponibilidade real do servidor (APK)

- [x] O botão de geração não aparece enquanto `QuickConnect/Enabled` ainda está sendo verificado.
- [x] A tela exibe estado explícito `Verificando Quick Connect…`, evitando iniciar a operação antes da resposta do servidor.
- [x] O fluxo desativado continua orientando o usuário para login com usuário e senha.
- [x] Teste instrumentado cobre a ausência do botão durante o estado desconhecido; o teste de início autorizado continua coberto.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 6/6 testes instrumentados de autenticação aprovados, sem skips ou falhas; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.48.apk`, 7.369.451 bytes, SHA-256 `A14D76317B408B4A60269F65B1B1DE4DD7B87B0795DEF086C4BC45193FF5357E`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.47 — Correção de micro-seeks nas atualizações de fila SyncPlay (APK)

- [x] Atualizações de fila com diferença de até 750 ms não forçam seek, reduzindo microtravadas durante a reprodução.
- [x] Drift acima da tolerância continua corrigido para a posição autoritativa do servidor.
- [x] Comandos explícitos `Pause`, `Unpause` e `Seek` continuam usando a posição autoritativa recebida.
- [x] Teste unitário cobre drift pequeno, drift significativo e posição negativa.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 15/15 testes instrumentados aprovados na Biblioteca; SyncPlay reportou 0 testes; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.47.apk`, 7.369.451 bytes, SHA-256 `592D64DFD0C04DC0BD8E5B8CBC036ED5AAB28FCAD2D3E4CAFC61CA939C630E46`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.46 — Estado visual da reconexão SyncPlay no player (APK)

- [x] O player exibe aviso não bloqueante quando o SyncPlay perde a conexão e tenta reconectar.
- [x] O aviso não aparece para mídia offline nem para sessões SyncPlay conectadas, evitando ruído visual.
- [x] O layout usa espaçamento próprio quando o aviso offline também está visível e permanece compatível com TV/D-pad.
- [x] Teste unitário cobre as mensagens dos estados `NONE`, `CONNECTED` e `RECONNECTING`.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 15/15 testes instrumentados aprovados na Biblioteca; SyncPlay reportou 0 testes; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.46.apk`, 7.369.451 bytes, SHA-256 `3657713368B1447CC46854929F2BA9A93913BCC99532841ECCA62E2BEA4BAC30`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.45 — Proteção contra callbacks obsoletos do SyncPlay (APK)

- [x] Mensagens recebidas por um WebSocket substituído durante reconexão ou troca de sala agora são descartadas antes de alcançar o player.
- [x] A mesma validação de identidade foi aplicada aos callbacks de abertura, mensagem, fechamento e falha.
- [x] Teste unitário cobre socket ativo, socket antigo e callback sem conexão ativa.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 15/15 testes instrumentados aprovados na Biblioteca; SyncPlay reportou 0 testes; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.45.apk`, 7.369.451 bytes, SHA-256 `4B2A2AD74A36059423C69E56FA4680ABD5D1D7EFFEC76F2EA1464CEA0FBEA113`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.44 — Posicionamento autoritativo nos comandos SyncPlay (APK)

- [x] `Pause` e `Unpause` agora aplicam `PositionTicks` do servidor antes de alterar o estado do player, evitando divergência após comandos remotos.
- [x] `Stop` continua sem forçar uma posição recebida, preservando o contrato semântico do comando.
- [x] Teste unitário cobre a conversão de posição para `Pause`/`Unpause` e a ausência de seek em `Stop`.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 15/15 testes instrumentados aprovados na Biblioteca; SyncPlay reportou 0 testes; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.44.apk`, 7.369.451 bytes, SHA-256 `71192BE76FB02E4EA3541E4FDDE52DCB2EF8815FBD7AB4EB692368775E354DD6`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.43 — Reconexão resiliente do SyncPlay (APK)

- [x] WebSocket SyncPlay reconecta automaticamente após falha ou fechamento inesperado enquanto a sala continua ativa.
- [x] Backoff limitado de 1, 2, 4 e 8 segundos evita tempestade de conexões durante perda de Wi‑Fi/Internet.
- [x] Saída da sala cancela a reconexão pendente e impede que uma conexão antiga volte a controlar o player.
- [x] Testes unitários cobrem os limites da política de backoff, incluindo tentativas negativas e tentativas prolongadas.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 15/15 testes instrumentados aprovados na Biblioteca; SyncPlay reportou 0 testes; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.43.apk`, 7.369.451 bytes, SHA-256 `CC13834674027F4D3313D649FCAEA986B6244C22AF0CCE0FCECDA3BB265ADED9`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.42 — Aplicação de comandos SyncPlay no player (APK)

- [x] Contrato WebSocket atualizado com `PlaylistItemId`, `ItemId`, `PositionTicks`, `When` e estado da fila conforme o servidor.
- [x] Player aplica automaticamente pausa, retomada, parada e busca recebidas da sala ativa, respeitando a mídia e o grupo atuais.
- [x] Atualizações de fila trocando a mídia atual são aplicadas ao player com posição e estado de reprodução sincronizados.
- [x] Comandos de outra sala, outra mídia ou sem identificação de playlist são descartados para evitar controles indevidos.
- [x] Testes unitários cobrem parser de comandos/atualização de fila e política de filtragem/posição.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 15/15 testes instrumentados aprovados na Biblioteca; SyncPlay reportou 0 testes; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.42.apk`, 7.369.451 bytes, SHA-256 `FBB62AF1042548A8C7176B780970597F2147C43D607208257700EC8B37B3482A`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.41 — SyncPlay em tempo real (APK)

- [x] Cliente WebSocket autenticado no endpoint `/socket`, usando `api_key` e `deviceId` da sessão.
- [x] Parser coberto por testes para `SyncPlayCommand` e `SyncPlayGroupUpdate`; mensagens não relacionadas são ignoradas.
- [x] Ciclo de vida da conexão ligado à entrada/saída da sala SyncPlay, com estado de conexão e último evento visíveis na UI.
- [x] Gate Android executado com `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` (`BUILD SUCCESSFUL`).
- [x] Android TV: 15 testes instrumentados, 15 aprovados, 0 falhas e 0 ignorados; emulador encerrado automaticamente.
- [x] APK local gerado: `dist/mulletaflix-app-v1.3.41.apk`, 7.353.067 bytes, SHA-256 `BB29FA73BBA9D1AEE38D512F148CC087A3165F17CA0570EB680FFD0E5C35D54F`.
- [x] Aplicar comandos remotos automaticamente ao player local (concluído na v1.3.42).

---

## 🏛️ 1. Arquitetura & Infraestrutura (Clean Architecture + Multi-module)

- [x] **Configuração Gradle & Version Catalog (`libs.versions.toml`)**
  - [x] AGP 8.7.3, Kotlin 2.0.21, Compose BOM 2024.12.01, Media3 1.5.0, Hilt 2.53.1, Room 2.6.1, Retrofit 2.11.0, Moshi 1.15.2, Ktor Client
- [x] **Divisão Modular**
  - [x] `:app` (Orquestração, Navigation, Services, Splash, Cast Options)
  - [x] `:core:common` (Result/Resource, Coroutine Dispatchers Hilt Module, FormatUtils, NetworkMonitor)
  - [x] `:core:api` (Retrofit, Ktor, DTOs, Interceptors, WebSocket)
  - [x] `:domain` (Modelos puros, Interfaces de Repositórios, UseCases: `GetNextEpisodeUseCase`, `GetHomeFeedUseCase`, `SearchMediaUseCase`, `ManageDownloadsUseCase`, `GetLiveTvChannelsUseCase`, `GetLibraryItemsUseCase`, `GetItemDetailUseCase`, `ToggleFavoriteUseCase`, `TogglePlayedUseCase`, `ManagePlaylistUseCase`, `ManageSyncPlayUseCase`, `LoginUseCase`, `RegisterUseCase`, `VerifyServerUseCase`, `GetUserProfileUseCase`, `LogoutUseCase`, `SwitchUserUseCase`)
  - [x] `:data` (Room DB, DataStore, Mappers, Implementação dos Repositórios)
  - [x] `:design-system` (Tokens, Cores, Temas, Tipografia Noto Sans, Componentes)
  - [x] `:feature:auth`
  - [x] `:feature:home`
  - [x] `:feature:library`
  - [x] `:feature:item-detail`
  - [x] `:feature:player`
  - [x] `:feature:search`
  - [x] `:feature:downloads`
  - [x] `:feature:live-tv`
  - [x] `:feature:user`
  - [x] `:feature:sync-play`
  - [x] `:feature:settings`

---

## 🔐 2. Autenticação & Sessão (`:feature:auth`)

- [x] **Seleção e Conexão de Servidor**
  - [x] Descoberta local de servidores (SSDP / UDP broadcast)
  - [x] Entrada manual de URL (HTTP/HTTPS com validação de certificados locais/IPs privados)
  - [x] Histórico de servidores conectados e status de latência
- [x] **Autenticação de Usuário**
  - [x] Login por Nome de Usuário e Senha
  - [x] Quick Connect (Código de 6 dígitos para autenticação rápida em smart devices/navegadores)
  - [x] Seleção visual de usuários com avatares em servidores públicos
  - [x] Armazenamento seguro de token (`MediaBrowser Token="..."`) via EncryptedDataStore
  - [x] Renovação e interceptor de autenticação em todas as requisições (`AuthInterceptor`)

---

## 🏠 3. Home & Descoberta (`:feature:home`)

- [x] **Hero Banner Dinâmico**
  - [x] Destaques em carrossel animado com backdrop de alta resolução
  - [x] Ações rápidas: Assistir Agora, Adicionar à Minha Lista, Detalhes
- [x] **Fileiras de Mídia (Horizontal Rows)**
  - [x] Continue Assistindo (com barra de progresso e porcentagem precisa)
  - [x] Próximos Episódios (Next Up para séries em andamento)
  - [x] Minha Lista com favoritos sincronizados ao servidor
  - [x] Adicionados Recentemente (Filmes, Séries, Álbuns)
  - [x] Recomendações personalizadas baseadas no histórico
  - [x] Bibliotecas Principais (Atalhos rápidos para Filmes, Séries, Músicas, Live TV)
- [x] **Atualização e Cache**
  - [x] Pull-to-refresh com feedback tátil
  - [x] Carregamento paralelo não-bloqueante via Coroutines (`HomeViewModel`)

---

## 📚 4. Navegação por Bibliotecas (`:feature:library`)

- [x] **Visualização e Layouts**
  - [x] Alternância entre Grade (Grid) e Lista (List)
  - [x] Paginação infinita fluida com pre-fetching
- [x] **Filtros e Ordenação**
  - [x] Ordenação por Nome, Data de Adição, Ano de Lançamento, Nota da Crítica, Duração
  - [x] Filtros por Gênero, Ano, Classificação Indicativa, Status de Reprodução (Assistido/Não Assistido)
  - [x] Filtro alfabético rápido (A-Z jump bar)

---

## 🎬 5. Detalhes do Item (`:feature:item-detail`)

- [x] **Apresentação Multimídia**
  - [x] Suporte adaptativo para Filmes, Séries, Episódios, Álbuns de Música e Livros
  - [x] Pôster, Logo oficial em alta resolução, Backdrop cinematográfico e Trilha sonora temática (theme song)
- [x] **Metadados Completos**
  - [x] Sinopse com expansão de texto
  - [x] Classificação indicativa, Ano, Duração, Resolução (4K, HDR, Dolby Vision, 1080p), Codecs de áudio
  - [x] Elenco e Equipe técnica com fotos e navegação por ator
  - [x] Itens Similares recomendados
- [x] **Séries & Temporadas**
  - [x] Seletor de temporadas em abas/dropdown
  - [x] Lista de episódios com thumbnails, sinopses e indicador de progresso
- [x] **Ações do Usuário**
  - [x] Marcar como Assistido/Não Assistido
  - [x] Favoritar / Desfavoritar
  - [x] Download para reprodução offline

---

## ▶️ 6. Player Avançado de Vídeo & Áudio (`:feature:player`)

- [x] **Motor de Reprodução (ExoPlayer Media3)**
  - [x] Streaming HLS, DASH e Direct Play com fallback automático para Transcoding
  - [x] Picture-in-Picture (PiP) automático ao sair do app
  - [x] Suporte a rotação automática e bloqueio de orientação da tela
  - [x] Controle de brilho e volume por gestos verticais nas laterais da tela
  - [x] Liberação imediata de decodificadores de hardware e codecs no `onCleared` (sem vazamento de memória)
  - [x] Tratamento de navegação no `BackHandler` retornando à tela anterior sem finalizar a Activity
- [x] **Interface OSD (On-Screen Display)**
  - [x] Controles modernos de play/pause, avançar/retroceder 10s
  - [x] Barra de progresso com visualização de capítulos e thumbnails de busca
  - [x] Botões "Pular Introdução" (Skip Intro) e "Pular Créditos" (Skip Credits)
  - [x] **Próximo Episódio Automático**: Descoberta inteligente do próximo episódio de séries (`GetNextEpisodeUseCase`), contagem regressiva visual de 5s, botão "Assistir Agora" e avanço automático ao fim do episódio
- [x] **Seleção de Faixas & Qualidade**
  - [x] Seleção de faixas de áudio (Dolby Atmos, 5.1, Estéreo, idiomas secundários)
  - [x] Seleção de legendas (embutidas e externas via OpenSubtitles / SRT / VTT)
  - [x] Ajuste de velocidade de reprodução (0.5x até 2.0x)
  - [x] Ajuste manual de taxa de bits / resolução máxima
- [x] **Transmissão Externa**
  - [x] Suporte nativo a Google Cast (Chromecast) integrado
- [x] **Sincronização com o Servidor**
  - [x] Relatórios periódicos de progresso (a cada 5 segundos) para manter o progresso exato no servidor

---

## 🔍 7. Busca Global Instantânea (`:feature:search`)

- [x] **Mecanismo de Busca em Tempo Real**
  - [x] Busca instantânea com debounce de digitação (350ms)
  - [x] Chips de filtro rápido por tipo de mídia (Filmes, Séries, Músicas, Pessoas)
  - [x] Histórico de buscas recentes com remoção individual por item e limpeza total
  - [x] Tratamento robusto de erros com card explicativo e botão "Tentar novamente"
  - [x] Navegação de retorno (`onBack`) com ícone na SearchBar
  - [x] Resultados agrupados por categoria com navegação direta

---

## 💾 8. Downloads & Reprodução Offline (`:feature:downloads`)

- [x] **Gerenciador de Downloads**
  - [x] Integração com `Media3 DownloadService` para downloads estáveis em segundo plano
  - [x] Notificação de progresso persistente com pausa e cancelamento
  - [x] Fila de downloads priorizada com controle de Wi-Fi apenas
- [x] **Experiência do Usuário & Armazenamento Local**
  - [x] Navegação com TopAppBar e botão de voltar (`onBack`)
  - [x] Estado vazio intuitivo com chamada para ação ("Explorar Catálogo")
  - [x] Diálogo de confirmação de exclusão prevenindo perda acidental de mídias baixadas
  - [x] Registro Room para itens baixados e reprodução offline direta no Player

---

## 📺 9. Live TV & Guia de Programação (EPG) (`:feature:live-tv`)

- [x] **Canais Ao Vivo**
  - [x] Lista de canais com logos oficiais e programas atuais
  - [x] Sintonização instantânea de stream de TV
- [x] **Guia Eletrônico de Programação (EPG)**
  - [x] Grade de horários por canal com navegação temporal
  - [x] Detalhes do programa ao vivo e sinopse
  - [x] Ação para agendar gravações (DVR) no servidor
- [x] **Arquitetura & Navegação**
  - [x] TopAppBar com navegação de retorno
  - [x] `GetLiveTvChannelsUseCase` orquestrando canais e gravações com isolamento de camada de apresentação

---

## 👥 10. SyncPlay (Sessões Sincronizadas) (`:feature:sync-play`)

- [x] **Salas de Sincronização**
  - [x] Criação, listagem, entrada e saída via endpoints REST oficiais do MulletaFlix
  - [x] Comandos remotos de Pausar, Retomar e Parar via endpoints REST oficiais
  - [x] Lista de participantes retornada pelo servidor
  - [x] Listener WebSocket para aplicar automaticamente no player local as ações iniciadas por outros participantes
  - [x] Sincronização de Play/Seek/Stop e troca de mídia em tempo real no player Android
  - [x] Player envia transições SyncPlay `Buffering`/`Ready` com `When`, posição em ticks, intenção de reprodução e `PlaylistItemId`; processador serial com fila pendente limitada/coalescida, deduplicação por sessão e validação da sala/mídia/rede atuais. Testes cobrem atraso, estado obsoleto, offline, falha/retry e reconexão na mesma sala. Validado no trabalho pós-v1.3.55; ainda sem nova release.

---

## 🎨 11. Design System, Temas & Visual (`:design-system`)

- [x] **Paleta Oficial MulletaFlix**
  - [x] Cores primárias `#00A4DC`, fundos cinematográficos `#101010`
  - [x] Tipografia oficial Noto Sans
- [x] **Suporte aos 8 Temas do MulletaFlix Web**
  - [x] Padrão Escuro (Dark)
  - [x] Padrão Claro (Light)
  - [x] Tema Netflix (Vermelho vibrante)
  - [x] Tema Purple Haze (Roxo neon)
  - [x] Tema Blue Radiance (Azul ciano moderno)
  - [x] Tema Windows Media Center (WMC)
  - [x] Tema Apple TV (Minimalista translúcido)
  - [x] Dynamic Color (Material You no Android 12+)
- [x] **Componentes Reutilizáveis**
  - [x] `MediaCard` (Modos Portrait, Landscape, Square e Banner com badges 4K/HDR e progresso)
  - [x] `MulletaFlixTopAppBar` e `MulletaFlixNavigationBar`
  - [x] Estados de Loading com Shimmer Effect e Tratamento de Erros amigável

---

## ⚙️ 12. Configurações & Perfil (`:feature:settings` & `:feature:user`)

- [x] **Configurações Gerais**
  - [x] Seletor interativo de temas em tempo real
  - [x] Configuração de qualidade padrão de streaming e downloads
  - [x] Preferências de áudio e legendas padrão
  - [x] Gerenciamento de cache e limpeza de dados
- [x] **Perfil de Usuário**
  - [x] Exibição de foto de perfil e permissões
  - [x] Alternância rápida entre usuários cadastrados no servidor
  - [x] Ação de logout seguro com limpeza de sessão

---

## 📺 13. Suporte a Android TV & Dispositivos de Sala

- [x] **Compatibilidade com Leanback & Smart TVs**
  - [x] Suporte a `android.software.leanback` com `required="false"` (híbrido TV + Mobile)
  - [x] Suporte a dispositivos sem touchscreen (`android.hardware.touchscreen` required="false")
  - [x] Banner oficial 16:9 (`@drawable/ic_tv_banner`) com proporções corretas para o Launcher da Android TV / Google TV
  - [x] Categoria de inicialização `android.intent.category.LEANBACK_LAUNCHER` no manifesto

---

## 📦 14. Otimização de Release, Assinatura & CI/CD

- [x] **Minificação & Otimização ProGuard / R8**
  - [x] Regras personalizadas em `app/proguard-rules.pro` para Moshi, Retrofit, Room, Media3 ExoPlayer, Hilt e Cast SDK
  - [x] Redução do tamanho final do APK de Release para **7.28 MB** com encolhimento agressivo de recursos
- [x] **Assinatura Automatizada**
  - [x] Bloco `signingConfigs` configurado com suporte a variáveis de ambiente (`KEYSTORE_PATH`, `KEYSTORE_PASSWORD`, etc.) e fallback transparente para debug, gerando APKs imediatamente instaláveis via sideload
- [x] **Integração com GitHub Actions CI**
  - [x] Job automatizado `android-build-test` configurado em `.github/workflows/ci.yml` com Java 17 Temurin, cache de dependências Gradle, execução de testes unitários e build de artefatos Debug e Release

---

## 🧪 15. Validação E2E em Dispositivo Real / Emulador (Gauntlet Loop)

- [x] **Autenticação Real com Servidor Remoto**: Login executado com credenciais reais (`Raphael`), token persistido e recuperação de sessão.
- [x] **Carregamento de Catálogo em Tempo Real**: Hero Banner dinâmico, carrosséis de Filmes e Séries alimentados pela API Jellyfin/MulletaFlix.
- [x] **Navegação & Detalhes**: Backdrop, pôster, metadados, classificação, elenco/equipe técnica e títulos similares renderizados.
- [x] **Player Media3 ExoPlayer**: Inicialização do player com controles de transporte, scrubbing, seletores de faixas de áudio, legendas e resolução.
- [x] **Busca Global**: Query com debounce (`"dias"`), filtros categorizados e resultados divididos entre filmes e outras mídias.
- [x] **TV Ao Vivo & EPG**: Listagem de canais remotos e guia de programação.
- [x] **Downloads Offline**: Interface de mídias baixadas e navegação rápida para o catálogo.
- [x] **Configurações & Temas**: Preferências de reprodução, legendas, tema escuro e informações de servidor.
- [x] **SyncPlay**: Lobbies de reprodução em grupo sincronizados.
- [x] **Perfil de Usuário & Limpeza de Cache**: Diagnóstico de latência (33 ms), limpeza de cache em tempo real (17.2 MB -> 0.0 MB) e encerramento de sessão com diálogo de confirmação.

---

## 🩹 16. Correções v1.0.5 (app Android)

Relatado em uso real: a biblioteca de Séries listava temporadas e episódios como cartões soltos
e abrir uma série fechava o aplicativo. Verificado no emulador (API 35) contra o servidor MulletaFlix real.

- [x] **Fechamento do app ao abrir série**: o seletor de temporadas (`PrimaryScrollableTabRow`) era medido
      com zero abas enquanto a consulta de temporadas ainda estava em andamento, lançando
      `IndexOutOfBoundsException: Index 0 out of bounds for length 0` (`TabRow.kt`) e derrubando o processo.
      O seletor agora só é desenhado quando existem temporadas; antes disso aparece indicador de carga e,
      sem temporadas, uma mensagem. Regressão coberta por teste instrumentado (`SeriesSectionTest`).
- [x] **Temporadas separadas da série**: a listagem de biblioteca consultava
      `Users/{userId}/Items?Recursive=true` sem `IncludeItemTypes`, devolvendo séries, temporadas e episódios
      no mesmo nível (270 itens na biblioteca de Séries do servidor, contra 10 séries reais). A consulta agora
      declara os tipos de navegação da biblioteca (`LibraryBrowseTypes`), seguindo a mesma regra do cliente web
      (`src/controllers/list.ts`).
- [x] **Temporada ou episódio abrem dentro da série**: entrar em uma temporada/episódio carrega a série pai,
      seleciona a temporada correspondente e exibe a lista de episódios.
- [x] **Botão Reproduzir em série**: tocava a série (item não reproduzível) e exibia
      "O servidor não conseguiu preparar esta média". Agora reproduz o primeiro episódio carregado e fica
      desabilitado quando não há episódio disponível.
- [x] **Faixas de álbum de música**: `Shows/{albumId}/Episodes` responde 404 para álbuns; as faixas agora são
      obtidas de `Items?ParentId={albumId}&IncludeItemTypes=Audio`.
- [x] **Avatar do usuário criado no servidor**: a imagem (`Users/{userId}/Images/Primary?tag=...`) passou a ser
      exibida no perfil e na troca rápida de usuários, com a letra inicial como fallback.
- [x] **Rótulo de episódio**: episódios sem numeração (extras) exibiam `nullx00`; agora mostram apenas o nome.
- [x] **Paginação da biblioteca**: `loadMore` usa a quantidade de itens já carregados como cursor, em vez de
      assumir que cada página veio cheia.

Pendências registradas nesta correção:

- [x] Avatar do usuário na barra superior da Home: o `HomeViewModel` carrega o perfil em paralelo ao feed,
      e a barra usa a imagem autenticada com fallback acessível.
- [x] Avatares na tela de login para usuários públicos: o endpoint de imagem pode exigir token; a tela agora
      tenta a imagem autenticada e exibe a inicial do usuário como fallback acessível quando a imagem não está
      disponível (`UserAvatarFallbackTest`).
- [x] Diagnóstico de conexão nas configurações: a ação "Testar conexão" consulta o servidor configurado e
      exibe estado, latência e versão, com cobertura para sucesso e falha no `SettingsViewModelTest`.
- [x] Proteção contra downloads duplicados: títulos já enfileirados, em andamento ou concluídos não são
      reenviados ao `DownloadManager`; itens com falha ou em remoção continuam podendo ser solicitados
      novamente (`DownloadRequestPolicyTest`).
- [x] Histórico de buscas persistente e isolado por usuário: os 10 termos mais recentes são salvos no
      DataStore local e continuam disponíveis após reabrir o aplicativo, sem misturar contas.
- [x] Biblioteca "TV ao Vivo" abre diretamente a tela dedicada de TV ao Vivo, evitando `Items?ParentId=`;
      o servidor não enumera os canais por esse endpoint e a tela dedicada usa `LiveTv/Channels`.

---

## 🖼️ 17. Capas das mídias iguais ao acesso web (v1.0.6)

- [x] **Capas das temporadas na série**: o seletor de temporadas era apenas texto. Agora cada temporada é um
      card com a capa vinda do servidor (`Items/{seasonId}/Images/Primary?tag=...`, com fallback para a capa
      da série via `SeriesPrimaryImageTag`), igual ao cliente web. Coberto por teste instrumentado
      (`SeriesSectionTest`, incluindo o estado "selecionada").
- [x] **Destaque da temporada selecionada**: contorno e nome na cor de acento (`secondary`). O destaque
      anterior usava `colorScheme.primary`, que no tema escuro padrão é o preto da marca
      (`DarkColorScheme.primary = MulletaFlixBlack`) e ficava invisível sobre superfícies escuras.
- [x] **Conferência das capas**: grades de Filmes/Séries, cartões de biblioteca da Home e lista de episódios
      carregam a capa de todo item que possui imagem no servidor — conferido item a item contra a API
      (`ImageTags`). Itens sem nenhuma imagem cadastrada (ex.: `BoJack Horseman`, `Series`) exibem o mesmo
      espaço reservado do cliente web.

Pendências relacionadas:

- [x] Revisar o tema escuro padrão: o preto permanece como `primary` da marca, enquanto ações, ícones e
      seleções usam `secondary` vermelho para manter contraste e acessibilidade.
- [x] Badges de episódios não assistidos e faixa de anos (ex.: "2022 - Presente") que o cliente web mostra
      nos cards: exigem mapear `RecursiveUnplayedItemCount` / `PremiereDate`+`EndDate` no DTO.

---

## 🛡️ 18. Resiliência de reprodução (v1.0.7)

- [x] **Reconexão automática do player**: falhas transitórias de rede são repetidas quando a conectividade
      volta, preservando a posição atual; mídias offline e falhas permanentes não entram nesse ciclo.
- [x] **Estado visível de conectividade**: o player exibe um aviso acessível enquanto está sem rede e tenta
      recuperar o stream automaticamente.
- [x] **Política coberta por testes**: cenários de restauração de rede, reprodução offline e erro permanente.

---

## 🎨 19. Ícone do aplicativo (v1.0.7)

- [x] Launcher usa o logo oficial com transparência e a silhueta externa original.
- [x] Fundo do adaptive icon é transparente; o APK não desenha disco ou círculo ao redor do logo.

---

## 📡 20. Identidade do servidor na descoberta LAN (v1.0.8)

- [x] A verificação do endpoint persiste o `serverId` retornado pelo servidor.
- [x] Servidores salvos mantêm a identidade para que a recuperação LAN não selecione outro servidor da rede.
- [x] Teste de autenticação cobre a persistência do identificador verificado.
## 21. Minha Lista dedicada (v1.0.10)

- [x] Criar rota dedicada `main/favorites` para favoritos completos do usuário.
- [x] Carregar favoritos usando `IsFavorite`, ordenação alfabética e paginação do servidor.
- [x] Adicionar estados de carregamento, vazio, erro recuperável e atualização manual.
- [x] Adicionar atalho com ícone de favorito na barra superior da Home.
- [x] Cobrir carregamento, filtro, paginação e falha de página no `FavoritesViewModelTest`.

## 22. Descoberta LAN segura em sessões legadas (v1.0.11)

- [x] Manter a conexão automática quando somente um servidor responde na LAN.
- [x] Evitar selecionar silenciosamente o primeiro servidor quando múltiplos servidores respondem sem `serverId` persistido.
- [x] Cobrir seleção única e ambiguidade de múltiplos servidores em `LanServerRecoveryPolicyTest`.

## 23. Atualizador Android somente com releases estáveis (v1.0.12)

- [x] Ignorar releases GitHub marcadas como `draft` ou `prerelease`.
- [x] Evitar oferecer APKs de teste pelo atualizador integrado.
- [x] Cobrir o filtro no `AppUpdateRepositoryTest`.

## 24. Regressão do cadastro na tela de login (v1.0.13)

- [x] Garantir no teste instrumentado que o fluxo de login exibe a ação `Cadastrar`.
- [x] Manter a verificação do branding, URL padrão e ação `Entrar` no mesmo smoke test.

## 25. Versão do cliente no cabeçalho de autenticação (v1.0.14)

- [x] Centralizar a versão do APK no Version Catalog do Gradle.
- [x] Remover a versão fixa `12.0.2` do cabeçalho `MediaBrowser`.
- [x] Cobrir os cabeçalhos anônimo e autenticado em `AuthHeaderTest`.

## 26. Preferências manuais de idioma no player (v1.0.15)
- [x] Persistir o idioma técnico da faixa quando o usuário troca o áudio.
- [x] Persistir o idioma técnico da legenda quando o usuário troca a legenda.
- [x] Persistir a opção de legendas desativadas para as próximas reproduções.
- [x] Manter o rótulo visual separado do idioma usado pela seleção automática.

## 27. Qualidade persistente e plataforma atualizada (v1.0.16)
- [x] Persistir a qualidade escolhida manualmente no player para as próximas reproduções.
- [x] Atualizar Media3 para a versão estável mais recente disponível no ciclo atual.
- [x] Atualizar Core KTX e DataStore para as versões estáveis atuais.
- [x] Preparar o APK para Android 16 (targetSdk 36) mantendo minSdk 24.

## 28. Progresso de reprodução completo (v1.0.17)

- [x] Enviar os índices atuais de áudio e legenda também nos relatórios periódicos de progresso.
- [x] Evitar que o servidor perca a preferência de faixa durante uma sessão longa de reprodução.

## 29. Notas de atualização legíveis (v1.0.18)

- [x] Renderizar títulos, listas e negrito das notas de release no APK.
- [x] Tornar o bloco de novidades rolável em diálogos pequenos.
- [x] Cobrir o parser e a formatação de negrito com testes unitários.

## 30. Deep links de mídia (v1.0.19)

- [x] Abrir links `mulletaflix://details?id=...` diretamente no detalhe da mídia.
- [x] Aceitar links oficiais `http(s)://mulletaflix.duckdns.org:8096/web/#/details?id=...`.
- [x] Preservar o destino durante a seleção do servidor e concluir a navegação após o login.
- [x] Cobrir parsing, validação de host e rejeição de links externos com testes unitários.

## 31. Deep links com o APK em execução (v1.0.20)

- [x] Entregar novos links à `MainActivity` existente com `singleTop` e `onNewIntent`.
- [x] Navegar para outro detalhe sem reiniciar o processo nem duplicar a atividade.
- [x] Não ultrapassar autenticação quando o link chega antes do login.
- [x] Cobrir a política de navegação com testes unitários e validar a entrega real no AVD.

## 32. Controles de mídia externos (v1.0.21)

- [x] Encaminhar eventos `MEDIA_BUTTON` para o `MediaSessionService` via receiver oficial do Media3.
- [x] Remover o receiver inerte que recebia comandos Bluetooth/fone sem despachá-los para a sessão.
- [x] Validar a resolução do receiver no APK instalado no AVD.

## 33. Metadados de reprodução externos (v1.0.22)

- [x] Exibir título contextual de série/temporada/episódio nos controles Media3.
- [x] Propagar descrição e capa autenticada para a sessão de mídia e destinos Cast.
- [x] Manter título correto também na reprodução offline.
- [x] Cobrir a formatação de filmes e episódios com testes unitários.

## 34. Metadados preservados no fallback de transcodificação (v1.0.23)

- [x] Manter título, descrição e capa quando o player troca do Direct Play para transcodificação após uma falha.
- [x] Limpar o estado de metadados ao alternar entre mídia remota e download offline.

## 35. Foco de áudio e desconexão de fones (v1.0.24)

- [x] Solicitar foco de áudio automaticamente para reprodução de mídia.
- [x] Pausar o player quando fones ou dispositivos Bluetooth forem desconectados.
- [x] Aplicar a mesma política ao player de fallback do serviço em segundo plano.

## 36. Retomada de downloads offline (v1.0.25)

- [x] Persistir a posição local de mídias baixadas entre sessões do aplicativo.
- [x] Retomar a reprodução offline a partir da última posição salva.
- [x] Remover a posição persistida quando a mídia chega ao fim.
- [x] Evitar que a chave local exponha URL ou token de acesso.

## 37. Player widescreen responsivo (v1.0.26)

- [x] Entrar no player em orientação paisagem sensorizada para mídias widescreen.
- [x] Restaurar a orientação anterior ao sair do player, sem alterar a navegação do restante do aplicativo.
- [x] Cobrir a política de orientação para entradas retrato, indefinida e paisagem.

## 38. Retomada remota resiliente (v1.0.27)

- [x] Persistir localmente a última posição remota por usuário e mídia enquanto o servidor sincroniza.
- [x] Priorizar o progresso oficial do servidor quando ele estiver disponível.
- [x] Usar o fallback local somente quando o progresso do servidor estiver ausente ou zerado.
- [x] Remover a posição local ao concluir a mídia e manter chaves sem URL, token ou identificador em texto.
- [x] Cobrir prioridade, fallback, isolamento por usuário/mídia e valores inválidos com testes unitários.

## 39. Velocidade de reprodução persistente (v1.0.28)

- [x] Persistir a velocidade escolhida no OSD para as próximas reproduções.
- [x] Normalizar valores finitos entre 0,5x e 2x, com fallback seguro para 1x.
- [x] Cobrir limites, valores inválidos e o valor padrão com testes unitários.

## 40. Backoff de reconexão do player (v1.0.29)

- [x] Repetir falhas transitórias de rede até três vezes, sem repetir erros permanentes.
- [x] Aplicar backoff limitado de 750 ms, 1,5 s e 3 s preservando a posição atual.
- [x] Cobrir limite de tentativas e atrasos com testes unitários.

## 41. MIME explícito para reprodução e Cast (v1.0.30)

- [x] Informar o MIME type conhecido no `MediaItem` para HLS, MP4, WebM e Matroska.
- [x] Permitir que Media3 faça sniffing quando o container não for reconhecido.
- [x] Cobrir URL HLS, container informado e fallback de formatos desconhecidos.

## 42. Cards de mídia acessíveis (v1.0.31)

- [x] Agrupar imagem, badges e título do `MediaCard` em um único alvo semântico.
- [x] Anunciar o card como botão com a ação "Abrir <título>".
- [x] Informar explicitamente quando o conteúdo estiver ao vivo.
- [x] Cobrir o anúncio e a ação de toque com teste instrumentado no AVD.

## 43. Preferência de idioma por rótulo regional (v1.0.32)

- [x] Normalizar rótulos humanizados como `Português (Brasil)` e `English (US)`.
- [x] Preservar a compatibilidade com códigos ISO e nomes simples.
- [x] Cobrir áudio/legenda com nomes regionais no teste unitário do player.

## 44. Menu de faixas selecionável e acessível (v1.0.33)

- [x] Transformar cada linha de áudio/legenda em uma opção de rádio semântica.
- [x] Tornar a linha inteira acionável e remover o clique duplicado do `RadioButton`.
- [x] Cobrir a seleção de faixa com teste instrumentado no AVD.

## 45. Temporizador de suspensão do player (v1.0.34)

- [x] Oferecer pausas automáticas de 15, 30, 45, 60 e 90 minutos.
- [x] Exibir a contagem regressiva no conteúdo do menu e no estado semântico do ícone.
- [x] Cancelar o temporizador com segurança ao trocar de mídia ou sair do player.
- [x] Cobrir limites e formatação do temporizador com testes unitários.

## 46. Busca acessível de 10 segundos no player (v1.0.35)

- [x] Expor botões explícitos para voltar e avançar 10 segundos.
- [x] Limitar o alvo de busca ao intervalo válido da mídia.
- [x] Manter os gestos existentes e cobrir os novos limites com teste unitário.

## 47. Visibilidade de senha no cadastro (v1.0.36)

- [x] Permitir revelar/ocultar a senha do cadastro sem alterar o valor digitado.
- [x] Permitir revelar/ocultar a confirmação de forma independente.
- [x] Cobrir os dois controles de visibilidade com teste instrumentado.

## 48. Estado de pausa no progresso remoto (v1.0.37)

- [x] Reportar ao servidor quando a mídia estiver pausada durante a sincronização periódica.
- [x] Manter o estado ativo quando o player estiver reproduzindo.
- [x] Cobrir os dois estados com teste unitário.

## 49. Atualizador Android resiliente a releases do servidor (v1.0.38)

- [x] Consultar até 100 releases do GitHub para não perder releases do APK atrás das releases do servidor.
- [x] Manter o filtro exclusivo de tags `app-*`, sem oferecer pacotes do servidor no APK.
- [x] Cobrir a separação entre canais Android e servidor com teste unitário.

## 50. Recuperação automática do Home após conexão (v1.0.39)

- [x] Detectar a transição de offline para online sem repetir atualizações em estados estáveis.
- [x] Recarregar o feed do Home automaticamente quando a rede voltar.
- [x] Cobrir a transição com teste unitário.

## 51. Ação Cast acessível no player (v1.0.40)

- [x] Agrupar ícone e rótulo de transmissão em um único alvo semântico.
- [x] Informar que a ação transmite para um dispositivo compatível.
- [x] Cobrir a descrição unificada com teste instrumentado.

## 52. Proporção do player persistente (v1.0.41)

- [x] Persistir a proporção escolhida no player entre mídias e sessões.
- [x] Restaurar a preferência com fallback seguro para Ajustar (Original).
- [x] Cobrir valores válidos, antigos e inválidos com teste unitário.

## 53. Busca de downloads offline (v1.0.42)

- [x] Filtrar downloads localmente por título sem alterar a fila original.
- [x] Exibir estado vazio específico para buscas sem resultado.
- [x] Cobrir busca sem distinção de maiúsculas/minúsculas e consulta vazia.

## 54. Menus do player roláveis e sem clique duplicado (v1.0.46)

- [x] Permitir rolagem vertical nos menus de qualidade, velocidade, temporizador e proporção.
- [x] Usar uma única ação semântica de rádio por opção, incluindo o alvo de toque da linha inteira.
- [x] Cobrir uma lista longa de qualidades no teste instrumentado do player.

## 55. Barra de ações do player responsiva (v1.0.47)

- [x] Permitir rolagem horizontal das ações superiores em telas estreitas e fontes ampliadas.
- [x] Manter o título e o botão de voltar fora da área rolável.
- [x] Cobrir a navegação horizontal da faixa de ações no teste instrumentado.

## 56. Cadastro rolável em telas pequenas (v1.0.48)

- [x] Limitar e permitir rolagem vertical do conteúdo do diálogo de cadastro.
- [x] Manter mensagens de erro extensas alcançáveis com o teclado e fontes ampliadas.
- [x] Cobrir a rolagem do diálogo com teste instrumentado.

## 57. Dados técnicos do player roláveis (v1.0.49)

- [x] Limitar o diálogo de informações técnicas para não ultrapassar telas pequenas.
- [x] Permitir rolagem vertical para codecs, resolução, bitrate e método de reprodução.
- [x] Cobrir informações extensas com teste instrumentado.

## 58. Compartilhamento de diagnóstico do player (v1.0.50)

- [x] Formatar método, codecs, resolução e bitrate em texto estável.
- [x] Permitir copiar os dados técnicos para a área de transferência.
- [x] Cobrir a ação de copiar no teste instrumentado e o formato no teste unitário.

## 59. Confirmação de diagnóstico copiado (v1.0.51)

- [x] Confirmar visualmente a cópia dos dados técnicos com o estado “Copiado”.
- [x] Evitar que a ação pareça não executada em telas com feedback transitório.
- [x] Cobrir a confirmação com teste instrumentado.

## 60. Compartilhamento nativo de diagnóstico (v1.0.52)

- [x] Expor os dados técnicos pela folha nativa de compartilhamento do Android.
- [x] Usar texto simples compatível com mensageiros, e-mail e ferramentas de suporte.
- [x] Cobrir a ação de compartilhamento no teste instrumentado.

## 61. Diagnóstico identificado por mídia (v1.0.53)

- [x] Incluir o título da mídia no texto copiado e compartilhado.
- [x] Preservar o formato técnico para suporte e reprodução do problema.
- [x] Cobrir a identificação no teste unitário e no diálogo instrumentado.

## 62. Integridade do atualizador Android (próxima release)

- [x] Ler o digest SHA-256 publicado pelo GitHub para o asset APK.
- [x] Validar o APK baixado antes de disponibilizá-lo para instalação.
- [x] Rejeitar digest inválido ou divergente e manter compatibilidade com releases antigas sem digest.
- [x] Cobrir digest correto, incorreto e malformado com teste unitário.
- [x] Ignorar releases com campo de digest presente, mas malformado, antes do download.

## 63. Capas nos downloads offline (próxima release)

- [x] Persistir a referência da capa junto ao item da fila de downloads.
- [x] Exibir a capa autenticada na lista de downloads quando o servidor estiver acessível.
- [x] Manter o fallback por ícone e título para entradas antigas ou sem imagem.
- [x] Cobrir a propagação da referência de capa no caso de uso e no detalhe da mídia.

## 64. Capas verticais completas para filmes e séries (próxima release)

- [x] Renderizar filmes e séries em cards de pôster vertical `2:3` nas grades e favoritos.
- [x] Preservar a capa inteira com `ContentScale.Fit` nos cards de pôster.
- [x] Aplicar a mesma proporção vertical aos resultados de busca.

## 65. Regra única para apresentação de capas (próxima release)

- [x] Centralizar no domínio a classificação de mídias que usam arte de pôster.
- [x] Reutilizar a regra em biblioteca, favoritos e busca.
- [x] Cobrir filmes, séries, episódios e TV ao vivo com testes unitários.

## 66. Carrossel adaptativo da Home (próxima release)

- [x] Exibir filmes e séries retomados com cards verticais no “Continuar Assistindo”.
- [x] Manter episódios e canais em cards paisagem nos carrosséis apropriados.
- [x] Cobrir a política de formato da Home com testes unitários.

## 67. Identificação de episódios nos cards (v1.0.60)

- [x] Exibir temporada e episódio em cards de episódios quando a numeração estiver disponível.
- [x] Manter o metadado opcional para não alterar cards de filmes, séries e canais.
- [x] Cobrir numeração completa e incompleta com testes unitários.

## 68. Metadados de episódios consistentes (v1.0.61)

- [x] Centralizar a regra de temporada/episódio no domínio.
- [x] Reutilizar o metadado em Home, busca, biblioteca, favoritos e detalhe.
- [x] Cobrir temporadas, episódios e numeração incompleta com testes unitários.

## 69. Marca com contraste MULLETA/FLIX (v1.0.62)

- [x] Exibir “MULLETA” em vermelho e “FLIX” em branco no cabeçalho da Home.
- [x] Preservar um único texto semântico “MULLETAFLIX” para acessibilidade e testes.
- [x] Manter contraste adequado sobre o fundo preto.

## 70. Progresso seguro nos cards (v1.0.63)

- [x] Limitar o progresso recebido do servidor ao intervalo aceito pelo Compose.
- [x] Tratar valores `NaN` e infinitos como progresso inexistente.
- [x] Cobrir valores negativos, normais, excedentes e não finitos com testes unitários.

## 71. Progresso unificado em toda a UI (v1.0.64)

- [x] Centralizar a conversão percentual do servidor em uma fração segura no domínio.
- [x] Aplicar a mesma normalização nos cards da Home, biblioteca, favoritos e episódios.
- [x] Cobrir valores ausentes, negativos, excedentes e não finitos com testes unitários.

## 72. Faixas de áudio e legendas sem opções inválidas (v1.0.65)

- [x] Representar ausência de faixa de áudio com índice `-1`, sem selecionar uma posição inexistente.
- [x] Desabilitar os controles de áudio e legendas quando o servidor não retornar faixas.
- [x] Reutilizar a cobertura existente de listas vazias na política de seleção de faixas.

## 73. Política de versionamento semântico

- [x] Manter `1.0.x` até `1.0.99`.
- [x] Usar `1.1.0` após `1.0.99`, sem criar `1.0.100`.
- [x] Continuar o incremento semântico (`1.1.1`…`1.1.99`, depois `1.2.0`).

## 74. Atualizador alinhado ao versionamento (v1.0.66)

- [x] Ordenar corretamente a transição `1.0.99` para `1.1.0`.
- [x] Ignorar releases Android inválidas com patch `100` ou superior.
- [x] Cobrir a transição e a filtragem com testes do parser de releases.

## 75. Download de atualização com origem confiável (v1.0.67)

- [x] Aceitar downloads somente do endpoint oficial de assets APK do GitHub.
- [x] Rejeitar HTTP, hosts externos e repositórios diferentes antes do download.
- [x] Sanitizar o nome local do APK e cobrir URLs confiáveis e maliciosas com testes.

## 76. Limpeza segura do updater (v1.0.68)

- [x] Fechar sempre a resposta HTTP após o download ou falha de rede.
- [x] Remover APKs parciais em erro HTTP, resposta vazia, hash inválido ou cancelamento.
- [x] Preservar o cancelamento da coroutine e cobrir a limpeza idempotente com teste unitário.

## 77. Feedback e nova tentativa do updater (v1.0.69)

- [x] Exibir no diálogo o motivo retornado quando o download da atualização falhar.
- [x] Manter o diálogo aberto após falha, sem exigir reinicialização do aplicativo.
- [x] Oferecer uma nova tentativa explícita e limpar o erro ao iniciar o novo download.
- [x] Preservar cancelamento de coroutine sem convertê-lo em erro visual.

## 78. Instalação de atualização com feedback (v1.0.70)

- [x] Não fechar o diálogo antes de confirmar que o instalador Android foi aberto.
- [x] Informar bloqueios de permissão ou falhas ao iniciar o instalador.
- [x] Permitir nova tentativa após uma instalação que não pôde ser iniciada.

## 79. Estabilidade da recuperação LAN (v1.0.71)

- [x] Comparar URLs descobertas ignorando espaços e barras finais equivalentes.
- [x] Evitar alternância repetida entre o mesmo endpoint LAN durante a recuperação.
- [x] Cobrir aliases de endpoint com teste unitário de regressão.

## 80. Atualizador consistente nas Configurações (v1.0.72)

- [x] Aplicar na tela de Configurações o mesmo feedback de instalação usado na Home.
- [x] Manter o diálogo aberto quando o instalador Android não puder ser iniciado.
- [x] Exibir a falha e oferecer “Tentar novamente” sem reiniciar o aplicativo.

## 81. Permissão de notificações no player (v1.0.73)

- [x] Detectar a permissão runtime de notificações em Android 13 ou superior.
- [x] Exibir aviso não bloqueante para ativar controles de mídia e notificações de downloads.
- [x] Permitir dispensar o aviso sem interromper a reprodução.
- [x] Cobrir a política por versão do Android e estado da permissão com testes unitários.

## 82. Lembrança da preferência de notificações (v1.0.74)

- [x] Persistir a dispensa do aviso de notificações no armazenamento local do APK.
- [x] Não reapresentar o aviso após “Agora não” ou recusa da permissão.
- [x] Manter o aviso disponível para usuários que ainda não decidiram.
- [x] Cobrir a política de apresentação para permissão ausente e aviso dispensado.

## 83. Descoberta LAN resiliente (v1.0.75)

- [x] Repetir sondagens UDP durante a janela de descoberta sem aumentar o timeout total.
- [x] Manter a descoberta responsiva com intervalos curtos de recepção.
- [x] Cobrir a agenda de tentativas e intervalos inválidos com testes unitários.

## 84. Pausa persistente da fila offline (v1.0.76)

- [x] Persistir o estado de pausa da fila de downloads no armazenamento local.
- [x] Restaurar a pausa ao recriar o processo do aplicativo.
- [x] Expor o estado persistido por `Flow` para manter a tela sincronizada.
- [x] Cobrir pausa, retomada e falha da operação nos testes do módulo.

## 85. Atualização por gesto na biblioteca (v1.0.77)

- [x] Adicionar pull-to-refresh à tela de biblioteca.
- [x] Separar o estado de atualização da paginação para não exibir feedback incorreto.
- [x] Manter o botão de atualização manual e o retry de erro compatíveis com o gesto.

## 86. Atualização por gesto em Minha Lista (v1.0.78)

- [x] Conectar o estado `isRefreshing` existente ao indicador visual da tela.
- [x] Adicionar pull-to-refresh aos favoritos sem interferir na paginação.
- [x] Preservar o botão manual, retry e estados vazio/erro.

## 87. EPG rolável no Live TV (v1.0.79)

- [x] Renderizar a programação em uma lista virtualizada dentro do diálogo.
- [x] Limitar a altura do EPG e permitir rolagem vertical para muitos programas.
- [x] Preservar o agendamento individual e o estado “Agendado”.

## 88. Histórico de busca rolável (v1.0.80)

- [x] Renderizar o histórico em `LazyColumn` para suportar vários itens.
- [x] Preservar seleção, remoção individual e limpeza completa.
- [x] Exibir estado vazio orientando o usuário sem conteúdo oculto.

## 89. Busca sem resultados obsoletos (v1.0.81)

- [x] Cancelar a consulta anterior ao iniciar uma nova busca, troca de filtro ou nova tentativa.
- [x] Identificar cada requisição com uma geração para bloquear respostas atrasadas.
- [x] Cobrir a corrida entre consultas com teste unitário controlado.

## 90. Atualização por gesto na busca (v1.0.82)

- [x] Adicionar pull-to-refresh aos resultados sem sair da consulta atual.
- [x] Manter os resultados visíveis enquanto a atualização está em andamento.
- [x] Cobrir o estado de atualização e a substituição dos resultados após o refresh.

## 91. Rolagem estável na Home (v1.0.83)

- [x] Adicionar chaves estáveis aos cards de mídia nas fileiras horizontais.
- [x] Adicionar chaves estáveis aos cards de bibliotecas.
- [x] Evitar que atualizações do servidor troquem a identidade visual dos itens e desloquem a rolagem.

## 92. Rolagem estável na biblioteca (v1.0.84)

- [x] Adicionar chaves estáveis aos itens da grade/lista da biblioteca.
- [x] Preservar a identidade visual durante paginação, filtros e atualização.
- [x] Alinhar o comportamento da biblioteca com Home, Favoritos e Live TV.

## 93. Preferência persistente de visualização da biblioteca (v1.0.85)

- [x] Persistir a escolha entre grade e lista no DataStore local.
- [x] Restaurar a preferência ao abrir qualquer biblioteca.
- [x] Cobrir restauração e gravação da preferência com teste unitário.

## 94. Ordenação persistente da biblioteca (v1.0.86)

- [x] Persistir a ordenação escolhida no DataStore local.
- [x] Restaurar a ordenação ao abrir a biblioteca.
- [x] Cobrir restauração e gravação da ordenação com teste unitário.

## 95. Filtros persistentes da biblioteca (v1.0.87)

- [x] Persistir Favoritos, Assistidos e Não assistidos no DataStore local.
- [x] Restaurar os filtros ao reabrir a biblioteca.
- [x] Cobrir restauração, gravação e limpeza dos filtros com teste unitário.

## 96. Ordem determinística dos filtros (v1.0.88)

- [x] Normalizar filtros restaurados para a ordem visual estável.
- [x] Ignorar valores desconhecidos persistidos.
- [x] Cobrir a normalização com teste unitário.

## 97. Formato de capa por tipo na biblioteca (v1.0.89)

- [x] Manter filmes e séries com capa vertical completa.
- [x] Exibir episódios e canais com arte horizontal completa também na lista.
- [x] Cobrir o mapeamento de formato com testes de apresentação.

## 98. Wordmark oficial nas telas do aplicativo (v1.0.90)

- [x] Centralizar a composição `MULLETA` vermelho + `FLIX` branco no design system.
- [x] Aplicar o wordmark na tela de login e na Home.
- [x] Cobrir as cores e os limites dos dois trechos com teste unitário.

## 99. Preservação integral das artes nos cartões (v1.0.91)

- [x] Preservar pôsteres e artes horizontais completas no componente compartilhado `MediaCard`.
- [x] Manter o preenchimento intencional em cartões quadrados e banners.
- [x] Cobrir as políticas de escala com testes unitários.

## 100. Wordmark oficial no fluxo inicial (v1.0.92)

- [x] Aplicar o wordmark oficial na seleção do servidor.
- [x] Manter o mesmo branding na seleção, login e Home.
- [x] Atualizar o smoke test instrumentado para validar `MULLETAFLIX`.

## 101. Conectividade LAN sem internet externa (v1.0.93)

- [x] Não exigir `NET_CAPABILITY_VALIDATED` para considerar uma rede local utilizável.
- [x] Preservar a recuperação automática do servidor quando o Wi-Fi não possui internet externa.
- [x] Cobrir a política de conectividade local com testes unitários.

## 102. Latência real no perfil (v1.0.94)

- [x] Remover o valor de latência fictício exibido antes da verificação do servidor.
- [x] Exibir `Conectado` enquanto ainda não houver uma medição real.
- [x] Cobrir latência válida, ausente e inválida com testes unitários.

## 103. Imagens absolutas autenticadas (v1.0.95)

- [x] Anexar o token somente às imagens absolutas do mesmo servidor selecionado.
- [x] Preservar imagens externas/CDN sem expor o token da sessão.
- [x] Evitar duplicação de `api_key` quando a URL já estiver autenticada.
- [x] Cobrir os cenários de servidor local, CDN externo e token existente.

## 104. Preferência de idioma do áudio (v1.0.96)

- [x] Adicionar a seleção persistente de Português, Inglês ou idioma original nas configurações.
- [x] Conectar a preferência ao mesmo fluxo usado pelo player para escolher a faixa de áudio.
- [x] Restaurar a escolha ao reabrir as configurações.
- [x] Cobrir restauração e gravação da preferência com teste unitário.

## 105. Qualidades avançadas na configuração (v1.0.97)

- [x] Expor 4K e 1440p na preferência de qualidade padrão.
- [x] Manter as restrições de qualidade alinhadas ao player Media3.
- [x] Normalizar valores persistidos e substituir preferências inválidas por Automático.
- [x] Cobrir as opções e a normalização com testes unitários.

## 106. Preferências de qualidade canônicas no player (v1.0.98)

- [x] Normalizar aliases persistidos como `2160p`, `1440`, `FULL HD`, `HD` e `SD`.
- [x] Aplicar rótulos canônicos antes de criar as restrições de vídeo do Media3.
- [x] Evitar divergência entre a preferência restaurada e a opção selecionada na interface do player.
- [x] Cobrir aliases válidos e valores inválidos com testes unitários.

## 107. Qualidade disponível refletida no player (v1.0.99)

- [x] Evitar que uma preferência salva sem faixa correspondente fique sem seleção visual.
- [x] Exibir Automático quando a mídia atual não possui a resolução preferida.
- [x] Preservar o limite máximo configurado sem inventar opções de qualidade inexistentes.
- [x] Cobrir a seleção efetiva com testes unitários.

## 108. Mensagens de erro de reprodução acionáveis (v1.1.0)

- [x] Traduzir falhas de rede, decodificação, arquivo ausente e DRM para mensagens orientadas ao usuário.
- [x] Preservar o retry automático para falhas transitórias de rede.
- [x] Usar uma mensagem segura quando o Media3 não fornecer detalhes úteis.
- [x] Cobrir os grupos de erro e os fallbacks com testes unitários.

## 109. Proporção global da imagem (v1.1.1)

- [x] Expor Ajustar, Preencher/Zoom e Esticar nas configurações.
- [x] Persistir a proporção selecionada no DataStore existente.
- [x] Restaurar e normalizar valores antigos ou inválidos com segurança.
- [x] Cobrir restauração e persistência no teste do SettingsViewModel.

## 110. Feedback de limpeza de cache (v1.1.2)

- [x] Informar visualmente quando o cache de imagens terminar de ser removido.
- [x] Manter a limpeza restrita ao cache local do aplicativo.
- [x] Cobrir a mensagem de conclusão no teste do SettingsViewModel.

## 111. Confirmação para limpeza total (v1.1.3)

- [x] Exigir confirmação explícita antes de remover dados locais e encerrar a sessão.
- [x] Informar claramente o impacto e a irreversibilidade da ação.
- [x] Manter Cancelar como saída segura sem alterar preferências ou sessão.

## 112. Temporizador de suspensão consistente no player (v1.1.4)

- [x] Marcar no diálogo a duração atualmente selecionada.
- [x] Limpar o temporizador visual ao trocar de mídia ou cancelar a contagem.
- [x] Manter o estado da contagem regressiva e da duração sincronizados.
- [x] Cobrir a seleção da duração com teste unitário.

## 113. Pausa ao fim da mídia (v1.1.5)

- [x] Oferecer a opção de pausar ao fim do filme ou episódio atual.
- [x] Impedir o avanço automático para o próximo episódio quando essa opção estiver ativa.
- [x] Exibir o modo ativo no acessível do botão do temporizador.
- [x] Cobrir o rótulo do novo modo com teste unitário.

## 114. Capa completa no detalhe do título (v1.1.6)

- [x] Exibir a capa vertical completa de filmes e séries no detalhe.
- [x] Preservar a imagem inteira com `ContentScale.Fit`, sem recorte da arte.
- [x] Manter ações, título e metadados acessíveis em telas estreitas com rolagem horizontal.

## 115. Rolagem de metadados no detalhe (v1.1.7)

- [x] Permitir rolagem horizontal dos badges de ano, qualidade, HDR e áudio.
- [x] Evitar que metadados sejam cortados em telas estreitas.
- [x] Cobrir a ação de rolagem com teste Compose.

## 116. Cor configurável das legendas (v1.1.8)

- [x] Persistir a preferência de cor Branco, Amarelo ou Ciano.
- [x] Aplicar a cor selecionada no SubtitleView do Media3.
- [x] Manter contorno preto para legibilidade em cenas claras e escuras.
- [x] Cobrir normalização e mapeamento de cores com testes unitários.

## 117. Grade adaptativa da biblioteca (v1.1.9)

- [x] Adaptar a quantidade de colunas à largura real do dispositivo.
- [x] Preservar capas verticais inteiras em telas pequenas, tablets e orientação horizontal.
- [x] Oferecer densidades Confortável e Compacta nas configurações.
- [x] Persistir e normalizar a preferência da grade.
- [x] Cobrir a política de densidade com testes unitários.

## 118. Seletor de faixas multimídia informativo (v1.1.10)

- [x] Exibir codec e configuração de canais nas faixas de áudio.
- [x] Identificar faixa padrão e legenda forçada no seletor do player.
- [x] Preservar a seleção efetiva e os índices enviados ao servidor.
- [x] Cobrir a formatação dos rótulos com testes unitários.

## 119. Busca por voz no Android (v1.1.11)

- [x] Adicionar microfone opcional à busca, solicitando permissão somente ao tocar no botão.
- [x] Preencher e executar a busca com o primeiro resultado reconhecido.
- [x] Tratar indisponibilidade, permissão negada e falhas do reconhecimento com mensagens acionáveis.
- [x] Liberar o reconhecedor ao sair da tela.
- [x] Cobrir normalização do resultado e mensagens de erro com testes unitários.

## 120. Capas completas nos Downloads Offline (v1.1.12)

- [x] Preservar a capa inteira de filmes e séries na lista de downloads.
- [x] Evitar recorte da arte vertical no card compacto offline.
- [x] Cobrir a política de escala com teste unitário.

## 121. Paginação protegida na biblioteca (v1.1.13)

- [x] Fechar a janela de corrida entre o sentinel do Scroll e o início da requisição.
- [x] Impedir páginas duplicadas durante recomposição ou rolagem rápida.
- [x] Cobrir o bloqueio de nova página enquanto uma requisição está em andamento ou após o fim.

## 122. Recuperação de sessão durante paginação (v1.1.14)

- [x] Encerrar o carregamento quando a sessão expira durante a rolagem.
- [x] Exibir uma mensagem acionável em vez de deixar o Scroll em estado infinito.
- [x] Cobrir o caminho de sessão ausente com teste de regressão.

## 123. Repetir falhas da fila offline (v1.1.15)

- [x] Adicionar ação única para repetir todos os downloads que falharam.
- [x] Preservar a ordem e os metadados originais de cada item reenfileirado.
- [x] Exibir a quantidade de falhas no botão e cobrir a seleção com teste unitário.

## 124. Verificação visual da recuperação offline (v1.1.16)

- [x] Cobrir o botão de repetição em teste Compose instrumentado.
- [x] Confirmar que a ação fica oculta quando não há falhas.
- [x] Confirmar que o toque dispara exatamente o callback de recuperação.

## 125. Reenvio determinístico da fila offline (v1.1.17)

- [x] Repetir falhas a partir do snapshot renderizado, sem depender de coleta tardia do Flow.
- [x] Preservar a ordem dos IDs reenfileirados.
- [x] Cobrir a operação completa do ViewModel com teste unitário.

## 126. Agendamento protegido do EPG (v1.1.18)

- [x] Impedir gravações duplicadas causadas por toques rápidos no mesmo programa.
- [x] Cancelar requisições anteriores de atualização e guia quando uma nova é iniciada.
- [x] Cobrir o bloqueio de agendamento duplicado com teste unitário.

## 127. Agendamentos concorrentes do EPG (v1.1.19)

- [x] Permitir agendar programas diferentes simultaneamente sem cancelar requisições independentes.
- [x] Manter o bloqueio apenas para toques repetidos no mesmo programa.
- [x] Cobrir a preservação de dois agendamentos concorrentes com teste unitário.

## 128. Limpeza segura de downloads concluídos (v1.1.20)

- [x] Adicionar ação para remover todos os downloads concluídos em uma única operação.
- [x] Exigir confirmação explícita antes da limpeza em lote.
- [x] Preservar downloads em andamento, enfileirados e com falha.
- [x] Cobrir a seleção de concluídos e o encaminhamento ao repositório.

## 129. Descoberta LAN sem respostas obsoletas (v1.1.21)

- [x] Cancelar uma busca LAN anterior quando uma nova busca começa.
- [x] Impedir que uma resposta atrasada substitua o resultado da busca mais recente.
- [x] Cobrir a corrida entre buscas com teste unitário.

## 130. Consultas de busca normalizadas (v1.1.22)

- [x] Aparar espaços nas buscas manuais antes de atualizar estado e histórico.
- [x] Não criar histórico nem requisição para consultas vazias.
- [x] Cobrir normalização e rejeição de entradas somente com espaços.

## 131. Confirmação do histórico de buscas (v1.1.23)

- [x] Exigir confirmação antes de apagar todo o histórico do usuário.
- [x] Manter Cancelar como saída segura sem modificar o histórico.
- [x] Cobrir o diálogo e a ação de confirmação em teste Compose.

## 132. Limpeza de downloads com falha (v1.1.24)

- [x] Expor limpeza exclusiva de downloads falhos, sem afetar ativos ou concluídos.
- [x] Exigir confirmação antes de remover falhas da fila offline.
- [x] Cobrir a política de filtragem e delegação no ViewModel.

## 133. Regressão instrumentada da limpeza de falhas (v1.1.25)

- [x] Atualizar o teste Compose de `OfflineSummary` para os callbacks de limpeza.
- [x] Validar que “Limpar falhas” aparece somente quando há falhas.
- [x] Executar a suíte instrumentada do módulo no emulador API 35.

## 134. Proteção contra respostas obsoletas da biblioteca (v1.1.26)

- [x] Associar cada carregamento de biblioteca a uma geração de requisição.
- [x] Ignorar sucesso ou falha de respostas que já não pertencem à tela atual.
- [x] Cobrir resposta atrasada que ignora cancelamento com teste unitário.

## 135. Confirmação para limpar cache de imagens (v1.1.27)

- [x] Exigir confirmação antes de remover o cache visual local.
- [x] Manter Cancelar como saída segura sem chamar a limpeza.
- [x] Cobrir confirmação e cancelamento em teste Compose instrumentado.

## 136. Limpeza de cache fora da thread principal (v1.1.28)

- [x] Executar remoção e cálculo de diretórios de cache no dispatcher de I/O.
- [x] Reutilizar o qualifier `@IoDispatcher` nas ViewModels de Settings e Perfil.
- [x] Manter testes determinísticos com dispatcher controlado e preservar o cache de downloads offline.

## 137. Proteção contra consultas duplicadas de atualização (v1.1.29)

- [x] Bloquear uma nova verificação enquanto a consulta atual ou a instalação estiver em andamento.
- [x] Desabilitar a ação de atualização na tela durante esses estados.
- [x] Cobrir dois acionamentos consecutivos com teste unitário, garantindo uma única requisição.

## 138. Verificação de atualização resiliente (v1.1.30)

- [x] Exigir a versão instalada explicitamente, sem fallback silencioso para `1.0.0`.
- [x] Normalizar espaços da versão antes de consultar o canal de releases do APK.
- [x] Informar indisponibilidade ou versão ausente sem iniciar uma requisição inválida.
- [x] Garantir que exceções inesperadas do repositório liberem o estado de verificação.
- [x] Cobrir normalização, entrada vazia e exceção de rede com testes unitários.

## 139. Ordenação padrão da biblioteca nas configurações (v1.1.31)

- [x] Expor a ordenação persistida da biblioteca na tela de Configurações.
- [x] Oferecer nome, data de adição, data de lançamento, duração e avaliação.
- [x] Reutilizar os valores de ordenação compatíveis com a API do servidor.
- [x] Restaurar a preferência salva e cobrir sua persistência com teste unitário.

## 140. Proteção contra episódios obsoletos ao trocar de temporada (v1.1.32)

- [x] Cancelar o carregamento anterior de episódios quando uma nova temporada é selecionada.
- [x] Ignorar respostas atrasadas de temporadas que já não estão selecionadas.
- [x] Cobrir a corrida entre temporadas com teste unitário usando resposta suspensa.

## 141. Proteção contra detalhes obsoletos ao trocar de título (v1.1.33)

- [x] Cancelar o carregamento anterior ao abrir outro filme, série ou episódio.
- [x] Limpar os dados dependentes do título anterior enquanto o novo detalhe carrega.
- [x] Ignorar respostas atrasadas de detalhes, capas, extras e faixas do título anterior.
- [x] Cobrir a troca rápida de títulos com teste unitário concorrente.

## 142. Proteção contra downloads duplicados por toque repetido (v1.1.34)

- [x] Bloquear nova preparação enquanto o servidor ainda resolve a fonte do download.
- [x] Mostrar progresso no botão de download durante a preparação.
- [x] Ignorar atualizações de uma preparação antiga quando o título muda.
- [x] Cobrir dois toques consecutivos com teste unitário concorrente.

## 143. Proteção contra respostas obsoletas da Home (v1.1.35)

- [x] Associar cada carregamento ou atualização da Home a uma geração monotônica.
- [x] Ignorar respostas antigas mesmo quando a requisição não respeita cancelamento.
- [x] Preservar o conteúdo mais recente durante atualizações concorrentes.
- [x] Cobrir a corrida entre uma resposta antiga e uma atualização nova com teste unitário.

## 144. Mutação protegida de favoritos e status assistido (v1.1.36)

- [x] Bloquear toques repetidos enquanto o servidor atualiza favorito ou status assistido.
- [x] Mostrar progresso visual nas ações de detalhe durante a mutação.
- [x] Cancelar e invalidar mutações antigas ao abrir outro título.
- [x] Cobrir a proteção contra duas solicitações de favorito com teste unitário concorrente.

## 145. Paginação protegida de Minha Lista (v1.1.37)

- [x] Bloquear duas solicitações da mesma página causadas por toques rápidos.
- [x] Associar cada carga e refresh a uma geração monotônica.
- [x] Ignorar respostas atrasadas de refresh que já não representam a lista atual.
- [x] Cobrir paginação duplicada e refresh obsoleto com testes unitários concorrentes.

## 146. Guia da TV ao vivo protegido contra respostas obsoletas (v1.1.38)

- [x] Associar cada atualização de canais e guia a uma geração monotônica.
- [x] Invalidar o guia anterior quando uma nova lista de canais é carregada.
- [x] Ignorar respostas atrasadas do guia mesmo quando o transporte não respeita cancelamento.
- [x] Cobrir duas solicitações concorrentes do guia com teste unitário.

## 147. Retry offline sem IDs duplicados (v1.1.39)

- [x] Deduplicar entradas com o mesmo ID antes de reenfileirar downloads com falha.
- [x] Preservar a primeira ocorrência e a ordem apresentada na fila.
- [x] Cobrir entradas duplicadas com teste unitário.

## 148. Histórico de busca isolado por sessão (v1.1.40)

- [x] Associar cada observador de histórico a uma geração monotônica.
- [x] Confirmar também o usuário atual antes de atualizar a UI.
- [x] Ignorar emissões atrasadas de uma sessão anterior.
- [x] Cobrir troca de usuário com emissão tardia em teste concorrente.

## 149. Recuperação do player protegida por geração (v1.1.41)

- [x] Associar cada carga remota ou offline a uma geração de reprodução.
- [x] Impedir que erros de uma carga antiga apareçam na mídia atual.
- [x] Invalidar retries atrasados ao recarregar o mesmo título ou trocar de mídia.
- [x] Cobrir geração obsoleta e título divergente com teste unitário.

## 150. Relatórios de reprodução protegidos contra sessões obsoletas (v1.1.42)

- [x] Capturar item, sessão, fonte e posição antes de iniciar relatórios assíncronos.
- [x] Ignorar progresso e parada atrasados quando a mídia ou a sessão atuais mudarem.
- [x] Evitar que uma troca rápida de mídia envie o progresso para o título errado.
- [x] Cobrir geração, sessão e fonte divergentes com teste unitário.

## 151. Relatório de seek protegido contra sessão obsoleta (v1.1.43)

- [x] Aplicar a mesma captura de geração, item, sessão e fonte ao seek manual.
- [x] Impedir que um seek antigo atualize o título aberto depois da troca de mídia.
- [x] Revalidar o APK v1.1.42 antes de publicar a correção residual.

## 152. Descoberta LAN vinculada à identidade do servidor (v1.1.44)

- [x] Priorizar na LAN o servidor cujo `serverId` coincide com um servidor salvo.
- [x] Manter o primeiro anúncio como fallback para a primeira configuração.
- [x] Passar os servidores salvos à seleção automática durante a inicialização.
- [x] Cobrir servidor correto, servidor desconhecido e primeiro uso com testes unitários.

## 153. Snapshot consistente de faixas no relatório periódico (v1.1.45)

- [x] Capturar áudio, legenda, posição e estado no mesmo evento periódico.
- [x] Evitar misturar uma faixa escolhida depois da captura com a posição anterior.
- [x] Cobrir a imutabilidade dos dados do snapshot com teste unitário.

## 154. Usuários disponíveis sincronizados com o endpoint conectado (v1.1.46)

- [x] Recarregar a lista de usuários após verificar uma URL LAN ou pública.
- [x] Evitar que o login mostre usuários do servidor anterior após trocar de endpoint.
- [x] Cobrir conexão sem URL salva e atualização do seletor de usuários.

## 155. Seletor de usuários resiliente à troca e falha de endpoint (v1.1.47)

- [x] Limpar usuários do servidor anterior assim que uma nova URL válida começa a ser verificada.
- [x] Ignorar respostas tardias de carregamentos cancelados ou de endpoints que já não estão ativos.
- [x] Permitir nova tentativa quando o carregamento de usuários falhar temporariamente.
- [x] Cobrir retry no mesmo endpoint com teste unitário.

## 156. Seleção de servidor protegida contra verificações atrasadas (v1.1.48)

- [x] Cancelar a verificação anterior quando o usuário escolhe outro servidor.
- [x] Associar cada verificação à geração da última seleção.
- [x] Ignorar sucesso ou falha atrasados de uma conexão que já não é a escolhida.
- [x] Cobrir resposta antiga não cooperativa com teste concorrente.

## 157. Recuperação LAN protegida contra varredura obsoleta (v1.1.49)

- [x] Associar cada descoberta LAN a uma geração monotônica.
- [x] Ignorar o resultado de uma varredura cancelada após uma nova tentativa.
- [x] Invalidar varreduras pendentes ao parar a recuperação de rede.
- [x] Cobrir geração antiga e recuperação parada com teste unitário.

## 158. Home não bloqueada por perfil lento (v1.1.50)

- [x] Renderizar o catálogo assim que o feed de mídia terminar.
- [x] Carregar avatar e perfil em paralelo sem bloquear a Home.
- [x] Atualizar o perfil depois sem substituir um carregamento mais recente.
- [x] Cobrir resposta de perfil suspensa com teste unitário.

## 159. Busca isolada por sessão de usuário (v1.1.51)

- [x] Invalidar buscas pendentes quando a conta ativa mudar.
- [x] Limpar resultados da sessão anterior ao trocar de usuário.
- [x] Ignorar respostas tardias de uma busca iniciada por outra conta.
- [x] Cobrir transporte não cooperativo com teste unitário concorrente.

## 160. Biblioteca isolada por sessão de usuário (v1.1.52)

- [x] Invalidar carregamentos e paginações quando a conta ativa mudar.
- [x] Limpar itens da biblioteca anterior antes de exibir a nova sessão.
- [x] Validar geração, usuário e biblioteca antes de aplicar respostas.
- [x] Cobrir resposta tardia de biblioteca com teste unitário concorrente.

## 161. Minha Lista isolada por sessão de usuário (v1.1.53)

- [x] Observar troca de conta enquanto Minha Lista está aberta.
- [x] Limpar favoritos da conta anterior e recarregar a nova sessão.
- [x] Validar usuário e geração antes de aplicar respostas de favoritos.
- [x] Cobrir resposta tardia não cooperativa com teste unitário concorrente.

## 162. Home sincronizada com a sessão ativa (v1.1.54)

- [x] Observar troca de usuário enquanto a Home permanece aberta.
- [x] Limpar conteúdo e perfil da conta anterior antes do novo carregamento.
- [x] Recarregar automaticamente o feed da nova sessão.
- [x] Ignorar resposta tardia não cooperativa da conta anterior.

## 163. TV ao vivo isolada por sessão de usuário (v1.1.55)

- [x] Invalidar canais, gravações e guia ao trocar de conta.
- [x] Recarregar a programação da nova sessão automaticamente.
- [x] Impedir callbacks atrasados de agendamentos de alterar a conta atual.
- [x] Cobrir resposta tardia do guia com teste unitário concorrente.

## 164. Estado da transmissão Cast refletido no player (v1.1.56)

- [x] Observar a disponibilidade da sessão Cast pelo `CastPlayer` do Media3.
- [x] Atualizar o rótulo e a descrição de acessibilidade para “Transmitindo” durante o espelhamento.
- [x] Limpar o observador ao destruir o `PlayerViewModel`.
- [x] Cobrir os rótulos de conexão e desconexão com teste unitário.

## 165. SyncPlay isolado por sessão de usuário (v1.1.57)

- [x] Invalidar salas e sala ativa ao trocar de conta.
- [x] Recarregar as salas da nova sessão automaticamente.
- [x] Impedir callbacks atrasados de criação, entrada, saída e listagem de alterar a conta atual.
- [x] Cobrir resposta tardia de salas com teste unitário concorrente.

## 166. Detalhes de mídia isolados por sessão de usuário (v1.1.58)

- [x] Invalidar detalhes, temporadas, favoritos, estado assistido e downloads ao trocar de conta.
- [x] Ignorar respostas atrasadas de detalhes, temporadas, playlists e preparação de download da conta anterior.
- [x] Recomeçar a tela de detalhes limpa para a nova sessão ativa.
- [x] Cobrir resposta tardia de detalhes com teste unitário concorrente.

## 167. Perfil do usuário isolado por sessão (v1.1.59)

- [x] Invalidar carregamentos de perfil, usuários disponíveis e verificação do servidor ao trocar de conta.
- [x] Limpar o perfil anterior enquanto a nova sessão é carregada.
- [x] Recarregar automaticamente o perfil após a troca de usuário.
- [x] Cobrir resposta tardia do perfil com teste unitário concorrente.

## 168. Player invalidado ao trocar de sessão (v1.1.60)

- [x] Observar a sessão ativa enquanto o player está aberto.
- [x] Interromper a reprodução e limpar a mídia da conta anterior ao trocar de usuário.
- [x] Invalidar carregamentos, retries e relatórios de progresso atrasados.
- [x] Cobrir a invalidação por geração de sessão com teste unitário.

## 169. Downloads offline isolados por conta (v1.1.61)

- [x] Escopar IDs de requisição do Media3 pelo usuário ativo.
- [x] Armazenar proprietário e ID público da mídia nos metadados locais.
- [x] Exibir, remover e limpar somente downloads da conta atual.
- [x] Cobrir identidade e filtragem por usuário com testes unitários.

## 170. Feedback de falhas nas ações offline (v1.1.62)

- [x] Expor falhas de retry, remoção, limpeza, pausa, retomada e Wi-Fi somente.
- [x] Exibir a mensagem de erro em Snackbar sem interromper a tela de downloads.
- [x] Permitir limpar o aviso após a apresentação e cobrir erros em testes de ViewModel.

## 171. Mensagens de falha dos downloads (v1.1.63)

- [x] Converter códigos de falha do Media3 em mensagens de retry amigáveis.
- [x] Preservar o código técnico apenas como diagnóstico complementar.
- [x] Cobrir ausência de falha, falha desconhecida e códigos inesperados em testes unitários.

## 172. Feedback da limpeza total local (v1.1.64)

- [x] Informar sucesso ou falha da limpeza total de dados locais.
- [x] Preservar downloads offline durante a limpeza geral.
- [x] Só concluir a ação após limpar preferências e encerrar a sessão.
- [x] Cobrir o fluxo completo em teste de ViewModel.

## 173. Posições offline isoladas por conta (v1.1.65)

- [x] Escopar a retomada de downloads pela conta ativa.
- [x] Migrar uma posição legada sem expor a posição entre usuários.
- [x] Limpar chaves escopadas e legadas ao concluir a reprodução.
- [x] Cobrir a separação entre usuários em teste unitário.
## 174. Inicialização robusta da reprodução offline (v1.1.66)
- [x] Usar a sessão persistida quando o observador de autenticação ainda não emitiu o usuário.
- [x] Abortar a preparação se a conta mudar enquanto a sessão é resolvida.
- [x] Cobrir prioridade, fallback e ausência de usuário em testes unitários.
## 175. Autoridade da sessão offline durante troca de conta (v1.1.67)
- [x] Priorizar a sessão persistida quando o observador ainda contém a conta anterior.
- [x] Usar o usuário observado somente como fallback durante o bootstrap.
- [x] Cobrir a condição de corrida entre cache e sessão persistida em teste unitário.
## 176. Espaço real para downloads offline (v1.1.68)
- [x] Substituir o limite estático de 10 GB pelo espaço utilizável real do cache de downloads.
- [x] Atualizar o indicador periodicamente sem bloquear a interface.
- [x] Cobrir arredondamento e formatação do espaço disponível em teste unitário.
## 177. Feedback das ações no detalhe (v1.1.69)
- [x] Informar sucesso ao favoritar ou marcar uma mídia como assistida.
- [x] Informar falha do servidor e preservar o rollback do estado otimista.
- [x] Cobrir rollback e mensagem acionável em teste de ViewModel.
## 178. Links de compartilhamento públicos (v1.1.70)
- [x] Evitar que o compartilhamento do APK gere links de localhost ou loopback.
- [x] Usar a URL pública oficial somente para hosts locais.
- [x] Preservar URLs reais de LAN/internet e cobrir a normalização em teste.
## 179. Busca resiliente durante refresh (v1.1.71)
- [x] Preservar resultados válidos quando uma atualização falhar.
- [x] Exibir aviso acionável sem remover a navegação existente.
- [x] Cobrir falha de refresh e preservação dos resultados em teste.
## 180. Retry de streaming com posição (v1.1.72)
- [x] Repetir a fonte remota já preparada sem recarregar toda a mídia.
- [x] Preservar a posição conhecida no momento da falha.
- [x] Cobrir a política de posição do retry em teste unitário.
## 181. Recuperação de rede com posição (v1.1.73)
- [x] Usar a maior posição conhecida quando a rede retornar após uma falha.
- [x] Evitar que o reset interno do player retome o streaming do início.
- [x] Cobrir o cenário de posição zerada após erro em teste unitário.
## 182. Deep link consumível (v1.1.74)
- [x] Abrir links de mídia recebidos somente uma vez por destino.
- [x] Preservar a abertura do link após o login sem reabrir ao voltar para a Home.
- [x] Permitir que um novo link substitua o link já tratado.
- [x] Cobrir a política de consumo do deep link em testes unitários.
## 183. Recuperação LAN ao perder a rede (v1.1.75)
- [x] Reexecutar a descoberta quando uma rede disponível for perdida.
- [x] Evitar manter um endereço privado obsoleto durante a sessão.
- [x] Voltar automaticamente à URL pública quando o servidor LAN desaparecer.
- [x] Cobrir o fallback LAN com variações de URL em teste unitário.

## 184. Semântica completa nos cards de mídia (v1.1.76)
- [x] Anunciar ao TalkBack os estados ao vivo, assistido e Minha Lista.
- [x] Expor qualidade, episódios pendentes e progresso de reprodução.
- [x] Evitar anunciar progresso obsoleto para itens já assistidos.
- [x] Cobrir os rótulos acessíveis com testes unitários.

## 185. Retry seguro da API em oscilações transitórias (v1.1.77)
- [x] Repetir somente requisições de leitura idempotentes.
- [x] Cobrir respostas 408, 425, 429 e 5xx com orçamento limitado.
- [x] Respeitar `Retry-After` sem bloquear por períodos excessivos.
- [x] Não repetir login, progresso ou comandos de alteração.
- [x] Cobrir a política de retry com testes unitários.

## 186. Recuperação automática da TV ao vivo após rede (v1.1.78)

- [x] Atualizar o catálogo de canais quando a conectividade voltar.
- [x] Exibir aviso não bloqueante enquanto o dispositivo estiver offline.
- [x] Evitar refresh duplicado na emissão inicial ou em estados inalterados.
- [x] Cobrir a transição offline/online com testes unitários.

## 187. Preservação de faixas no retry do player (v1.1.79)

- [x] Preservar a seleção manual de áudio durante a reconstrução das faixas.
- [x] Preservar a seleção manual de legenda durante retry e reconexão.
- [x] Manter legendas explicitamente desativadas após a recuperação.
- [x] Cobrir a política de recuperação de faixas com testes unitários.

## 188. Idiomas legíveis nas faixas do player (v1.1.80)

- [x] Converter códigos de idioma do servidor em nomes compreensíveis.
- [x] Cobrir variantes como `pt-BR`, `por`, `eng` e `en-US`.
- [x] Preservar títulos explícitos enviados pelo servidor.
- [x] Cobrir a apresentação de idiomas com testes unitários.
## 189. Fallback LAN resiliente a perda transitória (v1.1.81)
- [x] Evitar troca imediata para a URL pública após uma única descoberta sem resposta.
- [x] Exigir duas ausências consecutivas antes de abandonar um endpoint privado ativo.
- [x] Resetar o contador assim que o servidor LAN autenticado voltar a responder.
- [x] Cobrir a política com teste unitário.

## 190. Descoberta LAN por interface local (v1.1.82)
- [x] Vincular probes UDP às redes Wi-Fi e Ethernet locais elegíveis.
- [x] Manter fallback compatível para dispositivos sem rede local exposta pelo sistema.
- [x] Coletar respostas de múltiplas interfaces sem duplicar servidores.
- [x] Cobrir o orçamento de retries da varredura com teste unitário.

## 191. Qualidade adaptativa para rede medida (v1.1.83)
- [x] Detectar o estado de rede medida pelo Android.
- [x] Limitar o modo automático a 720p em rede medida.
- [x] Preservar escolhas manuais de qualidade do usuário.
- [x] Cobrir a política com testes unitários.

## 192. Transparência da qualidade adaptativa (v1.1.84)
- [x] Expor no estado do player quando a rede atual é medida.
- [x] Informar no menu de qualidade que o modo Auto está limitado a 720p.
- [x] Manter as escolhas manuais sem alteração.
- [x] Cobrir os rótulos da qualidade adaptativa com teste unitário.

## 193. UX adaptativa para celular, tablet e TV (v1.1.85)
- [x] Definir contratos de layout por família de dispositivo.
- [x] Centralizar e limitar o conteúdo em tablets.
- [x] Ampliar artes e espaçamento para navegação por foco na TV.
- [x] Criar testes unitários e testes de uso Compose para as três superfícies.

## 194. Grade e atualização automática na Android TV (v1.1.86)
- [x] Reduzir o tamanho mínimo das capas na TV para exibir mais títulos por linha.
- [x] Usar colunas previsíveis e amigáveis ao controle remoto na TV.
- [x] Atualizar a biblioteca automaticamente enquanto a tela estiver em primeiro plano.
- [x] Cobrir a densidade e o cálculo de colunas com testes unitários.
## 208. Ordenação ascendente e descendente (v1.1.98)
- [x] Adicionar direção Ascendente/Descendente ao menu de ordenação da biblioteca.
- [x] Enviar `SortOrder` ao servidor e persistir a preferência localmente.
- [x] Cobrir restauração, persistência e requisição com teste unitário.

## 209. Direção da ordenação nas Configurações (v1.1.99)
- [x] Exibir a direção atual junto da ordenação padrão.
- [x] Permitir escolher Ascendente ou Descendente nas Configurações.
- [x] Persistir e restaurar a direção com teste unitário.

## 210. Rótulo neutro para o critério de nome (v1.2.0)
- [x] Separar o critério “Nome” da direção Ascendente/Descendente.
- [x] Evitar o texto contraditório “Nome A-Z” quando Descendente estiver ativo.
- [x] Cobrir a preferência de direção nas Configurações.

## 211. Refresh de foreground resiliente na TV (v1.2.1)
- [x] Evitar que o timer de atualização cancele uma carga de Home ou Biblioteca ainda em andamento.
- [x] Atualizar imediatamente ao retornar ao foreground sem duplicar requisições.
- [x] Cobrir a proteção contra refresh concorrente em testes de ViewModel.
- [x] Remover o uso de ícone Compose obsoleto na tela de Configurações.

## 212. Quick Connect alinhado à disponibilidade do servidor (v1.2.2)
- [x] Consultar `QuickConnect/Enabled` antes de oferecer a ação de autenticação.
- [x] Exibir uma mensagem clara quando o recurso estiver desativado no servidor.
- [x] Reconsultar a disponibilidade ao trocar de endpoint LAN/internet.
- [x] Cobrir o contrato Retrofit e o bloqueio no ViewModel.

## 213. Recuperação automática da busca ao voltar a rede (v1.2.3)
- [x] Reexecutar a busca ativa uma única vez quando a conectividade retornar.
- [x] Exibir aviso não bloqueante enquanto a busca estiver offline.
- [x] Preservar query, filtro e resultados atuais durante a oscilação.
- [x] Cobrir a transição offline/online sem refresh duplicado.

## 214. Recuperação de episódios na tela de detalhes (v1.2.4)
- [x] Encerrar o carregamento quando a API de episódios falhar.
- [x] Exibir erro localizado dentro da seção de temporadas/episódios.
- [x] Permitir tentar novamente sem recarregar todos os detalhes da mídia.
- [x] Limpar episódios antigos ao trocar de temporada.

## 215. Acessibilidade da ordenação da biblioteca (v1.2.5)
- [x] Anunciar o critério e a direção atuais no botão de ordenação.
- [x] Cobrir as opções Ascendente e Descendente em teste Compose.

## 216. Recuperação inline da Minha Lista (v1.2.6)
- [x] Exibir ação “Tentar novamente” quando a atualização falhar com títulos já carregados.
- [x] Cobrir a ação de retry no aviso inline com teste Compose.

## 217. Estado de sessão expirado na Minha Lista (v1.2.7)
- [x] Encerrar corretamente o carregamento quando não houver usuário salvo.
- [x] Exibir a mensagem de reautenticação sem deixar o refresh preso.
- [x] Cobrir o estado sem sessão em teste unitário.

## 218. Atualização automática da biblioteca na Android TV
- [x] Atualizar a biblioteca imediatamente ao voltar ao primeiro plano na TV.
- [x] Reconciliar alterações do servidor a cada minuto enquanto a biblioteca estiver visível na TV.
- [x] Manter celular e tablet com atualização explícita/pull-to-refresh, sem timer em segundo plano.
- [x] Cobrir as políticas de TV e dispositivos móveis em teste unitário.

## 219. Recuperação da biblioteca após retorno da rede
- [x] Observar a transição offline/online no ViewModel da biblioteca.
- [x] Atualizar a biblioteca carregada uma única vez quando a rede retornar.
- [x] Não duplicar a requisição se outra atualização já estiver em andamento.
- [x] Cobrir a política e o fluxo de recuperação em testes unitários.

## 220. Indicador offline recuperável na biblioteca
- [x] Informar visualmente quando a biblioteca está sem conexão.
- [x] Manter a grade/rolagem disponível durante a indisponibilidade da rede.
- [x] Oferecer retry acessível e compatível com foco da Android TV.
- [x] Cobrir o banner e a ação de retry em teste Compose.

## 221. Polling da biblioteca suspenso sem rede
- [x] Evitar novas chamadas periódicas da Android TV enquanto o dispositivo estiver offline.
- [x] Preservar a atualização automática única quando a conectividade retornar.
- [x] Cobrir a proteção contra polling offline no teste do ViewModel.

## 222. Cancelamento de Quick Connect ao trocar de servidor
- [x] Cancelar o polling ativo antes de verificar novo endpoint.
- [x] Invalidar também a coroutine que ainda inicia o polling.
- [x] Ignorar respostas em voo do endpoint anterior.
- [x] Limpar PIN, segredo e contador do fluxo anterior.
- [x] Cobrir troca de servidor sem chamadas ao endpoint antigo.

## 223. Paginação da biblioteca respeita estado offline
- [x] Bloquear carregamento de páginas adicionais sem rede.
- [x] Manter paginação disponível após recuperação da conectividade.
- [x] Cobrir sentinel offline no teste do ViewModel.

## 224. Respostas de rede obsoletas isoladas (v1.2.12)
- [x] Não iniciar carregamento de biblioteca quando a rede já estiver offline.
- [x] Ignorar disponibilidade do Quick Connect retornada por endpoint anterior.
- [x] Cobrir chamadas offline e troca de servidor com testes de regressão.

## 225. Foco remoto no modo lista (v1.2.13)
- [x] Tornar a linha inteira da biblioteca um único alvo de clique e foco na Android TV.
- [x] Remover o clique aninhado do card usado dentro da linha.
- [x] Cobrir o card não clicável e a navegação instrumentada na TV.

## 226. OSD acessível por controle remoto na TV (v1.2.14)
- [x] Manter os controles do player acessíveis durante a reprodução na Android TV.
- [x] Preservar o auto-ocultamento de 3 segundos em celulares.
- [x] Cobrir a política de visibilidade em teste unitário.

## 227. Buffer adaptativo e transporte resiliente no player (v1.2.15)
- [x] Configurar buffer maior para streaming remoto e buffer menor para reprodução offline.
- [x] Aumentar os timeouts HTTP para redes internet/LAN com latência variável.
- [x] Permitir redirecionamentos entre HTTP/HTTPS usados pelo servidor ou proxy.
- [x] Cobrir os limites da política em testes unitários.

## 228. Transporte resiliente nos downloads (v1.2.16)
- [x] Aplicar os mesmos timeouts HTTP resilientes ao DownloadManager do APK.
- [x] Permitir que downloads acompanhem redirecionamentos HTTP/HTTPS do servidor ou proxy.
- [x] Confirmar por busca que não restou caminho de mídia com a configuração antiga.

## 229. Política de transporte Media3 compartilhada (v1.2.17)
- [x] Expor uma única política de timeout para player e downloads do APK.
- [x] Remover a duplicação dos valores de transporte entre módulos Android.
- [x] Cobrir os valores compartilhados no teste unitário da política de streaming.

## 230. Indicador de rota LAN/internet no perfil (v1.2.18)
- [x] Classificar a URL ativa como LAN, Internet ou servidor remoto.
- [x] Exibir a rota atual ao lado do status do servidor conectado.
- [x] Cobrir IPs privados, hostnames `.local`, DuckDNS e entradas inválidas.

## 231. Cópia da URL ativa no perfil (v1.2.19)
- [x] Adicionar ação acessível para copiar a URL efetivamente usada pelo APK.
- [x] Exibir confirmação não bloqueante após a cópia.
- [x] Cobrir a seleção e normalização do endpoint ativo em teste unitário.

## 232. Links compartilhados no formato oficial do web player (v1.2.20)
- [x] Gerar links de mídia com `/web/#/details?id=...` no compartilhamento do APK.
- [x] Preservar a substituição segura de endpoints de loopback pelo endereço público.
- [x] Atualizar os testes de URL e texto compartilhado para o formato oficial.

## 233. Validação estrita de links oficiais no APK (v1.2.21)
- [x] Aceitar somente o caminho oficial `/web` e seus subcaminhos.
- [x] Rejeitar caminhos parecidos, como `/website`, para evitar capturas indevidas.
- [x] Cobrir a validação com testes JVM de deep link.

## 234. Executor assíncrono e limitado para downloads (v1.2.22)
- [x] Remover o executor direto que podia executar operações do DownloadManager no thread chamador.
- [x] Usar pool adaptativo de 2 a 4 workers para celular, tablet e Android TV.
- [x] Cobrir os limites da política de concorrência com testes JVM.

## 235. Janela limitada para descoberta LAN (v1.2.23)
- [x] Limitar a descoberta automática a uma janela máxima de 10 segundos.
- [x] Preservar timeout zero para desativação imediata da varredura.
- [x] Cobrir valores negativos, padrão e excessivos com teste unitário.

## 236. Carga imediata da Biblioteca na TV (v1.2.24)
- [x] Carregar a biblioteca imediatamente ao entrar na tela.
- [x] Evitar que a primeira atualização dependa do intervalo automático de 60 segundos da TV.
- [x] Preservar o refresh ao retornar ao primeiro plano e o refresh manual em dispositivos móveis.
- [x] Encerrar a árvore do emulador criada pelo wrapper após o teste.

## 237. Refresh não destrutivo de Minha Lista na TV (v1.2.25)
- [x] Não cancelar uma requisição de Minha Lista que ainda esteja em andamento.
- [x] Evitar requisições duplicadas no refresh periódico da TV.
- [x] Cobrir o comportamento com teste unitário de concorrência.

## 238. Descoberta LAN limitada ao foreground (v1.2.26)
- [x] Iniciar a recuperação LAN quando a Activity entra no foreground.
- [x] Interromper callbacks e varreduras ao deixar o aplicativo em segundo plano.
- [x] Revalidar a rota automaticamente ao retornar ao aplicativo.

## 239. Preferências de ordenação aplicadas na primeira carga (v1.2.27)
- [x] Aguardar sort, direção e filtros persistidos antes da primeira consulta da biblioteca.
- [x] Evitar que a abertura rápida da tela substitua temporariamente a preferência do usuário.
- [x] Cobrir a primeira requisição com ordenação descendente em teste unitário.

## 240. Verificação de atualização ao retornar ao foreground (v1.2.28)
- [x] Verificar novas releases do APK a cada retorno ao foreground.
- [x] Não bloquear a navegação quando o GitHub ou a rede estiverem indisponíveis.
- [x] Evitar reapresentar a mesma versão dispensada durante a sessão.
- [x] Cobrir disponibilidade, URL de download e versão dispensada em testes unitários.

## 241. Retry do player condicionado à conectividade (v1.2.29)
- [x] Pausar retries automáticos de streams remotos enquanto o dispositivo estiver offline.
- [x] Retomar a reprodução automaticamente após o retorno da rede.
- [x] Preservar reprodução local/offline sem depender de conectividade.
- [x] Cobrir a política de pausa para erros transitórios e codecs permanentes.

## 242. Busca sem chamadas enquanto offline (v1.2.30)
- [x] Evitar chamadas ao servidor durante pesquisa digitada sem conectividade.
- [x] Exibir mensagem recuperável na tela de busca.
- [x] Retomar a pesquisa automaticamente após a reconexão.
- [x] Cobrir ausência de chamada offline e execução após reconectar.

## 243. TV ao vivo sem polling offline (v1.2.31)
- [x] Bloquear refresh periódico dos canais enquanto não houver conexão.
- [x] Bloquear carregamento do guia e agendamento de gravações offline.
- [x] Atualizar os canais automaticamente após a reconexão.
- [x] Cobrir a ausência de chamadas offline e o refresh pós-rede.

## 244. Refresh não destrutivo da TV ao vivo (v1.2.32)
- [x] Evitar que o timer de foreground cancele uma resposta lenta de canais.
- [x] Manter o refresh manual destrutivo disponível no botão Atualizar.
- [x] Cobrir o comportamento ocioso, carregando e offline em teste de política.

## 245. Downloads com foco remoto na Android TV (v1.2.33)
- [x] Transformar download concluído em alvo único de foco e reprodução pelo controle remoto.
- [x] Exibir contorno visual no item focado na TV.
- [x] Manter Play separado no celular e retry/remoção disponíveis para falhas.
- [x] Cobrir clique do alvo remoto em teste Compose executado na Android TV.

## 246. Layout adaptativo de Downloads para tablet e TV (v1.2.34)
- [x] Limitar largura do conteúdo em tablets para melhorar leitura e navegação.
- [x] Usar largura máxima maior e centralizada na Android TV.
- [x] Manter largura total em celulares.
- [x] Cobrir os três perfis de largura em teste unitário.

## 248. Seleção LAN após carregar servidores persistidos (v1.3.8)
- [x] Aguardar a emissão da lista persistida antes de escolher automaticamente um servidor LAN.
- [x] Evitar que uma resposta UDP chegue antes do `serverId` salvo e conecte a um servidor incorreto.
- [x] Cobrir a corrida com teste unitário de política de seleção.

## 249. Foco remoto na autenticação para Android TV (v1.3.9)
- [x] Exibir anel vermelho de foco nos botões de login, cadastro e Quick Connect.
- [x] Preservar a navegação por controle remoto sem alterar a lógica de autenticação.
- [x] Manter os testes semânticos dos formulários e do botão Quick Connect.

## 250. Foco remoto em seleção de usuário e cadastro (v1.3.10)
- [x] Exibir foco vermelho nos avatares selecionáveis da tela de login.
- [x] Exibir foco vermelho nas ações de confirmar e voltar do cadastro.
- [x] Cobrir a ativação acessível da seleção de usuário por avatar.

## 251. Ordenação acessível para controle remoto (v1.3.11)
- [x] Exibir foco vermelho nos itens de ordenação e na ação de aplicar na Android TV.
- [x] Expor descrições acessíveis explícitas para ascendente e descendente.
- [x] Cobrir a presença das duas direções e a aplicação conjunta da ordenação em teste Compose.

## 252. Filtros acessíveis para controle remoto (v1.3.12)
- [x] Exibir foco vermelho nos chips de filtro e nas ações do diálogo.
- [x] Expor descrições acessíveis para selecionar e remover filtros.
- [x] Cobrir seleção, limpeza e fechamento do diálogo em teste Compose.

## 253. Encerramento do Quick Connect ao sair da autenticação (v1.3.13)
- [x] Cancelar o polling ao retornar da aba Quick Connect para o login.
- [x] Cancelar o polling ao deixar a tela de autenticação.
- [x] Preservar a limpeza de spinner, segredo e estado de espera coberta no ViewModel.

## 254. Corrida de refresh ao abrir biblioteca na Android TV (v1.3.14)
- [x] Impedir que o refresh de foreground substitua o carregamento inicial ainda não refletido no estado visual.
- [x] Preservar uma única requisição quando a tela dispara carga inicial e refresh simultaneamente.
- [x] Cobrir a janela de corrida com teste unitário do `LibraryViewModel`.

## 255. Corrida de refresh ao abrir a Home na Android TV (v1.3.15)
- [x] Tratar o `Job` ativo como fonte de verdade antes da publicação de `isLoading`.
- [x] Evitar que o refresh de foreground duplique ou cancele a carga inicial da Home.
- [x] Cobrir a janela de corrida no `HomeViewModel`.

## 256. Atualização manual da Home por controle remoto (v1.3.16)
- [x] Expor a ação `Atualizar Home` no menu superior da Home.
- [x] Manter o alvo acessível no D-pad e mostrar estado ocupado sem removê-lo do foco.
- [x] Cobrir o acionamento da ação em teste instrumentado de Android TV.

## 257. Versão do servidor somente após verificação (v1.3.17)
- [x] Remover a versão histórica fixa do servidor oficial na tela inicial de conexão.
- [x] Exibir a versão apenas quando o handshake do servidor retornar essa informação.
- [x] Cobrir a ausência de versão não verificada em teste unitário.

## 258. Corrida de refresh na TV ao vivo (v1.3.18)
- [x] Tratar o `Job` ativo como fonte de verdade antes da publicação de `isLoading`.
- [x] Evitar que o timer de foreground cancele a primeira carga de canais.
- [x] Cobrir a janela de corrida em teste unitário do `LiveTvViewModel`.

## 259. Controle remoto na barra da TV ao vivo (v1.3.19)
- [x] Separar a barra superior em componente testável sem dependência de rede ou Hilt.
- [x] Cobrir foco D-pad, atualização de canais e abertura do EPG em teste instrumentado.
- [x] Impedir o acesso ao EPG enquanto ainda não existem canais carregados.

## 260. EPG atualizado enquanto está aberto (v1.3.20)
- [x] Atualizar a programação automaticamente a cada minuto enquanto o diálogo EPG estiver visível.
- [x] Pausar a consulta quando o diálogo for fechado ou a Activity sair do foreground.
- [x] Cobrir a política de atualização aberta/fechada em teste unitário.

## 261. Conteúdo visual do EPG (v1.3.21)
- [x] Cobrir o botão `Gravar` para programas agendáveis.
- [x] Impedir que programas já agendados sejam apresentados para gravação novamente.
- [x] Manter programas visíveis e oferecer retry quando a atualização do EPG falhar.

## 262. Foco remoto nas ações do EPG (v1.3.22)
- [x] Destacar em vermelho o botão `Gravar` quando receber foco na Android TV.
- [x] Manter o comportamento padrão de toque em celular e tablet.
- [x] Cobrir o botão agendável no conteúdo do EPG em teste Compose instrumentado.

## 263. Foco remoto completo no EPG (v1.3.23)
- [x] Aplicar destaque vermelho também ao retry de erro do guia.
- [x] Aplicar destaque vermelho ao botão `Fechar` do diálogo.
- [x] Reutilizar um componente único para manter o comportamento consistente entre as ações.

## 264. Retry do player com foco remoto (v1.3.24)
- [x] Extrair o cartão de erro do player para um componente testável.
- [x] Destacar em vermelho o botão `Tentar novamente` quando focado na Android TV.
- [x] Preservar o retry remoto para streams online e reprodução offline.

## 265. Foco automático na recuperação do player (v1.3.25)
- [x] Direcionar automaticamente o foco para `Tentar novamente` quando um erro aparece na Android TV.
- [x] Manter o foco automático restrito à televisão, sem interferir em celular e tablet.
- [x] Verificar a semântica de foco e a ativação do retry em teste Compose instrumentado.

## 266. Foco no próximo episódio (v1.3.26)
- [x] Direcionar o foco para `Assistir Agora` quando o prompt surgir na Android TV.
- [x] Destacar a ação principal do próximo episódio com a cor de foco vermelha.
- [x] Manter o cancelamento e o comportamento de toque em outras superfícies.

## 267. Retry focável na TV ao vivo (v1.3.27)
- [x] Aplicar o mesmo destaque vermelho de controle remoto aos retries de erro dos canais e gravações.
- [x] Manter o retry de toque em celular/tablet e o comportamento de atualização existente.
- [x] Cobrir foco e acionamento do retry do EPG em teste instrumentado de Android TV.

## 268. Retry focável na Home (v1.3.28)
- [x] Aplicar destaque vermelho de foco remoto aos três cartões de erro recuperáveis da Home.
- [x] Preservar o comportamento de toque em celular/tablet.
- [x] Cobrir foco e acionamento do retry em teste Compose instrumentado.

## 269. Foco remoto nos menus do player (v1.3.29)
- [x] Destacar em vermelho as opções de áudio, legendas e qualidade quando focadas na Android TV.
- [x] Preservar seleção por toque e semântica de rádio em celular/tablet.
- [x] Cobrir o foco remoto de uma opção de idioma em teste instrumentado.

## 270. Foco remoto nos controles avançados do player (v1.3.30)
- [x] Aplicar o mesmo foco visual aos menus de velocidade, temporizador e proporção da tela.
- [x] Manter a seleção inteira da linha e a semântica de rádio existente.
- [x] Cobrir o foco remoto de uma opção do temporizador em teste instrumentado.

## 271. Retry focável na biblioteca (v1.3.31)
- [x] Aplicar o foco vermelho aos retries de erro inicial, erro em grade e banner offline.
- [x] Preservar a grade adaptativa e a interação por toque.
- [x] Cobrir o foco remoto do retry offline em teste instrumentado.

## 272. Encerramento seguro da sessão remota ao trocar mídia (v1.3.32)
- [x] Conferir a versão anterior antes do bump: `v1.3.31`, `versionCode=332`, SHA-256 `8BDF480C8BEAC44DE5951E6066D9C00B34E07E175559CBF9B3EBBF8798671385`.
- [x] Encerrar o playback remoto anterior antes de iniciar outra mídia, aguardando o relatório de parada.
- [x] Impedir que relatórios atrasados atravessem troca de conta, sessão ou geração de carregamento.
- [x] Preservar o fluxo offline sem chamadas indevidas ao servidor e proteger a carga offline contra callbacks obsoletos.
- [x] Validar 19 testes instrumentados do player na Android TV: 18 aprovados, 1 skipped esperado, 0 falhas.
- [x] Validar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.

## 273. Mensagens acionáveis de conexão e Quick Connect (v1.3.33)
- [x] Conferir a versão anterior antes do bump: `v1.3.32`, `versionCode=333`, SHA-256 `60F9A6A831DB3A27544CCB127BA68C6037D877CBB9915795C66DEBC5C41B5F78`.
- [x] Traduzir falhas de HTTP, timeout, host indisponível e bloqueio CLEARTEXT em mensagens úteis para conexão, login, cadastro e Quick Connect.
- [x] Validar 5 testes instrumentados de autenticação na Android TV, sem falhas.
- [x] Validar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Gerar o APK local `v1.3.33`, `versionCode=334`, 7.336.683 bytes, SHA-256 `D08648413EE7BF87EA3215594F8F23691DE4CEBD7F2AC6619532B31061D0117E`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 274. Loop de atualização resiliente na TV (v1.3.34)
- [x] Conferir a versão anterior antes do bump: `v1.3.33`, `versionCode=334`, SHA-256 `D08648413EE7BF87EA3215594F8F23691DE4CEBD7F2AC6619532B31061D0117E`.
- [x] Proteger os loops de atualização da Home e da Biblioteca contra falhas transitórias sem engolir cancelamento de lifecycle.
- [x] Validar os testes instrumentados de regressão: Home 12/12 e Biblioteca 14/14 na Android TV.
- [x] Validar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Gerar e registrar o APK local `v1.3.34`, `versionCode=335`, 7.336.679 bytes, SHA-256 `46E34B8C6BFC73FD41B9A0ED0CB42862AD1F9D231B13643C2CEC2553FFAF62B4`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 280. Controles básicos de reprodução no SyncPlay (v1.3.40)
- [x] Conferir a versão anterior antes do bump: `v1.3.39`, `versionCode=340`, 7.353.067 bytes, SHA-256 `3095F6E29CB528773250E0484636E73AD774281E7B0334C40C153B7CED08FD31`.
- [x] Mapear as rotas reais do servidor para `SyncPlay/Pause`, `SyncPlay/Unpause` e `SyncPlay/Stop` no cliente Android.
- [x] Expor os comandos na sala ativa com foco compatível com controle remoto/D-pad e proteção contra toques duplicados.
- [x] Adicionar cobertura de contrato da API e teste unitário do ViewModel para envio único durante submissão.
- [x] Executar a suíte instrumentada da Biblioteca na Android TV: 15/15, 0 skipped, 0 falhas; o módulo SyncPlay não possui testes instrumentados (`0 testes`).
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0 antes do empacotamento.
- [x] Gerar e registrar o APK local `v1.3.40`, `versionCode=341`, 7.353.067 bytes, SHA-256 `F693CFC81D1D4128FD5CC45E13C149AA82436A026CDADF11D6F846BB01C16767`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 279. Cobertura instrumentada do banner offline contextual (v1.3.39)
- [x] Conferir a versão anterior antes do bump: `v1.3.38`, `versionCode=339`, SHA-256 `FAAAEDD038CF40EC7B35C1EFF3A722978A59156EFEE68E2CEC7A0F020B3442F9`.
- [x] Adicionar teste instrumentado para a mensagem contextual de Minha Lista no banner offline compartilhado.
- [x] Confirmar que a ação `Tentar novamente` continua visível e compatível com foco remoto/D-pad.
- [x] Validar a suíte instrumentada da Biblioteca na Android TV: 15/15, 0 skipped, 0 falhas.
- [x] Validar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0 antes do bump.
- [x] Gerar e registrar o APK local `v1.3.39`, `versionCode=340`, 7.353.067 bytes, SHA-256 `3095F6E29CB528773250E0484636E73AD774281E7B0334C40C153B7CED08FD31`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 278. Banner offline contextual em Minha Lista (v1.3.38)
- [x] Conferir a versão anterior antes do bump: `v1.3.37`, `versionCode=338`, SHA-256 `CC613BFFDD9E63F65AF8E9BDF0C861C4700EA0D804CDFF2A9A25858F44A681DC`.
- [x] Exibir em Minha Lista o estado offline enquanto a conectividade estiver indisponível.
- [x] Reutilizar o banner focável da Biblioteca com mensagem contextual e ação de retry para TV/D-pad.
- [x] Cobrir o estado offline e sua limpeza após o retorno da rede no teste unitário de reconciliação.
- [x] Validar a suíte instrumentada da Biblioteca na Android TV: 14/14, 0 skipped, 0 falhas.
- [x] Validar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0 antes do bump.
- [x] Gerar e registrar o APK local `v1.3.38`, `versionCode=339`, 7.353.067 bytes, SHA-256 `FAAAEDD038CF40EC7B35C1EFF3A722978A59156EFEE68E2CEC7A0F020B3442F9`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 277. Recuperação de Minha Lista após retorno da rede (v1.3.37)
- [x] Conferir a versão anterior antes do bump: `v1.3.36`, `versionCode=337`, SHA-256 `2AEF6EADBD1109FBA95BDA678CFF89B7A8DAC4F10169FA22046F152ED1D0BC8C`.
- [x] Fazer Minha Lista observar o estado de conectividade e atualizar quando a rede voltar após uma interrupção.
- [x] Reutilizar a política de refresh ocioso para não cancelar uma requisição em andamento nem duplicar chamadas.
- [x] Adicionar teste unitário de regressão para uma lista stale ser reconciliada após o retorno da rede.
- [x] Validar a suíte instrumentada da Biblioteca na Android TV: 14/14, 0 skipped, 0 falhas.
- [x] Validar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0 antes do bump.
- [x] Gerar e registrar o APK local `v1.3.37`, `versionCode=338`, 7.353.067 bytes, SHA-256 `CC613BFFDD9E63F65AF8E9BDF0C861C4700EA0D804CDFF2A9A25858F44A681DC`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 276. Atualização resiliente de Minha Lista/Favoritos na TV (v1.3.36)
- [x] Conferir a versão anterior antes do bump: `v1.3.35`, `versionCode=336`, SHA-256 `9F87A81CE26E38933C07851AC14F49E7F0CDB28A409653783C694C91353AC5AF`.
- [x] Reutilizar o scheduler resiliente da Biblioteca em Minha Lista/Favoritos, preservando atualização automática na Android TV após falha transitória.
- [x] Validar o cancelamento pelo lifecycle e o bloqueio de polling fora do foreground.
- [x] Validar a suíte instrumentada da Biblioteca na Android TV: 14/14, 0 skipped, 0 falhas.
- [x] Validar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Gerar e registrar o APK local `v1.3.36`, `versionCode=337`, 7.353.067 bytes, SHA-256 `2AEF6EADBD1109FBA95BDA678CFF89B7A8DAC4F10169FA22046F152ED1D0BC8C`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 275. Atualização resiliente da TV ao vivo e EPG (v1.3.35)
- [x] Conferir a versão anterior antes do bump: `v1.3.34`, `versionCode=335`, SHA-256 `46E34B8C6BFC73FD41B9A0ED0CB42862AD1F9D231B13643C2CEC2553FFAF62B4`.
- [x] Centralizar canais e EPG em scheduler de foreground resiliente a falhas transitórias.
- [x] Validar os testes instrumentados do Live TV: 8/8, 0 skipped, 0 falhas.
- [x] Validar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Gerar e registrar o APK local `v1.3.35`, `versionCode=336`, 7.353.067 bytes, SHA-256 `9F87A81CE26E38933C07851AC14F49E7F0CDB28A409653783C694C91353AC5AF`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## Estado APK validado — Hero, erros parciais da Home e sessões remotas
- [x] Corrigir as ações do destaque da Home: `Reproduzir` abre o player e `Mais informações` abre os detalhes.
- [x] Exibir falhas recuperáveis por seção na Home (Continuar assistindo, Próximos episódios, Favoritos e adicionados recentemente), preservando conteúdo válido já carregado e evitando estado vazio enganoso.
- [x] Limpar sessões remotas obsoletas quando a atualização falha e fechar a confirmação de parada se a sessão desaparecer; impedir envio de STOP para sessão stale.
- [x] Validar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease`: todos concluídos com código 0; suíte unitária total: 1.025 testes, 0 falhas/erros.
- [x] Validar testes instrumentados na Android TV: Home 13/13, SyncPlay 5/5 e Biblioteca 16/16; emulador encerrado após os testes.
- [x] Registrar artefato local de validação, sem publicação: APK `v1.3.55`, `versionCode=356`, 7.404.867 bytes, SHA-256 `49C87BDF85609532084CCC774EDF98C36E26F2E652C400411A593AF268AAB751`.
- [ ] Não houve bump de versão nem publicação nesta rodada. Ao atualizar uma release autorizada, atualizar as notas para descrever somente estas melhorias/correções efetivamente presentes e validadas no APK publicado; não incluir alterações exclusivas do servidor.

## Correções de limites de busca local e remota (v1.3.55 — validação local)
- [x] Impedir que a busca local avance quando a mídia não informa duração, não é seekable ou o player não oferece o comando de seek.
- [x] Desabilitar slider e botões de avanço/retrocesso nessas condições; ocultar/bloquear skip de intro/créditos e gestos de busca sem duração válida.
- [x] Evitar salvar progresso local inválido nessas condições, inclusive nas gravações forçadas ao pausar ou liberar o player.
- [x] Limitar o avanço/retrocesso do SyncPlay remoto à duração conhecida, incluindo posição stale fora do intervalo e proteção contra overflow; preservar o passo quando o servidor não informa duração.
- [x] Validar a suíte JVM completa: 1.034 testes, 0 falhas, 0 erros.
- [x] Validar `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Validar player na Android TV: 23 testes executados, 0 falhas e 1 skip (medição Cast requer serviços Cast); SyncPlay: 6 testes, 0 falhas/skip.
- [x] Validar player no tablet: 23 testes, 0 falhas e 4 skips de foco remoto exclusivos da TV; emuladores encerrados após os testes.
- [x] APK local de validação (sem bump/publicação): `v1.3.55`, `versionCode=356`, 7.404.867 bytes, SHA-256 `A2F125B0C8FBF35A063FD756BB27D9E29840C341F83BB5277E120319E132381D`.
- [x] Ao atualizar uma release autorizada, as notas devem espelhar somente as melhorias/correções implementadas e validadas no APK correspondente; nenhuma alteração exclusiva do servidor deve constar.
- [ ] Não houve bump nem publicação nesta rodada. Nas notas da próxima release autorizada, descrever precisamente os limites de busca local/remota e o progresso/skip, somente se presentes no APK publicado.
