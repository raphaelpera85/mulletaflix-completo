-- Auditoria das animações: pastas sob a raiz Series cujo item tem gênero Animação.
-- A barra invertida é montada com CHAR(92) para não depender de escape de shell.
SELECT
    SUBSTRING(b.Path, 11) AS Folder,
    b.Name AS ItemName,
    b.OriginalLanguage AS Lang,
    b.ProductionYear AS Ano,
    b.Genres AS Generos
FROM baseitems b
JOIN itemvaluesmap m ON m.ItemId = b.Id
JOIN itemvalues v ON v.ItemValueId = m.ItemValueId
WHERE b.Type LIKE '%Series'
  AND v.Value = 'Animação'
  AND SUBSTRING(b.Path, 1, 10) = CONCAT('N:', CHAR(92), 'Series', CHAR(92))
ORDER BY Folder;
