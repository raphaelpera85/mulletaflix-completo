# Notas para a próxima release

## Interface web

- **Busca com navegação horizontal:** as faixas de mídias e pessoas encontradas na busca exibem controles de seta mesmo em navegadores desktop que também anunciam suporte a touch. A inicialização do scroller também aguarda os cards do React, evitando erro quando o callback roda antes da criação de `.scrollSlider`.

## Solicitações e reportes

- **Solicitação de mídias:** clientes autenticados podem pedir inclusão de filmes, séries, animações, novelas, doramas e outros títulos pela tela inicial.
- **Reporte de reprodução:** cada título permite enviar a categoria e a descrição de um problema de execução.
- **Painel de gestão:** foram criadas páginas separadas para consultar solicitações de mídias e reportes de reprodução, com registro de usuário, título e data no servidor.
- Os envios usam endpoints autenticados do servidor e são persistidos no histórico de atividades para consulta administrativa.
- **Catálogo de sugestões STRM**: o autocomplete inclui agora arquivos `.strm` tanto de `MonitorPaths` quanto das localizações reais das bibliotecas Jellyfin; catálogo vazio renova em 30 segundos e alterações nas raízes invalidam o cache.
- **Reconhecimento de livros pelo Open Library**: a busca agora consulta ISBN/OLID diretamente, aceita URLs de edição, usa os endpoints atuais de edição e busca por título após remover prefixos de nomes de arquivo; corrige o caso Cityscape (OL8144537M / ISBN 9780786939398).
- **Opção de solicitação na Web**: a tela inicial e os bundles web de produção são recompilados junto do servidor para distribuir o atalho e o formulário atuais.
