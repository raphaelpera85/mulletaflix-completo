# Sistema de Agentes Especialistas — MulletaFlix Web

Este diretório contém o sistema de agentes especialistas para desenvolvimento contínuo do projeto.

## Arquitetura

```
supervisor (orquestrador)
 ├── backend
 ├── frontend
 ├── database
 ├── plugins
 ├── security
 ├── performance
 └── deploy/review
```

## Como chamar os agentes

Os agentes são **executados pelo Hermes** (via `delegate_task`), usando o **modelo ativo da sessão** — não há conexão com LM Studio.

### Pelo chat (forma principal)

Basta pedir, em linguagem natural:

> "chame o agente **plugins** para revisar o SyncPlay e propor o refactor"
> "chame o agente **frontend** para corrigir os null-checks do filterdialog"
> "chame o agente **security** para auditar XSS nos componentes"
> "chame o agente **supervisor** para coordenar a melhoria X"

O Hermes carrega o prompt do agente (`ai/prompts/<nome>.md`) e executa com o modelo ativo.

### Pelo terminal (utilitários)

```bash
npm run agents:list         # lista os agentes disponíveis
npm run agents:map-plugins  # mostra o mapa de plugins
npm run agents:check        # roda build:check + testes
node ai/agents.mjs prompt <agente>   # imprime o prompt de um agente
```

## Fluxo

1. Supervisor recebe a demanda e quebra em subtarefas.
2. Especialistas executam (via Hermes, modelo ativo), respeitando o limite de concorrência.
3. Supervisor reconcilia resultados, resolve conflitos e aprova deploy.
4. Toda entrega passa por `npm run build:check` + `npm test` antes de ser considerada pronta.

## Regras de execução

- Cada agente trabalha somente no seu escopo.
- Toda mudança deve ser mínima e verificável.
- `npm run build:check` e `npm test` são o gate de aceite.
- Contratos entre áreas são definidos pelo supervisor.
- Melhorias em plugins seguem o mapeamento em `plugins-map.md`.
