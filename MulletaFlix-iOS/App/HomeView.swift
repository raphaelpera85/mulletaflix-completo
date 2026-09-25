import SwiftUI
import UIKit
import AVKit
import AVFAudio
import Speech

enum IOSVideoAspectRatio: String, CaseIterable, Identifiable {
    case fit
    case zoom
    case fill

    var id: String { rawValue }

    var title: String {
        switch self {
        case .fit: return "Ajustar (Original)"
        case .zoom: return "Preencher / Zoom"
        case .fill: return "Esticar"
        }
    }

    var videoGravity: AVLayerVideoGravity {
        switch self {
        case .fit: return .resizeAspect
        case .zoom: return .resizeAspectFill
        case .fill: return .resize
        }
    }
}

struct HomeView: View {
    @Environment(AppModel.self) private var model
    @Environment(\.scenePhase) private var scenePhase
    @State private var deepLinkItem: MediaItem?

    var body: some View {
        TabView {
            NavigationStack {
                HomeDashboard(model: model)
                    .navigationDestination(for: MediaItem.self) { item in ItemDetailView(item: item, model: model) }
            }
                .tabItem { Label("Início", systemImage: "house.fill") }
            NavigationStack { LibraryView(model: model) }
                .tabItem { Label("Biblioteca", systemImage: "rectangle.stack.fill") }
            NavigationStack { SearchView(model: model) }
                .tabItem { Label("Buscar", systemImage: "magnifyingglass") }
            NavigationStack { LiveTVView(model: model) }
                .tabItem { Label("TV ao vivo", systemImage: "dot.radiowaves.left.and.right") }
            NavigationStack { DownloadsView(model: model) }
                .tabItem { Label("Downloads", systemImage: "arrow.down.circle.fill") }
            NavigationStack { ProfileView(model: model) }
                .tabItem { Label("Perfil", systemImage: "person.crop.circle") }
        }
        .task {
            await model.loadHome()
            await model.loadLibraries()
            await model.loadLiveTV()
        }
        .refreshable { await model.loadHome() }
        .sheet(item: $deepLinkItem) { item in
            NavigationStack {
                ItemDetailView(item: item, model: model)
            }
        }
        .task(id: model.deepLinkRevision) {
            guard model.state == .signedIn else { return }
            deepLinkItem = await model.loadPendingDeepLinkItem()
        }
        .onChange(of: scenePhase) { _, phase in
            guard phase == .active,
                  ForegroundRefreshPolicy.shouldRefresh(
                      isSignedIn: model.state == .signedIn,
                      isNetworkAvailable: model.isNetworkAvailable,
                      isHomeLoading: model.isHomeLoading,
                      isLibrariesLoading: model.isLibrariesLoading,
                      isLiveTVLoading: model.isLiveTVLoading
                  ) else { return }
            Task {
                await model.loadHome()
                await model.loadLibraries()
                await model.loadLiveTV()
            }
        }
        .overlay(alignment: .top) {
            if model.isNetworkAvailable == false {
                Label("Sem conexão: conteúdo em cache continua disponível", systemImage: "wifi.slash")
                    .font(.footnote.weight(.medium))
                    .padding(.horizontal, 12)
                    .padding(.vertical, 8)
                    .background(.thinMaterial, in: Capsule())
                    .padding(.top, 8)
            }
        }
        .alert(
            "Não foi possível concluir",
            isPresented: Binding(
                get: { model.errorMessage != nil },
                set: { isPresented in
                    if !isPresented { model.errorMessage = nil }
                }
            )
        ) {
            Button("OK") { model.errorMessage = nil }
        } message: {
            Text(model.errorMessage ?? "Tente novamente.")
        }
    }
}

private struct ProfileView: View {
    let model: AppModel
    @State private var showingSwitchUser = false
    @State private var profileImageURL: URL?
    @State private var didCopyServerURL = false

    var body: some View {
        Form {
            if model.isProfileLoading && model.profile == nil {
                ProgressView("Carregando perfil…")
            }
            if let error = model.profileError {
                Section("Atualização do perfil") {
                    Label("Não foi possível atualizar as permissões.", systemImage: "exclamationmark.triangle")
                        .foregroundStyle(.secondary)
                    Text(error)
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                    Button("Tentar novamente") {
                        Task { await model.loadProfile() }
                    }
                    .buttonStyle(.bordered)
                }
            }
            Section("Conta") {
                HStack(spacing: 14) {
                    ZStack {
                        Circle().fill(.red.gradient)
                        Text(String((model.profile?.name ?? model.session?.userName ?? "M").prefix(1)).uppercased())
                            .font(.title2.bold())
                            .foregroundStyle(.white)
                        if let profileImageURL {
                            AuthenticatedArtwork(url: profileImageURL, token: model.session?.accessToken)
                                .clipShape(Circle())
                        }
                    }
                    .frame(width: 64, height: 64)
                    .accessibilityLabel("Foto de perfil")
                    VStack(alignment: .leading) {
                        Text(model.profile?.name ?? model.session?.userName ?? "")
                            .font(.headline)
                        if model.profile?.isAdministrator == true {
                            Label("Administrador", systemImage: "checkmark.shield.fill")
                                .font(.caption)
                                .foregroundStyle(.green)
                        }
                    }
                }
            }
            Section("Permissões do servidor") {
                PermissionRow(title: "Reprodução de mídia", enabled: model.profile?.canPlayMedia ?? true)
                PermissionRow(title: "TV ao vivo", enabled: model.profile?.canAccessLiveTV ?? true)
                PermissionRow(title: "Downloads offline", enabled: model.profile?.canDownload ?? true)
            }
            Section("Servidor") {
                HStack {
                    Label(model.serverInfo?.displayName ?? "Servidor MulletaFlix", systemImage: "server.rack")
                    Spacer()
                    if model.isVerifyingServer {
                        ProgressView()
                    } else {
                        Button("Verificar") {
                            Task { await model.verifyServer() }
                        }
                        .buttonStyle(.borderless)
                    }
                }
                if let info = model.serverInfo {
                    VStack(alignment: .leading, spacing: 4) {
                        Text(info.productName ?? "MulletaFlix")
                            .font(.subheadline.weight(.medium))
                        if let version = info.version {
                            Text("Versão \(version)")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }
                    }
                }
                HStack {
                    Text(model.serverURL)
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                        .textSelection(.enabled)
                    Spacer()
                    Button {
                        UIPasteboard.general.string = model.serverURL
                        didCopyServerURL = true
                        Task {
                            try? await Task.sleep(for: .seconds(2))
                            didCopyServerURL = false
                        }
                    } label: {
                        Label(
                            didCopyServerURL ? "Copiada" : "Copiar URL",
                            systemImage: didCopyServerURL ? "checkmark" : "doc.on.doc"
                        )
                    }
                    .buttonStyle(.borderless)
                    .accessibilityLabel(didCopyServerURL ? "URL do servidor copiada" : "Copiar URL do servidor")
                }
                NavigationLink {
                    SyncPlayView(model: model)
                } label: {
                    Label("Salas SyncPlay", systemImage: "person.2.fill")
                }
            }
            Section("Biblioteca") {
                NavigationLink {
                    FavoritesView(model: model)
                } label: {
                    Label("Minha Lista", systemImage: "heart.fill")
                }
            }
            Section("Preferências") {
                NavigationLink {
                    SettingsView(model: model)
                } label: {
                    Label("Reprodução e aparência", systemImage: "slider.horizontal.3")
                }
            }
            Section("Sessão") {
                Button("Alternar usuário", systemImage: "person.2") {
                    showingSwitchUser = true
                }
                Button("Sair", role: .destructive) { model.signOut() }
            }
        }
        .navigationTitle("Perfil")
        .task(id: model.session?.userID) {
            profileImageURL = nil
            await model.loadProfile()
            profileImageURL = await model.profileImageURL()
        }
        .sheet(isPresented: $showingSwitchUser) {
            SwitchUserView(model: model)
        }
    }
}

private struct FavoritesView: View {
    let model: AppModel

    private var columns: [GridItem] {
        [GridItem(.adaptive(minimum: model.compactGrid ? 112 : 150), spacing: 14)]
    }

    var body: some View {
        Group {
            if model.isFavoritesLoading && model.favoriteItems.isEmpty {
                ProgressView("Carregando Minha Lista…")
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if let error = model.favoritesError, model.favoriteItems.isEmpty {
                ContentUnavailableView {
                    Label("Minha Lista indisponível", systemImage: "wifi.exclamationmark")
                } description: {
                    Text(error)
                } actions: {
                    Button("Tentar novamente") {
                        Task { await model.loadFavorites() }
                    }
                    .buttonStyle(.borderedProminent)
                }
            } else {
                VStack(spacing: 8) {
                    if let error = model.favoritesError {
                        Label(error, systemImage: "exclamationmark.triangle")
                            .font(.footnote)
                            .foregroundStyle(.secondary)
                            .multilineTextAlignment(.center)
                            .padding(.horizontal)
                        Button("Tentar novamente") {
                            Task { await model.loadFavorites() }
                        }
                        .buttonStyle(.bordered)
                    }
                    if model.favoriteItems.isEmpty {
                        ContentUnavailableView(
                            "Nenhum favorito",
                            systemImage: "heart.slash",
                            description: Text("Adicione filmes, séries ou músicas à sua lista para vê-los aqui.")
                        )
                    } else {
                        ScrollView {
                            LazyVGrid(columns: columns, spacing: 18) {
                                ForEach(model.favoriteItems) { item in
                                    NavigationLink(value: item) {
                                        MediaCard(item: item, model: model)
                                    }
                                    .buttonStyle(.plain)
                                }
                            }
                            .padding()
                        }
                    }
                }
            }
        }
        .navigationTitle("Minha Lista")
        .navigationDestination(for: MediaItem.self) { item in
            ItemDetailView(item: item, model: model)
        }
        .task(id: model.session?.userID) { await model.loadFavorites() }
        .refreshable { await model.loadFavorites() }
    }
}

private struct SwitchUserView: View {
    let model: AppModel
    @Environment(\.dismiss) private var dismiss
    @State private var selectedUser = ""
    @State private var password = ""
    @State private var showingError = false

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    Picker("Usuário", selection: $selectedUser) {
                        Text("Selecione um usuário").tag("")
                        ForEach(model.publicUsers) { user in
                            Text(user.name).tag(user.name)
                        }
                    }
                    SecureField("Senha (opcional)", text: $password)
                } footer: {
                    Text("O servidor pode exigir a senha do usuário selecionado.")
                }
                if showingError, let error = model.errorMessage {
                    Section {
                        Text(error).foregroundStyle(.red)
                    }
                }
                Button(model.isSwitchingUser ? "Alternando…" : "Alternar") {
                    Task {
                        let success = await model.switchUser(username: selectedUser, password: password)
                        if success { dismiss() } else { showingError = true }
                    }
                }
                .disabled(selectedUser.isEmpty || model.isSwitchingUser)
                .buttonStyle(.borderedProminent)
            }
            .navigationTitle("Alternar usuário")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancelar") { dismiss() }
                }
            }
        }
        .task {
            await model.loadPublicUsers()
            if selectedUser.isEmpty {
                selectedUser = model.publicUsers.first?.name ?? ""
            }
        }
    }
}

private struct SettingsView: View {
    let model: AppModel

    private let playbackRates = [0.5, 0.75, 1.0, 1.25, 1.5, 2.0]
    @State private var showingCacheCleared = false
    @State private var showingLicenses = false

    private var appVersion: String {
        let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "desconhecida"
        return "MulletaFlix iOS \(version)"
    }

    var body: some View {
        Form {
            Section("Reprodução") {
                Toggle("Iniciar reprodução automaticamente", isOn: Bindable(model).autoPlay)
                Toggle("Picture-in-Picture", isOn: Bindable(model).pictureInPictureEnabled)
                Toggle("Mostrar botão de pular introdução", isOn: Bindable(model).skipIntro)
                Toggle("Baixar somente no Wi-Fi", isOn: Bindable(model).wifiOnlyDownloads)
                if model.wifiOnlyDownloads {
                    Text("Downloads aguardam uma conexão Wi-Fi antes de começar.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                }
                Picker("Tema", selection: Bindable(model).themePreference) {
                    ForEach(IOSThemePreference.allCases) { theme in
                        Text(theme.title).tag(theme)
                    }
                }
                Picker("Velocidade padrão", selection: Bindable(model).playbackRate) {
                    ForEach(playbackRates, id: \.self) { rate in
                        Text(rate == 1.0 ? "Normal" : "\(rate, specifier: \"%.2g\")x").tag(rate)
                    }
                }
                Picker("Qualidade padrão", selection: Bindable(model).defaultQuality) {
                    Text("Automático").tag("Auto")
                    Text("4K").tag("4K")
                    Text("1440p").tag("1440p")
                    Text("1080p").tag("1080p")
                    Text("720p").tag("720p")
                    Text("480p").tag("480p")
                }
                Picker("Proporção da imagem", selection: Bindable(model).videoAspectRatio) {
                    ForEach(IOSVideoAspectRatio.allCases) { ratio in
                        Text(ratio.title).tag(ratio)
                    }
                }
            }
            Section("Idioma") {
                TextField("Idioma do áudio (ex.: pt-BR)", text: Bindable(model).preferredAudioLanguage)
                    .textInputAutocapitalization(.never)
                TextField("Idioma da legenda (ex.: pt-BR)", text: Bindable(model).preferredSubtitleLanguage)
                    .textInputAutocapitalization(.never)
                Text("O idioma será usado como preferência nas próximas sessões de reprodução.")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
            }
            Section("Catálogo") {
                Toggle("Grade compacta", isOn: Bindable(model).compactGrid)
                Toggle("Usar lista nas bibliotecas", isOn: Bindable(model).libraryListLayout)
                Text("A grade compacta será aplicada às prateleiras e resultados do catálogo.")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
                Picker("Ordenar bibliotecas por", selection: Bindable(model).librarySort) {
                    Text("Nome").tag("SortName")
                    Text("Data de lançamento").tag("PremiereDate")
                    Text("Data adicionada").tag("DateCreated")
                    Text("Avaliação").tag("CommunityRating")
                    Text("Data de atualização").tag("DateLastMediaAdded")
                }
                Picker("Direção", selection: Bindable(model).librarySortOrder) {
                    Text("Ascendente").tag("Ascending")
                    Text("Descendente").tag("Descending")
                }
            }
            Section("Cache") {
                Button(role: .destructive) {
                    ArtworkImageCache.removeAll()
                    showingCacheCleared = true
                } label: {
                    Label("Limpar cache de imagens", systemImage: "trash")
                }
                Text("Remove imagens armazenadas temporariamente. Downloads offline e dados da sessão não são afetados.")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
            }
            Section("Sobre") {
                Label(appVersion, systemImage: "info.circle")
                    .foregroundStyle(.secondary)
                Link(destination: URL(string: "https://github.com/raphaelpera85/MulletaFlix")!) {
                    Label("GitHub", systemImage: "safari")
                }
                Button {
                    showingLicenses = true
                } label: {
                    Label("Licenças", systemImage: "scroll")
                }
            }
        }
        .navigationTitle("Preferências")
        .alert("Cache limpo", isPresented: $showingCacheCleared) {
            Button("OK", role: .cancel) { }
        } message: {
            Text("As imagens serão carregadas novamente quando necessário.")
        }
        .alert("Licenças", isPresented: $showingLicenses) {
            Button("OK", role: .cancel) { }
        } message: {
            Text("MulletaFlix iOS é distribuído sob GPL-2.0 e utiliza componentes das plataformas Apple e Swift.")
        }
        .onChange(of: model.librarySort) { _, _ in
            Task { await model.reloadSelectedLibrary() }
        }
        .onChange(of: model.librarySortOrder) { _, _ in
            Task { await model.reloadSelectedLibrary() }
        }
    }
}

private struct SyncPlayView: View {
    let model: AppModel
    @State private var groupName = ""

    var body: some View {
        List {
            Section {
                Text("Crie ou entre em uma sala compartilhada. O cliente mantém uma conexão em tempo real com o servidor enquanto você estiver na sala.")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
                Label("WebSocket: \(model.syncPlayRealtimeStatus)", systemImage: model.syncPlayRealtimeStatus == "Conectado" ? "checkmark.circle.fill" : "circle.dotted")
                    .font(.caption)
                    .foregroundStyle(model.syncPlayRealtimeStatus == "Conectado" ? .green : .secondary)
                HStack {
                    TextField("Nome da nova sala", text: $groupName)
                    Button("Criar") {
                        let name = groupName
                        groupName = ""
                        Task { await model.createSyncPlayGroup(name: name) }
                    }
                    .disabled(groupName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
            }
            if let error = model.syncPlayError {
                Section {
                    HStack(alignment: .top, spacing: 10) {
                        Label("Não foi possível atualizar as salas", systemImage: "wifi.exclamationmark")
                            .foregroundStyle(.red)
                        Spacer()
                        Button("Tentar novamente") {
                            Task { await model.loadSyncPlay() }
                        }
                        .buttonStyle(.bordered)
                    }
                    Text(error)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
            if model.isSyncPlayLoading && model.syncPlayGroups.isEmpty {
                ProgressView().frame(maxWidth: .infinity)
            } else if model.syncPlayGroups.isEmpty {
                ContentUnavailableView("Nenhuma sala", systemImage: "person.2.slash", description: Text("Crie uma sala para assistir em grupo."))
            } else {
                Section("Salas disponíveis") {
                    ForEach(model.syncPlayGroups) { group in
                        VStack(alignment: .leading, spacing: 8) {
                            HStack {
                                Text(group.groupName).font(.headline)
                                Spacer()
                                Text(group.state ?? "Pronto")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                            }
                            Text("Participantes: \(group.participants.joined(separator: ", ").isEmpty ? "Ninguém" : group.participants.joined(separator: ", "))")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                            Button(model.activeSyncPlayGroupID == group.groupId ? "Sala atual" : "Entrar na sala") {
                                Task { await model.joinSyncPlayGroup(group) }
                            }
                            .disabled(model.activeSyncPlayGroupID == group.groupId)
                        }
                        .padding(.vertical, 4)
                    }
                }
            }
            if model.activeSyncPlayGroupID != nil {
                Section("Controles da sala") {
                    Label(
                        model.syncPlayRealtimeStatus == "Conectado"
                            ? "Sincronização em tempo real conectada"
                            : "Conectando à sincronização em tempo real…",
                        systemImage: model.syncPlayRealtimeStatus == "Conectado" ? "checkmark.circle.fill" : "circle.dotted"
                    )
                    .font(.caption)
                    .foregroundStyle(model.syncPlayRealtimeStatus == "Conectado" ? .green : .secondary)
                    if let command = model.lastSyncPlayCommand?.name {
                        Text("Último comando: \(command)")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    HStack {
                        Button("Pausar", systemImage: "pause.fill") {
                            Task { await model.sendSyncPlayCommand(.pause) }
                        }
                        .disabled(model.isSyncPlaySubmitting)
                        Button("Retomar", systemImage: "play.fill") {
                            Task { await model.sendSyncPlayCommand(.unpause) }
                        }
                        .disabled(model.isSyncPlaySubmitting)
                    }
                    Button("Parar reprodução do grupo", systemImage: "stop.fill") {
                        Task { await model.sendSyncPlayCommand(.stop) }
                    }
                    .disabled(model.isSyncPlaySubmitting)
                    Button("Sair da sala atual", role: .destructive) {
                        Task { await model.leaveSyncPlayGroup() }
                    }
                    .disabled(model.isSyncPlaySubmitting)
                }
            }
        }
        .navigationTitle("Salas SyncPlay")
        .refreshable { await model.loadSyncPlay() }
        .task {
            while !Task.isCancelled {
                await model.loadSyncPlay()
                try? await Task.sleep(for: .seconds(5))
            }
        }
    }
}

private struct PermissionRow: View {
    let title: String
    let enabled: Bool

    var body: some View {
        Label(title, systemImage: enabled ? "checkmark.circle.fill" : "xmark.circle.fill")
            .foregroundStyle(enabled ? .primary : .secondary)
            .accessibilityValue(enabled ? "Permitido" : "Bloqueado")
    }
}

struct LiveTVView: View {
    let model: AppModel
    @State private var section = 0

    private var columns: [GridItem] {
        [GridItem(.adaptive(minimum: model.compactGrid ? 112 : 150), spacing: 14)]
    }

    var body: some View {
        ScrollView {
            if model.isLiveTVLoading {
                ProgressView().frame(maxWidth: .infinity).padding(.top, 48)
            } else {
                if let error = model.liveTVError {
                    ContentUnavailableView {
                        Label("TV ao vivo indisponível", systemImage: "wifi.exclamationmark")
                    } description: {
                        Text(error)
                    } actions: {
                        Button("Tentar novamente") {
                            Task { await model.loadLiveTV() }
                        }
                        .buttonStyle(.borderedProminent)
                    }
                    .padding(.horizontal)
                }
                Picker("TV ao vivo", selection: $section) {
                    Text("Canais").tag(0)
                    Text("Guia").tag(1)
                    Text("Gravações").tag(2)
                }
                .pickerStyle(.segmented)
                .padding(.horizontal)
                switch section {
                case 1:
                    if model.livePrograms.isEmpty {
                        ContentUnavailableView("Guia", systemImage: "list.rectangle", description: Text("Nenhum programa encontrado nas próximas 24 horas."))
                            .padding(.top, 40)
                    } else {
                        LazyVStack(spacing: 10) {
                            ForEach(model.livePrograms) { program in
                                LiveProgramRow(program: program, model: model)
                            }
                        }
                        .padding()
                    }
                case 2:
                    if model.liveRecordings.isEmpty {
                        ContentUnavailableView("Gravações", systemImage: "recordingtape", description: Text("Nenhuma gravação disponível."))
                            .padding(.top, 40)
                    } else {
                        LazyVGrid(columns: columns, spacing: 18) {
                            ForEach(model.liveRecordings) { recording in
                                NavigationLink(value: recording) { MediaCard(item: recording, model: model) }
                            }
                        }
                        .padding()
                    }
                default:
                    if model.liveChannels.isEmpty {
                        ContentUnavailableView("TV ao vivo", systemImage: "tv", description: Text("Nenhum canal ao vivo disponível neste servidor."))
                            .padding(.top, 40)
                    } else {
                        LazyVGrid(columns: columns, spacing: 18) {
                            ForEach(model.liveChannels) { channel in
                                NavigationLink(value: channel) { MediaCard(item: channel, model: model) }
                            }
                        }
                        .padding()
                    }
                }
            }
        }
        .navigationTitle("TV ao vivo")
        .refreshable { await model.loadLiveTV() }
        .task { await model.loadLiveTV() }
        .navigationDestination(for: MediaItem.self) { item in ItemDetailView(item: item, model: model) }
    }
}

private struct LiveProgramRow: View {
    let program: MediaItem
    let model: AppModel

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(program.name).font(.headline)
            if let channel = program.channelName { Text(channel).font(.subheadline).foregroundStyle(.secondary) }
            if let start = LiveTVDateFormatting.recordingStartLabel(program.startDate),
               let end = LiveTVDateFormatting.recordingStartLabel(program.endDate) {
                Text("\(start) – \(end)").font(.caption).foregroundStyle(.secondary)
            }
            HStack {
                NavigationLink(value: program) {
                    Label("Detalhes", systemImage: "info.circle")
                }
                Spacer()
                if model.scheduledLiveProgramIDs.contains(program.id) {
                    Label("Agendado", systemImage: "checkmark.circle.fill").foregroundStyle(.green)
                } else if model.schedulingLiveProgramIDs.contains(program.id) {
                    ProgressView()
                        .accessibilityLabel("Agendando gravação")
                } else {
                    Button {
                        Task { await model.scheduleLiveProgram(program) }
                    } label: {
                        Label("Gravar", systemImage: "record.circle")
                    }
                }
            }
            .font(.subheadline)
        }
        .padding()
        .background(.thinMaterial, in: .rect(cornerRadius: 12))
    }
}

struct SearchView: View {
    let model: AppModel
    @State private var query = ""
    @State private var filter: SearchFilter = .all
    @StateObject private var voiceSearch = IOSVoiceSearchController()

    private var columns: [GridItem] {
        [GridItem(.adaptive(minimum: model.compactGrid ? 104 : 135), spacing: 12)]
    }

    var body: some View {
        ScrollView {
            if let searchError = model.searchError, !model.isSearching {
                VStack(spacing: 12) {
                    ContentUnavailableView("Busca indisponível", systemImage: "wifi.exclamationmark", description: Text(searchError))
                    Button("Tentar novamente") {
                        Task { await model.search(query, filter: filter) }
                    }
                    .buttonStyle(.borderedProminent)
                }
                .frame(maxWidth: .infinity)
                .padding(.top, 60)
            } else if model.isSearching {
                ProgressView().frame(maxWidth: .infinity).padding(.top, 36)
            } else if query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && !model.searchHistory.isEmpty {
                VStack(alignment: .leading, spacing: 0) {
                    HStack {
                        Text("Buscas recentes").font(.headline)
                        Spacer()
                        Button("Limpar") { model.clearSearchHistory() }
                            .font(.subheadline)
                    }
                    .padding(.horizontal)
                    ForEach(model.searchHistory, id: \.self) { term in
                        HStack {
                            Button {
                                query = term
                            } label: {
                                Label(term, systemImage: "clock.arrow.circlepath")
                                    .frame(maxWidth: .infinity, alignment: .leading)
                            }
                            .buttonStyle(.plain)
                            Button {
                                model.removeSearchHistory(term)
                            } label: {
                                Image(systemName: "xmark")
                                    .foregroundStyle(.secondary)
                            }
                            .buttonStyle(.plain)
                            .accessibilityLabel("Remover busca \(term)")
                        }
                        .padding(.horizontal)
                        .padding(.vertical, 10)
                    }
                }
            } else if !model.searchHints.isEmpty && model.searchItems.isEmpty {
                LazyVStack(alignment: .leading, spacing: 0) {
                    ForEach(model.searchHints) { hint in
                        Button {
                            query = hint.name
                        } label: {
                            HStack {
                                Image(systemName: "magnifyingglass")
                                    .foregroundStyle(.secondary)
                                VStack(alignment: .leading) {
                                    Text(hint.name)
                                    if let series = hint.series {
                                        Text(series).font(.caption).foregroundStyle(.secondary)
                                    }
                                }
                                Spacer()
                            }
                            .padding(.vertical, 10)
                        }
                        .buttonStyle(.plain)
                    }
                }
                .padding(.horizontal)
            } else if model.searchItems.isEmpty {
                ContentUnavailableView("Buscar", systemImage: "magnifyingglass", description: Text("Digite o nome de um filme, série, música ou pessoa."))
                    .padding(.top, 60)
            } else {
                LazyVGrid(columns: columns, spacing: 18) {
                    ForEach(model.searchItems) { item in
                        NavigationLink(value: item) { MediaCard(item: item, model: model) }
                    }
                }
                .padding()
                if let total = model.searchTotalMatching, total > model.searchItems.count {
                    Text("Mostrando \(model.searchItems.count) de \(total) resultados.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                        .frame(maxWidth: .infinity)
                        .padding(.horizontal)
                }
            }
        }
        .navigationTitle("Buscar")
        .searchable(text: $query, prompt: "Filmes, séries, músicas e pessoas")
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button { voiceSearch.toggle() } label: {
                    Image(systemName: voiceSearch.isListening ? "waveform.circle.fill" : "mic")
                }
                .accessibilityLabel(voiceSearch.isListening ? "Parar busca por voz" : "Buscar por voz")
                .symbolEffect(.pulse, isActive: voiceSearch.isListening)
            }
        }
        .onChange(of: voiceSearch.transcript) { _, value in
            guard !value.isEmpty else { return }
            query = value
        }
        .alert("Busca por voz", isPresented: Binding(
            get: { voiceSearch.errorMessage != nil },
            set: { if !$0 { voiceSearch.errorMessage = nil } }
        )) {
            Button("OK") { voiceSearch.errorMessage = nil }
        } message: {
            Text(voiceSearch.errorMessage ?? "Não foi possível iniciar a busca por voz.")
        }
        .onDisappear { voiceSearch.stopListening() }
        .safeAreaInset(edge: .top) {
            Picker("Tipo", selection: $filter) {
                ForEach(SearchFilter.allCases) { value in
                    Text(value.title).tag(value)
                }
            }
            .pickerStyle(.segmented)
            .padding(.horizontal)
            .background(.bar)
        }
        .task(id: "\(query)|\(filter.rawValue)") {
            try? await Task.sleep(for: .milliseconds(350))
            guard !Task.isCancelled else { return }
            await model.search(query, filter: filter)
        }
        .navigationDestination(for: MediaItem.self) { item in ItemDetailView(item: item, model: model) }
    }
}

@MainActor
private final class IOSVoiceSearchController: NSObject, ObservableObject {
    @Published var transcript = ""
    @Published var isListening = false
    @Published var errorMessage: String?

    private let recognizer = SFSpeechRecognizer(locale: Locale(identifier: "pt-BR"))
    private let audioEngine = AVAudioEngine()
    private var recognitionRequest: SFSpeechAudioBufferRecognitionRequest?
    private var recognitionTask: SFSpeechRecognitionTask?

    func toggle() {
        if isListening { stop() } else { requestPermissionsAndStart() }
    }

    func stopListening() { stop() }

    private func requestPermissionsAndStart() {
        errorMessage = nil
        SFSpeechRecognizer.requestAuthorization { status in
            Task { @MainActor [weak self] in
                guard let self else { return }
                guard status == .authorized else {
                    self.errorMessage = "Permita o reconhecimento de fala nos Ajustes para usar a busca por voz."
                    return
                }
                AVAudioSession.sharedInstance().requestRecordPermission { granted in
                    Task { @MainActor [weak self] in
                        guard let self else { return }
                        guard granted else {
                            self.errorMessage = "Permita o microfone nos Ajustes para usar a busca por voz."
                            return
                        }
                        self.start()
                    }
                }
            }
        }
    }

    private func start() {
        guard let recognizer, recognizer.isAvailable else {
            errorMessage = "O reconhecimento de fala não está disponível agora."
            return
        }
        stop()
        transcript = ""
        let request = SFSpeechAudioBufferRecognitionRequest()
        request.shouldReportPartialResults = true
        recognitionRequest = request
        recognitionTask = recognizer.recognitionTask(with: request) { [weak self] result, error in
            let recognizedText = result?.bestTranscription.formattedString ?? ""
            let isFinal = result?.isFinal == true
            let didFail = error != nil
            Task { @MainActor [weak self] in
                guard let self else { return }
                if !recognizedText.isEmpty { self.transcript = recognizedText }
                if isFinal || didFail { self.stop() }
            }
        }

        let inputNode = audioEngine.inputNode
        inputNode.installTap(onBus: 0, bufferSize: 1_024, format: inputNode.outputFormat(forBus: 0)) { [weak request] buffer, _ in
            request?.append(buffer)
        }
        do {
            let session = AVAudioSession.sharedInstance()
            try session.setCategory(.record, mode: .measurement, options: [.duckOthers])
            try session.setActive(true, options: .notifyOthersOnDeactivation)
            audioEngine.prepare()
            try audioEngine.start()
            isListening = true
        } catch {
            errorMessage = "Não foi possível acessar o microfone."
            stop()
        }
    }

    private func stop() {
        guard isListening || recognitionTask != nil else { return }
        audioEngine.stop()
        audioEngine.inputNode.removeTap(onBus: 0)
        recognitionRequest?.endAudio()
        recognitionTask?.cancel()
        recognitionRequest = nil
        recognitionTask = nil
        isListening = false
        try? AVAudioSession.sharedInstance().setActive(false, options: .notifyOthersOnDeactivation)
    }
}

struct HomeDashboard: View {
    let model: AppModel

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 28) {
                if model.isHomeLoading && model.heroItem == nil && homeItemsAreEmpty {
                    ProgressView("Carregando conteúdo…")
                        .frame(maxWidth: .infinity, minHeight: 260)
                }
                if let error = model.homeError {
                    ContentUnavailableView {
                        Label("Não foi possível carregar o conteúdo", systemImage: "wifi.exclamationmark")
                    } description: {
                        Text(error)
                    } actions: {
                        Button("Tentar novamente") {
                            Task { await model.loadHome() }
                        }
                        .buttonStyle(.borderedProminent)
                    }
                    .padding(.horizontal)
                }
                if let heroItem = model.heroItem {
                    HeroBanner(item: heroItem, model: model)
                }
                if !model.favoriteItems.isEmpty { MediaRail(title: "Minha Lista", items: model.favoriteItems, model: model) }
                if !model.resumeItems.isEmpty { MediaRail(title: "Continuar assistindo", items: model.resumeItems, model: model) }
                if !model.nextUpItems.isEmpty { MediaRail(title: "Próximo episódio", items: model.nextUpItems, model: model) }
                if !model.latestItems.isEmpty { MediaRail(title: "Adicionados recentemente", items: model.latestItems, model: model) }
                if !model.popularItems.isEmpty { MediaRail(title: "Mais populares", items: model.popularItems, model: model) }
                if !model.movieItems.isEmpty { MediaRail(title: "Filmes", items: model.movieItems, model: model) }
                if !model.seriesItems.isEmpty { MediaRail(title: "Séries", items: model.seriesItems, model: model) }
                if !model.liveChannels.isEmpty { MediaRail(title: "TV ao vivo", items: model.liveChannels, model: model) }
                if !model.isHomeLoading && model.homeError == nil && model.heroItem == nil && homeItemsAreEmpty {
                    ContentUnavailableView("Início", systemImage: "film", description: Text("Nenhum título disponível agora."))
                }
            }
            .padding(.vertical)
        }
        .navigationTitle("Início")
    }

    private var homeItemsAreEmpty: Bool {
        model.favoriteItems.isEmpty && model.resumeItems.isEmpty && model.nextUpItems.isEmpty &&
        model.latestItems.isEmpty && model.popularItems.isEmpty && model.movieItems.isEmpty &&
        model.seriesItems.isEmpty && model.liveChannels.isEmpty
    }
}

private struct HeroBanner: View {
    let item: MediaItem
    let model: AppModel

    var body: some View {
        ZStack(alignment: .bottomLeading) {
            Group {
                if let imageURL {
                    AuthenticatedArtwork(url: imageURL, token: model.session?.accessToken)
                } else {
                    Color.gray.opacity(0.25)
                }
            }
            .frame(maxWidth: .infinity, minHeight: 260, maxHeight: 340)
            .clipped()
            LinearGradient(
                colors: [.clear, .black.opacity(0.9)],
                startPoint: .center,
                endPoint: .bottom
            )
            VStack(alignment: .leading, spacing: 8) {
                Text(item.name)
                    .font(.title.bold())
                    .lineLimit(2)
                if let overview = item.overview, !overview.isEmpty {
                    Text(overview)
                        .font(.caption)
                        .lineLimit(2)
                }
                NavigationLink(value: item) {
                    Label("Mais informações", systemImage: "info.circle.fill")
                }
                .buttonStyle(.borderedProminent)
            }
            .foregroundStyle(.white)
            .padding(18)
        }
        .clipShape(.rect(cornerRadius: 16))
        .padding(.horizontal)
        .task { imageURL = await model.imageURL(for: item) }
    }

    @State private var imageURL: URL?
}

struct MediaRail: View {
    let title: String
    let items: [MediaItem]
    let model: AppModel

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(title).font(.title3.bold()).padding(.horizontal)
            if items.isEmpty {
                Text("Nada para mostrar").foregroundStyle(.secondary).padding(.horizontal)
            } else {
                ScrollView(.horizontal) {
                    LazyHStack(spacing: 14) {
                        ForEach(items) { item in
                            NavigationLink(value: item) { MediaCard(item: item, model: model) }
                                .frame(width: model.compactGrid ? 112 : 140)
                        }
                    }
                    .padding(.horizontal)
                }
                .scrollIndicators(.hidden)
            }
        }
    }
}

struct MediaShelf: View {
    let title: String
    let items: [MediaItem]
    let model: AppModel

    private var columns: [GridItem] {
        [GridItem(.adaptive(minimum: model.compactGrid ? 104 : 135), spacing: 12)]
    }

    var body: some View {
        ScrollView {
            if model.libraryListLayout {
                LazyVStack(spacing: 10) {
                    ForEach(items) { item in
                        NavigationLink(value: item) {
                            HStack(spacing: 12) {
                                Image(systemName: "film")
                                    .frame(width: 32, height: 32)
                                    .foregroundStyle(.red)
                                Text(item.name)
                                    .font(.headline)
                                    .multilineTextAlignment(.leading)
                                Spacer()
                                Image(systemName: "chevron.right")
                                    .foregroundStyle(.secondary)
                            }
                            .padding(.horizontal, 14)
                            .padding(.vertical, 12)
                            .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 12))
                        }
                    }
                }
            } else {
                LazyVGrid(columns: columns, spacing: 18) {
                    ForEach(items) { item in
                        NavigationLink(value: item) { MediaCard(item: item, model: model) }
                    }
                }
            }
            .padding()
            if items.isEmpty {
                ContentUnavailableView(title, systemImage: "film", description: Text("Nenhum título disponível agora."))
                    .padding(.top, 40)
            }
        }
        .navigationTitle(title)
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button {
                    model.libraryListLayout.toggle()
                } label: {
                    Label(
                        model.libraryListLayout ? "Usar grade" : "Usar lista",
                        systemImage: model.libraryListLayout ? "square.grid.2x2" : "list.bullet"
                    )
                }
                .accessibilityLabel(model.libraryListLayout ? "Alternar para grade" : "Alternar para lista")
            }
        }
    }
}

struct LibraryView: View {
    let model: AppModel
    @State private var showingFilters = false

    var body: some View {
        Group {
            if let selected = model.selectedLibrary {
                if model.isLibraryLoading && model.libraryItems.isEmpty {
                    ProgressView("Carregando biblioteca…")
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else if let error = model.libraryError, model.libraryItems.isEmpty {
                    ContentUnavailableView {
                        Label("Biblioteca indisponível", systemImage: "wifi.exclamationmark")
                    } description: {
                        Text(error)
                    } actions: {
                        Button("Tentar novamente") {
                            Task { await model.openLibrary(selected) }
                        }
                        .buttonStyle(.borderedProminent)
                    }
                } else {
                    VStack(spacing: 10) {
                        if let error = model.libraryError {
                            Label(error, systemImage: "exclamationmark.triangle")
                                .font(.footnote)
                                .foregroundStyle(.secondary)
                                .multilineTextAlignment(.center)
                                .padding(.horizontal)
                            Button("Tentar novamente") {
                                Task { await model.openLibrary(selected) }
                            }
                            .buttonStyle(.bordered)
                        }
                        MediaShelf(title: selected.name, items: model.libraryItems, model: model)
                    }
                }
            } else {
                if model.isLibrariesLoading && model.libraries.isEmpty {
                    ProgressView("Carregando bibliotecas…")
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else if let error = model.librariesError, model.libraries.isEmpty {
                    ContentUnavailableView {
                        Label("Bibliotecas indisponíveis", systemImage: "wifi.exclamationmark")
                    } description: {
                        Text(error)
                    } actions: {
                        Button("Tentar novamente") {
                            Task { await model.loadLibraries() }
                        }
                        .buttonStyle(.borderedProminent)
                    }
                } else {
                    VStack(spacing: 8) {
                        if let error = model.librariesError {
                            Label(error, systemImage: "exclamationmark.triangle")
                                .font(.footnote)
                                .foregroundStyle(.secondary)
                                .multilineTextAlignment(.center)
                                .padding(.horizontal)
                            Button("Tentar novamente") {
                                Task { await model.loadLibraries() }
                            }
                            .buttonStyle(.bordered)
                        }
                        List(model.libraries) { library in
                            Button {
                                Task { await model.openLibrary(library) }
                            } label: {
                                Label(library.name, systemImage: "rectangle.stack.fill")
                            }
                            .accessibilityHint("Abre esta biblioteca")
                        }
                        .overlay {
                            if model.libraries.isEmpty {
                                ContentUnavailableView("Bibliotecas", systemImage: "rectangle.stack", description: Text("Nenhuma biblioteca disponível."))
                            }
                        }
                    }
                }
            }
        }
        .navigationTitle(model.selectedLibrary?.name ?? "Biblioteca")
        .toolbar {
            if model.selectedLibrary != nil {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        showingFilters = true
                    } label: {
                        Label("Filtros", systemImage: "line.3.horizontal.decrease.circle")
                    }
                    .accessibilityLabel("Filtrar biblioteca")
                }
            }
        }
        .sheet(isPresented: $showingFilters, onDismiss: reloadFilteredLibrary) {
            LibraryFilterSheet(model: model)
        }
        .navigationDestination(for: MediaItem.self) { item in ItemDetailView(item: item, model: model) }
    }

    private func reloadFilteredLibrary() {
        guard model.selectedLibrary != nil else { return }
        Task { await model.reloadSelectedLibrary() }
    }
}

private struct LibraryFilterSheet: View {
    let model: AppModel
    @Environment(\.dismiss) private var dismiss
    @State private var genre: String
    @State private var year: String
    @State private var rating: String
    @State private var playedFilter: LibraryPlayedFilter
    @State private var favoritesOnly: Bool

    init(model: AppModel) {
        self.model = model
        _genre = State(initialValue: model.libraryGenreFilter)
        _year = State(initialValue: model.libraryYearFilter)
        _rating = State(initialValue: model.libraryRatingFilter)
        _playedFilter = State(initialValue: model.libraryPlayedFilter)
        _favoritesOnly = State(initialValue: model.libraryFavoritesOnly)
    }

    var body: some View {
        NavigationStack {
            Form {
                Section("Catálogo") {
                    TextField("Gênero (ex.: Drama)", text: $genre)
                    TextField("Ano (ex.: 2024)", text: $year)
                        .keyboardType(.numberPad)
                    TextField("Classificação (ex.: 16, PG-13)", text: $rating)
                        .textInputAutocapitalization(.characters)
                    Picker("Status", selection: $playedFilter) {
                        ForEach(LibraryPlayedFilter.allCases) { filter in
                            Text(filter.title).tag(filter)
                        }
                    }
                    Toggle("Somente Minha Lista", isOn: $favoritesOnly)
                }
            }
            .navigationTitle("Filtros")
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Concluir") {
                        model.libraryGenreFilter = genre
                        model.libraryYearFilter = year
                        model.libraryRatingFilter = rating
                        model.libraryPlayedFilter = playedFilter
                        model.libraryFavoritesOnly = favoritesOnly
                        dismiss()
                    }
                }
            }
        }
    }
}

struct ItemDetailView: View {
    let item: MediaItem
    let model: AppModel
    @State private var detailItem: MediaItem?
    @State private var playbackURL: URL?
    @State private var playerItem: MediaItem?
    @State private var nextEpisode: MediaItem?
    @State private var showPlayer = false
    @State private var showPlaylistSheet = false
    @State private var newPlaylistName = ""
    @State private var lyricsTrack: MediaItem?

    var body: some View {
        let displayedItem = detailItem ?? item
        let hasResumePosition = displayedItem.playbackPositionTicks > 0
            || model.offlinePlaybackPosition(for: displayedItem.id) > 0
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                MediaCard(item: displayedItem, model: model)
                    .frame(maxWidth: 220)
                    .frame(maxWidth: .infinity)
                Text(displayedItem.name).font(.title.bold())
                if let metadata = detailMetadata(for: displayedItem), !metadata.isEmpty {
                    Text(metadata)
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }
                if !displayedItem.genres.isEmpty {
                    Text(displayedItem.genres.joined(separator: " • "))
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                }
                if let overview = displayedItem.overview, !overview.isEmpty { Text(overview).font(.body) }
                if !displayedItem.people.isEmpty {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Elenco e equipe").font(.headline)
                        ForEach(displayedItem.people, id: \.self) { person in
                            HStack {
                                Text(person.name)
                                if let role = person.role, !role.isEmpty {
                                    Text("• \(role)").foregroundStyle(.secondary)
                                }
                            }
                            .font(.subheadline)
                        }
                    }
                }
                if displayedItem.type == "MusicAlbum" {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Faixas").font(.headline)
                        if model.albumTracks.isEmpty {
                            Text("Nenhuma faixa encontrada.")
                                .font(.subheadline)
                                .foregroundStyle(.secondary)
                        } else {
                            ForEach(Array(model.albumTracks.enumerated()), id: \.element.id) { index, track in
                                HStack(spacing: 12) {
                                    Button {
                                        Task {
                                            playerItem = track
                                            if let localURL = model.localURL(for: track) {
                                                playbackURL = localURL
                                            } else {
                                                playbackURL = await model.playbackURL(for: track)
                                            }
                                            nextEpisode = nil
                                            showPlayer = playbackURL != nil
                                        }
                                    } label: {
                                        HStack(spacing: 12) {
                                        Text("\(index + 1)")
                                            .foregroundStyle(.secondary)
                                            .frame(width: 28, alignment: .leading)
                                        VStack(alignment: .leading) {
                                            Text(track.name).font(.subheadline.weight(.medium))
                                            if let artist = track.overview, !artist.isEmpty {
                                                Text(artist).font(.caption).foregroundStyle(.secondary).lineLimit(1)
                                            }
                                        }
                                            Spacer()
                                            Image(systemName: "play.circle.fill")
                                        }
                                    }
                                    .buttonStyle(.plain)
                                    Button {
                                        lyricsTrack = track
                                    } label: {
                                        Image(systemName: "quote.bubble")
                                            .accessibilityLabel("Mostrar letra")
                                    }
                                    .buttonStyle(.borderless)
                                }
                            }
                        }
                    }
                }
                if !model.similarItems.isEmpty {
                    MediaRail(title: "Semelhantes", items: model.similarItems, model: model)
                }
                if !model.specialFeatures.isEmpty {
                    MediaRail(title: "Recursos especiais", items: model.specialFeatures, model: model)
                }
                if !model.seriesSeasons.isEmpty {
                    VStack(alignment: .leading, spacing: 10) {
                        Text("Temporadas").font(.headline)
                        Picker("Temporada", selection: Binding(
                            get: { model.selectedSeasonIndex },
                            set: { index in Task { await model.selectSeason(index) } }
                        )) {
                            ForEach(Array(model.seriesSeasons.enumerated()), id: \.element.id) { index, season in
                                Text(season.name).tag(index)
                            }
                        }
                        .pickerStyle(.menu)
                        ForEach(model.seriesEpisodes) { episode in
                            NavigationLink(value: episode) {
                                HStack {
                                    Text(episode.indexNumber.map { String($0) } ?? "•")
                                        .foregroundStyle(.secondary)
                                        .frame(width: 28)
                                    VStack(alignment: .leading) {
                                        Text(episode.name).font(.subheadline.weight(.medium))
                                        if let overview = episode.overview, !overview.isEmpty {
                                            Text(overview).font(.caption).foregroundStyle(.secondary).lineLimit(2)
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                Button(hasResumePosition ? "Continuar" : "Assistir", systemImage: "play.fill") {
                    guard model.profile?.canPlayMedia != false else { return }
                    Task {
                        nextEpisode = await model.nextEpisode(after: displayedItem)
                        playerItem = displayedItem
                        if let localURL = model.localURL(for: displayedItem) {
                            playbackURL = localURL
                        } else {
                            playbackURL = await model.playbackURL(for: displayedItem)
                        }
                        showPlayer = playbackURL != nil
                    }
                }
                .buttonStyle(.borderedProminent)
                .disabled(model.profile?.canPlayMedia == false || (displayedItem.mediaSources.isEmpty && model.localURL(for: displayedItem) == nil))
                if model.profile?.canPlayMedia == false {
                    Label("Seu perfil não tem permissão para reproduzir mídia.", systemImage: "lock.fill")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                }
                ShareLink(item: model.shareText(for: displayedItem), subject: Text("Compartilhar título")) {
                    Label("Compartilhar", systemImage: "square.and.arrow.up")
                }
                if model.localURL(for: displayedItem) != nil {
                    Label("Disponível offline", systemImage: "checkmark.circle.fill")
                        .foregroundStyle(.green)
                } else {
                    Button("Baixar para assistir offline", systemImage: "arrow.down.circle") {
                        Task { await model.enqueueOfflineDownload(for: displayedItem) }
                    }
                    .disabled(model.profile?.canDownload == false)
                }
                Button(displayedItem.isFavorite ? "Remover da Minha Lista" : "Adicionar à Minha Lista", systemImage: displayedItem.isFavorite ? "heart.slash" : "heart") {
                    Task { detailItem = await model.toggleFavorite(for: displayedItem) }
                }
                .accessibilityValue(displayedItem.isFavorite ? "Adicionado" : "Não adicionado")
                Button(displayedItem.isPlayed ? "Marcar como não assistido" : "Marcar como assistido", systemImage: displayedItem.isPlayed ? "eye.slash" : "checkmark.circle") {
                    Task { detailItem = await model.togglePlayed(for: displayedItem) }
                }
                .accessibilityValue(displayedItem.isPlayed ? "Assistido" : "Não assistido")
                Button("Adicionar à playlist", systemImage: "text.badge.plus") {
                    showPlaylistSheet = true
                    Task { await model.loadPlaylists() }
                }
            }
            .padding()
        }
        .navigationTitle(displayedItem.name)
        .navigationBarTitleDisplayMode(.inline)
        .task {
            let loaded = await model.loadDetail(for: item)
            detailItem = loaded
            await model.loadSeriesContext(for: loaded ?? item)
        }
        .fullScreenCover(isPresented: $showPlayer) {
            if let playbackURL, let playerItem {
                let isLocal = model.localURL(for: playerItem) != nil
                let localPosition = model.offlinePlaybackPosition(for: playerItem.id)
                let startTimeTicks = PlaybackResumePolicy.initialPositionTicks(
                    server: playerItem.playbackPositionTicks,
                    local: localPosition,
                    isLocal: isLocal
                )
                PlayerView(
                    url: playbackURL,
                    model: model,
                    itemID: playerItem.id,
                    mediaSourceID: playerItem.mediaSources.first?.id,
                    availableQualityOptions: playerItem.mediaSources.first?.qualityLabels ?? [],
                    startTimeTicks: startTimeTicks,
                    isLocal: isLocal,
                    nextEpisode: nextEpisode,
                    onPlayNext: {
                        Task {
                            guard let target = nextEpisode else { return }
                            let targetURL = model.localURL(for: target) ?? await model.playbackURL(for: target)
                            guard let targetURL else { return }
                            playerItem = target
                            nextEpisode = await model.nextEpisode(after: target)
                            playbackURL = targetURL
                        }
                    }
                )
                .id(playerItem.id)
            }
        }
        .sheet(item: $lyricsTrack) { track in
            LyricsView(track: track, model: model)
        }
        .sheet(isPresented: $showPlaylistSheet) {
            PlaylistPickerView(model: model, item: displayedItem, newPlaylistName: $newPlaylistName)
        }
    }

    private func detailMetadata(for item: MediaItem) -> String? {
        var values: [String] = []
        if let year = item.productionYear { values.append(String(year)) }
        if let rating = item.officialRating, !rating.isEmpty { values.append(rating) }
        if let score = item.communityRating { values.append(String(format: "%.1f/10", score)) }
        if let ticks = item.runtimeTicks, ticks > 0 {
            let minutes = Int((Double(ticks) / 10_000_000) / 60)
            values.append("\(minutes) min")
        }
        return values.isEmpty ? nil : values.joined(separator: "  •  ")
    }
}

private struct LyricsView: View {
    let track: MediaItem
    let model: AppModel
    @Environment(\.dismiss) private var dismiss
    @State private var lines: [LyricLine] = []
    @State private var isLoading = true

    var body: some View {
        NavigationStack {
            Group {
                if isLoading {
                    ProgressView("Carregando letra…")
                } else if lines.isEmpty {
                    ContentUnavailableView("Letra indisponível", systemImage: "quote.bubble", description: Text("O servidor não forneceu letras para esta faixa."))
                } else {
                    ScrollView {
                        VStack(alignment: .leading, spacing: 14) {
                            ForEach(Array(lines.enumerated()), id: \.offset) { _, line in
                                Text(line.text)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                            }
                        }
                        .padding()
                    }
                }
            }
            .navigationTitle(track.name)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Fechar") { dismiss() }
                }
            }
            .task {
                lines = await model.lyrics(for: track)
                isLoading = false
            }
        }
    }
}

private struct PlaylistPickerView: View {
    let model: AppModel
    let item: MediaItem
    @Binding var newPlaylistName: String
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            List {
                if model.isPlaylistLoading && model.playlists.isEmpty {
                    ProgressView().frame(maxWidth: .infinity)
                } else if model.playlists.isEmpty {
                    Text("Nenhuma playlist criada ainda.").foregroundStyle(.secondary)
                } else {
                    Section("Playlists") {
                        ForEach(model.playlists) { playlist in
                            Button(playlist.name) {
                                Task {
                                    await model.addToPlaylist(playlist, item: item)
                                    dismiss()
                                }
                            }
                        }
                    }
                }
                Section("Nova playlist") {
                    TextField("Nome", text: $newPlaylistName)
                    Button("Criar e adicionar") {
                        let name = newPlaylistName
                        newPlaylistName = ""
                        Task {
                            await model.createPlaylist(name: name, item: item)
                            dismiss()
                        }
                    }
                    .disabled(newPlaylistName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
            }
            .navigationTitle("Adicionar à playlist")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Fechar") { dismiss() }
                }
            }
        }
        .navigationTitle(model.selectedLibrary?.name ?? "Bibliotecas")
        .toolbar {
            if model.selectedLibrary != nil {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Bibliotecas", systemImage: "chevron.backward") {
                        model.closeLibrary()
                    }
                }
            }
        }
    }
}

private enum DownloadStatusFilter: String, CaseIterable, Identifiable {
    case all, active, completed, failed

    var id: String { rawValue }

    var title: String {
        switch self {
        case .all: return "Todos"
        case .active: return "Ativos"
        case .completed: return "Concluídos"
        case .failed: return "Falhos"
        }
    }

    func matches(_ entry: OfflineDownload) -> Bool {
        switch self {
        case .all: return true
        case .active: return entry.state == .queued || entry.state == .downloading || entry.state == .paused
        case .completed: return entry.state == .completed
        case .failed: return entry.state == .failed
        }
    }
}

struct DownloadsView: View {
    let model: AppModel
    @State private var searchQuery = ""
    @State private var statusFilter: DownloadStatusFilter = .all
    @State private var offlinePlayerItem: MediaItem?
    @State private var showingStorageSummary = false
    @State private var showingClearCompleted = false
    @State private var showingClearFailed = false

    private var filteredDownloads: [OfflineDownload] {
        let query = searchQuery.trimmingCharacters(in: .whitespacesAndNewlines)
        return model.offlineDownloads.filter { entry in
            statusFilter.matches(entry) && (query.isEmpty || entry.title.localizedCaseInsensitiveContains(query))
        }
    }

    var body: some View {
        List {
            if model.offlineDownloads.isEmpty {
                ContentUnavailableView("Downloads offline", systemImage: "arrow.down.circle", description: Text("Baixe um título pela tela de detalhes para assisti-lo sem conexão."))
            } else {
                Picker("Filtro", selection: $statusFilter) {
                    ForEach(DownloadStatusFilter.allCases) { filter in
                        Text(filter.title).tag(filter)
                    }
                }
                .pickerStyle(.segmented)

                if filteredDownloads.isEmpty {
                    ContentUnavailableView("Nenhum download encontrado", systemImage: "line.3.horizontal.decrease.circle", description: Text("Ajuste a busca ou o filtro de estado."))
                }

                    ForEach(filteredDownloads) { entry in
                        VStack(alignment: .leading, spacing: 8) {
                            HStack {
                            if let artworkURL = entry.artworkURL.flatMap({ URL(string: $0) }) {
                                AuthenticatedArtwork(url: artworkURL, token: model.session?.accessToken)
                                    .frame(width: 48, height: 72)
                                    .aspectRatio(2 / 3, contentMode: .fit)
                                    .clipShape(.rect(cornerRadius: 6))
                                    .accessibilityHidden(true)
                            } else {
                                RoundedRectangle(cornerRadius: 6)
                                    .fill(.gray.opacity(0.25))
                                    .frame(width: 48, height: 72)
                                    .overlay { Image(systemName: "film") }
                                    .accessibilityHidden(true)
                            }
                            VStack(alignment: .leading, spacing: 4) {
                                Text(entry.title).font(.headline)
                                Text(statusText(entry))
                                    .font(.caption)
                                    .foregroundStyle(entry.state == .failed ? .red : .secondary)
                            }
                            Spacer()
                        }
                        if entry.state == .completed {
                            Button {
                                let item = MediaItem(id: entry.itemID, name: entry.title)
                                guard model.localURL(for: item) != nil else { return }
                                offlinePlayerItem = item
                            } label: {
                                Label("Reproduzir", systemImage: "play.fill")
                            }
                            .buttonStyle(.borderedProminent)
                            .accessibilityHint("Abre o player usando o arquivo armazenado neste dispositivo")
                        }
                        if entry.state == .downloading || entry.state == .queued || entry.state == .paused {
                            ProgressView(value: Double(entry.percent), total: 100)
                            if entry.bytesDownloaded > 0 {
                                Text(downloadSizeText(entry))
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                            }
                        }
                        if let error = entry.error {
                            Text(error).font(.caption).foregroundStyle(.red)
                        }
                        HStack {
                            if entry.state == .downloading {
                                Button("Pausar") { model.pauseOfflineDownload(entry) }
                            } else if entry.state == .paused {
                                Button("Retomar") { model.resumeOfflineDownload(entry) }
                            } else if entry.state == .failed {
                                Button("Tentar novamente") { model.retryOfflineDownload(entry) }
                            }
                            Spacer()
                            Button("Remover", role: .destructive) { model.removeOfflineDownload(entry) }
                        }
                        .font(.subheadline)
                    }
                    .padding(.vertical, 4)
                }
            }
        }
        .navigationTitle("Downloads offline")
        .searchable(text: $searchQuery, prompt: "Buscar downloads")
        .fullScreenCover(item: $offlinePlayerItem) { item in
            if let url = model.localURL(for: item) {
                PlayerView(
                    url: url,
                    model: model,
                    itemID: item.id,
                    mediaSourceID: nil,
                    startTimeTicks: PlaybackResumePolicy.initialPositionTicks(
                        server: 0,
                        local: model.offlinePlaybackPosition(for: item.id),
                        isLocal: true
                    ),
                    isLocal: true,
                    nextEpisode: nil,
                    onPlayNext: {}
                )
                .id(item.id)
            } else {
                ContentUnavailableView(
                    "Arquivo indisponível",
                    systemImage: "exclamationmark.triangle",
                    description: Text("Este download não está mais armazenado no dispositivo.")
                )
            }
        }
        .toolbar {
            ToolbarItemGroup(placement: .topBarTrailing) {
                Button { showingStorageSummary = true } label: {
                    Label("Armazenamento offline", systemImage: "internaldrive")
                }
                if !model.offlineDownloads.isEmpty {
                    Button {
                        if model.offlineQueuePaused {
                            model.resumeAllOfflineDownloads()
                        } else {
                            model.pauseAllOfflineDownloads()
                        }
                    } label: {
                        Label(
                            model.offlineQueuePaused ? "Retomar fila" : "Pausar fila",
                            systemImage: model.offlineQueuePaused ? "play.fill" : "pause.fill"
                        )
                    }
                }
                if model.offlineDownloads.contains(where: { $0.state == .failed }) {
                    Button("Tentar falhos") { model.retryFailedOfflineDownloads() }
                }
                Menu {
                    Button("Limpar concluídos") { showingClearCompleted = true }
                    Button("Limpar falhos", role: .destructive) { showingClearFailed = true }
                } label: {
                    Label("Gerenciar downloads", systemImage: "ellipsis.circle")
                }
            }
        }
        .alert("Armazenamento offline", isPresented: $showingStorageSummary) {
            Button("OK", role: .cancel) {}
        } message: {
            Text(storageSummary)
        }
        .alert("Limpar concluídos?", isPresented: $showingClearCompleted) {
            Button("Cancelar", role: .cancel) {}
            Button("Limpar", role: .destructive) { model.removeCompletedOfflineDownloads() }
        } message: {
            Text("Os arquivos concluídos serão removidos. Downloads em andamento e falhas serão preservados.")
        }
        .alert("Limpar falhos?", isPresented: $showingClearFailed) {
            Button("Cancelar", role: .cancel) {}
            Button("Limpar", role: .destructive) { model.removeFailedOfflineDownloads() }
        } message: {
            Text("Os downloads que falharam serão removidos da fila offline.")
        }
    }

    private var storageSummary: String {
        let formatter = ByteCountFormatter()
        formatter.countStyle = .file
        let completed = model.offlineDownloads.filter { $0.state == .completed }.count
        let failed = model.offlineDownloads.filter { $0.state == .failed }.count
        return "\(completed) concluído(s), \(failed) falho(s) e \(formatter.string(fromByteCount: model.offlineDownloadedBytes)) armazenado(s)."
    }

    private func statusText(_ entry: OfflineDownload) -> String {
        switch entry.state {
        case .queued:
            if model.offlineQueuePaused { return "Fila pausada" }
            if model.wifiOnlyDownloads && model.isWiFiAvailable != true { return "Aguardando Wi-Fi" }
            return "Na fila"
        case .downloading: return "Baixando"
        case .paused: return "Pausado"
        case .completed: return "Concluído"
        case .failed: return "Falhou"
        }
    }

    private func downloadSizeText(_ entry: OfflineDownload) -> String {
        let formatter = ByteCountFormatter()
        formatter.countStyle = .file
        let current = formatter.string(fromByteCount: entry.bytesDownloaded)
        if let contentLength = entry.contentLength {
            return "\(current) de \(formatter.string(fromByteCount: contentLength))"
        }
        return current
    }
}

private enum IOSSleepTimerMode {
    case off
    case countdown
    case atMediaEnd
}

private struct NativePlayerView: UIViewControllerRepresentable {
    let player: AVPlayer
    let pictureInPictureEnabled: Bool
    let videoGravity: AVLayerVideoGravity

    func makeUIViewController(context: Context) -> AVPlayerViewController {
        let controller = AVPlayerViewController()
        try? AVAudioSession.sharedInstance().setCategory(.playback, mode: .moviePlayback, options: [.allowAirPlay])
        try? AVAudioSession.sharedInstance().setActive(true)
        player.allowsExternalPlayback = true
        player.usesExternalPlaybackWhileExternalScreenIsActive = true
        controller.player = player
        controller.showsPlaybackControls = true
        controller.videoGravity = videoGravity
        controller.allowsPictureInPicturePlayback = pictureInPictureEnabled
        controller.canStartPictureInPictureAutomaticallyFromInline = pictureInPictureEnabled
        controller.videoGravity = videoGravity
        controller.updatesNowPlayingInfoCenter = true
        return controller
    }

    func updateUIViewController(_ controller: AVPlayerViewController, context: Context) {
        controller.player = player
        player.allowsExternalPlayback = true
        player.usesExternalPlaybackWhileExternalScreenIsActive = true
        controller.allowsPictureInPicturePlayback = pictureInPictureEnabled
        controller.canStartPictureInPictureAutomaticallyFromInline = pictureInPictureEnabled
    }
}

private struct AirPlayRoutePicker: UIViewRepresentable {
    func makeUIView(context: Context) -> AVRoutePickerView {
        let picker = AVRoutePickerView()
        picker.tintColor = .white
        picker.activeTintColor = .systemBlue
        picker.prioritizesVideoDevices = true
        return picker
    }

    func updateUIView(_ view: AVRoutePickerView, context: Context) {
        view.tintColor = .white
        view.activeTintColor = .systemBlue
    }
}

struct PlayerView: View {
    let url: URL
    let model: AppModel
    let itemID: String
    let mediaSourceID: String?
    let availableQualityOptions: [String]
    let startTimeTicks: Int64
    let isLocal: Bool
    let nextEpisode: MediaItem?
    let onPlayNext: () -> Void
    @State private var player: AVPlayer
    @State private var mediaSegments: [MediaSegment] = []
    @State private var chapters: [Chapter] = []
    @State private var currentTime = 0.0
    @State private var sleepTimerMode = IOSSleepTimerMode.off
    @State private var sleepTimerEndDate: Date?
    @State private var audioOptions: [AVMediaSelectionOption] = []
    @State private var subtitleOptions: [AVMediaSelectionOption] = []
    @State private var selectedQuality = "Auto"
    @State private var selectedPlaybackRate = 1.0
    @State private var nextEpisodeCountdown: Int?
    @State private var nextEpisodeCountdownTask: Task<Void, Never>?

    @State private var showNextEpisodePrompt = false
    @State private var showPlaybackStats = false
    @State private var gestureAxis: GestureAxis?
    @State private var gestureStartTime = 0.0
    @State private var gestureStartBrightness = 0.5
    @State private var gestureHint: String?
    @State private var gestureHintTask: Task<Void, Never>?
    @State private var playbackError: String?
    @State private var playbackRetryAttempt = 0
    @State private var playbackRetryTask: Task<Void, Never>?
    @State private var playbackWaitingForNetwork = false
    @State private var didFinishPlayback = false
    @State private var didReportRemoteStopped = false
    @State private var wasPlayingBeforeAudioInterruption = false

    private enum GestureAxis {
        case horizontal
        case vertical
    }

    init(url: URL, model: AppModel, itemID: String, mediaSourceID: String?, availableQualityOptions: [String] = [], startTimeTicks: Int64 = 0, isLocal: Bool, nextEpisode: MediaItem?, onPlayNext: @escaping () -> Void) {
        self.url = url
        self.model = model
        self.itemID = itemID
        self.mediaSourceID = mediaSourceID
        self.availableQualityOptions = availableQualityOptions
        self.startTimeTicks = max(0, startTimeTicks)
        self.isLocal = isLocal
        self.nextEpisode = nextEpisode
        self.onPlayNext = onPlayNext
        _player = State(initialValue: AVPlayer(url: url))
    }

    var body: some View {
        NativePlayerView(
            player: player,
            pictureInPictureEnabled: model.pictureInPictureEnabled,
            videoGravity: model.videoAspectRatio.videoGravity
        )
            .simultaneousGesture(
                DragGesture(minimumDistance: 12)
                    .onChanged { value in
                        handleGestureDrag(value, width: UIScreen.main.bounds.width)
                    }
                    .onEnded { value in
                        finishGestureDrag(value, width: UIScreen.main.bounds.width)
                    }
            )
            .simultaneousGesture(
                SpatialTapGesture(count: 2)
                    .onEnded { value in
                        handleDoubleTap(at: value.location.x, width: UIScreen.main.bounds.width)
                    }
            )
            .overlay(alignment: .topLeading) {
                if !chapters.isEmpty {
                    HStack(spacing: 8) {
                        if let previousChapterTarget {
                            Button {
                                player.seek(to: CMTime(seconds: previousChapterTarget, preferredTimescale: 600))
                            } label: {
                                Label("Capítulo anterior", systemImage: "backward.end.fill")
                            }
                        }
                        if let nextChapterTarget {
                            Button {
                                player.seek(to: CMTime(seconds: nextChapterTarget, preferredTimescale: 600))
                            } label: {
                                Label("Próximo capítulo", systemImage: "forward.end.fill")
                            }
                        }
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(.black.opacity(0.8))
                    .padding(.leading, 20)
                    .padding(.top, 60)
                }
            }
            .overlay(alignment: .topTrailing) {
                VStack(alignment: .trailing, spacing: 8) {
                    AirPlayRoutePicker()
                        .frame(width: 44, height: 44)
                        .background(.black.opacity(0.8), in: Circle())

                    if !audioOptions.isEmpty {
                        Menu {
                            ForEach(Array(audioOptions.enumerated()), id: \.offset) { _, option in
                                Button(option.displayName) {
                                    selectManualOption(option, characteristic: .audible)
                                }
                            }
                        } label: {
                            Label("Áudio", systemImage: "waveform")
                                .padding(10)
                                .background(.black.opacity(0.8), in: Capsule())
                        }
                    }

                    if !subtitleOptions.isEmpty {
                        Menu {
                            Button("Desativar legendas") {
                                selectManualOption(nil, characteristic: .legible)
                            }
                            Divider()
                            ForEach(Array(subtitleOptions.enumerated()), id: \.offset) { _, option in
                                Button(option.displayName) {
                                    selectManualOption(option, characteristic: .legible)
                                }
                            }
                        } label: {
                            Label("Legendas", systemImage: "captions.bubble")
                                .padding(10)
                                .background(.black.opacity(0.8), in: Capsule())
                        }
                    }

                    Menu {
                        ForEach(qualityOptions, id: \.self) { quality in
                            Button {
                                selectQuality(quality)
                            } label: {
                                if selectedQuality == quality {
                                    Label(quality == "Auto" ? "Automático" : quality, systemImage: "checkmark")
                                } else {
                                    Text(quality == "Auto" ? "Automático" : quality)
                                }
                            }
                        }
                    } label: {
                        Label("Qualidade: \(selectedQuality == "Auto" ? "Auto" : selectedQuality)", systemImage: "4k.tv")
                            .padding(10)
                            .background(.black.opacity(0.8), in: Capsule())
                    }

                    Menu {
                        ForEach([0.5, 0.75, 1.0, 1.25, 1.5, 2.0], id: \.self) { rate in
                            Button {
                                selectPlaybackRate(rate)
                            } label: {
                                if selectedPlaybackRate == rate {
                                    Label(rate == 1.0 ? "Normal" : "\(rate, specifier: \"%.2g\")x", systemImage: "checkmark")
                                } else {
                                    Text(rate == 1.0 ? "Normal" : "\(rate, specifier: \"%.2g\")x")
                                }
                            }
                        }
                    } label: {
                        Label("Velocidade: \(selectedPlaybackRate, specifier: \"%.2g\")x", systemImage: "speedometer")
                            .padding(10)
                            .background(.black.opacity(0.8), in: Capsule())
                    }

                    Menu {
                        Button("Desativado") { cancelSleepTimer() }
                        Divider()
                        ForEach([15, 30, 60, 90, 120], id: \.self) { minutes in
                            Button("\(minutes) minutos") { armSleepTimer(minutes: minutes) }
                        }
                        Button("Ao fim da mídia") {
                            sleepTimerMode = .atMediaEnd
                            sleepTimerEndDate = nil
                        }
                    } label: {
                    Label(sleepTimerLabel, systemImage: sleepTimerMode == .off ? "moon" : "moon.fill")
                            .padding(10)
                            .background(.black.opacity(0.8), in: Capsule())
                        }

                    Button {
                        showPlaybackStats = true
                    } label: {
                        Label("Estatísticas", systemImage: "info.circle")
                            .padding(10)
                            .background(.black.opacity(0.8), in: Capsule())
                    }
                }
                .foregroundStyle(.white)
                .padding(.trailing, 20)
                .padding(.top, 60)
            }
            .overlay(alignment: .bottomTrailing) {
                if let segment = activeSkippableSegment {
                    Button {
                        player.seek(to: CMTime(seconds: segment.endSeconds, preferredTimescale: 600))
                    } label: {
                        Label(segment.type == .intro ? "Pular introdução" : "Pular créditos", systemImage: "forward.end.fill")
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(.black.opacity(0.8))
                    .padding(.trailing, 20)
                    .padding(.bottom, 60)
                }
            }
            .overlay(alignment: .bottomLeading) {
                if showNextEpisodePrompt, let nextEpisode {
                    VStack(alignment: .leading, spacing: 6) {
                        Button {
                            cancelNextEpisodeCountdown()
                            onPlayNext()
                        } label: {
                            Label(
                                nextEpisodeCountdown.map { "Próximo episódio em \($0)s" } ?? "Próximo episódio: \(nextEpisode.name)",
                                systemImage: "forward.end.fill"
                            )
                        }
                        if nextEpisodeCountdown != nil {
                            Button("Cancelar contagem") { cancelNextEpisodeCountdown() }
                                .font(.caption)
                        }
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(.black.opacity(0.8))
                    .padding(.leading, 20)
                    .padding(.bottom, 60)
                }
            }
            .overlay {
                if let gestureHint {
                    Text(gestureHint)
                        .font(.headline.monospacedDigit())
                        .foregroundStyle(.white)
                        .padding(.horizontal, 18)
                        .padding(.vertical, 12)
                        .background(.black.opacity(0.78), in: Capsule())
                }
            }
            .overlay(alignment: .center) {
                if let playbackError {
                    VStack(spacing: 12) {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .font(.title2)
                        Text("Não foi possível continuar a reprodução")
                            .font(.headline)
                        Text(playbackError)
                            .font(.caption)
                            .multilineTextAlignment(.center)
                            .foregroundStyle(.secondary)
                        Button("Tentar novamente") {
                            retryPlayback()
                        }
                        .buttonStyle(.borderedProminent)
                    }
                    .foregroundStyle(.white)
                    .padding(24)
                    .frame(maxWidth: 360)
                    .background(.black.opacity(0.88), in: RoundedRectangle(cornerRadius: 16))
                    .padding()
                }
            }
            .ignoresSafeArea()
            .sheet(isPresented: $showPlaybackStats) {
                NavigationStack {
                    ScrollView {
                        Text(playbackStatsText)
                            .font(.body.monospaced())
                            .textSelection(.enabled)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding()
                    }
                    .navigationTitle("Estatísticas")
                    .navigationBarTitleDisplayMode(.inline)
                    .toolbar {
                        ToolbarItem(placement: .cancellationAction) {
                            Button("Fechar") { showPlaybackStats = false }
                        }
                        ToolbarItem(placement: .confirmationAction) {
                            ShareLink(
                                item: playbackStatsText,
                                subject: Text("Dados técnicos da mídia")
                            ) {
                                Label("Compartilhar", systemImage: "square.and.arrow.up")
                            }
                        }
                    }
                }
            }
            .onAppear {
                selectedQuality = qualityOptions.contains(model.defaultQuality) ? model.defaultQuality : "Auto"
                selectedPlaybackRate = model.playbackRate
                player.defaultRate = Float(selectedPlaybackRate)
                player.currentItem?.preferredPeakBitRate = selectedQuality.peakBitrate
                if isLocal, startTimeTicks > 0 {
                    player.seek(to: CMTime(seconds: Double(startTimeTicks) / 10_000_000, preferredTimescale: 600))
                }
                guard model.autoPlay else { return }
                player.play()
                player.rate = Float(selectedPlaybackRate)
            }
            .onReceive(NotificationCenter.default.publisher(for: .AVPlayerItemFailedToPlayToEndTime)) { notification in
                guard let failedItem = notification.object as? AVPlayerItem,
                      failedItem === player.currentItem else { return }
                let error = notification.userInfo?[AVPlayerItemFailedToPlayToEndTimeErrorKey] as? Error
                player.pause()
                playbackRetryTask?.cancel()
                playbackWaitingForNetwork = PlaybackRecoveryPolicy.isTransientNetworkError(error) && model.isNetworkAvailable == false
                if PlaybackRecoveryPolicy.shouldAutomaticallyRetry(
                    error: error,
                    attempt: playbackRetryAttempt,
                    isLocal: isLocal,
                    networkAvailable: model.isNetworkAvailable
                ) {
                    let attempt = playbackRetryAttempt
                    playbackRetryAttempt += 1
                    playbackError = "Conexão perdida. Tentando novamente…"
                    playbackRetryTask = Task { @MainActor in
                        try? await Task.sleep(for: .milliseconds(PlaybackRecoveryPolicy.retryDelayMilliseconds(for: attempt)))
                        guard !Task.isCancelled else { return }
                        retryPlayback(automatic: true)
                    }
                } else {
                    playbackError = PlaybackErrorPolicy.userFacingMessage(from: error)
                }
            }
            .onReceive(NotificationCenter.default.publisher(for: .AVPlayerItemDidPlayToEndTime)) { notification in
                guard let finishedItem = notification.object as? AVPlayerItem,
                      finishedItem === player.currentItem else { return }
                didFinishPlayback = true
                if isLocal {
                    model.clearOfflinePlaybackPosition(for: itemID)
                } else if !didReportRemoteStopped {
                    didReportRemoteStopped = true
                    let positionSeconds = player.currentTime().seconds
                    Task {
                        await model.reportPlaybackStopped(
                            itemID: itemID,
                            mediaSourceID: mediaSourceID,
                            positionSeconds: positionSeconds
                        )
                    }
                }
            }
            .onReceive(NotificationCenter.default.publisher(for: AVAudioSession.interruptionNotification)) { notification in
                guard let rawType = notification.userInfo?[AVAudioSessionInterruptionTypeKey] as? UInt,
                      let type = AVAudioSession.InterruptionType(rawValue: rawType) else { return }
                switch type {
                case .began:
                    wasPlayingBeforeAudioInterruption = player.timeControlStatus == .playing
                    player.pause()
                case .ended:
                    let rawOptions = notification.userInfo?[AVAudioSessionInterruptionOptionKey] as? UInt ?? 0
                    let options = AVAudioSession.InterruptionOptions(rawValue: rawOptions)
                    if wasPlayingBeforeAudioInterruption, options.contains(.shouldResume), model.autoPlay {
                        player.play()
                        player.rate = Float(selectedPlaybackRate)
                    }
                    wasPlayingBeforeAudioInterruption = false
                @unknown default:
                    break
                }
            }
            .onReceive(NotificationCenter.default.publisher(for: AVAudioSession.routeChangeNotification)) { notification in
                guard let rawReason = notification.userInfo?[AVAudioSessionRouteChangeReasonKey] as? UInt,
                      let reason = AVAudioSession.RouteChangeReason(rawValue: rawReason),
                      reason == .oldDeviceUnavailable else { return }
                player.pause()
            }
            .onChange(of: model.isNetworkAvailable) { wasAvailable, isAvailable in
                guard PlaybackRecoveryPolicy.shouldRetryAfterNetworkRestored(
                    wasOffline: wasAvailable == false,
                    isOnline: isAvailable == true,
                    isLocal: isLocal,
                    hasPlaybackError: playbackError != nil,
                    wasTransientNetworkFailure: playbackWaitingForNetwork
                ) else { return }
                playbackWaitingForNetwork = false
                playbackRetryAttempt = 0
                retryPlayback(automatic: true)
            }
            .onDisappear {
                player.pause()
                playbackRetryTask?.cancel()
                gestureHintTask?.cancel()
            }
            .task {
                if !isLocal {
                    await model.reportPlaybackStart(
                        itemID: itemID,
                        mediaSourceID: mediaSourceID,
                        positionSeconds: Double(startTimeTicks) / 10_000_000
                    )
                }
                while !Task.isCancelled {
                    try? await Task.sleep(for: .seconds(5))
                    guard !Task.isCancelled else { return }
                    let positionSeconds = player.currentTime().seconds
                    if player.timeControlStatus == .playing {
                        playbackRetryAttempt = 0
                        playbackError = nil
                    }
                    if isLocal {
                        model.saveOfflinePlaybackPosition(itemID: itemID, positionSeconds: positionSeconds)
                    } else {
                        await model.reportPlaybackProgress(itemID: itemID, mediaSourceID: mediaSourceID, positionSeconds: positionSeconds, isPaused: player.timeControlStatus != .playing)
                    }
                }
            }
            .task {
                await selectPreferredMediaOptions()
            }
            .task(id: model.syncPlayCommandRevision) {
                guard !isLocal, let command = model.takeSyncPlayCommand() else { return }
                switch command.name {
                case "Pause":
                    player.pause()
                case "Unpause", "Play":
                    player.play()
                case "Seek":
                    if let positionTicks = command.positionTicks {
                        player.seek(to: CMTime(seconds: Double(positionTicks) / 10_000_000, preferredTimescale: 600))
                    }
                default:
                    break
                }
            }
            .task {
                mediaSegments = await model.mediaSegments(for: itemID)
                chapters = await model.chapters(for: itemID)
                while !Task.isCancelled {
                    let seconds = player.currentTime().seconds
                    if seconds.isFinite { currentTime = max(0, seconds) }
                    if let duration = player.currentItem?.asset.duration.seconds,
                       duration.isFinite, duration > 0 {
                        showNextEpisodePrompt = nextEpisode != nil && currentTime >= duration - 1.0
                        if showNextEpisodePrompt, model.autoPlay {
                            beginNextEpisodeCountdown()
                        } else if !showNextEpisodePrompt {
                            cancelNextEpisodeCountdown()
                        }
                    }
                    evaluateSleepTimer()
                    try? await Task.sleep(for: .milliseconds(500))
                }
            }
            .onDisappear {
                playbackRetryTask?.cancel()
                cancelNextEpisodeCountdown()
                gestureHintTask?.cancel()
                let positionSeconds = player.currentTime().seconds
                if isLocal, !didFinishPlayback {
                    model.saveOfflinePlaybackPosition(itemID: itemID, positionSeconds: positionSeconds)
                } else if !isLocal, !didReportRemoteStopped {
                    didReportRemoteStopped = true
                    Task {
                        await model.reportPlaybackStopped(
                            itemID: itemID,
                            mediaSourceID: mediaSourceID,
                            positionSeconds: positionSeconds
                        )
                    }
                }
            }
    }

    private var activeSkippableSegment: MediaSegment? {
        guard model.skipIntro else { return nil }
        mediaSegments.first { segment in
            (segment.type == .intro || segment.type == .outro) &&
            currentTime >= segment.startSeconds &&
            currentTime < segment.endSeconds &&
            segment.endSeconds > segment.startSeconds
        }
    }

    private func handleGestureDrag(_ value: DragGesture.Value, width: CGFloat) {
        if gestureAxis == nil {
            gestureStartTime = currentTime
            gestureStartBrightness = UIScreen.main.brightness
            guard hypot(value.translation.width, value.translation.height) >= 12 else { return }
            gestureAxis = abs(value.translation.width) > abs(value.translation.height) ? .horizontal : .vertical
        }

        switch gestureAxis {
        case .horizontal:
            let duration = player.currentItem?.duration.seconds ?? 0
            guard duration.isFinite, duration > 0, width > 0 else { return }
            let target = min(max(gestureStartTime + (value.translation.width / width) * duration, 0), duration)
            player.seek(to: CMTime(seconds: target, preferredTimescale: 600), toleranceBefore: .zero, toleranceAfter: .zero)
            currentTime = target
            showGestureHint("\(formatPlaybackTime(target)) / \(formatPlaybackTime(duration))")
        case .vertical:
            let isBrightnessGesture = value.startLocation.x < width / 2
            if isBrightnessGesture {
                let brightness = min(max(gestureStartBrightness - value.translation.height / 900, 0.01), 1)
                UIScreen.main.brightness = brightness
                showGestureHint("Brilho \(Int((brightness * 100).rounded()))%")
            } else {
                let volume = AVAudioSession.sharedInstance().outputVolume
                showGestureHint("Volume \(Int((volume * 100).rounded()))% — use os controles do sistema")
            }
        case nil:
            break
        }
    }

    private func finishGestureDrag(_ value: DragGesture.Value, width: CGFloat) {
        defer { gestureAxis = nil }
        guard gestureAxis == .horizontal else { return }
        let duration = player.currentItem?.duration.seconds ?? 0
        guard duration.isFinite, duration > 0, width > 0 else { return }
        let target = min(max(gestureStartTime + (value.translation.width / width) * duration, 0), duration)
        player.seek(to: CMTime(seconds: target, preferredTimescale: 600))
    }

    private func handleDoubleTap(at x: CGFloat, width: CGFloat) {
        guard let duration = player.currentItem?.duration.seconds,
              duration.isFinite, duration > 0 else { return }
        let delta = x < width / 2 ? -10.0 : 10.0
        let target = min(max(currentTime + delta, 0), duration)
        player.seek(to: CMTime(seconds: target, preferredTimescale: 600))
        currentTime = target
        showGestureHint(delta < 0 ? "−10 segundos" : "+10 segundos")
    }

    private func showGestureHint(_ hint: String) {
        gestureHint = hint
        gestureHintTask?.cancel()
        gestureHintTask = Task { @MainActor in
            try? await Task.sleep(for: .seconds(1.2))
            guard !Task.isCancelled else { return }
            gestureHint = nil
        }
    }

    private func retryPlayback(automatic: Bool = false) {
        if !automatic {
            playbackRetryTask?.cancel()
            playbackRetryAttempt = 0
        }
        playbackWaitingForNetwork = false
        let position = player.currentTime()
        let replacement = AVPlayerItem(url: url)
        replacement.preferredPeakBitRate = selectedQuality.peakBitrate
        player.replaceCurrentItem(with: replacement)
        playbackError = nil
        if position.isValid && position.seconds.isFinite && position.seconds > 0 {
            player.seek(to: position)
        }
        player.play()
        player.rate = Float(selectedPlaybackRate)
    }

    private var playbackStatsText: String {
        let duration = player.currentItem?.duration.seconds ?? 0
        let durationText = duration.isFinite && duration > 0 ? formatPlaybackTime(duration) : "indisponível"
        let bitrate = player.currentItem?.preferredPeakBitRate ?? 0
        let bitrateText = bitrate > 0 ? "\(Int(bitrate / 1_000)) kbps" : "Automático"
        let status: String
        switch player.timeControlStatus {
        case .playing:
            status = "Reproduzindo"
        case .paused:
            status = "Pausado"
        case .waitingToPlayAtSpecifiedRate:
            status = "Aguardando buffer"
        @unknown default:
            status = "Desconhecido"
        }
        return [
            "Origem: \(isLocal ? "Download local" : "Streaming")",
            "Posição: \(formatPlaybackTime(currentTime)) / \(durationText)",
            "Qualidade: \(selectedQuality)",
            "Bitrate máximo: \(bitrateText)",
            "Velocidade: \(selectedPlaybackRate, specifier: "%.2g")x",
            "Estado: \(status)",
            "Item: \(itemID)"
        ].joined(separator: "\n")
    }

    private func formatPlaybackTime(_ seconds: Double) -> String {
        guard seconds.isFinite, seconds >= 0 else { return "--:--" }
        let totalSeconds = Int(seconds.rounded(.down))
        return String(format: "%02d:%02d:%02d", totalSeconds / 3600, (totalSeconds / 60) % 60, totalSeconds % 60)
    }

    private var previousChapterTarget: Double? {
        let starts = chapters.map(\.startSeconds).filter { $0 < currentTime - 0.5 }
        guard !starts.isEmpty else { return nil }
        let current = chapters.last { $0.startSeconds <= currentTime }
        if let current, currentTime - current.startSeconds > 3 {
            return current.startSeconds
        }
        return starts.dropLast().last ?? 0
    }

    private var nextChapterTarget: Double? {
        chapters.first(where: { $0.startSeconds > currentTime + 0.5 })?.startSeconds
    }

    private var sleepTimerLabel: String {
        switch sleepTimerMode {
        case .off:
            return "Temporizador"
        case .atMediaEnd:
            return "Ao fim da mídia"
        case .countdown:
            guard let sleepTimerEndDate else { return "Temporizador" }
            let remaining = max(0, Int(sleepTimerEndDate.timeIntervalSinceNow.rounded(.up)))
            let minutes = remaining / 60
            let seconds = remaining % 60
            return minutes > 0 ? "Pausa em \(minutes)m \(String(format: "%02d", seconds))s" : "Pausa em \(seconds)s"
        }
    }

    private func armSleepTimer(minutes: Int) {
        sleepTimerMode = .countdown
        sleepTimerEndDate = Date().addingTimeInterval(TimeInterval(minutes * 60))
    }

    private func cancelSleepTimer() {
        sleepTimerMode = .off
        sleepTimerEndDate = nil
    }

    private func evaluateSleepTimer() {
        switch sleepTimerMode {
        case .off:
            return
        case .countdown:
            guard let sleepTimerEndDate, Date() >= sleepTimerEndDate else { return }
        case .atMediaEnd:
            guard let duration = player.currentItem?.asset.duration.seconds,
                  duration.isFinite, duration > 0,
                  currentTime >= duration - 0.5 else { return }
        }
        player.pause()
        cancelSleepTimer()
    }

    private func selectPreferredMediaOptions() async {
        guard let currentItem = player.currentItem else { return }
        let asset = currentItem.asset
        do {
            _ = try await asset.load(.availableMediaCharacteristicsWithMediaSelectionOptions)
        } catch {
            return
        }
        selectOption(matching: model.preferredAudioLanguage, in: asset, characteristic: .audible, item: currentItem)
        selectOption(matching: model.preferredSubtitleLanguage, in: asset, characteristic: .legible, item: currentItem)
        audioOptions = asset.mediaSelectionGroup(forMediaCharacteristic: .audible)?.options ?? []
        subtitleOptions = asset.mediaSelectionGroup(forMediaCharacteristic: .legible)?.options ?? []
    }

    private func selectOption(matching language: String, in asset: AVAsset, characteristic: AVMediaCharacteristic, item: AVPlayerItem) {
        let normalized = language.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        guard !normalized.isEmpty,
              let group = asset.mediaSelectionGroup(forMediaCharacteristic: characteristic),
              let option = group.options.first(where: { option in
                  guard let identifier = option.locale?.identifier.lowercased() else { return false }
                  return identifier == normalized || identifier.hasPrefix("\(normalized)-") || normalized.hasPrefix("\(identifier)-")
              }) else { return }
        item.select(option, in: group)
    }

    private func selectManualOption(_ option: AVMediaSelectionOption?, characteristic: AVMediaCharacteristic) {
        guard let currentItem = player.currentItem,
              let group = currentItem.asset.mediaSelectionGroup(forMediaCharacteristic: characteristic) else { return }
        currentItem.select(option, in: group)
    }

    private func selectQuality(_ quality: String) {
        selectedQuality = quality
        player.currentItem?.preferredPeakBitRate = quality.peakBitrate
    }

    private var qualityOptions: [String] {
        let available = availableQualityOptions.isEmpty
            ? ["4K", "1440p", "1080p", "720p", "480p"]
            : availableQualityOptions
        return ["Auto"] + available.filter { $0 != "Auto" }
    }

    private func selectPlaybackRate(_ rate: Double) {
        selectedPlaybackRate = rate
        player.defaultRate = Float(rate)
        if player.timeControlStatus == .playing {
            player.rate = Float(rate)
        }
    }

    private func beginNextEpisodeCountdown() {
        guard nextEpisode != nil, nextEpisodeCountdown == nil else { return }
        nextEpisodeCountdown = 5
        nextEpisodeCountdownTask = Task { @MainActor in
            for value in stride(from: 5, through: 1, by: -1) {
                guard !Task.isCancelled else { return }
                nextEpisodeCountdown = value
                try? await Task.sleep(for: .seconds(1))
            }
            guard !Task.isCancelled else { return }
            nextEpisodeCountdown = nil
            onPlayNext()
        }
    }

    private func cancelNextEpisodeCountdown() {
        nextEpisodeCountdownTask?.cancel()
        nextEpisodeCountdownTask = nil
        nextEpisodeCountdown = nil
    }
}

private extension String {
    var peakBitrate: Double {
        Double(PlaybackQualityPolicy.maxStreamingBitrate(for: self) ?? 0)
    }
}

struct MediaCard: View {
    let item: MediaItem
    let model: AppModel
    @State private var imageURL: URL?

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Group {
                if let imageURL {
                    AuthenticatedArtwork(url: imageURL, token: model.session?.accessToken)
                } else {
                    RoundedRectangle(cornerRadius: 10).fill(.gray.opacity(0.25)).overlay { Image(systemName: "film") }
                }
            }
            .aspectRatio(2 / 3, contentMode: .fit)
            .clipShape(.rect(cornerRadius: 10))
            .overlay(alignment: .topLeading) {
                if let qualityBadge = item.qualityBadge {
                    Text(qualityBadge)
                        .font(.caption2.weight(.bold))
                        .padding(.horizontal, 6)
                        .padding(.vertical, 4)
                        .background(.black.opacity(0.78), in: Capsule())
                        .foregroundStyle(.white)
                        .padding(8)
                        .accessibilityLabel("Qualidade \(qualityBadge)")
                }
            }
            Text(item.name).font(.subheadline.weight(.medium)).lineLimit(2)
            if let year = item.productionYear { Text(String(year)).font(.caption).foregroundStyle(.secondary) }
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel(item.name)
        .task { imageURL = await model.imageURL(for: item) }
    }
}

@MainActor
private enum ArtworkImageCache {
    static let images = NSCache<NSURL, UIImage>()

    static func removeAll() {
        images.removeAllObjects()
        URLCache.shared.removeAllCachedResponses()
    }
}

private struct AuthenticatedArtwork: View {
    let url: URL
    let token: String?
    @State private var image: Image?

    var body: some View {
        Group {
            if let image {
                image.resizable().scaledToFill()
            } else {
                Color.gray.opacity(0.25)
            }
        }
            .task {
                if let cached = ArtworkImageCache.images.object(forKey: url as NSURL) {
                    image = Image(uiImage: cached)
                    return
                }
                var request = URLRequest(url: url)
                request.setValue("MulletaFlix-iOS/0.1.0", forHTTPHeaderField: "User-Agent")
                request.setValue(APIClient.authorizationHeader(accessToken: token), forHTTPHeaderField: "Authorization")
                if let token { request.setValue(token, forHTTPHeaderField: "X-Emby-Token") }
                if let (data, _) = try? await URLSession.shared.data(for: request), let uiImage = UIImage(data: data) {
                    ArtworkImageCache.images.setObject(uiImage, forKey: url as NSURL)
                    image = Image(uiImage: uiImage)
                }
            }
    }
}
