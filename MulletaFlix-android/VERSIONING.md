# Versionamento do APK

O MulletaFlix Android usa Semantic Versioning com três componentes (`major.minor.patch`).

- A versão atualmente configurada em `gradle/libs.versions.toml` é `1.3.68` (`versionCode` 369).
- Incrementar o patch até `.99`; depois zerar o patch e incrementar o minor. Exemplo: `1.3.99` -> `1.4.0`. Nunca publicar `.100`.
- O `versionCode` do Android continua inteiro e monotonicamente crescente, independentemente do `versionName`.
- Antes de cada nova release oficial do APK, conferir a versão do APK anteriormente entregue e validar `versionName`, `versionCode` e SHA-256 do artefato.
- Atualize as notas da release junto com cada release do APK. Elas devem espelhar, item por item, as melhorias e correções realmente incluídas naquele APK e comprovadas pelo diff e pelos testes; confira também se versão e artefato citados correspondem ao APK publicado.
- Redija e valide as notas a partir do diff final e dos resultados de teste do artefato que será publicado; não copie notas de uma versão anterior nem antecipe mudanças ainda não incluídas no APK.
- Não use notas genéricas, não prometa funcionalidades ausentes e não anuncie mudanças exclusivas do servidor nas notas do APK.
