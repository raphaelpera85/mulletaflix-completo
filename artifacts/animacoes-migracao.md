# Migração de animações: N:\Series -> N:\Animações

Executado em: 22/09/2026 22:03

## Resultado

| Item | Valor |
| --- | --- |
| Títulos movidos | 374 |
| Alterações de `parent` no MongoDB (`ftp.files`) | 441 (374 títulos + 67 descendentes com pai legado) |
| Nova raiz | `Animações` (irmã de `Series`, parent `/raphael`) |
| Backup | coleção `files_backup_20260922-215517` + `artifacts/backup-pre-animacoes/files.jsonl` |
| Rollback | `NebulaNovelaMigration rollback --map artifacts/animacoes-rollback.json` |
| Pastas deixadas em Series para revisão | 8 (metadados errados no app — ver abaixo) |

## Critério

Títulos da raiz `Series` cujo item na biblioteca tem o gênero `Animação` (TMDb Animation).
Inclui animes japoneses, animação ocidental, Disney, Cartoon Network e animação brasileira.

## Verificação

| Checagem | Resultado |
| --- | --- |
| Resolvedor do `NebulaFileSystem` | `Títulos migrados: 374/374`, `Descendentes com pai legado apontando para Series: 0`, `VERIFICAÇÃO: OK` |
| Raiz do drive (MongoDB) | `Filmes`, `Series`, `Porno`, `Novelas`, `Animações` |
| Filhos em Series | 2224 documentos / 2217 nomes distintos (2591 - 374) |
| Filhos em Animações | 374 documentos / 374 nomes distintos |
| Idempotência | reexecutar o `apply` com a mesma lista devolve `Títulos a mover: 0` |

## Pastas NÃO movidas (metadados errados na biblioteca)

Estas 8 pastas contêm episódios corretos, mas o item correspondente na biblioteca está
nomeado como `Batman: A Série Animada` (ano 1992) e herdou o gênero `Animação` do Batman.
Como o gênero não é confiável nesses casos, elas continuam em `N:\Series`.

| Pasta | Nome atual no app |
| --- | --- |
| `100 Coisas para Fazer Antes de Virar Zumbi` | Batman: A Série Animada |
| `100 Dias para Morar - Começando do Zero` | Batman: A Série Animada |
| `100 Namoradas Que Te Amam Muuuuuito` | Batman: A Série Animada |
| `190 - Inteligência Contra o Crime` | Batman: A Série Animada |
| `190 - O Assassino Ligou_` | Batman: A Série Animada |
| `1995 - No Tempo dos Badboys` | Batman: A Série Animada |
| `900 Dias Sem Anabel` | Batman: A Série Animada |
| `911 - Quando a Morte Chama` | Batman: A Série Animada |

Para corrigi-las: abra o item no painel e use "Atualizar metadados" (ou renomeie o item).
Depois disso elas podem ser movidas com o mesmo comando, bastando colocá-las na lista.

## Títulos movidos

| # | Pasta em `N:\Animações` | Título (metadados) | Idioma | Ano |
| --- | --- | --- | --- | --- |
| 1 | `4 Contra o Apocalipse` | 4 Contra o Apocalipse | en | 2019 |
| 2 | `44 Gatos` | 44 Gatos | it | 2018 |
| 3 | `86 Eighty-Six` | 86 EIGHTY-SIX | ja | 2021 |
| 4 | `91 Days` | 91 Days | ja | 2016 |
| 5 | `A Árvore Familiar dos Croods` | A Árvore Familiar dos Croods | en | 2021 |
| 6 | `A Casa Coruja` | A Casa Coruja | en | 2020 |
| 7 | `A Casa do Mickey Mouse` | A Casa do Mickey Mouse | en | 2006 |
| 8 | `A Casa Mágica da Gabby` | A Casa Mágica da Gabby | en | 2021 |
| 9 | `A Concierge Pokémon` | A Concierge Pokémon | ja | 2023 |
| 10 | `A Couple of Cuckoos` | A Couple of Cuckoos | ja | 2022 |
| 11 | `A Família Radical` | A Família Radical | en | 2001 |
| 12 | `A Família Radical - Maior e Melhor` | A Família Radical: Maior e Melhor | en | 2022 |
| 13 | `A Festa da Salsicha - Comilândia` | Festa da Salsicha: Comilândia | en | 2024 |
| 14 | `A Guarda do Leão` | A Guarda do Leão | en | 2016 |
| 15 | `A Lei de Milo Murphy` | A Lei de Milo Murphy | en | 2016 |
| 16 | `A Lenda de Vox Machina` | A Lenda de Vox Machina | en | 2022 |
| 17 | `A Luz do Futuro` | A Luz do Futuro | ja | 2026 |
| 18 | `A Mansão Foster para Amigos Imaginários` | A Mansão Foster para Amigos Imaginários | en | 2004 |
| 19 | `A Menina e o Porquinho` | A Menina e o Porquinho | en | 2025 |
| 20 | `A Nova Escola do Imperador` | A Nova Escola do Imperador | en | 2006 |
| 21 | `A Pequena Sereia` | A Pequena Sereia | en | 1992 |
| 22 | `A Saga Wingfeather` | A Saga Wingfeather | en | 2022 |
| 23 | `A Sombra do Batman` | A Sombra do Batman | en | 2013 |
| 24 | `A Tia é Top` | A Tia é Top | en | 2021 |
| 25 | `A Turma do Charlie Brown e Snoopy` | A Turma do Charlie Brown e Snoopy | en | 1983 |
| 26 | `A Turma do Pateta` | A Turma do Pateta | en | 1992 |
| 27 | `A Veterana Pitica da Firma` | A Veterana Pitica da Firma | ja | 2023 |
| 28 | `A Vida de Dug` | A Vida de Dug | en | 2021 |
| 29 | `A Vida Moderna de Rocko` | A Vida Moderna de Rocko | en | 1993 |
| 30 | `A Xerife Callie no Oeste` | A Xerife Callie no Oeste | en | 2013 |
| 31 | `Academia Unicórnio` | Academia Unicórnio | en | 2023 |
| 32 | `Academia Unicórnio - Segredos Revelados` | Academia Unicórnio: Segredos Revelados | en | 2026 |
| 33 | `Acampamento de Verão` | Acampamento de Verão | en | 2018 |
| 34 | `Acampamento Mágico` | Acampamento Mágico | en | 2026 |
| 35 | `Ace Attorney` | Ace Attorney | ja | 2016 |
| 36 | `Ace of the Diamond` | Ace of the Diamond | ja | 2013 |
| 37 | `Acorda, Carlo!` | Acorda, Carlo! | pt | 2023 |
| 38 | `Active Raid` | Active Raid: Kidou Kyoushuushitsu Dai Hachi Gakari | ja | 2016 |
| 39 | `Ada Batista, cientista` | Ada Batista, cientista | en | 2021 |
| 40 | `Adachi to Shimamura` | Adachi and Shimamura | ja | 2020 |
| 41 | `Adolepeixes` | Adolepeixes | en | 2010 |
| 42 | `Agent P, Under C` | Agente P. Disfarçado | en | 2026 |
| 43 | `Agente Elvis` | Agente Elvis | en | 2023 |
| 44 | `Agents of the Four Seasons - Dance of Spring` | Agents of the Four Seasons: Dance of Spring | ja | 2026 |
| 45 | `Aggretsuko` | Aggretsuko | ja | 2018 |
| 46 | `Ajin` | AJIN: Demi-Human | ja | 2016 |
| 47 | `Akame ga Kill` | Akame ga Kill! | ja | 2014 |
| 48 | `Akane-banashi` | Akane-banashi | ja | 2026 |
| 49 | `Akudama Drive` | Akudama Drive | ja | 2020 |
| 50 | `Akuyaku Reijou nanode Last Boss wo Kattemimashita` | I'm the Villainess, So I'm Taming the Final Boss | ja | 2022 |
| 51 | `Alien TV` | Alien TV | en | 2020 |
| 52 | `Alma-chan Wants to Be a Family!` | Alma-chan Wants to Be a Family! | ja | 2025 |
| 53 | `Alvinnn!!! E os Esquilos` | Alvinnn!!! E os Esquilos | en | 2015 |
| 54 | `Alya Sometimes Hides Her Feelings in Russian` | Alya Sometimes Hides Her Feelings in Russian | ja | 2024 |
| 55 | `American Dad!` | American Dad! | en | 2005 |
| 56 | `Among Us` | Among Us | en | 2026 |
| 57 | `Amphibia` | Amphibia | en | 2019 |
| 58 | `Ana` | Ana Pimentinha | en | 1997 |
| 59 | `Angel Beats!` | Angel Beats! | ja | 2010 |
| 60 | `Angels of Death` | Angels of Death | ja | 2018 |
| 61 | `Animals` | Animals. | en | 2016 |
| 62 | `Animaniacs` | Animaniacs | en | 1993 |
| 63 | `Anne Shirley` | Anne Shirley | ja | 2025 |
| 64 | `Another` | Another | ja | 2012 |
| 65 | `Aoashi` | Aoashi | ja | 2022 |
| 66 | `Apenas Um Show` | Apenas um Show | en | 2010 |
| 67 | `Apenas um Show - As Fitas Perdidas` | Apenas um Show: As Fitas Perdidas | en | 2026 |
| 68 | `Appare-Ranman!` | Appare-Ranman! | ja | 2020 |
| 69 | `Aprendendo Com Disney Junior` | Aprendendo Com Disney Junior | en | 2019 |
| 70 | `Aqua Teen - Esquadrão Força Total` | Aqua Teen: Esquadrão Força Total | en | 2000 |
| 71 | `Aquaman - Rei de Atlântida` | Aquaman: Rei de Atlântida | en | 2021 |
| 72 | `Arcane` | Arcane | en | 2021 |
| 73 | `Arifureta Shokugyou de Sekai Saikyou` | Arifureta: From Commonplace to World's Strongest | ja | 2019 |
| 74 | `Arnold` | Ei Arnold! | en | 1996 |
| 75 | `As Aventuras de Chuck e Amigos` | As aventuras de Chuck e amigos | en | 2010 |
| 76 | `As Aventuras de Horton` | As Aventuras de Horton | en | 2025 |
| 77 | `As Aventuras de Jackie Chan` | As Aventuras de Jackie Chan | en | 2000 |
| 78 | `As Aventuras do Gato de Botas` | As Aventuras do Gato de Botas | en | 2015 |
| 79 | `As Aventuras Escolhidas` | As Aventuras Escolhidas | en | 2025 |
| 80 | `As Enroladas Aventuras da Rapunzel` | As Enroladas Aventuras da Rapunzel | en | 2017 |
| 81 | `As Fabulosas Aventuras dos Freak Brothers` | As Fabulosas Aventuras dos Freak Brothers | en | 2021 |
| 82 | `As Irmãs Grimm` | As Irmãs Grimm | en | 2025 |
| 83 | `As Lendas - Mestres dos Mitos` | As Lendas: Mestres dos Mitos | es | 2019 |
| 84 | `As Meninas Superpoderosas (2016)` | As Meninas Superpoderosas | en | 1998 |
| 85 | `As palavras` | Barca de Palavras | ja | 2016 |
| 86 | `As Tartarugas Ninjas` | As Tartarugas Ninjas | en | 2012 |
| 87 | `As Terríveis Aventuras de Billy e Mandy` | As Terríveis Aventuras de Billy e Mandy | en | 2001 |
| 88 | `As Trapalhadas de Flapjack` | As Trapalhadas de Flapjack | en | 2008 |
| 89 | `Assassímio da Marvel` | Assassímio da Marvel | en | 2021 |
| 90 | `Assassination Classroom` | Assassination Classroom | ja | 2015 |
| 91 | `Assim Falava Kishibe Rohan` | Assim Falava Kishibe Rohan | ja | 2017 |
| 92 | `Astronauta` | Astronauta | pt | 2024 |
| 93 | `Atomic` | Betty Atômica | en | 2004 |
| 94 | `Attack on Titan` | Attack on Titan | ja | 2013 |
| 95 | `Avatar - A Lenda de Aang` | Avatar: A Lenda de Aang | en | 2005 |
| 96 | `Avatar - A Lenda De Korra` | A Lenda de Korra | en | 2012 |
| 97 | `Azur Lane` | AZUR LANE | ja | 2019 |
| 98 | `Baby Looney Tunes` | Baby Looney Tunes | en | 2002 |
| 99 | `Back Arrow` | Back Arrow | ja | 2021 |
| 100 | `Baki - O Campeão` | Baki - O Campeão | ja | 2018 |
| 101 | `Baki Hanma` | Baki Hanma | ja | 2021 |
| 102 | `Banished from the Heroes Party` | Scooped Up by an S-Rank Adventurer! | ja | 2025 |
| 103 | `Barbie - Dreamhouse Adventures` | Barbie: Dreamhouse Adventures | en | 2018 |
| 104 | `Barbie Dreamhouse Adventures - Go Team Roberts` | Barbie Dreamhouse Adventures: Go Team Roberts | en | 2019 |
| 105 | `Bat-Família` | Bat-Família | en | 2025 |
| 106 | `Batman - A Série Animada` | Batman: A Série Animada | en | 1992 |
| 107 | `Batman - Cruzado Encapuzado` | Batman: Cruzado Encapuzado | en | 2024 |
| 108 | `Batman - Os Bravos e Destemidos` | Batman: Os Bravos e Destemidos | en | 2008 |
| 109 | `Battle Game in 5 Seconds` | Battle Game in 5 Seconds | ja | 2021 |
| 110 | `Batwheels` | Batwheels | en | 2022 |
| 111 | `Baymax!` | Baymax! | en | 2022 |
| 112 | `Beast Tamer` | Beast Tamer | ja | 2022 |
| 113 | `Beastars o Lobo Bom` | BEASTARS - O Lobo Bom | ja | 2019 |
| 114 | `Bebefinn` | 베베핀 | ko | 2022 |
| 115 | `Ben 10` | Ben 10 | en | 2005 |
| 116 | `Ben 10 - Força Alienígena` | Ben 10: Força Alienígena | en | 2008 |
| 117 | `Ben 10 - Omniverse` | Ben 10: Omniverse | en | 2012 |
| 118 | `Ben 10 - Supremacia Alienígena` | Ben 10: Supremacia Alienígena | en | 2010 |
| 119 | `Ben 10 (2016)` | Ben 10 | en | 2005 |
| 120 | `Berserk` | Berserk | ja | 1997 |
| 121 | `Beyblade Metal Fusion (2009)` | Beyblade: Metal Fusion | ja | 2009 |
| 122 | `Big Blue - O Grande Oceano` | Big Blue: O Grande Oceano | en | 2021 |
| 123 | `Big Mouth` | Big Mouth | en | 2017 |
| 124 | `Billy Dilley` | Billy Dilley | en | 2017 |
| 125 | `Bingo e Rolly` | Bingo e Rolly | en | 2017 |
| 126 | `Birdgirl` | Birdgirl | en | 2021 |
| 127 | `Black Butler` | Black Butler | ja | 2008 |
| 128 | `Black Clover` | Black Clover | ja | 2017 |
| 129 | `Black Rock Shooter - Dawn Fall` | Black Rock Shooter: Amanhecer | ja | 2022 |
| 130 | `Black Summoner` | Black Summoner | ja | 2022 |
| 131 | `BLACK TORCH` | BLACK TORCH | ja | 2026 |
| 132 | `Blaze e os Monster Machines` | Blaze e os Monster Machines | en | 2014 |
| 133 | `Bleach` | Bleach | ja | 2004 |
| 134 | `Blood Blockade Battlefront` | Blood Blockade Battlefront | ja | 2015 |
| 135 | `Blue Box` | Blue Box | ja | 2024 |
| 136 | `Blue Exorcist` | Blue Exorcist | ja | 2011 |
| 137 | `Blue Lock` | BLUE LOCK | ja | 2022 |
| 138 | `Blue Reflection Ray` | Blue Reflection Ray | ja | 2021 |
| 139 | `Bluey` | Bluey | en | 2018 |
| 140 | `Bob Esponja Kamp Koral` | Kamp Koral: Bob Esponja, Primeiros Anos! | en | 2021 |
| 141 | `Bobs Burgers` | Bob's Burgers | en | 2011 |
| 142 | `Bocchi the Rock!` | BOCCHI THE ROCK! | ja | 2022 |
| 143 | `BoJack Horseman` | BoJack Horseman | en | 2014 |
| 144 | `Boku no Hero Academia` | My Hero Academia: Vigilantes | ja | 2025 |
| 145 | `Bonkers - De Astro a Tira` | Bonkers: De Astro a Tira | en | 1993 |
| 146 | `Bordertown` | Bordertown | en | 2016 |
| 147 | `Boruto - Naruto Next Generations` | Boruto: Naruto Next Generations | ja | 2017 |
| 148 | `Boss` | Helluva Boss | en | 2020 |
| 149 | `Bravos Guerreiros (2012)` | Bravos Guerreiros | en | 2012 |
| 150 | `Brickleberry` | Brickleberry | en | 2012 |
| 151 | `Brotherhood` | Fullmetal Alchemist: Brotherhood | ja | 2009 |
| 152 | `Bubble Guppies` | Bubble Guppies | en | 2011 |
| 153 | `Build Divide - Code Black` | Build Divide: Code Black | ja | 2021 |
| 154 | `Bungo Stray Dogs` | Bungo Stray Dogs | ja | 2016 |
| 155 | `By the Grace of the Gods` | By the Grace of the Gods | ja | 2020 |
| 156 | `Caça às Nozes com Tico e Teco` | Caça às Nozes com Tico e Teco | en | 2017 |
| 157 | `Caçadores de Demônios` | Caçadores de Demônios | zh | 2023 |
| 158 | `Caçadores de Trolls` | Caçadores de Trolls | en | 2016 |
| 159 | `Caligula` | Caligula | ja | 2018 |
| 160 | `Campfire Cooking in Another World with My Absurd Skill` | Campfire Cooking in Another World with My Absurd Skill | ja | 2023 |
| 161 | `Cãoventuras` | Cãoventuras | es | 2021 |
| 162 | `Capitão Planeta` | Capitão Planeta | en | 1990 |
| 163 | `Capitão Tsubasa` | Captain Tsubasa | ja | 2018 |
| 164 | `Captain Tsubasa` | Captain Tsubasa | ja | 2018 |
| 165 | `Cardcaptor Sakura` | Cardcaptor Sakura | ja | 1998 |
| 166 | `Carmen Sandiego` | Carmen Sandiego | en | 2019 |
| 167 | `Carol e o Fim do Mundo` | Carol e o Fim do Mundo | en | 2023 |
| 168 | `Carros na Estrada` | Carros na Estrada | en | 2022 |
| 169 | `Castlevania` | Castlevania | en | 2017 |
| 170 | `Castlevania - Noturno` | Castlevania: Noturno | en | 2023 |
| 171 | `Cat's Eye (2025)` | Cat's Eye | ja | 1983 |
| 172 | `CatDog` | CatDog | en | 1998 |
| 173 | `Caverna do Dragão` | Caverna do Dragão | en | 1983 |
| 174 | `Central Park` | Central Park | en | 2020 |
| 175 | `Chainsaw Man` | Chainsaw Man | ja | 2022 |
| 176 | `Chainsmoker Cat` | Chainsmoker Cat | ja | 2026 |
| 177 | `Charlotte` | Charlotte | ja | 2015 |
| 178 | `Chaves Em Desenho Animado` | Chaves Em Desenho Animado | es | 2006 |
| 179 | `Chip e Potato` | Chip e Potato | en | 2018 |
| 180 | `Chowder` | Chowder | en | 2007 |
| 181 | `Círculo de Fogo - The Black` | Círculo de Fogo: The Black | en | 2021 |
| 182 | `Clarêncio, O Otimista` | Clarêncio, O Otimista | en | 2014 |
| 183 | `Classroom of the Elite` | Classroom of the Elite | ja | 2017 |
| 184 | `Claymore` | Claymore | ja | 2007 |
| 185 | `Clevatess` | Clevatess | ja | 2025 |
| 186 | `Clube Winx - A Magia está de volta` | Clube Winx: A Magia está de volta | it | 2025 |
| 187 | `Code Geass` | Code Geass | ja | 2006 |
| 188 | `Combatants Will Be Dispatched` | Combatants Will Be Dispatched! | ja | 2021 |
| 189 | `Como Raeliana Foi Parar na Mansão do Duque` | Como Raeliana Foi Parar na Mansão do Duque | ja | 2023 |
| 190 | `Coragem, o Cão Covarde` | Coragem, o Cão Covarde | en | 1999 |
| 191 | `Cowboy Bebop` | Cowboy Bebop | ja | 1998 |
| 192 | `Cowboy Bebop (1998)` | Cowboy Bebop | ja | 1998 |
| 193 | `Crepúsculo dos Deuses` | Crepúsculo dos Deuses | en | 2024 |
| 194 | `Cuphead - A Série` | Cuphead: A Série | en | 2022 |
| 195 | `Cyberpunk - Mercenários` | Cyberpunk: Mercenários | ja | 2022 |
| 196 | `Death Note` | Death Note | ja | 2006 |
| 197 | `Deca-Dence` | DECA-DENCE | ja | 2020 |
| 198 | `Deep Insanity - The Lost Child` | Deep Insanity THE LOST CHILD | ja | 2021 |
| 199 | `Devilman Crybaby` | Devilman Crybaby | ja | 2018 |
| 200 | `Digimon` | Digimon Beatbreak | ja | 2025 |
| 201 | `Digimon Adventure (2020)` | Digimon Adventure: | ja | 2020 |
| 202 | `Dorothy e o Mágico de Oz` | Dorothy e o Mágico de Oz | en | 2017 |
| 203 | `DOTA - Dragon’s Blood` | DOTA: Dragon's Blood | en | 2021 |
| 204 | `Doutora Brinquedos` | Doutora Brinquedos | en | 2012 |
| 205 | `Dr. Stone` | Dr. Stone | ja | 2019 |
| 206 | `Dragões - Equipe de Resgate ‑ Heróis do Céu` | Dragões: Equipe de Resgate ‑ Heróis do Céu | en | 2021 |
| 207 | `Dragon Ball` | Dragon Ball Z | ja | 1989 |
| 208 | `Dragon Ball GT` | Dragon Ball GT | ja | 1996 |
| 209 | `Dragon Ball Super` | Dragon Ball Super | ja | 2015 |
| 210 | `Dragon Ball Z` | Dragon Ball Z | ja | 1989 |
| 211 | `Dragon Ball Z Kai` | Dragon Ball Z Kai | ja | 2009 |
| 212 | `Dragon Goes House-Hunting` | Dragon Goes House-Hunting | ja | 2021 |
| 213 | `Drama Total Kids` | Drama Total Kids | en | 2018 |
| 214 | `Duncanville` | Duncanville | en | 2020 |
| 215 | `Elena de Avalor` | Elena de Avalor | en | 2016 |
| 216 | `Elliott O Terráqueo` | Elliott, O Terráqueo | en | 2021 |
| 217 | `Ergo Proxy` | Ergo Proxy | ja | 2006 |
| 218 | `Esquadrão De Heróis` | Esquadrão de Heróis | en | 2009 |
| 219 | `Eu Elvis Riboldi` | Eu, Elvis Riboldi | es | 2020 |
| 220 | `Eu Sou Groot` | Eu Sou Groot | en | 2022 |
| 221 | `Evil or Live` | Evil or Live | ja | 2017 |
| 222 | `F is for Family` | F is for Family | en | 2015 |
| 223 | `Fairfax` | Fairfax | en | 2021 |
| 224 | `Family Guy` | Uma Família da Pesada | en | 1999 |
| 225 | `Fancy Nancy Clancy` | Fancy Nancy Clancy | en | 2018 |
| 226 | `Final Space` | Final Space | en | 2018 |
| 227 | `Food Wars Shokugeki no Soma` | Food Wars! Shokugeki no Soma | ja | 2015 |
| 228 | `Fruits Basket` | Fruits Basket | ja | 2019 |
| 229 | `Full Dive This Ultimate Next-Gen Full Dive RPG Is Even Shittier Than Real Life` | Full Dive: This Ultimate Next-Gen Full Dive RPG Is Even Shittier than Real Life! | ja | 2021 |
| 230 | `Fullmetal Alchemist - Brotherhood` | Fullmetal Alchemist: Brotherhood | ja | 2009 |
| 231 | `Gigantossauro` | Gigantossauro | en | 2019 |
| 232 | `Gleipnir` | Gleipnir | ja | 2020 |
| 233 | `GNOSIA` | GNOSIA | ja | 2025 |
| 234 | `Godzilla Singular Point` | Godzilla Ponto Singular | ja | 2021 |
| 235 | `Gokushufudou` | Gokushufudou: Tatsu Imortal | ja | 2021 |
| 236 | `Great Pretender` | Great Pretender | ja | 2020 |
| 237 | `Haha, You Clowns` | Haha, You Clowns | en | 2025 |
| 238 | `Harley Quinn` | Arlequina | en | 2019 |
| 239 | `Hataraku Saibou` | Cells at Work! | ja | 2018 |
| 240 | `Hatena Illusion` | Hatena☆Illusion | ja | 2020 |
| 241 | `Hell's Paradise` | Hell's Paradise | ja | 2023 |
| 242 | `Hércules` | Hércules | en | 1998 |
| 243 | `Higurashi When They Cry Gou` | Higurashi: When They Cry - GOU | ja | 2020 |
| 244 | `Homem-Aranha da Marvel` | Seu Amigão da Vizinhança: Homem-Aranha | en | 2025 |
| 245 | `Hora de Aventura` | Hora de Aventura | en | 2010 |
| 246 | `Hora de Aventura - Terras Distantes` | Hora de Aventura: Terras Distantes | en | 2020 |
| 247 | `Horimiya` | Horimiya | ja | 2021 |
| 248 | `Hulk e os Agentes de S.M.A.S.H` | Hulk e os Agentes de S.M.A.S.H. | en | 2013 |
| 249 | `ID - Invaded` | ID:Invaded | ja | 2020 |
| 250 | `Invencível` | INVENCÍVEL | en | 2021 |
| 251 | `Irina The Vampire Cosmonaut` | Irina: The Vampire Cosmonaut | ja | 2021 |
| 252 | `Irmão do Jorel` | Irmão do Jorel | pt | 2014 |
| 253 | `Jujutsu Kaisen` | Jujutsu Kaisen | ja | 2020 |
| 254 | `Kaguya-sama Love Is War` | Kaguya-sama: Love Is War | ja | 2019 |
| 255 | `Kakegurui` | Kakegurui | ja | 2017 |
| 256 | `Koala Man` | Koala Man | en | 2023 |
| 257 | `Kono Oto Tomare!` | Kono Oto Tomare!: Sounds of Life | ja | 2019 |
| 258 | `Kuroko no Basket` | Kuroko's Basketball | ja | 2012 |
| 259 | `Kyokou Suiri` | In/Spectre | ja | 2020 |
| 260 | `LEGO Aventuras na Cidade` | LEGO Aventuras na Cidade | en | 2019 |
| 261 | `LEGO Ninjago - Mestres do Spinjitzu` | Ninjago: Mestres do Spinjitzu | en | 2012 |
| 262 | `LEGO Star Wars - All-Stars` | LEGO Star Wars: All-Stars | en | 2018 |
| 263 | `LEGO Star Wars - As Aventuras dos Freemaker` | LEGO Star Wars: As Aventuras dos Freemaker | en | 2016 |
| 264 | `Liga da Justiça Ação` | Liga da Justiça Ação | en | 2016 |
| 265 | `Lilo e Stitch - A Série` | Lilo e Stitch: A Série | en | 2003 |
| 266 | `Looney Tunes Cartoons` | Looney Tunes Cartoons | en | 2020 |
| 267 | `Lost Song` | Lost Song | ja | 2018 |
| 268 | `Love Death e Robots` | Love, Death & Robots | en | 2019 |
| 269 | `Magos - Contos da Arcadia` | Magos: Contos da Arcadia | en | 2020 |
| 270 | `Mao Mao - Heróis de Coração Puro` | Mao Mao: Heróis de Coração Puro | en | 2019 |
| 271 | `Mars Red` | Mars Red | ja | 2021 |
| 272 | `Marvels M.O.D.O.K` | Marvel's M.O.D.O.K. | en | 2021 |
| 273 | `Mashiro no Oto` | Those Snow White Notes | ja | 2021 |
| 274 | `Megas XLR` | Megas XLR | en | 2004 |
| 275 | `Meikyuu Black Company` | The Dungeon of Black Company | ja | 2021 |
| 276 | `Meus Amigos Tigrão e Pooh` | Meus Amigos Tigrão e Pooh | en | 2007 |
| 277 | `Mike Judges Beavis and Butt-Head` | Beavis e Butt-Head | en | 2022 |
| 278 | `Miss Kuroitsu from the Monster Development Department` | Miss KUROITSU from the Monster Development Department | ja | 2022 |
| 279 | `Mob Psycho 100` | Mob Psycho 100 | ja | 2016 |
| 280 | `Molly McGee e o Fantasma` | Molly McGee e o Fantasma | en | 2021 |
| 281 | `Momonstros` | Momonstros | es | 2020 |
| 282 | `Monstros no Trabalho` | Monstros no Trabalho | en | 2021 |
| 283 | `Moriarty the Patriot` | Moriarty the Patriot | ja | 2020 |
| 284 | `Mr. Pickles` | Mr. Pickles | en | 2014 |
| 285 | `My Little Pony - A Amizade é Mágica` | My Little Pony:  A Amizade é Mágica | en | 2010 |
| 286 | `Napoleon Dynamite` | Napoleon Dynamite | en | 2012 |
| 287 | `Naruto` | Naruto | ja | 2002 |
| 288 | `New Looney Tunes` | New Looney Tunes | en | 2015 |
| 289 | `Night Sky` | Love Unseen Beneath the Clear Night Sky | ja | 2026 |
| 290 | `No Game No Life` | No Game, No Life | ja | 2014 |
| 291 | `Noragami` | Noragami | ja | 2014 |
| 292 | `O Incrível Mundo de Gumball` | O Incrível Mundo de Gumball | en | 2011 |
| 293 | `O Mundo dos Centauros` | O Mundo dos Centauros | en | 2021 |
| 294 | `O Mundo Maravilhoso de Mickey Mouse` | O Mundo Maravilhoso de Mickey Mouse | en | 2020 |
| 295 | `O Ônibus Mágico Decola Novamente` | O Ônibus Mágico Decola Novamente | en | 2017 |
| 296 | `O Príncipe Dragão` | O Príncipe Dragão | en | 2018 |
| 297 | `O Sangue de Zeus` | O Sangue de Zeus | en | 2020 |
| 298 | `O Surreal Mundo de Any Malu` | O (Sur)real Mundo de Any Malu | pt | 2015 |
| 299 | `O Vazio` | O Vazio | en | 2018 |
| 300 | `Odo` | Odo | en | 2021 |
| 301 | `One Punch Man` | One-Punch Man | ja | 2015 |
| 302 | `Operação Big Hero - A Série` | Operação Big Hero: A Série | en | 2017 |
| 303 | `Orient` | Orient | ja | 2022 |
| 304 | `Os Cavaleiros do Zodíaco` | Os Cavaleiros do Zodíaco | ja | 1986 |
| 305 | `Os Cavaleiros do Zodíaco (2019)` | Os Cavaleiros do Zodíaco | ja | 1986 |
| 306 | `Os Jovens Titãs em Ação` | Os Jovens Titãs em Ação! | en | 2013 |
| 307 | `Os Padrinhos Mágicos` | Os Padrinhos Mágicos | en | 2001 |
| 308 | `Os Simpsons` | Os Simpsons | en | 1989 |
| 309 | `Os Sustos Ocultos de Frankelda` | Os Sustos Ocultos de Frankelda | es | 2021 |
| 310 | `Os Vingadores - A Série` | Os Vingadores: A Série | en | 1999 |
| 311 | `Os Vizinhos Green` | Os Vizinhos Green | en | 2018 |
| 312 | `Our Last Crusade or the Rise of a New World` | Our Last Crusade or the Rise of a New World | ja | 2020 |
| 313 | `Overlord` | Overlord | ja | 2015 |
| 314 | `Panic` | Full Metal Panic | ja | 2002 |
| 315 | `Paradise Police` | Paradise Police | en | 2018 |
| 316 | `Peter Grill and the Philosopher's Time` | Peter Grill and the Philosopher's Time | ja | 2020 |
| 317 | `Phineas e Ferb` | Phineas e Ferb | en | 2007 |
| 318 | `Pinky e o Cérebro` | Pinky e o Cérebro | en | 1995 |
| 319 | `Players` | Power Players | en | 2019 |
| 320 | `Plunderer` | Plunderer | ja | 2020 |
| 321 | `Princesinha Sofia` | Princesinha Sofia | en | 2013 |
| 322 | `Prisma` | Pelo Prisma do Amor | ja | 2026 |
| 323 | `Pui Pui - Porquinhos com Rodinhas` | Pui Pui - Porquinhos com Rodinhas | ja | 2021 |
| 324 | `Radiant` | RADIANT | ja | 2018 |
| 325 | `Ranking of Kings` | Ranking of Kings | ja | 2021 |
| 326 | `Reboot` | ReBoot | en | 1994 |
| 327 | `Record of Ragnarok` | Record of Ragnarok | ja | 2021 |
| 328 | `Recursos Humanos` | Recursos Humanos | en | 2022 |
| 329 | `Resident Evil - No Escuro Absoluto` | Resident Evil: No Escuro Absoluto | ja | 2021 |
| 330 | `Rick e Morty` | Rick e Morty | en | 2013 |
| 331 | `RILAKKUMA (2026)` | RILAKKUMA | ja | 2026 |
| 332 | `Rumble Garanndoll` | Gyakuten Sekai no Denchi Shoujo | ja | 2021 |
| 333 | `Samurai Jack` | Samurai Jack | en | 2001 |
| 334 | `Santo` | Santo Bugito | en | 1995 |
| 335 | `Scarlet Nexus` | Scarlet Nexus | ja | 2021 |
| 336 | `Scooby Doo Cadê Você_` | Scooby-Doo, Cadê Você? | en | 1969 |
| 337 | `Seraph of the End Vampire Reign` | Seraph of the End | ja | 2015 |
| 338 | `Sereno - O Panda Zen` | Sereno: O Panda Zen | en | 2020 |
| 339 | `Shadows House` | SHADOWS HOUSE | ja | 2021 |
| 340 | `Shadowverse` | Shadowverse | ja | 2020 |
| 341 | `SHAMAN KING (2021)` | Shaman King | ja | 2021 |
| 342 | `SK8 the Infinity` | SK8 the Infinity | ja | 2021 |
| 343 | `Snoopy no Espaço` | Snoopy no Espaço: À Procura de Vida | en | 2019 |
| 344 | `Solar Opposites` | Solar Opposites | en | 2020 |
| 345 | `Sonic Prime` | Sonic Prime | en | 2022 |
| 346 | `Sonny Boy` | Sonny Boy | ja | 2021 |
| 347 | `South Park` | South Park | en | 1997 |
| 348 | `Spidey e Seus Amigos Espetaculares` | Spidey e Seus Amigos Espetaculares | en | 2021 |
| 349 | `Spirit - Cavalgando Livre` | Spirit - Cavalgando Livre | en | 2017 |
| 350 | `Star Wars - Forças do Destino` | Star Wars: Forças do Destino | en | 2017 |
| 351 | `Star Wars - Histórias dos Jedi` | Star Wars: Histórias dos Jedi | en | 2022 |
| 352 | `Star Wars - Rebels` | Star Wars: Rebels | en | 2014 |
| 353 | `Star Wars - The Bad Batch` | Star Wars: The Bad Batch | en | 2021 |
| 354 | `Star Wars - Visions` | Star Wars: Visions | en | 2021 |
| 355 | `Steins Gate` | Steins;Gate | ja | 2011 |
| 356 | `Steins Gate 0` | Steins;Gate 0 | ja | 2018 |
| 357 | `Steven Universo` | Steven Universo | en | 2013 |
| 358 | `Super Choque` | Super Choque | en | 2000 |
| 359 | `Super Cub` | Super Cub | ja | 2021 |
| 360 | `Super Dragon Ball Heroes` | Super Dragon Ball Heroes | ja | 2018 |
| 361 | `Super Drags` | Super Drags | pt | 2018 |
| 362 | `Super Monsters Monster Pets` | Super Monstros: Pet-Monstros | en | 2019 |
| 363 | `Sylvanian Families` | Sylvanian Families | en | 1987 |
| 364 | `Tainá e os Guardiões da Amazônia` | Tainá e os Guardiões da Amazônia | pt | 2018 |
| 365 | `Talking Tom and Friends` | Talking Tom and Friends | en | 2014 |
| 366 | `Teen Titans` | Os Jovens Titãs em Ação! | en | 2013 |
| 367 | `Teenage Euthanasia` | Teenage Euthanasia | en | 2021 |
| 368 | `Tenkuu Shinpan` | Tenku Shinpan - Sem Saída | ja | 2021 |
| 369 | `The Big Show Show` | The Big Lez Show | en | 2012 |
| 370 | `The Boys Apresenta - Diabólicos` | The Boys Apresenta: Diabólicos | en | 2022 |
| 371 | `The Day I Became a God` | The Day I Became a God | ja | 2020 |
| 372 | `The Deep` | Sob O Mar | en | 2015 |
| 373 | `The Great North` | The Great North | en | 2021 |
| 374 | `The Journey of Elaina` | Wandering Witch: The Journey of Elaina | ja | 2020 |

## Biblioteca no aplicativo

Definição criada em `C:\Users\Raphael\AppData\Local\MulletaFlix\root\default\Animações`
(`Animações.mblink` -> `N:\Animações`, `tvshows.collection`, `options.xml` com `N:\Animações`).
É registrada na próxima validação de biblioteca ("Varrer todas as bibliotecas").
