# Homologação da release 12.1.53

Data: 2026-11-13. Base: `origin/main`/`f82b04b1542ff9689a441a0330169b8f8234c699`.

## Correção

O IntroSkipper aplica timeout mínimo de 900 segundos a probes FFprobe e scans
que recebem um caminho `.strm`; timeout configurado acima disso é respeitado e
`0` continua ilimitado. Probes de mídia local continuam com 10 segundos e scans
locais usam `ScanTimeoutSeconds`. A interface identifica concorrência como
quantidade de arquivos simultâneos (1–2).

## Verificações

- Suíte .NET Release do workflow, com filtro `FullyQualifiedName!~Integration`:
  executada em Windows/.NET 10; todos os resultados de projeto observados
  passaram. As integrações foram excluídas pelo filtro.
- `FFmpegAudioDurationTests`: 10 aprovados, 0 falhas.
- `IntroSkipper.Integration.Tests`: 66 aprovados, 58 ignorados, 0 falhas / 124.
  Os ignorados não receberam crédito.
- Build Release do plugin: 0 avisos, 0 erros.
- `node --check` da configuração do IntroSkipper, assertivas estáticas de UI e
  `git diff --check`: passaram.
- Build isolado `win-x64`, web, tray `net472`, extras de runtime, stage e NSIS:
  concluídos. `Assert-StageIntegrity.ps1` passou após o empacotamento do ZIP.
- Installer: 12.1.53.0, 444.387.430 bytes, SHA-256
  `FA760AB67B34FA87498030EEAC0C18836231B7AA9DFAB0930FD71CEED867B3A9`,
  Authenticode `NotSigned`.
- Update ZIP: 409.714.493 bytes, SHA-256
  `9F541494AB3C71623DC0CE759C6FE40A9EFCF260B38923D6C6BF7CE945D0115A`.
- DLL `IntroSkipper.dll` no publish e no stage: SHA-256 idêntico
  `6B729AEB1D2F6B75B8DCF8C10C5A16C5B1BC418DDC0A330B7380CB1C8F10A665`.
- ZIP contém 3.262 entradas e inclui `IntroSkipper.dll`, `MulletaFlix.dll` e o
  cliente web.

## Limites

Não foram exercitados uma origem de streaming externa real, instalação visual
em VM Windows, CI hospedada, primeiro boot de produção, Android instrumentado ou
iOS. A release inclui artefatos Windows; não foi construído pacote Linux nesta
homologação local. A assinatura Authenticode não está configurada.
