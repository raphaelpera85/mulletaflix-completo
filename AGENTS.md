# Memória do projeto

Antes de agir, leia `D:\Users\Raphael\Documents\Obsidian\UmbrellaPowerTech\AGENTS.md`, `00 Início.md`, a ficha `01 Projetos/MulletaFlix/MulletaFlix.md`, o `Mapa técnico e estrutura do projeto.md` na mesma pasta e `02 Operações/Protocolo de memória contínua.md` no vault.

Toda sessão que modificar qualquer coisa do projeto — código, testes, documentação, configuração, assets ou arquivos do vault — deve criar/atualizar um registro em `D:\Users\Raphael\Documents\Obsidian\UmbrellaPowerTech\01 Projetos\MulletaFlix\Sessões\`, mesmo quando a mudança for pequena. Inclua data, objetivo, arquivos afetados, verificações realmente executadas, resultado e pendências; não registre segredos, credenciais ou dados pessoais.

Ao concluir trabalho duradouro, atualize documentação-fonte e ficha do projeto com evidências e data real. Confira `01 Projetos/MulletaFlix/Mapa técnico e estrutura do projeto.md` em toda sessão e atualize-o se módulos, APIs, classes, dependências, componentes ou fluxo mudarem; registre na sessão quando a estrutura foi conferida e permaneceu igual. Registre decisões em `01 Projetos/MulletaFlix/Decisões/`.

Não copie segredos, credenciais, dados pessoais ou conteúdo de `.env` para o vault. Separe fatos de hipóteses; se não puder verificar algo, marque como pendente. Preserve mudanças preexistentes e siga as instruções específicas deste projeto.

## Premissa contínua de engenharia do servidor

Em cada rodada de trabalho no servidor, procure técnicas atuais que possam melhorar confiabilidade, segurança, desempenho, observabilidade, persistência, operação e testes em todo o escopo do servidor, incluindo Nebula e provedores de mídia. Priorize documentação oficial atual e evidência do código; registre oportunidade, compatibilidade, riscos e validação necessária. Não atualize frameworks, dependências ou runtime apenas por haver versão nova: proponha a adoção somente após benefício e compatibilidade demonstrarem-se em testes isolados. Pesquisa, build local e testes não equivalem a homologação de produção.
