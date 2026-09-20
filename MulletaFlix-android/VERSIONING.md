# Versionamento do APK

O MulletaFlix Android usa Semantic Versioning com três componentes (`major.minor.patch`).

- As releases atuais seguem `1.0.x` até `1.0.99`.
- A versão seguinte a `1.0.99` é `1.1.0`; nunca publicar `1.0.100`.
- Depois de `1.1.0`, incrementar o patch até `1.1.99`; em seguida, usar `1.2.0`.
- O `versionCode` do Android continua inteiro e monotonicamente crescente, independentemente do `versionName`.
