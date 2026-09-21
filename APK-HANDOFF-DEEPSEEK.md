# MulletaFlix Android — handoff para continuidade com DeepSeek

Atualizado em: 21/09/2026  
Escopo desta conversa: **somente o APK Android**.

> Nota desta rodada: as evidências abaixo foram coletadas em 21/09/2026 pelo agente
> DeepSeek. O servidor real (MulletaFlix 12.0.27) respondeu em
> `http://192.168.15.9:8096` e via `http://mulletaflix.duckdns.org:8096`; nenhum
> script de release do servidor foi executado.

## Skills e metodologias utilizadas

Estas são as skills efetivamente usadas para orientar o desenvolvimento, testes, revisão e handoff deste projeto. O próximo agente deve manter a mesma combinação; skills adicionais podem ser incluídas quando uma tarefa exigir, mas não devem substituir as regras abaixo.

| Skill/metodologia | Localização nesta máquina | Aplicação no projeto |
|---|---|---|
| `fable-method` | `C:\Users\Raphael\.agents\skills\fable-method\SKILL.md` | Definir intenção verificável, distinguir comportamento/defeito e registrar evidências. |
| `fable-loop` | `C:\Users\Raphael\.agents\skills\fable-loop\SKILL.md` | Repetir o ciclo implementação → avaliação → correção até a Quality Bar passar. |
| `fable-judge` | `C:\Users\Raphael\.agents\skills\fable-judge\SKILL.md` | Revisão crítica, critérios de aceite e linhas `INTENT:`, `TWINS:` e `AUTH:` quando aplicáveis. |
| `everything-claude-code` | `C:\Users\Raphael\.agents\skills\everything-claude-code\SKILL.md` | Engenharia incremental, leitura do repositório, testes e preservação de alterações existentes. |
| `android-cli` | `C:\Users\Raphael\.agents\skills\android-cli\SKILL.md` | Gradle, ADB, instalação, execução e diagnóstico em dispositivos Android. |
| `android-dev` | `C:\Users\Raphael\.agents\skills\android-dev\SKILL.md` | Práticas de implementação Android, arquitetura e compatibilidade de plataforma. |
| `android_ui_verification` | `C:\Users\Raphael\.agents\skills\android_ui_verification\SKILL.md` | Verificação visual/funcional de Compose, foco remoto, scroll e layouts por dispositivo. |
| `mobile-design` | `C:\Users\Raphael\.agents\skills\mobile-design\SKILL.md` | UX responsiva, densidade de grade, acessibilidade e diferenças entre celular/tablet/TV. |
| `caveman` | `C:\Users\Raphael\.agents\skills\caveman\SKILL.md` | Execução disciplinada, objetiva e orientada a evidências no terminal. |
| `caveman-compress` | `C:\Users\Raphael\.agents\skills\caveman-compress\SKILL.md` | Compactar contexto, decisões e resultados para continuidade entre agentes. |
| `caveman-learn` | `C:\Users\Raphael\.agents\skills\caveman-learn\SKILL.md` | Capturar aprendizados, falhas recorrentes e procedimentos reutilizáveis. |
| `cavecrew` | `D:\Users\Raphael\Documents\Projetos\mulletaflix\.agents\skills\cavecrew\SKILL.md` | Coordenação de especialistas, divisão de investigação e revisão do trabalho. |
| `gauntlet-loop` | `D:\Users\Raphael\Documents\Projetos\mulletaflix\.agents\skills\gauntlet-loop\SKILL.md` | Quality Gate do projeto: Builder versus Evaluator, testes reais e saída 0 obrigatória. |

### Metodologias combinadas

1. **Fable Method/Loop/Judge**: transformar a solicitação em intenção e critérios observáveis; implementar; buscar twins/duplicações; executar avaliação crítica; corrigir até passar.
2. **Gauntlet Loop — Builder vs. Evaluator**: o agente que altera o código não pode ser a única validação. O Evaluator deve executar testes, lint, build, instalação e, quando aplicável, teste visual/real contra o servidor.
3. **Caveman + Cavecrew**: manter o trabalho simples, rastreável e econômico em contexto; registrar o que foi aprendido e delegar investigações independentes quando necessário.
4. **Android profissional por camadas**: preservar separação entre `domain`, `data`, `core`, `design-system`, `feature` e `app`; mudanças de UI devem ter teste de política/Compose proporcional ao risco.
5. **Release APK-only nesta conversa**: servidor e APK têm releases separadas; alterações do APK só geram pacote/publicação Android.

### Ordem operacional recomendada

```text
Fable: intenção/aceite
  → Caveman: inspeção curta e segura
  → Builder: implementação/testes
  → Cavecrew: revisão especializada quando necessário
  → Fable Judge + Gauntlet Evaluator: testes reais/lint/build/UI
  → Caveman Compress/Learn: registrar evidências e aprendizado
  → release somente do APK, se a tarefa estiver concluída
```

## Estado confirmado

- Versão atual do APK: **1.2.43**.
- `versionCode`: **244**.
- Última release: [app-v1.2.43](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.2.43) (ID 393107153).
- APK: [mulletaflix-app-v1.2.43.apk](https://github.com/raphaelpera85/mulletaflix-completo/releases/download/app-v1.2.43/mulletaflix-app-v1.2.43.apk).
- Tamanho confirmado local/remoto: **7.324.749 bytes**.
- SHA-256 confirmado local/remoto: `B2264737FC9B6C2BFC11C0B041CDCC4DAC0E4C9D7A1866015730C2B2C85752A1`.
- Release anterior conferida antes do pacote: [app-v1.2.42](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.2.42) — 7.324.745 bytes, `sha256:fb5e323b82249dd9673df18501f7ddd5e38c0fe5e703b36b07fe543e8666c268`.
- Quality Bar desta rodada: **582 testes unitários, 0 falhas**; **43 testes instrumentados** na AVD `MulletaflixTvApi34` (9 no design-system + 34 nos demais módulos), 0 falhas; `:app:lintDebug` código 0; `:app:assembleRelease` código 0.
- O APK final foi instalado (`Success`) e conferido no dispositivo como `versionCode=244 / versionName=1.2.43`.
- Nenhum emulador permaneceu aberto (`emulator=0`, `qemu=0`).

> Histórico: a v1.2.40 foi a última release antes desta rodada e foi instalada no
> Android TV com `Success`.

## Rodada v1.2.43 — acessibilidade do cartão de mídia e reconhecimento do que NÃO foi corrigido

### O que mudou

O cartão de mídia (`MediaCard`) é o componente mais reutilizado do app (Home,
Biblioteca, Busca, Minha Lista, Detalhes). A árvore de semântica foi inspecionada
**no aparelho** com `printToLog`, e não presumida:

- A capa, os selos e o overlay entravam como nós próprios dentro do nó mesclado,
  além da `contentDescription` que o cartão já define. Os elementos decorativos
  agora são `invisibleToUser()`, então o cartão se anuncia uma única vez.
- O selo `AO VIVO` era concatenado em texto cru na descrição; permanece apenas na
  forma falada (`ao vivo`), que é o que o leitor de tela deve dizer.
- O ícone de favorito tinha `contentDescription` definido duas vezes no mesmo nó
  (parâmetro **e** bloco `semantics`); unificado em um só lugar.

### Correção que tentei, medi e reverti

Havia uma segunda suspeita: a capa tinha `contentDescription = title` enquanto o
cartão já anuncia `Abrir <título>`. Troquei por `null` — e o teste que escrevi
para provar a duplicação **passou nos dois estados**, ou seja, não provava nada.
Medindo a árvore real no aparelho, a verdade apareceu:

```
ContentDescription = '[Abrir Filme único, assistido, na Minha Lista, 4K]'
Text = '[Filme único, 4K, Filme único]'
```

A duplicação real está no **fallback de capa quebrada**, que desenha o título
dentro da área da imagem, e no rótulo visível embaixo. Tentei
`invisibleToUser()` no fallback e **medi de novo**: continuou 2. A propriedade
`Text` mesclada não é afetada por `invisibleToUser()`, então reverti a mudança
ineficaz em vez de deixar código que não faz o que promete.

O comportamento atual está documentado por um teste
(`mediaCardFallbackRepeatsTheTitleInTheMergedTextProperty`) que afirma **2** e
diz explicitamente que, se um dia passar com 1, o teste pode ser apagado. Remover
a repetição exige mudança visual (parar de desenhar o título sobre a arte) e é
decisão de produto.

### Lição registrada

Teste que passa com e sem a correção **não é evidência**. Dois dos meus testes
desta rodada estavam nesse estado; só medi-los no aparelho revelou o que era
real. Antes de afirmar um defeito de acessibilidade, inspecionar a árvore de
semântica com `printToLog` + `adb logcat`.

### Casting/espelhamento: avaliado e NÃO implementado

Investigação do estado atual:

- O app inicializa o Cast (`CastContext.getSharedInstance`) e o `PlayerViewModel`
  observa sessões (`SessionManagerListener`), alimentando `isCasting`.
- O botão em uso é o `MediaRouteButton` **do Media3** (`androidx.media3.cast`),
  que é um composable autocontido — não precisa de
  `CastButtonFactory.setUpMediaRouteButton`.
- **O que falta:** nada envia mídia para a sessão. Não existe `RemoteMediaClient`
  em lugar nenhum, então estabelecer uma sessão não reproduz nada. O receptor é o
  `DEFAULT_MEDIA_RECEIVER_APPLICATION_ID`.
- **Por que não implementei:** o receptor padrão carrega a URL de mídia a partir
  do Chromecast, e o servidor MulletaFlix está numa rede privada. Fazer isso
  funcionar exige receptor próprio, token de acesso e teste com hardware real —
  que não existe nesta máquina. Implementar sem poder testar contraria a regra do
  handoff ("não chamar de concluído sem teste real").

## Rodada v1.2.42 — bugs confirmados por auditoria, contraste de texto secundário e guarda de logs

Esta rodada começou por uma auditoria independente dos itens P1 do backlog. Quatro
auditorias (paginação, refresh/polling, logs e deep link/compartilhamento) foram
executadas em paralelo, somente leitura, e cada achado abaixo foi confirmado por
leitura de código **e** por teste que reprova sem a correção.

### Bugs corrigidos

| Bug | Onde | Efeito observável |
|---|---|---|
| Refresh de canais invalidava o guia em voo sem limpar `isLoadingGuide` | `LiveTvViewModel.refresh()` | Spinner eterno no guia EPG e botão "Guia EPG" desabilitado até reiniciar o app |
| `serverId` do deep link nunca era lido | `MediaDeepLink.kt` | Link de outro servidor abria o item no servidor errado |
| Segmento de rota `web` lido como id de mídia | `MediaDeepLink.kt` | `…/web` abria `detail/web` com "Erro ao carregar detalhes" |
| Deep link entregue por id, não por requisição | `MulletaFlixNavHost.kt` | Abrir o mesmo link duas vezes não navegava na segunda |
| Link pendente nunca era limpo | `MainActivity.kt` | Sair e entrar de novo reabria o detalhe antigo |
| Endereço privado vazava para o link compartilhado | `ShareItemContent.kt` | Link inútil fora de casa (estado normal: sessão em IP de LAN) |
| Link compartilhado sem `serverId` | `ShareItemContent.kt` | Destinatário resolvia o id na própria biblioteca |
| Página vazia com total maior mantinha `hasMore` | `LibraryViewModel`, `FavoritesViewModel` | Sentinela carregando para sempre e nova requisição no mesmo offset |
| Troca de biblioteca preservava o catálogo anterior | `LibraryViewModel.loadLibrary` | Se a 1ª página da nova biblioteca falhasse, a próxima era pedida em offset que pulava os primeiros itens |
| Polling em segundo plano | `SettingsScreen` (30 s), `SyncPlayScreen` (5 s) | Requisições continuavam com a tela invisível |

Cada correção tem teste que **reprova sem ela**:

- `LiveTvViewModelTest`: revertendo as duas linhas do fix, a suíte fica com **16 testes e 1 falha** (`refresh() must not leave the guide flag stuck while a request is in flight`); com o fix, 11/11 verdes.
- `MediaDeepLinkTest`: `…/web`, `…/web/details` e `…/web/item` agora retornam nulo; `serverId` é lido do query e do fragmento.
- `DeepLinkNavigationPolicyTest`: mesma requisição não é entregue duas vezes; requisições com sequências diferentes para o mesmo id **são**.
- `ShareItemContentTest`: 192.168/10/172.16-31/169.254 viram o endpoint público; `serverId` presente; cada tipo de mídia usa o próprio id.
- `LibraryPaginationPolicyTest` e `LibraryViewModelTest`: página vazia encerra a paginação; troca de biblioteca descarta o catálogo anterior.
- `ApiLayerLoggingGuardTest`: injetando um `println` em `AuthInterceptor.kt`, a suíte fica com **2 testes e 1 falha**.

### Contraste: segunda passada

A primeira rodada corrigiu o **acento**. Esta corrige o **texto secundário** e os
**limites de componente**, usando números medidos pela própria implementação
(`contrastRatio`) em vez de estimativa — a estimativa manual inicial estava
errada e o próprio teste a desmentiu.

- Só os alphas que reprovam são elevados: branco 0,4 (3,83:1), `onSurface` 0,4
  (3,21:1) e `onSurface` 0,5 (4,29:1). Branco 0,5 (5,34:1) e tudo em 0,6+ já
  passavam e foram **preservados**.
- `DarkOutline` foi de `#424242` (1,83:1) para `#808080`, atendendo o mínimo de
  3:1 do SC 1.4.11 para a borda de campos de texto. Confirmado no pixel
  renderizado da TV: `#808080` sobre `#141414` = **4,66:1**.
- `outlineVariant` continua reservado para divisórias decorativas.

### Auditoria de logs: sem vazamento

195 arquivos de produção, **zero** chamadas de log (`Log.*`, `println`,
`Timber`, `printStackTrace`, `System.out/err`), confirmado por duas ferramentas
independentes. `HttpLoggingInterceptor` está em `Level.NONE` **incondicional**
(não depende de `BuildConfig.DEBUG`), o que importa porque URLs de imagem
carregam `api_key` e o Quick Connect carrega o secret na query string. A guarda
automatizada existe para que isso não regrida em um commit.

### Achado NÃO corrigido (decisão do usuário)

`app/build.gradle.kts`: quando `KEYSTORE_PATH` e as demais variáveis de ambiente
de assinatura não estão definidas, a build de **release** cai silenciosamente
para a chave de depuração (`initWith(getByName("debug"))`). Isso permite publicar
uma release assinada com chave debug — todas as releases até a v1.2.42 estão
nessa situação. Trocar isso para falhar o build interromperia o fluxo atual de
release; a decisão é do usuário.

### Auditorias sem achado (para não repetir)

- **Paginação por offset**: o defeito de "offset avança pelo limite pedido" **não
  existe**; foi corrigido no commit `e963a541` e o offset vem de
  `_state.value.items.size`.
- **Refresh automático duplicado**: `repeatOnLifecycle(RESUMED)` cancela e espera
  o bloco antes de relançar, e todo timer passa por `refreshIfIdle` com guarda
  síncrona. O único job redundante é benigno (mata antes de qualquer HTTP).
- **Deep link não é perdido no login nem ignorado no cold start**; temporada e
  episódio compartilham o próprio id, não o da série.

### O que continua NÃO validado

- Login com conta real, Quick Connect autorizado, seleção de usuário e logout:
  exigem credencial ou autorização do PIN no servidor.
- Descoberta LAN preferindo o endereço local: sem sessão o app para no login.
- Reprodução real, capas, faixas de áudio/legenda e casting.
- TalkBack e tamanhos de toque.
- Casting/espelhamento continua não implementado (item P1 do backlog).

## Rodada v1.2.41 — acessibilidade de contraste + verificação instrumentada

### Defeito encontrado e corrigido

Medido na tela real de login renderizada na Android TV (`adb exec-out screencap`) e
confirmado pelos valores de tema: o vermelho vívido da marca `#E50914` era usado
como **texto** sobre as superfícies escuras e não atingia o mínimo AA.

| Par vermelho/texto | Contraste medido | Mínimo WCAG 2.2 AA |
|---|---:|---:|
| `#E50914` sobre `#141414` (surface) | 3,84:1 | 4,5:1 |
| `#E50914` sobre `#080808` (background) | 4,18:1 | 4,5:1 |
| `#E50914` sobre `#1F1F1F` (Netflix surface) | 3,44:1 | 4,5:1 |
| `#9C27B0` sobre `#1D1028` (Purple Haze) | 2,88:1 | 4,5:1 |
| `#1565C0` sobre `#0D1628` (Blue Radiance) | 3,14:1 | 4,5:1 |

Correção aplicada: o vermelho passa a ter dois papéis.

- `MulletaFlixRed` (`#E50914`) continua em preenchimentos, anel de foco, indicador
  de aba e arte — onde o texto branco por cima mede 4,79:1 e passa.
- O acento do tema (`colorScheme.secondary`) passa a ser calculado por
  `accessibleAccent(...)`, que eleva o vermelho de cada tema até 4,5:1 usando a
  superfície mais desfavorável (`surface`, `background`, `surfaceVariant`).
- O tema Light troca `primary` para `MulletaFlixRedDark` (`#B20710`) porque o
  rótulo branco sobre o vermelho vívido media 4,40:1.
- Só o `LightColorScheme` manteve o `secondary` padrão do Material 3 (o teste
  `every theme accent is readable on its own surfaces after lifting` confirma que
  ele já é legível nas três superfícies claras); nos demais temas o acento foi
  derivado a partir do vermelho da marca.

Evidência automatizada (`:design-system:testDebugUnitTest` e teste instrumentado
de pixel em `design-system/src/androidTest`):

- `BrandColorContrastTest` mede o contraste WCAG 2.2 de todos os temas antes de
  aceitar o acento. Ele **reprovou duas vezes** durante esta rodada e obrigou
  duas correções adicionais: calibrar pelo `surfaceVariant` (o acento media
  4,21:1 nele) e corrigir o `primary` do tema Light (4,40:1).
- `AccessibleAccentRenderTest` renderiza o tema no dispositivo, captura o pixel
  do acento e mede o contraste do que foi realmente desenhado. Ele afirma o
  **contraste medido** (`>= 4,5:1`) e não um hexadecimal fixo, porque o
  emulador compõe a cor (`#FF3333` chega ao framebuffer como `#FF4545`, que
  mede 5,85:1). O segundo teste prova que um preenchimento continua sendo o
  vermelho vívido e não o vermelho de texto.

### Verificação instrumentada contra o servidor real

Executada na AVD `MulletaflixTvApi34` (porta 5556) com o servidor ativo em
`192.168.15.9:8096`: **39 testes instrumentados, 0 falhas, 0 erros**, distribuídos
em `app` (1), `design-system` (3), `feature:auth` (2), `feature:downloads` (6),
`feature:home` (3), `feature:item-detail` (4), `feature:library` (6),
`feature:player` (10), `feature:search` (3) e `feature:settings` (1).

Também confirmado visualmente na TV: o app abre direto no login com o servidor
já reconhecido, logo octogonal não recortado em círculo pelo launcher, wordmark
com `MULLETA` vermelho e `FLIX` branco, fundo preto e detalhes vermelhos.
O emulador foi encerrado ao final (0 processos `emulator`/`qemu-system-x86_64`).

### O que esta rodada NÃO validou

- Login com conta real, Quick Connect autorizado, seleção de usuário e logout:
  exigem credencial ou autorização do PIN no servidor e **não foram executados**.
- Descoberta LAN preferindo o endereço local: sem sessão autenticada o app para
  na tela de login, então a preferência LAN → DuckDNS não pôde ser exercitada.
- Reprodução real, capas, faixas de áudio/legenda e casting: não executados.
- TalkBack e tamanhos de toque: pendentes; apenas contraste foi tratado.

### Armadilha registrada: o APK não é reproduzível byte a byte

Dois `assembleRelease` do mesmo código-fonte geraram tamanho idêntico
(7.324.749 bytes) mas SHA-256 diferente:
`A10392DC…3B6CA` antes e `7EF34D8E…D0A2F` depois de um `--rerun-tasks`. O carimbo
de assinatura muda a cada build. Portanto:

- Sempre medir o digest do APK que será publicado, nunca reutilizar o digest de
  uma build anterior do mesmo código.
- Instalar e exercitar exatamente o arquivo que foi para `dist/`, e só então
  publicar. A release v1.2.41 foi publicada com o arquivo cujo digest remoto
  confere com o local.

## Regras que o próximo agente deve respeitar

1. Tratar esta conversa como APK-only. Não executar nem publicar servidor.
2. Não executar `build-update-package.ps1` nem `publish-release.ps1`.
3. Só usar `build-app-package.ps1` e `publish-app-release.ps1` para releases do APK.
4. Antes de cada nova release, consultar a release anterior no GitHub e registrar tamanho e digest.
5. Não apagar, resetar ou formatar alterações existentes. O worktree contém alterações de servidor e web pertencentes ao usuário.
6. Não incluir credenciais em código, commits, logs, documentação ou mensagens.
7. A URL remota padrão é `http://mulletaflix.duckdns.org:8096`; na mesma LAN a descoberta deve preferir o endereço local automaticamente.
8. Emuladores devem ser iniciados somente durante o teste e encerrados ao final pelo wrapper.
9. A versão segue SemVer: depois de `1.2.99`, usar `1.3.0`; nunca criar `1.2.100`.
10. Cada tarefa de código deve seguir Builder → testes/lint → Evaluator/Gauntlet → release APK, quando concluída.
11. Existe um processo externo que faz `git commit` automático no repositório. Durante a rodada v1.2.41 ele criou o commit `fbe35724 chore/feat: implement IntroSkipper database maintenance and Android theme enhancements`, que incluiu as alterações do APK **e** alterações de servidor que já estavam no worktree. Verifique o estado do repositório antes e depois de trabalhar; `main` ficou 1 commit à frente de `origin/main` e **nada foi enviado ao remoto** por esta sessão.

## Backlog priorizado

### P0 — validar antes de considerar o APK pronto para uso real

- [ ] Executar smoke test instrumentado no servidor ativo com a conta de teste, sem registrar a senha em arquivos.
  - Parcial: a suíte instrumentada existente (**39 testes** em `app`, `design-system`, `auth`, `downloads`, `home`, `item-detail`, `library`, `player`, `search` e `settings`) foi executada com o servidor ativo em `192.168.15.9:8096` e passou 39/39. Ela cobre boot, branding e telas sem sessão; **o login com a conta de teste continua pendente** porque exige a credencial.
  - Comando usado:
    ```powershell
    .\gradlew.bat :design-system:connectedDebugAndroidTest :feature:auth:connectedDebugAndroidTest `
      :feature:home:connectedDebugAndroidTest :feature:library:connectedDebugAndroidTest `
      :feature:item-detail:connectedDebugAndroidTest :feature:player:connectedDebugAndroidTest `
      :feature:search:connectedDebugAndroidTest :feature:settings:connectedDebugAndroidTest `
      :feature:downloads:connectedDebugAndroidTest :app:connectedDebugAndroidTest --no-daemon --no-parallel --console=plain
    ```
- [ ] Validar login tradicional, botão **Criar conta**, seleção de usuário e logout em celular, tablet e Android TV.
- [ ] Validar Quick Connect contra o servidor real: gerar PIN, autorizar, autenticar, expirar e trocar de servidor.
- [ ] Validar descoberta LAN real: servidor na mesma rede deve ser escolhido automaticamente; fora da LAN deve cair para DuckDNS.
- [ ] Executar reprodução de um filme e de um episódio reais, incluindo retomada, pausa, seek, retry e retorno de rede.
- [ ] Validar seleção de áudio e legendas no player, inclusive preferência persistida e fallback quando o servidor só informa faixas na resposta de reprodução.
- [ ] Validar capas/posters em celular, tablet e TV com mídias reais; confirmar que posters verticais são exibidos inteiros e que a grade da TV mostra vários títulos por linha.
- [ ] Validar atualização automática da Home, Biblioteca, Minha Lista e TV ao vivo na Android TV ao abrir, voltar do background e aguardar o intervalo.
- [ ] Executar testes instrumentados reais de foco remoto, navegação D-pad, scroll vertical/horizontal e ativação por Enter na TV.

### P1 — bugs/lacunas prováveis a investigar

- [ ] Investigar qualquer divergência entre o estado `isAuthenticated` do Quick Connect e a sessão persistida após reinício do processo.
- [ ] Confirmar que a troca entre endpoint LAN e DuckDNS atualiza imagens, playback URLs, deep links e tokens sem cache do servidor anterior.
- [ ] Confirmar que falhas de imagem mostram fallback acessível sem quebrar o scroll ou a ativação do card.
- [ ] Confirmar que paginação da Biblioteca não duplica nem pula itens quando o servidor retorna uma página menor que o limite.
- [ ] Confirmar que refresh manual não cancela uma resposta lenta; refresh automático deve continuar não destrutivo.
- [ ] Verificar que a atualização automática não cria jobs duplicados ao alternar rapidamente entre telas/background/foreground.
- [ ] Verificar permissões e comportamento real de descoberta em Wi‑Fi, Ethernet, VPN e Android TV sem Wi‑Fi.
- [ ] Fazer auditoria de logs: URLs, tokens, PINs e credenciais nunca podem aparecer em logs de produção.

### P1 — funcionalidades ainda incompletas ou que precisam de decisão técnica

- [ ] Implementar e testar espelhamento/casting para dispositivos compatíveis. Avaliar separadamente Google Cast/Media3 Cast, Android MediaProjection e reprodução remota compatível com o servidor; não chamar de concluído sem teste real.
- [ ] Validar deep link oficial `/web/#/details?id=...&serverId=...` com endpoint público, sem gerar `localhost`.
- [ ] Validar compartilhamento de filme, série, temporada e episódio com título, capa e metadados corretos.
- [x] ~~Confirmar ícone não redondo seguindo o contorno externo do logo correto e wordmark com `MULLETA` vermelho e `FLIX` branco.~~ Confirmado no APK v1.2.41 instalado na TV: o launcher não recorta o logo octogonal em círculo (o manifesto usa `@drawable/ic_mulletaflix_logo`, não o `mipmap-anydpi-v26`), o wordmark renderiza `MULLETA` em `#E50914` e `FLIX` em branco sobre fundo preto. Ver `artifacts/tv-v1.2.41-final.png`.
- [x] ~~Confirmar tema preto predominante com detalhes vermelhos em login, cards, foco remoto, player, estados de erro e telas vazias.~~ Confirmado no login renderizado na TV. Em v1.2.41 o contraste do vermelho como texto foi corrigido (ver seção da rodada).
- [ ] Testar atualização do APK a partir do Centro de Atualizações e confirmar que uma versão dispensada não reaparece durante a sessão.
- [ ] Verificar suporte de acessibilidade: TalkBack, content descriptions, foco visível e tamanhos de toque.
- [x] ~~Verificar suporte de acessibilidade: contraste.~~ Contraste AA tratado e coberto por testes unitários e por teste instrumentado de pixel. **Pendências remanescentes:** rótulos secundários com `Color.White.copy(alpha = 0.4f/0.5f)` e `onSurface.copy(alpha = 0.4f/0.5f)` medem entre 3,1:1 e 3,5:1 sobre as superfícies escuras (ex.: `ProfileScreen.kt`, `ServerSelectionScreen.kt`) e ainda precisam de tratamento; `outline` (`#424242`) tem 1,83:1 sobre a surface e reprova o mínimo de 3:1 para limites de componente.

### P2 — melhorias de produto e qualidade

- [ ] Criar matriz de compatibilidade: telefone, tablet, Android TV/Box, resolução, orientação e controle remoto.
- [ ] Adicionar testes Compose instrumentados de Home, Biblioteca, Player, Login, Quick Connect e TV ao vivo nos três perfis.
- [ ] Adicionar teste de processo/recriação para preservar servidor selecionado, preferências de ordenação, idioma e sessão.
- [ ] Adicionar teste de rede intermitente durante login, descoberta, carregamento de capas e playback.
- [ ] Medir tempo até conteúdo utilizável e memória de listas/Coil em bibliotecas grandes.
- [ ] Auditar dependências Android/Media3/Compose e atualizar somente com testes e release reproduzível.
- [ ] Documentar uma matriz de resultados por AVD, versão Android, tamanho do APK e SHA-256.

## Emuladores disponíveis

O wrapper está em `MulletaFlix-android/tools/with-emulator.ps1` e encerra somente o emulador que ele iniciou.

| Perfil | AVD | Porta |
|---|---|---:|
| Celular | `MulletaflixApi35` | 5554 |
| Android TV | `MulletaflixTvApi34` | 5556 |
| Tablet | `MulletaflixTabletApi35` | 5558 |

Exemplo de instalação na TV:

```powershell
powershell -ExecutionPolicy Bypass -File .\MulletaFlix-android\tools\with-emulator.ps1 `
  -AvdName MulletaflixTvApi34 -Port 5556 `
  C:\Android\Sdk\platform-tools\adb.exe '-s' 'emulator-5556' 'install' '-r' `
  'D:\Users\Raphael\Documents\Projetos\mulletaflix\dist\mulletaflix-app-v<VERSAO>.apk'
```

## Fluxo de desenvolvimento e release — somente APK

Substitua `<VERSAO>` pela próxima versão. Atualmente a próxima versão é **1.2.41**, com `versionCode` esperado **242**.

### 1. Inspecionar antes de editar

```powershell
git status --short
Get-Content .\MulletaFlix-android\gradle\libs.versions.toml | Select-String 'appVersion'
Get-Content .\MulletaFlix-android\app\build.gradle.kts | Select-String 'versionCode|versionName'
```

Se houver alterações não relacionadas, preservá-las. Não usar `git reset --hard` ou `git checkout --`.

### 2. Registrar a release anterior

```powershell
$release = Invoke-RestMethod -Headers @{Accept='application/vnd.github+json'} `
  -Uri 'https://api.github.com/repos/raphaelpera85/mulletaflix-completo/releases/tags/app-v1.2.40'
$asset = $release.assets | Where-Object name -eq 'mulletaflix-app-v1.2.40.apk'
[pscustomobject]@{tag=$release.tag_name; size=$asset.size; digest=$asset.digest} |
  ConvertTo-Json -Compress
```

Só continuar quando a release anterior estiver disponível e o tamanho/digest forem registrados.

### 3. Quality Bar

Executar no diretório `MulletaFlix-android`:

```powershell
.\gradlew.bat testDebugUnitTest --no-daemon --no-parallel --console=plain
.\gradlew.bat :app:lintDebug --no-daemon --no-parallel --console=plain
.\gradlew.bat :app:assembleRelease --no-daemon --no-parallel --console=plain
```

Se um comando falhar, corrigir e repetir. Não publicar APK baseado em build parcial.

### 4. Gerar e instalar o pacote

Na raiz do repositório:

```powershell
.\build-app-package.ps1 -Version <VERSAO> -SkipBuild
```

Instalar em pelo menos o dispositivo afetado. Para UI responsiva, repetir no celular, tablet e TV quando a mudança atingir layout/navegação.

### 5. Publicar somente o APK

```powershell
.\publish-app-release.ps1 -Version <VERSAO>
```

Não executar estes comandos nesta conversa:

```text
build-update-package.ps1
publish-release.ps1
```

### 6. Confirmar o artefato remoto

```powershell
$release = Invoke-RestMethod -Headers @{Accept='application/vnd.github+json'} `
  -Uri 'https://api.github.com/repos/raphaelpera85/mulletaflix-completo/releases/tags/app-v<VERSAO>'
$asset = $release.assets | Where-Object name -eq 'mulletaflix-app-v<VERSAO>.apk'
$localPath = 'dist\mulletaflix-app-v<VERSAO>.apk'
$local = Get-FileHash $localPath -Algorithm SHA256
$emulators = @(Get-Process -Name emulator -ErrorAction SilentlyContinue).Count
[pscustomobject]@{
  tag = $release.tag_name
  remoteSize = $asset.size
  remoteDigest = $asset.digest
  localSize = (Get-Item $localPath).Length
  localSha = $local.Hash
  emulatorProcesses = $emulators
} | ConvertTo-Json -Compress
```

Aceitar somente se tamanho/hash local e remoto coincidirem e `emulatorProcesses` for zero.

### 7. Checklist de encerramento

- [ ] Testes relevantes passaram com código de saída 0.
- [ ] Lint passou com código de saída 0.
- [ ] APK release foi instalado e exercitado no dispositivo afetado.
- [ ] Versão anterior foi conferida antes do pacote.
- [ ] APK foi publicado na tag `app-v<VERSAO>`.
- [ ] Digest remoto coincide com o hash local.
- [ ] Nenhum emulador ficou aberto.
- [ ] `git diff --check` passou; avisos de conversão LF/CRLF podem ser registrados, mas não ignorar erros reais.
- [ ] Nenhum script de release do servidor foi executado.

## Formato recomendado para o próximo relatório

1. Resultado e versão do APK.
2. Alterações realizadas com caminhos de arquivos.
3. Testes executados e códigos de saída.
4. Dispositivos/AVDs usados e resultado visual/funcional.
5. Tamanho e SHA-256 local/remoto.
6. Link da release e do APK.
7. Pendências que continuam abertas.
8. Se aplicável, incluir as linhas Fable `INTENT:`, `TWINS:` e `AUTH:`.

Nunca afirmar que uma funcionalidade foi testada se apenas compilou; diferenciar claramente “teste automatizado”, “teste em emulador” e “teste contra servidor real”.
