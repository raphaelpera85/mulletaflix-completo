# Release de produção do MulletaFlix iOS

## Primeira versão

A primeira release pública é `1.0.0`. O projeto mantém essa versão em
`MARKETING_VERSION` no target Release e em `Info.plist` por meio de
`$(MARKETING_VERSION)`. O `CURRENT_PROJECT_VERSION` inicial é `1`.

## Gates obrigatórios em macOS

Executar a partir de `MulletaFlix-iOS`:

```bash
swift test --enable-code-coverage
xcodebuild -project MulletaFlix.xcodeproj \
  -scheme MulletaFlix \
  -configuration Release \
  -destination 'generic/platform=iOS Simulator' \
  CODE_SIGNING_ALLOWED=NO \
  build
```

Antes da distribuição, abrir o projeto em Xcode com uma equipe Apple válida,
confirmar assinatura automática para distribuição e gerar um archive Release
para dispositivo (`generic/platform=iOS`). A release não pode usar o produto
Debug, um build de simulador ou um archive sem assinatura de distribuição.

## Publicação

Depois dos gates verdes, publicar o artefato assinado como `mulletaflix-ios-v1.0.0.ipa`
na tag `ios-v1.0.0`, com notas que descrevam somente as mudanças presentes no
diff validado, os testes executados e limitações conhecidas. Para versões
seguintes, incrementar `MARKETING_VERSION`, `CURRENT_PROJECT_VERSION`, tag e
nome do artefato juntos.

Na mesma entrega, atualizar e publicar `portal-site/index.html`,
`portal-site/downloads.html` e `portal-site/docs.html` com a versão, link do
IPA, link das notas e status real dos gates. Validar no portal publicado que a
versão exibida coincide com a release do GitHub antes de concluir.
