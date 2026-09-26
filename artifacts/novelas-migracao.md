# Migração de novelas: N:\Series -> N:\Novelas

Executado em: 22/09/2026 21:24

## Resultado

| Item | Valor |
| --- | --- |
| Títulos movidos | 176 |
| Alterações de `parent` no MongoDB (`ftp.files`) | 274 (176 títulos + 98 descendentes com pai legado) |
| Nova raiz | `Novelas` (irmã de `Series`, parent `/raphael`) |
| Backup | coleção `files_backup_20260922-211623` + `artifacts/backup-pre-novelas/files.jsonl` |
| Rollback | `tools/nebula-novela-migration` comando `rollback --map artifacts/novelas-rollback.json` |

## Critério

Títulos da raiz `Series` cujos metadados do TMDb trazem o gênero `Soap` e cujo idioma original é `pt` ou `es`.
As novelas brasileiras, mexicanas/latinas entraram; séries dos EUA, turcas e chinesas ficaram de fora.

## Títulos movidos

| # | Pasta em `N:\Novelas` | Título (metadados) | Idioma | Ano |
| --- | --- | --- | --- | --- |
| 1 | `A Caverna Encantada` | A Caverna Encantada | pt | 2024 |
| 2 | `A Cozinheira de Castamar` | A Cozinheira de Castamar | es | 2021 |
| 3 | `A Desalmada` | A Desalmada | es | 2021 |
| 4 | `A Dona do Pedaço` | A Dona do Pedaço | pt | 2019 |
| 5 | `A Escrava Isaura (2004)` | A Escrava Isaura | pt | 2004 |
| 6 | `A Favorita` | A Favorita | pt | 2008 |
| 7 | `A Força do Querer` | A Força do Querer | pt | 2017 |
| 8 | `A Gata Comeu` | A Gata Comeu | pt | 1985 |
| 9 | `A Indomada` | A Indomada | pt | 1997 |
| 10 | `A Infância de Romeu e Julieta` | A Infância de Romeu e Julieta | pt | 2023 |
| 11 | `A Lei do Amor` | A Lei do Amor | pt | 2016 |
| 12 | `A Lua Me Disse` | A Lua Me Disse | pt | 2005 |
| 13 | `A Mentira` | A Mentira | es | 1998 |
| 14 | `A Mulher Proibida` | A Mulher Proibida | es | 2026 |
| 15 | `A Nobreza do Amor` | A Nobreza do Amor | pt | 2026 |
| 16 | `A Nova Vida de Ana` | A Nova Vida de Ana | es | 2023 |
| 17 | `A Promessa` | A Promessa | es | 2023 |
| 18 | `A Próxima Vítima` | A Próxima Vítima | pt | 1995 |
| 19 | `A Rainha da Pérsia` | A Rainha da Pérsia | pt | 2024 |
| 20 | `A Regra do Jogo` | A Regra do Jogo | pt | 2015 |
| 21 | `A Sucessora` | A Sucessora | pt | 1978 |
| 22 | `A Terra Prometida` | A Terra Prometida | pt | 2016 |
| 23 | `A Usurpadora` | A Usurpadora | es | 1998 |
| 24 | `A Usurpadora (2019)` | A Usurpadora | es | 1998 |
| 25 | `A Viagem` | A Viagem | pt | 1975 |
| 26 | `A Vida da Gente` | A Vida da Gente | pt | 2011 |
| 27 | `A Vítima` | A Próxima Vítima | pt | 1995 |
| 28 | `Acorrentada` | Acorrentada | pt | 1983 |
| 29 | `Agora é Que São Elas` | Agora é Que São Elas | pt | 2003 |
| 30 | `Além da Ilusão` | Além da Ilusão | pt | 2022 |
| 31 | `Além da Usurpadora` | Além da Usurpadora | es | 1998 |
| 32 | `Além do Horizonte` | Além do Horizonte | pt | 2013 |
| 33 | `Além do Tempo` | Além do Tempo | pt | 2015 |
| 34 | `Alma` | Alma Rebelde | es | 1999 |
| 35 | `Alma Gêmea` | Alma Gêmea | pt | 2005 |
| 36 | `Alma Gêmea (2026)` | Alma Gêmea | pt | 2005 |
| 37 | `Alma Indomável` | Alma Indomável | es | 2009 |
| 38 | `Alto Astral` | Alto Astral | pt | 2014 |
| 39 | `Amar a Morte` | Amar a Morte | es | 2018 |
| 40 | `Amar Demais` | Amar Demais | pt | 2020 |
| 41 | `Amor à Vida` | Amor à Vida | pt | 2013 |
| 42 | `Amor com Amor Se Paga` | Amor com Amor Se Paga | pt | 1984 |
| 43 | `Amor de Mãe` | Amor de Mãe | pt | 2019 |
| 44 | `Amor e Ódio` | Amor e Ódio | pt | 2001 |
| 45 | `Amor e Revolução (2011)` | Amor e Revolução | pt | 2011 |
| 46 | `Amor em Ruínas` | Amor em Ruínas | pt | 2026 |
| 47 | `Amor Perfeito` | Amor Perfeito | pt | 2023 |
| 48 | `Amor sem Igual` | Amor sem Igual | pt | 2019 |
| 49 | `Andando nas Nuvens` | Andando nas Nuvens | pt | 1999 |
| 50 | `Anjo Mau (1976)` | Anjo Mau | pt | 1997 |
| 51 | `Anjo Mau (1997)` | Anjo Mau | pt | 1997 |
| 52 | `Ao Sul do Coração` | Ao Sul do Coração | es | 2024 |
| 53 | `Apocalipse` | Apocalipse | pt | 2017 |
| 54 | `Aquele Beijo` | Aquele Beijo | pt | 2011 |
| 55 | `Araguaia` | Araguaia | pt | 2010 |
| 56 | `As Aventuras de Poliana` | As Aventuras de Poliana | pt | 2018 |
| 57 | `As Filhas da Mãe` | As Filhas da Mãe | pt | 2001 |
| 58 | `As Filhas da Senhora Garcia` | As Filhas da Senhora Garcia | es | 2024 |
| 59 | `Até que se Prove o Contrário` | Até que o Dinheiro nos Separe | es | 2022 |
| 60 | `Avenida Brasil` | Avenida Brasil | pt | 2012 |
| 61 | `Babilônia` | Babilônia | pt | 2015 |
| 62 | `Baila Comigo` | Baila Comigo | pt | 1981 |
| 63 | `Bambolê` | Bambolê | pt | 1987 |
| 64 | `Barriga de Aluguel` | Barriga de Aluguel | pt | 1990 |
| 65 | `Bebê a Bordo` | Bebê a Bordo | pt | 1988 |
| 66 | `Bela, a Feia (2009)` | Bela, a Feia | pt | 2009 |
| 67 | `Belíssima` | Belíssima | pt | 2005 |
| 68 | `Betty, A Feia - A História Continua` | Betty, A Feia - A História Continua | es | 2024 |
| 69 | `BIA` | BIA | es | 2019 |
| 70 | `Bibi de A Força do Querer` | Bibi de A Força do Querer | pt | 2025 |
| 71 | `Boogie Oogie` | Boogie Oogie | pt | 2014 |
| 72 | `Brega e Chique` | Brega & Chique | pt | 1987 |
| 73 | `Cabocla` | Cabocla | pt | 2004 |
| 74 | `Café com Aroma de Mulher` | Café com Aroma de Mulher | es | 2021 |
| 75 | `Cair em Tentação` | Cair em Tentação | es | 2017 |
| 76 | `Cama de Gato` | Cama de Gato | pt | 2009 |
| 77 | `Caminho das Índias` | Caminho das Índias | pt | 2009 |
| 78 | `Caras e Bocas` | Caras & Bocas | pt | 2009 |
| 79 | `Celebridade` | Celebridade | pt | 2003 |
| 80 | `Cheias de Charme` | Cheias de Charme | pt | 2012 |
| 81 | `Chiquititas (1997)` | Chiquititas | es | 1995 |
| 82 | `Chocolate` | Chocolate com Pimenta | pt | 2003 |
| 83 | `Chocolate com Pimenta` | Chocolate com Pimenta | pt | 2003 |
| 84 | `Ciranda de Pedra` | Ciranda de Pedra | pt | 1981 |
| 85 | `Cobras e Lagartos` | Cobras & Lagartos | pt | 2006 |
| 86 | `Colisão` | Colisão | es | 2026 |
| 87 | `Coração Marcado` | Coração Marcado | es | 2022 |
| 88 | `Cristal (2006)` | Cristal | pt | 2006 |
| 89 | `Cúmplices de um Resgate` | Cúmplices de um Resgate | pt | 2015 |
| 90 | `Da Cor do Pecado` | Da Cor do Pecado | pt | 2004 |
| 91 | `Deus Salve o Rei` | Deus Salve o Rei | pt | 2018 |
| 92 | `Duas Caras` | Duas Caras | pt | 2007 |
| 93 | `Em Família` | Em Família | pt | 2014 |
| 94 | `Éramos Seis` | Éramos Seis | pt | 2019 |
| 95 | `Escrava Mãe` | Escrava Mãe | pt | 2016 |
| 96 | `Espelho da Vida` | Espelho da Vida | pt | 2018 |
| 97 | `Estrela-Guia` | Estrela-Guia | pt | 2001 |
| 98 | `Êta Mundo Bom!` | Êta Mundo Bom! | pt | 2016 |
| 99 | `Explode Coração` | Explode Coração | pt | 1995 |
| 100 | `Felicidade` | Felicidade | pt | 1991 |
| 101 | `Fera Radical` | Fera Radical | pt | 1988 |
| 102 | `Fina Estampa` | Fina Estampa | pt | 2011 |
| 103 | `Fuzuê` | Fuzuê | pt | 2023 |
| 104 | `Gabriela` | Gabriela | pt | 2012 |
| 105 | `Gênesis` | Gênesis | pt | 2021 |
| 106 | `Geração Brasil` | Geração Brasil | pt | 2014 |
| 107 | `Guerra dos Sexos` | Guerra dos Sexos | pt | 1983 |
| 108 | `Haja Coração` | Haja Coração | pt | 2016 |
| 109 | `Hilda Furacão` | Hilda Furacão | pt | 1998 |
| 110 | `I Love Paraisópolis` | I Love Paraisópolis | pt | 2015 |
| 111 | `Império` | Império | pt | 2014 |
| 112 | `Império de Mentiras` | Império de Mentiras | es | 2020 |
| 113 | `Insensato Coração` | Insensato Coração | pt | 2011 |
| 114 | `Jezabel` | Jezabel | pt | 2019 |
| 115 | `Kubanacan` | Kubanacan | pt | 2003 |
| 116 | `Laços de Família` | Laços de Família | pt | 2000 |
| 117 | `Liberdade` | Sonhos de Liberdade | es | 2024 |
| 118 | `Liberdade, Liberdade` | Liberdade, Liberdade | pt | 2016 |
| 119 | `Lua Cheia de Amor` | Lua Cheia de Amor | pt | 1990 |
| 120 | `Malhação - vidas brasileiras` | Malhação | pt | 1995 |
| 121 | `Malhação (2010)` | Malhação | pt | 1995 |
| 122 | `Malhação (2012)` | Malhação | pt | 1995 |
| 123 | `Malhação (2014)` | Malhação | pt | 1995 |
| 124 | `Malhação Viva a Diferença` | Malhação | pt | 1995 |
| 125 | `Mania de Você` | Mania de Você | pt | 2024 |
| 126 | `Maria do Bairro` | Maria do Bairro | es | 1995 |
| 127 | `Marimar` | Marimar | es | 1994 |
| 128 | `Meu Bem, Meu Mal` | Meu Bem, Meu Mal | pt | 1990 |
| 129 | `Morde e Assopra` | Morde & Assopra | pt | 2011 |
| 130 | `Mulheres Apaixonadas` | Mulheres Apaixonadas | pt | 2003 |
| 131 | `Mulheres de Areia` | Mulheres de Areia | pt | 1973 |
| 132 | `Novo Mundo` | Novo Mundo | pt | 2017 |
| 133 | `O Bem-Amado` | O Bem-Amado | pt | 1973 |
| 134 | `O Clone` | O Clone | pt | 2001 |
| 135 | `O Cravo e a Rosa` | O Cravo e a Rosa | pt | 2000 |
| 136 | `O Outro Lado do Paraíso` | O Outro Lado do Paraíso | pt | 2017 |
| 137 | `O Profeta` | O Profeta | pt | 2006 |
| 138 | `O Rebu` | O Rebu | pt | 2014 |
| 139 | `O Rei do Gado` | O Rei do Gado | pt | 1996 |
| 140 | `O Rico e Lázaro` | O Rico e Lázaro | pt | 2017 |
| 141 | `O Salvador da Pátria` | O Salvador da Pátria | pt | 1989 |
| 142 | `O Sétimo Guardião` | O Sétimo Guardião | pt | 2018 |
| 143 | `O11ZE` | O11ZE | es | 2017 |
| 144 | `Operação Pacífico` | Operação Pacífico | es | 2020 |
| 145 | `Orgulho e Paixão` | Orgulho e Paixão | pt | 2018 |
| 146 | `Os Dez Mandamentos` | Os Dez Mandamentos | pt | 2015 |
| 147 | `Os Inocentes` | Os Inocentes | pt | 1974 |
| 148 | `Os Ricos Também Choram (2022)` | Os Ricos Também Choram | es | 1979 |
| 149 | `Pacto de Sangue` | Pacto de Sangue | pt | 1989 |
| 150 | `Páginas da Vida` | Páginas da Vida | pt | 2006 |
| 151 | `Pantanal` | Pantanal | pt | 1990 |
| 152 | `Pátria` | Pátria Minha | pt | 1994 |
| 153 | `Pedra Sobre Pedra` | Pedra Sobre Pedra | pt | 1992 |
| 154 | `Pega Pega` | Pega Pega | pt | 2017 |
| 155 | `Por Amor` | Por Amor | pt | 1997 |
| 156 | `Porto dos Milagres` | Porto dos Milagres | pt | 2001 |
| 157 | `Quatro por Quatro` | Quatro por Quatro | pt | 1994 |
| 158 | `Que Rei Sou Eu_` | Que Rei Sou Eu? | pt | 1989 |
| 159 | `Renascer (1993)` | Renascer | pt | 2024 |
| 160 | `Renascer (2024)` | Renascer | pt | 2024 |
| 161 | `Rock Story` | Rock Story | pt | 2016 |
| 162 | `Roda de Fogo` | Roda de Fogo | pt | 1986 |
| 163 | `Roque Santeiro` | Roque Santeiro | pt | 1985 |
| 164 | `Rubí` | Rubí | es | 2020 |
| 165 | `Salve Jorge` | Salve Jorge | pt | 2012 |
| 166 | `Salve-se Quem Puder` | Salve Jorge | pt | 2012 |
| 167 | `Sangue Bom` | Sangue Bom | pt | 2013 |
| 168 | `Sassaricando` | Sassaricando | pt | 1987 |
| 169 | `Segundo Sol` | Segundo Sol | pt | 2018 |
| 170 | `Selva de Pedra` | Selva de Pedra | pt | 1972 |
| 171 | `Senhora do Destino` | Senhora do Destino | pt | 2004 |
| 172 | `Sete Vidas` | Sete Vidas | pt | 2015 |
| 173 | `Sinhá Moça` | Sinhá Moça | pt | 2006 |
| 174 | `Sol Nascente` | Sol Nascente | pt | 2016 |
| 175 | `Terra Nostra` | Terra Nostra | pt | 1999 |
| 176 | `Vale Tudo (2025)` | Vale Tudo | pt | 2025 |

## Verificação no drive N: (após montagem)

| Checagem | Resultado |
| --- | --- |
| Raiz do drive | `Filmes`, **`Novelas`**, `Porno`, `Series` |
| Pastas em `N:\Novelas` | 176 |
| Pastas em `N:\Series` | 2591 (2767 - 176) |
| Novelas restantes em `N:\Series` | 0 |
| Conteúdo preservado | ex.: `N:\Novelas\Vale Tudo (2025)\Season 01\Vale Tudo (2025) - S01E01.mkv` |

Verificação pelo resolvedor do `NebulaFileSystem` (mesmos filtros de `parent` que o servidor usa):
`Títulos migrados: 176/176`, `Descendentes com pai legado apontando para Series: 0`, `VERIFICAÇÃO: OK`.

## Biblioteca no aplicativo
Criada a definição da biblioteca `Novelas` em `C:\Users\Raphael\AppData\Local\MulletaFlix\root\default\Novelas`
(`Novelas.mblink` -> `N:\Novelas`, `tvshows.collection`, `options.xml` com `N:\Novelas`).
Ela é registrada pelo servidor na próxima inicialização, seguida de varredura da biblioteca.
