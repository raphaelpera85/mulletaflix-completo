# Auditoria de leitura de entradas EPUB

Data: 2026-10-26 18:10 BRT  
Estado: limite de expansão implementado; homologação parcial.

## Problema

EPUB é um arquivo ZIP. `container.xml`, OPF, XHTML usado como fallback de capa e a imagem de capa eram lidos sem limite de bytes descompactados. Um EPUB criado de forma maliciosa podia fazer o processo alocar memória excessiva.

## Correção

`EpubUtils` agora copia cada entrada por blocos e aborta a leitura ao exceder o limite efetivo, sem confiar somente no tamanho declarado no diretório ZIP. Limites por entrada: 16 MiB para XML/XHTML e 32 MiB para imagem. Os quatro caminhos de leitura usam o helper comum síncrono ou assíncrono; os streams de resultado são descartados se a leitura falhar.

## Evidência

`EpubUtilsTests`: entrada no limite aceita; leitura síncrona e assíncrona acima do limite rejeitadas. Release/.NET 10 passou 3/3 em 2026-10-26 no Windows. A suíte completa `Jellyfin.Providers.Tests` passou 881, ignorou 13 integrações externas e falhou 0/894. Formatação direcionada passou. Isso não valida imagens EPUB reais, perfil de consumo sob carga, Linux ou runtime instalado.

## Pendências

- Validar limites com amostra representativa de EPUB real e monitorar falsos positivos em arquivos XML/capas excepcionalmente grandes.
- Validar Linux nativo e integrar esta alteração ao gate amplo de regressão do servidor.
- Nenhuma release, instalação ou produção foi executada.
