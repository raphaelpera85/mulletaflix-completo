# Triagem contextual de `innerHTML` — 2026-11-07

## Veredito

O scanner inicialmente encontrou 12 atribuições. Após TDD do scanner, 9 atribuições estáticas ou de limpeza deixaram de gerar ruído. As duas construções dinâmicas do GetAvatar revelaram uma falha real de escaping de aspas em contexto de atributo e foram corrigidas. O scanner ainda as reporta heurísticamente. A auditoria AST do IntroSkipper não encontrou fluxo externo para o sink, mas o bundle continua sem fonte/mapa e permanece em revisão de defesa em profundidade.

## Classificação

| Arquivo:linha | Classificação | Evidência | Ação |
|---|---|---|---|
| `Jellyfin.Api/Web/NebulaFTP/configPage.js:743` | Classificado pelo scanner | Limpeza estática; o scanner agora ignora atribuições literais/limpezas. | Nenhuma alteração de produto. |
| `Jellyfin.Api/Web/NebulaFTP/configPage.js:787` | Classificado pelo scanner | Markup fixo de estado vazio; nenhum dado externo. | Nenhuma alteração de produto. |
| `Jellyfin.Api/Web/NebulaFTP/configPage.js:791` | Classificado pelo scanner | Limpeza estática; o scanner agora ignora atribuições literais/limpezas. | Nenhuma alteração de produto. |
| `Jellyfin.Api/Web/NebulaFTP/configPage.js:918` | Classificado pelo scanner | Limpeza estática; o scanner agora ignora atribuições literais/limpezas. | Nenhuma alteração de produto. |
| `Jellyfin.Api/Web/NebulaFTP/configPage.js:1068` | Classificado pelo scanner | Markup fixo de estado vazio; nenhum dado externo. | Nenhuma alteração de produto. |
| `Jellyfin.Api/Web/NebulaFTP/configPage.js:1072` | Classificado pelo scanner | Limpeza estática; o scanner agora ignora atribuições literais/limpezas. | Nenhuma alteração de produto. |
| `MulletaFlix.Plugin.GetAvatar/Configuration/Web/configPage.js:65` | Classificado pelo scanner | Markup fixo para coleção vazia. | Nenhuma alteração de produto. |
| `MulletaFlix.Plugin.GetAvatar/Configuration/Web/configPage.js:68` | Corrigido | `escapeHtml` agora codifica também aspas; TDD DOM provou que payloads com atributos não criam elementos executáveis. | Manter teste DOM; o scanner heurístico continua reportando a linha. |
| `MulletaFlix.Plugin.GetAvatar/Configuration/Web/clientScript.js:56` | Classificado pelo scanner | Modal com markup totalmente constante. | Nenhuma alteração de produto. |
| `MulletaFlix.Plugin.GetAvatar/Configuration/Web/clientScript.js:72` | Classificado pelo scanner | Mensagem fixa de lista vazia. | Nenhuma alteração de produto. |
| `MulletaFlix.Plugin.GetAvatar/Configuration/Web/clientScript.js:75` | Corrigido | `escapeHtml` agora codifica também aspas; TDD DOM provou que payloads com atributos não criam elementos executáveis. | Manter teste DOM; o scanner heurístico continua reportando a linha. |
| `MulletaFlix.Plugin.IntroSkipper/Configuration/introskipper.js:1` | Mitigado por contrato estático; revisão estrutural aberta | AST encontrou um único helper; seus callers de três argumentos usam markup literal, `t.description`/`t.warning` e uma descrição computada local. Todas as 49 chamadas de `U` recebem objetos literais; nenhum `fetch`/`ApiClient` alimenta o sink. | Manter `test_introskipper_sink.mjs`; recuperar fonte/mapa ou trocar o helper por construção DOM/allowlist antes de classificar como encerrado. |

## Decisão

O scanner recebeu uma melhoria de classificação: atribuições `innerHTML` cujo RHS é um literal estático deixaram de ser reportadas. TDD do scanner: 7/7. O TDD DOM do GetAvatar inicialmente falhou com payload contendo aspas, reproduzindo XSS em contexto de atributo; a correção passou 2/2 após codificar aspas simples/duplas. O scanner ainda reporta as duas linhas por ser heurístico. O contrato AST do IntroSkipper passou 1/1 e reduziu a incerteza de fluxo, mas não substitui fonte/mapa nem uma defesa estrutural do helper.

## Limitação

A classificação é estática e contextual. O TDD DOM cobre payloads controlados, mas não substitui validação no runtime instalado. O checklist continua fail-closed enquanto o scanner reportar os 3 padrões heurísticos restantes, embora 2 estejam corrigidos e 1 permaneça aberto.

## Gate do artefato

O publish self-contained `win-x64` foi reconstruído em diretório scratch com o comando oficial de publicação. O `MulletaFlix.Plugin.GetAvatar.dll` resultante contém `&quot;` e `&#39;` nos recursos embutidos. Um DLL preexistente em `bin/.../publish` não continha `&#39;`; foi classificado como artefato obsoleto e não usado para homologação.

## Drift confirmado no runtime instalado

Leitura pública de `http://127.0.0.1:8096/GetAvatar/ClientScript` retornou HTTP 200, mas o bundle instalado ainda contém `return node.innerHTML;`. O source e o publish scratch contêm `return node.innerHTML.replace(/\"/g, '&quot;').replace(/'/g, '&#39;');`. A rota de configuração retornou HTTP 401 sem sessão. Portanto, a correção foi validada em source/artefato, mas não está presente no runtime instalado.
