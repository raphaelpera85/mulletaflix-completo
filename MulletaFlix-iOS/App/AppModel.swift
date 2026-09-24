import Foundation
import Network
import Observation

enum IOSThemePreference: String, CaseIterable, Identifiable {
    case system
    case dark
    case light
    case midnight
    case ocean
    case forest
    case sunset
    case violet

    var id: String { rawValue }

    var title: String {
        switch self {
        case .system: return "Sistema"
        case .dark: return "Escuro"
        case .light: return "Claro"
        case .midnight: return "Meia-noite"
        case .ocean: return "Oceano"
        case .forest: return "Floresta"
        case .sunset: return "Pôr do sol"
        case .violet: return "Violeta"
        }
    }
}

@MainActor
@Observable
final class AppModel {
    enum State: Equatable { case signedOut, loading, signedIn }

    private(set) var state: State = .signedOut
    private(set) var session: UserSession?
    private(set) var profile: UserProfile?
    private(set) var heroItem: MediaItem?
    private(set) var latestItems: [MediaItem] = []
    private(set) var resumeItems: [MediaItem] = []
    private(set) var nextUpItems: [MediaItem] = []
    private(set) var favoriteItems: [MediaItem] = []
    private(set) var popularItems: [MediaItem] = []
    private(set) var movieItems: [MediaItem] = []
    private(set) var seriesItems: [MediaItem] = []
    private(set) var libraries: [MediaItem] = []
    private(set) var libraryItems: [MediaItem] = []
    private(set) var selectedLibrary: MediaItem?
    private(set) var searchItems: [MediaItem] = []
    private(set) var searchHints: [SearchHint] = []
    private(set) var searchHistory: [String] = []
    private(set) var similarItems: [MediaItem] = []
    private(set) var specialFeatures: [MediaItem] = []
    private(set) var albumTracks: [MediaItem] = []
    private(set) var seriesSeasons: [MediaItem] = []
    private(set) var seriesEpisodes: [MediaItem] = []
    private(set) var selectedSeasonIndex = 0
    private(set) var isSeriesLoading = false
    private var activeSeriesID: String?
    private(set) var playlists: [Playlist] = []
    private(set) var isPlaylistLoading = false
    private(set) var isSearching = false
    private(set) var liveChannels: [MediaItem] = []
    private(set) var livePrograms: [MediaItem] = []
    private(set) var liveRecordings: [MediaItem] = []
    private(set) var scheduledLiveProgramIDs: Set<String> = []
    private(set) var isLiveTVLoading = false
    private(set) var syncPlayGroups: [SyncPlayGroup] = []
    private(set) var activeSyncPlayGroupID: String?
    private(set) var isSyncPlayLoading = false
    private(set) var syncPlayRealtimeStatus = "Desconectado"
    private(set) var lastSyncPlayCommand: SyncPlayCommand?
    private(set) var syncPlayCommandRevision = 0
    private(set) var offlineDownloads: [OfflineDownload] = []
    var offlineQueuePaused = UserDefaults.standard.bool(forKey: "downloads.queuePaused") {
        didSet { UserDefaults.standard.set(offlineQueuePaused, forKey: "downloads.queuePaused") }
    }
    private(set) var isQuickConnectAvailable: Bool?
    private(set) var quickConnectCode: String?
    private(set) var quickConnectSecondsRemaining: Int?
    private(set) var isQuickConnectWaiting = false
    private(set) var discoveredServers: [DiscoveredServer] = []
    private(set) var isDiscoveringServers = false
    private(set) var serverInfo: ServerInfo?
    private(set) var pendingDeepLinkItemID: String?
    private(set) var deepLinkRevision = 0
    private(set) var isVerifyingServer = false
    private(set) var isNetworkAvailable: Bool?
    private(set) var isWiFiAvailable: Bool?
    private(set) var isRegistering = false
    private(set) var registrationError: String?
    private(set) var publicUsers: [PublicUser] = []
    private(set) var isSwitchingUser = false
    var serverURL = "http://mulletaflix.duckdns.org:8096"
    var username = ""
    var password = ""
    var errorMessage: String?
    var autoPlay = UserDefaults.standard.object(forKey: "settings.autoPlay") as? Bool ?? true {
        didSet { UserDefaults.standard.set(autoPlay, forKey: "settings.autoPlay") }
    }
    var playbackRate = UserDefaults.standard.object(forKey: "settings.playbackRate") as? Double ?? 1.0 {
        didSet { UserDefaults.standard.set(playbackRate, forKey: "settings.playbackRate") }
    }
    var defaultQuality = UserDefaults.standard.string(forKey: "settings.defaultQuality") ?? "Auto" {
        didSet { UserDefaults.standard.set(defaultQuality, forKey: "settings.defaultQuality") }
    }
    var videoAspectRatio = IOSVideoAspectRatio(rawValue: UserDefaults.standard.string(forKey: "settings.videoAspectRatio") ?? "fit") ?? .fit {
        didSet { UserDefaults.standard.set(videoAspectRatio.rawValue, forKey: "settings.videoAspectRatio") }
    }
    var themePreference = IOSThemePreference(rawValue: UserDefaults.standard.string(forKey: "settings.theme") ?? "dark") ?? .dark {
        didSet { UserDefaults.standard.set(themePreference.rawValue, forKey: "settings.theme") }
    }
    var pictureInPictureEnabled = UserDefaults.standard.object(forKey: "settings.pictureInPicture") as? Bool ?? true {
        didSet { UserDefaults.standard.set(pictureInPictureEnabled, forKey: "settings.pictureInPicture") }
    }
    var skipIntro = UserDefaults.standard.object(forKey: "settings.skipIntro") as? Bool ?? true {
        didSet { UserDefaults.standard.set(skipIntro, forKey: "settings.skipIntro") }
    }
    var wifiOnlyDownloads = UserDefaults.standard.object(forKey: "settings.wifiOnlyDownloads") as? Bool ?? false {
        didSet {
            UserDefaults.standard.set(wifiOnlyDownloads, forKey: "settings.wifiOnlyDownloads")
            if !wifiOnlyDownloads { resumeQueuedOfflineDownloads() }
        }
    }
    var preferredAudioLanguage = UserDefaults.standard.string(forKey: "settings.audioLanguage") ?? "" {
        didSet { UserDefaults.standard.set(preferredAudioLanguage, forKey: "settings.audioLanguage") }
    }
    var preferredSubtitleLanguage = UserDefaults.standard.string(forKey: "settings.subtitleLanguage") ?? "" {
        didSet { UserDefaults.standard.set(preferredSubtitleLanguage, forKey: "settings.subtitleLanguage") }
    }
    var librarySort = UserDefaults.standard.string(forKey: "settings.librarySort") ?? "SortName" {
        didSet { UserDefaults.standard.set(librarySort, forKey: "settings.librarySort") }
    }
    var librarySortOrder = UserDefaults.standard.string(forKey: "settings.librarySortOrder") ?? "Ascending" {
        didSet { UserDefaults.standard.set(librarySortOrder, forKey: "settings.librarySortOrder") }
    }
    var compactGrid = UserDefaults.standard.object(forKey: "settings.compactGrid") as? Bool ?? false {
        didSet { UserDefaults.standard.set(compactGrid, forKey: "settings.compactGrid") }
    }
    var libraryListLayout = UserDefaults.standard.bool(forKey: "settings.libraryListLayout") {
        didSet { UserDefaults.standard.set(libraryListLayout, forKey: "settings.libraryListLayout") }
    }
    var libraryGenreFilter = UserDefaults.standard.string(forKey: "settings.libraryGenreFilter") ?? "" {
        didSet { UserDefaults.standard.set(libraryGenreFilter, forKey: "settings.libraryGenreFilter") }
    }
    var libraryYearFilter = UserDefaults.standard.string(forKey: "settings.libraryYearFilter") ?? "" {
        didSet { UserDefaults.standard.set(libraryYearFilter, forKey: "settings.libraryYearFilter") }
    }
    var libraryPlayedFilter = LibraryPlayedFilter(rawValue: UserDefaults.standard.string(forKey: "settings.libraryPlayedFilter") ?? "all") ?? .all {
        didSet { UserDefaults.standard.set(libraryPlayedFilter.rawValue, forKey: "settings.libraryPlayedFilter") }
    }
    var libraryFavoritesOnly = UserDefaults.standard.bool(forKey: "settings.libraryFavoritesOnly") {
        didSet { UserDefaults.standard.set(libraryFavoritesOnly, forKey: "settings.libraryFavoritesOnly") }
    }

    private let sessionStore: SessionStore
    private let downloadCoordinator = OfflineDownloadCoordinator.shared
    private let connectivityMonitor = NWPathMonitor()
    private let connectivityQueue = DispatchQueue(label: "com.mulletaflix.ios.connectivity")
    private var client: APIClient?
    private let syncPlayRealtimeClient = SyncPlayRealtimeClient()
    private var syncPlayRealtimeTask: Task<Void, Never>?
    private var quickConnectTask: Task<Void, Never>?
    private var downloadTasks: [String: Task<Void, Never>] = [:]
    private var searchRequestID = UUID()
    private var previousNetworkAvailability: Bool?

    init(sessionStore: SessionStore = KeychainSessionStore()) {
        self.sessionStore = sessionStore
        if let saved = sessionStore.load() {
            session = saved
            serverURL = saved.serverURL.absoluteString
            client = APIClient(serverURL: saved.serverURL, urlSession: .shared, deviceID: "ios-session")
            state = .signedIn
        }
        offlineDownloads = OfflineDownloadStore.load()
        searchHistory = UserDefaults.standard.stringArray(forKey: "search.history") ?? []
        connectivityMonitor.pathUpdateHandler = { [weak self] path in
            let available = path.status == .satisfied
            let wifi = path.status == .satisfied && path.usesInterfaceType(.wifi)
            Task { @MainActor [weak self] in
                self?.handleNetworkAvailabilityChange(available, wifi: wifi)
            }
        }
        connectivityMonitor.start(queue: connectivityQueue)
        Task { @MainActor [weak self] in
            await self?.restoreOfflineDownloads()
        }
    }

    deinit {
        connectivityMonitor.cancel()
    }

    func signIn() async {
        guard let url = normalizedURL else { errorMessage = APIError.invalidServerURL.localizedDescription; return }
        state = .loading
        errorMessage = nil
        do {
            let api = APIClient(serverURL: url, deviceID: "ios-session")
            serverInfo = try await api.publicSystemInfo()
            let saved = try await api.authenticate(username: username.trimmingCharacters(in: .whitespacesAndNewlines), password: password)
            try sessionStore.save(saved)
            client = api
            session = saved
            password = ""
            try await loadHome(using: api, userID: saved.userID)
            try await loadLibraries(using: api, userID: saved.userID)
            applyProfile(try? await api.userProfile(userID: saved.userID))
            state = .signedIn
        } catch {
            state = .signedOut
            errorMessage = error.localizedDescription
        }
    }

    func receiveDeepLink(_ url: URL) {
        guard let link = MediaDeepLinkParser.link(from: url) else { return }
        if let targetServerID = link.serverID,
           let currentServerID = serverInfo?.id,
           !targetServerID.isEmpty,
           !currentServerID.isEmpty,
           targetServerID.caseInsensitiveCompare(currentServerID) != .orderedSame {
            errorMessage = "Este link pertence a outro servidor MulletaFlix."
            return
        }
        pendingDeepLinkItemID = link.itemID
        deepLinkRevision += 1
    }

    func loadPendingDeepLinkItem() async -> MediaItem? {
        guard let itemID = pendingDeepLinkItemID else { return nil }
        guard let session, let client else { return nil }
        pendingDeepLinkItemID = nil
        return try? await client.item(userID: session.userID, itemID: itemID)
    }

    func register(username: String, password: String, confirmation: String) async -> Bool {
        let cleanUsername = username.trimmingCharacters(in: .whitespacesAndNewlines)
        registrationError = nil
        guard !cleanUsername.isEmpty else {
            registrationError = "Informe um usuário ou e-mail."
            return false
        }
        guard password.count >= 4 else {
            registrationError = "A senha deve ter pelo menos 4 caracteres."
            return false
        }
        guard password == confirmation else {
            registrationError = "As senhas não coincidem."
            return false
        }
        guard let url = normalizedURL else {
            registrationError = APIError.invalidServerURL.localizedDescription
            return false
        }

        isRegistering = true
        defer { isRegistering = false }
        do {
            let api = APIClient(serverURL: url, deviceID: "ios-registration")
            let result = try await api.register(username: cleanUsername, password: password)
            guard result.success else {
                throw APIError.serverMessage(result.message ?? "Não foi possível concluir o cadastro.")
            }
            return true
        } catch {
            registrationError = error.localizedDescription
            return false
        }
    }

    func loadPublicUsers() async {
        guard let client else { return }
        publicUsers = (try? await client.publicUsers()) ?? []
    }

    func switchUser(username: String, password: String) async -> Bool {
        guard let url = normalizedURL else {
            errorMessage = APIError.invalidServerURL.localizedDescription
            return false
        }
        isSwitchingUser = true
        defer { isSwitchingUser = false }
        do {
            let api = APIClient(serverURL: url, deviceID: "ios-session")
            let saved = try await api.authenticate(
                username: username.trimmingCharacters(in: .whitespacesAndNewlines),
                password: password
            )
            try sessionStore.save(saved)
            client = api
            session = saved
            self.password = ""
            clearUserScopedContent()
            try await loadHome(using: api, userID: saved.userID)
            try await loadLibraries(using: api, userID: saved.userID)
            applyProfile(try? await api.userProfile(userID: saved.userID))
            state = .signedIn
            return true
        } catch {
            errorMessage = error.localizedDescription
            state = .signedIn
            return false
        }
    }

    func shareText(for item: MediaItem) -> String {
        let title = item.name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? "um título" : item.name
        let base = canonicalShareBaseURL
        guard !base.isEmpty,
              let encodedID = item.id.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed)?
                .replacingOccurrences(of: "/", with: "%2F") else {
            return "Confira \"\(title)\" no MulletaFlix."
        }
        var text = "Confira \"\(title)\" no MulletaFlix.\n\(base)/web/#/details?id=\(encodedID)"
        if let serverID = serverInfo?.id?.trimmingCharacters(in: .whitespacesAndNewlines),
           !serverID.isEmpty,
           let encodedServerID = serverID.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed)?
            .replacingOccurrences(of: "/", with: "%2F") {
            text += "&serverId=\(encodedServerID)"
        }
        return text
    }

    func verifyServer() async {
        guard let url = normalizedURL else { errorMessage = APIError.invalidServerURL.localizedDescription; return }
        isVerifyingServer = true
        defer { isVerifyingServer = false }
        do {
            let api = APIClient(serverURL: url, deviceID: "ios-session")
            serverInfo = try await api.publicSystemInfo()
            errorMessage = nil
        } catch {
            serverInfo = nil
            errorMessage = "Não foi possível verificar o servidor: \(error.localizedDescription)"
        }
    }

    func startQuickConnect() async {
        guard let url = normalizedURL else { errorMessage = APIError.invalidServerURL.localizedDescription; return }
        cancelQuickConnect()
        state = .loading
        errorMessage = nil
        do {
            let api = APIClient(serverURL: url, deviceID: "ios-session")
            serverInfo = try await api.publicSystemInfo()
            let available = try await api.isQuickConnectEnabled()
            isQuickConnectAvailable = available
            guard available else {
                state = .signedOut
                errorMessage = "Quick Connect está desativado neste servidor."
                return
            }
            let challenge = try await api.initiateQuickConnect()
            client = api
            quickConnectCode = challenge.code
            quickConnectSecondsRemaining = 300
            isQuickConnectWaiting = true
            state = .signedOut
            quickConnectTask = Task { [weak self] in
                guard let self else { return }
                await self.pollQuickConnect(api: api, secret: challenge.secret)
            }
        } catch {
            state = .signedOut
            errorMessage = error.localizedDescription
        }
    }

    func cancelQuickConnect() {
        quickConnectTask?.cancel()
        quickConnectTask = nil
        quickConnectCode = nil
        quickConnectSecondsRemaining = nil
        isQuickConnectWaiting = false
        if state == .loading { state = .signedOut }
    }

    func loadHome() async {
        guard let session, let client else { return }
        do { try await loadHome(using: client, userID: session.userID) }
        catch { errorMessage = error.localizedDescription }
    }

    func loadLibraries() async {
        guard let session, let client else { return }
        do { try await loadLibraries(using: client, userID: session.userID) }
        catch { errorMessage = error.localizedDescription }
    }

    func loadProfile() async {
        guard let session, let client else { return }
        do {
            applyProfile(try await client.userProfile(userID: session.userID))
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func loadLiveTV() async {
        guard let session, let client else { return }
        isLiveTVLoading = true
        defer { isLiveTVLoading = false }
        var firstError: String?
        do {
            liveChannels = try await client.allLiveTVChannels(userID: session.userID)
        } catch {
            liveChannels = []
            firstError = error.localizedDescription
        }
        do {
            liveRecordings = try await client.allLiveTVRecordings(userID: session.userID)
        } catch {
            liveRecordings = []
            firstError = firstError ?? error.localizedDescription
        }
        do {
            scheduledLiveProgramIDs = try await client.scheduledLiveTVProgramIDs()
        } catch {
            scheduledLiveProgramIDs = []
            firstError = firstError ?? error.localizedDescription
        }
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        let start = formatter.string(from: Date())
        let end = formatter.string(from: Date().addingTimeInterval(24 * 60 * 60))
        do {
            livePrograms = try await client.allLiveTVPrograms(channelIDs: liveChannels.map(\.id), startDate: start, endDate: end)
        } catch {
            livePrograms = []
            firstError = firstError ?? error.localizedDescription
        }
        if let firstError { errorMessage = firstError }
    }

    func scheduleLiveProgram(_ program: MediaItem) async {
        guard let client else { return }
        do {
            try await client.scheduleLiveTV(program: program)
            scheduledLiveProgramIDs.insert(program.id)
        } catch { errorMessage = error.localizedDescription }
    }

    func enqueueOfflineDownload(for item: MediaItem) async {
        guard let client, let url = await client.playbackURL(for: item) else {
            errorMessage = "Este item não possui uma fonte disponível para download."
            return
        }
        if offlineDownloads.contains(where: { $0.itemID == item.id && $0.state == .completed }) { return }
        let entry = OfflineDownload(itemID: item.id, title: item.name, state: .queued, percent: 0, fileName: OfflineDownloadStore.fileName(for: item), sourceURL: url.absoluteString)
        upsertDownload(entry)
        if canStartOfflineDownloads {
            startOfflineDownload(entry, url: url)
        }
    }

    func retryOfflineDownload(_ entry: OfflineDownload) {
        guard let url = URL(string: entry.sourceURL) else { return }
        let next = entry.with(state: .queued, percent: 0, error: nil)
        upsertDownload(next)
        if canStartOfflineDownloads {
            startOfflineDownload(next, url: url)
        }
    }

    func pauseOfflineDownload(_ entry: OfflineDownload) {
        guard entry.state == .downloading || entry.state == .queued else { return }
        upsertDownload(entry.with(state: .paused, percent: entry.percent, error: nil))
        downloadCoordinator.pause(id: entry.id)
    }

    func resumeOfflineDownload(_ entry: OfflineDownload) {
        guard entry.state == .paused,
              let url = URL(string: entry.sourceURL) else { return }
        let queued = entry.with(state: .queued, percent: 0, error: nil)
        upsertDownload(queued)
        if canStartOfflineDownloads {
            startOfflineDownload(queued, url: url)
        }
    }

    func pauseAllOfflineDownloads() {
        offlineQueuePaused = true
        offlineDownloads
            .filter { $0.state == .downloading || $0.state == .queued }
            .forEach(pauseOfflineDownload)
    }

    func resumeAllOfflineDownloads() {
        offlineQueuePaused = false
        offlineDownloads
            .filter { $0.state == .paused }
            .forEach(resumeOfflineDownload)
        if canStartOfflineDownloads {
            resumeQueuedOfflineDownloads()
        }
    }

    func removeOfflineDownload(_ entry: OfflineDownload) {
        downloadTasks[entry.id]?.cancel()
        downloadTasks[entry.id] = nil
        OfflineDownloadStore.removeFile(entry.fileName)
        offlineDownloads.removeAll { $0.id == entry.id }
        OfflineDownloadStore.save(offlineDownloads)
    }

    func removeCompletedOfflineDownloads() {
        let completed = offlineDownloads.filter { $0.state == .completed }
        completed.forEach { OfflineDownloadStore.removeFile($0.fileName) }
        offlineDownloads.removeAll { $0.state == .completed }
        OfflineDownloadStore.save(offlineDownloads)
    }

    func removeFailedOfflineDownloads() {
        offlineDownloads.removeAll { entry in
            guard entry.state == .failed else { return false }
            OfflineDownloadStore.removeFile(entry.fileName)
            return true
        }
        OfflineDownloadStore.save(offlineDownloads)
    }

    func retryFailedOfflineDownloads() {
        offlineDownloads
            .filter { $0.state == .failed }
            .forEach(retryOfflineDownload)
    }

    var offlineDownloadedBytes: Int64 {
        offlineDownloads.reduce(0) { $0 + max(0, $1.bytesDownloaded) }
    }

    func localURL(for item: MediaItem) -> URL? {
        guard let entry = offlineDownloads.first(where: { $0.itemID == item.id && $0.state == .completed }) else { return nil }
        return OfflineDownloadStore.url(for: entry.fileName)
    }

    func reportPlaybackStart(itemID: String, mediaSourceID: String?) async {
        guard let client else { return }
        try? await client.reportPlaybackStart(itemID: itemID, mediaSourceID: mediaSourceID)
    }

    func reportPlaybackProgress(itemID: String, mediaSourceID: String?, positionSeconds: Double, isPaused: Bool) async {
        guard let client else { return }
        let safeSeconds = positionSeconds.isFinite ? max(0, positionSeconds) : 0
        let ticks = Int64(safeSeconds * 10_000_000)
        try? await client.reportPlaybackProgress(itemID: itemID, mediaSourceID: mediaSourceID, positionTicks: ticks, isPaused: isPaused)
    }

    func reportPlaybackStopped(itemID: String, mediaSourceID: String?, positionSeconds: Double) async {
        guard let client else { return }
        let safeSeconds = positionSeconds.isFinite ? max(0, positionSeconds) : 0
        let ticks = Int64(safeSeconds * 10_000_000)
        try? await client.reportPlaybackStopped(itemID: itemID, mediaSourceID: mediaSourceID, positionTicks: ticks)
    }

    func loadSyncPlay() async {
        guard let client else { return }
        isSyncPlayLoading = true
        defer { isSyncPlayLoading = false }
        do {
            syncPlayGroups = try await client.syncPlayGroups()
        } catch {
            syncPlayGroups = []
            errorMessage = error.localizedDescription
        }
    }

    func createSyncPlayGroup(name: String) async {
        guard let client else { return }
        do {
            try await client.createSyncPlayGroup(name: name.trimmingCharacters(in: .whitespacesAndNewlines))
            await loadSyncPlay()
        } catch { errorMessage = error.localizedDescription }
    }

    func joinSyncPlayGroup(_ group: SyncPlayGroup) async {
        guard let client else { return }
        do {
            try await client.joinSyncPlayGroup(id: group.groupId)
            activeSyncPlayGroupID = group.groupId
            startSyncPlayRealtime()
            await loadSyncPlay()
        } catch { errorMessage = error.localizedDescription }
    }

    func leaveSyncPlayGroup() async {
        guard let client else { return }
        do {
            try await client.leaveSyncPlayGroup()
            stopSyncPlayRealtime()
            activeSyncPlayGroupID = nil
            await loadSyncPlay()
        } catch { errorMessage = error.localizedDescription }
    }

    private func startSyncPlayRealtime() {
        guard let session else { return }
        syncPlayRealtimeTask?.cancel()
        syncPlayRealtimeStatus = "Conectando…"
        let stream = syncPlayRealtimeClient.connect(
            serverURL: session.serverURL,
            accessToken: session.accessToken,
            deviceID: "ios-session"
        )
        syncPlayRealtimeTask = Task { [weak self] in
            guard let self else { return }
            syncPlayRealtimeStatus = "Conectado"
            for await event in stream {
                if case .command(let command) = event {
                    lastSyncPlayCommand = command
                    syncPlayCommandRevision &+= 1
                }
            }
            if !Task.isCancelled { syncPlayRealtimeStatus = "Desconectado" }
        }
    }

    private func stopSyncPlayRealtime() {
        syncPlayRealtimeTask?.cancel()
        syncPlayRealtimeTask = nil
        syncPlayRealtimeClient.disconnect()
        syncPlayRealtimeStatus = "Desconectado"
        lastSyncPlayCommand = nil
    }

    func takeSyncPlayCommand() -> SyncPlayCommand? {
        defer { lastSyncPlayCommand = nil }
        return lastSyncPlayCommand
    }

    func discoverServers() async {
        isDiscoveringServers = true
        defer { isDiscoveringServers = false }
        discoveredServers = await LocalServerDiscovery().discover()
        if discoveredServers.count == 1, let server = discoveredServers.first {
            serverURL = server.address.absoluteString
        }
    }

    func search(_ term: String, filter: SearchFilter = .all) async {
        let requestID = UUID()
        searchRequestID = requestID
        let cleanTerm = term.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleanTerm.isEmpty, let session, let client else {
            searchItems = []
            searchHints = []
            return
        }
        guard cleanTerm.count >= 2 else {
            searchItems = []
            searchHints = []
            return
        }
        isSearching = true
        defer { isSearching = false }
        async let hints = client.searchHints(userID: session.userID, term: cleanTerm)
        async let results = client.allSearchItems(userID: session.userID, term: cleanTerm, includeItemTypes: filter.includeItemTypes)
        let loadedHints = (try? await hints) ?? []
        let loadedItems = (try? await results) ?? []
        guard searchRequestID == requestID else { return }
        searchHints = loadedHints
        searchItems = loadedItems
        if !loadedItems.isEmpty { rememberSearch(cleanTerm) }
    }

    func removeSearchHistory(_ term: String) {
        searchHistory.removeAll { $0 == term }
        UserDefaults.standard.set(searchHistory, forKey: "search.history")
    }

    func clearSearchHistory() {
        searchHistory = []
        UserDefaults.standard.removeObject(forKey: "search.history")
    }

    func openLibrary(_ library: MediaItem) async {
        guard let session, let client else { return }
        selectedLibrary = library
        do {
            libraryItems = try await client.allItems(
                userID: session.userID,
                parentID: library.id,
                sortBy: librarySort,
                sortOrder: librarySortOrder,
                filters: libraryFavoritesOnly ? "IsFavorite" : nil,
                isFavorite: libraryFavoritesOnly ? true : nil,
                genres: nonEmptyFilter(libraryGenreFilter),
                years: nonEmptyFilter(libraryYearFilter),
                isPlayed: libraryPlayedFilter.isPlayed
            )
        }
        catch { errorMessage = error.localizedDescription }
    }

    func reloadSelectedLibrary() async {
        guard let selectedLibrary else { return }
        await openLibrary(selectedLibrary)
    }

    func closeLibrary() {
        selectedLibrary = nil
        libraryItems = []
    }

    private func nonEmptyFilter(_ value: String) -> String? {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }

    func toggleFavorite(for item: MediaItem) async -> MediaItem? {
        guard let session, let client else { return nil }
        let nextValue = !item.isFavorite
        do {
            try await client.setFavorite(userID: session.userID, itemID: item.id, isFavorite: nextValue)
            let updated = item.withFavorite(nextValue)
            replace(updated)
            return updated
        } catch { errorMessage = error.localizedDescription; return nil }
    }

    func togglePlayed(for item: MediaItem) async -> MediaItem? {
        guard let session, let client else { return nil }
        let nextValue = !item.isPlayed
        do {
            try await client.setPlayed(userID: session.userID, itemID: item.id, isPlayed: nextValue)
            let updated = item.withPlayed(nextValue)
            replace(updated)
            return updated
        } catch { errorMessage = error.localizedDescription; return nil }
    }

    func loadDetail(for item: MediaItem) async -> MediaItem? {
        guard let session, let client else { return nil }
        similarItems = []
        specialFeatures = []
        albumTracks = []
        let loaded = try? await client.item(userID: session.userID, itemID: item.id)
        let detail = loaded ?? item
        async let similar = client.similarItems(userID: session.userID, itemID: detail.id)
        async let special = client.specialFeatures(userID: session.userID, itemID: detail.id)
        similarItems = (try? await similar) ?? []
        specialFeatures = (try? await special) ?? []
        if detail.type == "MusicAlbum" {
            albumTracks = (try? await client.audioTracks(userID: session.userID, albumID: detail.id)) ?? []
        }
        return detail
    }

    func mediaSegments(for itemID: String) async -> [MediaSegment] {
        guard let client else { return [] }
        return (try? await client.mediaSegments(itemID: itemID)) ?? []
    }

    func chapters(for itemID: String) async -> [Chapter] {
        guard let session, let client else { return [] }
        let item = try? await client.item(userID: session.userID, itemID: itemID)
        return item?.chapters ?? []
    }

    func loadSeriesContext(for item: MediaItem) async {
        guard let session, let client else { return }
        let seriesID: String
        switch item.type {
        case "Series": seriesID = item.id
        case "Season", "Episode":
            guard let value = item.seriesId else { seriesSeasons = []; seriesEpisodes = []; return }
            seriesID = value
        default:
            seriesSeasons = []
            seriesEpisodes = []
            return
        }
        activeSeriesID = seriesID
        isSeriesLoading = true
        defer { isSeriesLoading = false }
        seriesSeasons = (try? await client.seasons(userID: session.userID, seriesID: seriesID)) ?? []
        if let currentSeasonID = item.type == "Season" ? item.id : item.seasonId,
           let index = seriesSeasons.firstIndex(where: { $0.id == currentSeasonID }) {
            selectedSeasonIndex = index
        } else {
            selectedSeasonIndex = min(selectedSeasonIndex, max(0, seriesSeasons.count - 1))
        }
        let seasonID = seriesSeasons.indices.contains(selectedSeasonIndex) ? seriesSeasons[selectedSeasonIndex].id : nil
        seriesEpisodes = (try? await client.episodes(userID: session.userID, seriesID: seriesID, seasonID: seasonID)) ?? []
    }

    func selectSeason(_ index: Int) async {
        guard index >= 0, index < seriesSeasons.count, let session, let client else { return }
        selectedSeasonIndex = index
        guard let seriesID = activeSeriesID else { return }
        seriesEpisodes = (try? await client.episodes(userID: session.userID, seriesID: seriesID, seasonID: seriesSeasons[index].id)) ?? []
    }

    func nextEpisode(after item: MediaItem) async -> MediaItem? {
        guard let session, let client,
              let seriesID = item.seriesId,
              let seasonID = item.seasonId,
              let currentNumber = item.indexNumber else { return nil }

        let currentEpisodes = (try? await client.episodes(userID: session.userID, seriesID: seriesID, seasonID: seasonID)) ?? []
        if let next = currentEpisodes
            .filter({ ($0.indexNumber ?? Int.max) > currentNumber })
            .sorted(by: episodeOrder)
            .first {
            return next
        }

        let seasons = ((try? await client.seasons(userID: session.userID, seriesID: seriesID)) ?? [])
            .sorted(by: episodeOrder)
        guard let currentSeasonIndex = seasons.firstIndex(where: { $0.id == seasonID }) else { return nil }
        for season in seasons.dropFirst(currentSeasonIndex + 1) {
            let episodes = (try? await client.episodes(userID: session.userID, seriesID: seriesID, seasonID: season.id)) ?? []
            if let first = episodes.sorted(by: episodeOrder).first {
                return first
            }
        }
        return nil
    }

    private func episodeOrder(_ lhs: MediaItem, _ rhs: MediaItem) -> Bool {
        (lhs.indexNumber ?? Int.max) < (rhs.indexNumber ?? Int.max)
    }

    func loadPlaylists() async {
        guard let session, let client else { return }
        isPlaylistLoading = true
        defer { isPlaylistLoading = false }
        playlists = (try? await client.playlists(userID: session.userID)) ?? []
    }

    func addToPlaylist(_ playlist: Playlist, item: MediaItem) async {
        guard let session, let client else { return }
        do {
            try await client.addToPlaylist(playlistID: playlist.id, itemID: item.id, userID: session.userID)
        } catch { errorMessage = error.localizedDescription }
    }

    func createPlaylist(name: String, item: MediaItem) async {
        guard let session, let client else { return }
        let cleanName = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleanName.isEmpty else { return }
        do {
            let created = try await client.createPlaylist(name: cleanName, userID: session.userID, itemID: item.id)
            playlists.insert(created, at: 0)
        } catch { errorMessage = error.localizedDescription }
    }

    func playbackURL(for item: MediaItem) async -> URL? {
        guard let client else { return nil }
        if let session, let prepared = try? await client.preparedPlaybackURL(userID: session.userID, item: item) {
            return prepared
        }
        return await client.playbackURL(for: item)
    }

    func signOut() {
        cancelQuickConnect()
        downloadTasks.values.forEach { $0.cancel() }
        downloadTasks.removeAll()
        sessionStore.clear()
        client = nil
        session = nil
        profile = nil
        serverInfo = nil
        heroItem = nil
        latestItems = []
        resumeItems = []
        nextUpItems = []
        favoriteItems = []
        popularItems = []
        movieItems = []
        seriesItems = []
        libraries = []
        libraryItems = []
        selectedLibrary = nil
        searchItems = []
        searchHints = []
        clearSearchHistory()
        seriesSeasons = []
        seriesEpisodes = []
        activeSeriesID = nil
        playlists = []
        liveChannels = []
        livePrograms = []
        liveRecordings = []
        scheduledLiveProgramIDs = []
        syncPlayGroups = []
        activeSyncPlayGroupID = nil
        stopSyncPlayRealtime()
        state = .signedOut
    }

    private func clearUserScopedContent() {
        profile = nil
        heroItem = nil
        latestItems = []
        resumeItems = []
        nextUpItems = []
        favoriteItems = []
        popularItems = []
        movieItems = []
        seriesItems = []
        libraries = []
        libraryItems = []
        selectedLibrary = nil
        searchItems = []
        searchHints = []
        similarItems = []
        specialFeatures = []
        albumTracks = []
        seriesSeasons = []
        seriesEpisodes = []
        activeSeriesID = nil
        selectedSeasonIndex = 0
        isSeriesLoading = false
        playlists = []
        isPlaylistLoading = false
        liveChannels = []
        livePrograms = []
        liveRecordings = []
        scheduledLiveProgramIDs = []
        isLiveTVLoading = false
        syncPlayGroups = []
        activeSyncPlayGroupID = nil
        stopSyncPlayRealtime()
        isSyncPlayLoading = false
    }

    private func applyProfile(_ value: UserProfile?) {
        profile = value
        guard let configuration = value?.configuration else { return }
        if preferredAudioLanguage.isEmpty, let language = configuration.audioLanguagePreference {
            preferredAudioLanguage = language
        }
        if preferredSubtitleLanguage.isEmpty, let language = configuration.subtitleLanguagePreference {
            preferredSubtitleLanguage = language
        }
    }

    private var canStartOfflineDownloads: Bool {
        OfflineDownloadPolicy.canStart(
            queuePaused: offlineQueuePaused,
            wifiOnly: wifiOnlyDownloads,
            wifiAvailable: isWiFiAvailable
        )
    }

    private func handleNetworkAvailabilityChange(_ available: Bool, wifi: Bool) {
        let wasAvailable = previousNetworkAvailability
        previousNetworkAvailability = available
        isNetworkAvailable = available
        isWiFiAvailable = wifi
        if canStartOfflineDownloads {
            resumeQueuedOfflineDownloads()
        }
        guard available, wasAvailable == false, state == .signedIn else { return }
        Task { @MainActor [weak self] in
            await self?.loadHome()
            await self?.loadLibraries()
            await self?.loadLiveTV()
        }
    }

    private func resumeQueuedOfflineDownloads() {
        offlineDownloads
            .filter { $0.state == .queued && downloadTasks[$0.id] == nil }
            .compactMap { entry in URL(string: entry.sourceURL).map { (entry, $0) } }
            .forEach { entry, url in startOfflineDownload(entry, url: url) }
    }

    private func startOfflineDownload(_ entry: OfflineDownload, url: URL, reattachExisting: Bool = false) {
        downloadTasks[entry.id]?.cancel()
        downloadTasks[entry.id] = Task { [weak self] in
            guard let self else { return }
            let startingEntry = entry.with(
                state: .downloading,
                percent: reattachExisting ? entry.percent : 0,
                error: nil
            )
            self.upsertDownload(startingEntry)
            do {
                let temporaryURL: URL
                let existingURL: URL?
                if reattachExisting {
                    existingURL = try await self.downloadCoordinator.reattachExistingDownload(
                        id: entry.id,
                        sourceURL: url.absoluteString,
                        onProgress: { [weak self] percent, bytesDownloaded, contentLength in
                            Task { @MainActor [weak self] in
                                self?.updateDownloadProgress(id: entry.id, percent: percent, bytesDownloaded: bytesDownloaded, contentLength: contentLength)
                            }
                        }
                    )
                } else {
                    existingURL = nil
                }
                if let existingURL {
                    temporaryURL = existingURL
                } else {
                    temporaryURL = try await self.downloadCoordinator.download(id: entry.id, from: url, resumeData: entry.resumeData) { [weak self] percent, bytesDownloaded, contentLength in
                    Task { @MainActor [weak self] in
                        self?.updateDownloadProgress(id: entry.id, percent: percent, bytesDownloaded: bytesDownloaded, contentLength: contentLength)
                    }
                    }
                }
                try OfflineDownloadStore.move(temporaryURL, to: entry.fileName)
                let completed = self.offlineDownloads.first(where: { $0.id == entry.id }) ?? startingEntry
                self.upsertDownload(completed.with(state: .completed, percent: 100, error: nil))
            } catch let paused as OfflineDownloadPaused {
                if let current = self.offlineDownloads.first(where: { $0.id == entry.id }) {
                    self.upsertDownload(current.withResumeData(paused.resumeData))
                }
                return
            } catch is CancellationError {
                return
            } catch {
                self.upsertDownload(entry.with(state: .failed, percent: 0, error: error.localizedDescription))
            }
            self.downloadTasks[entry.id] = nil
        }
    }

    private func restoreOfflineDownloads() async {
        let interrupted = offlineDownloads.filter { $0.state == .downloading }
        for entry in interrupted {
            guard let url = URL(string: entry.sourceURL) else {
                upsertDownload(entry.with(state: .failed, percent: entry.percent, error: "URL de download inválida."))
                continue
            }
            let queued = entry.with(state: .queued, percent: entry.percent, error: nil)
            upsertDownload(queued)
            if canStartOfflineDownloads {
                startOfflineDownload(queued, url: url, reattachExisting: true)
            }
        }
    }

    private func upsertDownload(_ entry: OfflineDownload) {
        if let index = offlineDownloads.firstIndex(where: { $0.id == entry.id }) {
            offlineDownloads[index] = entry
        } else {
            offlineDownloads.insert(entry, at: 0)
        }
        OfflineDownloadStore.save(offlineDownloads)
    }

    private func updateDownloadProgress(id: String, percent: Int, bytesDownloaded: Int64, contentLength: Int64?) {
        guard let entry = offlineDownloads.first(where: { $0.id == id }), entry.state == .downloading else { return }
        let boundedPercent = min(99, max(0, percent))
        guard entry.percent != boundedPercent || entry.bytesDownloaded != bytesDownloaded || entry.contentLength != contentLength else { return }
        upsertDownload(entry.withProgress(percent: boundedPercent, bytesDownloaded: bytesDownloaded, contentLength: contentLength))
    }

    private func pollQuickConnect(api: APIClient, secret: String) async {
        do {
            for attempt in 0..<100 {
                if attempt > 0 { try await Task.sleep(for: .seconds(3)) }
                try Task.checkCancellation()
                let status = try await api.connectQuickConnect(secret: secret)
                quickConnectSecondsRemaining = max(0, 300 - attempt * 3)
                if status.authenticated {
                    let saved = try await api.authenticateQuickConnect(secret: secret)
                    try sessionStore.save(saved)
                    session = saved
                    password = ""
                    isQuickConnectWaiting = false
                    quickConnectCode = nil
                    quickConnectSecondsRemaining = nil
                    try await loadHome(using: api, userID: saved.userID)
                    try await loadLibraries(using: api, userID: saved.userID)
                    applyProfile(try? await api.userProfile(userID: saved.userID))
                    state = .signedIn
                    return
                }
            }
            isQuickConnectWaiting = false
            quickConnectCode = nil
            quickConnectSecondsRemaining = nil
            errorMessage = "O código Quick Connect expirou. Gere um novo código."
        } catch is CancellationError {
            return
        } catch {
            isQuickConnectWaiting = false
            quickConnectCode = nil
            quickConnectSecondsRemaining = nil
            errorMessage = error.localizedDescription
        }
    }

    func imageURL(for item: MediaItem) async -> URL? {
        await client?.imageURL(for: item)
    }

    func profileImageURL() async -> URL? {
        guard let profile else { return nil }
        return await client?.userImageURL(userID: profile.id, tag: profile.primaryImageTag)
    }

    private func loadHome(using api: APIClient, userID: String) async throws {
        async let latest = api.latestItems(userID: userID)
        async let resume = api.resumeItems(userID: userID)
        async let nextUp = api.nextUpItems(userID: userID)
        async let favorites = api.allFavoriteItems(userID: userID)
        async let popular = api.items(userID: userID, parentID: nil, limit: 16, sortBy: "CommunityRating", sortOrder: "Descending")
        async let movies = api.items(userID: userID, parentID: nil, limit: 16, sortBy: "SortName", sortOrder: "Ascending", includeItemTypes: "Movie")
        async let series = api.items(userID: userID, parentID: nil, limit: 16, sortBy: "SortName", sortOrder: "Ascending", includeItemTypes: "Series")
        latestItems = try await latest
        heroItem = latestItems.first
        resumeItems = try await resume
        nextUpItems = (try? await nextUp) ?? []
        favoriteItems = (try? await favorites) ?? []
        popularItems = (try? await popular)?.items ?? []
        movieItems = (try? await movies)?.items ?? []
        seriesItems = (try? await series)?.items ?? []
    }

    private func loadLibraries(using api: APIClient, userID: String) async throws {
        libraries = try await api.libraries(userID: userID)
    }

    private func replace(_ item: MediaItem) {
        latestItems = latestItems.map { $0.id == item.id ? item : $0 }
        resumeItems = resumeItems.map { $0.id == item.id ? item : $0 }
        nextUpItems = nextUpItems.map { $0.id == item.id ? item : $0 }
        if item.isFavorite {
            if favoriteItems.contains(where: { $0.id == item.id }) {
                favoriteItems = favoriteItems.map { $0.id == item.id ? item : $0 }
            } else {
                favoriteItems.insert(item, at: 0)
            }
        } else {
            favoriteItems.removeAll { $0.id == item.id }
        }
        popularItems = popularItems.map { $0.id == item.id ? item : $0 }
        movieItems = movieItems.map { $0.id == item.id ? item : $0 }
        seriesItems = seriesItems.map { $0.id == item.id ? item : $0 }
        libraryItems = libraryItems.map { $0.id == item.id ? item : $0 }
    }

    private func rememberSearch(_ term: String) {
        searchHistory.removeAll { $0.caseInsensitiveCompare(term) == .orderedSame }
        searchHistory.insert(term, at: 0)
        if searchHistory.count > 10 { searchHistory.removeLast(searchHistory.count - 10) }
        UserDefaults.standard.set(searchHistory, forKey: "search.history")
    }

    private var normalizedURL: URL? {
        var value = serverURL.trimmingCharacters(in: .whitespacesAndNewlines)
        if !value.contains("://") { value = "http://\(value)" }
        guard var components = URLComponents(string: value),
              components.scheme == "http" || components.scheme == "https",
              let url = components.url else { return nil }
        components.path = components.path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        return URL(string: components.string ?? url.absoluteString)
    }

    private var canonicalShareBaseURL: String {
        guard let url = normalizedURL, let host = url.host?.lowercased() else {
            return ""
        }
        let isLoopback = host == "localhost" || host == "127.0.0.1" || host == "::1"
        let octets = host.split(separator: ".").compactMap { Int($0) }
        let isPrivate = octets.count == 4 && (
            octets[0] == 10 ||
            (octets[0] == 172 && (16...31).contains(octets[1])) ||
            (octets[0] == 192 && octets[1] == 168) ||
            (octets[0] == 169 && octets[1] == 254)
        )
        if isLoopback || isPrivate {
            return "http://mulletaflix.duckdns.org:8096"
        }
        return url.absoluteString.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
    }
}

enum OfflineDownloadState: String, Codable {
    case queued, downloading, paused, completed, failed
}

struct OfflineDownload: Codable, Identifiable, Equatable {
    let itemID: String
    let title: String
    let state: OfflineDownloadState
    let percent: Int
    let fileName: String
    let sourceURL: String
    let error: String?
    let bytesDownloaded: Int64
    let contentLength: Int64?
    let resumeData: Data?

    var id: String { itemID }

    init(itemID: String, title: String, state: OfflineDownloadState, percent: Int, fileName: String, sourceURL: String = "", error: String? = nil, bytesDownloaded: Int64 = 0, contentLength: Int64? = nil, resumeData: Data? = nil) {
        self.itemID = itemID
        self.title = title
        self.state = state
        self.percent = percent
        self.fileName = fileName
        self.sourceURL = sourceURL
        self.error = error
        self.bytesDownloaded = bytesDownloaded
        self.contentLength = contentLength
        self.resumeData = resumeData
    }

    private enum CodingKeys: String, CodingKey {
        case itemID, title, state, percent, fileName, sourceURL, error, bytesDownloaded, contentLength, resumeData
    }

    init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        self.init(
            itemID: try values.decode(String.self, forKey: .itemID),
            title: try values.decode(String.self, forKey: .title),
            state: try values.decode(OfflineDownloadState.self, forKey: .state),
            percent: try values.decode(Int.self, forKey: .percent),
            fileName: try values.decode(String.self, forKey: .fileName),
            sourceURL: try values.decodeIfPresent(String.self, forKey: .sourceURL) ?? "",
            error: try values.decodeIfPresent(String.self, forKey: .error),
            bytesDownloaded: try values.decodeIfPresent(Int64.self, forKey: .bytesDownloaded) ?? 0,
            contentLength: try values.decodeIfPresent(Int64.self, forKey: .contentLength),
            resumeData: try values.decodeIfPresent(Data.self, forKey: .resumeData)
        )
    }

    func with(state: OfflineDownloadState, percent: Int, error: String?) -> OfflineDownload {
        OfflineDownload(itemID: itemID, title: title, state: state, percent: percent, fileName: fileName, sourceURL: sourceURL, error: error, bytesDownloaded: bytesDownloaded, contentLength: contentLength, resumeData: resumeData)
    }

    func withProgress(percent: Int, bytesDownloaded: Int64, contentLength: Int64?) -> OfflineDownload {
        OfflineDownload(itemID: itemID, title: title, state: state, percent: percent, fileName: fileName, sourceURL: sourceURL, error: error, bytesDownloaded: bytesDownloaded, contentLength: contentLength, resumeData: resumeData)
    }

    func withResumeData(_ value: Data?) -> OfflineDownload {
        OfflineDownload(itemID: itemID, title: title, state: state, percent: percent, fileName: fileName, sourceURL: sourceURL, error: error, bytesDownloaded: bytesDownloaded, contentLength: contentLength, resumeData: value)
    }
}

private struct OfflineDownloadPaused: Error {
    let resumeData: Data?
}

final class OfflineDownloadCoordinator: NSObject, URLSessionDownloadDelegate, @unchecked Sendable {
    static let shared = OfflineDownloadCoordinator()
    static let sessionIdentifier = "com.mulletaflix.ios.downloads"
    private static let completionLock = NSLock()
    private static var backgroundCompletionHandler: (() -> Void)?

    static func registerBackgroundCompletionHandler(_ handler: @escaping () -> Void) {
        completionLock.lock()
        backgroundCompletionHandler = handler
        completionLock.unlock()
        _ = shared.session
    }

    private let lock = NSLock()
    private lazy var session: URLSession = {
        let configuration = URLSessionConfiguration.background(withIdentifier: Self.sessionIdentifier)
        configuration.sessionSendsLaunchEvents = true
        configuration.isDiscretionary = false
        configuration.waitsForConnectivity = true
        return URLSession(configuration: configuration, delegate: self, delegateQueue: nil)
    }()
    private var continuations: [Int: CheckedContinuation<URL, Error>] = [:]
    private var copiedLocations: [Int: URL] = [:]
    private var progressHandlers: [Int: @Sendable (Int, Int64, Int64?) -> Void] = [:]
    private var taskIDs: [String: Int] = [:]
    private var tasks: [String: URLSessionDownloadTask] = [:]
    private var itemIDs: [Int: String] = [:]
    private var pauseRequested = Set<String>()
    private var pauseResumeData: [String: Data?] = [:]

    func download(id: String, from url: URL, resumeData: Data?, onProgress: @escaping @Sendable (Int, Int64, Int64?) -> Void) async throws -> URL {
        let task = resumeData.flatMap { session.downloadTask(withResumeData: $0) } ?? session.downloadTask(with: url)
        return try await observe(task: task, id: id, onProgress: onProgress, resumeTask: true)
    }

    func reattachExistingDownload(id: String, sourceURL: String, onProgress: @escaping @Sendable (Int, Int64, Int64?) -> Void) async throws -> URL? {
        guard let task = await existingDownloadTask(sourceURL: sourceURL) else { return nil }
        return try await observe(task: task, id: id, onProgress: onProgress, resumeTask: task.state != .running)
    }

    private func observe(task: URLSessionDownloadTask, id: String, onProgress: @escaping @Sendable (Int, Int64, Int64?) -> Void, resumeTask: Bool) async throws -> URL {
        try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<URL, Error>) in
                lock.lock()
                continuations[task.taskIdentifier] = continuation
                progressHandlers[task.taskIdentifier] = onProgress
                taskIDs[id] = task.taskIdentifier
                tasks[id] = task
                itemIDs[task.taskIdentifier] = id
                lock.unlock()
                if resumeTask { task.resume() }
            }
        } onCancel: {
            task.cancel()
        }
    }

    private func existingDownloadTask(sourceURL: String) async -> URLSessionDownloadTask? {
        await withCheckedContinuation { continuation in
            session.getAllTasks { tasks in
                let task = tasks
                    .compactMap { $0 as? URLSessionDownloadTask }
                    .first { $0.originalRequest?.url?.absoluteString == sourceURL }
                continuation.resume(returning: task)
            }
        }
    }

    func pause(id: String) {
        lock.lock()
        guard let task = tasks[id] else {
            lock.unlock()
            return
        }
        pauseRequested.insert(id)
        lock.unlock()
        task.cancel(byProducingResumeData: { [weak self] data in
            guard let self else { return }
            self.lock.lock()
            self.pauseResumeData[id] = data
            self.lock.unlock()
        })
    }

    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask, didWriteData bytesWritten: Int64, totalBytesWritten: Int64, totalBytesExpectedToWrite: Int64) {
        let progress = totalBytesExpectedToWrite > 0 ? Int((Double(totalBytesWritten) / Double(totalBytesExpectedToWrite)) * 100) : 0
        lock.lock()
        let handler = progressHandlers[downloadTask.taskIdentifier]
        lock.unlock()
        handler?(min(99, max(0, progress)), totalBytesWritten, totalBytesExpectedToWrite > 0 ? totalBytesExpectedToWrite : nil)
    }

    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask, didFinishDownloadingTo location: URL) {
        let copyURL = FileManager.default.temporaryDirectory.appendingPathComponent("mulletaflix-\(UUID().uuidString).download")
        do {
            try FileManager.default.copyItem(at: location, to: copyURL)
            lock.lock()
            copiedLocations[downloadTask.taskIdentifier] = copyURL
            lock.unlock()
        } catch {
            complete(downloadTask, result: .failure(error))
        }
    }

    func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
        if let error {
            lock.lock()
            let id = itemIDs[task.taskIdentifier]
            let shouldPause = id.map { pauseRequested.contains($0) } ?? false
            let data = id.flatMap { pauseResumeData[$0] ?? nil }
            lock.unlock()
            if shouldPause, let id {
                complete(task, result: .failure(OfflineDownloadPaused(resumeData: data)))
                lock.lock()
                pauseRequested.remove(id)
                pauseResumeData.removeValue(forKey: id)
                lock.unlock()
            } else if (error as NSError).domain == NSURLErrorDomain && (error as NSError).code == NSURLErrorCancelled {
                complete(task, result: .failure(CancellationError()))
            } else {
                complete(task, result: .failure(error))
            }
        } else if let location = takeCopiedLocation(task.taskIdentifier) {
            complete(task, result: .success(location))
        } else {
            complete(task, result: .failure(APIError.invalidResponse))
        }
    }

    func urlSessionDidFinishEvents(forBackgroundURLSession session: URLSession) {
        Self.completionLock.lock()
        let handler = Self.backgroundCompletionHandler
        Self.backgroundCompletionHandler = nil
        Self.completionLock.unlock()
        handler?()
    }

    private func takeCopiedLocation(_ id: Int) -> URL? {
        lock.lock()
        defer { lock.unlock() }
        return copiedLocations.removeValue(forKey: id)
    }

    private func complete(_ task: URLSessionTask, result: Result<URL, Error>) {
        lock.lock()
        let continuation = continuations.removeValue(forKey: task.taskIdentifier)
        progressHandlers.removeValue(forKey: task.taskIdentifier)
        if let id = itemIDs.removeValue(forKey: task.taskIdentifier) {
            taskIDs.removeValue(forKey: id)
            tasks.removeValue(forKey: id)
        }
        lock.unlock()
        continuation?.resume(with: result)
    }
}

private enum OfflineDownloadStore {
    private static let fileManager = FileManager.default
    private static var directory: URL {
        let base = fileManager.urls(for: .documentDirectory, in: .userDomainMask)[0]
        let directory = base.appendingPathComponent("Downloads", isDirectory: true)
        try? fileManager.createDirectory(at: directory, withIntermediateDirectories: true)
        return directory
    }
    private static var indexURL: URL { directory.appendingPathComponent("index.json") }

    static func load() -> [OfflineDownload] {
        guard let data = try? Data(contentsOf: indexURL), let entries = try? JSONDecoder().decode([OfflineDownload].self, from: data) else { return [] }
        return entries
    }

    static func save(_ entries: [OfflineDownload]) {
        guard let data = try? JSONEncoder().encode(entries) else { return }
        try? data.write(to: indexURL, options: .atomic)
    }

    static func fileName(for item: MediaItem) -> String {
        let safeID = item.id.replacingOccurrences(of: "/", with: "_")
        return "\(safeID).media"
    }

    static func url(for fileName: String) -> URL { directory.appendingPathComponent(fileName) }

    static func move(_ temporaryURL: URL, to fileName: String) throws {
        let destination = url(for: fileName)
        if fileManager.fileExists(atPath: destination.path) { try fileManager.removeItem(at: destination) }
        try fileManager.moveItem(at: temporaryURL, to: destination)
    }

    static func removeFile(_ fileName: String) { try? fileManager.removeItem(at: url(for: fileName)) }
}

/// UDP discovery compatible with the Android client and Jellyfin's LAN contract.
private final class LocalServerDiscovery: @unchecked Sendable {
    private let port: NWEndpoint.Port = 7359
    private let message = Data("who is MulletaFlixServer?".utf8)

    func discover(timeout: TimeInterval = 2.5) async -> [DiscoveredServer] {
        await withTaskGroup(of: [DiscoveredServer].self) { group in
            group.addTask { await self.probe(host: "255.255.255.255", timeout: timeout) }
            group.addTask {
                try? await Task.sleep(for: .milliseconds(750))
                return await self.probe(host: "255.255.255.255", timeout: timeout)
            }
            var merged: [String: DiscoveredServer] = [:]
            for await servers in group {
                for server in servers { merged[server.address.absoluteString] = server }
            }
            return Array(merged.values).sorted { $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending }
        }
    }

    private func probe(host: String, timeout: TimeInterval) async -> [DiscoveredServer] {
        await withCheckedContinuation { continuation in
            let connection = NWConnection(host: NWEndpoint.Host(host), port: port, using: .udp)
            let queue = DispatchQueue(label: "com.mulletaflix.ios.discovery")
            let lock = NSLock()
            var results: [String: DiscoveredServer] = [:]
            var finished = false

            func finish() {
                lock.lock()
                guard !finished else { lock.unlock(); return }
                finished = true
                let value = Array(results.values)
                lock.unlock()
                connection.cancel()
                continuation.resume(returning: value)
            }

            func receive() {
                connection.receiveMessage { data, _, _, _ in
                    if let data, let server = try? JSONDecoder().decode(DiscoveredServer.self, from: data) {
                        lock.lock()
                        results[server.address.absoluteString] = server
                        lock.unlock()
                    }
                    receive()
                }
            }

            connection.stateUpdateHandler = { state in
                if case .ready = state {
                    connection.send(content: message, completion: .contentProcessed { _ in })
                    receive()
                }
            }
            connection.start(queue: queue)
            queue.asyncAfter(deadline: .now() + timeout) { finish() }
        }
    }
}
