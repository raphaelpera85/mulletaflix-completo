# Rotação da chave de assinatura Android

## Chave adotada para novas releases

- Certificado público: `mulletaflix-release-current.cer`
- SHA-256: `4890D80B87FE27C804FF875B549ACD57B946FE6F9BBA0B9D7718989EC5A0A24C`
- Alias: `mulletaflix-release`
- Keystore privada local: `C:\Users\Raphael\.android\mulletaflix-release.jks`

O arquivo `.jks`, as senhas e qualquer cópia da chave privada ficam fora do Git. O CI deve receber a keystore por segredo seguro (`ANDROID_KEYSTORE_BASE64`) e as senhas por secrets separados.

## Compatibilidade

A release Android `v1.3.80` foi assinada com o certificado legado `224F9A6BD12690E1114ACE649BBFA778D3E7E99DAE608FF711DDF9131E036273`. O proprietário confirmou que fará uma reinstalação limpa para receber a v1.3.81 assinada com a chave rotacionada `4890D80B87FE27C804FF875B549ACD57B946FE6F9BBA0B9D7718989EC5A0A24C`; remover o app pode apagar dados locais. Não altere o `applicationId`.

Antes de qualquer publicação, execute `:app:verifyProductionSigningCertificate` e confirme que o APK foi assinado com o certificado desta documentação.

## Android Developer Verification / Play Protect

O pacote oficial é `org.mulletaflix.android` e deve permanecer associado ao certificado de produção SHA-256 `4890D80B87FE27C804FF875B549ACD57B946FE6F9BBA0B9D7718989EC5A0A24C`.

O certificado legado `224F9A6BD12690E1114ACE649BBFA778D3E7E99DAE608FF711DDF9131E036273` pertence ao fluxo antigo de desenvolvimento e não deve voltar a ser usado em releases oficiais.

Para comprovar a posse da chave no Android Developer Console quando o Google fornecer o snippet de registro:

```powershell
.\tools\Prepare-AndroidDeveloperVerification.ps1 -RegistrationSnippet '<snippet fornecido pelo Android Developer Console>'
```

O script cria temporariamente `app/src/main/assets/adi-registration.properties`, gera um APK `release`, valida a assinatura de produção e o package name, copia o APK para `dist/android-developer-verification/mulletaflix-android-developer-verification.apk` e remove o arquivo de registro do código-fonte ao final.

Envie esse APK somente na etapa de verificação de posse da chave no Android Developer Console. Releases normais continuam sendo geradas por `build-app-package.ps1`.
