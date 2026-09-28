# Roadmap de evolução tecnológica — servidor e frontend web

**Data:** 28/09/2026
**Status:** planejamento; nenhuma tarefa abaixo foi iniciada por este documento.
**Escopo:** servidor MulletaFlix para Windows e Linux e frontend web distribuído junto ao servidor. O aplicativo Android/iOS não faz parte deste roadmap.

## Objetivo

Consolidar oportunidades de evolução em confiabilidade, desempenho, segurança, operação e experiência do usuário. Priorizar por evidência e risco, preservando a arquitetura existente e evitando introduzir infraestrutura ou dependências sem benefício medido.

## Estado atual observado

- O servidor já compila para `net10.0`; .NET 10 é LTS, com suporte anunciado até novembro de 2028.
- O cliente web usa React 18, TypeScript, Vite 7, Material UI, TanStack Query e Swiper.
- Há ferramentas existentes de testes: Vitest, Playwright e Selenium/Cucumber, além de `build:check`, ESLint e Stylelint.
- O servidor já integra MariaDB, MongoDB/Nebula, Telegram, FFmpeg e cache local de reprodução. Este plano propõe melhorar sua operação, não substituir esses componentes sem uma decisão baseada em métricas.

## Princípios e limites

1. Medir antes e depois; não aceitar ganhos presumidos.
2. Resiliência e modo degradado: indisponibilidade de IA, internet, GPU ou provedor externo não pode impedir o uso normal do servidor.
3. Dados e correções reversíveis: manter origem, histórico e opção de rollback para mudanças de catálogo.
4. Processar grandes bibliotecas incrementalmente, com limites de concorrência e cancelamento.
5. Manter Windows e Linux como plataformas de produção de primeira classe.
6. Não adotar um broker externo, cache distribuído, SSR ou reescrita do frontend sem demonstrar necessidade.
7. Qualquer recurso baseado em IA começa em modo de análise/sugestão; não altera títulos, imagens ou NFO em massa sem aprovação e trilha de auditoria.

## Backlog priorizado

### Fase 0 — Baseline, inventário e critérios de qualidade (P0)

- [ ] **T0.1 — Definir baseline de produção.** Registrar tempo de inicialização, memória, uso de disco, latências p50/p95 dos endpoints mais usados, duração das tarefas Nebula e volume de mídia processado.
- [ ] **T0.2 — Mapear fluxos e dependências.** Documentar reprodução Telegram/STRM/cache, download/upload Nebula, MariaDB, MongoDB, backups, scanners e telas web que consomem cada API.
- [ ] **T0.3 — Criar conjunto de cenários representativo.** Incluir biblioteca pequena/grande, mídia local e Telegram, interrupção de banco/rede, cache cheio, Windows e Linux.
- [ ] **T0.4 — Definir limites de regressão.** Fixar budgets iniciais para tempo de boot, tamanho do bundle web, latência de busca, espaço temporário e memória; calibrar com medições reais, não valores arbitrários.

**Aceite:** relatório reproduzível com ambiente, comandos, métricas iniciais e limitações; sem alteração de comportamento do produto.

### Fase 1 — Observabilidade e operação (P0)

- [ ] **T1.1 — Adicionar instrumentação OpenTelemetry.** Traces e métricas correlacionando requisição, consulta, varredura, transferência Nebula, chamadas Telegram e sessão de reprodução.
- [ ] **T1.2 — Criar indicadores de operação no painel.** Saúde/degradação de MongoDB e MariaDB, espaço do cache, fila mais antiga, itens em retry, throughput e falhas por etapa.
- [ ] **T1.3 — Separar logs operacionais de auditoria.** IDs de correlação, retenção configurável e remoção/redação de tokens, credenciais, URLs assinadas e dados pessoais.
- [ ] **T1.4 — Expor health checks úteis.** Diferenciar processo ativo de serviço pronto; reportar dependências essenciais e estado degradado sem expor segredos publicamente.
- [ ] **T1.5 — Definir alertas e diagnósticos.** Alertar fila sem progresso, backup vencido, disco próximo do limite, erro repetido de provedor e falha de restauração.

**Aceite:** um incidente de teste pode ser rastreado do endpoint/tarefa até a dependência causadora; health check não revela configuração sensível; métricas não usam títulos, caminhos ou IDs de usuário como labels de alta cardinalidade.

### Fase 2 — Nebula: fila, concorrência e recuperação (P0)

- [ ] **T2.1 — Especificar máquina de estados durável.** Definir estados, transições, lease/heartbeat, retomada após reinício e como distinguir retry, falha permanente e cancelamento.
- [ ] **T2.2 — Fortalecer idempotência e deduplicação.** Revalidar identidade canônica e estado Telegram antes de baixar, enviar ou reenfileirar; não duplicar arquivos concluídos.
- [ ] **T2.3 — Tornar prioridade observável e consistente.** Aplicar prioridade explícita de solicitações, ordem de categorias configurada e A–Z dentro da categoria; mostrar na interface posição, motivo e próximo item.
- [ ] **T2.4 — Implementar backpressure e fairness.** Limites independentes por operação/rede, proteção contra rajadas e evitar starvation de tarefas não prioritárias.
- [ ] **T2.5 — Adicionar recuperação operacional.** Retentativas com backoff/jitter, limite de tentativas, fila de falhas com reprocessamento administrativo e cancelamento seguro.
- [ ] **T2.6 — Revisar limpeza e startup.** Varredura/cleanup incremental, concorrência limitada, checkpoint e progresso reportado; inicialização não deve bloquear o host por uma limpeza completa.

**Aceite:** reiniciar o servidor durante download/upload retoma ou encerra o trabalho de modo consistente; cenário de retry não produz duplicação; prioridades efetivas coincidem com a ordem exibida; tarefas não ficam indefinidamente sem progresso.

### Fase 3 — Reprodução e cache temporário (P0)

- [ ] **T3.1 — Formalizar o contrato do cache de reprodução.** Cache em disco com limite configurável, chave canônica, política de expiração/evicção, espaço reservado e comportamento quando o volume está cheio.
- [ ] **T3.2 — Validar leitura em partes e prefetch.** Garantir que a mídia original começa a tocar enquanto o cache pré-carrega as partes necessárias, com limites de concorrência e cancelamento ao encerrar/trocar a sessão.
- [ ] **T3.3 — Preservar leases ativos.** Limpeza não remove conteúdo usado por leitores ou downloads em andamento; liberar lease mesmo em exceção, cancelamento e encerramento do servidor.
- [ ] **T3.4 — Prevenir duplicação de downloads concorrentes.** Uma única operação por parte/arquivo atende leitores simultâneos; outros aguardam o mesmo resultado.
- [ ] **T3.5 — Exibir diagnóstico de cache.** Bytes e arquivos em cache, hits/misses, latência Telegram, prefetch em andamento, leases, erros e limpeza segura.
- [ ] **T3.6 — Fazer testes de falha e recuperação.** Rede lenta/interrompida, parte ausente, servidor reiniciado, cliente cancelado, mudança de caminho e disco cheio.

**Aceite:** a reprodução direta do disco permanece inalterada; com Nebula, o cache não impede o primeiro frame, não remove partes ativas e demonstra redução mensurável de pausas em cenários equivalentes.

### Fase 4 — Backups e recuperação de dados (P0)

- [ ] **T4.1 — Inventariar dados recuperáveis.** MongoDB, MariaDB, usuários, configuração, índices, credenciais e dados de operação; classificar mídia volumosa separadamente.
- [ ] **T4.2 — Garantir consistência do backup.** Definir snapshot/backup seguro para cada banco, criptografia, retenção, rotação, verificação de integridade e política de remoção remota.
- [ ] **T4.3 — Criar teste automático de restauração isolada.** Restaurar em diretório/instância temporária, validar contagens e consultas críticas e registrar duração/resultado.
- [ ] **T4.4 — Expor status acionável no painel.** Última execução, próxima execução, destino, conteúdo incluído, tamanho, validação, erros e ação de teste/restauração protegida.
- [ ] **T4.5 — Testar recuperação completa.** Documentar RPO/RTO realistas e exercício de recuperação em ambiente Windows e Linux.

**Aceite:** backup com status “sucesso” só após integridade validada; restauração de teste passa sem tocar nos dados de produção; falhas e backups vencidos geram alerta.

### Fase 5 — Banco, consultas e cache de aplicação (P1)

- [ ] **T5.1 — Medir consultas caras e pool de conexões.** Baseline de consultas lentas, espera por conexão, lock, índice usado e custo de varredura.
- [ ] **T5.2 — Otimizar consultas por evidência.** Paginação, projeção mínima, evitar materialização precoce, índices validados com plano de execução e limites para consultas grandes.
- [ ] **T5.3 — Revisar consistência entre MongoDB/MariaDB.** Documentar fonte de verdade por entidade, sincronização, reconciliação e comportamento diante de falha parcial.
- [ ] **T5.4 — Avaliar HybridCache para metadados.** Aplicar somente a leituras repetidas e seguras; definir chave que inclui identidade/idioma/permissão, TTL e invalidação. Não cachear respostas personalizadas como públicas.
- [ ] **T5.5 — Avaliar Change Streams somente se cabível.** Verificar a topologia MongoDB primeiro: Change Streams exigem replica set ou cluster fragmentado. Se a instalação permanecer standalone, manter polling/indexação incremental ou planejar a mudança operacional separadamente.
- [ ] **T5.6 — Testar carga e degradação.** Pool esgotado, conexão reiniciada, alto volume de consultas, índice ausente e migração/upgrade.

**Aceite:** melhoria demonstrada em benchmark e plano de consulta, sem regressão de consistência; cache invalida dados alterados; estratégia MongoDB compatível com a topologia realmente suportada.

### Fase 6 — APIs, segurança e configuração (P1)

- [ ] **T6.1 — Revisar autenticação/autorização por endpoint.** Principalmente solicitações, reports, uploads, operação Nebula, cache, backups e diagnósticos.
- [ ] **T6.2 — Aplicar rate limits e limites concorrentes seletivos.** Diferenciar login, busca, ações caras e operações de administração; retornar `429`/`Retry-After` sem degradar reprodução normal.
- [ ] **T6.3 — Validar entrada e caminhos de arquivo.** Tamanho, tipo, canonicalização, traversal, symlinks, extensões e acesso por usuário.
- [ ] **T6.4 — Revisar segredos, logs e transporte.** Proteção em repouso, rotação, permissões de arquivo e redação de credenciais; revisar dependências vulneráveis.
- [ ] **T6.5 — Validar configurações seguras por padrão.** Rede local/remota, TLS, CORS, headers, contas administrativas e exposição de endpoints internos.

**Aceite:** testes de autorização impedem acesso cruzado entre usuários; abuso de endpoint não esgota worker/banco; logs e mensagens de erro não revelam segredos.

### Fase 7 — FFmpeg, transcodificação e suporte de hardware (P1)

- [ ] **T7.1 — Criar diagnóstico do FFmpeg/FFprobe.** Versão, codecs, filtros, acelerações compiladas, drivers, permissões e teste simples de execução.
- [ ] **T7.2 — Detectar capacidades por host.** Windows e Linux; D3D11VA/Media Foundation, Quick Sync e VAAPI conforme hardware/build disponível.
- [ ] **T7.3 — Implementar seleção com fallback.** Escolher modo suportado e voltar a software em falha, sem impedir Direct Play ou reprodução.
- [ ] **T7.4 — Limitar concorrência de transcodes.** Limite ajustável com fila e visibilidade no painel; teste de cancelamento e liberação de recursos.
- [ ] **T7.5 — Validar compatibilidade por codec/container.** Matriz real de mídia, legendas, busca/seek, áudio e reprodução remota.

**Aceite:** aceleração só aparece como ativa após teste real; fallback funciona; qualidade, sincronismo e seek preservados; CPU/GPU e número de transcodes visíveis.

### Fase 8 — Catálogo, metadados e curadoria opcional por IA (P2)

- [ ] **T8.1 — Criar relatório determinístico de inconsistências.** Comparar nome de pasta/arquivo, NFO, título original, ano, tipo de mídia, ID do provedor, poster/backdrop e categoria.
- [ ] **T8.2 — Validar fontes por tipo de catálogo.** Para livros, testar busca e identificadores do Open Library (ISBN, edição e obra); para vídeo, comparar provedores compatíveis com o tipo. Armazenar ID/origem e respeitar limites/termos; não usar API de livros para varredura em massa.
- [ ] **T8.3 — Preservar proveniência e campos travados.** Registrar origem, data e confiança por campo; NFO local e IDs explícitos não devem ser silenciosamente substituídos.
- [ ] **T8.4 — Implementar modo de auditoria sem escrita.** Gerar candidatos e evidências para revisão; filtros por categoria, confiança e erro.
- [ ] **T8.5 — Prototipar IA local opcional.** Avaliar Ollama/Qwen3-VL em amostra pequena; medir precisão, latência, RAM/VRAM, armazenamento e consumo. A falha/ausência do modelo não pode parar a biblioteca.
- [ ] **T8.6 — Restringir IA a comparar candidatos.** Usar fontes/candidatos identificáveis; saída estruturada, confiança calibrada e abstenção quando ambígua.
- [ ] **T8.7 — Aprovação, histórico e rollback.** Aprovar correções individualmente; guardar estado anterior e permitir desfazer metadados, imagens e NFO.
- [ ] **T8.8 — Revisar direitos e termos de provedores.** Credenciais, limites, atribuição, uso permitido e armazenamento de imagens/dados.

**Aceite:** conjunto de teste rotulado mede precisão/recall; nenhuma alteração automática em massa no piloto; toda correção tem evidência e rollback; uso do modelo é opcional e offline após download.

## Backlog do frontend web

### Fase W1 — Confiabilidade e fluxo de usuário (P0)

- [ ] **W1.1 — Padronizar estados de página.** Loading/skeleton, vazio, erro com retry, offline/degradado e sucesso em home, busca, detalhes, solicitações e telas do painel.
- [ ] **W1.2 — Cobrir solicitação de mídia de ponta a ponta.** Botão próximo a Favoritos quando autorizado; autocomplete com debounce/cancelamento; estados de indexação, resultados e inclusão existente; grids separados para fila pendente e títulos já incluídos; posição/prioridade e confirmação do envio.
- [ ] **W1.3 — Corrigir navegação de carrosséis e resultados.** Setas visíveis quando aplicáveis, estados disabled corretos, rolagem por teclado/controle remoto, foco e comportamento responsivo.
- [ ] **W1.4 — Tratar falhas de API sem tela vazia.** Erros de rede/servidor mostram contexto e ação possível; logs técnicos ficam no console/telemetria sem expor detalhes sensíveis ao usuário.
- [ ] **W1.5 — Consolidar componentes e tokens de UI.** Harmonizar controles React/MUI e componentes Jellyfin legados sem reescrita global; priorizar cabeçalho, botões, grids, diálogos, alertas e estados.

**Aceite:** Playwright confirma todos os estados e caminhos; não há página de solicitação/grid permanentemente vazia quando API responde com dados ou erro; ações podem ser concluídas com mouse, teclado e layout estreito.

### Fase W2 — Acessibilidade e navegação em dispositivos (P1)

- [ ] **W2.1 — Auditoria WCAG 2.2 AA.** Contraste, foco, semântica, labels, mensagens de status, zoom/reflow, orientação e alvos de toque.
- [ ] **W2.2 — Navegação por teclado e controle remoto.** Ordem de foco, setas de carrossel, escape de diálogos, retorno de foco e atalhos sem armadilhas.
- [ ] **W2.3 — Automatizar axe com Playwright.** Rodar nas páginas principais; complementar com avaliação manual por teclado/leitor de tela.
- [ ] **W2.4 — Melhorar formulários e erros.** Label visível, validação junto ao campo, sugestão para corrigir e anúncio acessível de sucesso/falha.

**Aceite:** zero violações críticas/altas automatizáveis nas páginas alvo; checklist manual teclado/controle remoto; nenhum componente interativo sem nome/foco acessível.

### Fase W3 — Desempenho percebido (P1)

- [ ] **W3.1 — Medir Web Vitals por rota.** LCP, INP, CLS e TTFB, com dados de laboratório e medição real opt-in/anônima se aprovada.
- [ ] **W3.2 — Definir budgets de bundle e recursos.** Inspecionar chunks e dependências grandes; carregar rotas administrativas, PDF/EPUB e ferramentas pesadas sob demanda.
- [ ] **W3.3 — Otimizar posters/backdrops.** Dimensões adequadas, lazy loading fora do viewport, prioridade para imagem principal, placeholders BlurHash e reserva de espaço.
- [ ] **W3.4 — Ajustar cache de dados TanStack Query.** `staleTime`, chaves, invalidação, cancelamento e retry conforme semântica; evitar refetches duplicados e respostas de busca fora de ordem.
- [ ] **W3.5 — Avaliar virtualização.** Só adotar em grids/listas após benchmark demonstrar custo; preservar navegação por teclado, acessibilidade e medição de rolagem.
- [ ] **W3.6 — Testar redes e dispositivos lentos.** Throttling, CPU lenta, telas pequenas, TV e navegação por controle remoto.

**Aceite:** metas por rota são documentadas; nenhum chunk inicial acima do orçamento aprovado; melhoria comprovada em dispositivos/rede de referência sem regressão de imagem ou navegação.

### Fase W4 — Testes visuais e prevenção de regressão (P1)

- [ ] **W4.1 — Completar cobertura Playwright dos fluxos críticos.** Busca, detalhes, solicitações, reprodução, perfil e painel de gestão.
- [ ] **W4.2 — Adicionar snapshots visuais estáveis.** Viewports celular, desktop e TV; baseline revisto por pessoa; ambiente de navegador fixado.
- [ ] **W4.3 — Validar interações e paginação.** Setas, scroll, foco, estados de carregamento e carregamento de mais resultados.
- [ ] **W4.4 — Integrar quality gate do frontend.** `npm run build:check`, `npm test`, ESLint/Stylelint, `npm run build:production`, verificação do artefato e Playwright relevante.
- [ ] **W4.5 — Definir política para flaky tests.** Diagnóstico com trace/screenshot; nenhuma instabilidade escondida por retries ilimitados.

**Aceite:** rotas críticas têm teste funcional e visual; regressão de layout/ausência de grids é detectada antes da release; pipeline registra artefatos de falha.

## Ordem e dependências

1. **T0 baseline** precede qualquer decisão sobre performance, limites ou troca de tecnologia.
2. **T1 observabilidade** habilita diagnóstico e verificação de T2–T8 e W3.
3. **T2 fila, T3 reprodução e T4 backup** são frentes de confiabilidade prioritárias; cada uma deve ter teste isolado e cenário de falha.
4. **W1 estados e fluxos** pode começar após contratos mínimos de API/estados serem acordados; não depende de nova tecnologia.
5. **W2 e W4** acompanham cada melhoria visual, não ficam apenas para o fim.
6. **T5–T7 e W3** seguem o baseline e precisam demonstrar ganho mensurável.
7. **T8 IA** é experimento por último, depois de proveniência, auditoria sem escrita e rollback.

## Quality gate para cada tarefa executada

### Servidor

- Testes .NET focados no componente alterado e regressão relacionada; ampliar a suíte conforme o alcance.
- Build e pacote de produção para Windows e Linux quando aplicável.
- Testes de integração/falha para bancos, Telegram, fila e reprodução conforme o fluxo modificado.
- Critérios Gauntlet do repositório: evidência de terminal e código de saída zero; qualquer falha deve ser corrigida e reexecutada.

### Frontend web

- `npm run build:check`
- `npm test`
- `npm run lint` e `npm run stylelint` nos arquivos aplicáveis
- `npm run build:production` e `npm run verify:build`
- `npm run test:playwright:user`, `npm run test:playwright:admin` ou `npm run test:playwright:full` conforme as rotas alteradas
- Verificação visual e manual responsiva/acessível para mudanças de interface

### Definition of Done e release

- [ ] Diff revisado; testes relevantes e Quality Bar aprovados em configuração de produção.
- [ ] Notas de release descrevem apenas mudanças realmente incluídas e validadas.
- [ ] Publicar release do servidor com assets Windows (ZIP + EXE) e Linux quando a versão/build for aplicável.
- [ ] Atualizar e publicar o portal com a mesma versão/notas e validar a versão pública.
- [ ] Consultar API de releases e confirmar assets/versão publicados.

Este documento por si só é planejamento e não cria uma release funcional.

## Indicadores de sucesso do roadmap

- Nenhuma fila sem progresso sem alerta; retomada após reinício validada.
- Zero duplicações em reprocessamento e deduplicação Nebula.
- Menos pausas de reprodução medidas em sessões equivalentes, sem aumentar indefinidamente o uso de disco.
- Backup validado por restauração periódica, não apenas por upload concluído.
- Erros de API e UI diagnosticáveis por correlação sem exposição de segredos.
- Tarefas críticas do usuário concluíveis em celular, desktop e TV; nenhum grid essencial renderizado em branco sem estado de erro.
- Metas de Web Vitals, bundle e acessibilidade medidas e acompanhadas por rota.
- Curadoria por IA permanece opcional, auditável e reversível; precisão demonstrada em conjunto rotulado antes de qualquer automação.

## Referências técnicas

Fontes oficiais consultadas em 28/09/2026. Disponibilidade, suporte e requisitos de hardware devem ser revalidados antes de qualquer implementação.

### Servidor

- [.NET releases and support](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support)
- [.NET caching e HybridCache](https://learn.microsoft.com/en-us/dotnet/core/extensions/caching)
- [OpenTelemetry para .NET](https://opentelemetry.io/docs/languages/dotnet/)
- [Rate limiting ASP.NET Core (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [.NET health checks](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/diagnostic-health-checks)
- [MongoDB Change Streams — disponibilidade e requisitos](https://www.mongodb.com/docs/manual/changestreams/)
- [Documentação FFmpeg](https://www.ffmpeg.org/ffmpeg.html)
- [Qwen3-VL no Ollama](https://ollama.com/library/qwen3-vl)
- [Ollama — saídas estruturadas](https://ollama.com/blog/structured-outputs)
- [Open Library — APIs de busca e catálogo](https://openlibrary.org/developers/api)

### Frontend web

- [Vite — recursos, importação dinâmica e divisão de código](https://vite.dev/guide/features)
- [TanStack Query — referência/cache](https://tanstack.com/query/latest/docs/framework/react/reference/classes/Query)
- [TanStack Virtual](https://tanstack.com/virtual/latest/docs/introduction)
- [React Compiler](https://react.dev/learn/react-compiler/introduction)
- [Web Vitals — medição](https://web.dev/articles/vitals-measurement-getting-started)
- [WCAG 2.2](https://www.w3.org/TR/WCAG22/)
- [Playwright — testes de acessibilidade](https://playwright.dev/docs/accessibility-testing)
- [Playwright — comparações visuais](https://playwright.dev/docs/test-snapshots)
