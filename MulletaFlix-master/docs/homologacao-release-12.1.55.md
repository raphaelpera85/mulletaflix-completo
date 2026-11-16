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
- CI hospedada: o workflow da tag `v12.1.55` terminou em falha antes de iniciar qualquer job porque o GitHub bloqueou a conta por problema de cobrança (annotation da execução `43888239322`). Os jobs de build foram ignorados; isso não representa falha de teste/build. A execução precisa ser repetida após resolver a cobrança.

## Limitações

- Nenhum scan foi executado sobre a biblioteca de produção nesta homologação.
- A homologação inicial de mídia usou arquivos sintéticos; a rechecagem posterior exercitou transporte e FFmpeg em STRMs reais, sem executar o analisador IntroSkipper nem persistir segmentos.
- O pacote Linux foi inspecionado após cross-publish, mas não executado em host Linux. A validação hospedada aguarda desbloqueio da conta GitHub e reexecução do workflow.

## Rechecagem Windows — 2026-11-16

- O servidor continuou saudável na 12.1.55 (`/health` 200, `System/Info/Public` 12.1.55). Login real na interface confirmou que a imagem do usuário carrega (300×300); o navegador não registrou erro de imagem.
- Uma probe FFprobe em um `.strm` real terminou com conexão recusada em `127.0.0.1:2124`. Não havia listener nessa porta. O `nebulaftp.xml` instalado mostra `Enabled=false` e `HttpStreamPort=2123`; a URL desse arquivo usa o relay HTTP local 2124 da ferramenta Nebula separada. Isso explica a ausência de mídia nesse item, mas não confirma o estado de toda a biblioteca. Nenhum scan de produção foi executado.
- Na sondagem inicial, o Nebula não foi iniciado porque o launcher normal pode iniciar tarefas de manutenção e um feed com opções `--delete-source` e `--prune-completed-strm`. Depois, o usuário autorizou o launcher; quando a validação foi retomada, o relay já estava ativo.
- O teste direto `InstallerLocalization.Tests.ps1` passou. A regressão do workflow ficou coberta localmente: RED reproduziu a ausência do teste na lista Windows; GREEN, após incluir o script e sua asserção de contrato, passou contrato focal 7/7 e Node release 34/34. A validação hospedada da tag e o CI da `main` (`5667e519`) falharam antes de iniciar runners; annotation informa bloqueio da conta GitHub por cobrança (run `43894940916`).
- O código-fonte do instalador declara UTF-8/Unicode e a checagem de localização passou. O instalador público foi baixado novamente (SHA-256 coincide com a publicação) e aberto, mas sua janela em integridade elevada não ficou acessível à ferramenta de inspeção visual; o processo foi encerrado antes de instalar. A renderização final dos rótulos segue sem confirmação visual.
- Após autorização do usuário para iniciar o Nebula de produção, o relay loopback 2124 apareceu ativo; não iniciei uma segunda instância. FFprobe leu um STRM real (duração 1462,503 s; vídeo H.264 e faixas de áudio/subtítulos). Dois episódios reais foram fingerprintados em paralelo pelo FFmpeg instalado: ambos saíram com exit 0 e 705 pontos; a comparação local encontrou uma sequência aproximada compartilhada de 123 pontos com Hamming ≤4. A execução não chamou IntroSkipper nem gravou segmentos no catálogo.
- Ao conferir o processo ativo, não havia `feed_ftp.py` com `--delete-source`/`--prune-completed-strm`; o bot de limpeza e rclone do Nebula estavam presentes. Não foi feita auditoria de todos os efeitos de background do Nebula.
- Rechecagem final da porta 2124: listener ainda ativo e `/health` do servidor retornou 200 `Healthy`. A CI hospedada da `main` após os commits de cobertura/documentação também foi bloqueada antes do runner pela mesma pendência de cobrança (run `43895194778`); nenhum teste remoto foi executado.
- A execução posterior no commit `891089b2` (run `43898878593`) também não homologou o código: a maioria dos jobs recebeu annotation de bloqueio da conta por cobrança, antes de iniciar runners. Dois jobs macOS foram rejeitados pelo GitHub com a configuração interna `macos-14-arm64-us-default` não suportada, embora o workflow peça `macos-14`; não houve resultado de testes desses jobs.
- O run seguinte, `43899449084` para o commit `a2a2b7e2`, também terminou cancelado pelas annotations de cobrança; runners e testes continuam sem execução. Última confirmação local: `/health` 200 `Healthy`, versão 12.1.55, hash instalado do `IntroSkipper.dll` `DF2F868D8B5D26754E600977D24458F5F98ACACC780A4B6C10B9004978D315A6`, relay 2124 ativo.
