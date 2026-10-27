# Auditoria de instaladores e atualizações por ZIP

Data: 2026-10-26  
Estado: extração limitada e transporte HTTPS verificados localmente; autenticidade do publicador permanece pendente.

## Callers conferidos

Busca completa de `ExtractToDirectory` e `ExtractToFile` em C# mostrou quatro callers de `BoundedZipArchiveExtractor`: instalação de plugin, bootstrap de plugin, tarefa automática de atualização e endpoint do painel. Nenhum caller restante usa extração ZIP direta por pathname.

O helper valida traversal, entradas duplicadas e symlink, limita tamanho compactado/expandido/quantidade de entradas, grava em staging e só promove depois da validação. Todos os quatro callers usam o mesmo helper. Os dois fluxos de atualização exigem SHA-256 da release e vinculam o estado preparado ao checksum. Plugin/bootstrap mantêm checksum MD5 por compatibilidade com o contrato do catálogo legado; isso detecta erro acidental, mas não é resistente a colisões nem autentica o publicador.

## Transporte de atualização do servidor

O manifesto configurável por `MulletaFlix_UPDATE_MANIFEST_URL`, sua URL final após redirects, a URL HTTPS do pacote e a URL final do download precisam permanecer em HTTPS sem credenciais embutidas. A resposta do manifesto usa `ResponseHeadersRead` e leitura limitada a 2 MiB de bytes reais nos fluxos automático e administrativo. Isso evita buffering sem limite antes da validação. O host pode ser customizado pelo administrador; HTTPS e SHA-256 não constituem assinatura criptográfica do publicador nem validam a integridade do endpoint/manifesto em caso de comprometimento da CA ou da origem.

## Evidência local

- `BoundedZipArchiveExtractorTests` + `ServerUpdateTaskTests`: 32/32 Release em Windows/.NET 10; testes cobrem limite exato, excesso, UTF-8 inválido e leitura suspensa além do timeout de inatividade de 45 segundos.
- `UpdateInfoControllerTests`: 9/9 Release em Windows/.NET 10.
- Formatação whitespace direcionada e contrato do portal foram verificados nesta sessão. A execução completa anterior de `dotnet format --verify-no-changes` terminou com exit 1 por seis avisos xUnit1051 nos testes; os pontos do extrator foram corrigidos e a verificação whitespace separada passou.
- Nenhuma CI remota, execução Linux, pacote real, runtime instalado ou produção foi validada.

## Pendências

- Migrar o formato de checksum dos plugins de MD5 para SHA-256 sem quebrar os catálogos instalados.
- Definir verificação de assinatura/autenticidade do publicador e do manifesto; digest obtido da mesma origem não prova identidade.
- Exercitar redirecionamento HTTPS→HTTP e resposta de manifesto acima de 2 MiB em handler de integração local; validar em Windows/Linux.
- Não conceder crédito à homologação formal nem publicar release a partir desta auditoria parcial.
