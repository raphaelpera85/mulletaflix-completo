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
- CI hospedada, publicação GitHub e atualização do servidor: pendentes de execução.

## Limitações

- Nenhum scan foi executado sobre a biblioteca de produção nesta homologação.
- A homologação de mídia usou arquivos sintéticos e FFmpeg real; a mídia remota de produção não foi exercitada.
- Os jobs GitHub Actions dependem da habilitação financeira da conta; o resultado remoto deve ser registrado após push.
