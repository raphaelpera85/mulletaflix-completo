# MulletaFlix Android — handoff para continuidade com DeepSeek

Atualizado em: 21/09/2026  
Escopo desta conversa: **somente o APK Android**.

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

- Versão atual do APK: **1.2.40**.
- `versionCode`: **241**.
- Última release: [app-v1.2.40](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/app-v1.2.40).
- APK: [mulletaflix-app-v1.2.40.apk](https://github.com/raphaelpera85/mulletaflix-completo/releases/download/app-v1.2.40/mulletaflix-app-v1.2.40.apk).
- Tamanho confirmado local/remoto: **7.324.745 bytes**.
- SHA-256 confirmado local/remoto: `2B6AF192A9E6C7A7C785E5DB599FF081B8E2B8760C65879A51A7675CCC3A8106`.
- A v1.2.39 anterior também foi conferida antes da v1.2.40: 7.324.745 bytes, SHA-256 `E3AFAAD66048BAB755AA8DA86CCFBEAF5301CC981F0DC0F167E6D07678E3C920`.
- A v1.2.40 foi instalada no Android TV com `Success`; nenhum emulador permaneceu aberto.
- Último Quality Bar verde: `testDebugUnitTest` com **456 tarefas**, lint do APK e `assembleRelease`.

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

## Backlog priorizado

### P0 — validar antes de considerar o APK pronto para uso real

- [ ] Executar smoke test instrumentado no servidor ativo com a conta de teste, sem registrar a senha em arquivos.
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
- [ ] Confirmar ícone não redondo seguindo o contorno externo do logo correto e wordmark com `MULLETA` vermelho e `FLIX` branco.
- [ ] Confirmar tema preto predominante com detalhes vermelhos em login, cards, foco remoto, player, estados de erro e telas vazias.
- [ ] Testar atualização do APK a partir do Centro de Atualizações e confirmar que uma versão dispensada não reaparece durante a sessão.
- [ ] Verificar suporte de acessibilidade: TalkBack, contraste, content descriptions, foco visível e tamanhos de toque.

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
