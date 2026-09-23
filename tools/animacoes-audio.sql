-- Sinal objetivo para as 8 pastas com metadados suspeitos:
-- idioma das faixas de áudio dos episódios (jpn sugere anime; por/spa/eng live-action dublado).
SELECT SUBSTRING(e.Path, 11, 48) AS Pasta, s.Language AS Idioma, COUNT(*) AS Faixas
FROM baseitems e
JOIN mediastreaminfos s ON s.ItemId = e.Id
WHERE e.Type LIKE '%Episode'
  AND s.StreamType = 0
  AND (
        e.Path LIKE '%Coisas para Fazer Antes de Virar Zumbi%'
     OR e.Path LIKE '%100 Dias para Morar%'
     OR e.Path LIKE '%100 Namoradas Que Te Amam%'
     OR e.Path LIKE '%190 - Intelig%'
     OR e.Path LIKE '%190 - O Assassino Ligou%'
     OR e.Path LIKE '%1995 - No Tempo dos Badboys%'
     OR e.Path LIKE '%900 Dias Sem Anabel%'
     OR e.Path LIKE '%911 - Quando a Morte Chama%'
  )
GROUP BY Pasta, Idioma
ORDER BY Pasta, Faixas DESC;
