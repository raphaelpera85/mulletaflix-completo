# Homologação local da release 12.1.52

Data: 2026-11-12
Base oficial escolhida: tag pública `v12.1.51` (`69856155e724b3b7b7ffbcdae018598c8fdcb9ecd7`)
Branch local: `codex/release-v12.1.52-public-base
Worktree: `D:\mf-release-12.1.52

## Resultado

O source candidato 12.1.52, a suíte .NET, o E2E isolado do IntroSkipper, a suíte web e a geração Windows do instalador foram validados localmente. A homologação de publicação permanece **parcial**: o instalador não foi executado em VM/host descartável, nenhum upgrade real ou scan de biblioteca foi feito, e a CI hospedada não foi executada.

## Correções incluídas

- `MulletaFlix.Plugin.IntroSkipper/FFmpeg/StrmInputResolver.cs`: converte URI `file:` para `LocalPath`; preserva URIs remotas.
- `FFmpegService`, `BaseItemAnalyzerTask`, `PluginConfiguration` e `SegmentChange`: log diagnóstico sem stderr, fallback de duração de áudio/contêiner, caminho FFmpeg dinâmico e limite efetivo de duas análises simultâneas.
- GetAvatar: dependência MVC 2.x substituída por `FrameworkReference Microsoft.AspNetCore.App`; cliente nomeado e validação explícita de redirects.
- Web: `UserAvatar` obtém URL via cliente legado autenticado e define o tipo de imagem primária. Corrigido o parâmetro de precisão do teste `toBeCloseTo` que anteriormente era timeout de 10 ms.
- NSIS: fonte UTF-8, geometria revisada, campo protegido de token DuckDNS e botão com URL oficial.
- `package-lock.json`: dependências corrigidas sem upgrade incompatível de ferramentas.
- `FFmpegProcessRunner.RedactArgument`: remove user/password, query e fragmento das mensagens Debug de início e falha de processo. `FFmpegProcessRunnerTests` cobre URL Basic Auth/token e preserva argumentos normais/caminhos locais.
- GetAvatar permite ZIP até 1 GiB e imagens expandidas até 2 GiB. O asset público `Steam.zip` tem 958.257.135 bytes conforme metadado da release consultado; não baixamos nem importamos o asset, então a expansão segue sem validação.

## Evidências observadas

| Gate | Resultado |
| --- | --- |
| `dotnet test MulletaFlix-master\MulletaFlix.sln -c Release --no-restore -m:1` | Exit 0; 5.717 aprovados, 305 ignorados, 0 falhas / 6.022 em 17 projetos. |
| Integração IntroSkipper com MariaDB descartável e FFmpeg | Exit 0; 113 aprovados, 1 ignorado, 0 falhas / 114. Um E2E analisou quatro mídias `.strm`, persistiu os segmentos e mediu concorrência máxima 2. O banco isolado escutou apenas em `127.0.0.1:33099` e foi desligado. `FFmpegProcessRunnerTests`: 4/4. |
| Build Release de GetAvatar | Exit 0. |
| Vitest web | 79 arquivos, 427 testes aprovados; sem falhas. |
| `npm run build:check` e build de produção | Exit 0. |
| `npm audit --omit=dev` | Zero vulnerabilidades de produção. Auditoria completa ainda indica 8 moderadas em dependências de desenvolvimento; a correção automática exigiria upgrade incompatível do `eslint-plugin-sonarjs`. |
| Tray `net472` | Compilação com 0 avisos e 0 erros, usando checkout limpo f882f5f local. |
| Stage | `Assert-StageIntegrity` passou; contém server `win-x64`, web, tray, MariaDB, FFmpeg/FFprobe, NSSM, Nginx e win-acme. |
| Localização DuckDNS | `InstallerLocalization.Tests.ps1` passou. `node --test tools/release/tests/windows-duckdns-token-transport.test.mjs`: 16/16. |
| NSIS | Exit 0; instalador atualizado `mulletaflix_12.1.52_windows-x64.exe`, 447.192.465 bytes, FileVersion 12.1.52.0, SHA-256 `A9E4C3B59933331EDAF6FEB73FFA87F496595DDF9C8ABF1C8122C038ADB83509`. DLL IntroSkipper publish/stage: `4823DE2DFE2DBD2D27005AE98B12DBF56BE0F79A997C119EC75C088B615673D6` em ambos. Authenticode: `NotSigned`. O novo artefato está em `.build/release-v12.1.52-public-base/build/installer-output-fresh/`; a cópia homônima em `dist/` é a compilação anterior e está obsoleta. |
| `git diff --check` | Exit 0. |

Logs de teste e build locais ficaram em `.build/release-v12.1.52-public-base/`; o artefato atualizado está em `build/installer-output-fresh/` dentro dessa worktree. A cópia em `dist/` é anterior e obsoleta.

## Pendências de homologação

- Instalar em VM Windows descartável e validar páginas do instalador em português, campo token DuckDNS, botão abrir site, fluxo Next/Back/Cancel, upgrade/rollback e desinstalação.
- Validar primeiro boot do server publicado, `/System/Info/Public` 12.1.52, configuração de Avatar, navegação de usuário e scan do IntroSkipper em uma biblioteca de teste isolada.
- Corrigir/aceitar explicitamente os 8 avisos moderados de dependências dev, sem forçar upgrade incompatível.
- Executar CI hospedada e revisar artefato/atestação antes de qualquer tag ou publicação.
- Revalidar Android instrumentado: o relatório anterior da suíte Android ampla foi 64/75 com 11 fases host-orquestradas sem argumentos; não foi resolvido nesta trilha do instalador.
- Obter o commit oficial/pinado do submódulo `jellyfin-server-windows` para que o build do tray seja reproduzível a partir da tag principal.
- O resolver `.strm` aceita URI `file:` sem restringir o destino às raízes de biblioteca. Se usuários não confiáveis puderem adicionar `.strm` analisáveis, o FFmpeg poderá tentar abrir arquivos locais acessíveis à conta do serviço. Controlar a origem dos `.strm` até validar o modelo de confiança e o limite das raízes.

Não houve instalação, restart de serviço, acesso ao banco de produção, scan da biblioteca real, tag, push ou publicação.

## Portagem para a base atual da `main` — 2026-11-13

Decisão posterior do usuário: usar a `main` atual e portar apenas as correções 12.1.52. Esta decisão substitui a escolha anterior de migrar a árvore inteira de `v12.1.51`; o ensaio de merge integral documentado acima permanece rejeitado. O patch portado muda 26 arquivos fonte/teste/docs; quatro arquivos adicionais de GetAvatar já estavam idênticos na `main`.

### Gates na árvore baseada em `origin/main` 8438271f8

- Solução .NET Release: 17 projetos, **6.103 aprovados, 315 ignorados, 0 falhas / 6.418**.
- IntroSkipper com MariaDB descartável loopback (`127.0.0.1:33099`) e FFmpeg/FFprobe do stage: **116 aprovados, 1 ignorado, 0 falhas / 117**. O E2E de quatro `.strm` passou; persistência de segmentos e concorrência máxima 2 verificadas. MariaDB foi encerrado e a porta liberada.
- Web: **443/443**; `build:check`, build de produção e `verify:build` passaram. Bundle inicial: 299,8 KiB gzip. `npm audit --omit=dev`: zero vulnerabilidades; auditoria completa ainda lista oito moderadas em dependências de desenvolvimento, exigindo upgrade incompatível do SonarJS para correção completa.
- `InstallerLocalization.Tests.ps1` e `windows-duckdns-token-transport.test.mjs`: passaram; o transporte DuckDNS ficou em **16/16**.
- Build `win-x64`, tray `net472`, stage e NSIS concluíram; `Assert-StageIntegrity` passou. Instalador 12.1.52.0: 444.387.312 bytes, SHA-256 `797564D54434F875330BC890D35AF84429C03AC430DB47D6EE22A47DB131C4ED`, Authenticode `NotSigned`. Artefato em `.build/integrated-main-12.1.52/build/installer-output/`.

O instalador não foi executado em VM descartável; instalação/upgrade, primeiro boot e scan em Windows continuam pendentes. CI hospedada não foi executada. O submódulo de tray permanece não pinado pela fonte principal e foi compilado do checkout limpo f882f5f2ca7d84322f90b647114667273ee08b21.

### Resultado remoto e limite da validação visual — 2026-11-13

- O commit `b89224cb4fc4ed3ce66a36ceb2deb1fd1f28bf27` foi confirmado em `origin/main` (fast-forward a partir de `8438271f8d9b99e0b80e047eff23c375668eaa8c`). O workflow GitHub Actions run `43356247471` terminou com `failure`; a API do GitHub retornou zero jobs e zero check-runs. A causa não foi identificada; portanto, CI hospedada não está aprovada.
- A inspeção visual do instalador em Windows não foi executada: abrir/executar este binário local exige confirmação no momento da ação. Os testes `InstallerLocalization.Tests.ps1`, transporte DuckDNS 16/16 e a compilação NSIS passaram, mas não demonstram a renderização da tela em runtime.
- Nenhuma instalação, atualização ou alteração no serviço deste computador foi realizada. Permanecem pendentes a confirmação para executar o instalador em ambiente isolado, homologação visual/fluxo do instalador, CI hospedada e verificação independente do deploy de produção.
