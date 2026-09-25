# Validação macOS

Execute os comandos a partir de `MulletaFlix-iOS` em macOS com Xcode 16 ou
superior, Swift disponível no PATH e um SDK iOS 18 instalado.

## Camada Core

```sh
swift test
```

Esse comando executa os testes de modelos, contratos HTTP, política de
downloads e mensagens realtime do SyncPlay.

## Target iOS

```sh
xcodebuild \
  -project MulletaFlix.xcodeproj \
  -scheme MulletaFlix \
  -sdk iphonesimulator \
  -destination 'generic/platform=iOS Simulator' \
  -configuration Debug \
  CODE_SIGNING_ALLOWED=NO \
  -resultBundlePath xcodebuild-results.xcresult \
  build
```

O build usa o target do projeto Xcode e não exige uma equipe de assinatura para
validar a compilação do simulador. O bundle `xcodebuild-results.xcresult` pode
ser aberto no Xcode para inspecionar avisos e fases do build.

## Execução no simulador

Para selecionar, inicializar, executar os testes Core e compilar no primeiro
iPhone Simulator disponível:

```sh
bash scripts/run-simulator-tests.sh
```

Também é possível informar o nome do dispositivo:

```sh
bash scripts/run-simulator-tests.sh "iPhone 16 Pro"
```

```sh
xcrun simctl list devices available
xcodebuild \
  -project MulletaFlix.xcodeproj \
  -scheme MulletaFlix \
  -sdk iphonesimulator \
  -destination 'platform=iOS Simulator,name=iPhone 16' \
  -configuration Debug \
  CODE_SIGNING_ALLOWED=NO \
  build
```

Depois do build, abra o scheme no Xcode para validar login, descoberta LAN,
reprodução, downloads em background, AirPlay e salas SyncPlay contra um
servidor de teste.

## Casos de paridade Android/iOS

Com o app instalado e o simulador inicializado, valide também:

1. Favoritos: no Perfil, abra “Minha Lista”, confirme a grade, o estado vazio e
   a navegação para o detalhe.
2. Deep link após login:

   ```sh
   xcrun simctl openurl booted 'mulletaflix://item/movie-123'
   ```

   O detalhe do item deve abrir. Repita o comando antes do login e confirme que
   o link é resolvido depois da autenticação.
3. Player: altere a proporção entre Original, Zoom e Esticar; teste toque duplo
   em cada lado, busca por arraste, brilho, estatísticas/compartilhamento e o
   botão de retry após interromper a fonte.
4. Configurações: desative o botão de pular introdução, limpe o cache de
   imagens e confirme que downloads offline e sessão permanecem intactos.
5. Música: abra um álbum, confira as faixas e use o botão de citação para
   carregar a letra de uma faixa; valide também o estado de letra indisponível.
6. Busca por voz: permita microfone/reconhecimento de fala, toque no microfone,
   dite um título e confirme que o texto preenche a busca e dispara os resultados.
7. Servidores: entre em dois servidores, saia e confirme que ambos aparecem em
   “Servidores salvos”; remova um deles e confirme que ele desaparece da lista.
8. Branding: verifique um servidor que possua aviso de login e confirme que o
   texto aparece na tela de acesso; repita com um servidor sem aviso.
9. Saúde do servidor: confirme que um servidor que expõe `Health` mostra o
   estado na tela de acesso e que a ausência dessa rota não impede o login.

Para testar um link web oficial no Simulator, use:

```sh
xcrun simctl openurl booted 'https://mulletaflix.duckdns.org/web/#?id=movie-123'
```

O workflow `.github/workflows/ci.yml` executa automaticamente os dois primeiros
comandos em `macos-14`.
