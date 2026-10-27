# Decisão: resolver o link de ajuda sem roteador descendente

**Data:** 2026-10-27  
**Estado:** aceita

## Contexto

`HelpButton` era renderizado no `AppLayout` do painel e criava uma segunda árvore com `<Routes>` apenas para escolher um link. O console reportou que essa árvore descendente estava sob uma rota pai sem `*`.

## Decisão

Usar `useLocation()` e `matchPath()` contra a lista existente `HelpLinks`. Manter exatamente a mesma tabela de caminhos e destinos. Retornar `null` quando a rota não tiver ajuda associada.

## Consequências

- Elimina a necessidade de `<Routes>` aninhado para a seleção contextual.
- Mantém parâmetros de query fora do match; por exemplo, `/dashboard/livetv/guide?type=xmltv` continua apontando para o guia XMLTV.
- Teste de componente cobre rota conhecida com query e rota sem link.
- `HelpLinks` continua sendo a fonte de caminhos e URLs.
