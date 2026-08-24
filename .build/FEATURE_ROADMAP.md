# MulletaFlix Feature Roadmap

## Objetivo
Aumentar o nível de profissionalismo, percepção de qualidade e valor de produto do MulletaFlix sem perder estabilidade nem compatibilidade.

## Prioridades sugeridas

### 1. Painel de saúde do servidor
**Valor:** alto

- status do banco, storage, tasks e plugins
- uso de CPU/memória/armazenamento
- alertas visuais para falhas de backup, migração, indexação e tarefas agendadas
- cards com estado "OK / Warning / Critical"

### 2. Auditoria e histórico de ações administrativas
**Valor:** alto

- trilha de quem alterou usuário, plugin, tarefa, backup ou licença
- histórico filtrável por data, usuário e entidade
- exportação para CSV/JSON

### 3. Busca unificada e mais profissional
**Valor:** alto

- busca global por filmes, séries, artistas, episódios, canais e itens de biblioteca
- sugestões com debounce e destaque de termos
- filtros por tipo, status, ano, idioma e disponibilidade

### 4. Backup e recuperação aprimorados
**Valor:** alto

- agendamento de backup
- restauração por ponto no tempo
- validação de integridade do backup
- histórico de execuções com resultado e duração

### 5. Gestão de plugins mais elegante
**Valor:** médio-alto

- catálogo com cards mais ricos
- status de compatibilidade e versão
- botão de atualização/instalação com feedback claro
- changelog resumido por plugin

### 6. Licenciamento e permissões mais claras
**Valor:** médio-alto

- página de licença por usuário com timeline
- permissões e papéis mais explícitos
- visão de impacto de licença expirada / sem licença

### 7. SyncPlay e colaboração
**Valor:** médio

- lista de grupos com estado em tempo real
- convite rápido
- controle de host e participantes
- feedback de latência e sincronização

### 8. UX premium nas telas principais
**Valor:** médio

- skeletons consistentes
- empty states com CTA útil
- microanimações leves
- ações principais sempre visíveis
- melhoria visual do dashboard e das telas de biblioteca

### 9. Observabilidade e troubleshooting
**Valor:** médio

- página de logs com filtros melhores
- detalhes de erro com contexto
- botão de copiar diagnóstico
- pacote de suporte para reportar bugs

### 10. Centro de atualizações
**Valor:** médio

- status da versão atual e disponível
- changelog resumido
- links para release notes
- aviso de atualização recomendada

## Sequência prática recomendada

1. Painel de saúde do servidor
2. Auditoria e histórico de ações
3. Busca unificada
4. Backup e recuperação aprimorados
5. Gestão de plugins
6. Relatório de playback por mídia e usuário

## Critério para próxima entrega

Uma funcionalidade entra na próxima iteração se ela:
- tiver contrato backend claro
- puder ser validada com build/teste local
- melhorar a percepção de produto imediatamente
- não exigir refatoração grande de arquitetura para começar
