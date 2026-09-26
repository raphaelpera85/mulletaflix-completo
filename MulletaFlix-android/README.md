# MulletaFlix Android

Aplicativo Android oficial do [MulletaFlix](https://github.com/raphaelpera85/MulletaFlix) — o servidor de mídia open-source para sua coleção pessoal.

O APK atua como cliente/player: autenticação, catálogo, metadados e streams são
fornecidos pelo servidor MulletaFlix remoto. A URL pública padrão é
`http://mulletaflix.duckdns.org:8096`. Ao iniciar, o app procura automaticamente
um servidor MulletaFlix na mesma LAN, verifica a conexão e o prioriza; se nenhum
servidor local responder, usa a URL pública. A busca manual continua disponível.

## ✨ Funcionalidades

| Módulo | Status |
|---|---|
| 🔐 Autenticação (Senha + Quick Connect) | ✅ |
| 🏠 Home com Hero Banner + 6 seções | ✅ |
| 📚 Biblioteca com Grid/Lista + Filtros + Sort | ✅ |
| 🎬 Detalhe completo (Filmes, Séries, Músicas, Livros) | ✅ |
| ▶️ Player ExoPlayer HLS/DASH + OSD completo | ✅ |
| 🔍 Busca universal em tempo real | ✅ |
| ⬇️ Downloads offline | ✅ |
| 📺 Live TV + EPG | ✅ |
| ⚙️ Configurações com 8 temas | ✅ |
| 👤 Perfil multi-usuário com preferências de áudio/legenda por conta e servidor | ✅ |
| 📡 SyncPlay | ✅ |
| 📺 Chromecast | ✅ |

## 🏗️ Arquitetura

```
MulletaFlix-android/
├── app/                    # Entry point — MainActivity, NavHost, DI root
├── core/
│   ├── api/                # Retrofit, AuthInterceptor, DTOs
│   └── common/             # SessionRepository, extensions
├── domain/                 # Entidades puras + interfaces de repositório
├── data/                   # Implementações de repositório + Room + DTOs
├── design-system/          # Tema Material 3, tipografia, componentes
└── feature/
    ├── auth/               # Login + ServerSelection
    ├── home/               # HomeScreen + HomeViewModel
    ├── library/            # LibraryScreen + paginação
    ├── item-detail/        # ItemDetailScreen (todos os tipos)
    ├── player/             # VideoPlayerScreen + ExoPlayer
    ├── search/             # SearchScreen
    ├── downloads/          # DownloadsScreen
    ├── live-tv/            # LiveTvScreen + EPG
    ├── settings/           # SettingsScreen + 8 temas
    ├── user/               # ProfileScreen
    └── sync-play/          # SyncPlayScreen
```

## 🛠️ Stack Tecnológica

- **Language / build:** Kotlin 2.3.21 + Android Gradle Plugin 9.3.2
- **SDK:** compile 37, target 36, minimum 24
- **UI:** Jetpack Compose (BOM 2026.09.00) + Material 3
- **DI:** Hilt
- **Network:** Retrofit + OkHttp + Moshi
- **Media:** Media3 / ExoPlayer (HLS, DASH, MP4, MKV)
- **Image:** Coil
- **DB:** Room (cache local)
- **Async:** Coroutines + Flow
- **Cast:** Google Cast Framework (Chromecast)
- **Splash:** AndroidX SplashScreen API

## 🚀 Como Compilar

```bash
# Requisitos: Android Studio Iguana ou superior, JDK 17
git clone <repo>
cd MulletaFlix-android
./gradlew assembleDebug
```

## 📡 Configuração

1. Instale o servidor MulletaFlix (veja `MulletaFlix-master/`)
2. Abra o app → selecione o servidor (ex: `http://192.168.1.100:8096`)
3. Faça login com sua conta ou use Quick Connect

## 🎨 Temas Disponíveis

| Tema | Descrição |
|---|---|
| Sistema | Segue o modo claro/escuro do Android |
| Escuro | Fundo preto cinematográfico, detalhes em vermelho (padrão) |
| Claro | Fundo branco |
| Netflix | Fundo preto, vermelho |
| Purple Haze | Fundo roxo escuro |
| Blue Radiance | Fundo azul escuro |
| WMC | Windows Media Center |
| Apple TV | Estilo Apple TV |

## 📄 Licença

GNU General Public License v2.0 — mesma licença do servidor MulletaFlix.
