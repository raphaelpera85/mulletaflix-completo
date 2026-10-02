# Rotação da chave de assinatura Android

## Chave adotada para novas releases

- Certificado público: `mulletaflix-release-current.cer`
- SHA-256: `4890D80B87FE27C804FF875B549ACD57B946FE6F9BBA0B9D7718989EC5A0A24C`
- Alias: `mulletaflix-release`
- Keystore privada local: `C:\Users\Raphael\.android\mulletaflix-release.jks`

O arquivo `.jks`, as senhas e qualquer cópia da chave privada ficam fora do Git. O CI deve receber a keystore por segredo seguro (`ANDROID_KEYSTORE_BASE64`) e as senhas por secrets separados.

## Compatibilidade

A release Android `v1.3.80` foi assinada com o certificado legado `224F9A6BD12690E1114ACE649BBFA778D3E7E99DAE608FF711DDF9131E036273`. Android não aceita uma atualização do mesmo pacote assinada por esta nova chave. Portanto, uma próxima publicação com a nova chave exige uma estratégia explícita: distribuição como novo aplicativo/pacote ou recuperação da chave legada para manter a atualização.

Antes de qualquer publicação, execute `:app:verifyProductionSigningCertificate` e confirme que o APK foi assinado com o certificado desta documentação.
