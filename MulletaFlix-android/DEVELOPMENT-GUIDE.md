# Guia de desenvolvimento Android

Guia de entrada para quem mantém o aplicativo MulletaFlix. Este diretório é o
cliente Android; confirme código, testes e documentação atuais antes de confiar
em resumos ou no grafo derivado.

## Limite do produto

- O APK é cliente/player do servidor MulletaFlix remoto: login, catálogo,
  metadados e streams são fornecidos pelo servidor escolhido pelo usuário.
- Descoberta automática na LAN é responsabilidade do cliente Android, com
  fallback para `https://mulletaflix.duckdns.org`.
- Mudanças nesta pasta podem alterar somente o aplicativo Android e seus testes.
  Servidor, portal, iOS e seus releases não fazem parte deste escopo.
- Release de produção não é necessária para validar alterações locais. Não
  incrementar versão, assinar ou publicar sem pedido explícito e sem seguir os
  gates aplicáveis do repositório.

## Ordem de leitura

1. `../AGENTS.md` — regras do repositório e limites globais.
2. `AGENTS.md` — TDD, quality gate Android e uso obrigatório do Graphify.
3. `README.md` — módulos, stack e entrada do cliente.
4. `HOMOLOGACAO-APK.md` — critérios de aceite, áreas validadas localmente,
   em validação e pendentes; confira data, evidência e gates em aberto.
5. `TDD-PLAN.md` — regressões, evidências RED/GREEN e lacunas por ordem recente.
6. `TODO-APP.md` — fila canônica de bugs, melhorias e critérios ainda abertos.
7. `TESTING.md` — comandos, dispositivos, perfis e evidências de teste.
8. Para um defeito, siga a tela/estado para o módulo dono, teste relacionado e
   registro do Graphify; confirme cada relação no código-fonte.

Todos os caminhos dos documentos acima são relativos à raiz Android. O segundo
cérebro é índice complementar e não substitui esta fonte. Para retomar e curar
o trabalho, use:

- `01 Projetos/MulletaFlix.md` — contexto verificado e mapa das fontes;
- `01 Projetos/MulletaFlix — Centro de Controle.md` — navegação do APK;
- `01 Projetos/MulletaFlix — Grafo de arquitetura.md` — proveniência e limites
  dos exports;
- `Bases/Grafos.base` — catálogo de grafos de projeto com estado de frescor;
- `00 Painéis/Centro de Controle.md` — entrada global para projetos, tarefas e
  curadoria.

Use o vault para encontrar fontes, não para replicar o backlog. Não copie
segredos, credenciais ou conteúdo privado para as notas.

## Mapa dos módulos

| Área | Dono primário | Comece por |
| --- | --- | --- |
| Inicialização, navegação e composição | `app/` | `app/src/` e testes de navegação |
| Contratos HTTP e sessão comum | `core/api/`, `core/common/` | interceptors, repositórios e testes do consumidor |
| Modelos e regras de negócio | `domain/` | entidades, casos de uso e contratos |
| Implementações de dados/cache | `data/` | repositórios, Room/DataStore e testes |
| Tema e componentes Compose | `design-system/` | tokens e componentes existentes |
| Funcionalidades | `feature/<área>/` | ViewModel/tela e testes do mesmo módulo |

Módulos de feature presentes: `auth`, `downloads`, `home`, `item-detail`,
`library`, `live-tv`, `player`, `search`, `settings`, `sync-play` e `user`.
Confirme os diretórios atuais antes de abrir uma nova área; não crie um módulo
duplicado por conveniência.

## Planejar, executar e curar tarefas

1. Localize a tarefa em `TODO-APP.md`; identifique comportamento observável,
   prioridade, dependências e critério de aceite. Não transforme um relato em
   fato confirmado sem reproduzi-lo ou encontrar evidência no código.
2. Consulte o grafo com `graphify query`/`graphify explain` para descobrir
   caminhos requisito → código → teste; confirme no repositório.
3. Para mudança de comportamento, escreva primeiro o teste, rode-o em RED pela
   razão esperada, aplique a menor correção e prove GREEN. Não alegue RED
   retroativo para cobertura adicionada após uma correção antiga.
4. Rode testes focais e depois os gates deste módulo. Para UI, ciclo de vida ou
   APIs de dispositivo, use instrumentação no perfil PHONE/TABLET/TV pertinente
   e siga `tools/with-emulator.ps1` em `TESTING.md`.
5. Atualize no mesmo trabalho o teste/documentação fonte, `TDD-PLAN.md`,
   `TESTING.md` ou `TODO-APP.md` que mudou de estado; rode Graphify e confira os
   nós afetados.
6. Registre fato, comando, resultado, limitação e próximo passo na nota do
   projeto e em uma sessão durável do vault. O dashboard reúne links, não é uma
   segunda lista de tarefas.

Estados sugeridos para curadoria: `pendente`, `em andamento`, `bloqueado` e
`concluído`. Só use concluído com evidência. A prioridade segue P0 crítica, P1
alta, P2 normal, P3 baixa; prazo só quando realmente acordado. Mantenha no máximo
três próximas ações visíveis na ficha e o restante no backlog detalhado.

## Verificação Android

Execute na raiz deste módulo, adaptando o lint ao módulo dono da alteração:

```powershell
.\gradlew.bat :feature:item-detail:testDebugUnitTest --no-daemon --console=plain
.\gradlew.bat testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain
```

O segundo comando corresponde ao gate abrangente registrado para a área de
leitura; não o descreva como teste instrumentado executado. Para testar em
dispositivo, veja `TESTING.md`. O AVD `MulletaflixApi35` está reservado ao
usuário: não o inicie, encerre, limpe ou reutilize sem liberação explícita.

## Métodos, habilidades e ferramentas

- TDD Red–Green–Refactor e auditoria do histórico: `AGENTS.md`, `TDD-PLAN.md`.
- Fable Method/Loop/Judge, Cavecrew, Caveman e Gauntlet: protocolos globais do
  vault; use conforme propósito e disponibilidade, sem simular delegação.
- Graphify: navegação relacional e atualização incremental do grafo; não é fonte
  de verdade nem substitui inspeção do código/documentação.
- Android/Compose UI verification: skills instaladas no ambiente e matriz de
  `TESTING.md`; escolha apenas as pertinentes à superfície alterada.
- Catálogo atualizado de skills: vault, nota `02 Operações/Skills/Catálogo de
  skills instaladas.md` e protocolo obrigatório associado.

## Registros principais

- Plano TDD e histórico RED/GREEN: `TDD-PLAN.md`.
- Comandos, dispositivos e cenários: `TESTING.md`.
- Backlog detalhado: `TODO-APP.md`.
- Critérios e estado da homologação formal: `HOMOLOGACAO-APK.md`. Não derive
  homologação integral da quantidade de testes nem da conclusão de tarefas do
  backlog; confirme data, evidências e gates ainda abertos.
- Mapa relacional da fonte: `graphify-out/graph.json` e `graphify-out/graph.html`.
- Ficha e mapa curado: vault `01 Projetos/MulletaFlix.md` e
  `01 Projetos/MulletaFlix — Grafo de arquitetura.md`.

> [!warning] Estado não presumido
> Uma contagem de testes passada não certifica a cobertura funcional integral.
> Compare a data/evidência da tarefa com a fonte atual e o relatório da execução
> antes de declarar uma área homologada.
