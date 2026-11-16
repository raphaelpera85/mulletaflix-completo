# Homologação da release 12.1.55

Data: 2026-11-16.

## Correções incluídas

- O IntroSkipper converte erro de leitura e de resolução de `.strm` em `FingerprintException`, mantendo a falha dentro do processamento por mídia; não grava `NoSegments` nem remove item do catálogo por arquivo ausente.
- Workflow CI: variável do Python do teste Mongo transferida para o escopo do step. Teste de contrato normaliza CRLF em workflows no Windows.

## Evidências locais

- RED/GREEN: dois testes de regressão para `.strm` ausente nos caminhos fingerprint e detecção de quadros.
- E2E com FFmpeg instalado e MariaDB descartável em loopback, porta não padrão: análise de STRM, persistência de intros compartilhadas e concorrência máxima 2 — aprovado.
- Suíte completa IntroSkipper: 125 aprovados, 1 ignorado, 0 falhas / 126.
- Suíte unitária Release da solução (`dotnet test ... -m:1 --filter FullyQualifiedName!~Integration`): 6.041 aprovados, 101 ignorados, 0 falhas / 6.142. A primeira passagem apresentou uma falha transitória no teste `LimitedConcurrencyLibrarySchedulerTests.DisposeAsync_WaitsForAdmittedEnqueueToDrain`; o teste isolado passou 10/10, o projeto Controller passou 73/73 e a suíte completa repetida passou sem falhas.
- Suíte Node de release: 34 aprovados, 0 falhas.
- Web: `npm run build:check` sem erros, testes **444/444** e `build:production` concluído; npm reportou 8 vulnerabilidades moderadas no lockfile e avisos de ambiente/jsdom, sem falha de build/teste.
- Nebula Python 3.13 Windows, dependências de runtime travadas por hash e dependências de teste: **727 aprovados, 2 ignorados, 0 falhas**.
- Build Release da solução e publicação self-contained Windows x64 concluídos; stage integrou servidor, web, tray e runtimes requeridos. Foram exibidos warnings preexistentes de analisadores/cref.
- Atualização Windows x64: 409.714.874 bytes; SHA-256 `6074ECF12963BBFF8D4B0CFF9EC01E1182DCB98D9E458DEF0103194D2B54E316`.
- Instalador Windows x64: 444.387.860 bytes; SHA-256 `0CE91D986FF6C2442197A06EC9BED36F684A9DBD7E281DAE7116EEC425A35501`.
- Tarball Linux x64: 126.969.402 bytes; SHA-256 `5E5A02B172D7382E9B046776598DE996D6890188F45690C951CAE4341F73F460`. O archive foi gerado por cross-publish Windows e teve caminhos/componentes inspecionados, mas não foi executado em host Linux.
- Publicação: release pública Latest [v12.1.55](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/v12.1.55), publicada em 2026-11-16; commit `55134a5f5f4710f796485f66a00740457808165b` está em `main` e no tag `v12.1.55`.
- Atualização de produção Windows aplicada com elevação em `C:\Program Files\MulletaFlix\Server`; `MulletaFlix.dll` tem FileVersion 12.1.55 e `IntroSkipper.dll` instalado corresponde ao pacote (SHA-256 `DF2F868D8B5D26754E600977D24458F5F98ACACC780A4B6C10B9004978D315A6`). `/health` respondeu 200 `Healthy` e `/System/Info/Public` informou versão 12.1.55. O diretório de dados não foi alterado.
- CI hospedada não foi verificada: GitHub CLI está sem autenticação nesta sessão.

## Limitações

- Nenhum scan foi executado sobre a biblioteca de produção nesta homologação.
- A homologação de mídia usou arquivos sintéticos e FFmpeg real; a mídia remota de produção não foi exercitada.
- O pacote Linux foi inspecionado após cross-publish, mas não executado em host Linux. Os jobs GitHub Actions também não foram consultados por falta de autenticação local no GitHub CLI.
