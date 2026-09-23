# MulletaFlix Android — handoff para continuidade com DeepSeek

Atualizado em: 23/09/2026
Escopo principal desta conversa: **APK Android**. O `AGENTS.md` atual do repositório
também exige a geração/publicação da release do servidor ao concluir qualquer alteração;
essa exigência foi aplicada nesta rodada.

> Nota desta rodada: as evidências de build e publicação foram coletadas em 23/09/2026.
> O servidor permanece na versão publicada `12.0.76`; a release Android nesta rodada é a versão `1.2.94`.

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
5. **Releases separadas por sistema**: servidor e APK continuam tendo tags e assets
   independentes, mas o `AGENTS.md` atual exige executar ambos os fluxos quando uma
   alteração é concluída.

### Ordem operacional recomendada

```text
Fable: intenção/aceite
  → Caveman: inspeção curta e segura
  → Builder: implementação/testes
  → Cavecrew: revisão especializada quando necessário
  → Fable Judge + Gauntlet Evaluator: testes reais/lint/build/UI
  → Caveman Compress/Learn: registrar evidências e aprendizado
  → pacote/publicação do servidor e do APK em releases separadas
```

## Estado confirmado

- Versão atual do APK (local e publicada): **1.2.94**. `versionCode`: **295**.
- **Release Android publicada: [app-v1.2.94](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.2.94)** — 7.336.679 bytes, sha256 `9E97245CFC7D3C58D162F35811A86C0E780DF0EA85C9F490BD00450B7D73A98C`.
- **Release do servidor confirmada: [v12.0.76](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/v12.0.76)** — API do GitHub confirmou `mulletaflix-update-win-x64.zip` 370.545.524 bytes e `mulletaflix_12.0.76_windows-x64.exe` 405.477.990 bytes; SHA-256 local respectivamente `BC111F80E42B1CD7E47B1E864977E27C20CC50D50ACE15097887000A1FF85D40` e `34DE942FB72AC884696D38459683E7F6A0382CAAEA61D45CD2373591971652DE`.
- **Atenção (v1.2.81 … v1.2.85): cinco builds com o MESMO tamanho** — 7.320.299 bytes em v1.2.81, v1.2.82, v1.2.84 e v1.2.85, com quatro digests diferentes. A v1.2.86 finalmente mudou de tamanho (7.336.683), o que é a prova barata de que o número não serve para identificar build — só SHA-256 + `versionCode`.
- Quality Bar desta rodada: `:app:connectedDebugAndroidTest` executou **13 testes instrumentados aprovados** no AVD `MulletaflixTvApi34`; o opt-in explícito da API experimental do Coil removeu o aviso do teste de cache; `:app:assembleRelease` passou com código 0 (662 tarefas); o APK v1.2.94 foi instalado e aberto no mesmo ciclo do wrapper, com `am start` sem erro; `publish-app-release.ps1` anexou o APK; emuladores encerrados (`emulator=0`, `qemu=0`). O servidor não foi alterado nem publicado nesta conversa APK-only; o build do servidor segue bloqueado pelo erro existente em `ShortMaxSeriesProvider.cs` (`GetProviderId`/`SetProviderId` ausentes).
- **Alvos de toque: a lista está fechada.** O último item (o `MediaRouteButton` de 40 dp) foi **medido** na v1.2.84 e **não tinha defeito**: alvo de toque 48,0 dp × 48,0 dp, layout 40,0 dp. Ver a rodada — inclusive o erro de método que quase transformou isso num conserto desnecessário.
- **Método que passou a valer (v1.2.83):** revisar o próprio trabalho é a forma mais fraca de verificação. Depois de duas rodadas seguidas de mudanças minhas, uma **auditoria adversarial independente** (subagente, read-only, instruído a procurar defeito e não elogiar) comparou as mudanças com `HEAD` e achou **dois defeitos reais que eu tinha acabado de introduzir** — ambos corrigidos na v1.2.83. Repetir esse passo depois de qualquer bloco de mudanças próprias: ele também **refutou** duas hipóteses minhas (o que é tão útil quanto achar defeito) e deixou dois suspeitos registrados, em vez de "corrigidos às cegas".
- **Prova de que o cache morto saiu do pacote (medida, não deduzida):** varredura dos `classes*.dex` dentro dos dois APKs — na v1.2.80 as strings `androidx/room/RoomDatabase`, `mulletaflix.db` e `MediaItemEntity` aparecem **1, 1 e 3 vezes**; na v1.2.81 aparecem **0, 0 e 0**. O APK encolheu de 7.357.517 para 7.320.299 bytes (**−37.218**).
- **ATENÇÃO — releases:** o `AGENTS.md` atual exige, para toda alteração concluída, `build-update-package.ps1` sem `-SkipInstaller`, `build-app-package.ps1`, `publish-release.ps1` e `publish-app-release.ps1`, com conferência da API do GitHub. O handoff antigo registrava APK-only e cadência de 10 versões; essa anotação está supersedida pelas regras atuais do repositório.
- **Onde as coisas moram agora:** cores, faixa de tamanho e matemática de legenda em `:domain` (`SubtitleStylePolicy`); regras de paginação em `:domain/paging/PagingPolicy`; política de qualidade em `:domain/model/PlaybackQuality.kt`; política de URL de mídia em `:design-system/media/MediaImageUrl.kt` (`resolveMediaUrl`, `redactToken`, `retargetMediaUrl`, `canonicalImageCacheKey` — a regra de "o que é um token" tem **uma** definição); decisão de repontar o player em `:feature:player/StreamRetargetPolicy.kt` (`shouldRetargetPreparedStream`, `retargetPreparedStreamUrl`) e a ordem dos passos em `:feature:player/PreparedStreamRetarget.kt`; chave de cache do Coil em `artworkCacheKeyInterceptor` (`MulletaFlixApp.kt`); pedido de download em `downloadRequestFor` (`Media3DownloadRepository.kt`); mapeamento de tema em `staticColorSchemeFor` (`MulletaFlixTheme.kt`) — é ele que o `ThemeVariantsTest` lê, de propósito; catálogos dos diálogos de Ajustes e a lista rolável em `SettingsOptionLists.kt` / `ChoiceDialogOptions` (`SettingsScreen.kt`); o que a busca devolve e se ela está truncada em `:domain/repository/SearchRepository.kt` (`SearchResults`) e em `:feature:search/SearchTruncationNotice.kt` (`searchTruncationNotice`), com a frase desenhada por `SearchTruncationBanner` (`SearchScreen.kt`).
- **Atenção 17:** substituição de texto por script sobre fonte Kotlin **quebra em silêncio** quando o trecho tem interpolação ou quebra de linha. Na v1.2.77, restaurar uma reversão com `[System.IO.File]::ReadAllText` + `.Replace` deixou **duas** cópias de um comentário no `DownloadsScreen.kt` e **apagou** a linha `modifier = Modifier.semantics { selected = … }` do `LibraryScreen.kt` (o `old_string` tinha crases de interpolação e o PowerShell as interpretou). Nos dois casos a compilação passou — só a releitura do trecho pegou. **Depois de qualquer substituição por script, leia o trecho de volta**; ou use a ferramenta de edição, que falha quando o alvo não casa. **Atenção 17b (v1.2.80):** a ferramenta de edição também cola linhas se o `old_string` terminar em quebra de linha e o `new_string` não — `) {` seguido de `    AlertDialog(` virou `) {    AlertDialog(` no `SearchScreen.kt`. A compilação passou de novo; só a releitura pegou. Regra prática: incluir a linha seguinte (ou não incluir a quebra) quando o alvo é o fim de uma assinatura.
- **Atenção 16:** nome de teste com crase (`` fun `frase com espaços`() ``) funciona em teste **unitário** e **quebra o build** de teste instrumentado: o D8 recusa `Space characters in SimpleName ... are not allowed prior to DEX version 040` (o `minSdk` 24 fixa uma versão de DEX anterior à 040). Em `src/androidTest` use `camelCase`, como os testes que já existiam.
- **Atenção 15:** o `build-app-package.ps1` pode falhar com `FileSystemException: ...classes.jar: O arquivo já está sendo usado por outro processo` logo depois de outra tarefa Gradle (o lint, por exemplo). É lock de arquivo do Windows, não erro de código: repetir o comando resolve — aconteceu na v1.2.71 e passou na segunda tentativa sem mudança alguma. Na v1.2.72 não aconteceu.
- **Atenção 14:** `java.time` **não** está disponível abaixo da API 26 neste projeto (minSdk 24, sem core library desugaring). Formatação de data usa `SimpleDateFormat` com `TimeZone` explícito.
- **Atenção 11 (resolvida na v1.2.92):** `@ApplicationContext` na linha 33 de `Media3DownloadRepository.kt` gerava um aviso do Kotlin 2.3 sobre alvo da anotação (`KT-73255`); o parâmetro agora usa explicitamente `@param:ApplicationContext`.
- **Atenção 12:** o lint é um portão real, não decorativo: ele reprovou a v1.2.68 com 3 erros de `UnsafeOptInUsageError` enquanto o `assembleRelease` passava. Sempre rodar `:app:lintDebug` antes de publicar.
- **Atenção 13:** não usar `Get-Content`/`Set-Content` para editar fontes Kotlin: o `-replace` do PowerShell opera sobre o array de linhas e o `Set-Content -NoNewline` junta tudo numa linha só. Aconteceu nesta rodada com um arquivo de teste; foi reescrito. Editar com as ferramentas de edição.
- **Atenção 9:** `logcat -b crash` no AVD de TV contém um crash **do YouTube TV** (`com.google.android.youtube.tv`), não do app. Filtrar por `org.mulletaflix` antes de tratar um crash como regressão.
- **Atenção 10:** `$env:ANDROID_HOME` está **vazio** nesta máquina; o `adb` fica em `C:\Android\Sdk\platform-tools\adb.exe`. Dentro de um comando aninhado do wrapper, variável de ambiente não chega — passe o caminho absoluto.
- Screenshots das builds publicadas: `artifacts/v1.2.57-tv.png` (TV), `artifacts/v1.2.60-phone.png` e `artifacts/v1.2.61-phone.png` (celular).
- **Atenção 3:** rodar `connectedDebugAndroidTest` de **10 módulos de uma vez** falha com "Execution failed for task" em todos, sem nenhum teste executado — não é defeito de teste. Rodar em lotes (1 módulo por vez, ou até 8) funciona. Não gastar tempo investigando a falha do lote grande.
- **Atenção 4:** a tarefa de lint dos módulos (`:feature:auth:lintAnalyzeDebugUnitTest` e `:feature:auth:lintAnalyzeDebugAndroidTest`) falhou uma vez durante a Quality Bar completa e passou nas execuções seguintes sem nenhuma mudança. Tratar como transitória: repetir antes de investigar.
- **Atenção 5:** a densidade do AVD de TV é 2,0 e a do AVD de celular é 2,625 (420 dpi). Medições em pixels **não** são comparáveis entre os dois; converter para dp antes de concluir qualquer coisa.
- **Atenção 6:** `onNodeWithTag` não enxerga nós dentro de um `IconButton` na árvore mesclada — é preciso `useUnmergedTree = true`. Custou duas execuções descobrir.
- **Atenção 7:** em aparelho de toque, um nó pode aceitar `requestFocus()` e ainda assim reportar `Focused = false` (modo de toque). Testes de foco do controle remoto precisam se declarar **TV apenas** e pular no celular, senão falham por um motivo que não tem a ver com o componente.
- **Atenção 8:** a Quality Bar completa levou **9m47s** numa execução (lint de todos os módulos incluído). Rodar em background e não bloquear nela.
- Nenhum emulador permaneceu aberto (`emulator=0`, `qemu=0`).
- **Verificação visual por screenshot é possível** (e foi usada): instalar o APK no AVD de TV, `adb shell input keyevent` (20=baixo, 22=direita, 23=OK) e `adb shell screencap -p` + `adb pull`. A tela de login é alcançável **sem sessão**, então é o melhor alvo; as telas logadas não são.
- **Atenção:** o tamanho do APK não identifica a build (cinco builds seguidas saíram com 7.341.133 bytes apesar de conteúdo diferente). Usar sempre o SHA-256 e o `versionCode` do manifesto.
- **Atenção 2:** uma falha de lint alegando `FileNotFoundException` num arquivo gerado pelo KSP (`data/build/generated/ksp/release/...`) é transitória; repetir a tarefa resolve.
- **A API do GitHub limita requisições anônimas.** Conferir o artefato remoto exige chamada autenticada (o `publish-app-release.ps1` já faz).
- **O servidor MulletaFlix voltou ao ar na rodada 7.** A confirmação do 429 nas capas continua pendente: o servidor não expõe usuários públicos (`/Users/Public` responde `[]`) e não há conta sem senha, então não é possível autenticar sem a credencial do usuário.

> Diagnóstico do 429 — evidência indireta que ficou mais forte (rodada 7): no log do
> servidor, **todos** os avisos `Rate limit exceeded for anonymous requests` de
> `192.168.15.10` se concentram em duas janelas (13:12–13:13 e 13:19:37–13:19:38) e
> **nenhum** aparece depois das 13:20, apesar de o servidor seguir ativo às 15:14.
> Isso é consistente com a correção de identidade, mas **não a prova**: o app pode
> simplesmente não ter aberto a biblioteca nesse intervalo, e a versão instalada
> durante aquelas janelas era a v1.2.44 ou anterior. Confirmar com uma sessão real.
>
> Um dado que reforça o diagnóstico: os avisos apareciam enquanto o *catálogo*
> carregava normalmente. Como o `RateLimitMiddleware` só conta requisições com
> `IsAuthenticated == false`, as chamadas de API (que já mandavam o cabeçalho)
> passavam e **só as capas** eram contadas como anônimas — exatamente o que a
> correção da v1.2.44 endereça.

> Histórico: a v1.2.40 foi a última release antes desta rodada e foi instalada no
> Android TV com `Success`.

## Rodada v1.2.94 — Suíte instrumentada do app no Android TV

Vigésima nona rodada. `versionCode` 295. A release Android foi publicada em
[app-v1.2.94](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.2.94).

### O que mudou

- A suíte instrumentada do módulo `app` foi executada no AVD `MulletaflixTvApi34`.
- O teste `ArtworkCacheKeyInterceptorTest` passou a declarar explicitamente o opt-in
  `ExperimentalCoilApi`, eliminando o aviso de API experimental durante a compilação.
- A versão do APK foi atualizada para `1.2.94` (`versionCode` 295).

### Evidências

- `:app:connectedDebugAndroidTest`: **13 testes aprovados** no Android TV.
- `:app:assembleRelease`: **BUILD SUCCESSFUL**, 662 tarefas.
- APK: `dist/mulletaflix-app-v1.2.94.apk`, 7.336.679 bytes, SHA-256
  `9E97245CFC7D3C58D162F35811A86C0E780DF0EA85C9F490BD00450B7D73A98C`.
- A release anterior `app-v1.2.93` foi verificada pela API antes do empacotamento.
- Instalação e `MainActivity` foram executadas no mesmo ciclo do wrapper do AVD, sem erro;
  os emuladores foram encerrados automaticamente.
- Apenas a release do APK foi publicada. Nenhum código ou release do servidor foi alterado.

## Rodada v1.2.93 — Deep link troca para o servidor correto

Vigésima oitava rodada. `versionCode` 294. A release Android foi publicada em
[app-v1.2.93](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.2.93).

### O que mudou

- Links oficiais que contêm `serverId` diferente do servidor autenticado agora encaminham
  automaticamente para a seleção de servidor, em vez de apenas exibir um Toast e descartar
  o link.
- O pedido de deep link permanece pendente durante a troca de servidor e o login; após a
  autenticação, o mesmo item é retomado no servidor correto.
- O fluxo não interfere quando a Activity já está em seleção ou login.
- Adicionados testes unitários para a decisão de redirecionamento.

### Evidências

- `:app:testDebugUnitTest` + `:app:lintDebug`: **BUILD SUCCESSFUL**, 648 tarefas.
- `:app:assembleRelease`: **BUILD SUCCESSFUL**, 662 tarefas.
- APK: `dist/mulletaflix-app-v1.2.93.apk`, 7.336.683 bytes, SHA-256
  `416CE6AB62F092E67573CD5D4C0DDE9304CC130060A74A6784F5F36683196AFF`.
- A release anterior `app-v1.2.92` foi verificada na API antes da publicação.
- No AVD `MulletaflixTvApi34`, instalação e `MainActivity` foram executadas no mesmo
  ciclo do wrapper, sem erro de `am start`; emulador encerrado ao final.
- Nenhuma alteração ou release do servidor foi feita; o bloqueio existente de
  `ShortMaxSeriesProvider.cs` permanece fora do escopo APK-only.

## Rodada v1.2.92 — Compatibilidade futura do Kotlin

Vigésima sétima rodada. `versionCode` 293. A release Android foi publicada em
[app-v1.2.92](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.2.92).

### O que mudou

- `MediaDeepLinkRequest` foi anotado com `ConsistentCopyVisibility`, removendo o aviso
  de visibilidade futura do `copy()` gerado pelo Kotlin.
- `Media3DownloadRepository` agora fixa `@ApplicationContext` no parâmetro, evitando a
  mudança futura de alvo da anotação.
- A versão do APK avançou para `1.2.92` / `versionCode` 293.

### Evidências

- `testDebugUnitTest`: **BUILD SUCCESSFUL**, 456 tarefas.
- `:app:testDebugUnitTest :app:lintDebug`: **BUILD SUCCESSFUL**, 648 tarefas.
- `:app:assembleRelease`: **BUILD SUCCESSFUL**, 662 tarefas.
- APK: `dist/mulletaflix-app-v1.2.92.apk`, 7.336.683 bytes, SHA-256
  `0EEF5B978515A4C76333419BE4849E67E161D3FE938D2498A6A6B62B133DCA20`.
- A release anterior `app-v1.2.91` foi verificada na API antes da publicação; a nova
  release foi confirmada com o asset `mulletaflix-app-v1.2.92.apk`.
- No AVD `MulletaflixTvApi34`, instalação e `MainActivity` foram executadas no mesmo
  ciclo do wrapper; emulador encerrado ao final.
- Nenhuma alteração ou release do servidor foi feita; o bloqueio existente de
  `ShortMaxSeriesProvider.cs` permanece fora do escopo APK-only.

## Rodada v1.2.91 — Descoberta LAN IPv6 consistente

Vigésima sexta rodada. `versionCode` 292. A release Android foi publicada em
[app-v1.2.91](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.2.91).

### O que mudou

- `ServerHost` passou a normalizar hosts IPv6 com colchetes e reconhecer endereços ULA,
  link-local, site-local e loopback como servidores locais.
- O endereço não especificado `::` continua sendo local para classificação, mas não é
  considerado um endpoint discável.
- A apresentação do perfil reutiliza a mesma política compartilhada da recuperação LAN,
  evitando que o mesmo servidor seja classificado como remoto em uma tela e local em outra.
- Foram adicionados testes para IPv6 na política de recuperação LAN e na apresentação do perfil.

### Evidências

- `:design-system:testDebugUnitTest`, `:app:testDebugUnitTest` e
  `:feature:user:testDebugUnitTest`: **passaram**.
- A execução completa de testes/lint passou antes de o reempacotamento encontrar um artefato
  APK incremental corrompido; após remover somente esse artefato gerado, `:app:assembleRelease`
  passou com código 0 e 662 tarefas.
- APK: `dist/mulletaflix-app-v1.2.91.apk`, 7.336.683 bytes, SHA-256
  `587F5E40A9DD497CA8629E28B4F356D8F24420FCE8182D75067133DF3E8E879E`.
- Smoke test no AVD `MulletaflixTvApi34`: instalação e abertura bem-sucedidas;
  `versionCode=292`, `versionName=1.2.91`, sem crash do pacote; emulador encerrado ao final.
- A release Android foi conferida na API do GitHub e publicada separadamente.
- A release do servidor não foi reconstruída nesta rodada: o build permanece bloqueado pelo
  erro existente de `ShortMaxSeriesProvider.cs`; o escopo desta conversa é APK-only.

## Rodada v1.2.90 — Contrato JSON das faixas padrão

Vigésima quinta rodada. `versionCode` 291. A release Android foi publicada em
[app-v1.2.90](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.2.90).

### O que mudou

- Adicionado `MediaSourceApiContractTest`, que desserializa um payload JSON com
  `DefaultAudioStreamIndex=7` e `DefaultSubtitleStreamIndex=-1` usando Moshi.
- Adicionado teste de compatibilidade para payloads antigos sem esses campos, mantendo
  ambos como `null` quando o servidor não os envia.

### Evidências

- `:core:api:testDebugUnitTest` e `:data:testDebugUnitTest`: **passaram**.
- `testDebugUnitTest :app:lintDebug :app:assembleRelease`: **BUILD SUCCESSFUL**, 1414
  tarefas, sem erros de lint.
- APK: `dist/mulletaflix-app-v1.2.90.apk`, 7.336.683 bytes, SHA-256
  `90394E6EAFF873E98B2992C60B4FDCC19C202E1215C8B81754AC8C52A48C45FE`.
- Smoke test no AVD `MulletaflixTvApi34`: instalação e abertura bem-sucedidas;
  `versionCode=291`, `versionName=1.2.90`, sem crash do pacote após logcat limpo;
  emulador encerrado ao final.
- A release de servidor não foi reconstruída nesta rodada: o build falhou no código
  existente de `ShortMaxSeriesProvider.cs`, que usa `GetProviderId`/`SetProviderId`
  ausentes. O APK foi publicado separadamente, conforme o escopo desta conversa.

## Rodada v1.2.89 — Índices de áudio e legendas preservados do servidor

Vigésima quarta rodada. `versionCode` 290. A release Android foi publicada em
[app-v1.2.89](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.2.89).

### O que mudou

- `MediaSourceDto` agora desserializa `DefaultAudioStreamIndex` e
  `DefaultSubtitleStreamIndex`, e `MediaMapper` os encaminha para o domínio.
- O valor `-1` de legenda é preservado como escolha explícita de legendas desativadas;
  ele não é confundido com `IsDefault` das faixas.
- Foi incluído teste unitário do mapper com índice de áudio `7` e legenda `-1`.

### Quality Bar e validação

- `:data:testDebugUnitTest`, `:feature:player:testDebugUnitTest` e
  `:core:api:testDebugUnitTest`: **passaram**.
- `testDebugUnitTest :app:lintDebug :app:assembleRelease`: **BUILD SUCCESSFUL**, 1414
  tarefas, sem erros de lint.
- APK: `dist/mulletaflix-app-v1.2.89.apk`, 7.336.683 bytes, SHA-256
  `DAAB511A56AEC8603CEB5EAB50272866DF77FAE9B3487407D0051CE2A828F812`.
- Smoke test no AVD `MulletaflixTvApi34`: instalação e abertura bem-sucedidas;
  `versionCode=290`, `versionName=1.2.89`, sem `FATAL EXCEPTION` após limpar o logcat;
  emulador encerrado ao final.
- A tentativa de autenticação real no servidor retornou HTTP 400; por isso a reprodução
  E2E com mídia autenticada não foi declarada como validada. O contrato do servidor e o
  mapeamento local estão cobertos, mas a sessão real continua pendente.

## Rodada v1.2.88 — Os dois fluxos de atualização passam a dividir a decisão de instalação

Vigésima terceira rodada. `versionCode` 289 (o salto v1.2.48 → v1.2.87 veio no commit
multi-recursos ainda não empurrado; este é o primeiro incremento depois dele).
**Publicada** em [app-v1.2.88](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.2.88).

Fecha o item do backlog que estava aberto: os **dois fluxos de atualização do app** — a checagem
automática da `MainActivity` e a manual do Centro de Atualizações — mantinham cópias separadas do
laço "baixar → instalar → classificar o resultado", com as mesmas duas mensagens escritas duas
vezes, palavra por palavra. Regra de atualização mudando = dois lugares para mexer. Agora é um.

### O que mudou

- A classificação do resultado da instalação saiu para `:core:common/update/AppUpdateInstallOutcome.kt`:
  `Started` / `Rejected` (o instalador respondeu `false` — falta permissão de fontes desconhecidas) /
  `Failed(detalhe?)`, com as duas mensagens canônicas como constantes e `errorMessageOrNull()`
  dando o texto visível de cada desfecho. O `install` entra como lambda — é o que permite teste de JVM.
- `AppUpdateViewModel.downloadUpdate` e `SettingsViewModel.downloadAndInstallUpdate` usam a política.
  O que cada tela faz com o resultado **não mudou**: a automática fecha o diálogo ao abrir o instalador;
  o Centro de Atualizações mostra "Download concluído. Iniciando instalação...".
- `SettingsViewModel.downloadAndInstallUpdate` trocou `context: Context` por `install: (File) -> Boolean`
  (o `SettingsScreen` passa `AppUpdateInstaller.installApk`), igualando o padrão que o
  `AppUpdateViewModel` já tinha — e destravando a cobertura que faltava.

### Cobertura que esta rodada criou

- `AppUpdateInstallOutcomeTest` (5 testes): os três desfechos, o instalador chamado exatamente uma
  vez, e as duas mensagens presas como **literais** — a primeira versão comparava com a constante, e a
  prova por reversão 2 mostrou que isso não pega quem edita o texto.
- `SettingsViewModelTest` ganhou 3 testes do ramo `Completed`, que até aqui só era verificado por
  compilação: sucesso fecha o diálogo e anuncia o status; recusa mantém o diálogo com a mensagem de
  permissão; exceção preserva o detalhe como erro.

### Provas por reversão (executadas)

| reversão | testes que falham |
|---|---|
| `Rejected` vira `Started` (installer `false`) | 2 de 22 em `AppUpdateInstallOutcomeTest` |
| `FAILED_MESSAGE` troca de texto | `an installer exception without detail falls back to the generic message` (1 de 22) |
| sucesso deixa `showUpdateDialog = true` na `SettingsViewModel` | `a completed download closes the dialog and reports the install start` (1 de 25) |

### Ressalva honesta

A **auditoria adversarial independente** desta vez não rodou como subagente: o harness desta sessão
não expõe tipos de subagente utilizáveis, e nenhum dos identificadores testados foi aceito. A revisão
foi feita inline, caminho a caminho contra o `git diff` (as 4 combinações sucesso/recusa/exceção com
e sem detalhe têm estado final idêntico ao anterior, em ambos os ViewModels), e a lacuna que ela
achou — o ramo `Completed` da `SettingsViewModel` sem teste — foi coberta nesta mesma rodada. Fica o
aviso do método: revisão do próprio trabalho é a forma mais fraca de verificação; reexecutar com
auditor independente quando o harness permitir.

### Quality Bar

- `gradlew testDebugUnitTest` — **BUILD SUCCESSFUL**, 456 tarefas executadas/atualizadas no
  ciclo completo desta rodada; os testes direcionados de `:core:common`, `:feature:settings` e
  `:app` também passaram.
- `gradlew :app:lintDebug :app:assembleRelease` — **BUILD SUCCESSFUL**, lint sem erros.
- Após a cobertura nova: 924 XMLs de `testDebugUnitTest`, 0 falhas/erros; SARIF do lint com 0 erros.
- APK release montado e empacotado (`dist/mulletaflix-app-v1.2.88.apk`, 7.336.683 bytes,
  SHA-256 `E4E2F277E78EA64A488867C9A2BF7D2FFD1ED980F5E186D28613BE0E147EC8C1`).
- O servidor também foi empacotado sem `-SkipInstaller` e publicado em `v12.0.75`, com ZIP
  e instalador anexados; o APK foi publicado em `app-v1.2.88`.
- O AVD `MulletaflixTvApi34` instalou e abriu o APK; após limpar o logcat, não houve crash
  do pacote do aplicativo. Nenhum emulador ficou aberto ao final.

## Rodada v1.2.86 — "Mostrando 30 de 412": agora dá para ver os outros 382

Vigésima segunda rodada. **Não publicada** (cadência de 10 versões; a próxima publicação é a v1.2.90).

Fecha a metade que a v1.2.80 deixou aberta de propósito: naquela rodada a busca passou a **dizer** que
havia mais ("Mostrando 30 de 412 — refine a busca"), e a nota dizia com todas as letras que "paginar a
grade" era funcionalidade, não correção. Isto é a funcionalidade.

### O que mudou

- `SearchRepository.searchItems` ganhou `startIndex`, e o `searchItems` do `SearchMediaUseCase` também.
  O `TotalRecordCount` de cada página é comparado com o que **já foi paginado**
  (`startIndex + items.size`), não só com a página atual — sem isso o total de uma página seguinte era
  lido como contradição.
- Um único teto de página: `SEARCH_PAGE_SIZE` em `:domain`, junto do contrato. Antes havia um `30` no
  repositório e outro na tela, e a tela precisava saber o mesmo número para dizer "mostrando N de M".
- `SearchViewModel.loadMore()` — anexa em vez de trocar, e traz três regras que a paginação da
  Biblioteca já tinha aprendido a duras penas:
  - o `startIndex` é o **tamanho do que já chegou**, não `página × tamanho` (o servidor pode devolver
    página menor, e avançar pelo tamanho pedido pularia itens);
  - a página nova é **deduplicada por id** (ordenação instável do servidor repete itens entre páginas);
  - página que volta vazia — ou que só traz repetidos — **para**: insistir devolveria o mesmo.
- `SearchState` ganhou `isLoadingMore` (separado de `isLoading`, que troca a lista por um indicador
  enquanto "carregar mais" precisa **manter** o que está na tela) e `hasMore`.
- A tela ganhou `LoadMoreRow` no fim da lista, com a tag `LOAD_MORE_TEST_TAG`: botão "Carregar mais"
  quando ocioso, indicador **no lugar dele** enquanto carrega — dois toques pediriam a mesma página.
- A frase do aviso perdeu o "refine a busca": com o botão logo abaixo, o conselho virou contradição.
  Agora é só "Mostrando 30 de 412 resultados."

### O que dá errado sem cuidado (e como foi tratado)

Uma pergunta nova (digitar, buscar, trocar filtro) tem de **baixar `isLoadingMore` na hora**, antes do
debounce. A resposta da página antiga é descartada pela geração, então ela nunca baixaria a flag — o
indicador giraria para sempre. Isso está no teste, e é observável **sem** esperar o debounce.

### Provas por reversão (executadas)

| reversão | testes que falham |
|---|---|
| a página seguinte é pedida com `startIndex = 0` | 4 de 40 em `SearchViewModelTest` (anexar, deduplicar, parar no fim e recomeçar) |
| a deduplicação por id sai | `loadMore does not repeat an item the server sent twice` (1 de 40) |

### Ressalva honesta

O "carregar mais" é manual — um botão no fim da lista — e não carregamento automático ao rolar. É a
opção que não muda a rolagem nem o foco do controle remoto, que o projeto trata como risco desde a
v1.2.73. `hasMore` também é conservador: quando o servidor **não** informa o total, a tela oferece mais
uma página enquanto a última vier cheia de itens novos, e para na primeira que não trouxer nada.

## Rodada v1.2.85 — trocar o tema no meio do download matava o download

Vigésima primeira rodada. **Não publicada** (cadência de 10 versões; a próxima publicação é a v1.2.90).

### O defeito, e por que ele não era teórico

O aviso de atualização da `MainActivity` guardava seu estado em `remember { mutableStateOf(...) }` —
seis valores — e o download do APK rodava em **`rememberCoroutineScope()`**. O escopo de composição é
cancelado quando a composição sai, e o manifesto declara
`android:configChanges="orientation|screenSize|smallestScreenSize|screenLayout|colorMode|keyboard|keyboardHidden|navigation"`
— isto é, **`uiMode`, `density`, `fontScale` e `locale` não estão lá**. Então:

> trocar o tema do sistema, o tamanho da fonte, o tamanho da tela ou o idioma **recria a Activity**;
> a recriação cancela o escopo; o download do APK morre no meio.

E morria **em silêncio**: o `catch (CancellationException)` relançava, o estado voltava a
"não estou baixando", o progresso ia a zero e o espectador via exatamente o mesmo diálogo de antes.
Pior: a versão que ele já tinha dispensado voltava a ser oferecida, porque `dismissedUpdateVersion`
também era `remember`. A única coisa que não se perdia era o arquivo parcial — o `finally` do
`AppUpdateDownloader` apaga o APK incompleto.

### A correção

O estado e o trabalho saíram da composição para um `AppUpdateViewModel`
(`:app/.../update/AppUpdateViewModel.kt`), preso ao store da Activity: `viewModelScope` só é cancelado
em `onCleared`, que acontece quando a Activity é **encerrada**, não recriada. A `MainActivity` passou a
ler um `AppUpdateState` e a chamar `checkForUpdate` / `dismissDialog` / `downloadUpdate`; o diálogo
inteiro (a UI) continuou onde estava.

Três guardas novas, todas com teste: não consultar durante um download; **não deixar uma resposta
atrasada da checagem fechar o diálogo com o download em curso**; e a instalação entra como parâmetro
(`install: (File) -> Boolean`), o que permite exercitar sucesso, falha e instalação recusada sem
Android e sem um `Context` dentro do `ViewModel`.

`dismissedVersion` é de propósito um campo comum, e não algo persistido: "dispensado" vale para esta
sessão, e a próxima abertura pode voltar a oferecer a versão — que é o que o comentário original já
dizia.

### Provas por reversão (executadas)

| reversão | testes que falham |
|---|---|
| tira a guarda de "não mexer no diálogo durante o download" | `a check that lands mid-download cannot close the dialog` (1 de 95) |
| a dispensa deixa de ser consultada | `a dismissed update is not offered again in the same session` (1 de 95) |
| qualquer dispensa silencia tudo, em vez de só aquela versão | `a newer release than the dismissed one is offered again` **e** `an update without a download link is not offered` (2 de 95) |

**Um teste meu que não discriminava, pego antes de virar prova:** o primeiro teste da guarda de
download só conferia `isDownloading` e `progress` — e esses dois sobrevivem a um `copy()` **sem
guarda nenhuma**, então ele passaria com o defeito no lugar. Foi reescrito para o caso que discrimina:
uma checagem que chega dizendo "não há mais novidade" e tentaria fechar o diálogo com o download
rodando.

### Ressalva honesta

O que está provado é o **comportamento** do fluxo (dez testes de JVM) e a **estrutura** que corrige o
defeito: estado e corrotina num `ViewModel` do store da Activity. A recriação em si **não** foi
exercitada por um teste instrumentado — fazê-lo exigiria subir a `MainActivity` real com sessão e
injetar um downloader falso, e a checagem de atualização vai à rede. Fica registrado como o que ainda
não está provado, em vez de como provado.

### Achado lateral: existem **dois** fluxos de atualização

A `MainActivity` (checagem automática ao abrir) e a `SettingsViewModel` (checagem manual no Centro de
Atualizações) implementam o mesmo "checar → diálogo → baixar → instalar", com estados e diálogos
separados. Este round corrigiu só o da `MainActivity`. O da `SettingsViewModel` **já vive num
ViewModel** e portanto já sobrevive à recriação; o que sobra é a duplicação em si — dois lugares para
mexer quando a regra de atualização mudar. Registrado no backlog.

## Rodada v1.2.84 — o alvo de 40 dp, finalmente medido: **não havia defeito**

Vigésima rodada. **Não publicada** (cadência de 10 versões; a próxima publicação é a v1.2.90).

Esta rodada fecha **a última pendência da lista de acessibilidade**, aberta desde a v1.2.73 e
repetida em quatro rodadas com a mesma frase: *"o alvo de 40 dp do controle de transmissão precisa de
medição no aparelho, não de um palpite"*.

### O que foi preciso para conseguir medir

O bloco era um `Row` solto dentro do `VideoPlayerScreen`, inalcançável por teste. Virou
`PlayerCastControl` (com `PLAYER_CAST_CONTROL_TEST_TAG`), o que também é a divisão que o resto do
projeto usa. Depois disso, **três paredes** apareceram, e ficam registradas porque qualquer tentativa
futura bate nas mesmas:

1. `MediaRouteButton` **não compõe** sem o Cast SDK inicializado — `IllegalStateException: Must
   initialize Cast prior to using it`. Em produção quem inicializa é o `:app`
   (`MulletaFlixApp.onCreate` + `OPTIONS_PROVIDER_CLASS_NAME` no manifesto). O módulo não enxerga
   nada disso, então o APK de teste ganhou o seu próprio `TestCastOptionsProvider` e um
   `src/androidTest/AndroidManifest.xml`.
2. `CastContext.getSharedInstance` **só na thread principal** → `runOnMainSync`.
3. **A regra de teste importa.** Com `createComposeRule` (a variante `v2` usada no resto da suíte) a
   composição roda no dispatcher do teste e o serviço de media router recusa: *"The media router
   service must only be accessed on the application's main thread"*. O teste da medição usa
   `createAndroidComposeRule<ComponentActivity>()`, que compõe na thread da Activity de verdade.

### O resultado

```
alvo de toque medido no aparelho: 48.0dp x 48.0dp (layout: 40.0dp x 40.0dp; mínimo 48dp)
```

**Não havia defeito.** O `Modifier.size(40.dp)` sempre foi o **desenho**; o **alvo de toque** que o
Compose publica já eram os 48 dp mínimos, porque a fundação expande o alvo sozinha. O item fecha como
**medido, sem defeito**, e **nenhum modificador foi acrescentado**.

### O erro que eu quase cometi — e que é o ponto da rodada

A primeira medição foi de `boundsInRoot`, e deu **40,0 dp × 40,0 dp**. Eu já tinha escrito o conserto
(`minimumInteractiveComponentSize()`) antes de medir, e a medição "confirmou" o defeito. Só que
`boundsInRoot` é o **layout**, não o alvo de toque. Ao medir `touchBoundsInRoot` no mesmo nó, o valor
deu 48 dp — **e o teste passou sem mudança nenhuma**. O conserto foi removido.

Medir a coisa errada é tão ruim quanto não medir: teria produzido um "defeito" inexistente, um
modificador a mais numa fileira já apertada e um changelog mentindo. Fica a lição de método:
**alvo de toque e tamanho de layout são medidas diferentes**, e é a primeira que a acessibilidade
exige.

### O que o teste é — e o que ele não é

Ele **registra uma medição** e vira guarda contra uma classe específica de regressão: se o controle
for trocado por algo que não participe da expansão de alvo do Compose, ele reprova. Ele **não** pega
uma redução de tamanho, porque o Compose expande para 48 dp independentemente do layout — dizer que
ele "prende os 48 dp" seria falso.

**Ressalva honesta:** o medido é `touchBoundsInRoot`, que é o que os serviços de acessibilidade usam
(é a área que o TalkBack enxerga). Um **dedo** tocando os 4 dp da borda externa não foi exercitado — o
clique pertence à `View` do Media3, e não há como observá-lo neste teste sem uma rota de Cast real.
O que está provado é a medida de acessibilidade, não o hit-test do dedo no anel externo.

### Onde rodou

No **AVD de celular** (`MulletaflixApi35`), porque a regra dos 48 dp é sobre toque — a densidade da TV
é outra e o controle remoto não toca. A suíte instrumentada de `:feature:player` foi de 13 para **14
testes** e passou inteira no celular.

## Rodada v1.2.83 — a auditoria adversarial pegou dois defeitos que EU tinha acabado de introduzir

Décima nona rodada. **Não publicada** (cadência de 10 versões; a próxima publicação é a v1.2.90).

Esta rodada começou com uma tarefa de infraestrutura — extrair o laço de nova tentativa do próximo
episódio do `PlayerViewModel`, que era a parte "que nenhum teste alcança" desde a v1.2.79 — e terminou
com **dois defeitos reais corrigidos**, os dois introduzidos por mim nas duas rodadas anteriores.

### O método: auditoria adversarial independente das minhas próprias mudanças

Revisar o próprio trabalho é a forma mais fraca de verificação. Deleguei uma auditoria adversarial
(read-only, com instrução explícita de **procurar defeito e não elogiar**) sobre as três mudanças que
eu tinha acabado de fazer: M4 (busca truncada), M2 (cache Room apagado) e M9/TV ao vivo. O auditor
comparou com `HEAD`, varreu chamadores no repositório e leu o servidor para checar premissas.

Resultado: **dois defeitos confirmados, dois suspeitos, nove pontos verificados e limpos** — incluindo
a refutação de duas hipóteses minhas (o carrossel e o cartão de erro não podem aparecer juntos; e
`getLiveTvChannelPreview` é mesmo um no-op). Os dois suspeitos ficaram registrados como "precisa de
servidor não canônico para observar", não como corrigidos.

### Defeito 1 — a linha de truncamento afirmava uma contagem para uma busca que nunca rodou

**Meu, da v1.2.80.** `performSearch` tem uma saída antecipada quando o aparelho está offline, e ela
atualizava `isLoading`/`isRefreshing`/`error` mas **não** `results`/`totalMatching`. Somado a
`onQueryChange`/`search`/`setFilter`, que também não mexiam no total:

1. buscar "batman" online → 30 itens, total 412;
2. cair a rede;
3. digitar "zzz" → o `onQueryChange` mantém os 30 resultados e o 412, o `performSearch` bate em
   `isOffline` e volta.

A tela então mostrava **30 cartões de "batman" e a linha "Mostrando 30 de 412 resultados" embaixo do
campo com "zzz"**. Antes da v1.2.80 os cartões velhos já apareciam (isso é anterior), mas agora a
tela *afirmava um número* — a mudança transformou uma lista desatualizada numa frase falsa.

**Correção:** a saída offline passou a seguir a mesma regra que a saída por falha já seguia
(`results`/`totalMatching` só sobrevivem num refresh, onde rever a mesma lista é a intenção); e
`onQueryChange`, `search` e `setFilter` zeram `totalMatching`, porque uma pergunta nova — ou um filtro
novo — invalida a contagem anterior.

### Defeito 2 — o aviso da TV ao vivo sobrevivia à carga e aparecia ao lado do erro do feed

**Meu, da v1.2.82.** O `HomeViewModel` limpava `librariesError` no início da carga mas não
`liveTvError`; e o ramo de falha total não limpava nenhum dos dois. Sequência: carga 1 com
`LiveTv/Channels` falhando (aviso na tela) → recarga com o servidor fora → o feed inteiro falha →
**os dois cartões de erro juntos**, justamente o que o comentário da `HomeScreen` documenta como
impossível. O mesmo valia para a recarga offline, que volta antes da limpeza.

**Correção:** a limpeza dos dois avisos de seção foi para o **início** da carga, cobrindo as três
saídas (offline, sessão expirada e falha total). É a mesma regra que já valia para `librariesError`,
só que num lugar onde ela vale para todos os caminhos.

### O teste que não discriminava — terceiro da série

O primeiro teste que escrevi para o Defeito 2 (`a reload clears the previous live tv failure`) fazia a
**segunda carga ter sucesso**, e aí o `onSuccess` já sobrescrevia o aviso antigo: ele passava **com e
sem** a correção. A prova por reversão pegou. Em vez de mantê-lo, reescrevi para o caminho que
discrimina — uma recarga que volta cedo por estar offline e não produz feed nenhum — e ele passou a
falhar sem o conserto. É a terceira vez na série que a reversão pega um teste meu sem poder de
discriminação (as outras: o aviso de offline na v1.2.77 e o `SingleFlightIdTest` na v1.2.79).

### Provas por reversão (executadas)

| reversão | testes que falham |
|---|---|
| saída offline volta a manter `results`/`totalMatching`, e `onQueryChange` volta a não zerar o total | `SearchViewModelTest > offline with a new query drops the previous results and total` e `... > typing a new query drops the previous server total before the answer arrives` (2 falhas, 34 testes) |
| limpeza de início de carga removida do `HomeViewModel` | `HomeViewModelTest > an offline reload clears the previous live tv failure` e `... > a whole feed failure does not leave a stale live tv warning` (2 falhas, 27 testes) |

### E a tarefa original: o laço de nova tentativa virou testável

`settleNextEpisodeLookup` saiu do `PlayerViewModel` (onde só era provado por compilação) para
`NextEpisodeLookupRetry.kt`, com 5 testes novos que exercitam o **laço** e não só as políticas:
acerto de primeira não repete, sucesso sem episódio encerra ("a série acabou" é resposta, não falha),
duas falhas e um acerto devolvem o episódio, três falhas desistem e devolvem a **última** falha, e a
espera acumulada é exatamente 1000 + 2000 ms. Esse último só é possível em tempo virtual: a classe
inteira roda em **0,141 s**, quando as esperas reais seriam 3 s. `:feature:player` ganhou
`testImplementation(kotlinx.coroutines.test)` para isso.

**Prova por reversão:** tirar o `delay` do laço derruba exatamente
`a espera entre tentativas e a que a politica promete` (1 falha, 8 testes).

**O que continua fora de alcance:** o laço agora tem teste, mas o **resto** do `PlayerViewModel`
(prepare, faixas, sleep timer, o efeito de publicar o episódio) segue sem harness. A ligação
`ViewModel → settleNextEpisodeLookup` continua verificada por compilação.

## Rodada v1.2.82 — a TV ao vivo que sumia, e a armadilha de nome no repositório

Décima oitava rodada da série, quarta do bloco de **dados**: ataca o achado **M9**. **Não
publicada** (cadência de 10 versões; a próxima publicação é a v1.2.90).

### O defeito: "não há canais" e "a busca falhou" eram a mesma tela

`GetHomeFeedUseCase` fazia `liveTvResult.getOrDefault(emptyList())` e a `HomeScreen` só desenha o
carrossel de TV ao vivo `if (state.liveTvChannels.isNotEmpty())`. Consequência: um servidor com TV
ao vivo **desligada** (que responde *sucesso com zero canais*) e um servidor que **respondeu 500**
produziam exatamente a mesma Home — sem carrossel, sem aviso, sem "tentar novamente".

É o mesmo defeito que a v1.2.75 corrigiu para as bibliotecas (M1), na seção vizinha: lá o
`librariesError` foi criado justamente porque "a Home sem blocos de biblioteca era indistinguível de
uma conta sem biblioteca nenhuma". A TV ao vivo ficou para trás.

**A correção:**

- `HomeFeed.liveTvError` e `HomeState.liveTvError`, alimentados por um `sectionError(fallback)`
  novo em `GetHomeFeedUseCase` — que agora serve **as duas** seções, para bibliotecas e TV ao vivo
  contarem a mesma história em vez de cada uma inventar o seu texto.
- `HomeScreen` desenha o `HomeLoadErrorCard` (o mesmo componente do erro de bibliotecas, com o
  "Tentar novamente" que o torna recuperável) **na posição onde o carrossel estaria** — e não junto
  das bibliotecas, para não sugerir que o problema é delas.
- **Zero canais por sucesso continua não sendo erro.** Essa distinção é o ponto: quem não usa TV ao
  vivo não ganha um aviso por isso.

### A armadilha: `getLiveTvChannels` era uma página com nome de catálogo

`MediaRepositoryImpl.getLiveTvChannels` chamava `api.getLiveTvChannels(userId = userId)` e deixava o
`Limit` cair no default do Retrofit (`LIVE_TV_CHANNEL_PAGE_SIZE = 100`), ignorando o
`TotalRecordCount`. O `LiveTvRepository.getChannels` pagina até o fim, porque a lista da tela de TV
ao vivo precisa de todos os canais — mas quem precisasse da lista inteira e chamasse o método do
`MediaRepository` receberia 100 canais **em silêncio**.

O carrossel da Home quer o oposto (uma amostra barata, sem disparar uma dúzia de requisições ao
abrir o app), então o método não foi apagado: foi **renomeado para `getLiveTvChannelPreview`**, com o
`limit` explícito na chamada em vez de herdado do default, e com um KDoc que diz, na primeira linha,
que é uma prévia e que a lista completa é `LiveTvRepository.getChannels`. O contrato virou o nome.

### Código morto removido (o resto do M9)

Sem chamador de produção, todos confirmados por varredura antes de sair:

- `MediaRepository.search` + implementação — duplicava o caminho real (`SearchMediaUseCase` →
  `SearchRepository.searchItems`).
- `MediaRepository.getRecordings` + implementação — duplicava o **paginado**
  `LiveTvRepository.getRecordings`. Era a mesma armadilha do nome: um chamador futuro receberia uma
  página achando que tinha o acervo.
- `MediaRepository.getSuggestions` + implementação + `api.getSuggestions`
  (`Items/{itemId}/Suggestions`). O `getSimilarItems` (`Items/{id}/Similar`) é outro endpoint e
  **continua**, porque é usado pelo detalhe do item.

### O que foi deliberadamente mantido: `searchHints`

`SearchRepository.searchHints` e `api.searchHints` (`GET Search/Hints`) também não têm chamador. Eles
**não** foram removidos, e a razão é explícita: é o encanamento de uma funcionalidade real
(type-ahead na busca), o `SearchBar` da tela de busca já tem um slot `content = {}` vazio esperando
por ele, e não é código *errado* como o cache da v1.2.81 — é uma funcionalidade que o app não oferece.
Ficou registrado no backlog como o que é: **decisão de produto adiada**, não esquecimento. M9 fecha
como "duplicatas mortas removidas, armadilha desarmada, uma superfície adiada com motivo".

### Provas por reversão (executadas)

| reversão | teste que falha |
|---|---|
| `GetHomeFeedUseCase` volta a não preencher `liveTvError` | `UseCaseTest > GetHomeFeedUseCase surfaces a live tv failure instead of a silent empty carousel` (1 falha, 95 testes) |
| `HomeViewModel` deixa de copiar `feed.liveTvError` para o estado | `HomeViewModelTest > partial section failure such as live tv does not crash home screen` (1 falha, 25 testes) |

Nos dois casos **só o teste nomeado** falhou — inclusive o par `GetHomeFeedUseCase treats a server
without live tv as success, not as failure`, que precisa continuar passando, porque marcar "servidor
sem TV" como erro encheria a Home de aviso para quem não usa TV.

### Ressalva honesta sobre a cobertura

O que está provado é a **propagação do estado** (use case → ViewModel), por reversão. O desenho do
cartão na `HomeScreen` é verificado por compilação, seguindo exatamente o caminho já publicado do
`librariesError` (mesmo componente `HomeLoadErrorCard`, mesma forma de inserção na lista). Não há
teste instrumentado que monte a `HomeScreen` inteira — o `HomeScreen` depende do `HomeViewModel` por
`hiltViewModel()`, e montar isso exigiria um harness de tela inteira que o projeto ainda não tem.

### O que ficou

Dados: de dois achados para **um** — só **M5** (índices de faixa do servidor) resta, e ele está
marcado **não mexer sem sessão real**. **M9 fechado.**

## Rodada v1.2.81 — o cache que não existia, e a regra nova de publicar a cada 10 versões

Décima sétima rodada da série, terceira do bloco de **dados**: fecha o achado **M2** do backlog
prioritário. **Esta versão não foi publicada** — ver a decisão do usuário no fim desta seção.

### O defeito: um cache que mentia em três camadas

O `MediaRepositoryImpl` abria com um KDoc descrevendo uma *"offline-first strategy"* em quatro
passos — emitir do cache Room, buscar da rede, atualizar o cache, emitir de novo — e recebia um
`MediaItemDao` injetado. **Nada disso era verdade:**

1. **Nenhum método de produção escrevia no banco.** `upsert`, `upsertAll`, `delete`, `clearForUser`
   e `getById` não tinham um único chamador.
2. **Os dois únicos leitores não tinham chamador tampouco.** `observeFavorites` e
   `observeRecentlyWatched` existiam na interface `MediaRepository` e só apareciam em *fakes* de
   teste. Ou seja: o `mulletaflix.db` nunca chegava a ser criado no disco (o Room abre o arquivo na
   primeira consulta, não no `build()`), e mesmo assim a classe anunciava cache.
3. **Se alguém o ligasse, ele corrompia dados.** A chave primária era `id` sozinho enquanto a linha
   guardava `userId`, e `MediaItem.toEntity` tinha `userId = ""` como padrão — dois usuários com o
   mesmo item se sobrescreveriam. `getById`/`delete` também não filtravam usuário. Somado a
   `fallbackToDestructiveMigration(dropAllTables = true)` no `DatabaseModule`, era uma perda de
   dados esperando um chamador.

### A correção: apagar, não consertar

O próprio achado oferecia duas saídas — apagar, ou dar chave composta `(userId, id)` com `userId`
obrigatório. **Apagar** foi a escolha, por três razões: não havia comportamento de usuário a
preservar (nada lia o cache), um cache correto-mas-desligado continuaria sendo código morto com
manutenção e dependência, e "cache offline de favoritos" é uma **funcionalidade** que merece desenho
próprio — não a ressurreição de uma estrutura cuja chave estava errada.

Saiu tudo: `db/MulletaFlixDatabase.kt` (entidade + DAO + banco), `di/DatabaseModule.kt`, os dois
métodos de `MediaRepository` e suas implementações, os dois mapeadores `MediaItemEntity.toDomain` /
`MediaItem.toEntity`, as dependências `room-runtime`/`room-ktx`/`room-compiler` (`:data` e catálogo
de versões) e os três `-keep` de Room no `proguard-rules.pro`. O KDoc falso foi substituído por um
que explica o que foi embora e por quê.

### Provas (executadas)

| prova | evidência |
|---|---|
| nenhuma referência sobrevive | `grep` por `androidx.room`, `MediaItemEntity`, `MediaItemDao`, `MulletaFlixDatabase`, `observeFavorites`, `observeRecentlyWatched`, `mulletaflix.db`, `libs.room`, `toEntity` → **zero** fora do comentário explicativo |
| o compilador concorda | `testDebugUnitTest` + `:app:lintDebug` → `BUILD SUCCESSFUL`, 0 erros de lint |
| nada foi injetado a menos em execução | APK instalado e iniciado na TV: `Success`, PID 2528, **0 linhas de crash** — a remoção do `@Module` do Hilt não deixou binding faltando |
| **o pacote ficou menor, medido nos DEX** | varredura das strings dentro de `classes*.dex`: v1.2.80 tinha `androidx/room/RoomDatabase` ×1, `mulletaflix.db` ×1, `MediaItemEntity` ×3; v1.2.81 tem **0, 0, 0**. Tamanho: 7.357.517 → **7.320.299** bytes (−37.218) |

Os dois únicos testes que saíram (`observeFavorites_mapsEntitiesFromDao` e o round-trip
`entity round trip preserves offline playback fields`) testavam exatamente o que deixou de existir —
por isso a contagem caiu de 873 para **871**, e não porque algo regrediu.

### Nota de método: como se prova uma remoção

Prova por reversão não se aplica a apagar código — não há defeito para reintroduzir e ver o teste
falhar. O que vale aqui é: (a) o **compilador** provando que nada ficou pendurado, (b) a **suíte
inteira** continuando verde, e (c) uma **medição do artefato** antes/depois mostrando que o que se
queria remover realmente saiu do que é distribuído. Sem (c), "apaguei o cache" seria uma afirmação
sobre o repositório, não sobre o APK.

### O que ficou

Dados: de três achados para **dois** — **M9** (API morta, com a armadilha viva do
`getLiveTvChannels`) e **M5** (índices de faixa do servidor, **não mexer sem sessão real**).
**M2 fechado.**

### Mudança de processo pedida pelo usuário nesta rodada

> "só publique release a cada 10 versões com a explicação de tudo o que foi alterado"

A partir da v1.2.81, **release no GitHub só a cada 10 versões do app**, e as notas têm de cobrir
**todas** as versões acumuladas desde a publicação anterior — não só a última rodada. A v1.2.80 é a
âncora do bloco, então a próxima publicação é a **v1.2.90**. Nas rodadas 1.2.81 … 1.2.89 o APK é
compilado, testado e instalado localmente e **não** é publicado. A regra está registrada em
`## Regras que o próximo agente deve respeitar` (regra 4) e no fluxo de release (passo 5).

## Rodada v1.2.80 — a busca parava em 30 e não contava o resto

Décima sexta rodada da série, e a segunda do bloco de **dados**: fecha o achado **M4** do backlog
prioritário (busca truncada em 30, descartando o `TotalRecordCount`).

### O defeito

`SearchRepositoryImpl.searchItems` pedia `limit = 30` e mapeava **só** `response.items`. O
`TotalRecordCount` vinha na mesma resposta HTTP e era jogado no lixo. Numa biblioteca de 4 000
itens, procurar por "a" mostrava 30 capas e nada na tela distinguia "a busca achou 30" de "a busca
achou 412 e eu mostrei 30". Não havia paginação, não havia "carregar mais", não havia aviso: a tela
afirmava, pelo silêncio, que 30 era tudo.

### A correção

O total agora viaja do servidor até a tela, e a tela é obrigada a se pronunciar:

- `:domain` ganhou `SearchResults(items, totalMatching)` com `isTruncated`; `SearchRepository.searchItems`
  e `SearchMediaUseCase` passaram a devolver esse tipo em vez de `List<MediaItem>`.
- `SearchRepositoryImpl` mantém o teto de uma página (`SEARCH_PAGE_LIMIT = 30`, agora com nome e
  comentário em vez de um `30` solto) e preenche `totalMatching`. **`TotalRecordCount` é opcional no
  protocolo** e o DTO cai para `0` quando falta: um total **menor que a própria página recebida** não
  é contagem, é contradição, então vira `null` — "não sei" é mais honesto do que `0`, que a tela
  leria como "não há mais nada".
- `SearchViewModel.SearchState` carrega `totalMatching` e expõe `isTruncated`; o total é zerado junto
  com os resultados quando o campo é esvaziado ou o usuário troca, para nenhuma contagem antiga
  ficar pendurada ao lado do histórico.
- `SearchScreen` mostra, como primeiro item da lista de resultados, a linha
  **"Mostrando 30 de 412 resultados — refine a busca."** A frase é decidida por
  `searchTruncationNotice(shownCount, totalMatching)` (política pura, em `SearchTruncationNotice.kt`)
  e o `Composable` que a desenha é `SearchTruncationBanner` — a mesma divisão política/UI das
  rodadas anteriores, para que o texto seja testável sem Compose e a UI seja testável com Compose.

### Provas por reversão (executadas)

| reversão | teste que falha |
|---|---|
| `SearchRepositoryImpl` volta a não preencher o total | `SearchRepositoryImplTest > searchItems_keepsTheServerTotalSoTheScreenCanAdmitTruncation` |
| `SearchViewModel` deixa de copiar `totalMatching` para o estado | `SearchViewModelTest > successful search carries the server total into state`, `... > a total equal to what arrived is not truncation`, `... > clearing the query drops a stale server total` (3 falhas) |

A primeira reversão foi executada com o `totalMatching` forçado a `null` no repositório; a segunda
removendo a linha do `SearchViewModel`. Nos dois casos os testes nomeados acima falharam e os demais
passaram — inclusive `... > search without a server total does not claim truncation`, que é o caso
"não sei" e por isso **não deve** falhar quando o total some.

### O que ficou

Dados: de quatro achados para **três** — **M2** (cache Room morto, com chave errada se for ligado),
**M9** (API morta: `MediaRepository.search`, `.getRecordings`, `.getSuggestions`,
`SearchRepository.searchHints` e as mutações do `MediaItemDao`; mais a armadilha de
`MediaRepositoryImpl.getLiveTvChannels`, gêmeo de uma página só do `LiveTvRepository.getChannels`)
e **M5** (índices de faixa do servidor — **não mexer sem sessão real**).

### O que NÃO foi feito nesta rodada

Nenhum script de release do servidor foi executado — decisão do usuário na v1.2.76, registrada
acima. A busca continua **sem paginação**: quem procura algo muito comum vê 30 itens e a frase; não
há "carregar mais". Isso é uma limitação conhecida, não um defeito escondido — mas é o próximo passo
natural deste achado se alguém quiser continuá-lo.

## Rodada v1.2.79 — o identificador duplicado e a maratona que terminava em silêncio

Décima quinta rodada da série. Ataca o bloco de **dados** do backlog: dois achados em que o app
respondia a uma falha com uma afirmação que não era verdade.

### O aparelho se apresentava ao servidor como dois dispositivos

`SessionRepositoryImpl.getDeviceId()` fazia ler-gerar-gravar **sem trava**, e o interceptor de
identidade chama esse valor **a cada requisição**. No primeiro boot a Home dispara várias
requisições em paralelo: todas liam `null` antes de qualquer escrita e cada uma criava o seu
próprio UUID. O DataStore serializa as gravações, então a última vencia — e o mesmo aparelho
aparecia como dois dispositivos na lista do servidor, com a sessão criada no login possivelmente
amarrada a um id que os relatórios seguintes não usavam.

A resolução virou `SingleFlightId`: uma classe pequena com trava e **segunda checagem** dentro
dela, que é o que faz quem entrou na fila encontrar o valor pronto em vez de gerar outro. O
`SessionRepositoryImpl` passou a delegar — a parte que depende do DataStore ficou sendo só duas
lambdas.

Oito chamadas concorrentes com a leitura realmente suspensa provam a correção; o teste usa
`Dispatchers.Default` de propósito, porque com o dispatcher de teste os blocos rodam em sequência
e a janela de corrida não existiria — o teste passaria com o defeito no lugar.

### "Não consegui olhar" era respondido como "a série acabou"

`GetNextEpisodeUseCase` já propagava a falha da primeira busca de episódios, mas as duas buscas
de temporada colapsavam a falha em `Result.success(null)`. E `null`, nesse contrato, significa
**"esta série não tem próximo episódio"** — é o valor em que o player esconde o aviso de "Próximo
episódio". Um 5xx passageiro encerrava a maratona sem nada na tela explicando a diferença.

As três buscas agora concordam. E o chamador passou a fazer algo com a falha: o player insiste
até três vezes, com espera crescente e teto, antes de desistir. A decisão está em
`NextEpisodeLookupRetry` (política pura, testada); o laço ficou no `PlayerViewModel`, onde não há
harness — **é a mesma divisão da v1.2.73, e continua sendo a parte que nenhum teste alcança**.

### Provas por reversão (executadas)

| reversão | teste que falha |
|---|---|
| `SingleFlightId.get()` volta a ler-gerar-gravar sem trava | `SingleFlightIdTest > chamadas concorrentes recebem o mesmo identificador` |
| `SingleFlightId` deixa de guardar o valor resolvido | `... > a segunda chamada nao volta a ler` |
| as buscas de temporada voltam a `getOrNull().orEmpty()` | 2 testes de `UseCaseTest` |
| `shouldRetryNextEpisodeLookup` volta a `false` | 2 testes de `NextEpisodeLookupRetryTest` |

### Um teste que não discriminava, pego pela própria reversão

O quarto teste de `SingleFlightIdTest` criava o id a partir de um valor **constante**, então as
chamadas concorrentes devolviam o mesmo valor mesmo com o defeito no lugar — ele passava nas duas
versões. Foi removido em vez de mantido: um teste que sempre passa ocupa espaço e dá uma falsa
sensação de cobertura. É a segunda vez na série que a prova por reversão pega um teste meu sem
poder de discriminação (a primeira foi o aviso de offline, na v1.2.77).

### O que ficou

Dados: de seis achados para **quatro** — **M2** (cache Room morto, com chave errada se for ligado),
**M4** (busca truncada em 30, descartando o `TotalRecordCount`), **M5** (índices de faixa do
servidor — **não mexer sem sessão real**) e **M9** (API morta). A lista de acessibilidade segue
fechada, com a única pendência sendo a **medição** do alvo de 40 dp do controle de transmissão.

### O que NÃO foi feito nesta rodada

Nenhum script de release do servidor foi executado — decisão do usuário na v1.2.76, registrada
acima.

## Rodada v1.2.78 — o último achado de acessibilidade

Décima quarta rodada da série. Fecha **a lista inteira de acessibilidade** da auditoria da
v1.2.73: com esta, os 12 achados estão corrigidos, recusados com medição ou registrados como
não verificáveis neste ambiente.

### Uma linha de navegação não se declarava como algo que se ativa

`SettingsItem` era `Modifier.clickable(...)` sem papel nenhum. Para quem enxerga, a linha tem
ícone, título, subtítulo e um chevron; para o leitor de tela era um bloco de texto — o usuário
não descobria que dava para tocar. Agora publica `Role.Button`, e o mesmo vale para as linhas
clicáveis de Detalhes (visão geral expansível e lista de faixas), Perfil (opções e troca de
usuário), Login (escolha de usuário) e Episódios.

A capa de temporada já marcava `selected` mas não dizia o que era; passou a ter papel.

### Oito grupos de opções não se declaravam como grupo

As listas de seleção usavam `selectable(role = Role.RadioButton)` linha a linha — o que está
certo — mas o `Column` que as continha não tinha `selectableGroup()`. Sem isso o serviço de
acessibilidade não sabe que as opções são **mutuamente exclusivas**: cada uma é anunciada como
um botão solto, sem relação com as outras. Os oito (três em Ajustes — tema, legendas, áudio — e
cinco no player — faixas, qualidade, velocidade, proporção, capítulos/segmentos) agora se
declaram como grupo.

### Provas por reversão (executadas na TV)

| reversão | teste que falha |
|---|---|
| `role = Role.Button` sai do `SettingsItem` | `SettingsSemanticsTest > aSettingsRowIsAnnouncedAsSomethingThatCanBeActivated` |
| `selectableGroup()` sai do diálogo de legendas | `... > theSubtitleLanguageDialogDeclaresOneExclusiveGroup` |
| `selectableGroup()` sai do diálogo de tema | `... > theThemeDialogDeclaresOneExclusiveGroup` |

O quarto teste é um **controle negativo**: monta uma `Column` sem grupo e afirma que a asserção
usada nos outros dois não encontra nada. Ele passa com a correção e com a reversão — de
propósito: é o que impede a asserção de virar um teste que sempre passa.

### Armadilha registrada (Atenção 17, de novo — e pior)

Três das quatro edições por script desta rodada **quebraram em silêncio**, e as três passaram
por motivos diferentes:

1. O `VideoPlayerScreen.kt` usa **CRLF** e os outros arquivos usam **LF**: o mesmo `old_string`
   casou em um e não no outro, e a substituição simplesmente não aconteceu (retorno: 0
   substituições).
2. Ao inserir um import, `$t -notmatch 'import ...Role'` deu **falso** para um arquivo que tinha
   `import ...role`: o `-match` do PowerShell é **case-insensitive** por padrão. O import certo
   nunca entrou.
3. Uma substituição de import colou dois imports na **mesma linha**
   (`import ...role import ...semantics`), e a compilação apontou o erro com clareza — sorte.

Nenhuma foi detectada pela escrita; todas pela leitura de volta ou pela compilação. A lição já
estava no handoff como Atenção 17 e vale repetida: **depois de qualquer substituição por script
em fonte Kotlin, leia o trecho de volta** — ou use a ferramenta de edição, que falha quando o
alvo não casa.

### O que ficou

**Acessibilidade: 12 de 12 fechados.** Nove corrigidos (a11y-1, 2, 3, 4, 5, 6, 7, 8, 10, 12 —
dez na verdade), dois recusados com medição (as instruções de rolagem) e um não verificável
neste ambiente: **o alvo de 40 dp do controle de transmissão**, que precisa de medição de
limites no aparelho — fica como pendência explícita, não como suposição.

Em dados continuam **M2**, **M4**, **M5** (não mexer sem sessão real), **M6**, **M8** e **M9** —
seis achados, todos com arquivo e linha na seção de backlog.

### O que NÃO foi feito nesta rodada

Nenhum script de release do servidor foi executado — decisão do usuário na v1.2.76, registrada
acima.

## Rodada v1.2.77 — o resto da lista de acessibilidade (e dois "achados" que não eram)

Décima terceira rodada da série. Fecha cinco achados de acessibilidade chegando ao fim da lista
da auditoria da v1.2.73 — e, no caminho, **recusa dois** que a auditoria agrupou pela forma mas
não se sustentam no conteúdo.

### O menu de ordenação não dizia qual opção estava valendo

A única marca do campo ativo era um `Icon(Check)` com `contentDescription = null`, e o
`DropdownMenuItem` do material3 1.4.0 não tem parâmetro `selected` — então o menu publicava nós
**sem estado nenhum**: o usuário ouvia os nomes das opções e nunca descobria qual estava
aplicada, nem em que direção. Cada item (campo e direção) agora publica `selected`.

### A arte do download repetia o título

`AsyncImage(contentDescription = entry.title)` numa linha que já mostra o mesmo título num `Text`
ao lado. No celular o leitor de tela parava duas vezes no mesmo título; na TV a linha mescla os
descendentes e a frase saía "Reproduzir X offline, X". A arte é decorativa.

### O aviso de "sem conexão" era lido duas vezes

O `Surface` tinha `contentDescription` **sem** `mergeDescendants`, e o `Text` filho dizia a mesma
frase — dois nós, a mesma sentença. Quem fala agora é o `Text`.

**Nota de método que vale a pena guardar:** a primeira versão do teste contava só os nós de
**texto** e passava com o defeito no lugar (medido: a suíte ficou verde na reversão). O
`contentDescription` de um contêiner não cria nó de texto, mas o leitor de tela lê os dois. A
asserção que discrimina é `hasContentDescription(frase)` contando **zero** — só ela falha na
reversão.

### O controle de transmissão tinha um nome inventado pelo app

O `Row` do controle de transmissão publicava uma frase fixa em pt-BR **além** do rótulo visível
"Transmitir" e da descrição que o próprio `MediaRouteButton` do Media3 já traz (localizada e
ciente do estado da conexão): a mesma ação anunciada com duas ou três palavras diferentes. O app
deixou de inventar o segundo nome, e o helper `castActionContentDescription` saiu junto.

**Isto apagou um teste que não testava nada:** `castActionExposesAUnifiedDescription` montava a
semântica **dentro do próprio teste** (um `Row` com a mesma descrição que o app publicava) e
verificava que ela existia. Ele nunca tocou em código de produção. Foi reescrito.

### Um servidor descoberto era anunciado como "salvo"

`SavedServerCard` é reusado para duas seções — "Encontrados nesta rede" e "Servidores salvos" —
com o mesmo rótulo de ícone. Um servidor apenas descoberto era anunciado como "Servidor salvo"
logo abaixo de um título que dizia o contrário. O cartão passou a saber de onde veio
(`ServerCardOrigin`) e o nome diz o mesmo que a seção.

### Dois achados recusados (com o motivo)

- **a11y-9, parte do player:** o auditor listou também a descrição da fileira de ações
  ("Ações do player; deslize horizontalmente para ver mais") como duplicação. **Não é:** nenhum
  filho diz que a fileira rola, então ela é a única frase que informa isso. Ficou.
- **a11y-9, parte do login:** mesma forma na constante
  `REGISTER_DIALOG_CONTENT_DESCRIPTION` ("Conteúdo do cadastro; deslize verticalmente para ver
  mais"). Também é instrução de rolagem, não repetição de texto filho. Ficou.

### Provas por reversão (cada uma executada, na TV)

| reversão | teste que falha |
|---|---|
| `selected` sai dos itens do menu de ordenação | 3 testes de `SortDropdownSemanticsTest` |
| arte do download volta a anunciar o título | `DownloadRowAnnouncementTest > theTitleIsTheOnlyDescriptionOfItself` |
| `contentDescription` volta ao aviso de offline | `PlayerAnnouncementTest > theOfflineNoticeIsAnnouncedExactlyOnce` |
| `serverCardIconDescription` volta a dizer "Servidor salvo" | 2 testes de `ServerCardOriginLabelTest` |

### Armadilha registrada

O primeiro teste instrumentado de Downloads **derrubou o processo de instrumentação** com
`android_getaddrinfo failed: EPERM`: a `AsyncImage` recebia uma URL `http://server/...` e o Coil
tentava resolver o host. O teste agora gera um PNG de 2×2 no cache do próprio app e usa um
`file:` URI — sem rede, sem DNS.

### O que ficou

A lista de acessibilidade caiu de 6 para **1 achado**: **a11y-11** (linhas clicáveis sem papel e
oito grupos de rádio sem `selectableGroup()`), que é o mais mecânico e o mais largo — seis
arquivos e oito listas de opção. O alvo de 40 dp do controle de transmissão continua **sem
medição**: não vou mexer em área de toque sem medir no aparelho.

Em dados continuam **M2**, **M4**, **M5** (não mexer sem sessão real), **M6**, **M8** e **M9**.

### O que NÃO foi feito nesta rodada

Nenhum script de release do servidor foi executado — decisão do usuário na v1.2.76, registrada
acima.

## Rodada v1.2.76 — quatro achados de acessibilidade, medidos na TV

Décima segunda rodada da série. Ataca a lista de acessibilidade da auditoria da v1.2.73 —
o bloco que sobrava inteiro — e fecha quatro dos dez achados.

### O botão ficava sem nome enquanto trabalhava

Detalhes trocava o ícone por um `CircularProgressIndicator` **sem descrição** durante cada
atualização (favorito, assistido, download). O nó ficava sem nome nenhum: o leitor de tela
anunciava apenas "botão", sem dizer o que estava acontecendo.

O contrato do componente já tinha a resposta — `busy` + `busyContentDescription` mostra o
spinner **e** mantém o nome — e é o padrão que Biblioteca, TV ao Vivo e Busca já usavam. As
três ações passaram a usá-lo, e o `enabled = !...` saiu: um pedido em andamento não é
indisponibilidade, e escurecer ali é exatamente o defeito do "símbolo de atualizar parado"
que o usuário relatou na TV na v1.2.50.

Para isso a fileira de ações saiu de dentro de `DetailHero` para `DetailActionRow` — sem
extração não havia como medir, porque o resto do herói exige imagem, gradiente e composição
locais.

### "Informações Técnicas" não dizia se estava aberta

O rótulo nunca muda e o chevron é decorativo (`contentDescription = null`), então o leitor de
tela anunciava "Informações Técnicas, botão" e nada sobre o conteúdo. Agora o bloco publica
estado: "Expandido" / "Recolhido", com papel de botão.

### O tipo de faixa era o nome do enum

`stream.type.name` devolve "Video"/"Audio"/"Subtitle" e isso era lido literalmente dentro de
"Informações Técnicas". Agora é "Vídeo"/"Áudio"/"Legenda" (mais "Imagem embutida", "Anexo",
"Dados" para os casos raros).

### Um cartão sem ação se anunciava como botão

`MediaCard` com `isClickable = false` — usado pela linha da lista da Biblioteca — publicava
`role = Role.Button` **sem ação nenhuma** e com o mesmo rótulo da linha clicável que o contém.
O leitor de tela encontrava o mesmo item duas vezes, e uma delas era um botão que não fazia
nada ao ser ativado. Sem ação, o card agora não fala: quem fala é a linha.

**Isto mudou um teste que existia:** `nonClickableMediaCard_doesNotExposeNestedClickAction`
afirmava que o nó "Abrir Linha de biblioteca" **existia** (só sem ação de clique) — ou seja, o
teste prendia o defeito. Foi reescrito como `nonClickableMediaCard_isNotAnnouncedAsAButton`.

### Provas por reversão (cada uma executada, na TV)

| reversão | teste que falha |
|---|---|
| `busy`/`busyContentDescription` voltam a ser `enabled = !...` | 4 testes de `DetailActionRowTest` |
| `stateDescription` sai de "Informações Técnicas" | `DetailActionRowTest > theTechnicalSectionSaysWhetherItIsOpen` |
| `streamTypeLabel` volta a `stream.type.name` | `DetailActionRowTest > theTechnicalSectionNamesTheStreamTypesInPortuguese` + `theStreamTypeLabelsAreNotEnumConstants` |
| `MediaCard` volta a publicar papel de botão sem ação | `MediaCardAccessibilityTest > nonClickableMediaCard_isNotAnnouncedAsAButton` |

Os quatro foram revertidos na mesma execução; como vivem em módulos diferentes
(`:design-system` × `:feature:item-detail`) e cada teste tem nome próprio, a atribuição é
direta. Uma nota honesta: o teste `aBusyActionRefusesTheSecondRequest` falha na reversão com
"Failed to inject touch input" — indireto, porque o nó que ele procura deixa de existir. A
asserção é legítima, mas a mensagem de falha é pior que a dos outros.

### O que ficou

A lista de acessibilidade caiu de 10 para 6 achados: **a11y-2** (transmissão anunciada três
vezes e a medição do alvo de 40 dp, que **precisa de medição no aparelho**), **a11y-7** (menu
de ordenação sem estado de seleção), **a11y-8** (arte do download repetindo o título),
**a11y-9** (aviso de offline lido duas vezes) e **a11y-10/11** (ícones que duplicam legendas,
linhas sem papel, grupos de rádio sem `selectableGroup()`).

No lado de dados continuam **M2**, **M4**, **M5**, **M6**, **M8** e **M9**.

### O que NÃO foi feito nesta rodada

O `AGENTS.md` do repositório foi alterado **durante** esta rodada e passou a exigir a release do
servidor junto (`build-update-package.ps1` + `publish-release.ps1`, com zip e instalador). As
regras 1 e 2 deste handoff proíbem esses dois scripts nesta conversa, e o worktree contém
alterações de servidor que são do usuário. O conflito foi levado ao usuário, que decidiu
**manter somente o APK** — nenhum script de servidor foi executado.

## Rodada v1.2.75 — a Home sem bibliotecas e a sala que nunca tocava

Décima primeira rodada da série. Fecha dois achados MÉDIA da auditoria da v1.2.73 — os dois
em que a tela afirmava algo falso — e adiciona a primeira guarda de contrato para o SyncPlay.

### A Home desenhava a conta sem biblioteca

`GetHomeFeedUseCase` montava as bibliotecas com `getOrDefault(emptyList())` e só desfazia isso
por um `throw` que exigia **todas** as outras seções vazias também. Com "Continuar Assistindo"
funcionando, uma falha de `/Views` produzia `HomeFeed(libraries = [])`: a Home aparecia com a
fileira de retomada e **nenhum** bloco de biblioteca — exatamente a tela de quem não tem
biblioteca nenhuma, sem erro, sem aviso e sem "tentar novamente". O mesmo mecanismo escondia a
falha de `getLatestItems` de uma biblioteca específica.

A falha agora viaja como estado (`HomeFeed.librariesError` → `HomeState.librariesError`), no
mesmo padrão que a v1.2.72 usou para as gravações, e a tela mostra o cartão com o motivo e o
botão de tentar de novo. O `throw` quando *nada* carrega continua: uma conta sem catálogo
nenhum merece a tela de erro cheia, não um aviso sobre uma Home vazia.

O cartão de erro da Home virou uma função só (`HomeLoadErrorCard`), usada pelas duas falhas —
antes existia um desenho para o feed inteiro e **nenhum** para as bibliotecas.

### "Entrar na sessão" levava um id que o servidor nunca manda

`GroupInfoDto` declarava `PlayingItemId` e `PositionTicks`, e a tela de salas navegava para o
player com esse id. O servidor não envia esses campos:
`MediaBrowser.Model/SyncPlay/GroupInfoDto.cs` tem apenas `GroupId`, `GroupName`, `State`,
`Participants`, `LastUpdatedAt`, `Ping` e `Host`, e `Group.cs` constrói o DTO exatamente com
esses membros. O id era **sempre nulo**, então a navegação nunca acontecia — e como a tela
troca para "Sair da sala atual" depois do join, entrar **parecia** ter funcionado.

Os campos fantasmas saíram do DTO, do modelo de domínio e do mapeamento; a navegação morta
saiu do `MulletaFlixNavHost`; `joinGroup` deixou de receber um callback com um valor que era
sempre nulo. E o texto da tela parou de prometer o que o app não faz: ele diz agora que a sala
e os participantes ficam no servidor e que **acompanhar a reprodução do grupo ainda não é
suportado neste app** — seguir o grupo exige o WebSocket do SyncPlay, que não existe aqui (só
os quatro endpoints REST: `New`, `Join`, `Leave`, `List`).

**Nota honesta de método:** esta parte é remoção de código morto e correção de texto, não um
defeito que uma reversão consiga discriminar — um teste não falha porque um campo que o
servidor não manda **deixou** de existir. Por isso a rodada acrescentou o que dá para prender:
um teste de contrato (`SyncPlayApiContractTest`) que decodifica com Moshi o payload que o
servidor realmente manda (incluindo `LastUpdatedAt`, `Ping` e `Host`, que o app ignora) e
confere as quatro rotas de sala.

### Provas por reversão (executadas)

| reversão | teste que falha |
|---|---|
| `librariesError` deixa de ser preenchido no use case | `UseCaseTest > GetHomeFeedUseCase surfaces a libraries failure instead of an empty list` + 2 de `HomeViewModelTest` |

### Armadilha do fake, registrada

Os dois testes novos de `HomeViewModelTest` falharam primeiro por um motivo que não era o
defeito: `FakeMediaRepository.unavailable()` **lança**, e `getLiveTvChannels` (chamado dentro
de `async` sem proteção) derrubava o feed inteiro antes de o aviso de bibliotecas existir.
Depois foi `getLatestItems` fazendo o mesmo no reload. O sintoma era `error = HTTP 500` em vez
de `librariesError` — e o teste dizia isso porque a mensagem de falha passou a incluir o
estado. Dois ajustes no fake, nenhum no código de produção.

### O que ficou

O backlog da auditoria caiu de 15 para 13 achados. Continuam abertos: **M2** (cache Room morto,
com chave errada se for ligado), **M4** (busca truncada em 30, descartando `TotalRecordCount`),
**M5** (índices de faixa escolhidos pelo servidor são descartados — não mexer sem sessão real),
**M6** (falha ao buscar o próximo episódio vira "a série acabou"), **M8** (`DeviceId` não
atômico), **M9** (API morta) e os **dez de acessibilidade** de leitor de tela.

### O que NÃO foi feito nesta rodada

O `AGENTS.md` do repositório pede, para toda alteração, `build-update-package.ps1` e
`publish-release.ps1` (servidor). O escopo declarado desta conversa é **somente o APK**, e
nenhum script de release do servidor foi executado.

## Rodada v1.2.74 — a sessão não pertencia a nenhum servidor

Décima rodada da série. Fecha os dois achados de gravidade ALTA que a auditoria da v1.2.73
deixou registrados e não corrigidos, por serem os dois de maior alcance e os mais fáceis de
errar.

### A credencial de um servidor era enviada para outro [ALTA]

`AuthRepositoryImpl.verifyServer` gravava o endereço novo **antes** de a verificação terminar, e
o token, o `userId` e o `serverId` viviam em chaves independentes do mesmo armazenamento.
`clearSession()` só rodava no logout, e `isAuthenticated` era derivado de "tem token e tem
usuário" — nunca do servidor. Sequência real: o usuário está logado no servidor A, abre "Trocar
servidor", a tela verifica B (e ela mesma se conecta sozinha, veja abaixo), o endereço passa a
ser B e **o token de A continua guardado**. A partir dali toda requisição — capas, `/Users/Public`,
progresso de reprodução, Home — vai para B levando a credencial de A. Se o usuário voltar sem
logar, a Home fica em "Sessão expirada" para sempre.

A decisão virou uma função pura, `shouldClearSessionForServerChange`, e a identidade que decide
é o **`serverId`**, não o endereço: o mesmo servidor é legitimamente alcançado por dois endereços
(LAN e DuckDNS) e trocar entre eles é exatamente o que o app faz sozinho quando o Wi-Fi cai —
deslogar ali seria um defeito novo. Só uma diferença **provada** derruba a sessão; quando um dos
lados não informa identidade (instalação antiga sem `SERVER_ID`, ou servidor que não publica
`Id`), a sessão é mantida, porque deslogar por não conseguir provar é pior que o risco evitado —
e o próximo login grava a identidade.

### A tela de servidores conectava sozinha no servidor errado [ALTA]

`automaticServerCandidate` devolvia `discoveredServers.firstOrNull()?.url`: numa rede com dois
servidores Jellyfin-compatíveis, o primeiro a responder tomava o lugar do servidor da conta — e
isso valia até para uma instalação já logada em outro. O casamento por identidade que
`preferredServerUrl` implementa (e que existe justamente para isso) era aplicado ao endereço
**exibido**, não ao que a tela conecta por conta própria. Agora o caminho automático usa a mesma
regra. Descoberta vazia continua caindo no endereço guardado, então a primeira execução sem
servidor na LAN segue verificando o endpoint público.

### Cancelamento deixou de virar erro [ALTA]

Todos os ~20 pontos de chamada de API dos repositórios usavam `runCatching`, que pega
`Throwable` — e cancelamento é um `Throwable`. Este app cancela de propósito o tempo todo
(`loadJob?.cancel()`, `refreshJob?.cancel()`, troca de tela), então um carregamento abandonado
virava `Result.failure` e a tela escrevia um erro para um trabalho que ninguém mais esperava.
Entrou `suspendRunCatching`, que relança `CancellationException`, e os 37 pontos de API passaram
a usá-lo.

O efeito mais grave era dentro de `verifyServer`: a restauração do endereço anterior é `suspend`
e grava no DataStore, então numa corrotina já cancelada ela lançava antes de escrever e **o
endereço do servidor que não respondeu ficava gravado**. Agora a restauração roda em
`withContext(NonCancellable)`.

### Provas por reversão (cada uma executada)

| reversão | teste que falha |
|---|---|
| `clearSession()` deixa de ser chamado ao verificar outro servidor | `AuthRepositoryImplTest > apontar para outro servidor derruba a sessao do servidor anterior` |
| `suspendRunCatching` volta a engolir `CancellationException` | `AuthRepositoryImplTest > cancelamento nao vira falha` |
| restauração do endereço volta a rodar dentro do cancelamento | `AuthRepositoryImplTest > a restauracao do endereco acontece mesmo com a corrotina cancelada` |
| `automaticServerCandidate` volta a pegar o primeiro descoberto | `ServerSelectionPolicyTest > automatic verification prefers the server it is already logged into` |

O teste da restauração merece uma nota de método: o fake de `setBaseUrl` **suspende de verdade**
(`delay(1)`), porque um mock que só atribui uma variável não tem ponto de suspensão — sem isso o
teste passaria com o defeito no lugar. Foi a primeira armadilha desta rodada.

### O que ficou

O backlog da auditoria da v1.2.73 caiu de 18 para 15 achados: **A2** e **A3** fechados, mais o
caminho automático de descoberta que a auditoria citava dentro de A2. Continuam abertos M1
(Home mentindo sobre bibliotecas), M2 (cache Room morto), M3 (SyncPlay que nunca toca), M4
(busca truncada em 30), M5 (índices de faixa do servidor descartados — não corrigir sem sessão
real), M6, M8, M9 e os dez de acessibilidade.

### O que NÃO foi feito nesta rodada

O `AGENTS.md` do repositório pede, para toda alteração, `build-update-package.ps1` e
`publish-release.ps1` (servidor). O escopo declarado desta conversa é **somente o APK**, e
nenhum script de release do servidor foi executado.

## Rodada v1.2.73 — duas auditorias independentes, seis defeitos, dois deles invisíveis sem leitor de tela

Nona rodada da série. Duas auditorias delegadas (leitura apenas, sem alterar arquivo) varreram
pela primeira vez **toda a camada de dados/domínio** e **a semântica de acessibilidade de todas
as telas**. Elas devolveram 22 achados; esta rodada corrigiu quatro deles, mais três defeitos
encontrados durante a própria implementação, e registrou os dezoito restantes no backlog com
arquivo e linha.

### O motivo da recusa existia e ninguém lia [ALTA]

Quando nenhuma fonte é compatível, o servidor responde **HTTP 200** com `MediaSources = []` e
`ErrorCode` preenchido (`NotAllowed`, `RateLimitExceeded`, `NoCompatibleStream` — conferido em
`MediaBrowser.Model/MediaInfo/PlaybackInfoResponse.cs` e em `MediaInfoHelper`). O DTO do app
desserializava `ErrorCode` desde sempre e **não havia um único leitor**: "sua conta não tem
permissão para isto" e "você atingiu o limite de transmissões simultâneas" chegavam à tela como
"nenhuma fonte de reprodução está disponível para esta mídia".

A frase agora vive no tipo (`PlaybackInfo.unavailableMessage`), não no chamador: assim um `?:`
esquecido numa tela não pode voltar a apagar a causa.

### "Você já está na versão mais recente" para uma resposta ilegível [MÉDIA]

`AppUpdateRepositoryImpl.parseReleases` engolia qualquer exceção de JSON e devolvia sucesso com
`isUpdateAvailable = false`. Um corpo truncado, um `502` com HTML, uma mudança de forma da API
do GitHub — tudo virava a afirmação de que não há atualização. Agora a falha sobe e vira
`updateErrorMessage`. A tolerância **por release** (`optJSONObject`, `optString`) continua: um
item estranho no meio da lista é o caso legítimo.

### A permissão da conta era só uma cor [ALTA — acessibilidade]

`ProfilePrivilegeRow` (4K, TV ao vivo, downloads) mostrava um visto verde ou um X vermelho e
nada mais. Com `contentDescription = null` não havia estado nenhum: o leitor de tela anunciava
"Transmissão 4K HDR / Dolby Vision" e o usuário não descobria se tinha a permissão. Agora o
estado é anunciado (`stateDescription`), e as duas variações são provadas na TV.

### Uma ação indisponível era anunciada como botão comum [MÉDIA — acessibilidade]

`MulletaFlixTopBarAction` mantém `enabled = true` de propósito (o `clickable(enabled = false)`
remove o `RequestFocus` e derruba o nó da sequência do D-pad — medido em rodada anterior) e
apenas escurece o ícone para α=0.38. O escurecimento não chega a um serviço de acessibilidade:
o nó era anunciado como "botão" e a ativação não fazia nada, sem explicação. Agora a
indisponibilidade é publicada (`disabled()`). Um pedido em andamento continua **não** sendo
indisponibilidade — ali quem fala é o spinner com a descrição de "ocupado".

### Dois toques no mesmo frame, três telas

O padrão que já produziu dois downloads do mesmo arquivo em Ajustes (v1.2.70) reapareceu em
`SyncPlayViewModel` (criar sala, entrar, sair) e em `AuthViewModel` (`login`, `register`): o
botão desabilitado é lido na composição, e o flag era marcado **dentro** da corrotina, então
dois toques no mesmo frame passavam os dois. No cadastro a consequência é pior que a duplicação:
a segunda resposta volta como "usuário já existe" e escreve esse erro por cima do sucesso que já
navegou. Os três fluxos agora marcam o flag antes de lançar a corrotina.

### "Tentar novamente" ganhou política [a dívida do harness do player]

A decisão estava em quatro `if` dentro de `PlayerViewModel.retryPlayback()`, função que já foi
palco de dois defeitos reais, e nenhum teste podia alcançá-la porque o `ViewModel` constrói o
próprio `ExoPlayer`. A decisão saiu para `PlaybackRetryPlan`, um tipo selado exaustivo: item
ausente, reprodução offline, rede caída (que precisa **avisar**, não retornar em silêncio) e
reinício na posição mais adiantada conhecida. O que ficou no `ViewModel` são só os efeitos.

### Provas por reversão (cada uma executada)

| reversão | teste que falha |
|---|---|
| precedência: `!hasPreparedMedia` antes de `isOfflinePlayback` | `PlaybackRetryPlanTest > um arquivo baixado nao tenta nada, com ou sem rede` |
| queda de rede volta a devolver `Nothing` | 2 testes de `PlaybackRetryPlanTest` |
| reinício volta a usar só a posição atual | 2 testes de `PlaybackRetryPlanTest` |
| guardas de toque removidas do `SyncPlayViewModel` | 4 testes de `SyncPlayViewModelTest` |
| guardas de toque removidas do `AuthViewModel` | 3 testes de `AuthViewModelTest` |
| `errorCode` volta a ser ignorado no repositório | `PlaybackRepositoryImplTest > a recusa do servidor chega com o motivo que ele mandou` |
| parse de releases volta a engolir a exceção | `AppUpdateRepositoryTest > an unreadable body is a failure, not a claim of being up to date` |
| `stateDescription` removido da linha de privilégio | 3 testes de `ProfilePrivilegeRowTest` (instrumentados, TV) |
| `disabled()` removido do `MulletaFlixTopBarAction` | `TopBarActionBusyTest > aDisabledActionIsAnnouncedAsDisabled` (instrumentado, TV) |

Os dois últimos foram revertidos juntos na mesma execução; como vivem em módulos e componentes
diferentes (`:feature:user` × `:design-system`) e cada teste tem nome próprio, a atribuição é
direta. O teste de pixel que já existia (`aDisabledActionLooksDisabled`) **passou** com o
`disabled()` removido — é exatamente por isso que ele não bastava: ele prova o escurecimento,
não o anúncio.

### O que ficou (backlog novo, com arquivo e linha)

Os dezoito achados restantes estão na seção **Backlog da auditoria da v1.2.73**, logo abaixo.
Os dois de maior valor são **A2** (a sessão não é escopada ao servidor: trocar de servidor mantém
o token antigo e o app passa a mandá-lo para o host novo) e **A3** (`runCatching` engole
`CancellationException` em todos os repositórios, e a restauração de URL de `verifyServer` roda
numa corrotina já cancelada, então não roda).

### O que NÃO foi feito nesta rodada

O `AGENTS.md` do repositório pede, para toda alteração, `build-update-package.ps1` e
`publish-release.ps1` (servidor). O escopo declarado desta conversa é **somente o APK**, e
nenhum script de release do servidor foi executado.

## Rodada v1.2.72 — o que sumia sem dizer nada

Oitava rodada da série. Ela não tem um defeito grande: tem cinco defeitos pequenos com a
mesma forma — **a tela afirma algo que o app não faz, e ninguém percebe porque nada
falha**. É o tipo de defeito que só aparece quando alguém compara o que está escrito com o
que está implementado.

### Gravações de TV ao vivo: só a primeira página existia

`getRecordings` pedia `Limit = 20` com `StartIndex` fixo em zero e **descartava
`TotalRecordCount`**. Quem tivesse mais de vinte gravações simplesmente não via o resto: a
lista parecia completa. Agora ela pagina até o fim (`LIVE_TV_RECORDINGS_PAGE_SIZE = 100`,
`MAX_LIVE_TV_RECORDINGS_PAGES = 20`) com a mesma regra do resto do app —
`hasMorePagesWithUnknownTotal`, que decide por *"a página veio cheia?"* em vez de confiar
num total — e remove ids repetidos entre páginas (`distinctBy { id }`), porque uma
gravação que passa de uma janela para a seguinte aparece em duas páginas.

### O erro das gravações sumia

`LiveTvGuide.recordings` era montado com `getOrDefault(emptyList())`: uma falha de rede
virava lista vazia, indistinguível de *"você não tem gravações"*. O campo
`recordingsError` existia desde a v1.2.70 e **nunca era escrito**. Agora o use case
preserva a exceção, o ViewModel a expõe e a tela desenha o aviso com "Tentar novamente" —
que é a diferença entre um usuário que tenta de novo e um que conclui que perdeu as
gravações.

### Ajustes: três linhas que mentiam

- **"Qualidade de Download: 1080p (Original)"** não tinha chave no repositório, ninguém a
  lia e nenhum download a consultava. Foi removida junto com o campo de estado que só ela
  usava — uma linha fabricada é pior que uma linha ausente, porque promete um controle.
- **O menor tamanho de legenda aceito não podia ser escolhido.** A faixa do repositório é
  50..200; o diálogo oferecia 75..200. Um valor que a camada de dados preserva e o diálogo
  não mostra é um valor que o usuário não consegue reescolher.
- **O botão de voz parecia desabilitado enquanto ouvia.** Usava `enabled = !isListening`
  onde o contrato do componente pede `busy`; o ícone e o rótulo de "Ouvindo…" existiam para
  isso.

### O diálogo de ordenação cortava as últimas linhas

São oito campos de ordenação e cinco idiomas numa `Column` sem rolagem, dentro da janela
do diálogo. Numa janela baixa — a da TV — as últimas linhas são compostas fora dos
limites: recortadas, invisíveis, e inalcançáveis **também pelo controle remoto**, porque o
recorte corta o foco junto com o desenho. A lista passou a ter teto explícito e rolagem
(`ChoiceDialogOptions`, `heightIn` **antes** de `verticalScroll` — na ordem inversa o
modificador de rolagem mede o filho com altura infinita e devolve o tamanho do filho,
então nada rola).

### Dois dos oito temas não faziam nada

`WMC` e `AppleTV` mapeavam direto para `DarkColorScheme`: escolher "Apple TV" deixava a
interface idêntica a "Escuro (Padrão)", com a tela afirmando o contrário. Agora existem
`WmcColorScheme` e `AppleTvColorScheme` de verdade.

O mapeamento saiu do `when` do composable para `staticColorSchemeFor(variant)`, e o motivo
é o teste: a primeira versão do `ThemeVariantsTest` comparava as **constantes**, então
reverter o mapeamento (as duas variantes voltando para `DarkColorScheme`) continuava
passando. Um teste que não distingue o defeito não é prova. Lendo o mapeamento, a reversão
falha como deve.

### Provas por reversão

Cada linha abaixo foi executada: o código foi revertido ao defeito, o teste nomeado
falhou, e o código foi restaurado.

| reversão | teste que falha |
|---|---|
| paginação das gravações volta a uma página só | 4 testes de `LiveTvRepositoryImplTest` |
| `recordingsError` volta a ser `getOrDefault(emptyList())` | 2 testes de `LiveTvViewModelTest` |
| `subtitleFontSizeChoices` volta a começar em 75 | `the smallest subtitle size the repository accepts can be chosen` |
| `WMC` e `AppleTV` voltam para `DarkColorScheme` | `ThemeVariantsTest > no two themes look the same` |
| `ChoiceDialogOptions` perde `heightIn` + `verticalScroll` | `ChoiceDialogOptionsTest > aListTallerThanItsWindowIsScrollable` e `> everyOptionCanBeBroughtIntoView` (instrumentados, AVD `MulletaflixApi35`) |

### O que ficou

- **Não verificado no aparelho:** o caminho de abrir canal ao vivo (v1.2.71) continua sem
  prova real — exige tuner e sessão; a confirmação do 429 idem.
- **Não verificado no aparelho:** o aviso de erro das gravações foi verificado por teste de
  ViewModel, não por tela com servidor devolvendo erro.
- **BAIXA:** a lista de gravações não tem estado vazio próprio — sem erro e sem gravações,
  a seção simplesmente não aparece (o que é correto, mas é silencioso).
- **`PlayerViewModel` continua sem harness comportamental**: só as políticas extraídas têm
  teste.
- **SyncPlay sem cliente WebSocket**; adoção de downloads anteriores à 1.0.6 sem prova em
  aparelho com downloads antigos.

### O que NÃO foi feito nesta rodada

O `AGENTS.md` do repositório pede, para toda alteração, `build-update-package.ps1` e
`publish-release.ps1` (servidor). O escopo declarado desta conversa é **somente o APK**, e
nenhum script de release do servidor foi executado — nem nesta rodada nem nas anteriores.

## Rodada v1.2.71 — o canal de tuner que não reproduzia, e a qualidade que a tela não sabia mostrar

Sétima rodada da série. Fecha os dois achados mais graves que sobraram das auditorias da
v1.2.70.

### Canal ao vivo de tuner nunca abria

Um canal de tuner não é arquivo. O `PlaybackInfo` devolve a fonte com
`RequiresOpening = true` e **sem** `LiveStreamId`; a rota `/Videos/{id}/stream` só
encontra o feed depois de `POST LiveStreams/Open`, que devolve a fonte já aberta com o
id. O app tocava canal como VOD e nunca chamava essa rota — nenhuma ocorrência de
`LiveStreams/Open` existia no módulo — então qualquer canal cujo `MediaSource` não fosse
HTTP estático (UDP, HDHomeRun) falhava no "Assistir". O cliente web oficial faz
exatamente esse passo (`playbackmanager.ts`, `getLiveStream`).

Agora `PlaybackRepositoryImpl.getPlaybackInfo` abre a fonte quando ela exige
(`shouldOpenLiveStream`: `RequiresOpening && LiveStreamId` vazio), usa a fonte devolvida
— id e `LiveStreamId` — e monta a URL com `LiveStreamId=` nas duas variantes (direta e
transcodificação). Uma fonte já aberta por outra sessão é reaproveitada; um filme comum
não passa por essa rota; e a falha ao abrir é propagada, porque um player apontado para
uma URL quebrada é mais difícil de entender do que um erro dizendo que o canal não abriu.

### A qualidade tinha duas definições que discordavam

O player guarda a resolução que o título realmente oferece — um track de 360p é
guardado como `360p` (`PlayerOptions.qualityOptions` → `NNNp`), deliberadamente. A tela
de Ajustes conhecia seis presets e `normalizeDefaultQuality` respondia `Automático` para
qualquer outro valor. Resultado: escolher 360p enquanto assiste e abrir Ajustes mostra
"Automático", e o diálogo não tem linha para o valor real — ele fica invisível e não pode
ser reescolhido.

A regra virou uma definição só, em `:domain/model/PlaybackQuality.kt` (presets, faixa
144..4320, normalização e rótulo por altura), e tanto o player quanto os Ajustes leem
dela. É o mesmo movimento da política de legenda (v1.2.64) e do catálogo de ordenação:
duas cópias do mesmo conjunto é o defeito que este código mais produz.

### Um valor guardado fora do catálogo agora tem linha no diálogo

O mesmo problema aparecia com idioma: o player grava o código cru da faixa (`jpn`) e
`MediaLanguage.label("jpn")` = "JPN", que não está na lista do diálogo — nenhuma opção
ficava marcada e confirmar outra substituía a escolha real. `choicesIncludingCurrent`
acrescenta o valor guardado às opções quando o catálogo não o conhece, usado pelos
diálogos de qualidade, áudio e legendas.

### Provas por reversão

| reversão | testes que falham |
|---|---|
| `shouldOpenLiveStream` sempre falso e URL sem `LiveStreamId` | 4 (`PlaybackRepositoryImplTest` ×2, `PlaybackUrlFactoryTest` ×2) |
| `normalizeDefaultQuality` volta à lista de seis presets | `a quality the player can store is shown and kept by the settings screen` |
| `choicesIncludingCurrent` devolve a lista intacta | `a stored value outside the catalogue still gets a row in the dialog` |

### O que ficou

- **MÉDIA — gravações limitadas a 20**, sem paginação, sem estado de erro
  (`recordingsError` nunca é escrito) e com a falha engolida por `getOrDefault(emptyList())`.
- **MÉDIA — dois dos oito temas são idênticos ao escuro** (`WMC` e `AppleTV` mapeiam para
  `DarkColorScheme`).
- **BAIXA — "Qualidade de Download: 1080p (Original)" é valor fabricado**, sem chave no
  repositório e sem consumidor.
- **BAIXA — faixa de tamanho de legenda aceita (50..200) ≠ oferecida (75..200)**; o botão
  de voz usa `enabled` onde o contrato do componente pede `busy`; `ChoiceDialog` lista
  opções numa `Column` sem `verticalScroll` (pode cortar em tela baixa).
- **Não verificado no aparelho:** o caminho de abrir canal ao vivo não pode ser exercitado
  aqui — exige um tuner real e uma sessão. A prova é o teste com API falsa mais o contrato
  lido nos dois lados (servidor e cliente web).

## Rodada v1.2.70 — TV ao vivo, busca, ajustes e o primeiro alvo de toque medido

Sexta rodada da série. Duas auditorias delegadas novas (TV ao vivo; Busca e Ajustes)
levantaram 15 achados; esta rodada corrigiu os sete primeiros.

### TV ao vivo

**O guia era uma página só.** `LiveTv/Programs` era chamado com `Limit = 50` e
`StartIndex` fixo em zero, e o repositório consumia `items` ignorando
`TotalRecordCount`. O servidor ordena por data de início e corta com `Take(Limit)`, então
com centenas de canais os cinquenta programas devolvidos pertenciam a um punhado deles e
dezenas de canais apareciam sem nenhum programa. Agora cada lote de canais é paginado até
o fim, com a mesma regra `hasMorePages` do resto do app e um teto de segurança
(`MAX_LIVE_TV_GUIDE_PAGES`).

**O programa no ar ficava de fora.** A janela era `MinStartDate = agora` +
`MaxEndDate = agora + 24 h`, isto é, "começa depois de agora" — o filme que começou às
19:30 e vai até 21:30 não aparecia às 20:00, justamente o que está sendo assistido. O
servidor entende melhor o par de sobreposição: `MinEndDate` (fim depois de agora) e
`MaxStartDate` (início antes do fim da janela). O contrato mudou de nome junto
(`windowStartUtc`/`windowEndUtc`) para o nome no app não mentir sobre o filtro.

**Guia de um retrato de canais que já mudou.** O refresh invalidava a requisição do guia
mas mantinha `programs`; com a nova requisição falhando, a lista antiga continuava na
tela e dava para agendar um programa de um canal que sumiu do snapshot. Agora os
programas são descartados quando o conjunto de canais muda.

**Horário em UTC.** O rótulo da gravação removia o `Z` e trocava o `T` por espaço, o que
parece hora local mas é UTC: 20:00 em Brasília aparecia como 23:00. `recordingStartLabel`
converte para o fuso do aparelho e devolve `null` para uma data ilegível, em vez de
desenhar um horário errado. `SimpleDateFormat` e não `java.time` porque o `minSdk` é 24
sem desugaring (Atenção 14).

### Acessibilidade, medida no aparelho

O botão de reproduzir sobre a capa do episódio tinha 40 dp de alvo interativo, abaixo do
mínimo do Material de 48 dp, e fica em cima da arte que o usuário também toca para abrir
o episódio. O teste instrumentado mede o **nó clicável** e falha abaixo de 48 dp.

Uma tentativa de manter o disco visual em 40 dp com alvo de 48 dp (`size(48).padding(4)`)
foi medida e **rejeitada**: o `padding` encolhe o `clickable` que o componente aplica, e o
alvo continuou 40 dp. O disco passou a ter o mesmo tamanho do alvo.

### Busca e Ajustes

- **A busca digitada nunca gravava histórico.** Só `search()` (Enter, toque no histórico,
  voz) chamava o repositório; o caminho normal — digitar e esperar o debounce — pesquisava
  no servidor e não deixava rastro, então "Suas buscas recentes aparecerão aqui."
  continuava vazio. Agora uma busca com resultado entra na lista; uma busca sem resultado
  não entra, de propósito (é erro de digitação ou título que o servidor não tem).
- **"Limpar Todos os Dados Locais" não apagava o histórico de busca**, que vive em outro
  armazenamento e reaparecia no próximo login do mesmo usuário.
- **Dois toques no mesmo frame em "Atualizar Agora"** iniciavam dois downloads no mesmo
  arquivo, e o segundo apaga o arquivo que o primeiro abriu. A verificação de atualização
  já tinha a guarda de reentrância; o download não.

### Provas por reversão (9 testes nomeados)

| reversão | teste que falha |
|---|---|
| guia volta a uma página só e sem repontamento de janela | 4 testes de `LiveTvRepositoryImplTest` |
| `programs` deixa de ser limpo quando os canais mudam | `a guide from a replaced channel snapshot is dropped` |
| rótulo volta a remover o `Z` | 4 testes de `RecordingStartLabelTest` |
| `performSearch` deixa de gravar | `a search typed into the box is remembered` |
| download volta a marcar o flag dentro da corrotina | `a second update download in the same frame is ignored` |
| limpeza deixa de limpar o histórico | `clearing all local data also clears the search history` |

### O que ficou dos 15 achados (backlog)

- **ALTA — canal de tuner não abre:** o app toca canal como VOD e nunca chama
  `LiveStreams/Open`; um `MediaSource` com `RequiresOpening = true` (UDP/HDHomeRun) não
  reproduz. O cliente web faz esse passo.
- **MÉDIA — gravações limitadas a 20**, sem paginação, sem estado de erro (`recordingsError`
  nunca é escrito) e com a falha engolida por `getOrDefault(emptyList())`.
- **MÉDIA — qualidade do player não é editável em Ajustes:** o player aceita e grava
  qualquer `NNNp` (144..4320), mas `normalizeDefaultQuality` só conhece seis valores e
  colapsa para "Auto" — "360p" fica invisível e irrestaurável.
- **MÉDIA — idioma fora do catálogo:** o player grava o código cru (`jpn`), o rótulo vira
  "JPN", que não está na lista do diálogo, e nenhuma opção fica marcada.
- **MÉDIA — dois dos oito temas são idênticos ao escuro** (`WMC` e `AppleTV` mapeiam para
  `DarkColorScheme`).
- **BAIXA — "Qualidade de Download: 1080p (Original)" é valor fabricado**, sem chave no
  repositório e sem consumidor.
- **BAIXA — faixa de tamanho de legenda aceita (50..200) ≠ oferecida (75..200)** e o botão
  de voz usa `enabled` onde o contrato do componente pede `busy`.

## Rodada v1.2.69 — o repontamento de playback sai do ViewModel e ganha teste

Última ligação da série das auditorias, e o fim de uma dívida declarada em duas rodadas
seguidas: "o coletor em `PlayerViewModel` só está verificado por compilação".

### O obstáculo, e como foi contornado

`PlayerViewModel` cria o próprio `ExoPlayer` (`private val localPlayer: ExoPlayer =
ExoPlayer.Builder(context).build()`) e não pode ser montado fora de um app com grafo
Hilt. Falsificar a interface `Player` do Media3 inteira não era razoável.

O contorno foi inverter a dependência: a operação precisa de **cinco** coisas do player,
e são essas cinco que viraram a interface `RetargetableStream` (`preparedUrl`,
`isOffline`, `positionMs`, `isPlaying`, `replaceSource`). A ordem dos passos foi para
`PreparedStreamRetarget`, e o que ficou dentro do ViewModel é o adaptador — poucas
linhas que traduzem `ExoPlayer` para essa interface, e a única parte que nenhum teste
alcança.

### Dois guardas que o teste mostrou serem load-bearing

1. **Reprodução offline.** A URL de um download é a URL **http** original: o Media3 a
   reproduz através do cache compartilhado, que é indexado por ela. Sem o guarda
   `isOffline()`, uma troca de LAN para público **durante** a reprodução offline
   repontaria essa URL para outro host, a busca no cache erraria e o vídeo passaria a
   ser baixado da rede (ou falharia) — a correção viraria defeito. O teste usa a forma
   realista (URL http + `offline = true`), não um arquivo local, que a política já
   rejeitaria sozinha.
2. **Leitura do token.** O fluxo de endereço emite a cada gravação de preferência,
   quase sempre sem mudança de host. A primeira versão lia o token como argumento
   **antes** da decisão; o teste `the credential is only read when a move is going to
   happen` falhou e expôs a diferença entre o que o código dizia e o que fazia. Agora a
   pergunta "precisa mover?" (`shouldRetargetPreparedStream`) vem primeiro, e ela é a
   mesma definição usada para decidir para onde mover.

### Provas por reversão

| reversão | resultado |
|---|---|
| remove `if (stream.isOffline()) return false` | `an offline download is never re-pointed at a server` falha |
| remove a pergunta prévia e lê o token como antes | `the credential is only read when a move is going to happen` falha |

### Nota de método

Um comando de edição por `Set-Content`/`-replace` do PowerShell destruiu um arquivo de
teste (juntou todas as linhas em uma). Foi reescrito e virou a Atenção 13 do handoff:
editar fonte Kotlin com as ferramentas de edição, nunca com `Get-Content`/`Set-Content`.

## Rodada v1.2.68 — a ligação do retry de download, provada em aparelho

Quarto item das auditorias, e a segunda ligação provada em teste instrumentado.

### O que faltava

A v1.2.66 corrigiu o retry de download, mas só a **decisão** estava provada: o
`Media3DownloadRepository` não pode ser construído fora de um app com grafo Hilt
(depende do `DownloadManager` do sistema), então a ligação ficou "verificada por
compilação". Duas mudanças fecham isso.

### 1. Um único ponto de construção

A montagem do `DownloadRequest` saiu para `downloadRequestFor(requestId, uri, baseUrl,
accessToken)`, e **os dois** caminhos (enfileiramento e retry) passam por ela. Antes
eram dois `DownloadRequest.Builder` no arquivo, cada um com a sua chance de esquecer o
repontamento.

### 2. A guarda de fonte

`DownloadRequestRetargetGuardTest` (`:app`) lê o próprio fonte e falha se:

- houver mais de um `DownloadRequest.Builder(` no arquivo — um segundo é um pedido
  montado com a URL recebida, que envelhece;
- o corpo de `downloadRequestFor` deixar de passar por `retargetMediaUrl(`;
- `enqueueWithMetadata` ou `retry` pararem de chamar `downloadRequestFor(`.

Varredura de fonte porque nenhuma asserção de runtime prova a **ausência** de um
caminho.

### 3. O teste instrumentado

`DownloadRequestRetargetTest` (4 testes, AVD de TV) constrói o pedido com os tipos
reais do Media3 e afirma: host e porta do endereço atual, caminho preservado, token
gravado substituído, `MediaSourceId`/`Static` intactos, id estável, e que uma sessão
ainda não lida deixa a URL gravada em paz.

### Provas por reversão

| reversão | resultado |
|---|---|
| `retry` volta a montar o pedido com `DownloadRequest.Builder(requestId, Uri.parse(uri))` | os 2 testes da guarda falham |
| `downloadRequestFor` volta a usar `Uri.parse(storedUri)` | 2 dos 4 instrumentados falham (`aRequestQueuedAtHomeIsRetriedAgainstTheAddressInUse`, `theRequestKeepsThePathAndTheOtherParameters`) |

### O lint pegou o que o build não pegou

A primeira tentativa de release reprovou no lint com **3 erros** de
`UnsafeOptInUsageError`: a função de topo extraída usa `DownloadRequest`, que o Media3
marca como opt-in, e estava fora da classe anotada com `@UnstableApi`. O `assembleRelease`
**passou** com os 3 erros presentes — só o `lintDebug` os via. Anotação adicionada,
lint de volta a 0 erros, APK reconstruído, e o digest publicado é o do build corrigido.

Registrado como Atenção 12: publicar sem rodar lint publica um erro conhecido.

### Ainda não provado

A ligação do repontamento de **playback** (a outra metade da v1.2.66): o
`retargetPreparedStreamUrl` tem testes de JVM, mas o coletor em `PlayerViewModel` não.
O caminho agora é claro — extrair a peça, falsificar a interface pequena — e o
obstáculo é o tamanho da interface `Player` do Media3. Fica como o próximo item da
mesma família.

## Rodada v1.2.67 — o cache de capas jogava a grade fora a cada troca de endereço

Terceiro item das auditorias da v1.2.65, e o primeiro em que a **ligação** de uma
correção ficou provada em aparelho, não só por compilação.

### O defeito

O Coil chaveia memória e disco pela URL do pedido, e essa URL carrega o endereço do
servidor **e** o token da sessão. Consequência: a LAN (`192.168.15.9:8096`) e o
endereço público (`mulletaflix.duckdns.org:8096`) do **mesmo** servidor eram duas
entradas para a mesma capa. Entrar e sair de casa descartava a grade inteira e deixava
duas cópias de cada pôster no disco até o evictor por tamanho agir. Reautenticar tinha
o mesmo efeito, porque o token também muda.

### A chave

`canonicalImageCacheKey(url, serverId)` mantém o que identifica a **imagem** e descarta
o que identifica o **caminho até ela**:

- a identidade do servidor (`getServerId()`), para dois servidores diferentes não
  compartilharem entrada — os ids são GUIDs, então colisão é improvável, mas o app
  guarda vários servidores salvos e "improvável" não é "impossível";
- o caminho e a query **sem a credencial**. A query fica de propósito: `?tag=` faz
  parte da identidade da imagem (o servidor devolve outra figura quando a tag muda) e
  `?width=`/`?height=` distinguem miniatura de pôster inteiro — descartar a query
  entregaria um thumbnail a um pedido de capa cheia.

A credencial sai pela **mesma** regra que a redação e o repontamento usam, então
"o que é um token" continua tendo uma única definição no app.

### Como chegou ao Coil

Não em cada chamada: um `Interceptor` registrado no `ComponentRegistry` do
`ImageLoader` (`app/.../MulletaFlixApp.kt`) reescreve o pedido com
`memoryCacheKey`/`diskCacheKey` antes da consulta ao cache. Um lugar só, e nenhuma tela
precisa lembrar de nada.

O interceptor virou função de topo (`artworkCacheKeyInterceptor(currentServerId)`) e
não uma propriedade privada da `Application`, justamente para poder ser exercitado por
teste: a `Application` não pode ser construída fora de um app com grafo Hilt.

### Provas

- **JVM (`MediaImageUrlTest`, 5 testes):** mesma chave para LAN e público; mesma chave
  entre dois tokens (isola a remoção da credencial da normalização de endereço); `tag`
  e `width` diferentes dão chaves diferentes; servidores diferentes nunca compartilham;
  recurso local/file mantém a chave padrão.
- **Instrumentado (`ArtworkCacheKeyInterceptorTest`, 4 testes, AVD de TV):** prova que
  a chave **chega** ao Coil nos dois caches, que LAN e público produzem a mesma chave
  (com `assertNotNull`, senão dois nulos passariam), que um drawable local segue sem
  chave e que um pedido sem chave canônica é entregue sem ser reconstruído. Usa uma
  `Interceptor.Chain` falsa — cinco membros, falsificação honesta em vez de simulação
  da biblioteca.
- **Reversões:** com `canonicalImageCacheKey` devolvendo a URL crua (o comportamento do
  Coil sem o interceptor), 4 dos 5 testes de JVM falham; com o interceptor entregando o
  pedido sem chave, `aCoverGetsTheSameCanonicalKeyOnBothCaches` falha no aparelho.

### Ainda não provado

Os coletores que **ligam** o repontamento de download e de playback (rodada v1.2.66)
continuam sem teste de comportamento: `PlayerViewModel` não tem harness e
`Media3DownloadRepository` depende do `DownloadManager`. Foi provado agora que esse
harness é viável e barato quando a peça sob teste é extraída para uma função —
`PlayerViewModel` é o candidato seguinte, e exige decidir como falsificar um `Player`.

## Rodada v1.2.66 — os dois defeitos de troca de endereço que a auditoria deixou registrados

Segunda passada sobre os achados da v1.2.65. São os dois casos em que uma URL absoluta
sobrevive ao endereço com que foi montada.

### 1. "Tentar novamente" reenviava o endereço da LAN para sempre

`Media3DownloadRepository` grava a URL de reprodução no índice do Media3 quando o
download é enfileirado, e o `retry` readicionava exatamente esse URI — com o host e o
token daquele momento. O app troca sozinho entre a LAN e o público quando a rede some
(`LanServerRecovery`), então um download que falhou em casa era retentado contra um
endereço que o aparelho já tinha deixado, indefinidamente e sem dizer por quê.

Correção: o repositório passou a acompanhar `getBaseUrl()` e `getAccessToken()` (dois
coletores, como já fazia com o usuário) e tanto o enfileiramento quanto o retry passam
a URL por `retargetMediaUrl` antes de montar o `DownloadRequest`.

### 2. O episódio em andamento morria ao sair do alcance do Wi-Fi

O player recebe a URL uma única vez, no `setUri`, e só busca outra quando o título é
reaberto — a mesma troca automática de endereço deixava o vídeo abrindo conexão num
endereço inalcançável. A reprodução só voltava saindo e reabrindo o título.

Correção: `PlayerViewModel` observa o endereço e, quando ele muda de verdade,
repontar o stream já preparado — `setMediaItem(novoItem, posição)` (a posição vai na
chamada, em vez de um `seekTo` depois, para o player não carregar do zero e pular),
`prepare()`, e `play()` se o `playWhenReady` estava ligado. O flag
`restoreTrackSelectionOnNextTracksChange` é ligado porque uma fonte nova derruba as
faixas de áudio/legenda escolhidas.

### 3. Uma URL absoluta já autenticada ganhava uma segunda credencial

`resolveMediaUrl` só reconhecia `api_key` ao decidir se anexava o token, então uma URL
absoluta que o servidor já tinha autenticado como `ApiKey` ou `X-Emby-Token` voltava
com dois parâmetros de credencial. Passou a usar a mesma regra única de "o nome contém
`key` ou `token`".

### A decisão virou função pura, e é isso que está testado

`retargetPreparedStreamUrl` devolve a URL nova **ou null**, e cada motivo de null é
deliberado: arquivo local (download offline não se reponta para servidor), endereço em
branco ou ilegível (a sessão ainda não foi lida — chutar é pior que a URL em uso) e
host+porta já iguais (`getBaseUrl()` emite a cada gravação de preferência, e
repreparar um stream saudável só o faria engasgar). Host e porta são comparados do
mesmo jeito que `retargetMediaUrl` reescreve, para "não precisa mover" e "para onde
moveria" não poderem discordar.

### Provas por reversão

| defeito | reversão | resultado |
|---|---|---|
| 1 e 2 | `retargetMediaUrl` volta a devolver a URL recebida | 6 dos 7 testes de `MediaImageUrlTest` falham (`retargets a stored download…`, `replacing the stored credential…`, `adds the credential…`, `keeps the absolute path…`, `drops the old port…`, `an absolute url keeps one credential…`) |
| 2 | `retargetPreparedStreamUrl` sempre devolve null | 3 dos 6 testes de `StreamRetargetPolicyTest` falham (`a stream prepared on the lan moves…`, `the port participates…`, `the retargeted url is the one the download path would use`) |

Os testes que continuam passando sob reversão são os que afirmam "não mexa" — é o
comportamento que a reversão também tem, então eles não discriminam e não são citados
como prova.

### O que NÃO está provado

A **ligação** (o coletor em `Media3DownloadRepository` e o coletor em `PlayerViewModel`)
é verificada por compilação e pelos testes das políticas puras. Não existe teste
instrumentado que observe o `DownloadRequest` enfileirado nem um `Player` falso em
`:feature:player` — `PlayerViewModel` não tem harness de teste neste projeto, e
`Media3DownloadRepository` depende do `DownloadManager` do sistema. Criar esse harness
é o próximo passo natural para provar as duas ligações de ponta a ponta.

## Rodada v1.2.65 — duas auditorias independentes, seis defeitos, três deles invisíveis até medir

Rodada sem relato de usuário: dois auditores delegados varreram, em paralelo e sem
editar nada, (A) paginação da Biblioteca, refresh e troca de endereço LAN↔DuckDNS e
(B) vazamento de token/PIN/senha em logs, erros e UI. Os seis achados abaixo saíram
dessa leitura; os que sobraram estão no backlog com a prioridade sugerida.

### 1. "Aleatório" + paginação por offset: capas repetidas e títulos que nunca apareciam

`SortBy=Random` é traduzido pelo servidor para `ORDER BY RANDOM()`
(`Jellyfin.Server.Implementations/Item/OrderMapper.cs`). Cada requisição sorteia uma
permutação **nova**, então `Skip(offset)` da página 2 corta um embaralhamento
diferente: o acumulado ganhava repetidos, perdia títulos, e o teste "carregado < total"
encerrava o catálogo antes de mostrá-lo. Nenhuma tentativa de paginar isso funciona —
offset e ordem aleatória são incompatíveis.

Correção: `supportsOffsetPaging(sortBy)` no `:domain` diz que essa ordenação não tem
página 2, e a primeira resposta (que traz o total) é usada para pedir a lista inteira
numa requisição só, com teto `MAX_SINGLE_REQUEST_ITEMS = 1000` para não transformar
uma tela numa alocação sem limite. `loadMore()` também recusa essa ordenação.

### 2. Página sobreposta repetia o mesmo item na grade

`items + newItems` pressupõe páginas disjuntas e imutáveis. Uma varredura da
biblioteca que insere um título no topo de "Data de Adição" desloca a janela inteira,
e o fim da página N volta como começo da N+1. A grade renderiza `key = item.id`, então
o duplicado dava duas Composable com a mesma identidade.

Correção: `appendDistinctBy` no `:domain`, usado por Biblioteca e Minha Lista.

### 3. O offset passou a ser contado por item **entregue**, não por item visível

Consequência do item 2, e um defeito que eu teria introduzido se tivesse parado ali:
deduplicar faz `items.size` parar de crescer, e o offset era `items.size`. Uma página
inteira de repetidos deixaria o offset parado e a lista pediria a mesma janela para
sempre. Agora `fetchedItemCount` conta o que o servidor entregou e é ele que vira
offset; o `items.size` fica só para desenhar.

### 4. A redação do token era uma lista de três grafias

`redactToken` conhecia `api_key`, `ApiKey` e `X-Emby-Token`, e o teste enumerava
**as mesmas três** — ou seja, passava com e sem o defeito, o erro de método da
v1.2.58 outra vez. Uma grafia que ninguém lembrou (`api_token`, `apikey`, `token`)
ia inteira para o logcat.

A regra foi invertida: qualquer parâmetro cujo nome **contenha** `key` ou `token` é
tratado como segredo, mais a senha embutida em `esquema://usuario:senha@host`. A lista
negra agora erra para o lado seguro (redigir demais), que é o lado certo para um
diagnóstico de capa.

### 5. Um código de falha de playback ainda não mapeado podia mostrar a URL com token

O `fallback` é o `localizedMessage` do Media3, e alguns códigos embutem a URL que
falhou — que na reprodução carrega o token. Todos os códigos que fazem isso têm
mensagem curada, então o caminho é inalcançável hoje; `redactToken` foi aplicado ao
fallback para que um código que a lista ainda não conhece não ponha token na tela.

### 6. A guarda de log cobria um módulo, e já havia log em outro

`ApiLayerLoggingGuardTest` varria só `:core:api`. O `:design-system` já usava
`android.util.Log.d` para registrar a URL de capa resolvida, e a guarda estreita não
via isso. Guarda nova, `ProductionLoggingSurfaceGuardTest` (`:app`), varre os 17
módulos e:

- proíbe log fora de **uma** exceção revisada (`MediaImageUrl.kt`);
- exige que essa exceção continue logando (senão o allow-list vira buraco),
  continue atrás de `if (!isDebuggableApp()) return`, e passe toda linha por
  `redactToken(`;
- exige que **todo** `HttpLoggingInterceptor` fique em `Level.NONE` — antes só
  `BODY`/`HEADERS` eram proibidos e `BASIC` (que imprime a URL de cada requisição,
  incluindo as de capa e reprodução, com token) passaria.

### Provas por reversão

Cada uma das seis foi desfeita de propósito e o teste nomeado falhou:

| defeito | reversão | resultado |
|---|---|---|
| 1 | `loadLibrary` volta a usar só a primeira página | `a random ordering is fetched whole instead of paged by offset` falha |
| 2 e 3 | `items + newItems` e offset por `items.size` | `a shifted page does not repeat an item and the offset keeps advancing` falha |
| 2 (Minha Lista) | `it.items + items` | `a shifted page does not repeat a favorite and the offset keeps advancing` falha |
| 4 | volta às três grafias | `diagnostics redact a token spelling the deny-list never listed` falha |
| 5 | remove `redactToken` do fallback | `an unmapped failure never shows a token` falha |
| 6 | `Level.BASIC` + um `Log.d` em `:feature:search` | `every okhttp logger is pinned to none` e `no module outside the single exception writes to a log` falham |

Os 38 testes de `:feature:library` que não são os três novos continuaram passando com
os defeitos 1–3 de volta, o que mostra que os novos testes medem o que dizem medir.

### Fica no backlog, com prioridade sugerida

- **P1 — retry de download reusa a URL absoluta gravada** (host antigo + `api_key`
  antigo). `Media3DownloadRepository.kt:106/122` grava a URL no índice do Media3 e
  `retry` readiciona exatamente o URI recebido; `ManageDownloadsUseCase.kt:35` repassa
  `entry.uri`. Cenário: enfileira em casa, sai do Wi-Fi (a troca para DuckDNS acontece
  sozinha), toca "Tentar novamente" e a fila reenvia o endereço LAN para sempre.
  Registrar no índice o caminho + refazer o host e o token no retry.
- **P1 — reprodução em andamento continua no host anterior** depois da troca
  automática de endpoint (`PlayerViewModel.kt` faz `setUri` uma vez; nada observa a
  troca).
- **P2 — cache do Coil é chaveado por URL completa**, então trocar de endereço
  descarta as capas úteis e guarda duas cópias de cada pôster até o evictor agir. A
  chave deveria ser `serverId + caminho` (há `LocalMulletaFlixServerId`), o que também
  evita colisão entre servidores diferentes, que seria o risco de usar só o caminho.
- **Corrigido de passagem, sem código:** o backlog dizia que `ProfileScreen` ainda
  usava `onSurface.copy(alpha = 0.4f/0.5f)` cru. Lido o arquivo: as duas ocorrências já
  passam por `readableTextOn` desde a rodada anterior. A anotação estava velha.

## Rodada v1.2.64 — a política de legenda tinha duas definições, e só uma podia vencer

Rodada de dívida estrutural, sem relato de usuário. Nenhuma mudança visível.

### O defeito

O tamanho e a cor da legenda existiam **duas vezes**, com a mesma semântica:

- `design-system/.../subtitle/SubtitleStyle.kt` declarava `setOf("WHITE","YELLOW","CYAN")`
  e `coerceIn(50, 200)` para desenhar a prévia;
- `data/.../SettingsRepositoryImpl.kt` declarava `setOf("WHITE","YELLOW","CYAN")` e
  `coerceIn(50, 200)` de novo, para gravar o valor.

O `:design-system` não pode depender do `:data` e vice-versa, então as duas cópias
nasceram separadas por conveniência. O risco é concreto: no dia em que alguém aceitar
"GREEN" na tela de ajustes e a prévia não souber pintar, o usuário vê uma cor e recebe
outra — e nenhum teste reclamaria, porque cada lado testa a sua própria cópia.

### A correção

A política foi para o `:domain`, que os dois já podem ver:

- `domain/.../model/SubtitleStylePolicy.kt` (novo): `SUBTITLE_COLOR_WHITE/YELLOW/CYAN`,
  `subtitleColorCodes`, `MIN/MAX_SUBTITLE_SIZE_PERCENT` (50/200), `normalizeSubtitleColor`,
  `normalizeSubtitleSizePercent` e `subtitleFractionalTextSize`.
- `:design-system` ficou só com o que é Compose: `subtitleForegroundColor(value): Color` e
  `SUBTITLE_OUTLINE_COLOR`.
- `SettingsRepositoryImpl` passou a chamar `normalizeSubtitleSizePercent` /
  `normalizeSubtitleColor` do `:domain` em vez de redeclarar os literais.

### A guarda, e por que ela não é contra si mesma

Testes que exercitam a função corrigida passariam com ou sem a duplicação — foi
exatamente o erro de método da v1.2.58. A guarda desta rodada lê o **texto-fonte** de
`data/.../SettingsRepositoryImpl.kt` e falha se um literal de política reaparecer ali.
O argumento é independente do caminho sob teste: ela não pergunta se a função normaliza
certo, pergunta se a segunda definição voltou a existir.

Prova por reversão: reintroduzir `coerceIn(50, 200)` em `SettingsRepositoryImpl` fez
`SubtitleStyleSingleDefinitionTest` falhar; removido, passa.

### Registrado também

- `:design-system` ganhou `implementation(project(":domain"))` só para isso. É uma seta a
  mais, consciente: a alternativa era manter duas fontes de verdade, que é o defeito.
- Testes divididos: `SubtitleStylePolicyTest` (5, `:domain`), `SubtitleStyleTest` (2,
  `:design-system`) e `SubtitleStyleSingleDefinitionTest` (2, guarda por varredura de fonte).

## Rodada v1.2.63 — uma ação indisponível no menu superior ficava idêntica a uma disponível

Regressão que **eu** introduzi na v1.2.61, encontrada medindo.

### O defeito

Ao remover a caixa que embrulha o botão (v1.2.61), `enabled` continuou barrando o
clique mas **parou de escurecer o ícone**. Medido por captura de pixels: os estados
habilitado e desabilitado eram **pixel a pixel idênticos**.

Isso importa porque `enabled` é usado para dizer "não há no que agir" —
`SyncPlayScreen` desabilita "Criar sala" enquanto envia. A ação passou a parecer
disponível e a ignorar o toque, que é pior do que parecer indisponível.

### A correção

`topBarActionContentAlpha(enabled)` devolve `1f` ou a opacidade de conteúdo desabilitado
do Material (0,38). Ela **não** olha para `busy`: uma ação ocupada está trabalhando, não
indisponível, e escurecer exatamente essa era o defeito que o usuário relatou como o
símbolo de atualizar parado.

### Duas medições, porque a primeira não discriminava

A primeira versão do teste contava "pixels claros" nos dois estados e **não separava**:
branco a 0,38 sobre a superfície do tema fica perto de um limiar de brilho usual. A
decisão virou função (`topBarActionContentAlpha`), testada diretamente, e a versão
renderizada continua coberta pela asserção de pixel que compara habilitado contra
desabilitado.

### Registrado também

- `MulletaFlixTopBarAction` passou a aplicar o `modifier` do chamador **depois** das
  suas próprias modificações, então um chamador que passe `size(...)` vence. Há
  exatamente um ponto assim no app: `SeriesSection` dimensiona o botão de play sobre a
  capa do episódio em 40 dp, abaixo do mínimo interativo de 48 dp. Fica registrado como
  dívida visual, não corrigido: mudar o tamanho do botão sobre a capa é uma decisão de
  layout, não um defeito medido.

## Rodada v1.2.62 — fechando buracos de teste expostos pelo relato do usuário

O relato dos ícones grandes (v1.2.60/v1.2.61) expôs um problema de **método**: eu tinha
testes que passavam com e sem o defeito. Esta rodada caça essa classe.

### Corrigido — risco de o botão central do controle parar de ativar o menu superior

Na v1.2.61 removi o `clickable` do `Box` externo. A ação passou a depender do
`IconButton` interno para **toda** a ativação — então, se essa fosse a errada, o menu
superior teria ficado sem ativação nenhuma pelo controle remoto. Eu tinha trocado um
defeito por um risco pior e não havia testado esse lado.

Novo teste `aFocusedActionActivatesOnOneCentrePress` pressiona `Key.DirectionCenter`
**uma vez** sobre a ação focada e exige exatamente 1 ativação. Verificado revertendo o
manipulador para vazio: falha com `expected:<1> but was:<0>`.

### Provado por reversão — a prévia de legenda não podia ter tamanho fixo

`SubtitlePreviewTest` era o único guarda visual do projeto que eu não havia provado por
reversão. Provado agora: forçando `subtitlePreviewFontSizeSp(100)` no componente,
`aLargerSubtitleSizePaintsMoreText` falha com
`200% must render taller glyphs than 75%; measured 35px then 35px`. Reforçado com uma
asserção de **largura**, para não depender de a quebra de linha cair do lado certo.

### A regra que tira dessa classe de erro

Os dois defeitos de tamanho passaram pelos meus testes porque as comparações usavam o
**mesmo caminho de layout** dos dois lados: eram iguais por construção. Regra adotada:
um guarda visual compara contra uma referência **independente** do caminho do código
sob teste (um `Icon` Material de 24 dp, um `IconButton` nativo, a contagem de nós
ativáveis) — nunca o componente consigo mesmo.

### O que continua pendente

## Rodada v1.2.61 — segunda passada no menu superior: a ação ainda era maior que o botão do Android

Continuação da v1.2.60. O glifo já estava em 24 dp, mas a ação continuava sendo uma
caixa embrulhando o botão — e isso mudava o tamanho do controle.

### O que a medição mostrou

`theActionIsTheSameSizeAsAPlainIconButton`, no emulador de TV:

```
measured 96x96 against 80x80 expected:<80> but was:<96>
```

Ou seja: **48 dp** para a ação contra **40 dp** para o `IconButton` nativo do Material.
O `Box` externo impunha `size(48.dp)`, e o `minimumInteractiveComponentSize` do
`IconButton` não reduz para caber.

### Também havia dois alvos de clique empilhados

A ação declarava `clickable` no `Box` externo **e** no `IconButton` interno — dois nós
ativáveis sobre um único controle visível. É exatamente a forma que fez um `MediaCard`
precisar de dois toques no controle remoto (v1.2.52). O `Box` foi removido; `scale` e o
anel de foco são desenhados dentro da própria caixa do botão e não somam tamanho.

Novo guarda `theActionIsASingleActivatableNode` conta os nós com ação de clique e exige
exatamente 1. Verificado revertendo: com o `clickable` de volta no `Box`, ele falha.

### Mais dois aprendizados de teste, registrados para não repetir

1. **`onNodeWithTag` não enxerga dentro de um `IconButton`** na árvore mesclada; é
   preciso `useUnmergedTree = true`. O guarda de glifo falhava com `assertExists`
   mesmo com o nó presente.
2. **Em aparelho de toque o foco não é observável.** Rodando o `design-system` no
   emulador de celular, três testes de foco falharam esperando `Focused = 'true'`,
   porque o nó aceita `requestFocus()` e ainda assim não fica focado em modo de toque.
   Eles agora chamam `Assume.assumeTrue(isTelevisionSurface())` e são **pulados** no
   celular — sem isso eu teria "consertado" um componente que não estava quebrado.

## Rodada v1.2.60 — os ícones do menu superior estavam grandes demais no celular

Relato do usuário: **"os icones do menu superior estão muito grandes no celular"**.
Estava certo, e a causa era minha, introduzida na v1.2.57.

### O defeito

Ao trocar o `IconButton` por um `Box` clicável (v1.2.57, para manter a ação no D-pad
enquanto ocupada), o `Box` recebeu `size(48.dp)` **e** `propagateMinConstraints = true`.
Isso entrega o tamanho da **área de toque** para a **arte**: o ícone passou a ser
desenhado no tamanho do nó.

Medido no emulador de celular (`MulletaflixApi35`, densidade 420):

| Forma | Nó | **Glifo desenhado** |
|---|---|---|
| publicada na v1.2.56–v1.2.59 | 126 px (48 dp) | **84 px (32 dp)** |
| corrigida | 126 px (48 dp) | **43 px (24 dp)** |
| `Icon` Material de referência | — | 43 px (24 dp) |

Ou seja: **32 dp em vez de 24 dp**, 33% maior. O alvo de 48 dp permanece intacto.

### A correção

O `IconButton` voltou a dimensionar a ação sozinho e o glifo é fixo em 24 dp
(`TOP_BAR_ACTION_ICON_SIZE_DP`), que é o tamanho de ícone do Material. O `Box` externo
não declara mais tamanho — só carrega a tag de teste, o anel de foco e o clique.

### Por que o guard anterior não pegou

O teste que escrevi na v1.2.58 comparava o glifo da ação com o de um `IconButton`
comum. **As duas medidas vinham do mesmo caminho de layout**, então eram iguais por
construção e o teste passava nos dois estados — um teste que passa com e sem o defeito
não é evidência, e eu apresentei como se fosse.

Reescrito em `TopBarActionIconSizeTest`, com duas asserções:

1. o glifo da ação tem de ter a mesma altura que um `Icon` Material de 24 dp medido na
   **mesma composição** (a expectativa não é um número mágico);
2. um teste que reproduz a forma defeituosa (nó de 48 dp + `propagateMinConstraints`)
   prova que a asserção acima pega o problema, porque lá o glifo é maior.

## Rodada v1.2.58 — terceira causa do "clicar 2x", dívida do bridge e API morta

### C1 — as opções de rádio dos diálogos de Ajustes tinham dois alvos de foco

Terceira causa do mesmo defeito relatado. Os diálogos de **Tema**, **Idioma das
Legendas** e **Idioma do Áudio** montavam cada opção como uma linha `clickable`
contendo um `RadioButton` **com o próprio `onClick`**.

Medido no emulador de TV (`RadioOptionFocusTargetTest`, em `:design-system`), contando
nós focalizáveis por opção:

| Forma | Alvos de foco |
|---|---|
| `clickable` na linha + `RadioButton(onClick = { … })` | **2** |
| `selectable(role = Role.RadioButton)` na linha + `RadioButton(onClick = null)` | **1** |

Dois alvos significa que o controle pousa no primeiro e só a segunda pressão chega ao
que ativa a opção — o "clicar 2x" relatado. A segunda forma já era a usada pelos menus
do player (`SleepTimerMenu`), então a correção foi trazer as três telas para ela.

O teste agora fixa as **duas** contagens, incluindo a errada: se alguém reintroduzir a
forma de dois alvos, a medição continua documentando por quê.

### C2 — o bridge de MediaSession liberava um player que não criou

`PlayerMediaSessionBridge` existe para **emprestar** o player da tela ao serviço de
reprodução; ele nunca cria um. Mesmo assim chamava `player.release()` no que estivesse
segurando.

Verifiquei todos os chamadores: `PlayerViewModel.onCleared` libera `player` e
`localPlayer`, e `MulletaFlixPlaybackService` libera o fallback que construiu. Ou seja,
no caminho normal a liberação era redundante e, no caminho que importa — `attach` com um
player novo enquanto o dono anterior ainda vive — destruía um player que outro
componente ainda usava e ainda entregaria ao Media3.

A regra de posse saiu do comentário e virou função (`MediaSessionOwnership.kt`):
`shouldReleaseSession` diz quando a sessão é do bridge, `shouldReleasePlayer` diz que o
player nunca é. **Honestidade:** `MediaSession` é final e o bridge é singleton, então o
*comportamento* não é alcançável de um teste JVM. O que dá para provar — e está provado —
é a **decisão**: forçando `shouldReleasePlayer()` a `true`, o teste falha com a mensagem
que explica o dano. A ligação entre decisão e comportamento é leitura de código, não
medição.

### C3 — API de bitrate máximo removida

`getMaxBitrate`/`setMaxBitrate` existiam na interface `SettingsRepository`, na
implementação e no DataStore como chave `max_bitrate`, mas **nenhum** código do app os
lia ou escrevia (confirmado por varredura: as únicas referências eram os próprios testes
e o fake do `SettingsRepository`). O limite de banda real vem da escada de qualidade em
`PlayerOptions`.

Também eram a única parte da interface com implementação **abstrata** — todas as outras
têm default — então obrigavam todo fake de teste a implementá-las sem usar. Removidas.

### O que tentei e revertí (para não ser tentado de novo)

Tentei fazer `SettingsRepositoryImpl` delegar o clamp de tamanho de legenda e a lista de
cores ao `:design-system`. **Revertido:** exigiria `:data` depender de `:design-system`
(que carrega Compose) só para duas funções de clamp, e `:design-system` não depende de
`:domain` nem de `:core:common`, então não há um lar barato para elas hoje. Os valores
concordam e toda leitura passa por um normalizador, então a duplicação não causa defeito.
Fica como dívida: **se os limites mudarem, há dois lugares para mudar**
(`SettingsRepositoryImpl.getSubtitleFontSize` e `design-system/.../SubtitleStyle.kt`).

## Rodada v1.2.57 — prévia de legenda, faixas offline, o mapa de cores/estilo e o botão de atualizar na TV

Três frentes: duas funcionalidades que o usuário pediu por consequência (escolher
legenda às cegas e escolher faixa offline sem lista), a remoção de mais uma
duplicação de tabela entre módulos — o padrão de causa-raiz que já apareceu em
tema, ordenação, idiomas, ids de download e opções de ajustes — e uma hipótese
medida sobre o "símbolo parado" da TV.

### F0 — o botão de atualizar não sai mais da fila do controle na TV

Segunda causa candidata para o relato **"enquanto em um dos icones fica um simbolo
de atualizar o tempo todo no meio da tela"**. Live TV, Biblioteca, Minha Lista e
SyncPlay passavam `enabled = !state.isLoading` para a ação de atualizar. Isso tornava
a ação **desabilitada**, e uma ação desabilitada:

1. fica com o ícone permanentemente apagado — e o ícone é um glifo estático, então
   nunca anima: lê-se como botão quebrado, não como trabalho em andamento;
2. **sai da sequência do D-pad.** Em TV essas telas também atualizam por timer de
   primeiro plano (60 s), então a ação desaparecia debaixo do usuário enquanto ele
   navegava os ícones de cima.

Agora existe `busy`, separado de `enabled`: `enabled` continua significando "não há
no que agir" e remove a ação da sequência; `busy` mantém a ação alcançável, troca o
ícone por um indicador de progresso e só engole o clique.

**Medido (e a medição corrigiu a implementação duas vezes):**

- a sonda `FocusTargetProbeTest` mostrou que `clickable(enabled = false)` **remove a
  ação `RequestFocus`** — um alvo nesse estado não pode receber foco de jeito nenhum.
  A primeira versão da correção assumia o contrário e não funcionava;
- a primeira execução da sonda usou `runCatching`, então **todas as variantes
  "passaram" sem nenhuma ter pegado foco**. A sonda foi reescrita com asserções
  reais; foi ela que produziu a evidência acima. Registro isto porque um teste que
  engole a falha é pior que nenhum;
- o seletor por `contentDescription` resolvia para a `Icon` **filha**, não para a
  ação, porque o indicador de progresso carrega o mesmo rótulo. O componente ganhou
  `TOP_BAR_ACTION_TEST_TAG`;
- `MulletaFlixTopBarAction` deixou de ser um `IconButton` e virou um `Box` clicável
  com um único alvo de foco. Empilhar `focusable()` **e** `clickable` no mesmo nó é
  exatamente o defeito que fez um `MediaCard` exigir dois toques no controle
  (v1.2.52), então não se repete aqui.

Prova por reversão: com `enabled = acceptsInput` no `clickable`, 3 dos 5 testes
falham (`the node is missing [RequestFocus]`); com o portão no `onClick`, os 5
passam.

### F1 — a prévia de legenda agora existe e é medida por pixel

Até aqui "Legendas" mostrava `150%` e `Amarelo` sem nenhuma forma de julgar a
escolha sem abrir um vídeo. Foi criado `SubtitlePreview`
(`feature/settings/src/main/java/org/mulletaflix/feature/settings/SubtitlePreview.kt`),
inserido no fim do grupo "Legendas" de `SettingsScreen`, desenhando
"Exemplo de legenda" sobre uma placa escura.

O ponto é que a prévia **não pode prometer** um tamanho ou uma cor que o player não
produz. Ela usa o mesmo mapeamento compartilhado do `:design-system`
(`subtitlePreviewFontSizeSp` = `SUBTITLE_PREVIEW_REFERENCE_HEIGHT_DP ×
subtitleFractionalTextSize`), então "150%" é o mesmo passo nos dois lugares.

O teste instrumentado `SubtitlePreviewTest` mede os pixels na TV
(`MulletaflixTvApi34`), não o código:

- o texto a 200% tem de renderizar **mais alto** que a 75%;
- trocar branco por amarelo tem de repintar;
- as mesmas opções têm de produzir os mesmos bytes (senão as duas primeiras
  asserções não significam nada).

Duas hipóteses minhas foram **refutadas pela própria medição** e estão registradas
para não voltarem:

1. "contar pixels diferentes da placa" não mede o texto — a placa não tem altura
   fixa, então ela **encolhe** quando o texto grande passa de duas linhas para uma,
   e os dois efeitos se cancelam (medido: 1.861.738 → 1.778.841, ou seja, *menos*
   pixels no texto maior). A medição correta é a caixa delimitadora dos glifos.
2. limiar de luminância (`r>0.6 && g>0.6 && b>0.6`) reporta **zero** pixels para
   uma legenda ciano ou azul correta — esconderia exatamente a regressão que o
   teste deveria pegar. A caixa usa o branco puro da cor `White` como detector.

Prova por reversão: forçando a cor para ciano fixo, `changingTheColourChangesWhatIsDrawn`
falha com `Actual: 0`; forçando o tamanho para 100, `aLargerSubtitleSizePaintsMoreText`
falha com `measured 0x0` (o detector de branco não acha nada). Restaurado, os 3
testes passam.

### F2 — o item de faixas offline deixou de ser uma lista vazia permanente

No modo offline os botões de áudio e legenda ficavam permanentemente desabilitados,
porque as listas vinham só dos metadados do servidor. Agora `PlayerViewModel`
chama `refreshOfflineTracks(tracks)` quando a reprodução é offline e monta a partir
do Media3; `OfflineTrackPolicy.kt`
(`feature/player/src/main/java/org/mulletaflix/feature/player/OfflineTrackPolicy.kt`)
concentra o mapeamento e o índice de faixa selecionada.

A parte difícil é que o índice de stream do Jellyfin **não existe** offline. O
caminho de seleção foi separado em dois espaços de numeração explícitos —
`offlineStreamIndices` e `trackCandidatePosition` — em vez de comparar o índice do
servidor com `Format.id` (base 1 do contêiner) ou com um índice local ao grupo, que
foi exatamente o defeito P1 da v1.2.54.

### F3 — cor e tamanho de legenda passaram a ter um dono só

A lista de cores aceitas e o clamp de tamanho existiam em dois módulos
(`PlayerOptions`/`SubtitleStylePolicy` no player e `SubtitleSizePolicy` nos ajustes),
com duas fórmulas de tamanho. Foram para
`design-system/src/main/java/org/mulletaflix/designsystem/subtitle/SubtitleStyle.kt`
e os duplicados foram **apagados** (`SubtitleStylePolicy.kt`,
`SubtitleSizePolicy.kt` e seus testes). Guards novos:
`SubtitleStyleTest` e `SubtitlePreviewPolicyTest`.

### F4 — "Minha Lista" podia pedir a primeira página duas vezes

`FavoritesViewModel.load()` cancelava o job anterior por identidade de **usuário**
no `finally`. `refresh()` cancela e inicia outro load para o **mesmo** usuário: o
cancelamento não executa o `finally` imediatamente, então a comparação por usuário
ainda era verdadeira e liberava a flag (`loadInFlight`) que o load novo tinha
acabado de tomar. Agora a identidade é a geração.

**Honestidade sobre esta correção:** eu **não** consegui construir um teste que
distinga o código antigo do novo. Tentei três cenários (página pendente, página
concorrente, liberação da flag via `refreshIfIdle`) e em todos os dois códigos
passam; o `NonCancellable` do fake descarta a continuação do job cancelado antes de
ela chegar ao ponto perigoso. A mudança fica por ser estritamente mais correta —
a flag pertence a quem a tomou por último — mas está **não provada**. Os dois testes
que tentei e não discriminam foram removidos em vez de ficarem dando falsa
cobertura.

### F5 — um load sem sessão deixava o esqueleto na tela

`loadLibrary` levanta `isLoading`/`isRefreshing` e só depois descobre que não há
sessão salva. O ramo que informa "Sessão expirada" passa a baixar as duas flags —
senão a tela fica com o esqueleto de carregamento esperando uma requisição que
ninguém vai iniciar. Guard: `LibraryViewModelTest` ›
`a load started without a session does not leave the skeleton up`, que também
congela a mensagem em `LibraryViewModel.EXPIRED_SESSION_MESSAGE`.

### O que continua pendente

- **O "símbolo parado no meio da tela"** do relato original da TV. Continua sendo o
  item nº 1 e ainda **precisa de um screenshot do usuário**: as duas causas
  encontradas até aqui (foco invisível, v1.2.52; ação de atualizar que saía da fila
  do D-pad e ficava com o ícone apagado, v1.2.57) são consistentes com o relato, mas
  nenhuma foi confirmada contra a imagem que o usuário viu.
- **SyncPlay sem cliente WebSocket**: confirmado que não é contornável por REST (o
  `GroupInfoDto` do servidor não tem `PlayingItemId` nem `PositionTicks`, e nenhuma
  rota do `SyncPlayController` expõe a fila). Precisa de duas sessões reais.
- `PlayerMediaSessionBridge` **corrigido na v1.2.58**: não libera mais o player. A
  decisão virou função testável; o comportamento em si não é alcançável de teste JVM.
- Confirmar o 429 das capas com sessão real (bloqueado: o servidor não expõe
  usuários públicos e não há conta sem senha).
- Verificar a adoção de downloads legados em aparelho com downloads anteriores à 1.0.6.
- **A correção de identidade de geração no `FavoritesViewModel`** (v1.2.57) está
  **não provada**: três cenários de teste foram tentados e todos passam com e sem a
  mudança. A mudança ficou por ser estritamente mais correta, não por ter evidência.
- **Clamp de tamanho de legenda duplicado** entre `SettingsRepositoryImpl` e
  `:design-system/.../SubtitleStyle.kt`. Ver a rodada v1.2.58 para por que não foi
  unificado.

## Rodada v1.2.55 — o resto dos achados de player, mais cinco defeitos

Continuação da rodada v1.2.54: os cinco achados altos do player já tinham sido
corrigidos; aqui vão os que a auditoria marcou como suspeita **mas que dava para
provar sem sessão real**, mais um que a verificação no servidor confirmou.

### P1 — "legendas desativadas" não desativava nada no servidor

Escolher legendas desativadas enviava `SubtitleStreamIndex = null`. Para o Jellyfin
`null` significa **sem preferência**: o servidor calcula um padrão a partir do modo de
legenda do usuário (`MediaStreamSelector.GetDefaultSubtitleStreamIndex`) e pode
**embutir** uma legenda mesmo assim.

Verifiquei no servidor que `-1` é o valor que significa "nenhuma": em
`MediaSourceManager.SetDefaultSubtitleStreamIndex` o `-1` é aceito como escolha
lembrada e a função retorna com `DefaultSubtitleStreamIndex = -1`; em `EncodingHelper`
só há burn-in quando `SubtitleStreamIndex >= 0`; e o `StreamBuilder` de DLNA testa
`SubtitleStreamIndex != -1`. Agora "desativadas" é enviado como `-1`.

Efeito colateral bom: a detecção passou a usar o catálogo compartilhado
(`prefersNoSubtitles` → `MediaLanguage.canonicalize == OFF`), então `off`, `none`,
`Desativadas` e `desabilitadas`, em qualquer caixa, significam a mesma coisa. Antes só
os dois primeiros literais eram reconhecidos — e foi um teste meu que revelou isso, ao
falhar com `expected:<-1> but was:<null>` para "Desativadas".

### P2 — o aviso do próximo episódio não fechava ao cancelar

`cancelNextEpisodeCountdown()` limpava só `nextEpisodeCountdown`, mas a visibilidade do
aviso é `countdownActive || isPlaybackEnded` — e `isPlaybackEnded` continua verdadeiro
no fim de um item. O aviso reaparecia na hora e o botão de cancelar parecia morto.
Agora existe `nextEpisodePromptDismissed`, que vence as duas condições e é zerado no
carregamento do próximo item.

### P3 — "Tentar novamente" não fazia nada durante uma queda de rede

`retryPlayback()` retornava em silêncio enquanto `networkWasOffline` era verdadeiro.
Agora explica que a reprodução reinicia sozinha quando a conexão voltar — que é o que o
coletor de rede já faz.

### P4 — a preferência de faixa era apagada quando o servidor não informava o idioma

`selectSubtitle`/`selectAudio` gravavam `track.language`. Quando o servidor não manda
`Language` nem `DisplayLanguage`, isso é nulo; `SettingsRepositoryImpl` remove a chave
quando o valor é em branco e passa a responder o padrão `"por"`, então o item seguinte
ligava a faixa padrão em vez da escolhida. `persistableTrackLanguage` agora devolve nulo
nesse caso, e quem chama simplesmente não grava — a preferência anterior é preservada.
A escolha continua valendo para o item em reprodução.

### P5 — estado do episódio anterior vazava para o seguinte

O reset de `loadMedia` limpava título, posição, duração e timer de sono, mas não
`currentChapterName`, `chapters`, `playbackStats`, `showSkipIntro`, `showSkipCredits` nem
`skipTargetPosition`. Durante o carregamento do episódio seguinte a OSD mostrava o nome
do capítulo **anterior** e mantinha um "Pular Créditos" clicável que mandava o **novo**
item para o alvo do **anterior**.

### O que continua pendente

- **O "símbolo parado no meio da tela"** do relato original da TV. Continua sendo o
  item nº 1; precisa de um screenshot.
- **SyncPlay sem cliente WebSocket**: confirmado que não é contornável por REST (o
  `GroupInfoDto` do servidor não tem `PlayingItemId` nem `PositionTicks`, e nenhuma rota
  do `SyncPlayController` expõe a fila). Precisa de duas sessões reais para construir e
  verificar.
- **Playback offline não lista as faixas**: os botões de áudio/legenda ficam
  permanentemente desabilitados offline porque as listas vêm dos metadados do servidor.
  Corrigir é uma funcionalidade (mapear as faixas do Media3 para a UI e para um caminho
  de seleção que não dependa de um índice de stream do servidor), não uma linha.
- `PlayerMediaSessionBridge` libera um player que não criou; a segunda liberação é
  inofensiva (`SimpleBasePlayer.release()` sai cedo), mas a sessão fica ligada a um
  player liberado que o serviço ainda entrega a novos controles.
- Confirmar o 429 das capas com sessão real (bloqueado: o servidor não expõe usuários
  públicos e não há conta sem senha).
- Verificar a adoção de downloads legados em aparelho com downloads anteriores à 1.0.6.



## Rodada v1.2.54 — os cinco defeitos de player confirmados pela auditoria

A auditoria do player (rodada v1.2.53) deixou cinco achados altos, todos com arquivo
e linha. Esta rodada corrigiu os cinco.

### P1 — as faixas de áudio saíam trocadas

`selectTrackByServerIndex` comparava o índice de stream do Jellyfin (`Index`, base 0,
contando **todas** as faixas do arquivo) com `Format.id`, que é o nome que o contêiner
dá à faixa — o `TrackNumber` do Matroska, **base 1**. Num MKV com dois áudios o
servidor reporta índices `1, 2` e o contêiner ids `"2", "3"`: escolher a primeira faixa
tocava a segunda, e o fallback seguinte comparava o índice do servidor com um índice
**local ao grupo**, um terceiro espaço de numeração. A última entrada da lista caía no
`?: return` e não fazia nada.

Correção: o índice do servidor é resolvido pela **posição** entre as faixas do mesmo
tipo (`trackCandidatePosition`), que o contêiner preserva. Índice fora da lista resolve
para nulo e a chamada desiste, em vez de adivinhar. `currentAudioStreamIndices` /
`currentSubtitleStreamIndices` guardam essa ordem, montada com `orderedStreamIndices`
(ordenação explícita, sem depender da ordem em que o servidor serializou).

### P2 — a sessão de reprodução nunca era encerrada no servidor

`reportPlaybackStopped()` fazia `launch` no `viewModelScope`, e o `onCleared()` que o
chama roda **depois** de `ViewModel.clear()` cancelar esse escopo. O corpo nunca
executava: o item ficava como sessão ativa no servidor e o transcode iniciado pelo
servidor nunca era parado.

Correção: `@ApplicationScope CoroutineScope` novo em `:core:common` (qualifier +
provider com `SupervisorJob`), injetado no `PlayerViewModel` e usado **apenas** para
esse relatório de saída.

### P3 — a qualidade selecionada era calculada e logo sobrescrita

O carregamento calculava `effectiveQualitySelection(defaultQuality, availableQualities)`
e, logo depois, `applyDefaultPlaybackPreferences()` chamava `applyQuality(defaultQuality)`,
que gravava a preferência **crua**. O menu é montado de
`qualityMenuOptions(availableQualities)`, então em todo título cuja escada não tinha a
resolução salva nenhuma linha aparecia selecionada enquanto o estado dizia "4K".

Correção: `applyQuality` grava `appliedQualitySelection(...)`, que é sempre um valor que
o menu oferece.

### P4 — o playback offline ignorava os ajustes

`loadOffline` criava `PlayerState(...)` do zero, então cada campo vindo de ajustes caía
no padrão da data class: um download entrava em Picture-in-Picture mesmo com o ajuste
desligado, o tamanho da legenda voltava a 100 e a rede deixava de ser tratada como
medida.

Correção: `offlinePlaybackState(...)` é uma fábrica nomeada que exige os campos de
ajuste explicitamente, e os dois pontos de construção passam os valores correntes.

### P5 — o timer de suspensão marcava duas opções

A linha "Desativado" perguntava `remainingMs == null`, que também é verdadeiro em "Ao
fim da mídia": as duas linhas apareciam selecionadas e não dava para saber qual timer
estava armado.

Correção: `SleepTimerMenu` recebe `SleepTimerMode` e cada linha deriva a seleção do
mesmo modo (`isSleepTimerOffSelected`, `isSleepTimerAtMediaEndSelected`,
`isSleepTimerOptionSelected`). Um enum só pode ter um valor, então a contradição é
impossível por construção.

### O que continua pendente do player (suspeitas, precisam de sessão real)

- "Legendas desativadas" não é expressável ao servidor: `requestedPreferredStreamIndex`
  devolve nulo para "off", que o Jellyfin interpreta como **sem preferência**, então
  um burn-in do lado do servidor ainda pode acontecer conforme o modo de legenda do
  usuário.
- A preferência de faixa é gravada como `track.language`, que é nulo quando o servidor
  não manda `Language` nem `DisplayLanguage`; nesse caso a chave é removida e o padrão
  `"por"` volta, então o item seguinte pode ligar a faixa padrão em vez da escolhida.
- O estado de "pular créditos" não é limpo no reset de carregamento (`loadMedia` limpa
  `showSkipIntro`/`showSkipCredits`/`skipTargetPosition`, mas não `currentChapterName`),
  então durante o carregamento do próximo episódio o botão do anterior pode aparecer e
  pular para o alvo do episódio anterior.
- `PlayerMediaSessionBridge` libera um player que não criou; a segunda liberação é
  inofensiva (`SimpleBasePlayer.release()` sai cedo), mas a sessão fica ligada a um
  player liberado que o serviço ainda entrega a novos controles.
- `retryPlayback()` não faz nada enquanto `networkWasOffline` é verdadeiro, então o
  botão "Tentar novamente" parece morto durante uma queda de rede.
- `cancelNextEpisodeCountdown()` não fecha o aviso de fim porque `isPlaybackEnded`
  continua verdadeiro.



## Rodada v1.2.53 — duas auditorias delegadas, seis defeitos corrigidos

Duas auditorias independentes (player; shell do app + autenticação) produziram
achados com arquivo e linha. Corrigi os de maior impacto e verificação mais barata;
os de player que exigem refatoração maior ficaram registrados nas pendências.

### P1 — o foco nas abas do login era praticamente invisível (medido por screenshot)

Na TV, o foco sobre "Entrar"/"Quick Connect" produzia apenas um lavado escuro.
Coloquei `remoteFocusRing` no design system (anel + disco, sem elevação, para não
colidir com vizinhos) e apliquei nas duas abas.

Evidência: instalei o APK no AVD de TV, naveguei com `adb shell input keyevent` e
comparei screenshots — antes só o lavado, depois anel vermelho visível em "Entrar" e,
após um toque para a direita, em "Quick Connect". É também a primeira vez nesta
conversa que consigo **ver** uma tela logada ou não logada do app rodando de verdade.

### P2 — "Trocar de Servidor" não fazia nada

`ServerSelectionScreen` e `LoginScreen` se auto-avançam quando já existe sessão
(`LaunchedEffect(state.isAuthenticated)`), o que é certo para o destino inicial e
errado aqui: a lista de servidores mandava para o login, o login mandava para a Home,
e sobrava uma segunda Home na pilha. Só sair da conta permitia trocar de servidor.

Correção: `shouldAutoAdvanceAuthScreen(isAuthenticated, switchingServer)` em
`AuthAutoAdvancePolicy.kt`; o `MulletaFlixNavHost` guarda `switchingServer` (ligado por
`onSwitchServer`, desligado no login bem-sucedido) e repassa às duas telas.

### P3 — os downloads morriam ao sair do app

`DownloadService` estava declarado no manifesto e implementava
`getForegroundNotification`, mas **nada** chamava `DownloadService.send*`: o
repositório falava direto com o `DownloadManager`. O Media3 só conduz a fila enquanto
o serviço roda, então não havia notificação de progresso e um download na fila ou pela
metade parava quando o processo morria.

Correção: em `Media3DownloadRepository`, toda mutação da fila passa pelo serviço
(`sendAddDownload`, `sendRemoveDownload`, `sendResumeDownloads` no `init`).
`sendRemoveDownloads` (em lote) não existe no Media3 1.11, então a remoção em lote
itera. Guarda nova `DownloadQueueGuardTest`: o repositório não pode chamar
`manager.addDownload(`/`manager.removeDownload(`.

### P4 — link compartilhado de outro servidor abria o item errado

`ShareItemContent` grava `&serverId=` com o comentário explicando por quê; o
`extractMediaLink` já lia o valor, e o `MediaDeepLinkRequest` **descartava**. Um link
gerado no servidor B, aberto por quem está no servidor A, resolvia o id contra a
biblioteca local: item sem relação, ou "erro ao carregar detalhes".

Correção: `MediaDeepLinkRequest` ganhou `serverId`; `shouldOpenLinkOnCurrentServer`
decide; o NavHost recusa o link de outro servidor com uma mensagem em vez de abrir a
coisa errada. Ids desconhecidos de qualquer lado **não** bloqueiam, para servidores que
não informam `ServerId` não perderem o recurso. O teste JVM usa a nova sobrecarga de
string, porque `Uri.parse` é nulo sob `isReturnDefaultValues`.

### P5 — a recuperação de LAN podia gravar um endereço não-local como servidor salvo

`shouldSwitchToLan` só exigia que as URLs fossem diferentes — a direção oposta
(`publicFallbackAfterLanLoss`) exigia localidade, essa não. Qualquer host que
respondesse à sondagem da porta 7359, ou um servidor anunciando `0.0.0.0`, substituía o
endereço salvo; todas as requisições seguintes, **com o cabeçalho de autorização**,
iam para lá.

Correção: `shouldSwitchToLan` passou a exigir local **e** discável, e
`selectAuthenticatedLanServer` filtra os candidatos do mesmo jeito. `0.0.0.0` continua
classificado como local de propósito (o compartilhamento de link precisa reconhecê-lo
como endereço a substituir), então ganhou uma noção própria, `isDialableServerUrl`,
porque é endereço de escuta e não algo que um cliente abre.

### P6 — o atualizador podia dizer "você já está na versão mais recente" para sempre

O GitHub manda `"digest": null` para asset sem digest. O `org.json` do Android
devolve a **string** `"null"` nesse caso (implementado como
`JSON.toString(opt(name))`, e `JSON.toString` é `String.valueOf` para objeto não nulo);
o `org.json` da JVM usado nos testes devolve o padrão. As duas plataformas divergiam e
só o aparelho tomava o caminho errado: `"null"` não é sha256 válido, a release era
tratada como corrompida e descartada. Correção: `normalizedAssetDigest` normaliza o
literal nas duas plataformas, com teste que fixa o caso.

### Achados de player registrados, ainda NÃO corrigidos

A auditoria do player confirmou cinco defeitos altos, todos traçados até linha. Nenhum
foi corrigido nesta rodada por exigirem refatoração maior que o orçamento da rodada
permitia verificar:

1. `selectTrackByServerIndex` mistura o índice de stream do Jellyfin com o id do
   contêiner do Media3 (TrackNumber do MKV, 1-based): as duas primeiras faixas de áudio
   saem **trocadas**, e a última entrada da lista não faz nada.
2. `selectedQuality` é calculado com `effectiveQualitySelection` e **sobrescrito** logo
   depois por `applyDefaultPlaybackPreferences`: o menu de qualidade aparece sem
   seleção em todo título cuja escada não tem a resolução salva.
3. `/Sessions/Playing/Stopped` nunca é enviado ao sair do player: `reportPlaybackStopped`
   faz `launch` num `viewModelScope` que o `ViewModel.clear()` já cancelou antes de
   chamar `onCleared()`. A sessão fica aberta no servidor e o transcode não é parado.
4. Offline playback substitui o `PlayerState` inteiro, zerando as listas de faixas (os
   botões de áudio/legenda ficam permanentemente desabilitados offline) e ignorando a
   preferência de PiP e o tamanho de legenda.
5. O menu do timer de sono marca **duas** opções quando o timer é "Ao fim da mídia"
   (`remainingMs == null` e `isAtMediaEnd` são ambos verdadeiros).

Suspeitas que a mesma auditoria levantou e que precisam de sessão real: "legendas
desativadas" não é expressável ao servidor (`SubtitleStreamIndex = null` = "sem
preferência" para o Jellyfin); a preferência de faixa não sobrevive quando o servidor
não manda `Language`/`DisplayLanguage`; estado de "pular créditos" pode vazar de um
episódio para o próximo durante o carregamento.



## Rodada v1.2.52 — "tenho que clicar 2x para entrar em qualquer biblioteca ou midia"

Relato do usuário, e o defeito mais concreto encontrado até agora: **dois alvos de
foco no mesmo nó**.

### P1 — a primeira pressão do controle era engolida (CONFIRMADO e corrigido)

Um nó Compose com `Modifier.focusable()` **e** `Modifier.clickable` carrega dois
alvos de foco. No controle, a tecla central ia para o alvo que **não** tinha o
tratador de ativação: a primeira pressão era consumida e só a segunda abria o item.

Medição no AVD `MulletaflixTvApi34`, com o foco já no card e **uma** pressão de
`Key.DirectionCenter`:

| Estado | Ativações |
|---|---|
| Antes da correção | **0** |
| Depois da correção | **1** |

E duas pressões passam a ativar exatamente duas vezes (antes a segunda era a
primeira a contar).

Cinco telas tinham escrito o par à mão, além do `MediaCard`:

| Arquivo | Elemento |
|---|---|
| `MediaCard.kt` | todos os cards de mídia e as capas de biblioteca da Home |
| `LibraryScreen.kt` | linhas da lista da biblioteca |
| `LiveTvScreen.kt` (2 pontos) | linhas de canal e linhas de gravação |
| `SearchScreen.kt` | linhas do histórico de busca |
| `DownloadsScreen.kt` | linhas de download offline |

Correção: `focusable()` só é adicionado quando o nó **não** é clicável — o
`clickable` já traz o próprio alvo de foco e trata a tecla central.

### P2 — o foco invisível era em TODAS as barras, não só na Home

A rodada v1.2.50 corrigiu apenas a barra superior da Home. A resposta "todos" do
usuário estava certa: **toda** barra superior do app tinha o mesmo problema, e
telas que nem calculavam `isTelevision` (Ajustes, Perfil, Detalhes) ficavam de fora
em qualquer correção por tela.

Correção: o tratamento de foco virou `MulletaFlixTopBarAction` no `:design-system`,
com `focusFriendly` derivado internamente por `isTelevisionDevice()`, e passou a ser
usado nas barras de Live TV, Minha Lista, Biblioteca, SyncPlay, Downloads, Ajustes,
Perfil, Busca e Detalhes. `VideoPlayerScreen` foi **deixado de fora** de propósito:
seus 18 controles de transporte têm estilo próprio e o player não pôde ser
verificado sem reprodução real.

### Refatoração — uma expressão repetida em sete telas

`(LocalConfiguration.current.uiMode and UI_MODE_TYPE_MASK) == UI_MODE_TYPE_TELEVISION`
aparecia literalmente em sete arquivos. Virou `isTelevisionDevice()`.

### Guarda nova contra a reincidência

`RemoteFocusTargetGuardTest` (em `:app`) varre o `src/main` de todos os módulos e
falha se qualquer arquivo fora de `:design-system` declarar `.focusable()` — a regra
é que só o design system declara alvo de foco; uma tela usa `clickable` ou um
componente compartilhado. Verificado revertendo: reintroduzir `.focusable()` numa
tela faz a guarda falhar apontando arquivo e linha.

O mesmo arquivo fixa a combinação exata que causou o defeito
(`if (isClickable) Modifier else Modifier.focusable()`), caso a regra acima seja
relaxada algum dia.

### Lição de método

Duas vezes nesta rodada um filtro de saída meu (`Select-String`) escondeu o veredito
do Gradle e me fez ler "passou" onde havia falha. **Terminar a inspeção com
`Select-Object -Last N` sobre a saída crua, não com filtro por palavra** — o código de
saída e as últimas linhas são a evidência, o filtro é conveniência.

## Rodada v1.2.51 — as pendências de TV ao vivo, todas provadas por teste revertido

Quatro defeitos confirmados no Live TV e a duplicação da política de paginação. O
símbolo parado na tela do relato da TV **continua sem causa** (ver a rodada
v1.2.50); nenhuma correção especulativa foi escrita para ele.

### P1 — era possível criar duas gravações do mesmo programa

`LiveTvViewModel` nunca populava `scheduledProgramIds`: o conjunto só recebia ids
que **esta sessão** agendou. Ao reabrir o guia, um programa já marcado voltava a
mostrar "Gravar", e um toque criava um **segundo** timer no servidor.

Correção: `LiveTvRepository.getScheduledProgramIds()` consulta
`LiveTv/Timers?IsScheduled=true`. Confirmado no servidor
(`LiveTvController.cs:489` devolve `QueryResult<TimerInfoDto>`, e
`BaseTimerInfoDto.ProgramId` existe) e em `LiveTvManager.GetTimers`, onde
`IsScheduled=true` filtra `Status == RecordingStatus.New` — exatamente as
gravações pendentes, sem trazer concluídas nem canceladas.

O conjunto é **unido**, não substituído: um timer criado segundos antes pode ainda
não aparecer nessa resposta, e perder a marca voltaria a oferecer "Gravar" para algo
já agendado. Falha na consulta é ignorada de propósito — não saber o que está
agendado não pode quebrar a lista de canais.

### P2 — o guia aberto ficava vazio depois de uma atualização

`refresh()` invalida a requisição do guia e **limpa `isLoadingGuide`**. Se o
diálogo estava aberto e o guia ainda carregando quando a atualização chegou (timer
de 60 s da TV ou botão), o diálogo passava a mostrar "Nenhum programa encontrado
para as próximas 24 horas." sem nenhuma requisição por trás, e só fechar e reabrir
resolvia.

Correção: o ViewModel rastreia se o diálogo está aberto (`guideRequested`, marcado
por `loadGuide()` e limpo por `closeGuide()`, agora chamado no `onDismissRequest` e
no botão "Fechar") e a atualização recarrega o guia quando ele está aberto.

### P3 — um erro apagava o guia que já estava na tela

`GuideContent` fazia `state.guideError != null -> Text(state.guideError)` **no lugar**
dos programas: uma falha de atualização descartava um guia que o usuário já estava
lendo e o diálogo não oferecia nova tentativa.

Correção: a decisão virou `guideBody(isLoadingGuide, programmeCount)` em
`GuideBodyPolicy.kt` — programas na tela **sempre** ganham, então nem recarga nem
erro apagam conteúdo legível. O erro é uma faixa acima da lista com botão
"Tentar novamente" (`viewModel::loadGuide`).

### P4 — a lista de canais era truncada em 100, em silêncio

`getLiveTvChannels(limit = 100)` era pedido uma única vez e `TotalRecordCount` era
ignorado. Um provedor com 250 canais mostrava 100 e **nada** indicava que o resto
existia.

Correção: `fetchAllChannels` percorre as páginas. A condição de parada é
`hasMoreChannelPages`, que trata `totalRecordCount == 0` como "desconhecido" e não
como "acabou" — `TotalRecordCount` é um `Int` não nulo com default `0`
(`MediaDtos.kt:9`), então um servidor que omitisse o campo truncaria em 100 de novo.
Sem total, página cheia significa "pode haver mais" e página curta significa fim. O
laço tem dois limites: página vazia/total atingido e `MAX_LIVE_TV_CHANNEL_PAGES`.

Consequência tratada junto: o guia juntava **todos** os ids de canal em um único
`ChannelIds`. Com a lista deixa de ser limitada a 100, esse valor cresceria com o
catálogo do provedor (250 ids ≈ 8 KB, o limite padrão de request line do Kestrel) e
o guia responderia 414. Agora é pedido em lotes de `LIVE_TV_CHANNEL_BATCH_SIZE`,
com `distinctBy` no merge.

### Refatoração — a política de paginação existia só na biblioteca

`shouldRequestNextLibraryPage`/`hasMoreLibraryPages` eram `internal` de
`:feature:library`. O Live TV precisava da mesma regra e ia ganhar uma segunda
cópia — o defeito recorrente deste projeto. Foram para
`domain/src/main/java/org/mulletaflix/domain/paging/PagingPolicy.kt` como
`shouldRequestNextPage`/`hasMorePages`, com o teste movido junto
(`PagingPolicyTest`), e a biblioteca/Minha Lista passaram a importá-las.

### Evidência: cada correção foi revertida

Todos os testes novos falham quando a correção correspondente é revertida, medido:

| Correção revertida | Testes que falharam |
|---|---|
| semeadura dos agendados | 2 |
| recarga do guia aberto | 1 |
| paginação dos canais | 4 |
| lotes do guia | 2 |

### Pendências que continuam abertas

- **O "símbolo parado no meio da tela" do relato da TV.** Pedir um screenshot ao
  usuário é o próximo passo. Já descartados: spinner de progresso (giraria),
  indicador de pull-to-refresh preso (medido no emulador de TV: 0 pixels de
  diferença ao pressionar D-pad para cima), logo do app (é um emblema de estrela,
  não uma seta) e flag de carregamento preso na Home (auditoria dos 12 ViewModels).
- **SyncPlay não tem cliente WebSocket**, e isso não é contornável por REST: o
  `GroupInfoDto` do servidor não tem `PlayingItemId` nem `PositionTicks`
  (`MediaBrowser.Model/SyncPlay/GroupInfoDto.cs`), e nenhuma rota do
  `SyncPlayController` expõe a fila. O que toca chega pelo WebSocket
  (`PlayQueueUpdate`). Construir isso sem uma sessão real de SyncPlay para verificar
  seria trabalho não verificável, então **não foi feito** — precisa de duas sessões
  reais para testar.
- Confirmar o 429 das capas com sessão real (bloqueado: o servidor não expõe
  usuários públicos e não há conta sem senha).
- Verificar a adoção de downloads legados em aparelho com downloads anteriores à
  1.0.6 (hoje só coberto por teste unitário).

## Rodada v1.2.50 — relato da TV: foco invisivel no menu superior + simbolo preso na tela

Relato do usuario, em uma frase: *"a navegacao na tv nos menus superiores nao mostra
em qual icone esta e enquanto em um dos icones fica um simbolo de atualizar o tempo
todo no meio da tela"*. As duas metades foram tratadas de forma independente, e a
segunda **nao** foi confirmada.

### P1 — o foco do controle era invisivel nos seis icones do topo (CONFIRMADO e corrigido)

`HomeTopBar` usava `IconButton` puro. `IconButton` **e** focavel, entao o D-pad ja
percorria os seis icones (Buscar, TV Ao Vivo, Downloads, Minha Lista, Configuracoes,
Perfil) — mas cada `Icon` tinha um `tint` fixo e nada era desenhado em volta. O
usuario nao tinha como saber onde estava o foco.

Medicao, no AVD `MulletaflixTvApi34`, com `captureToImage().toPixelMap()`:

| Estado | "red bias" medio do icone (r - g) |
|---|---|
| Focado, com a correcao | >= 20 |
| Nao focado | <= 5 |
| **Focado, com a politica revertida (anel removido)** | **0** |

O valor **exato 0** e a evidencia: o icone focado era pixel a pixel identico a um
nao focado, e nem a camada de estado do ripple do Material pintava nada. O teste
`HomeTopBarFocusTest.theFocusedIconIsPaintedDifferentlyFromAnUnfocusedOne` falha com
a politica revertida e passa com ela.

Correcao: `HomeTopBarFocusPolicy.kt` (elevacao 1.15x, anel de 2 dp e disco com alfa
0.20 em `MulletaFlixRed`) com o mesmo idioma que `MediaCard` ja usava para TV, e um
`HomeTopBarAction` que aplica isso aos seis icones. Em celular/tablet
(`usesFocusFriendlySpacing == false`) os valores sao 1f / 0f / 0f, entao o layout de
toque nao muda — coberto por teste.

### P2 — o "simbolo de atualizar parado" NAO foi confirmado; a hipotese obvia foi refutada

O usuario esclareceu: e uma **seta circular que nao gira, parada, tipo um botao**, e
aparece em **todos** os icones. Isso descarta os `CircularProgressIndicator` (todos
giram) e aponta para o indicador do `PullToRefreshBox` do Material 3, que desenha uma
seta circular estatica quando `isRefreshing == false` mas a distancia de "puxada" e
maior que zero. Quatro telas usam `PullToRefreshBox`: Home, Biblioteca, Minha Lista e
Busca — todas alcancaveis pelos icones do topo.

A hipotese foi **medida antes de qualquer alteracao de producao**, em
`PullToRefreshRemoteProbeTest` (AVD de TV, comparacao de pixels da raiz):

- um toque de D-pad para cima no primeiro item muda **0 pixels** — o
  `PullToRefreshBox` nao trata tecla de controle como "puxada" e nao pode deixar o
  indicador preso na TV. **Hipotese refutada.**
- controle positivo no mesmo arquivo: com `isRefreshing = true` o indicador **e**
  visivelmente diferente do estado ocioso, entao a medicao nao passa por vacuidade.

Os dois testes ficaram no repositorio como regressao (o primeiro afirma 0 pixels
alterados). **Nenhuma correcao especulativa foi escrita para o P2.**

Tambem foi descartada, por auditoria dedicada de todos os 12 ViewModels, a
possibilidade de um flag de carregamento preso na Home: `HomeViewModel` limpa
`isLoading`/`isRefreshing` em todos os ramos terminais, e todo `loadJob?.cancel()` e
seguido de um novo `loadHome()`, entao sempre existe um job da geracao mais nova para
limpar os dois flags.

### P2b — bug deterministico de carregamento preso, encontrado na auditoria (corrigido)

O unico caminho de flag presa com confianca **alta** em todo o repositorio esta na
tela de login, nao na Home: `AuthViewModel.initiateQuickConnect()` levanta
`isLoading`, e ao cancelar (`cancelQuickConnectPolling()`) a geracao e incrementada;
os dois ramos terminais da requisicao checam a geracao e **pulam** a limpeza. Como
`AuthRepositoryImpl.initiateQuickConnect()` envolve a chamada em `runCatching`, o
cancelamento chega como `onFailure` em vez de cancelamento, e `isLoading` ficava
`true` para sempre — com os botoes "Entrar" e Quick Connect desabilitados ate
reiniciar o app. Correcao: `cancelQuickConnectPolling()` tambem baixa `isLoading`,
porque e ele quem invalida a unica requisicao que levantou o flag. Teste
`AuthViewModelTest.cancelling quick connect clears the loading state its request left
behind` falha sem a correcao.

Furo menor do mesmo tipo, tambem corrigido: `SearchViewModel.onQueryChange("")`
cancelava a busca e limpava `isLoading`, mas nao `isRefreshing`, deixando o indicador
de pull-to-refresh girando sem requisicao por tras.

### Outras correcoes desta rodada

- **Live TV: "Gravar" nunca funcionava.** `CreateLiveTvTimerDto` nao enviava
  `ServiceName`, e `LiveTvManager.CreateTimer` comeca com
  `GetService(timer.ServiceName)` — `KeyNotFoundException` → HTTP 500. O valor agora
  vem de `LiveTv/Timers/Defaults?programId=`, junto com a politica de padding do
  proprio servidor.
- **Live TV: o guia EPG buscava uma rota inexistente.** `LiveTv/EPG` nao existe em
  `LiveTvController.cs`; a rota correta e `LiveTv/Programs`. Coberto por
  `LiveTvApiContractTest`.
- **Qualidade escolhida era descartada.** `qualityOptions()` nomeia uma faixa de
  360p/288p/240p, mas `normalizeQualityPreference()` respondia `"Auto"` para esses
  valores: escolher 360p gravava "Auto" e a selecao voltava sozinha. Alem disso
  `videoQualityConstraint()` caia no ramo "sem limite nenhum", ou seja, podia servir
  4K para quem pediu 360p. Agora a escada de resolucoes e uma tabela unica
  (`qualityLadder`) e alturas fora dela sao aceitas com limite de altura e bitrate.
  Dois testes novos; ambos falham com a correcao revertida.
- **Ajustes: as listas de opcoes eram copias.** Ordenacao oferecia 5 dos 8 campos
  (`Aleatorio`, `Mais Assistidos`, `Assistido Recentemente` eram inacessiveis) e os
  idiomas ofereciam 2 dos 5. Alem disso as densidades de grade, direcao de ordenacao e
  cores de legenda tinham o rotulo no dialogo e o mapeamento no ViewModel. Tudo agora
  deriva de `SettingsOptionLists.kt` + `LibrarySortField` + `MediaLanguage`, com
  `SettingsOptionListsTest` falhando se um dialogo parar de oferecer um valor que o
  app sabe gravar. **E o mesmo padrao de lista duplicada das rodadas anteriores.**

### Pendencias abertas

- **O "simbolo parado no meio da tela" continua sem causa identificada.** Este e o
  item numero um da proxima rodada. O que ja esta descartado: spinner de progresso
  (giraria), indicador de pull-to-refresh preso (medido, 0 pixels), e flag de
  carregamento preso na Home (auditado nos 12 ViewModels). **Pedir um screenshot ao
  usuario** e o proximo passo — o `HomeTopBar` nao tem nenhum `Icons.Default.Refresh`,
  entao o glifo vem de outra superficie.
- SyncPlay continua sem cliente WebSocket: play/pause/seek nao sincronizam, so o poll
  de `SyncPlay/List` a cada 5 s.
- Live TV sem paginacao (limites fixos de 100 canais e 50 programas), `totalRecordCount`
  ignorado, `scheduledProgramIds` nunca populado.
- Confirmar o 429 das capas com sessao real (bloqueado: o servidor nao expoe usuarios
  publicos e nao ha conta sem senha).

### Licao de metodo desta rodada

Duas hipoteses minhas foram **refutadas por medicao** antes de virarem codigo: o
indicador de pull-to-refresh preso na TV (0 pixels de diferenca) e — na rodada
anterior — os alvos de toque do player. Nas duas vezes a explicacao "obvia" estava
errada. Medir primeiro custou minutos; teria custado uma correcao inutil e um teste
sem valor.

Detalhe pratico descoberto agora: **nomes de metodo de teste instrumentado com
espacos (crase) quebram o build de `androidTest`** neste projeto, porque `minSdk 24`
usa DEX anterior a versao 040 (`Space characters in SimpleName ... are not allowed`).
Usar camelCase em `src/androidTest`; crase com espacos continua valido nos testes
unitarios de JVM.



## Rodada v1.2.49 — as duas pendencias da rodada 10, ambas causadas por lista duplicada

Os dois defeitos que ficaram registrados na rodada 10 tinham a mesma causa: uma
lista de valores mantida em dois lugares, que divergiu.

### P1 — a escolha de ordenacao da biblioteca era destruida ao ser confirmada

O menu da Biblioteca usava `"Data de Adição"`; Configuracoes usava
`"Data de adição"`. As duas nunca casavam, entao:

- a tela de Configuracoes exibia **"Nome"** enquanto a biblioteca estava ordenada
  por outro campo;
- `Random`, `PlayCount` e `DatePlayed` nem existiam na lista de Configuracoes e
  colapsavam em "Nome";
- pior: o dialogo devolve o rotulo que exibiu e o ViewModel derivava o codigo
  **desse rotulo**. Confirmar "Nome" gravava `SortName`, apagando a escolha real.

Correcao: `LibrarySortField` no `:domain` passa a ser a unica lista; a tela da
Biblioteca e a de Configuracoes derivam dela. As cinco constantes privadas
duplicadas em `SettingsViewModel` foram removidas.

### P2 — idioma de audio e legenda colapsava em "Idioma original"

O player grava o codigo que o servidor informou (`spa`, `por`, `eng`, …). A tela
de Configuracoes reconhecia apenas portugues e ingles e mostrava todo o resto
como "Idioma original". Confirmar esse valor gravava `original`, descartando a
preferencia — e o app **e** feito para es/fr/de, como o proprio
`TrackPreferencePolicy` demonstrava.

Correcao: `MediaLanguage` no `:domain` concentra a canonicalizacao e os rotulos.
O `TrackPreferencePolicy` do player tinha uma **terceira** copia da lista e passou
a delegar para a mesma fonte, entao agora a lista existe em um lugar so.

Detalhe de comportamento que mudou de propósito: um codigo desconhecido agora
exibe o proprio codigo (`XX`) em vez de "Idioma original". Mostrar "Idioma
original" para algo que o app nao conhece era o que permitia a sobrescrita
silenciosa.

**Efeito nos dados existentes:** uma preferencia gravada como `por`/`eng`
continua funcionando, porque a comparacao de faixas canonicaliza os dois lados
(`por` e `pt` viram `pt`).

### Padrao que se repetiu nas ultimas rodadas

Quatro defeitos corrigidos em sequencia — tema, titulo offline, ordenacao e
idioma — foram todos **lista de valores duplicada em dois modulos**. Tema tinha
dois `when` para o mesmo par de enums; ordenacao tinha dois enums de rotulos;
idioma tinha tres listas de codigos. Vale desconfiar desse padrao antes de
procurar defeitos mais exóticos.

### O que segue pendente

Nada de novo ficou registrado nesta rodada com cenario conhecido. As pendencias
abertas continuam sendo as que dependem de acesso: confirmacao do 429 com
sessao real, login/Quick Connect, auditoria de semantica das telas fora do
player, casting (exige hardware) e a verificacao da adocao de downloads legados
em aparelho.

## Rodada v1.2.48 — três defeitos confirmados por auditoria delegada

Duas auditorias independentes em paralelo (somente leitura) varreram areas que eu
ainda nao tinha olhado: persistencia de settings e stack offline/downloads. As
duas acharam defeitos reais, todos confirmados por leitura de codigo e por teste
que reprova sem a correcao.

### D1 — o tema escolhido nunca era aplicado [ALTO]

`SettingsViewModel` mantinha uma copia privada do mapeamento tema→variante e a
exibia em Configuracoes, mas o app root chamava `MulletaFlixTheme()` **sem** a
variante, entao o default `Dark` valia sempre. Nenhum composable lia
`LocalMulletaFlixThemeVariant`.

Efeito: escolher Claro, Netflix, Purple Haze, Blue Radiance ou Sistema gravava a
preferencia, mostrava o nome escolhido e nao mudava uma cor.

Correcao: a raiz le `settingsRepository.getTheme()` e aplica a variante; o
mapeamento passou a existir em **um** lugar (`ThemeVariantMapper.kt` em
`:feature:settings`, o modulo onde `:domain` e `:design-system` ja se encontram),
com `toThemeVariant()` e o inverso `toAppThemeSetting()`. O
`ThemeVariantMapperTest` verifica que os 8 valores mapeiam e voltam — era
exatamente a duplicacao que deixou os dois lados divergirem.

### D2 — titulo offline exibido com `+` no lugar de espacos [MEDIO]

`MulletaFlixRoute.offlinePlayer` codificava com `URLEncoder`, que transforma
espaco em `+`. O Navigation decodifica query com `Uri.getQueryParameters`
(RFC 3986) e **nao** converte `+` de volta: o player offline mostrava
`O+Retorno+de+Jedi`.

Correcao: `encodeRouteQueryArgument`, um codificador RFC 3986 livre de
`android.net.Uri` para poder ser testado na JVM — no teste unitario
`Uri.encode` retorna null (`unitTests.isReturnDefaultValues = true`), e foi
exatamente isso que esconde o defeito ate agora.

O teste que existia **cristalizava** o comportamento errado
(`route.contains("Movie+%26+Show")`) e foi corrigido. Reintroduzindo o
`URLEncoder`, 5 testes falham.

A metade de decodificacao ficou em
`app/src/androidTest/.../OfflineRouteDecodingTest`, onde o framework real existe.

### D3 — downloads anteriores a 1.0.6 ficavam inalcancaveis para sempre [MEDIO]

Versoes ate 1.0.6 gravavam o id cru da midia, sem `owner:` nem escopo de conta.
`downloadBelongsToUser` filtra entradas sem `owner:`, entao elas nunca apareciam
na lista — nao podiam ser retomadas, removidas, nem alcancadas por "Limpar
concluidos". Os bytes ficavam no aparelho sem nenhuma forma de recuperar o espaco
pelo app.

Correcao: `isLegacyUnscopedDownload` identifica o caso e a conta ativa **adota** a
entrada na primeira vez que a ve, gravando `owner:` e `item:`. O id e resolvido
explicitamente em vez de reler o metadata recem-escrito, para nao depender do
momento em que a edicao fica visivel.

**O que NAO foi verificado:** o caminho de adocao em si. Ele toca
`DownloadManager`, que exige um aparelho com download real, entao a prova foi feita
na politica pura (`DownloadIdentityTest`). A adocao em um aparelho que tenha
downloads da 1.0.6 continua por testar.

### Auditorias que tambem refutaram hipoteses

- **Settings (7 classes verificadas):** chaves unicas, sem default contradizendo a
  UI, sem `collect` reescrevendo o proprio DataStore, sem `LaunchedEffect`
  regravando. A densidade da grade, citada como suspeita, e simetrica e correta.
- **Downloads (7 hipoteses):** `currentUserId` obsoleto, bytes negativos,
  posicao offline cruzando contas, mensagem enganosa e duplo submit foram todas
  refutadas com guarda citada. O fallback de id nao alcanca outra conta, mas e
  codigo morto — consequencia do D3, que agora o torna alcancavel.
- Ainda **nao cobertos**: F2 do achado do settings (ordenacao da biblioteca
  colapsa 3 das 8 opcoes em "Nome"; o round-trip `librarySortCode(librarySortLabel(v))`
  nao e identidade e confirmar "Nome" destroi a escolha) e o idioma de
  audio/legenda que colapsa em "Idioma original". Ficam registrados para a proxima
  rodada, com o cenario e o efeito ja descritos.

## Rodada v1.2.47 — deep link verificado no aparelho e consistência do escopo de downloads

### Deep link verificado nos três caminhos (rodada 9)

Os caminhos de deep link foram exercitados no APK de release instalado na Android
TV, com os dados do app limpos:

| Caminho | Comando | Resultado |
|---|---|---|
| App fechado (cold start) | `mulletaflix://details?id=movie-123` | MainActivity iniciada, sem crash |
| App aberto (`onNewIntent`) | `mulletaflix://details?id=movie-456` | Intent entregue à instância ativa, sem crash |
| Link oficial do servidor | `http://mulletaflix.duckdns.org:8096/web/#/details?id=movie-789&serverId=srv-1` | Aceito pelo filtro do manifesto, sem crash |

O logcat confirmou `START ... cmp=org.mulletaflix.android/.MainActivity` e o
processo permaneceu vivo nos três casos. **O que isso não prova**: que a tela de
detalhe abre com o item certo. Sem sessão autenticada o app para no login, e a
navegação até o detalhe depende de estar logado. A navegação em si está coberta
pelos testes unitários de `MediaDeepLink` e `DeepLinkNavigationPolicy`.

### Consistência do escopo de downloads (rodada 9)

O id de requisição do Media3 é montado como `<conta>::<mídia>`.
`scopedDownloadRequestId` normalizava a conta com `trim()`, mas
`publicDownloadItemId` removia o prefixo **sem** normalizar. Com uma conta
contendo espaços, o prefixo ficaria no lugar e o método devolveria o id completo
como se fosse o id da mídia — falha silenciosa em vez de erro visível.

Corrigido dos dois lados, e `saveSession` passa a gravar o id da conta já
normalizado. O teste `the scope contract trims the account id on both sides`
reprova com o código antigo (`expected:<[]movie-1> but was:<[user-1::]movie-1>`).

**Classificação honesta:** é endurecimento defensivo, não um bug observado. O id
da conta vem do servidor como GUID, então o caso com espaços provavelmente não
ocorre na prática — a inconsistência era real no contrato, mas eu não a vi
disparar. Registro assim para não inflar a correção.

### Armadilha: a API do GitHub limita requisições anônimas

A verificação do digest remoto falhou com `API rate limit exceeded` até a chamada
usar o token do Git Credential Manager. Vale para qualquer script que consulte o
GitHub: autenticar sempre.

## Rodada v1.2.46 — título duplicado no leitor de tela, e duas verificações sem defeito

### Verificação do APK de release em execução (rodada 8)

O APK v1.2.46 foi instalado na AVD `MulletaflixTvApi34` **junto com o pacote
release** (não o debug) e executado. Isso importa porque as mudanças recentes
tocaram a classe `Application` e adicionaram Coil ao caminho de imagens — o R8
roda no release e poderia ter quebrado algo que o build de debug esconde.

- App subiu e permaneceu em execução (`pidof` retornou processo vivo).
- **Nenhum crash** no logcat (`FATAL EXCEPTION` ausente).
- Tela de login renderizada corretamente (ver `artifacts/v1.2.46-release-run.png`).

### A URL `127.0.0.1` na primeira execução era estado antigo, não bug

Na primeira execução o app mostrou `http://127.0.0.1:8096` em vez do padrão
DuckDNS. Investiguei antes de tratar como defeito:

- Não existe `127.0.0.1` como URL padrão em nenhum ponto do código de produção
  (só nas listas de loopback para classificação de host).
- Limpando os dados do app (`pm clear`) e abrindo de novo, a tela mostrou
  `http://mulletaflix.duckdns.org:8096`, o padrão correto
  (`artifacts/v1.2.46-fresh-install.png`).

Conclusão: era estado persistido de sessões anteriores, não um bug. **Não
consegui determinar qual caminho gravou esse valor** — registro isso como
incerteza em vez de inventar uma explicação. O comportamento em instalação limpa
está correto.

### O guarda de diagnóstico de imagem funciona no release

O rótulo `MulletaFlixImage` **sobrevive ao R8** (está no `classes.dex` do APK de
release), porque o app não é minificado a ponto de removê-lo. Isso levantou a
dúvida legítima: o log de URL de imagem poderia escapar em produção?

Medido no APK de release instalado:

- `run-as` responde `package not debuggable` e `dumpsys` mostra `flags=0x0`
  (sem `DEBUGGABLE`) — o APK é uma release de verdade.
- Com o app iniciado, **0 linhas** com `MulletaFlixImage` no logcat.

O guarda por `ApplicationInfo.FLAG_DEBUGGABLE` está correto. O custo é o rótulo
permanecer no binário; a alternativa (propagar `BuildConfig.DEBUG` para o
design-system) foi considerada e não vale a mudança agora, já que o
comportamento está medido.

### Auditoria do player (rodada 7): sem defeito de rótulo

Antes de mexer, auditei os rótulos das ações do player, porque há 39 `Icon(...)`
com `contentDescription = null` espalhados pelas telas e o risco real seria um
`IconButton` sem rótulo (anunciado apenas como "botão").

Resultado da auditoria: **as ações do player estão todas rotuladas** —
"Proporção", "Áudio", "Legendas", "Qualidade", "Velocidade", "Estatísticas",
"Bloquear/Desbloquear controles", "Voltar", além dos controles de transporte.
Os `contentDescription = null` são de ícones **decorativos** (leading icons de
campo, ícones dentro de linhas já rotuladas), que é o uso correto.

Nada foi alterado aqui, e nenhum teste foi mantido: cheguei a escrever um teste
de auditoria, mas ele usava um composable "sonda" próprio e passava sempre — um
teste que não olha o código real não é evidência, então foi removido.

**Fica como pendência verificável**: as telas fora do player (Home, Biblioteca,
Downloads, Live TV, Perfil) ainda não tiveram os rótulos auditados deste jeito.
O caminho honesto é inspecionar a árvore de semântica de cada tela com o app
autenticado, não presumir a partir de `grep`.

### O defeito corrigido

### O defeito e o que o resolveu

Na rodada v1.2.43 eu tinha deixado isto documentado como "aceito, precisa de
decisão de produto". Não precisava: existe correção sem mudar nada do que é
desenhado.

Quando a capa falha ao carregar, o `MediaCard` desenha o título dentro da área da
arte e de novo embaixo. Os dois textos chegam à propriedade `Text` do nó
mesclado, então o leitor de tela lia o título duas vezes. Medido no aparelho:

```
ContentDescription = '[Abrir Filme único, assistido, na Minha Lista, 4K]'
Text = '[Filme único, 4K, Filme único]'
```

- `invisibleToUser()` no fallback: **tentado e medido** — não resolve, porque só
  esconde o nó dos serviços, sem remover o texto do nó mesclado.
- `clearAndSetSemantics { }` no fallback: **resolve**. A semântica do bloco é
  limpa por completo e o desenho visível continua idêntico.

O fallback é puramente decorativo: a `contentDescription` do cartão já leva o
título e o estado de reprodução. O teste
`mediaCardAnnouncesItsTitleOnceWhenArtworkFailsToLoad` reprova se a duplicação
voltar — verificado revertendo para `invisibleToUser()`, quando ele falha.

### Lição repetida nesta rodada

Duas vezes seguidas o caminho foi: hipótese razoável → medir → hipótese errada.
Em v1.2.43 foi a área de toque do player (não havia defeito); aqui foi o
`invisibleToUser`, que parecia resolver mas não removia o texto. Medir antes de
declarar continua sendo o que separa correção de suposição.

## Rodada v1.2.45 — contrato do cliente de capas e uma hipótese refutada

### Investigação registrada: área de toque do player NÃO era defeito

Ainda sobre tamanhos de toque (item P1 do backlog), investiguei os controles
centrais do player, que tinham `.size(44.dp)` e `.size(40.dp)` explícitos em
`IconButton`. A hipótese era que isso reduziria a área tocável abaixo dos 48 dp
exigidos. **Medido no AVD, a hipótese estava errada:** um `IconButton` com
`.size(44.dp)` — e até com `.size(30.dp)` — continua recebendo o
`minimumInteractiveComponentSize()` do Material 3, e a área de toque **não** cai
abaixo de 48 dp.

Consequência prática registrada para não repetir o trabalho:

- Não havia defeito de tamanho de toque nesses botões, e **nenhum teste foi
  mantido** para isso. Cheguei a escrever um, mas ele passava com e sem a
  mudança — um teste que não distingue não é evidência, então foi removido em vez
  de ficar no repositório dando falsa segurança.
- O que ficou de útil é a extração: os controles centrais saíram de dentro do
  `PlayerOsd` (função de ~350 linhas) para o composable `internal`
  `PlayerTransportControls`, com o mesmo código. Agora podem ser exercitados
  diretamente no futuro.
- Fica o alerta de método: antes de afirmar um defeito de acessibilidade, medir
  o comportamento real do framework. Nesta rodada a suposição custou mais que a
  medição.

### O que falta confirmar (honestamente)

O diagnóstico em logcat não produziu nenhuma linha porque o emulador está sem
sessão autenticada — a tela de login não carrega capas. Portanto:

- **Não está confirmado** que o 429 desapareceu; isso exige entrar com uma conta
  real e observar o log do servidor.
- O `api_key` na URL das capas continua existindo e é uma segunda via de
  autenticação. Como não consegui medir o que o servidor recebia antes, não
  afirmo que o 429 vinha do `api_key`; afirmo que as capas agora carregam a
  identidade do cliente, que era o que faltava para o servidor nomear o app.

### v1.2.45 — o contrato do cliente de capas virou teste

O que dava para verificar sem sessão foi verificado: o cliente entregue ao Coil
foi extraído para `buildAuthenticatedImageClient(...)` em `core:api`, e
`ArtworkClientIdentityTest` faz uma **requisição HTTP real** (MockWebServer) por
esse mesmo cliente e afirma o que o servidor receberia:

- `Authorization` com `Client`, `Device`, `DeviceId` e `Token`;
- `User-Agent: MulletaFlix-Android/<versão>`;
- **um único valor** de `Authorization` mesmo se o interceptor rodar duas vezes
  — reintroduzindo `addHeader`, o teste falha com `expected:<1> but was:<2>`,
  então a regressão do cabeçalho duplicado não passa despercebida;
- sessão encerrada continua identificando o cliente e **não** envia token.

O que continua sem verificação é apenas o comportamento do servidor real com uma
conta logada (o 429).

### Como confirmar na próxima sessão

1. Instalar a v1.2.45 e entrar com uma conta real.
2. Abrir Home e Biblioteca com uma biblioteca grande.
3. Observar o log do servidor (`%LOCALAPPDATA%\MulletaFlix\log\log_<data>.log`):
   não deve mais aparecer `Rate limit exceeded for anonymous requests` para o IP
   do dispositivo.
4. Se ainda aparecer, capturar em logcat com
   `adb logcat -s MulletaFlixImage` — a build de debug registra a URL de cada
   capa com o token **redigido**, o que mostra se o `api_key` está presente e
   qual host está sendo usado.

## Rodada v1.2.44 — capas lentas e servidor vendo o app como anônimo

### Relato do usuário

Com o APK atualizado, as capas demoram muito para carregar, e o log do servidor
mostra repetidamente:

```
Rate limit exceeded for anonymous requests from IP 192.168.15.10
```

Ou seja: o servidor **não identifica o app nem o dispositivo** nessas requisições.

### Causa encontrada no código

1. O `AuthInterceptor` do app autenticava as chamadas de **API** com
   `Authorization: MediaBrowser Token=...`, mas **as imagens não passam por ele**.
2. As capas são carregadas pelo **Coil**, que usava o próprio cliente OkHttp
   interno. A única autenticação que sobrava era o `?api_key=<token>` que
   `resolveMediaUrl` acrescenta à URL.
3. Como as capas iam para o servidor sem a identidade do cliente, o
   `RateLimitMiddleware` as contava como **anônimas** (30 requisições / 10 s por
   IP). Uma grade de capas estoura esse limite com facilidade, recebe HTTP 429 e
   as capas carregam devagar — exatamente o sintoma relatado.
4. Bug adicional encontrado: o `AuthInterceptor` usava `addHeader`, que
   **acrescenta** um segundo valor em vez de substituir. Um retry que reentrasse
   no interceptor poderia enviar o cabeçalho duplicado.

### Correção aplicada

- Novo `ClientIdentityInterceptor` (`core:api`) envia em toda requisição o
  `Authorization: MediaBrowser Token=..., Client=..., Device=..., DeviceId=...,
  Version=...` **e** um `User-Agent: MulletaFlix-Android/<versão>`, usando
  `header()` para nunca duplicar valores. Ele substitui o `AuthInterceptor`
  antigo, que foi removido.
- `MulletaFlixApp` agora implementa `ImageLoaderFactory` e entrega ao Coil um
  `OkHttpClient` com os mesmos interceptores de sessão, então **as capas passam a
  ser autenticadas e identificadas** como o resto do app.
- Diagnóstico temporário de URLs de imagem com token **redigido**
  (`redactToken`, testes incluídos), útil para confirmar o que é pedido sem
  vazar segredo.

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
4. **Publicar release do APK somente a cada 10 versões** (decisão do usuário na v1.2.81), com notas que expliquem tudo o que mudou no bloco acumulado. Nas rodadas intermediárias, gerar e testar o APK local, mas **não** publicar.
5. Antes de cada publicação, consultar a release anterior no GitHub e registrar tamanho e digest.
6. Não apagar, resetar ou formatar alterações existentes. O worktree contém alterações de servidor e web pertencentes ao usuário.
7. Não incluir credenciais em código, commits, logs, documentação ou mensagens.
8. A URL remota padrão é `http://mulletaflix.duckdns.org:8096`; na mesma LAN a descoberta deve preferir o endereço local automaticamente.
9. Emuladores devem ser iniciados somente durante o teste e encerrados ao final pelo wrapper.
10. A versão segue SemVer: depois de `1.2.99`, usar `1.3.0`; nunca criar `1.2.100`.
11. Cada tarefa de código deve seguir Builder → testes/lint → Evaluator/Gauntlet → release APK, quando concluída.
12. Existe um processo externo que faz `git commit` automático no repositório. Durante a rodada v1.2.41 ele criou o commit `fbe35724 chore/feat: implement IntroSkipper database maintenance and Android theme enhancements`, que incluiu as alterações do APK **e** alterações de servidor que já estavam no worktree. Verifique o estado do repositório antes e depois de trabalhar; `main` ficou 1 commit à frente de `origin/main` e **nada foi enviado ao remoto** por esta sessão.

## Backlog da auditoria da v1.2.73

Duas auditorias delegadas independentes (somente leitura; nenhum arquivo alterado) varreram a
camada de dados/domínio/`core:api` e a semântica de acessibilidade de todas as telas. Os quatro
achados corrigidos na v1.2.73 saíram daqui; os dezoito abaixo continuam abertos. As linhas são
do worktree naquele momento — confira antes de editar.

### Dados e domínio (10 achados; 10 corrigidos)

> A1 (`ErrorCode`) e M7 (parse das releases) na v1.2.73; A2 (sessão escopada ao servidor) e A3
> (`CancellationException`) na v1.2.74; M1 (Home sem bibliotecas) e M3 (SyncPlay fantasma) na
> v1.2.75; M8 (DeviceId não atômico) e M6 (falha do próximo episódio) na v1.2.79; M4 (busca
> truncada em 30) na v1.2.80; M2 (cache Room morto) na v1.2.81; M9 (API morta e a armadilha do
> `getLiveTvChannels`) na v1.2.82. **Só M5 continua aberto**, e ele está marcado "não mexer sem
> sessão real" — a lista de dados está, na prática, fechada.

- [x] ~~**A2 · ALTA · VERIFIED — a sessão não é escopada ao servidor.**~~ Corrigido na v1.2.74: `shouldClearSessionForServerChange` (a identidade que decide é o `serverId`, não o endereço) derruba a sessão guardada quando o servidor verificado é comprovadamente outro, e mantém quando é o mesmo servidor por outro endereço (LAN↔DuckDNS) ou quando um dos lados não informa identidade. O caminho automático de descoberta (`automaticServerCandidate`) passou a usar o mesmo casamento por identidade que `preferredServerUrl` já impunha ao endereço exibido. 4 + 7 testes, 2 provas por reversão. **Não verificado em aparelho com dois servidores na mesma rede.**
- [x] ~~**A3 · ALTA · VERIFIED — `runCatching` engole `CancellationException`.**~~ Corrigido na v1.2.74: entrou `suspendRunCatching` (relança `CancellationException`) e os 37 pontos de chamada de API dos repositórios passaram a usá-lo; a restauração de URL de `verifyServer` roda em `withContext(NonCancellable)`. 2 provas por reversão, uma delas com um `setBaseUrl` que suspende de verdade (um mock que só atribui variável não teria ponto de suspensão e passaria com o defeito).
- [x] ~~**M1 · MÉDIA · VERIFIED — a Home diz "não há bibliotecas" quando `/Views` falha.**~~ Corrigido na v1.2.75: `HomeFeed.librariesError` → `HomeState.librariesError` → cartão de erro com "Tentar novamente" (`HomeLoadErrorCard`), e o `throw` continua para o caso de nada carregar. 1 teste de use case + 2 de ViewModel, 1 prova por reversão.
- [x] ~~**M2 · MÉDIA · VERIFIED morto — o cache Room nunca é lido nem escrito.**~~
  Resolvido na v1.2.81 **apagando o cache**, e não consertando a chave: `db/MulletaFlixDatabase.kt`
  (entidade + DAO + banco), `di/DatabaseModule.kt` (com o `fallbackToDestructiveMigration(dropAllTables = true)`),
  os dois métodos de `MediaRepository`, os mapeadores `MediaItemEntity.toDomain` / `MediaItem.toEntity`,
  as dependências `room-runtime`/`room-ktx`/`room-compiler` e os `-keep` de Room no proguard saíram
  todos. O `MediaRepositoryImpl` tinha um KDoc anunciando "offline-first strategy" que nenhum código
  implementava; foi substituído pelo registro do que foi embora.
  **Medido no APK:** `androidx/room/RoomDatabase`, `mulletaflix.db` e `MediaItemEntity` apareciam
  1, 1 e 3 vezes nos DEX da v1.2.80 e **zero** na v1.2.81; o pacote encolheu 37.218 bytes.
  **Cache offline de favoritos/recentes continua não existindo** — se for desejado, é funcionalidade
  nova a ser desenhada (com chave `(userId, id)` desde o início), não um resgate deste código.
- [x] ~~**M3 · MÉDIA · VERIFIED — "Entrar na sessão" do SyncPlay nunca abre o player.**~~ Corrigido na v1.2.75: os campos que o servidor não envia (`PlayingItemId`, `PositionTicks`) saíram do DTO, do modelo de domínio e do mapeamento; a navegação morta saiu do `MulletaFlixNavHost`; `joinGroup` não recebe mais um callback com valor sempre nulo; e o texto da tela passou a dizer o que a sala realmente faz. `SyncPlayApiContractTest` passou a prender as quatro rotas e o formato exato do payload do servidor. **Não é provável por reversão** (o defeito era um caminho inalcançável, não um valor errado) — ver a nota de método na seção da rodada.
- [x] ~~**M4 · MÉDIA · VERIFIED — a busca trunca em 30 e joga fora `TotalRecordCount`.**~~
  Corrigido na v1.2.80: `SearchResults(items, totalMatching)` atravessa `:domain` → `:data` →
  `SearchViewModel`, e a tela mostra "Mostrando 30 de 412 resultados" quando o total do servidor é maior
  que a lista. `TotalRecordCount` opcional (DTO cai para `0`) e menor que a página vira `null` — "não
  sei" — em vez de `0`, que a tela leria como "não há mais nada". Provado por reversão nos dois pontos.
  **A segunda metade — "paginar a grade" — foi feita na v1.2.86:** `startIndex` no repositório e no
  UseCase, `loadMore()` no ViewModel (anexa, deduplica por id, para quando não há mais) e o controle
  "Carregar mais" no fim da lista. O aviso perdeu o "refine a busca", que virou contradição com o botão
  logo abaixo.
- [ ] **M5 · MÉDIA · VERIFIED (sintoma SUSPECTED) — os índices de faixa escolhidos pelo servidor
  são descartados.** `MediaSourceDto` (`MediaDtos.kt:80-99`) não tem
  `DefaultAudioStreamIndex`/`DefaultSubtitleStreamIndex`, que o servidor serializa
  (`MediaBrowser.Model/Dto/MediaSourceInfo.cs:125-127`, preenchidos por `MediaSourceManager`), e
  `MediaMapper.kt:159-173` nunca preenche os campos declarados em `MediaItem.kt:181-182`. Então
  `PlayerViewModel.kt:674,679` passa `serverDefaultIndex = null` sempre e
  `TrackPreferencePolicy.kt:38` cai no `IsDefault` do contêiner — que o servidor usa apenas como
  pontuação (`MediaStreamSelector.cs:181-191`), não como escolha. Mascarado quando a preferência
  padrão (`"por"`) casa com alguma faixa — por isso o sintoma é SUSPECTED. **Não corrigir sem
  sessão real para comparar**: mudar seleção de faixa às cegas é trocar um defeito por outro.
- [x] ~~**M6 · MÉDIA · VERIFIED — falha ao procurar o próximo episódio vira "a série acabou".**
  `GetNextEpisodeUseCase.kt:42-43,55-57` converte falha de temporadas/episódios em
  `Result.success(null)`, e o player esconde o aviso de próximo episódio
  (`PlayerViewModel.kt:1477-1493`). Correção mínima: propagar a falha — mas só vale a pena junto
  de um canal de erro no player, senão a falha continua invisível.
- [x] ~~**M8 · MÉDIA/BAIXA · VERIFIED — `DeviceId` não é atômico.** `SessionRepositoryImpl.kt:47-62`
  faz ler-gerar-gravar sem trava, e `ClientIdentityInterceptor.kt:58` chama isso **a cada
  requisição**. No primeiro boot a Home dispara ~5 requisições concorrentes; cada uma lê `null`
  antes de qualquer escrita e gera o seu UUID (a última escrita vence). O mesmo aparelho aparece
  como dois dispositivos no servidor, e a sessão criada no login pode não casar com os relatórios
  seguintes. Correção: `Mutex` ou valor único memoizado.
- [x] ~~**M9 · BAIXA · VERIFIED morto — API sem chamador de produção.**~~
  Resolvido na v1.2.82, em três partes: (1) **armadilha desarmada** — `MediaRepository.getLiveTvChannels`
  virou `getLiveTvChannelPreview`, com `limit` explícito no lugar do default herdado do Retrofit e KDoc
  dizendo que a lista completa é `LiveTvRepository.getChannels`; (2) **duplicatas mortas removidas** —
  `MediaRepository.search` (duplicava `SearchMediaUseCase`) e `MediaRepository.getRecordings`
  (duplicava o paginado `LiveTvRepository.getRecordings`) saíram junto do `MediaRepository.getSuggestions`
  e do `api.getSuggestions`; (3) **adiado com motivo** — `SearchRepository.searchHints` / `api.searchHints`
  **ficaram**: é o encanamento de type-ahead na busca, o `SearchBar` já tem o slot `content = {}` vazio
  esperando por ele, e remover seria decidir produto, não limpar código errado. Ver a seção da rodada.
  **Bônus achado nesta rodada:** a mesma família de defeito de M1 apareceu viva na seção de TV ao vivo
  (falha achatada em lista vazia = "não há canais") e foi corrigida com `HomeFeed.liveTvError`.

### Acessibilidade (12 achados; 10 corrigidos, 2 recusados — lista fechada na v1.2.78)

> a11y-1 (permissão só como cor) e a11y-5 (ação indisponível anunciada como botão) na v1.2.73;
> a11y-3 (cartão sem ação com papel de botão), a11y-4 (ação sem nome enquanto trabalha),
> a11y-6 (estado de "Informações Técnicas") e a11y-12 (nome de enum na tela) na v1.2.76;
> a11y-2 (transmissão com nome inventado), a11y-7 (ordenação sem estado), a11y-8 (arte do
> download repetindo o título), a11y-9 (aviso de offline lido duas vezes) e a11y-10 (servidor
> descoberto chamado de "salvo") na v1.2.77.
>
> **Recusados na v1.2.77, com medição:** as constantes `PLAYER_TOP_BAR_ACTIONS_CONTENT_DESCRIPTION`
> e `REGISTER_DIALOG_CONTENT_DESCRIPTION` são instruções de rolagem que **nenhum filho repete** —
> não são duplicação. Ficaram.

- [x] ~~**a11y-2 · MÉDIA — o controle de transmissão se anuncia três vezes.**~~ Corrigido na v1.2.77: o app deixou de publicar a frase fixa em pt-BR (e `castActionContentDescription` saiu); quem nomeia é o `MediaRouteButton` do Media3 e o rótulo visível. O teste `castActionExposesAUnifiedDescription` reconstruía a semântica dentro dele mesmo e foi reescrito.
  `VideoPlayerScreen.kt:647-663` + `CastPresentation.kt:7-8`: um `semantics(mergeDescendants)` com
  a frase fixa em pt-BR **mais** o `Text` visível "Transmitir" **mais** a descrição localizada que
  o próprio `MediaRouteButton` do Media3 já traz (`media3-cast:1.11.1`, verificado por
  desmontagem). Correção: remover o bloco `semantics`; se o pt-BR for obrigatório, vai para
  `strings.xml`. Verificar também, **medindo no aparelho**, se o `size(40.dp)` deixa o alvo
  interativo abaixo de 48 dp (não confirmado em código).
- [x] ~~**a11y-3 · MÉDIA — `MediaCard` não clicável publica `role = Role.Button` sem ação.**~~ Corrigido na v1.2.76: sem ação o card usa `clearAndSetSemantics { }`, então não duplica o rótulo da linha que o contém. O teste que prendia o defeito (`nonClickableMediaCard_doesNotExposeNestedClickAction`) foi reescrito como `nonClickableMediaCard_isNotAnnouncedAsAButton`, com prova por reversão.
- [x] ~~**a11y-4 · MÉDIA — três ações de Detalhes ficam sem nome enquanto rodam.**~~ Corrigido na v1.2.76: as três usam `busy` + `busyContentDescription`, a fileira saiu para `DetailActionRow` para poder ser medida, e 4 testes instrumentados provam por reversão. `aBusyActionRefusesTheSecondRequest` falha com "Failed to inject touch input" na reversão (indireto, mas legítimo).
- [ ] **a11y-6 · MÉDIA — "Informações Técnicas" não diz se está aberta.** *(fechado na v1.2.76 — ver acima; mantido aqui só como referência do achado original: o rótulo não muda e o chevron é decorativo.)*
- [x] ~~**a11y-7 · MÉDIA — a ordenação atual só existe como um visto.**~~ Corrigido na v1.2.77: cada item (campo e direção) publica `selected`; 3 testes instrumentados provam por reversão. `LibraryScreen.kt:424-452`:
  `DropdownMenuItem` (material3 1.4.0) não tem parâmetro `selected`, então nem campo nem direção
  são anunciados. Correção: `Modifier.semantics { selected = ... }` em cada item.
- [x] ~~**a11y-8 · MÉDIA — o título do download é anunciado duas vezes.**~~ Corrigido na v1.2.77: a arte é decorativa; teste instrumentado com prova por reversão. O primeiro teste derrubava a instrumentação com `android_getaddrinfo failed` — a imagem agora é um PNG local.
  `DownloadsScreen.kt:470-475`: a descrição da arte é o mesmo título do `Text` ao lado; na TV a
  linha mescla e sai "Reproduzir X offline, X". Correção: `contentDescription = null` na arte.
- [x] ~~**a11y-9 · MÉDIA — aviso de offline é lido duas vezes.**~~ Corrigido na v1.2.77 **só na parte que era defeito**: o aviso de offline do player. As duas constantes de rolagem que a auditoria agrupou junto (`PLAYER_TOP_BAR_ACTIONS_CONTENT_DESCRIPTION`, `REGISTER_DIALOG_CONTENT_DESCRIPTION`) são instruções que nenhum filho repete e **ficaram**. Nota de método: a primeira versão do teste contava só nós de texto e passava com o defeito; a asserção que discrimina conta `hasContentDescription(frase) == 0`. `VideoPlayerScreen.kt:385-404`
  (mesma forma em `:949-951` e `LoginScreen.kt:325-327`): `semantics { contentDescription = ... }`
  sem `mergeDescendants` num contêiner cujo filho `Text` já diz a mesma frase. Correção: remover o
  bloco e deixar o `Text` falar.
- [x] ~~**a11y-10 · BAIXA — ícones de botão duplicam a legenda; servidor descoberto é chamado de
  "salvo".** `ServerSelectionScreen.kt:226,295-299`: dois botões têm a mesma frase no ícone e no
  rótulo; e `SavedServerCard` é reusado para `discoveredServers` (:243-251) sob o título
  "Encontrados nesta rede", anunciando "Servidor salvo". Correção: ícones decorativos e um
  parâmetro de origem.
- [x] ~~**a11y-11 · BAIXA — linhas clicáveis sem papel e grupos de rádio sem `selectableGroup()`.**~~ Corrigido na v1.2.78: `SettingsItem` e as linhas clicáveis de Detalhes, Perfil, Login e Episódios publicam `Role.Button`; os oito grupos de opção (três em Ajustes, cinco no player) chamam `selectableGroup()`. 4 testes instrumentados em `:feature:settings` (três com prova por reversão + um controle negativo). **A pendência que sobrava — "o alvo de 40 dp do controle de transmissão precisa de medição de limites no aparelho" — foi medida e fechada na v1.2.84: alvo de toque 48,0 dp × 48,0 dp (layout 40,0 dp), sem defeito.** Ver a rodada v1.2.84.
  `SettingsScreen.kt:619-620`, `ItemDetailScreen.kt:447-450` e `:522-523`, `ProfileScreen.kt:490-494`
  e `:307-312`, `LoginScreen.kt:461-463`, `SeriesSection.kt:125-132`; e as oito listas de opção
  (`SettingsScreen.kt:662-681/700-716/735-751`, `VideoPlayerScreen.kt:978-1008/1040-1054/1078-1092/1123-1166/1189-1203`).
  Todas as linhas têm ≥48 dp (medido), então é só papel/agrupamento.
- [x] ~~**a11y-12 · BAIXA — nome de enum na tela.**~~ Corrigido na v1.2.76: `streamTypeLabel` traduz o tipo de faixa ("Vídeo", "Áudio", "Legenda", …).

### Verificado e **sem** defeito (não reabrir sem motivo)

- Paginação de TV ao vivo (`fetchAllChannels`, `getPrograms`, `getRecordings`) e de
  biblioteca/favoritos: o offset avança pelo que foi recebido, página vazia encerra, teto de
  páginas limita, `SortBy=Random` está corretamente fora da paginação por offset e
  `TotalRecordCount == 0` é tratado como desconhecido — necessário, porque `LiveTv/Channels` não
  tem `enableTotalRecordCount`. Sem laço infinito, salto, duplicata ou parada precoce.
- Os `List` não-nulos dos DTOs (`PlaybackInfoResponse.mediaSources`, `QueryResult.items`) casam
  com o servidor (`Array.Empty` nos dois), então não há NPE em resposta real.
- `GetLiveTvChannelsUseCase` **não** engole mais a falha de gravações (é o `recordingsError`).
- `SearchHistoryRepositoryImpl` chaveia por usuário; `SessionRepositoryImpl.deserializeSavedServers`
  é defensivo; `register` verifica `response.success` (sem "cadastrado" mentiroso).
- Nenhum ícone de controle sem nome; nenhum alvo interativo abaixo de 48 dp confirmado (o único
  candidato é o `MediaRouteButton` de 40 dp, que precisa de medição); nenhuma ordem de `padding`
  encolhendo área de toque; nenhuma lista sem contagem de itens.

## Backlog priorizado

### P0 — validar antes de considerar o APK pronto para uso real

- [ ] Executar smoke test instrumentado no servidor ativo com a conta de teste, sem registrar a senha em arquivos.
  - Parcial: a suíte instrumentada existente (**42 testes** em `app`, `design-system`, `auth`, `downloads`, `home`, `item-detail`, `library`, `player`, `search` e `settings` — os 42 incluem os 3 novos de `:feature:search` da v1.2.80) foi executada com o servidor ativo em `192.168.15.9:8096` e passou integralmente à época (39/39 antes dos 3 novos). Ela cobre boot, branding e telas sem sessão; **o login com a conta de teste continua pendente** porque exige a credencial.
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
- [x] ~~**Retry de download reusa a URL absoluta gravada** (host + `api_key` antigos) quando o endpoint trocou de LAN para DuckDNS.~~ Corrigido na v1.2.66: o repositório acompanha endereço e token atuais e reponta a URL no enfileiramento e no retry. Ligação verificada por compilação + política pura testada.
- [x] ~~**Reprodução em andamento continua apontando para o host anterior** depois da troca automática de endpoint.~~ Corrigido na v1.2.66: o `PlayerViewModel` observa o endereço e reponta o stream preservando posição e play/pause. Ligação verificada por compilação + política pura testada.
- [x] ~~**Provar a ligação do retry de download** (v1.2.66)~~ Provado na v1.2.68: a montagem do pedido virou uma função única, 4 testes instrumentados no AVD de TV a exercitam com tipos reais do Media3, e uma guarda de fonte falha se aparecer um segundo ponto de construção.
- [x] ~~**Provar a ligação do repontamento de playback** (a outra metade da v1.2.66)~~ Provado na v1.2.69: a operação saiu para `PreparedStreamRetarget` atrás de uma interface de cinco membros, com 8 testes de JVM; o guarda de reprodução offline e a leitura tardia do token foram provados por reversão.
- [ ] **Harness de comportamento do `PlayerViewModel`** continua faltando para o resto da classe (prepare, faixas, sleep timer). O padrão da v1.2.69 — extrair a peça atrás de uma interface pequena — é o caminho; um `Player` do Media3 inteiro não é. **Parcial na v1.2.73:** a decisão de "Tentar novamente" saiu para `PlaybackRetryPlan` (tipo selado) com 8 testes de JVM e 3 provas por reversão; o que ficou no `ViewModel` são só os efeitos. **Parcial na v1.2.83:** o **laço** de nova tentativa do próximo episódio saiu para `settleNextEpisodeLookup` com 5 testes de JVM que exercitam o laço (não só as políticas) e uma prova por reversão; a ligação `ViewModel → settleNextEpisodeLookup` e o efeito de publicar o episódio seguem verificados por compilação.
- [ ] Auditoria de acessibilidade das telas: TalkBack, nomes acessíveis de ícones, foco visível e alvos de toque — item P1 do backlog, ainda não medido.
- [x] ~~Trocar o endereço do servidor invalida o cache de capas do Coil.~~ Corrigido na v1.2.67 com chave canônica `serverId + caminho + query sem credencial`, aplicada por um interceptor do Coil; provado por 5 testes de JVM e 4 instrumentados, com reversão.
- [x] ~~Confirmar que a troca entre endpoint LAN e DuckDNS atualiza imagens, playback URLs, deep links e tokens sem cache do servidor anterior.~~ Verificado na v1.2.65: o interceptor reescreve scheme/host/porta/caminho a cada requisição (o cliente de imagem usa o mesmo interceptor), `bestImageUrl` devolve caminho relativo, o deep link carrega só o `itemId` e o link público é montado na hora. **Exceção:** o retry de download (acima) e o playback já preparado.
- [ ] Confirmar que falhas de imagem mostram fallback acessível sem quebrar o scroll ou a ativação do card.
- [x] ~~Confirmar que paginação da Biblioteca não duplica nem pula itens.~~ Corrigido e provado por reversão na v1.2.65: ordem "Aleatório" não pagina mais por offset (era a causa de pular e repetir títulos), a página sobreposta é deduplicada e o offset avança pelo que o servidor entregou. O caso "página menor que o limite" já estava certo (`startIndex` pelo número recebido) e o teste `loadMore pages from the items actually loaded` cobre.
- [x] ~~Confirmar que refresh manual não cancela uma resposta lenta; refresh automático deve continuar não destrutivo.~~ Verificado na v1.2.65 por leitura: `loadJob?.cancel()` + `++requestGeneration` antes de relançar, e toda resposta antiga é descartada por geração. **Ressalva honesta:** `HomeViewModel.loadHome()` não cancela o job anterior, mas o único chamador que não cancela antes é o coletor de sessão, que já cancela na troca de usuário; não foi encontrado caminho reproduzível.
- [x] ~~Verificar que a atualização automática não cria jobs duplicados ao alternar rapidamente entre telas/background/foreground.~~ Verificado na v1.2.65: `repeatOnLifecycle(RESUMED)` dentro de `LaunchedEffect` mantém um único laço, e todo tique passa por `refreshIfIdle`, que recusa iniciar com requisição viva.
- [ ] Verificar permissões e comportamento real de descoberta em Wi‑Fi, Ethernet, VPN e Android TV sem Wi‑Fi.
- [x] ~~Fazer auditoria de logs: URLs, tokens, PINs e credenciais nunca podem aparecer em logs de produção.~~ Feita na v1.2.65 por auditoria delegada independente: uma única chamada de log em produção, já atrás de `isDebuggableApp()` e mascarada; nenhum token/PIN/senha em `UiState` ou mensagem de erro; zero Crashlytics. A guarda passou a varrer os 17 módulos e a exigir `Level.NONE` em todo `HttpLoggingInterceptor`.

### P1 — funcionalidades ainda incompletas ou que precisam de decisão técnica

- [ ] Implementar e testar espelhamento/casting para dispositivos compatíveis. Avaliar separadamente Google Cast/Media3 Cast, Android MediaProjection e reprodução remota compatível com o servidor; não chamar de concluído sem teste real.
- [ ] Validar deep link oficial `/web/#/details?id=...&serverId=...` com endpoint público, sem gerar `localhost`.
- [ ] Validar compartilhamento de filme, série, temporada e episódio com título, capa e metadados corretos.
- [x] ~~Confirmar ícone não redondo seguindo o contorno externo do logo correto e wordmark com `MULLETA` vermelho e `FLIX` branco.~~ Confirmado no APK v1.2.41 instalado na TV: o launcher não recorta o logo octogonal em círculo (o manifesto usa `@drawable/ic_mulletaflix_logo`, não o `mipmap-anydpi-v26`), o wordmark renderiza `MULLETA` em `#E50914` e `FLIX` em branco sobre fundo preto. Ver `artifacts/tv-v1.2.41-final.png`.
- [x] ~~Confirmar tema preto predominante com detalhes vermelhos em login, cards, foco remoto, player, estados de erro e telas vazias.~~ Confirmado no login renderizado na TV. Em v1.2.41 o contraste do vermelho como texto foi corrigido (ver seção da rodada).
- [x] ~~Testar atualização do APK a partir do Centro de Atualizações e confirmar que uma versão dispensada não reaparece durante a sessão.~~ **Parcial na v1.2.85:** o fluxo do aviso automático da `MainActivity` saiu da composição para `AppUpdateViewModel` e ganhou 10 testes de JVM — inclusive "uma versão dispensada não é oferecida de novo" e "uma versão mais nova que a dispensada é oferecida". O que **não** foi feito: a recriação real da Activity num teste instrumentado (exigiria sessão e um downloader falso) e a consolidação com o fluxo paralelo da `SettingsViewModel`. Ver a rodada v1.2.85.
- [ ] **Consolidar os dois fluxos de atualização** (funcionalidade duplicada): a `MainActivity` (checagem automática) e a `SettingsViewModel` (checagem manual no Centro de Atualizações) implementam "checar → diálogo → baixar → instalar" com estados e diálogos separados. A v1.2.85 corrigiu só o primeiro. Regra de atualização mudando = dois lugares para mexer.
- [ ] Verificar suporte de acessibilidade: TalkBack, content descriptions, foco visível e tamanhos de toque.
- [x] ~~Verificar suporte de acessibilidade: contraste.~~ Contraste AA tratado e coberto por testes unitários e por teste instrumentado de pixel. **Verificado na v1.2.73 por releitura:** o `outline` **já é** `#808080` (`Color.kt:55`, com tabela de contraste medida) e os rótulos secundários que reprovavam passam por `readableTextOn` (`ProfileScreen.kt:248,505,514`, `ServerSelectionScreen.kt:118`); as combinações cruas que sobram (α 0.6/0.7/0.75/0.85) medem de 5,3:1 a 7,3:1. O que resta é o ícone de placeholder do `MediaCard` (branco α 0.35, decorativo, `MediaCard.kt:270`) — BAIXA, não é texto. **Acessibilidade de leitor de tela segue aberta:** ver a seção *Backlog da auditoria da v1.2.73* (a11y-2 a a11y-12).

### P2 — melhorias de produto e qualidade

- [ ] Criar matriz de compatibilidade: telefone, tablet, Android TV/Box, resolução, orientação e controle remoto.
- [ ] Adicionar testes Compose instrumentados de Home, Biblioteca, Player, Login, Quick Connect e TV ao vivo nos três perfis.
- [ ] Adicionar teste de processo/recriação para preservar servidor selecionado, preferências de ordenação, idioma e sessão.
- [ ] Adicionar teste de rede intermitente durante login, descoberta, carregamento de capas e playback.
- [ ] Medir tempo até conteúdo utilizável e memória de listas/Coil em bibliotecas grandes.
- [ ] **Type-ahead na busca usando `Search/Hints`** (funcionalidade nova, decisão de produto pendente). O encanamento já existe e foi **mantido de propósito** na v1.2.82: `SearchRepository.searchHints`, `api.searchHints` e `SearchHintDto`. O `SearchBar` em `SearchScreen.kt` já tem um slot `content = {}` vazio. O desenho precisa decidir como isso coexiste com a busca completa que o debounce de 350 ms já dispara — no controle remoto, uma lista sobreposta sobre os resultados é um risco de foco.
- [ ] **Suspeito da auditoria da v1.2.83 (não corrigido, de propósito):** `SearchRepositoryImpl` usa
  `totalRecordCount.takeIf { it >= items.size }`. O guarda nunca produz aviso falso, mas **suprime** o
  aviso quando um servidor reporta `TotalRecordCount == tamanho da página` num resultado de fato
  truncado (a forma `deduplicatedItems.Count` em `ItemsController.cs:969` do fork). Contra o servidor
  deste repositório é inócuo (`EnableTotalRecordCount` é `true` por padrão e `Limit=30` tem valor, então
  o total é `dbQuery.Count()`). **Só um servidor não canônico observa isso** — não mexer sem um.
- [ ] **Suspeito da auditoria da v1.2.83 (baixo impacto):** `BaseItemDtoQueryResultDto.totalRecordCount`
  é `Int = 0`; cobre chave **ausente**, mas um JSON com `null` explícito faria o Moshi falhar a busca
  inteira em vez de ler "não sei". O servidor do repositório sempre serializa número. Verificar só se
  aparecer um proxy/fork que emita `null`.
- [ ] Auditar dependências Android/Media3/Compose e atualizar somente com testes e release reproduzível.
- [ ] **Cache offline de favoritos e "continuar assistindo"** (funcionalidade nova, não correção). A v1.2.81 apagou o cache Room falso que existia; quem quiser o recurso tem de desenhá-lo: chave `(userId, id)` desde o início, escrita no caminho de leitura, invalidação ao trocar de servidor/usuário e uma política explícita de qual vence quando a rede responde — além de dizer na tela quando o conteúdo é do cache.
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

Substitua `<VERSAO>` pela próxima versão. Atualmente a próxima versão é **1.2.81**, com `versionCode` esperado **282**. Não confie neste número: leia os dois arquivos abaixo, porque ele envelhece a cada rodada.

**Cadência de publicação (decisão do usuário, v1.2.81):** release no GitHub **só a cada 10 versões** — próxima é **1.2.90**. Os passos 2, 5 e 6 abaixo só se aplicam quando a versão fecha o bloco de dez; nas rodadas intermediárias, pular do passo 4 para o passo 7 registrando o APK local (arquivo, tamanho e SHA-256) no handoff.

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
  -Uri 'https://api.github.com/repos/raphaelpera85/mulletaflix-completo/releases/tags/app-v1.2.79'
$asset = $release.assets | Where-Object name -eq 'mulletaflix-app-v1.2.79.apk'
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

### 5. Publicar somente o APK — **só a cada 10 versões**

> **Regra do usuário, dada em 22/09/2026 (rodada v1.2.81):** publicar release **somente a cada 10
> versões** do app, e as notas da release devem explicar **tudo** o que mudou no intervalo — não só
> a última rodada. Entre uma publicação e outra, o APK é compilado, testado e instalado
> **localmente**, mas não vai para o GitHub Releases.
>
> Ancoragem: a **v1.2.80** foi publicada (é o início do bloco atual), então a próxima publicação é a
> **v1.2.90**. As versões 1.2.81 … 1.2.89 são acumuladas.

Publicar **apenas** quando a versão fecha o bloco de dez:

```powershell
.\publish-app-release.ps1 -Version <VERSAO> -Notes $notasAcumuladas
```

As `-Notes` têm de cobrir todas as versões do bloco (a última publicação até a atual). Se o bloco
tiver nove rodadas, são nove blocos de mudanças — não repetir o texto padrão do script.

Nas rodadas intermediárias, o passo de release **é omitido**: o build segue com
`.\build-app-package.ps1 -Version <VERSAO>` para gerar o APK local, e o teste de instalação na TV
continua sendo feito (é o que prova que o pacote sobe).

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
