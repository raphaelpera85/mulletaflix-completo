# MulletaFlix

**Servidor de mídia pessoal, com interface web e aplicativos para assistir à sua coleção em diferentes dispositivos.** O projeto combina o servidor MulletaFlix, uma interface web personalizada e clientes Android e iOS. Você mantém o controle dos arquivos, da organização das bibliotecas e da configuração do servidor.

[Código-fonte](https://github.com/raphaelpera85/mulletaflix-completo) · [Releases](https://github.com/raphaelpera85/mulletaflix-completo/releases) · [Documentação operacional](docs/NEBULA-RUNBOOK.md)

> Este README descreve o estado e os componentes deste repositório. Alguns recursos dependem de configuração externa, de plugins ou de serviços de terceiros. O cliente iOS está em desenvolvimento; consulte as releases para saber quais plataformas têm pacotes prontos.

## O que o projeto oferece

### Servidor e bibliotecas

- Catálogo pessoal de filmes, séries, episódios, músicas, livros e outras mídias compatíveis, com varredura de pastas e atualização de metadados e imagens.
- Bibliotecas separadas por categoria — por exemplo, Filmes, Séries, Animações, Novelas e Doramas — configuradas pelo painel do servidor.
- Busca, filtros, detalhes das mídias, favoritos, histórico e progresso de reprodução.
- Reprodução direta ou conversão/transcodificação conforme o formato da mídia, o dispositivo e a configuração do FFmpeg.
- Seleção de faixas de áudio e legendas, quando disponíveis no arquivo e suportadas pelo cliente.
- Intro nativa antes da reprodução e pré-cache de mídia nas versões/configurações que oferecem esses recursos.
- Contas de usuário, permissões e acesso simultâneo por clientes compatíveis.
- TV ao vivo e guia de programação quando há fontes configuradas.
- Administração pelo painel web: bibliotecas, usuários, tarefas, reprodução, marca e estado do servidor.
- Banco de dados de aplicação MariaDB no servidor. O SQLite não é o banco de execução do MulletaFlix.

### Nebula (opcional)

Integração para operações de catálogo e arquivos STRM, transferência/streaming de arquivos e integração com MongoDB e Telegram. Pode também fazer backup do estado Nebula para Supabase. A unidade de rede (por exemplo, `N:`) é opcional para compatibilidade com ferramentas que precisam de uma letra de drive; o fluxo Nebula nativo não depende dela.

O backup automático do Nebula para Supabase vem habilitado por padrão e tem intervalo configurável, inicialmente de uma hora. Ele protege os dados Nebula suportados pela integração — não substitui uma estratégia de backup do servidor inteiro, dos arquivos de mídia, do MariaDB ou das credenciais.

### Clientes

- **Web:** interface para navegar, pesquisar, administrar e reproduzir a biblioteca no navegador.
- **Android:** login por senha ou Quick Connect; descoberta de servidor na rede local; home e bibliotecas; busca e detalhes; player ExoPlayer (incluindo HLS/DASH); downloads offline; TV ao vivo/EPG; SyncPlay; Chromecast; temas e preferências de áudio/legendas.
- **iOS:** cliente nativo em desenvolvimento, com login, bibliotecas, busca, detalhes, TV ao vivo, perfil e funcionalidades em evolução. Consulte o projeto e as releases antes de planejar uso em produção.

## Componentes do repositório

| Diretório | Conteúdo |
| --- | --- |
| `MulletaFlix-master/` | Servidor .NET, APIs, catálogo e testes. |
| `MulletaFlix-web-master/` | Cliente web React/TypeScript. |
| `MulletaFlix-android/` | Aplicativo Android Kotlin/Compose. |
| `MulletaFlix-iOS/` | Cliente iOS nativo em desenvolvimento. |
| `MulletaFlix-packaging-master/` | Recursos e documentação de empacotamento Windows. |
| `mulletaflix-server-windows-mulletaflix-installer-branding/` | Marca e recursos do instalador Windows. |
| `nebula/` | Componentes e materiais relacionados ao Nebula. |
| `tools/` | Ferramentas auxiliares, incluindo utilitários de migração. |
| `docs/` | Runbooks e documentação do projeto. |

## Instalação rápida no Windows

1. Abra [Releases](https://github.com/raphaelpera85/mulletaflix-completo/releases) e baixe os arquivos da versão desejada.
2. Para uma máquina nova, execute o instalador `mulletaflix_<versão>_windows-x64.exe`.
3. Para atualizar uma instalação existente, use `mulletaflix-update-win-x64.zip` e siga as instruções incluídas no pacote. Não use o instalador de instalação limpa sobre uma instalação existente sem antes conferir as instruções da release.
4. Inicie o serviço MulletaFlix e abra `http://localhost:8096` no navegador.
5. Complete o assistente inicial para criar a conta administrativa e configurar o servidor.

Os nomes dos artefatos podem variar entre releases; use sempre os arquivos anexados à release oficial correspondente. O pacote Android, quando publicado, é distribuído separadamente.

## Tutorial de configuração inicial

### 1. Crie as bibliotecas

No painel, abra **Bibliotecas** e adicione uma biblioteca para cada tipo de conteúdo. Selecione o tipo correto e indique o caminho das pastas que o servidor consegue ler. Exemplos de organização:

```text
 D:\Midias\Filmes
 D:\Midias\Series
 D:\Midias\Animacoes
 D:\Midias\Novelas
 D:\Midias\Doramas
```

Os caminhos acima são apenas exemplos: use os diretórios existentes na sua máquina. Evite adicionar uma pasta-pai e também suas subpastas como bibliotecas separadas sem necessidade, pois isso pode gerar itens duplicados ou classificados na categoria errada. Confirme as permissões de leitura da conta que executa o serviço e, depois de adicionar os caminhos, execute uma varredura.

### 2. Ajuste metadados e imagens

Nas opções da biblioteca, escolha idioma/região e provedores de metadados disponíveis. Execute a atualização da biblioteca após alterar provedores ou nomes/pastas. Se uma mídia receber correspondência errada, abra os detalhes do item, use a opção de identificação/correção de correspondência e então atualize os metadados. Para uma capa/banner específico, use a seleção de imagens do item. A disponibilidade depende dos provedores externos e de suas políticas/limites.

### 3. Configure usuários e dispositivos

No painel **Usuários**, crie uma conta para cada pessoa, ajuste permissões e bibliotecas acessíveis e defina se o usuário pode usar reprodução remota, downloads ou outras funções. Em cada cliente, informe o endereço do servidor — localmente, `http://localhost:8096`; em outro dispositivo da rede, use o IP local do computador servidor, como `http://192.168.1.20:8096` — e entre com a conta criada.

### 4. Configure reprodução e FFmpeg

Teste primeiro um arquivo local compatível com o dispositivo. Se a reprodução exigir conversão, confirme que a versão do FFmpeg incluída/instalada pelo pacote do servidor está acessível e consulte o log de transcodificação no painel. A reprodução direta depende do suporte do navegador ou aplicativo ao contêiner, codec, áudio e legendas; uma falha em um cliente não significa necessariamente que o arquivo esteja ausente ou corrompido.

### 5. Intro nativa e cache de reprodução

Se a versão instalada oferecer essas opções, configure a intro em **Painel → Marca** e habilite a reprodução antes da mídia. Selecione um arquivo de vídeo que exista e seja legível **no computador servidor**; um caminho do seu computador cliente não é automaticamente acessível pelo serviço. Algumas distribuições podem incluir o arquivo padrão em `media/mulletaflix_intro.mp4`. Verifique se ele existe no pacote instalado antes de depender dele.

O pré-cache de mídia, quando habilitado/disponível na versão, também depende de espaço gravável no servidor. Monitore o disco e os logs; não configure o cache na mesma unidade já sem espaço livre.

### 6. Configuração opcional do Nebula

Habilite o Nebula somente se for usar seus recursos. Configure pelo painel/arquivo de configuração suportado pela versão:

- conexão MongoDB e caminhos de monitoramento, staging e destino;
- integração Telegram (API/bots e chats necessários);
- credenciais e opções de transferência;
- URL/chave do Supabase, se for usar backup remoto;
- intervalo do backup automático Supabase (padrão atual: 1 hora).

Guarde chaves, tokens, senhas e strings de conexão em configuração protegida; nunca os publique no Git, em issues ou em logs. A chave Supabase deve ter apenas os privilégios necessários. Antes de restaurar um backup, faça uma cópia do estado atual: restore pode substituir dados Nebula.

**Segurança de rede:** o FTP/HTTP nativo do Nebula não oferece FTPS. Por padrão, o listener FTP é vinculado a `127.0.0.1` (porta `2121`); o streaming HTTP usa `2123`, e as portas passivas padrão são `60000–60009`. Não exponha FTP/HTTP em texto puro à internet. Mantenha o serviço em loopback/rede confiável ou use VPN/proxy TLS devidamente configurado. A opção que permite FTP remoto inseguro é uma decisão explícita de risco, não uma recomendação.

### 7. Acesso fora de casa

Para acesso remoto, prefira VPN ou proxy reverso com HTTPS, autenticação forte e firewall restritivo. Não encaminhe simplesmente a porta `8096` para a internet sem TLS e sem entender as implicações. Configure o endereço público usado pelos clientes e notificações somente depois de testar o acesso seguro de fora da rede. DNS dinâmico, roteador e certificado TLS são responsabilidade da instalação.

### 8. Verifique saúde e logs

Em instalações locais, confirme primeiro `http://localhost:8096/health` e `http://localhost:8096/ready`. Para diagnóstico do Nebula, use o painel de status/logs ou as rotas administrativas documentadas em [`docs/NEBULA-RUNBOOK.md`](docs/NEBULA-RUNBOOK.md). Remova tokens, cookies, senhas, URIs MongoDB/Supabase e URLs STRM autenticadas antes de compartilhar logs.

## Executar a partir do código-fonte

Os comandos abaixo são voltados ao desenvolvimento. Para uso normal, prefira os pacotes da página de releases.

### Servidor + web

Requisitos: SDK .NET 10, Node.js 24 ou superior e npm 11 ou superior. Na raiz do repositório:

```powershell
cd MulletaFlix-web-master
npm ci
npm run build:check
npm test
npm run build:production
cd ..
dotnet test MulletaFlix-master\MulletaFlix.sln
dotnet run --project MulletaFlix-master\Jellyfin.Server\Jellyfin.Server.csproj --webdir MulletaFlix-web-master\dist
```

O servidor de desenvolvimento abre normalmente em `http://localhost:8096`. Verifique os diretórios de projeto e a documentação específica caso os nomes mudem entre branches.

### Android

Com Android Studio e JDK conforme exigidos pelo projeto:

```powershell
cd MulletaFlix-android
.\gradlew.bat test
.\gradlew.bat assembleDebug
```

### iOS

O cliente iOS requer macOS e Xcode. Abra `MulletaFlix-iOS/MulletaFlix.xcodeproj` e selecione o scheme `MulletaFlix`; veja também o README do próprio diretório para estado atual e testes da camada Core.

## Releases, licença e contribuição

O código do servidor e dos clientes está sob GPL-2.0 ou posterior, conforme os arquivos de licença de cada componente. Consulte as licenças e avisos de terceiros antes de redistribuir pacotes.

Para reportar problema, inclua versão, sistema operacional, passos para reproduzir e logs **sanitizados**. Nunca publique tokens, senhas, credenciais de banco, URLs privadas ou dados pessoais. Para contribuir, faça alterações no componente apropriado, execute os testes correspondentes e atualize as notas de versão quando a mudança afetar o produto.

## Documentação adicional

- [Runbook Nebula: saúde, streaming, backup e restore](docs/NEBULA-RUNBOOK.md)
- [README do servidor e desenvolvimento](MulletaFlix-master/README.md)
- [README do Android](MulletaFlix-android/README.md)
- [README do iOS](MulletaFlix-iOS/README.md)
- [Documentação do empacotamento Windows](MulletaFlix-packaging-master/README.md)
