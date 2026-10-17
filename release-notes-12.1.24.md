# MulletaFlix Server 12.1.24

## Correção do Downloader STRM do Nebula

- Inclui o runtime atualizado do downloader, com reconhecimento antecipado do worker enquanto prepara o índice MongoDB.
- Processa a biblioteca por categoria e título, em ordem de prioridade, para evitar que uma varredura global longa atrase o primeiro item elegível.
- Expõe no painel as etapas de inicialização, preparação do índice e varredura; o marcador de worker ativo não representa confirmação de transferência.
- Mantém falha segura quando o índice MongoDB não pode ser atualizado, sem iniciar downloads com estado de duplicidade desconhecido.
- Corrige o pacote Linux para incluir o runtime Python Nebula (`strm_downloader.py`, módulos FTP e dependências) e cria um venv dedicado na instalação, gravando o caminho do interpretador para o servidor.
- Substitui TgCrypto arquivado, que não publica wheel CPython 3.12 Linux, por `tgcrypto-pyrofork` com wheel compatível para Ubuntu 24.04; o módulo importado permanece `tgcrypto`.
- Mantém o empacotador Linux alternativo executado via PowerShell em paridade com o builder Bash; validação do workflow de release agora exige o downloader e `requirements.txt` dentro do tarball.
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
- Commit da correção enviado ao `main` (`3716b04c`). A execução CI GitHub `39036436583` terminou em falha em cerca de 9 segundos; os jobs retornaram sem etapas executadas. CI/release automatizada permanecem bloqueadas, não contar esse resultado como teste.
- A anotação do run `39036753498` confirma bloqueio de todos os jobs por cobrança da conta. `gh auth status` não tem sessão; sem tag, release ou instalação em produção.

## Homologação pendente

A instalação ativa observada ainda responde 12.1.21 e contém um `strm_downloader.py` anterior ao runtime desta versão. O pacote 12.1.24 foi construído localmente, mas ainda não foi publicado. A correção só estará confirmada em produção após publicar/instalar este pacote em janela operacional e observar um arquivo iniciar e concluir o download.
