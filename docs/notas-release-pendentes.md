# Notas para a próxima release

## Interface web

- **Busca com navegação horizontal:** as faixas de mídias e pessoas encontradas na busca exibem controles de seta mesmo em navegadores desktop que também anunciam suporte a touch. A inicialização do scroller também aguarda os cards do React, evitando erro quando o callback roda antes da criação de `.scrollSlider`.

## Solicitações e reportes

- **Solicitação de mídias:** clientes autenticados podem pedir inclusão de filmes, séries, animações, novelas, doramas e outros títulos pela tela inicial.
- **Reporte de reprodução:** cada título permite enviar a categoria e a descrição de um problema de execução.
- **Painel de gestão:** foram criadas páginas separadas para consultar solicitações de mídias e reportes de reprodução, com registro de usuário, título e data no servidor.
- Os envios usam endpoints autenticados do servidor e são persistidos no histórico de atividades para consulta administrativa.
