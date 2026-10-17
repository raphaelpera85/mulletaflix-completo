# MulletaFlix Server 12.1.24

## Correção do Downloader STRM do Nebula

- Inclui o runtime atualizado do downloader, com reconhecimento antecipado do worker enquanto prepara o índice MongoDB.
- Processa a biblioteca por categoria e título, em ordem de prioridade, para evitar que uma varredura global longa atrase o primeiro item elegível.
- Expõe no painel as etapas de inicialização, preparação do índice e varredura; o marcador de worker ativo não representa confirmação de transferência.
- Mantém falha segura quando o índice MongoDB não pode ser atualizado, sem iniciar downloads com estado de duplicidade desconhecido.
- Corrige o pacote Linux para incluir o runtime Python Nebula (`strm_downloader.py`, módulos FTP e dependências) e cria um venv dedicado na instalação, gravando o caminho do interpretador para o servidor.
- Mantém o empacotador Linux alternativo executado via PowerShell em paridade com o builder Bash; validação do workflow de release agora exige o downloader e `requirements.txt` dentro do tarball.

## Validação

- `NebulaPython`: 89 testes aprovados.
- `NebulaDownloaderStartupTests` em Release: 11 testes aprovados.
- Esses testes validam código e startup controlado; não substituem a homologação de transferência real MongoDB/Telegram.
- Teste do empacotamento Linux: runtime obrigatório presente e downloader byte a byte igual à fonte.
- Teste do helper PowerShell de empacotamento: 1/1 passou; workflow exige ambos os arquivos no pacote Linux.
- Build Linux concluído; inspeção do tarball confirmou `Tools/NebulaPython/tools/strm_downloader.py` e `requirements.txt`. SHA-256 do downloader no pacote igual à fonte: `2A671AFCA11BF06B48AAAEAA0DA49032F0A7FAD96F4963F6FEFEBD08B0B9CDBA`.

## Homologação pendente

A instalação ativa observada ainda responde 12.1.21 e contém um `strm_downloader.py` anterior ao runtime desta versão. O pacote 12.1.24 foi construído localmente, mas ainda não foi publicado. A correção só estará confirmada em produção após publicar/instalar este pacote em janela operacional e observar um arquivo iniciar e concluir o download.
