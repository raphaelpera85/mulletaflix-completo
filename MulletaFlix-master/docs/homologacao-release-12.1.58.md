# Homologação da release 12.1.58

Data: 2026-11-17 (America/Sao_Paulo).

## Correções e fluxo validado

- `UserLibraryController.GetItem` agora resolve itens de introdução transitórios registrados durante playback quando não existem na biblioteca persistente. Itens transitórios não passam por priorização Nebula nem refresh de metadados da biblioteca.
- `MediaSegmentManager.GetSegmentsAsync` usa `Enumerable.Contains` explicitamente no filtro de providers. A chamada implícita anterior era compilada como `MemoryExtensions.Contains(ReadOnlySpan<T>)` e falhava na avaliação da expressão do EF Core.
- O player já existente solicita as intros antes da mídia principal e inicia `prebufferItemDuringIntro()`/`warmupMediaStream()` durante a introdução. O servidor mantém um prefixo parcial da origem `.strm` para iniciar a reprodução e continua a transferência sob demanda; não baixa a mídia inteira.

## TDD e regressão

- `GetItem_ReturnsRegisteredTransientIntro`: RED no comportamento anterior (404 para item fora da biblioteca); GREEN após buscar o registro transitório.
- `GetSegmentsAsync_FiltersByProviderId`: RED reproduziu a exceção de avaliação LINQ/EF associada ao overload de `ReadOnlySpan<T>`; GREEN com `Enumerable.Contains`.
- `Jellyfin.Api.Tests`: 1.248 aprovados, 3 ignorados, 0 falhas / 1.251.
- Solução .NET Release: 6.118 aprovados, 315 ignorados, 0 falhas / 6.433 em 17 projetos.
- Web: 5 testes focais de intro/prebuffer passaram em 2 arquivos. Saída do ambiente JSDOM incluiu aviso de `window` após o resultado verde; exit code 0.
- `Assert-StageIntegrity.ps1`, testes de localização/acentuação do NSIS e token DuckDNS passaram. O instalador foi compilado, versão `12.1.58.0`.
- Script Linux passou `bash -n` pelo Git Bash. WSL não tinha distribuição; runtime nativo Linux não foi testado.
- Pacotes: ZIP Windows abriu e listou 3.265 arquivos; tarball Linux listou 2.466 entradas e contém executáveis/script esperados.

## Homologação Windows instalada

- Atualização aplicada em `C:\Program Files\MulletaFlix\Server`, mantendo `C:\ProgramData\MulletaFlix\Server`. A comparação inicial do atualizador usava FileVersion com sufixo `.0`; o stage reporta `12.1.58`, então o script local foi ajustado ao formato real e a cópia terminou com código 0.
- `/health` respondeu HTTP 200. Interface autenticada reportou versão 12.1.58 e `Healthy 0 running task(s), 0 recent alert(s)`.
- Reprodução real em navegador autenticado: o endpoint `/Intros` foi solicitado; o vídeo inicial de introdução tocou (`paused=false`, `readyState=4`, duração aproximada de 10 s); o console registrou `Main media prebuffer successful` enquanto a intro tocava; em seguida, o vídeo mudou para a mídia principal (origem `blob:` do player, `paused=false`, `readyState=4`). A sessão seguiu para `.strm` sem aviso de falha de introdução.
- Nenhum novo erro 500 de segmentos apareceu nos logs da reprodução atual ou nas últimas linhas dos logs de produção. Os erros 500 do console correspondem à execução anterior à correção (04:06); a reprodução 12.1.58 ocorreu às 04:33. O player registrou um aviso HLS `bufferStalledError` não fatal durante a transição; a reprodução continuou.
- O episódio de ensaio ficou com estado não assistido e o player foi encerrado após a validação. Uma verificação anterior de IntroSkipper em produção foi interrompida em 80% pelo restart; não é declarada concluída.

## Artefatos locais

| Arquivo | Bytes | SHA-256 |
| --- | ---: | --- |
| `mulletaflix_12.1.58_windows-x64.exe` | 444400986 | `0F0FBD808231DD7F290B1234F41D7E33DA55165873E11BE6C16BA69305130E75` |
| `mulletaflix-update-win-x64.zip` | 409729675 | `31818EE682B4E6AAC644B9B2AD88CAAC698115028172F6AC9EACE5BEE9F82B3E` |
| `mulletaflix_12.1.58_linux-x64.tar.gz` | 129297506 | `907D19271B808044BA7D61F96112FF012987C06997F3E83E6A64555F25389527` |

## Publicação e limites

- Publicada como Latest: [v12.1.58](https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/v12.1.58), tag `v12.1.58` aponta para `d40f76a9255b8529de17ed3f26443cc6127a909b`, commit de release presente na história de `main`. A API remota confirmou três assets com tamanhos e SHA-256 iguais aos pacotes locais acima.
- CI hospedada: run [44018912372](https://github.com/raphaelpera85/mulletaflix-completo/actions/runs/44018912372) falhou em quatro segundos antes de steps observáveis; os jobs Linux e release foram ignorados. A causa não foi confirmada. Gates locais continuam aprovados.
- VM limpa/instalação limpa, runtime nativo Linux e conclusão do scan real interrompido permanecem pendentes.
- Os testes e runtime não provam desempenho sob concorrência de múltiplos clientes. O prebuffer parcial conserva o comportamento já configurado; dependência da disponibilidade/latência da origem `.strm` permanece.

## Fontes

`MulletaFlix-master/Jellyfin.Api/Controllers/UserLibraryController.cs`, `MulletaFlix-master/Jellyfin.Server.Implementations/MediaSegments/MediaSegmentManager.cs`, testes em `MulletaFlix-master/tests/`, fluxo web em `MulletaFlix-web-master/src/components/playback/playbackmanager.ts`, `TDD-PLAN.md` e log do servidor Windows (consultado sem copiar URLs/credenciais).

Revisão adversarial confirmou coerência do fluxo transient intro/prebuffer e apontou o gate de notas da release; notas adicionadas em `docs/releases/12.1.58.md`, caminho consumido pelo workflow. Limite adicional: teste de `MediaSegmentManager` usa EF InMemory e não reproduz SQL MariaDB. A equivalência do filtro foi reproduzida em EF; tradução do provider MariaDB não foi testada nesta rodada.


## Publicação — 2026-11-17

A tag `v12.1.58` aponta para `d40f76a9255b8529de17ed3f26443cc6127a909b`, commit presente na história de `main`. A release está publicada como Latest em https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/v12.1.58. A API do GitHub verificou installer, ZIP e tarball, bytes e digests correspondentes à seção de artefatos. A CI da tag (run `44018912372`) falhou em quatro segundos antes de steps observáveis; os jobs de Linux e publicação foram ignorados. Causa da falha não confirmada, portanto nenhuma validação hospedada é creditada.
