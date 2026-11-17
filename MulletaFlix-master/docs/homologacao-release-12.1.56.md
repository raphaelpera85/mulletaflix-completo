# Homologação da release 12.1.56

Data: 2026-11-16 (America/Sao_Paulo).

## Comportamento coberto

Na reprodução com `.strm`, o servidor registra o proxy/cache antes de devolver a introdução. A preparação remota espera apenas o primeiro bloco (ou a conclusão/erro da origem); após gravá-lo, a sessão pode servi-lo enquanto a tarefa continua recebendo o restante. Isso permite tocar a introdução durante o cache e, ao final, abrir a mídia pelo endpoint local com os bytes iniciais já disponíveis. O timeout HTTP configurado para origem externa é de dois minutos; o timeout de inatividade do fluxo permanece 45 segundos.

## Evidências locais

- Teste de regressão RED/GREEN de origem lenta: confirma que o primeiro trecho chega antes do EOF remoto; teste de `NativeIntroProvider` confirma que a preparação da sessão precede o retorno da introdução.
- Testes focais de STRM/intro/cache e migração: **12 aprovados, 0 falhas**.
- Sanitizador de logs: **2 aprovados, 0 falhas**; também foram revisados os locais equivalentes de logging no FFmpeg.
- Solução .NET: **6.116 aprovados, 315 ignorados, 0 falhas / 6.431**.
- Nebula Python: **727 aprovados, 2 ignorados, 0 falhas**.
- Web: **444 aprovados em 84 arquivos**, `npm run build:check` passou e o build de produção usado pelo pacote Linux terminou.
- Windows: compilação do stage/NSIS passou; integridade do stage, script de localização do instalador, teste de transporte DuckDNS e inspeção de versão/hash passaram. O FileVersion do instalador é 12.1.56.0.
- Linux: cross-publish Windows x64 e criação do arquivo concluídos. Caminhos do tarball foram listados e o `install.sh` embutido passou `bash -n`; não houve runtime em host Linux.
- Migração SQL gerada sem aplicar em banco: `ALTER TABLE BaseItems MODIFY CleanName varchar(512) CHARACTER SET utf8mb4 NULL;`.

## Artefatos locais

Diretório candidato: `.build/release-audit-12.1.56/dist`.

| Arquivo | Bytes | SHA-256 |
| --- | ---: | --- |
| `mulletaflix_12.1.56_windows-x64.exe` | 444400963 | `343409883EB812EB1BF27950811E15D0482EE8E32916585200025566689704FB` |
| `mulletaflix-update-win-x64.zip` | 409729676 | `5C7DD377F72537464A4893DB8B4D4B23F93405D028F400CD2E6764C23249C026` |
| `mulletaflix_12.1.56_linux-x64.tar.gz` | 126979943 | `54C958E33C8010861B1158461BC7B0C7364FFC82876D6A29514FE6533C1BE24B` |

## Pendências de homologação

- A migração ainda não foi aplicada nem verificada em MariaDB descartável. O banco de produção não foi alterado nesta fase de build.
- O teste STRM com origem controlada comprova a prontidão do proxy e o cache parcial; não foi feita reprodução real com a introdução no servidor Windows instalado.
- Não houve teste visual do assistente NSIS numa VM limpa, nem upgrade instalado nesta rodada. O servidor local estava parado ao início; produção só deve ser considerada atualizada após reinício e checagem de saúde/versão.
- O tarball Linux não foi executado num host Linux.
- CI hospedada do GitHub não foi considerada gate local; as execuções anteriores no projeto foram bloqueadas antes de iniciarem runners por pendência de cobrança. Verificar o status da conta/workflow desta tag após publicar.
- Logs de versões anteriores podem conter URLs/tokens já gravados; a correção previne novos vazamentos nos sinks revisados, mas não limpa arquivos existentes. Rotação de credenciais expostas deve ser avaliada pelo operador.

## Publicação e atualização Windows — 2026-11-16

- Release pública Latest: [v12.1.56](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/v12.1.56), commit `e76a2a7cba21f006404db21000710955f150e409` em `main` e no tag.
- A API pública da release confirma os três assets, tamanhos e SHA-256 iguais aos pacotes locais acima.
- CI hospedada: run [44003569145](https://github.com/raphaelpera85/mulletaflix-completo/actions/runs/44003569145) falhou antes de executar qualquer step; os outros dois jobs foram ignorados. A causa específica desta tentativa não foi confirmada, portanto não é evidência de teste remoto.
- Atualização de produção aplicada em `C:\Program Files\MulletaFlix\Server`; `MulletaFlix.dll` FileVersion é 12.1.56. `GET /health` retornou `Healthy`, e `/System/Info/Public` retornou 12.1.56. `C:\ProgramData\MulletaFlix\Server` foi preservada.
- Não havia serviço `MulletaFlixServer` no host; o servidor está rodando em processo oculto para manter a API disponível. O modo de inicialização persistente/serviço não foi reconfigurado nesta atualização e precisa ser conferido antes de reinicialização/logout.
