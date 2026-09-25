# Notas para a próxima release

## Interface web

- **Busca com navegação horizontal:** as faixas de mídias e pessoas encontradas na busca exibem controles de seta mesmo em navegadores desktop que também anunciam suporte a touch. A inicialização do scroller também aguarda os cards do React, evitando erro quando o callback roda antes da criação de `.scrollSlider`.
