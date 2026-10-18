# MulletaFlix Server 12.1.24

## Prontidão do Downloader STRM durante descoberta de bibliotecas

- Considera o Downloader pronto após o índice Mongo ser validado, antes da enumeração das raízes monitoradas. A descoberta de bibliotecas grandes/remotas não deve consumir o timeout de startup de 60 segundos nem encerrar um worker válido.
- Mantém `[STRM-SCAN-STARTED]` como progresso da varredura, separado do handshake de inicialização. Testes Release locais: C# 30/30 e Python 3.10 17/17.
- Estado de homologação publicado no portal (Git `7482a90`, Vercel Production `READY`; endpoint público HTTP 200). Isso não é release publicada nem confirmação de transferência em produção; runtime atual observado continua 12.1.21.
- Não homologado em transferência produtiva; a instalação observada ainda está na versão 12.1.21. Esta nota descreve candidato local, não release publicada.

## Correção de mount Nebula duplicado

- Reconhece uma unidade `N:` já acessível como montagem concluída antes de conferir o lock do helper.
- Diferencia helper de montagem concorrente de falhas reais ao adquirir o lock ou do rclone, evitando marcar uma unidade pronta como falha e repetir montagem indevidamente.
- Preserva processos de montagem de outras instâncias; não encerra listeners ou mounts externos.
- Testes locais: helper 3/3, NebulaPython 591/591 e testes .NET focados 26/26. A colisão FTP em `127.0.0.1:2121` observada no log continua pendente de janela controlada e download real.

## Correção do Downloader STRM do Nebula

- Inclui o runtime atualizado do downloader, com reconhecimento antecipado do worker enquanto prepara o índice MongoDB.
- Processa a biblioteca por categoria e título, em ordem de prioridade, para evitar que uma varredura global longa atrase o primeiro item elegível.
- Expõe no painel as etapas de inicialização, preparação do índice e varredura; o marcador de worker ativo não representa confirmação de transferência.
- Mantém falha segura quando o índice MongoDB não pode ser atualizado, sem iniciar downloads com estado de duplicidade desconhecido.
- Corrige o pacote Linux para incluir o runtime Python Nebula (`strm_downloader.py`, módulos FTP e dependências) e cria um venv dedicado na instalação, gravando o caminho do interpretador para o servidor.
- Substitui TgCrypto arquivado, que não publica wheel CPython 3.12 Linux, por `tgcrypto-pyrofork` com wheel compatível para Ubuntu 24.04; o módulo importado permanece `tgcrypto`.
- Mantém o empacotador Linux alternativo executado via PowerShell em paridade com o builder Bash; validação do workflow de release agora exige o downloader e `requirements.txt` dentro do tarball.
- Permite gerar candidatos Linux em diretório isolado sob `.build`, mantendo `dist` como padrão e recusando sobrescrita de artefatos existentes ou saída por caminho reanalisado.
- Valida a versão do stage contra os metadados correntes do produto, evitando que a suíte de packaging fique presa a um número de versão histórico.
- O workflow instala as dependências num venv limpo sem compilador e valida import de Pyrofork/TgCrypto antes de liberar o pacote Linux.

## Validação

- `NebulaPython`: 89 testes aprovados.
- `NebulaDownloaderStartupTests` em Release: 11 testes aprovados.
- Esses testes validam código e startup controlado; não substituem a homologação de transferência real MongoDB/Telegram.
- Teste do empacotamento Linux: runtime obrigatório presente e downloader byte a byte igual à fonte.
- Teste do helper PowerShell de empacotamento: 1/1 passou; workflow exige ambos os arquivos no pacote Linux.
- Resolução binária cruzada para CPython 3.12/manylinux: `tgcrypto-pyrofork` 1.2.8 seleciona wheel; o requisito `tgcrypto` 1.2.5 não tem wheel compatível e falha sob `--only-binary`.
- Revalidação em 17/10: NebulaPython 587/587, startup .NET 11/11, portal independente 57/57, espelho 43/43 e verificações de payload/sintaxe passaram. Instalação Ubuntu CPython 3.12 ainda aguarda CI.
- Suíte .NET sem integrações: 1.464 aprovados, 62 ignorados, 0 falhas; Web `build:check`, 407 testes, `build:production` e `verify:build` passaram (1.927 artefatos; 294,3 KB gzip inicial).
- Build Linux concluído; inspeção do tarball confirmou `Tools/NebulaPython/tools/strm_downloader.py` e `requirements.txt`. SHA-256 do downloader no pacote igual à fonte: `2A671AFCA11BF06B48AAAEAA0DA49032F0A7FAD96F4963F6FEFEBD08B0B9CDBA`.
- Revalidação dos candidatos isolados em 17/10: pacote Linux SHA-256 `142D3B9EF33FBABDD189A1D11665970F0B84FE877BFD543695793F2F3119464F`; instalador Windows SHA-256 `F100E72EC8761DD87B972975F521AC411F30F553A2AD51C748A5111E1E7C7B66`. O candidato Linux contém 2.544 entradas e o worker empacotado corresponde à fonte; a guarda confirmou que o pacote antigo em `dist` não foi sobrescrito. Packaging PowerShell passou; portal independente 61/61 e espelho 47/47.
- Commit da correção enviado ao `main` (`3716b04c`). A execução CI GitHub `39036436583` terminou em falha em cerca de 9 segundos; os jobs retornaram sem etapas executadas. CI/release automatizada permanecem bloqueadas, não contar esse resultado como teste.
- A anotação do run `39036753498` confirma bloqueio de todos os jobs por cobrança da conta. `gh auth status` não tem sessão; sem tag, release ou instalação em produção.

## Homologação pendente

A instalação ativa observada ainda responde 12.1.21 e contém um `strm_downloader.py` anterior ao runtime desta versão. O pacote 12.1.24 foi construído localmente, mas ainda não foi publicado. A correção só estará confirmada em produção após publicar/instalar este pacote em janela operacional e observar um arquivo iniciar e concluir o download.

### Correção adicional — prontidão Nebula e imagens

- A sonda do runtime Python verifica o PID reportado em `/health`, evitando aceitar um processo antigo na porta HTTP como o worker novo que falhou ao abrir o FTP.
- Falhas inesperadas/transitórias de providers e downloads de imagens são contabilizadas para permitir nova tentativa; respostas 403/404 de uma URL continuam ignoradas para dar oportunidade a uma imagem alternativa.
- Testes locais: NebulaDownloaderStartupTests 15/15; NebulaPython 588/588; HTTP stream security 7/7; falha de download de imagem 2/2. A suíte integral de Providers foi interrompida sem saída por mais de um minuto; resultado inconclusivo, não contado como aprovação.
- Sem homologação em produção: consulta read-only identificou `python.exe` PID 6080 ocupando 2121/2123 desde 16/10 e com 37 conexões TCP estabelecidas; foi preservado para evitar interromper sessões. Ainda é necessária janela de manutenção e smoke test.
- Log recebido em 17/10 também registra WinError 10048 na porta FTP 2121 após nova inicialização, seguido por tentativas repetidas de mount/rclone. Isso corrobora a colisão de listener em produção; não há comprovação de que a versão corrigida tenha sido instalada ou de download concluído.

### Correção adicional — serialização do restore Supabase com uploads

- O restore agora usa a mesma admissão exclusiva Mongo do backup Supabase: impede novos uploads e aguarda leases ativos antes de substituir documentos de estado, evitando corrida com workers C#/Python.
- Teste de integração com MongoDB descartável mantém lease de upload ativa e verifica que o restore aguarda antes de chamar Supabase; restore + concorrência passaram 20/20.
- Limite: o teste não valida Supabase remoto nem instalação em produção. Só processos com Mongo/database compartilhado e suporte ao gate ficam coordenados; deploy e homologação continuam pendentes.

### Correção adicional — inicialização do Downloader após falha do Envio

- Quando o componente Envio falha ao iniciar (incluindo colisão da porta FTP 2121), o serviço continua tentando iniciar o Downloader STRM independente, em vez de encerrar toda a sequência de inicialização.
- O retry automático de montagem de `N:` após o startup do Downloader agora só é agendado quando o Envio/FTP está ativo, evitando iniciar um worker de streaming concorrente enquanto a porta FTP permanece indisponível. A montagem explícita continua separada.
- Validação local em Release: suíte completa de `Jellyfin.Server.Implementations.Tests` com 1.473 aprovados, 63 ignorados e 0 falhas; testes focados de startup/mount Nebula 35/35. Portal independente 70/70 e espelho 56/56.
- O andamento foi publicado no portal; deployment Vercel Ready e endpoint público de homologação respondeu HTTP 200. Não houve instalação em produção nem smoke test de transferência real; o processo que ocupa 2121 e a causa da colisão ainda precisam ser identificados em janela controlada.

### Correção adicional — privacidade do caminho da intro

- `GET /Branding/Configuration` continua público para as opções visuais, mas oculta o caminho físico `IntroPath` de anônimos e usuários comuns; administradores autenticados ainda recebem o valor para preservar a configuração existente.
- Testes focados do controller cobrem os dois casos. A integração HTTP pública existente foi ignorada nesta execução e não é contada como aprovada.

### Nebula — promoção segura para modo completo

- O Envio completo não reutiliza mais um worker já iniciado em Somente Streaming; inicializações concorrentes são serializadas e listeners nativos são liberados antes da transição.
- O sincronizador Supabase existente é preservado e o host de lease de cache Python é limpo mesmo sem processo associado.
- Testes focados de startup Nebula em Release: 39/39 aprovados. `pytest` não está instalado no Python local; testes do helper não foram executados.
- Homologação produtiva pendente: o log registra WinError 10048 na porta 2121, sem identificar o processo proprietário. Não houve intervenção em processo, instalação/restart ou transferência real.

### Correção adicional — promoção de Somente Streaming para Envio (não publicada)

- Ao alternar a engine Python de Somente Streaming para Envio completo, o processo anterior agora é encerrado antes de iniciar o novo worker, liberando o FTP 2121 e evitando a colisão WinError 10048 registrada no log.
- Testes focados Nebula C# Release: 49/49; Python 3.10 ciclo de vida/health/mount: 16/16. Portal independente 80/80 e espelho 65/65; `git diff --check` aprovado.
- Esta nota documenta alteração ainda não liberada. Produção permanece em 12.1.21; instalação, transferência real e os gates gerais de release continuam pendentes.

### Correção adicional — pausar retry de mount quando outro helper possui o lock (não publicada)

- Código de saída 3 do `mount_drive_n.py` agora pausa retries automáticos da unidade N:, evitando relançar um helper a cada três segundos enquanto outra instância mantém o lock global.
- Testes recompilados Release `NebulaMountRuntimeHealthTests` 7/7; testes `NebulaMount*` 18/18; `git diff --check` aprovado.
- Correção local ainda não publicada nem homologada em produção; T2.1 e os gates globais permanecem abertos.

### Gate de release com Mongo descartável (não publicada)

- O workflow de release provisiona MongoDB 7 isolado em `127.0.0.1:27099` e executa os testes `RequiresMongo` antes do empacotamento Linux e da publicação Windows.
- Validado localmente: parsing YAML, encadeamento dos jobs, suíte NebulaPython 592/592, frontend 418/418 e typecheck. Os testes Mongo foram ignorados na execução local por falta do serviço isolado; execução do gate no GitHub Actions segue pendente.
- Esta é uma melhoria de CI ainda não publicada. Não indica que o fluxo de download foi testado em produção; a release 12.1.24 continua pendente dos gates e do smoke test controlado descritos acima.

### Correção adicional — primeira limpeza Nebula sem atraso de 15 minutos (não publicada)

- A tarefa de cleanup contínuo agora executa a primeira varredura imediatamente em background após o Mongo estar conectado/restaurado; os ciclos subsequentes continuam usando intervalo de 15 minutos, sem sobreposição.
- O refresh periódico de mídias concluídas e o cleanup compartilham um agendador testado; validação focada Release: 17/17 no loop/refresh e 33/33 incluindo checkpoint/lifecycle.
- Teste feito apenas no código. O processo instalado não foi reiniciado nem atualizado; tempo real de limpeza e homologação produtiva continuam pendentes. Não publicar release intermediária enquanto os gates globais estiverem abertos.
