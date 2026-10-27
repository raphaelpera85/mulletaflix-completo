# Roteamento do botão de ajuda no painel

`HelpButton` está montado dentro do roteador de dados do painel. Para selecionar o link contextual, o componente deve ler `useLocation()` e comparar `pathname` com `HelpLinks` por `matchPath()`. Não montar outro `<Routes>` descendente: além de desnecessário para este lookup, isso produz o aviso de rota pai sem `*` e cria uma segunda árvore de rotas.

O teste em `MulletaFlix-web-master/src/apps/dashboard/components/toolbar/HelpButton.test.tsx` cobre o formulário XMLTV com query string e um caminho sem link. A consulta de match ignora `search`, igual ao comportamento de rota, preservando o link contextual ao abrir provedores Live TV.
