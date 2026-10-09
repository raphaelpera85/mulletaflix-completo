# TDD do projeto MulletaFlix

Este documento detalha o inventário funcional, matriz e backlog de adoção do plano executivo [`TDD-PLAN.md`](../TDD-PLAN.md). Ambos cobrem os produtos e ferramentas deste repositório; os guias específicos continuam sendo a fonte dos comandos e cenários por plataforma:

- Android: [`MulletaFlix-android/TDD-PLAN.md`](../MulletaFlix-android/TDD-PLAN.md) e [`MulletaFlix-android/TESTING.md`](../MulletaFlix-android/TESTING.md).
- iOS: [`MulletaFlix-iOS/MACOS-VALIDATION.md`](../MulletaFlix-iOS/MACOS-VALIDATION.md).
- Web: scripts em [`MulletaFlix-web-master/package.json`](../MulletaFlix-web-master/package.json), testes unitários em `src/**`, Playwright e Cucumber.
- Servidor e integrações: `MulletaFlix-master/MulletaFlix.sln`, `MulletaFlix-master/Tools/NebulaPython/` e a suíte Python independente `nebula/`.
- Portal e ferramentas de release: `portal-site/`, `tools/` e scripts de build/publicação na raiz.

## Princípio e limites

Toda mudança de comportamento deve começar com um teste que expresse o resultado observável esperado. O teste deve falhar antes da implementação pela regressão correta, passar após a menor correção e continuar passando após refatoração. Testes existentes, cobertura de linhas e build verde não provam, por si só, que o ciclo RED-GREEN foi seguido.

Mudanças exclusivamente documentais, assets gerados e arquivos de configuração sem comportamento executável podem dispensar um teste RED; ainda exigem validação estrutural apropriada. Não invente percentuais de cobertura. Informe comandos, resultados, testes ignorados e limitações reais.

O escopo da mudança determina a suíte obrigatória. Não execute serviços reais, contas, tokens, keystores ou publicação externa em testes. Use fixtures temporárias, fake servers e credenciais fictícias. Testes E2E contra servidor real exigem ambiente de teste autorizado e dados controlados.

## Ciclo obrigatório RED-GREEN-REFACTOR

1. **Defina intenção:** descreva o comportamento, o limite do componente e a regressão que o teste detectará.
2. **RED:** escreva um teste pequeno na camada proprietária e execute-o antes de alterar o código de produção. Confirme falha por assertion/resultado esperado. Falha de compilação, fixture ou infraestrutura não conta.
3. **GREEN:** implemente somente o necessário e rode exatamente o teste novamente.
4. **REFACTOR:** simplifique após o verde, preservando o teste; execute novamente a suíte afetada.
5. **GAUNTLET:** execute os gates da tabela abaixo, mais os requisitos específicos do módulo. Um teste inteiramente ignorado não prova aprovação.
6. **EVIDÊNCIA:** registre comandos e contagens reais de aprovados, falhas, erros e ignorados. Identifique o perfil/dispositivo quando relevante.

Evite testes que afirmam apenas que um mock foi chamado. Prefira testar saída, estado persistido, evento, arquivo, requisição HTTP ou interação que o usuário/consumidor observa. Use mocks apenas quando a dependência não for controlável de outro modo; prefira fakes determinísticos para rede, relógio, armazenamento e sistema externo.

Modelo de evidência por mudança:

```text
INTENT: <comportamento e regressão observável>
RED: <comando> — falha esperada: <assertion/resultado>
GREEN: <comando> — <aprovados/falhas/erros/ignorados>
GAUNTLET: <gates executados e resultados>
DEVICE/ENVIRONMENT: <PHONE/TABLET/TV, SO, browser ou não aplicável>
LIMITAÇÕES: <lacunas reais ou nenhuma>
```

## Propriedade de testes e quality gates

Execute comandos no diretório indicado. Rode gate focal primeiro e suíte integral do produto afetado antes de encerrar. Para mudanças cross-product, execute todos os produtos tocados; não há comando único portátil porque iOS exige macOS/Xcode.

| Frente | Suíte focal/integral | Verificações adicionais e observações |
| --- | --- | --- |
| Android APK | Em `MulletaFlix-android`: `./gradlew.bat testDebugUnitTest --no-daemon --console=plain` | Módulo alterado e app: `./gradlew.bat :<modulo>:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain`. Para UI/plataforma: `tools/with-emulator.ps1` com perfil explícito `PHONE`, `TABLET` ou `TV`; valide relatório instrumentado não vazio. Guia detalhado em `TESTING.md`. |
| iOS Core/app | Em `MulletaFlix-iOS` no macOS: `swift test --enable-code-coverage` | `bash scripts/run-simulator-tests.sh` para Core + build de simulador. Para integração UI, execute `xcodebuild test` com destination de Simulator apropriado; o script atual não substitui cobertura UI. Windows: `./scripts/validate-structure.ps1` é apenas checagem estrutural, não teste Swift. |
| Web | Em `MulletaFlix-web-master`: `npm test -- --run` | `npm run build:check`, `npm run lint`, `npm run stylelint`, `npm run build:production`, `npm run verify:build`. Mudanças de interação também exigem Playwright apropriado; o E2E completo depende de fixtures/serviços descritos na configuração da suíte. |
| Servidor .NET | Na raiz: `dotnet test MulletaFlix-master/MulletaFlix.sln` | Para APIs/DB/processos/instaladores, execute os projetos de integração relevantes. Use `dotnet test ... --no-restore` somente se assets de restore estiverem atualizados. Não confunda testes unitários com validação de banco/FFmpeg/OS real. |
| Ferramentas Nebula Python | Em `MulletaFlix-master/Tools/NebulaPython`: `python -m pytest` | Instale dependências de teste do `requirements-test.txt` no ambiente virtual isolado. Para regressão focal: `python -m pytest tests/<arquivo>.py`. Cobertura opcional: `python -m pytest --cov --cov-report=term-missing`; só reportar percentuais deste relatório e escopo. |
| Nebula Python independente | Em `nebula`: `python -m pytest tests tools` | `nebula/pytest.ini` restringe a descoberta padrão a `tests/`; passe `tools/` explicitamente para executar também os testes utilitários. Use o ambiente/dependências declarados pelo projeto. |
| Portal | Na raiz: `node --test portal-site/tests/*.test.mjs` | Validar HTML/JS alterado e `portal-site/homologacao-status.json`. Para mudanças de rotas públicas, validar HTTP 200 e conteúdo publicado após deploy autorizado; teste local não prova deploy. |
| Shell/instaladores Linux | Na raiz, Bash/Git Bash/WSL conforme ambiente: `bash tools/release/tests/linux-atomic-write.test.sh`, `bash tools/release/tests/linux-install-preflight.test.sh`, `bash tools/release/tests/duckdns-update.test.sh` | Execute os testes relacionados ao script alterado. Requer ambiente Bash e utilitários POSIX; execução no PowerShell nativo não é equivalente. |
| Scripts Windows PowerShell | Na raiz: `Invoke-Pester -Path ./tools -CI` e `Invoke-Pester -Path ./MulletaFlix-android/tools -CI` | O primeiro caminho inclui testes de release e validadores Android na raiz de `tools/`; o segundo cobre as ferramentas específicas do projeto Android. Scripts de release/build exigem fixtures temporárias; nunca executar publicação nem usar segredos em Pester. Verifique `Get-Command Invoke-Pester` e versão antes do gate. |
| Script Node de release | Na raiz: `node --test tools/release/tests/windows-duckdns-token-transport.test.mjs` | Execute separadamente dos testes Pester e Bash. Não publica artefatos. |
| Packaging/build | Scripts de validação do pacote afetado e testes das plataformas acima | Validar conteúdo, versão, assinatura/hash e ausência de artefato obsoleto por metadados. Não publicar como parte de testes. |

Os comandos acima são referência de gates por componente. Se um script ou CI declarar comando mais específico, consulte-o e inclua-o; não reduza o gate apenas porque um alvo caro está indisponível. Registre indisponibilidade e deixe validação pendente.

## Mapa funcional de testes do repositório

Esta matriz define o inventário que deve ser fechado por comportamento. Ela não afirma que todos os casos já existem nem que qualquer frente está homologada. Ao trabalhar uma área, localize primeiro a implementação e os testes atuais; atualize o registro de rastreabilidade antes de fechar a lacuna.

| Frente / componente | Contratos e comportamentos que o TDD deve cobrir | Nível de teste esperado | Prioridade inicial |
| --- | --- | --- | --- |
| Servidor — identidade, contas e autorização | login, criação de conta, sessão, permissões por usuário/biblioteca, isolamento de dados, revogação e respostas não autorizadas | unitário de política + integração API/DB isolada | P0 |
| Servidor — catálogo e metadados | scan/refresh, inclusão/remoção, identidade de filmes e episódios, paginação, atualização parcial, imagens/NFO e consistência após falha | unitário de regra + integração com DB/filesystem temporário | P0 |
| Servidor — reprodução e entrega de mídia | seleção Direct Play/transcode, range, seek, legendas/faixas, cancelamento, timeout, limites de rede e URL externa/LAN | unitário de decisão + integração HTTP/processo controlado; dispositivo real quando codec/hardware exigir | P0 |
| Servidor — APIs de usuário | favoritos, progresso, Minha Lista, pedidos de mídia, relatório de reprodução e estados vazios/erro | integração API com DB isolada | P1 |
| Servidor — tarefas e notificações | gatilhos, conclusão de metadados antes de notificar, deduplicação, idempotência, retry, cancelamento e falha do provedor | testes determinísticos com fake de fila/provedor + integração de persistência | P0 |
| Servidor — TV ao vivo e SyncPlay | EPG, canal/ID, estado do grupo, eventos fora de ordem, cliente desconectado e reconexão | unitário de máquina de estados + integração WebSocket/API | P1 |
| Nebula — ingestão, deduplicação e arquivos | identidade canônica, não reenviar item concluído, STRM, staging, hierarquia legada/nova do Mongo, retomada, cancelamento e limpeza segura | pytest/.NET unitário + Mongo isolado; nenhum Telegram/Supabase real em unit tests | P0 |
| Nebula — provedores externos e backup | limites, falha parcial, retry, checkpoint, consistência do backup/restore e credenciais ausentes | contratos com fake HTTP + integração opt-in isolada | P1 |
| Web — sessão, navegação e catálogo | login/logout, autorização visual e de rota, busca/filtros/ordenação, paginação, deep links, carregamento, vazio/offline/erro | Vitest/componentes + Playwright para jornadas críticas | P0 |
| Web — player e administração | reprodução, seek, faixas, acessibilidade por teclado, ações administrativas, formulários, confirmação e proteção de dados sensíveis | teste de componente + Playwright; serviços fake/stage de teste | P0/P1 |
| Android — autenticação e rede | login/registro, Quick Connect, descoberta LAN, HTTPS remoto, troca de servidor, persistência/isolamento de sessão e bloqueio de envio de credenciais | JVM/MockWebServer + persistência instrumentada PHONE/TABLET/TV | P0 |
| Android — navegação e bibliotecas | restauração/logout/deep link, atualização automática, biblioteca correta por tipo de dispositivo, capas retangulares, scroll, busca e ordenação ascendente/descendente | JVM para estado + Compose instrumentado nos três perfis | P0/P1 |
| Android — player e transmissão | áudio/legenda, seek, controles D-pad, PiP, pausa no lifecycle, Cast conforme perfil, sessão remota e erros de mídia | JVM de política + instrumentação; TV/tablet/celular conforme interação | P0 |
| Android — livros e downloads | formatos EPUB/CBZ/CBR/PDF suportados, TOC/progresso, erro/retry, orientação, download/cancelamento/reinício/arquivo offline e cache | JVM/parser + integração de arquivo/HTTP + Compose em PHONE/TABLET; TV deve ocultar livros e downloads | P1 |
| Android — release e atualização | versão, URL/hash, redirect, assinatura, integridade, consentimento/instalação, retry e rejeição de artefato velho | JVM + instrumentação de intents/URI + Pester/inspeção do pacote; publicação real fora da suíte | P0 |
| iOS — Core e cliente | contratos de API, auth/persistência, biblioteca/detalhes, busca, ciclo de reprodução, lifecycle, acessibilidade e diferenças de dispositivo suportadas | Swift Testing/XCTest + Simulator UI/E2E; hardware real para capacidades não simuláveis | P1 |
| Portal — publicação e documentação | sincronização de release, links/versões/notas, sitemap/canonical, formulários e status de homologação sem conteúdo desatualizado | node:test para contratos/dados/HTML + validação HTTP/deploy autorizada como etapa separada | P1 |
| Build, packaging e scripts | entradas inválidas, paths com espaços, versão/package incorretos, checksum/assinatura, conteúdo do pacote, falha parcial, rollback, repetição e não publicação acidental | Pester/Node/Bash com temporários e executáveis falsos + inspeção de artefato | P0 |
| Contratos cross-platform | schema de mídia, erros, paginação, IDs, progresso e compatibilidade entre API e consumidores | fixtures versionadas + consumer tests em cada plataforma afetada | P1 |

### Registro de rastreabilidade

Mantenha um registro por requisito/comportamento (em issue, checklist de tarefa ou seção do plano específico), com estes campos:

```text
ID: <frente-área-comportamento>
REQUISITO: <resultado observável, sem prescrever implementação>
PROPRIETÁRIO: <módulo/projeto>
TESTE: <arquivo/classe/caso focal e integração relacionada>
ESTADO: <sem inventário | teste legado sem RED comprovado | RED observado | GREEN validado | bloqueado>
EVIDÊNCIA: <comandos, contagens e links/relatórios locais rastreáveis>
AMBIENTE: <JVM/API/DB/OS/dispositivo/perfil necessário>
BLOQUEIO: <dependência externa ou nenhum>
```

Não marque requisito como coberto apenas por encontrar um teste de nome parecido. Verifique que a asserção falharia com a regressão descrita. Para testes preexistentes sem histórico, use `teste legado sem RED comprovado`; ao alterar comportamento, acrescente regressão nova e registre o RED observado. Não use quantidade bruta de testes como percentual de cobertura.

## Sequência para criar o TDD em todo o projeto

1. **Inventário:** enumerar requisitos críticos por área, módulo dono, testes existentes, suites executáveis, ambientes e dependências externas. Registrar desconhecidos como pendentes, sem inferir cobertura.
2. **Fundação P0:** priorizar autorização/credenciais, integridade e perda de dados, ingestão/deduplicação, reprodução/download, migração/persistência, assinatura e publicação de artefatos.
3. **Fluxos de produto P1:** cobrir estados de sucesso, vazio, erro, offline, retry, concorrência e cancelamento em web, Android e iOS; executar testes de dispositivo apenas para perfis afetados.
4. **Integrações P2:** adicionar contratos cross-platform e E2E isolados para DB, filesystem, WebSocket, provedores externos e deploy. Integrações com serviço real permanecem opt-in e nunca substituem o teste determinístico.
5. **Fechamento:** revisar cada requisito contra a regressão que o teste detecta, rodar todos os quality gates da frente, relatar casos ignorados/falhas/bloqueios e manter a matriz atualizada.

O objetivo não é converter cada função privada em um teste, nem executar todas as suítes em cada mudança. O objetivo é associar cada requisito observável a testes estáveis na camada correta, executar a suíte integral da frente alterada e demonstrar RED-GREEN para cada mudança de comportamento.

## Backlog inicial de adoção por frente

Este backlog converte o inventário atual em trabalho TDD incremental. É uma lista de cobertura a construir, não uma declaração de defeitos nem de homologação. Antes de cada implementação, criar o teste focal e registrar RED válido; fechar cada item só com evidência GREEN e gates da frente.

| Prioridade | Frente | Primeiro comportamento a proteger | Teste focal e integração exigida | Situação do inventário |
| --- | --- | --- | --- | --- |
| P0 | Servidor/API .NET | autorização por usuário e escopo para operações de catálogo, playback e solicitações | testes de controller/serviço; integração com banco isolado quando persistência for parte do contrato | ampla suíte existente; relacionar requisitos críticos a testes e executar integrações sem confundir testes ignorados com aprovação |
| P0 | Nebula .NET/Python | identidade canônica e deduplicação; não reenviar item concluído; preservar recuperação após falha/cancelamento | testes determinísticos de política/fila; Mongo isolado para persistência; fake para Telegram/Supabase | suítes existentes em ambas as stacks; registrar separadamente dependências reais e cenários que exigem credenciais/serviços |
| P0 | Cliente web | sessão/autorização e contratos de API; estados de erro/offline sem ações inválidas | Vitest com comportamento observável; Playwright no stage isolado para fluxos críticos | suíte unitária e Playwright existem; manter typecheck, lint alterado, styles, build e verificação de artefatos |
| P0 | Android APK | sessão, atualização assinada, rede LAN/HTTPS, playback/download e proteção de credenciais | JVM primeiro; instrumentação PHONE/TABLET/TV para APIs Android, lifecycle e UX afetados | plano detalhado em `MulletaFlix-android/TDD-PLAN.md`; legado sem histórico RED-GREEN auditado |
| P1 | iOS | autenticação, persistência, contratos de API e ciclo de reprodução | XCTest/Swift Testing no macOS; `xcodebuild test` em simulador para UI e integração | há testes Core; build de simulador por si só não prova testes de UI |
| P1 | Portal | sincronização de versão, links, notas, sitemap e renderização de homologação | testes `node:test` para dados/DOM e validação sitemap; teste HTTP/deploy apenas após publicação autorizada | inventário atual encontrou 1 arquivo e 4 testes de homologação; ainda faltam contratos para `release-sync.js`, downloads, canonical/sitemap e rotas públicas |
| P1 | Scripts/build/release | rejeitar versão ou artefato obsoleto, assinatura errada, entrada inválida e cópia parcial | Pester/Node/Bash em diretórios temporários e ferramentas falsas; inspecionar pacote/hash/assinatura | há testes distribuídos; inventariar scripts sem regressão focal e manter publicação real fora da suíte |
| P2 | Compatibilidade transversal | mesmos contratos de mídia, paginação, erros e estado entre servidor, web, Android e iOS | fixtures contratuais compartilhadas/versionadas; testes de consumidor por plataforma | implementar por endpoint/fluxo alterado, não criar suíte monolítica dependente de serviços reais |

### Regras de execução do backlog

1. Trabalhar uma regressão observável por ciclo, em um componente proprietário; dividir mudanças cross-platform em contratos e consumidores separados.
2. Não abrir uma tarefa para adicionar testes a todo o legado de uma vez. Converter cada linha do backlog em itens menores por API, fluxo e perfil de dispositivo.
3. Para cada execução, registrar `INTENT`, `RED`, `GREEN`, `QUALITY GATE`, ambiente, contagens de aprovados/falhas/erros/ignorados e limitações.
4. Se ferramenta, SO, serviço ou credencial de teste estiver indisponível, marcar a integração como pendente/bloqueada; nunca substituir por build ou teste unitário e chamá-la de homologada.
5. Revisar este backlog após cada release, removendo apenas lacunas com evidência reproduzível e adicionando regressões novas descobertas.

O inventário das suítes é estático e não representa homologação. Nesta revisão, Android executou a suíte JVM completa (1.539 testes, 0 falhas/erros/ignorados), lint de `core:common`, `core:api` e app, `assembleDebug` e uma classe instrumentada PHONE API 35 (3/3); os quatro testes locais do portal passaram e o validador confirmou 7 URLs canônicas. As suítes .NET, Python, Web, iOS, Bash, Pester, Playwright e Cucumber não foram executadas nesta revisão. XCTest exige macOS/Xcode; os demais comandos continuam gates documentados, não resultados desta revisão.

## Matriz de design dos testes

| Tipo de mudança | Teste primário | Complementos |
| --- | --- | --- |
| Regra pura, parser, normalização ou política | Unitário determinístico na camada dona | Limites, entradas inválidas, Unicode, valores vazios e propriedade quando cabível |
| Estado, concorrência, retries ou cancelamento | Teste com relógio/scheduler controlado | Corridas, cancelamento, retry e resposta obsoleta |
| Persistência/migração | Repositório com banco/armazenamento isolado | Inicialização a partir de estado legado, rollback, corrupção e isolamento entre contas |
| HTTP/WebSocket | Servidor local/fake que verifica contrato real | Status, headers, auth fictícia, timeout, cancelamento, retry e payload inválido |
| UI/navegação/acessibilidade | Teste de componente/instrumentado | Teclado, TalkBack/VoiceOver, foco D-pad, escala de fonte, rolagem e estados vazios/erro |
| Integração de plataforma ou mídia | Instrumentação/simulador | Dispositivo físico para hardware, codecs, áudio, DRM ou comportamento de sistema não reproduzido |
| Script/installer/release | Processo isolado com diretório temporário e ferramentas falsas | Caminho com espaços, falha parcial, rollback, reexecução, versões e validação do artefato |
| Portal/deploy | Teste de dados/rotas antes de publicar | Verificação pública pós-deploy; apenas com autorização de publicação |

## Ordem de adoção para legado

1. Não alegue TDD histórico sem evidência RED-GREEN registrada. Marque testes existentes como legado com histórico não auditado.
2. Em toda alteração de uma área legada, escreva primeiro teste de regressão para o comportamento tocado. O restante da área pode continuar não auditado.
3. Priorize falhas de segurança/assinatura, autenticação/sessão, playback/download, persistência/migração e release; depois navegação/UI/acessibilidade, sincronização/integrações e utilitários.
4. Para cada módulo, mantenha uma lista de comportamentos críticos, casos cobertos, ambiente necessário e bloqueios. “Tem testes” não significa “homologado”.
5. Feche uma lacuna somente após evidência executável; cobertura total exige inventário de requisitos e relatório mensurável, não estimativa visual.

## Critério de aceite do projeto

Uma mudança está pronta quando RED foi observado pela razão correta, GREEN foi confirmado, gates pertinentes terminaram com código 0 e evidências são rastreáveis ao diff. Uma frente inteira só pode ser chamada de homologada quando seus requisitos e perfis de ambiente foram inventariados e cada um possui resultado atual. Deploy, release, upload e push são ações separadas e não são autorizadas por este documento.
