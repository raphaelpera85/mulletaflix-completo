# Versionamento do APK

O MulletaFlix Android usa Semantic Versioning com três componentes (`major.minor.patch`).

- A versão atualmente configurada em `gradle/libs.versions.toml` é `1.3.82` (`versionCode` 382).
- Incrementar o patch até `.99`; depois zerar o patch e incrementar o minor. Exemplo: `1.0.99` -> `1.1.0`. Nunca publicar `.100`.
- O `versionCode` do Android continua inteiro e monotonicamente crescente, independentemente do `versionName`.
- Antes de cada nova release oficial do APK, conferir a versão do APK anteriormente entregue e validar `versionName`, `versionCode` e SHA-256 do artefato.
- Toda release oficial do APK deve ser gerada exclusivamente em configuração de produção (`release`) e assinada com o keystore de produção. Nunca publicar APK `Debug`, assinado com a chave debug/desenvolvimento ou usando keystore temporário; se `KEYSTORE_PATH`, `KEYSTORE_PASSWORD`, `KEY_ALIAS` ou `KEY_PASSWORD` não estiverem configuradas, a publicação deve ser interrompida.
- A partir da rotação registrada em `signing/SIGNING-KEY-ROTATION.md`, o certificado SHA-256 aceito para novas releases é `4890D80B87FE27C804FF875B549ACD57B946FE6F9BBA0B9D7718989EC5A0A24C`. Gradle, empacotamento e publicação devem rejeitar APK assinado com qualquer outro certificado.
- A v1.3.80 e instalações anteriores usam o certificado legado `224F9A6BD12690E1114ACE649BBFA778D3E7E99DAE608FF711DDF9131E036273`; a v1.3.81 usa o certificado de produção rotacionado `4890D80B87FE27C804FF875B549ACD57B946FE6F9BBA0B9D7718989EC5A0A24C`. Android não permite instalar a v1.3.81 por cima da v1.3.80 sem a chave anterior; informe usuários para desinstalar e instalar novamente, com risco de perder dados locais.
- Atualize as notas da release junto com cada release do APK. Elas devem espelhar, item por item, as melhorias e correções realmente incluídas naquele APK e comprovadas pelo diff e pelos testes; confira também se versão e artefato citados correspondem ao APK publicado.
- Redija e valide as notas a partir do diff final e dos resultados de teste do artefato que será publicado; não copie notas de uma versão anterior nem antecipe mudanças ainda não incluídas no APK.
- Não use notas genéricas, não prometa funcionalidades ausentes e não anuncie mudanças exclusivas do servidor nas notas do APK.
