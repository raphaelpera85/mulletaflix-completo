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

enum IOSSubtitleColor: String, CaseIterable, Identifiable {
    case white
    case yellow
    case cyan
    case green

    var id: String { rawValue }

    var title: String {
        switch self {
        case .white: return "Branco"
        case .yellow: return "Amarelo"
        case .cyan: return "Ciano"
        case .green: return "Verde"
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
    private(set) var isProfileLoading = false
    private(set) var profileError: String?
    private var profileRequestID = UUID()
    private(set) var heroItem: MediaItem?
    private(set) var latestItems: [MediaItem] = []
    private(set) var resumeItems: [MediaItem] = []
    private(set) var nextUpItems: [MediaItem] = []
    private(set) var favoriteItems: [MediaItem] = []
    private(set) var resumeItemsFromCache = false
    private(set) var favoriteItemsFromCache = false
    private(set) var homeCachedAtEpochMillis: Int64?
    private(set) var isFavoritesLoading = false
    private(set) var favoritesError: String?
    private var favoritesRequestID = UUID()
    private(set) var popularItems: [MediaItem] = []
    private(set) var movieItems: [MediaItem] = []
    private(set) var seriesItems: [MediaItem] = []
    private(set) var isHomeLoading = false
    private(set) var homeError: String?
    private var homeRequestID = UUID()
    private(set) var libraries: [MediaItem] = []
    private(set) var isLibrariesLoading = false
    private(set) var librariesError: String?
    private var librariesRequestID = UUID()
    private(set) var libraryItems: [MediaItem] = []
    private(set) var selectedLibrary: MediaItem?
    private(set) var isLibraryLoading = false
    private(set) var libraryError: String?
    private var libraryRequestID = UUID()
    private(set) var searchItems: [MediaItem] = []
    private(set) var searchTotalMatching: Int?
    private(set) var searchHints: [SearchHint] = []
    private(set) var mediaSuggestions: [MediaSuggestion] = []
    private(set) var searchError: String?
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
    private(set) var selectedPlaylist: Playlist?
    private(set) var playlistItems: [MediaItem] = []
    private(set) var isPlaylistItemsLoading = false
    private(set) var playlistItemsError: String?
    private(set) var isSearching = false
    private(set) var liveChannels: [MediaItem] = []
    private(set) var livePrograms: [MediaItem] = []
    private(set) var liveRecordings: [MediaItem] = []
    private(set) var scheduledLiveProgramIDs: Set<String> = []
    private(set) var scheduledLiveTimerIDs: [String: String] = [:]
    private(set) var schedulingLiveProgramIDs: Set<String> = []
    private(set) var cancellingLiveProgramIDs: Set<String> = []
    private(set) var isLiveTVLoading = false
    private(set) var liveTVError: String?
    private(set) var syncPlayGroups: [SyncPlayGroup] = []
    private(set) var activeSyncPlayGroupID: String?
    private(set) var isSyncPlayLoading = false
    private(set) var syncPlayError: String?
    private(set) var isSyncPlaySubmitting = false
    private(set) var syncPlayRealtimeStatus = "Desconectado"
    private(set) var lastSyncPlayCommand: SyncPlayCommand?
    private(set) var syncPlayCommandRevision = 0
    private(set) var remotePlaybackSessions: [RemotePlaybackSession] = []
    private(set) var isRemotePlaybackLoading = false
    private(set) var remotePlaybackError: String?
    private(set) var offlineDownloads: [OfflineDownload] = []
    private(set) var pendingPlaybackIssues: [QueuedPlaybackIssue] = []
    private(set) var pendingPlaybackIssuesUnreadable = false
    private(set) var seasonDownloadProgress: SeasonDownloadProgress?
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
    private(set) var serverHealth: String?
    private(set) var branding: BrandingOptions?
    private(set) var pendingDeepLinkItemID: String?
    private(set) var deepLinkRevision = 0
    private(set) var isVerifyingServer = false
    private(set) var isNetworkAvailable: Bool?
    private(set) var isWiFiAvailable: Bool?
    private(set) var isMeteredNetwork = false
    private(set) var isRegistering = false
    private(set) var registrationError: String?
    private(set) var publicUsers: [PublicUser] = []
    private(set) var isSwitchingUser = false
    private(set) var savedServers: [SavedServer]
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
    var defaultQuality = PlaybackQualityPolicy.normalizedPreference(UserDefaults.standard.string(forKey: "settings.defaultQuality")) {
        didSet {
            defaultQuality = PlaybackQualityPolicy.normalizedPreference(defaultQuality)
            UserDefaults.standard.set(defaultQuality, forKey: "settings.defaultQuality")
        }
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
    var automaticIntroSkip = UserDefaults.standard.object(forKey: "settings.automaticIntroSkip") as? Bool ?? false {
        didSet { UserDefaults.standard.set(automaticIntroSkip, forKey: "settings.automaticIntroSkip") }
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
    var subtitleFontSize = SubtitleAppearancePolicy.normalizedFontSize(UserDefaults.standard.object(forKey: "settings.subtitleFontSize") as? Double ?? 20) {
        didSet {
            subtitleFontSize = SubtitleAppearancePolicy.normalizedFontSize(subtitleFontSize)
            UserDefaults.standard.set(subtitleFontSize, forKey: "settings.subtitleFontSize")
        }
    }
    var subtitleColor = IOSSubtitleColor(rawValue: UserDefaults.standard.string(forKey: "settings.subtitleColor") ?? "white") ?? .white {
        didSet { UserDefaults.standard.set(subtitleColor.rawValue, forKey: "settings.subtitleColor") }
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
    var libraryRatingFilter = UserDefaults.standard.string(forKey: "settings.libraryRatingFilter") ?? "" {
        didSet { UserDefaults.standard.set(libraryRatingFilter, forKey: "settings.libraryRatingFilter") }
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
    private var seasonDownloadTask: Task<Void, Never>?
    private var searchRequestID = UUID()
    private var mediaSuggestionTask: Task<Void, Never>?
    private var mediaSuggestionGeneration = 0
    private var publicUsersRequestID = UUID()
    private var registrationMutationID = UUID()
    private var detailRequestID = UUID()
    private var seriesRequestID = UUID()
    private var liveTVRequestID = UUID()
    private var liveTVTimerMutationID = UUID()
    private var playlistsRequestID = UUID()
    private var playlistItemsRequestID = UUID()
    private var syncPlayRequestID = UUID()
    private var remotePlaybackRequestID = UUID()
    private var remotePlaybackBusySessionIDs = Set<String>()
    private var serverDiscoveryRequestID = UUID()
    private var previousNetworkAvailability: Bool?
    private var isRefreshingAuthenticatedContent = false
    private var sessionMutationID = UUID()
    private var serverVerificationID = UUID()
    private static let savedServersKey = "auth.savedServers"

    private static func ownerKey(for session: UserSession) -> String {
        OfflineDownloadScope.ownerKey(serverURL: session.serverURL, userID: session.userID)
    }

    private var downloadOwnerKey: String? {
        session.map { Self.ownerKey(for: $0) }
    }

    private func isCurrentSession(userID: String, serverURL: URL?) -> Bool {
        guard let currentSession = session, currentSession.userID == userID else { return false }
        return serverURL == nil || currentSession.serverURL == serverURL
    }

    init(sessionStore: SessionStore = KeychainSessionStore()) {
        self.sessionStore = sessionStore
        savedServers = Self.loadSavedServers()
        if let saved = sessionStore.load() {
            session = saved
            serverURL = saved.serverURL.absoluteString
            client = APIClient(serverURL: saved.serverURL, urlSession: .shared, deviceID: "ios-session")
            rememberServer(url: saved.serverURL, info: nil)
            state = .signedIn
        }
        offlineDownloads = session.map { OfflineDownloadStore.load(ownerKey: Self.ownerKey(for: $0)) } ?? []
        loadPendingPlaybackIssues(for: session)
        searchHistory = session.map { SearchHistoryStore.load(ownerKey: Self.ownerKey(for: $0)) } ?? []
        connectivityMonitor.pathUpdateHandler = { [weak self] path in
            let available = path.status == .satisfied
            let wifi = path.status == .satisfied && path.usesInterfaceType(.wifi)
            let metered = path.status == .satisfied &&
                (path.usesInterfaceType(.cellular) || path.isConstrained)
            Task { @MainActor [weak self] in
                self?.handleNetworkAvailabilityChange(available, wifi: wifi, metered: metered)
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
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            state = .signedOut
            errorMessage = NetworkRequestPolicy.offlineMessage
            return
        }
        let mutationID = UUID()
        sessionMutationID = mutationID
        searchRequestID = UUID()
        registrationMutationID = UUID()
        isRegistering = false
        serverVerificationID = UUID()
        publicUsersRequestID = UUID()
        guard let url = normalizedURL else { errorMessage = APIError.invalidServerURL.localizedDescription; return }
        state = .loading
        errorMessage = nil
        do {
            let api = APIClient(serverURL: url, deviceID: "ios-session")
            let loadedServerInfo = try await api.publicSystemInfo()
            guard sessionMutationID == mutationID else { return }
            serverInfo = loadedServerInfo
            let loadedBranding = try? await api.brandingConfiguration()
            guard sessionMutationID == mutationID else { return }
            branding = loadedBranding
            let loadedHealth = await healthStatus(from: api)
            guard sessionMutationID == mutationID else { return }
            serverHealth = loadedHealth
            let saved = try await api.authenticate(username: username.trimmingCharacters(in: .whitespacesAndNewlines), password: password)
            guard sessionMutationID == mutationID else { return }
            try sessionStore.save(saved)
            cancelActiveDownloadsForSessionChange()
            rememberServer(url: saved.serverURL, info: serverInfo)
            client = api
            session = saved
            offlineDownloads = OfflineDownloadStore.load(ownerKey: Self.ownerKey(for: saved))
            loadPendingPlaybackIssues(for: saved)
            searchHistory = SearchHistoryStore.load(ownerKey: Self.ownerKey(for: saved))
            password = ""
            let contentRequestID = UUID()
            homeRequestID = contentRequestID
            favoritesRequestID = contentRequestID
            librariesRequestID = contentRequestID
            try await loadHome(using: api, userID: saved.userID, serverURL: saved.serverURL, requestID: contentRequestID)
            guard sessionMutationID == mutationID else { return }
            try await loadLibraries(using: api, userID: saved.userID, requestID: contentRequestID)
            guard sessionMutationID == mutationID else { return }
            let loadedProfile = try? await api.userProfile(userID: saved.userID)
            guard sessionMutationID == mutationID else { return }
            applyProfile(loadedProfile)
            guard sessionMutationID == mutationID else { return }
            state = .signedIn
            await syncPendingPlaybackIssues()
        } catch {
            guard sessionMutationID == mutationID else { return }
            state = .signedOut
            errorMessage = AuthErrorPolicy.authenticationMessage(for: error)
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

    func selectSavedServer(_ server: SavedServer) async {
        publicUsersRequestID = UUID()
        publicUsers = []
        serverURL = server.url
        await verifyServer()
    }

    func removeSavedServer(_ server: SavedServer) {
        savedServers.removeAll { $0.id == server.id }
        persistSavedServers()
    }

    func loadPendingDeepLinkItem() async -> MediaItem? {
        guard let itemID = pendingDeepLinkItemID else { return nil }
        guard let session, let client else { return nil }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else { return nil }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        let loaded = try? await client.item(userID: sessionID, itemID: itemID)
        guard self.session?.userID == sessionID,
              self.session?.serverURL == sessionServerURL else { return nil }
        pendingDeepLinkItemID = nil
        return loaded
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
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            registrationError = NetworkRequestPolicy.offlineMessage
            return false
        }
        guard let url = normalizedURL else {
            registrationError = APIError.invalidServerURL.localizedDescription
            return false
        }

        let mutationID = UUID()
        registrationMutationID = mutationID
        isRegistering = true
        defer {
            if registrationMutationID == mutationID {
                isRegistering = false
            }
        }
        do {
            let api = APIClient(serverURL: url, deviceID: "ios-registration")
            let result = try await api.register(username: cleanUsername, password: password)
            guard registrationMutationID == mutationID else { return false }
            guard result.success else {
                throw APIError.serverMessage(result.message ?? "Não foi possível concluir o cadastro.")
            }
            return true
        } catch {
            guard registrationMutationID == mutationID else { return false }
            registrationError = AuthErrorPolicy.registrationMessage(for: error)
            return false
        }
    }

    func loadPublicUsers() async {
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable),
              let client,
              let endpoint = normalizedURL else { return }
        let requestID = UUID()
        publicUsersRequestID = requestID
        let loadedUsers = try? await client.publicUsers()
        guard publicUsersRequestID == requestID,
              normalizedURL == endpoint,
              session != nil else { return }
        publicUsers = loadedUsers ?? []
    }

    func switchUser(username: String, password: String) async -> Bool {
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            errorMessage = NetworkRequestPolicy.offlineMessage
            return false
        }
        let mutationID = UUID()
        sessionMutationID = mutationID
        searchRequestID = UUID()
        registrationMutationID = UUID()
        isRegistering = false
        serverVerificationID = UUID()
        publicUsersRequestID = UUID()
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
            guard sessionMutationID == mutationID else { return false }
            try sessionStore.save(saved)
            cancelActiveDownloadsForSessionChange()
            rememberServer(url: saved.serverURL, info: serverInfo)
            client = api
            session = saved
            offlineDownloads = OfflineDownloadStore.load(ownerKey: Self.ownerKey(for: saved))
            loadPendingPlaybackIssues(for: saved)
            self.password = ""
            clearUserScopedContent()
            searchHistory = SearchHistoryStore.load(ownerKey: Self.ownerKey(for: saved))
            let contentRequestID = UUID()
            homeRequestID = contentRequestID
            favoritesRequestID = contentRequestID
            librariesRequestID = contentRequestID
            try await loadHome(using: api, userID: saved.userID, serverURL: saved.serverURL, requestID: contentRequestID)
            guard sessionMutationID == mutationID else { return false }
            try await loadLibraries(using: api, userID: saved.userID, requestID: contentRequestID)
            guard sessionMutationID == mutationID else { return false }
            let loadedProfile = try? await api.userProfile(userID: saved.userID)
            guard sessionMutationID == mutationID else { return false }
            applyProfile(loadedProfile)
            guard sessionMutationID == mutationID else { return false }
            state = .signedIn
            await syncPendingPlaybackIssues()
            return true
        } catch {
            guard sessionMutationID == mutationID else { return false }
            errorMessage = AuthErrorPolicy.authenticationMessage(for: error)
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
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            errorMessage = NetworkRequestPolicy.offlineMessage
            return
        }
        guard let url = normalizedURL else { errorMessage = APIError.invalidServerURL.localizedDescription; return }
        let verificationID = UUID()
        serverVerificationID = verificationID
        isVerifyingServer = true
        defer {
            if serverVerificationID == verificationID { isVerifyingServer = false }
        }
        do {
            let api = APIClient(serverURL: url, deviceID: "ios-session")
            let loadedServerInfo = try await api.publicSystemInfo()
            guard serverVerificationID == verificationID else { return }
            serverInfo = loadedServerInfo
            let loadedBranding = try? await api.brandingConfiguration()
            guard serverVerificationID == verificationID else { return }
            branding = loadedBranding
            let loadedHealth = await healthStatus(from: api)
            guard serverVerificationID == verificationID else { return }
            serverHealth = loadedHealth
            errorMessage = nil
        } catch {
            guard serverVerificationID == verificationID else { return }
            serverInfo = nil
            serverHealth = nil
            branding = nil
            errorMessage = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func startQuickConnect() async {
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            errorMessage = NetworkRequestPolicy.offlineMessage
            return
        }
        guard let url = normalizedURL else { errorMessage = APIError.invalidServerURL.localizedDescription; return }
        cancelQuickConnect()
        let mutationID = UUID()
        sessionMutationID = mutationID
        searchRequestID = UUID()
        registrationMutationID = UUID()
        isRegistering = false
        serverVerificationID = UUID()
        publicUsersRequestID = UUID()
        state = .loading
        errorMessage = nil
        do {
            let api = APIClient(serverURL: url, deviceID: "ios-session")
            let loadedServerInfo = try await api.publicSystemInfo()
            guard sessionMutationID == mutationID else { return }
            serverInfo = loadedServerInfo
            let loadedBranding = try? await api.brandingConfiguration()
            guard sessionMutationID == mutationID else { return }
            branding = loadedBranding
            let loadedHealth = await healthStatus(from: api)
            guard sessionMutationID == mutationID else { return }
            serverHealth = loadedHealth
            let available = try await api.isQuickConnectEnabled()
            guard sessionMutationID == mutationID else { return }
            isQuickConnectAvailable = available
            guard available else {
                state = .signedOut
                errorMessage = "Quick Connect está desativado neste servidor."
                return
            }
            let challenge = try await api.initiateQuickConnect()
            guard sessionMutationID == mutationID else { return }
            client = api
            quickConnectCode = challenge.code
            quickConnectSecondsRemaining = 300
            isQuickConnectWaiting = true
            state = .signedOut
            quickConnectTask = Task { [weak self] in
                guard let self else { return }
                await self.pollQuickConnect(api: api, secret: challenge.secret, mutationID: mutationID)
            }
        } catch {
            guard sessionMutationID == mutationID else { return }
            state = .signedOut
            errorMessage = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func cancelQuickConnect() {
        sessionMutationID = UUID()
        quickConnectTask?.cancel()
        quickConnectTask = nil
        quickConnectCode = nil
        quickConnectSecondsRemaining = nil
        isQuickConnectWaiting = false
        if state == .loading { state = .signedOut }
    }

    func loadHome() async {
        guard let session, let client else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            let hasSnapshot = applyHomeSnapshotIfAvailable(ownerKey: Self.ownerKey(for: session))
            homeError = hasSnapshot ? nil : NetworkRequestPolicy.offlineMessage
            return
        }
        let requestID = UUID()
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        homeRequestID = requestID
        favoritesRequestID = requestID
        isHomeLoading = true
        homeError = nil
        resumeItemsFromCache = false
        favoriteItemsFromCache = false
        homeCachedAtEpochMillis = nil
        defer {
            if homeRequestID == requestID {
                isHomeLoading = false
            }
        }
        do {
            try await loadHome(using: client, userID: sessionID, serverURL: sessionServerURL, requestID: requestID)
            guard homeRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            homeError = nil
        } catch {
            guard homeRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            if HomeSnapshotPolicy.shouldUseCache(for: error) {
                applyHomeSnapshotIfAvailable(ownerKey: Self.ownerKey(for: session))
            }
            homeError = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func loadLibraries() async {
        guard let session, let client else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            librariesError = NetworkRequestPolicy.offlineMessage
            return
        }
        let requestID = UUID()
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        librariesRequestID = requestID
        isLibrariesLoading = true
        librariesError = nil
        defer {
            if librariesRequestID == requestID {
                isLibrariesLoading = false
            }
        }
        do {
            try await loadLibraries(using: client, userID: sessionID, requestID: requestID)
            guard librariesRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            librariesError = nil
        } catch {
            guard librariesRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            librariesError = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func loadFavorites() async {
        guard let session, let client else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            if let snapshot = HomeSnapshotStore.load(ownerKey: Self.ownerKey(for: session)),
               snapshot.favoritesSavedAtEpochMillis > 0 {
                favoriteItems = snapshot.favoriteItems.map(\.mediaItem)
                favoriteItemsFromCache = true
                homeCachedAtEpochMillis = snapshot.favoritesSavedAtEpochMillis
                favoritesError = nil
            } else {
                favoritesError = NetworkRequestPolicy.offlineMessage
            }
            return
        }
        let requestID = UUID()
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        favoritesRequestID = requestID
        isFavoritesLoading = true
        favoritesError = nil
        favoriteItemsFromCache = false
        defer {
            if favoritesRequestID == requestID {
                isFavoritesLoading = false
            }
        }
        do {
            let loadedFavorites = try await client.allFavoriteItems(userID: sessionID)
            guard favoritesRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            favoriteItems = loadedFavorites
            HomeSnapshotStore.save(resumeItems: nil, favoriteItems: loadedFavorites, ownerKey: Self.ownerKey(for: session))
        } catch {
            guard favoritesRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            if HomeSnapshotPolicy.shouldUseCache(for: error),
               let snapshot = HomeSnapshotStore.load(ownerKey: Self.ownerKey(for: session)),
               snapshot.favoritesSavedAtEpochMillis > 0 {
                favoriteItems = snapshot.favoriteItems.map(\.mediaItem)
                favoriteItemsFromCache = true
                homeCachedAtEpochMillis = snapshot.favoritesSavedAtEpochMillis
                return
            }
            favoritesError = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func loadProfile() async {
        guard let session, let client else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            profileError = NetworkRequestPolicy.offlineMessage
            return
        }
        let requestID = UUID()
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        profileRequestID = requestID
        isProfileLoading = true
        profileError = nil
        defer {
            if profileRequestID == requestID {
                isProfileLoading = false
            }
        }
        do {
            let loadedProfile = try await client.userProfile(userID: sessionID)
            guard profileRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            applyProfile(loadedProfile)
        } catch {
            guard profileRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            profileError = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func loadLiveTV(background: Bool = false) async {
        guard !background || LiveTVRefreshPolicy.shouldStartBackgroundRefresh(isLoading: isLiveTVLoading) else { return }
        guard let session, let client else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            liveTVError = NetworkRequestPolicy.offlineMessage
            return
        }
        let requestID = UUID()
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        let timerMutationID = liveTVTimerMutationID
        liveTVRequestID = requestID
        isLiveTVLoading = true
        liveTVError = nil
        defer {
            if liveTVRequestID == requestID {
                isLiveTVLoading = false
            }
        }
        var firstError: String?
        var channelsForPrograms: [MediaItem] = liveChannels
        do {
            let channels = try await client.allLiveTVChannels(userID: sessionID)
            guard liveTVRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            liveChannels = channels
            channelsForPrograms = channels
        } catch {
            guard liveTVRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            firstError = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
        do {
            let recordings = try await client.allLiveTVRecordings(userID: sessionID)
            guard liveTVRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            liveRecordings = recordings
        } catch {
            guard liveTVRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            firstError = firstError ?? AuthErrorPolicy.serverConnectionMessage(for: error)
        }
        do {
            let scheduled = try await client.scheduledLiveTVTimerIDs()
            guard liveTVRequestID == requestID,
                  liveTVTimerMutationID == timerMutationID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            scheduledLiveTimerIDs = scheduled
            scheduledLiveProgramIDs = Set(scheduled.keys)
        } catch {
            guard liveTVRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            firstError = firstError ?? AuthErrorPolicy.serverConnectionMessage(for: error)
        }
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        let start = formatter.string(from: Date())
        let end = formatter.string(from: Date().addingTimeInterval(24 * 60 * 60))
        do {
            let programs = try await client.allLiveTVPrograms(channelIDs: channelsForPrograms.map(\.id), startDate: start, endDate: end)
            guard liveTVRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            livePrograms = programs
        } catch {
            guard liveTVRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            firstError = firstError ?? AuthErrorPolicy.serverConnectionMessage(for: error)
        }
        guard liveTVRequestID == requestID,
              self.session?.userID == sessionID,
              self.session?.serverURL == sessionServerURL else { return }
        liveTVError = firstError
    }

    func scheduleLiveProgram(_ program: MediaItem) async {
        guard let client, let session,
              !scheduledLiveProgramIDs.contains(program.id),
              !schedulingLiveProgramIDs.contains(program.id) else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            errorMessage = NetworkRequestPolicy.offlineMessage
            return
        }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        let timerMutationID = UUID()
        liveTVTimerMutationID = timerMutationID
        schedulingLiveProgramIDs.insert(program.id)
        defer {
            if self.session?.userID == sessionID, self.session?.serverURL == sessionServerURL {
                schedulingLiveProgramIDs.remove(program.id)
            }
        }
        do {
            try await client.scheduleLiveTV(program: program)
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL,
                  liveTVTimerMutationID == timerMutationID else { return }
            scheduledLiveProgramIDs.insert(program.id)
        } catch {
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL,
                  liveTVTimerMutationID == timerMutationID else { return }
            errorMessage = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func cancelLiveProgram(_ program: MediaItem) async {
        guard let client, let session,
              let timerID = scheduledLiveTimerIDs[program.id],
              !cancellingLiveProgramIDs.contains(program.id) else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            errorMessage = NetworkRequestPolicy.offlineMessage
            return
        }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        let timerMutationID = UUID()
        liveTVTimerMutationID = timerMutationID
        cancellingLiveProgramIDs.insert(program.id)
        defer {
            if self.session?.userID == sessionID, self.session?.serverURL == sessionServerURL {
                cancellingLiveProgramIDs.remove(program.id)
            }
        }
        do {
            try await client.cancelLiveTVTimer(timerID: timerID)
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL,
                  liveTVTimerMutationID == timerMutationID else { return }
            scheduledLiveProgramIDs.remove(program.id)
            scheduledLiveTimerIDs.removeValue(forKey: program.id)
        } catch {
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL,
                  liveTVTimerMutationID == timerMutationID else { return }
            errorMessage = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func enqueueOfflineDownload(for item: MediaItem) async {
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            errorMessage = NetworkRequestPolicy.offlineMessage
            return
        }
        guard let client, let url = await client.playbackURL(for: item, resume: false) else {
            errorMessage = "Este item não possui uma fonte disponível para download."
            return
        }
        enqueueOfflineDownload(for: item, url: url)
    }

    private func enqueueOfflineDownload(for item: MediaItem, url: URL) {
        if offlineDownloads.contains(where: { $0.itemID == item.id && $0.state == .completed }) { return }
        let artworkURL = client?.imageURL(for: item)?.absoluteString
        let subtitles = item.mediaSources.first?.mediaStreams
            .filter(ExternalSubtitlePolicy.isPlayable)
            .compactMap { stream -> OfflineSubtitleEntry? in
                guard let streamIndex = stream.index,
                      let mimeType = ExternalSubtitlePolicy.mimeType(codec: stream.codec, deliveryURL: stream.deliveryURL) else { return nil }
                return OfflineSubtitleEntry(
                    streamIndex: streamIndex,
                    fileName: OfflineDownloadStore.subtitleFileName(itemID: item.id, streamIndex: streamIndex, mimeType: mimeType),
                    mimeType: mimeType,
                    mediaSourceID: item.mediaSources.first?.id,
                    language: stream.language,
                    label: stream.displayTitle ?? stream.title,
                    isDefault: stream.isDefault,
                    isForced: stream.isForced
                )
            } ?? []
        let validEpisode = item.type == "Episode" && item.seriesId != nil && (item.indexNumber ?? 0) > 0
        let entry = OfflineDownload(
            itemID: item.id,
            title: item.name,
            seriesID: validEpisode ? item.seriesId : nil,
            seriesName: validEpisode ? item.seriesName : nil,
            seasonName: validEpisode ? item.seasonName : nil,
            seasonNumber: validEpisode ? item.parentIndexNumber : nil,
            episodeNumber: validEpisode ? item.indexNumber : nil,
            state: .queued,
            percent: 0,
            fileName: OfflineDownloadStore.fileName(for: item),
            sourceURL: url.absoluteString,
            artworkURL: artworkURL,
            subtitles: subtitles
        )
        upsertDownload(entry)
        if let ownerKey = downloadOwnerKey {
            Task { [weak self] in
                await self?.cacheOfflineArtwork(for: item, ownerKey: ownerKey)
            }
        }
        if canStartOfflineDownloads {
            startOfflineDownload(entry, url: url)
        }
    }

    func downloadSelectedSeason() {
        guard seasonDownloadTask?.isActive != true,
              let session,
              seriesSeasons.indices.contains(selectedSeasonIndex) else { return }
        let season = seriesSeasons[selectedSeasonIndex]
        let episodes = SeasonDownloadPolicy.eligibleEpisodes(seriesEpisodes)
        guard !episodes.isEmpty else {
            errorMessage = "Não há episódios disponíveis nesta temporada."
            return
        }

        let activeItemIDs = Set(offlineDownloads.compactMap { entry in
            entry.state == .failed ? nil : entry.itemID
        })
        let pendingEpisodes = SeasonDownloadPolicy.pendingEpisodes(episodes, activeItemIDs: activeItemIDs)
        let initialSkipped = episodes.count - pendingEpisodes.count
        seasonDownloadProgress = SeasonDownloadProgress(
            seasonID: season.id,
            seasonName: season.name,
            totalEpisodes: episodes.count,
            processedEpisodes: initialSkipped,
            alreadyAvailableEpisodes: initialSkipped,
            isRunning: !pendingEpisodes.isEmpty
        )
        guard !pendingEpisodes.isEmpty else {
            errorMessage = "Todos os episódios desta temporada já estão na fila ou disponíveis offline."
            return
        }

        let userID = session.userID
        let sessionServerURL = session.serverURL
        seasonDownloadTask = Task { [weak self] in
            guard let self else { return }
            var processed = initialSkipped
            var queued = 0
            var failed = 0

            for episode in pendingEpisodes {
                guard !Task.isCancelled,
                      self.session?.userID == userID,
                      self.session?.serverURL == sessionServerURL else { break }
                if self.offlineDownloads.contains(where: { $0.itemID == episode.id && $0.state != .failed }) {
                    processed += 1
                    continue
                }

                if let url = await self.playbackURL(for: episode, resume: false) {
                    self.enqueueOfflineDownload(for: episode, url: url)
                    queued += 1
                } else {
                    failed += 1
                }
                processed += 1
                self.seasonDownloadProgress = SeasonDownloadProgress(
                    seasonID: season.id,
                    seasonName: season.name,
                    totalEpisodes: episodes.count,
                    processedEpisodes: processed,
                    queuedEpisodes: queued,
                    alreadyAvailableEpisodes: initialSkipped,
                    failedEpisodes: failed,
                    isRunning: processed < episodes.count
                )
            }

            guard self.session?.userID == userID,
                  self.session?.serverURL == sessionServerURL else { return }
            let cancelled = Task.isCancelled
            self.seasonDownloadProgress = SeasonDownloadProgress(
                seasonID: season.id,
                seasonName: season.name,
                totalEpisodes: episodes.count,
                processedEpisodes: processed,
                queuedEpisodes: queued,
                alreadyAvailableEpisodes: initialSkipped,
                failedEpisodes: failed,
                isRunning: false,
                isCancelled: cancelled
            )
            if cancelled {
                self.errorMessage = "Preparação cancelada. Os episódios já adicionados permanecem na fila."
            } else {
                self.errorMessage = "Temporada \(season.name): \(queued) episódio(s) adicionado(s) à fila, \(initialSkipped) já disponível(is) e \(failed) falha(s)."
            }
            self.seasonDownloadTask = nil
        }
    }

    func cancelSeasonDownload() {
        seasonDownloadTask?.cancel()
    }

    func retryOfflineDownload(_ entry: OfflineDownload) {
        guard let url = URL(string: entry.sourceURL),
              url.scheme == "http" || url.scheme == "https" else {
            errorMessage = "Não é possível baixar novamente: a URL persistida deste item é inválida."
            return
        }
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
        guard let ownerKey = downloadOwnerKey else { return }
        OfflineDownloadStore.removeFile(entry.fileName, ownerKey: ownerKey)
        OfflineDownloadStore.removeArtwork(itemID: entry.itemID, ownerKey: ownerKey)
        OfflineDownloadStore.removeSubtitleFiles(entry.subtitles, ownerKey: ownerKey)
        clearOfflinePlaybackPosition(for: entry.itemID)
        offlineDownloads.removeAll { $0.id == entry.id }
        OfflineDownloadStore.save(offlineDownloads, ownerKey: ownerKey)
    }

    func removeCompletedOfflineDownloads() {
        let completed = offlineDownloads.filter { $0.state == .completed }
        guard let ownerKey = downloadOwnerKey else { return }
        completed.forEach {
            OfflineDownloadStore.removeFile($0.fileName, ownerKey: ownerKey)
            OfflineDownloadStore.removeArtwork(itemID: $0.itemID, ownerKey: ownerKey)
            OfflineDownloadStore.removeSubtitleFiles($0.subtitles, ownerKey: ownerKey)
            clearOfflinePlaybackPosition(for: $0.itemID)
        }
        offlineDownloads.removeAll { $0.state == .completed }
        OfflineDownloadStore.save(offlineDownloads, ownerKey: ownerKey)
    }

    func removeFailedOfflineDownloads() {
        offlineDownloads.removeAll { entry in
            guard entry.state == .failed else { return false }
            guard let ownerKey = downloadOwnerKey else { return false }
            OfflineDownloadStore.removeFile(entry.fileName, ownerKey: ownerKey)
            OfflineDownloadStore.removeArtwork(itemID: entry.itemID, ownerKey: ownerKey)
            OfflineDownloadStore.removeSubtitleFiles(entry.subtitles, ownerKey: ownerKey)
            clearOfflinePlaybackPosition(for: entry.itemID)
            return true
        }
        if let ownerKey = downloadOwnerKey { OfflineDownloadStore.save(offlineDownloads, ownerKey: ownerKey) }
    }

    func retryFailedOfflineDownloads() {
        offlineDownloads
            .filter { $0.state == .failed }
            .forEach(retryOfflineDownload)
    }

    var offlineDownloadedBytes: Int64 {
        offlineDownloads
            .filter { $0.state == .completed }
            .reduce(0) { $0 + max(0, $1.bytesDownloaded) }
    }

    var availableStorageBytes: Int64? {
        let values = try? URL(fileURLWithPath: NSHomeDirectory()).resourceValues(
            forKeys: [.volumeAvailableCapacityForImportantUsageKey]
        )
        return values?.volumeAvailableCapacityForImportantUsage
    }

    func localURL(for item: MediaItem) -> URL? {
        guard let entry = offlineDownloads.first(where: { $0.itemID == item.id && $0.state == .completed }) else { return nil }
        guard let ownerKey = downloadOwnerKey else { return nil }
        return OfflineDownloadStore.existingURL(for: entry.fileName, ownerKey: ownerKey)
    }

    func offlineArtworkURL(for entry: OfflineDownload) -> URL? {
        guard let ownerKey = downloadOwnerKey else { return nil }
        return OfflineDownloadStore.artworkURL(itemID: entry.itemID, ownerKey: ownerKey)
    }

    func offlineSubtitleData(itemID: String, streamIndex: Int) -> Data? {
        guard let entry = offlineDownloads.first(where: { $0.itemID == itemID && $0.state == .completed }),
              let subtitle = entry.subtitles.first(where: { $0.streamIndex == streamIndex }),
              let ownerKey = downloadOwnerKey else { return nil }
        return try? Data(contentsOf: OfflineDownloadStore.subtitleURL(fileName: subtitle.fileName, ownerKey: ownerKey))
    }

    func offlineSubtitleStreams(for itemID: String) -> [MediaStream] {
        offlineDownloads
            .first(where: { $0.itemID == itemID && $0.state == .completed })?
            .subtitles
            .map(\.mediaStream) ?? []
    }

    func offlinePlaybackPosition(for itemID: String) -> Int64 {
        guard let ownerKey = downloadOwnerKey else { return 0 }
        return Int64(UserDefaults.standard.integer(forKey: OfflinePlaybackPositionScope.key(ownerKey: ownerKey, itemID: itemID)))
    }

    func saveOfflinePlaybackPosition(itemID: String, positionSeconds: Double, durationSeconds: Double? = nil) {
        guard let ownerKey = downloadOwnerKey,
              let normalizedPosition = OfflinePlaybackPositionPolicy.normalized(
                  positionSeconds: positionSeconds,
                  durationSeconds: durationSeconds
              ) else { return }
        let ticks = PlaybackTicksPolicy.fromSeconds(normalizedPosition)
        UserDefaults.standard.set(ticks, forKey: OfflinePlaybackPositionScope.key(ownerKey: ownerKey, itemID: itemID))
    }

    func clearOfflinePlaybackPosition(for itemID: String) {
        guard let ownerKey = downloadOwnerKey else { return }
        UserDefaults.standard.removeObject(forKey: OfflinePlaybackPositionScope.key(ownerKey: ownerKey, itemID: itemID))
    }

    func reportPlaybackStart(itemID: String, mediaSourceID: String?, positionSeconds: Double = 0) async {
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable), let client else { return }
        let ticks = PlaybackTicksPolicy.fromSeconds(positionSeconds)
        try? await client.reportPlaybackStart(itemID: itemID, mediaSourceID: mediaSourceID, positionTicks: ticks)
    }

    func reportPlaybackProgress(itemID: String, mediaSourceID: String?, positionSeconds: Double, durationSeconds: Double? = nil, isPaused: Bool) async {
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable), let client else { return }
        let ticks = PlaybackTicksPolicy.fromSeconds(positionSeconds, durationSeconds: durationSeconds)
        try? await client.reportPlaybackProgress(itemID: itemID, mediaSourceID: mediaSourceID, positionTicks: ticks, isPaused: isPaused)
    }

    func reportPlaybackStopped(itemID: String, mediaSourceID: String?, positionSeconds: Double, durationSeconds: Double? = nil) async {
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable), let client else { return }
        let ticks = PlaybackTicksPolicy.fromSeconds(positionSeconds, durationSeconds: durationSeconds)
        try? await client.reportPlaybackStopped(itemID: itemID, mediaSourceID: mediaSourceID, positionTicks: ticks)
    }

    func loadSyncPlay(background: Bool = false) async {
        guard let client, let session else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            syncPlayError = NetworkRequestPolicy.offlineMessage
            return
        }
        if background && !SyncPlayRefreshPolicy.shouldStartBackgroundRefresh(isLoading: isSyncPlayLoading) { return }
        let requestID = UUID()
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        syncPlayRequestID = requestID
        isSyncPlayLoading = true
        defer {
            if syncPlayRequestID == requestID { isSyncPlayLoading = false }
        }
        do {
            let groups = try await client.syncPlayGroups()
            guard syncPlayRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            syncPlayGroups = groups
            syncPlayError = nil
        } catch {
            guard syncPlayRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            syncPlayError = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func createSyncPlayGroup(name: String) async {
        guard let client, let session else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            errorMessage = NetworkRequestPolicy.offlineMessage
            return
        }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        do {
            try await client.createSyncPlayGroup(name: name.trimmingCharacters(in: .whitespacesAndNewlines))
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            await loadSyncPlay()
        } catch {
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            errorMessage = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func joinSyncPlayGroup(_ group: SyncPlayGroup) async {
        guard let client, let session else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            errorMessage = NetworkRequestPolicy.offlineMessage
            return
        }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        do {
            try await client.joinSyncPlayGroup(id: group.groupId)
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            activeSyncPlayGroupID = group.groupId
            startSyncPlayRealtime()
            await loadSyncPlay()
        } catch {
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            errorMessage = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func leaveSyncPlayGroup() async {
        guard let client, let session else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            errorMessage = NetworkRequestPolicy.offlineMessage
            return
        }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        do {
            try await client.leaveSyncPlayGroup()
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            stopSyncPlayRealtime()
            activeSyncPlayGroupID = nil
            await loadSyncPlay()
        } catch {
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            errorMessage = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
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

    func sendSyncPlayCommand(_ command: SyncPlayPlaybackCommand) async {
        guard activeSyncPlayGroupID != nil, let client, let session else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            errorMessage = NetworkRequestPolicy.offlineMessage
            return
        }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        isSyncPlaySubmitting = true
        defer { isSyncPlaySubmitting = false }
        do {
            try await client.sendSyncPlayCommand(command)
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
        } catch {
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            errorMessage = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func loadRemotePlayback(background: Bool = false) async {
        guard let client, let session else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            remotePlaybackError = NetworkRequestPolicy.offlineMessage
            return
        }
        if background && !RemotePlaybackRefreshPolicy.shouldStartBackgroundRefresh(isLoading: isRemotePlaybackLoading) { return }
        let requestID = UUID()
        let sessionServerURL = session.serverURL
        remotePlaybackRequestID = requestID
        isRemotePlaybackLoading = true
        defer {
            if remotePlaybackRequestID == requestID { isRemotePlaybackLoading = false }
        }
        do {
            let sessions = try await client.remotePlaybackSessions(userID: session.userID)
            guard remotePlaybackRequestID == requestID,
                  self.session?.userID == session.userID,
                  self.session?.serverURL == sessionServerURL else { return }
            remotePlaybackSessions = sessions
            remotePlaybackError = nil
        } catch {
            guard remotePlaybackRequestID == requestID,
                  self.session?.userID == session.userID,
                  self.session?.serverURL == sessionServerURL else { return }
            remotePlaybackSessions = []
            remotePlaybackError = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func isRemotePlaybackBusy(_ sessionID: String) -> Bool {
        remotePlaybackBusySessionIDs.contains(sessionID)
    }

    func sendRemotePlaybackCommand(
        _ command: RemotePlaybackCommand,
        to remoteSession: RemotePlaybackSession,
        seekPositionTicks: Int64? = nil
    ) async {
        guard let client, let session, !remotePlaybackBusySessionIDs.contains(remoteSession.id) else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            remotePlaybackError = NetworkRequestPolicy.offlineMessage
            return
        }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        remotePlaybackBusySessionIDs.insert(remoteSession.id)
        defer { remotePlaybackBusySessionIDs.remove(remoteSession.id) }
        do {
            try await client.sendRemotePlaybackCommand(
                sessionID: remoteSession.id,
                command: command,
                userID: sessionID,
                seekPositionTicks: seekPositionTicks
            )
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            await loadRemotePlayback()
        } catch {
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            remotePlaybackError = AuthErrorPolicy.serverConnectionMessage(for: error)
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
        let requestID = UUID()
        serverDiscoveryRequestID = requestID
        let requestedServerURL = serverURL.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        let requestedSavedServerID = savedServers.first {
            $0.url.caseInsensitiveCompare(requestedServerURL) == .orderedSame
        }?.serverID
        isDiscoveringServers = true
        defer {
            if serverDiscoveryRequestID == requestID {
                isDiscoveringServers = false
            }
        }
        let loadedServers = await LocalServerDiscovery().discover()
        guard serverDiscoveryRequestID == requestID,
              serverURL.trimmingCharacters(in: CharacterSet(charactersIn: "/")) == requestedServerURL else { return }
        discoveredServers = loadedServers
        if let server = ServerDiscoverySelectionPolicy.selectedServer(
            from: loadedServers,
            savedServerID: requestedSavedServerID
        ) {
            serverURL = server.address.absoluteString
        }
    }

    func search(_ term: String, filter: SearchFilter = .all) async {
        let requestID = UUID()
        searchRequestID = requestID
        let cleanTerm = term.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleanTerm.isEmpty, let session, let client else {
            searchItems = []
            searchTotalMatching = nil
            searchHints = []
            searchError = nil
            return
        }
        guard cleanTerm.count >= 2 else {
            searchItems = []
            searchTotalMatching = nil
            searchHints = []
            searchError = nil
            return
        }
        guard SearchNetworkPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            searchItems = []
            searchTotalMatching = nil
            searchHints = []
            searchError = SearchNetworkPolicy.offlineMessage
            return
        }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        isSearching = true
        searchError = nil
        defer {
            if searchRequestID == requestID {
                isSearching = false
            }
        }
        async let hints = client.searchHints(
            userID: sessionID,
            term: cleanTerm,
            includeItemTypes: filter.includeItemTypes
        )
        async let results = client.allSearchItems(userID: sessionID, term: cleanTerm, includeItemTypes: filter.includeItemTypes)
        let loadedHints = (try? await hints) ?? []
        let loadedItems: [MediaItem]
        do {
            let loadedResults = try await results
            loadedItems = loadedResults.items
            let totalMatching = loadedResults.totalRecordCount
            guard searchRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            searchTotalMatching = totalMatching
        } catch {
            guard searchRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            searchItems = []
            searchTotalMatching = nil
            searchError = AuthErrorPolicy.serverConnectionMessage(for: error)
            searchHints = loadedHints
            return
        }
        guard searchRequestID == requestID,
              self.session?.userID == sessionID,
              self.session?.serverURL == sessionServerURL else { return }
        searchHints = loadedHints
        searchItems = loadedItems
        searchError = nil
        if !loadedItems.isEmpty { rememberSearch(cleanTerm) }
    }

    func removeSearchHistory(_ term: String) {
        searchHistory.removeAll { $0 == term }
        persistSearchHistory()
    }

    func clearSearchHistory() {
        searchHistory = []
        if let session {
            SearchHistoryStore.clear(ownerKey: Self.ownerKey(for: session))
        }
    }

    func selectSearchHint(_ hint: SearchHint) -> MediaItem {
        let term = hint.name.trimmingCharacters(in: .whitespacesAndNewlines)
        if !term.isEmpty { rememberSearch(term) }
        return SearchHintSelectionPolicy.item(from: hint)
    }

    func openLibrary(_ library: MediaItem) async {
        guard let session, let client else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            libraryError = NetworkRequestPolicy.offlineMessage
            return
        }
        let requestID = UUID()
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        let previousLibraryID = selectedLibrary?.id
        libraryRequestID = requestID
        selectedLibrary = library
        libraryError = nil
        if previousLibraryID != library.id { libraryItems = [] }
        isLibraryLoading = true
        defer {
            if libraryRequestID == requestID {
                isLibraryLoading = false
            }
        }
        do {
            let items = try await client.allItems(
                userID: sessionID,
                parentID: library.id,
                sortBy: librarySort,
                sortOrder: librarySortOrder,
                filters: libraryFavoritesOnly ? "IsFavorite" : nil,
                isFavorite: libraryFavoritesOnly ? true : nil,
                genres: nonEmptyFilter(libraryGenreFilter),
                years: nonEmptyFilter(libraryYearFilter),
                officialRatings: nonEmptyFilter(libraryRatingFilter),
                isPlayed: libraryPlayedFilter.isPlayed,
                fields: "Overview,MediaSources,ItemCounts"
            )
            guard libraryRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            libraryItems = items
        } catch {
            guard libraryRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            libraryError = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func reloadSelectedLibrary() async {
        guard let selectedLibrary else { return }
        await openLibrary(selectedLibrary)
    }

    func closeLibrary() {
        libraryRequestID = UUID()
        selectedLibrary = nil
        libraryItems = []
        libraryError = nil
        isLibraryLoading = false
    }

    private func nonEmptyFilter(_ value: String) -> String? {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }

    func toggleFavorite(for item: MediaItem) async -> MediaItem? {
        guard let session, let client else { return nil }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            errorMessage = NetworkRequestPolicy.offlineMessage
            return nil
        }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        let nextValue = !item.isFavorite
        do {
            try await client.setFavorite(userID: sessionID, itemID: item.id, isFavorite: nextValue)
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return nil }
            let updated = item.withFavorite(nextValue)
            replace(updated)
            return updated
        } catch { errorMessage = AuthErrorPolicy.serverConnectionMessage(for: error); return nil }
    }

    func togglePlayed(for item: MediaItem) async -> MediaItem? {
        guard let session, let client else { return nil }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            errorMessage = NetworkRequestPolicy.offlineMessage
            return nil
        }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        let nextValue = !item.isPlayed
        do {
            try await client.setPlayed(userID: sessionID, itemID: item.id, isPlayed: nextValue)
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return nil }
            let updated = item.withPlayed(nextValue)
            replace(updated)
            return updated
        } catch { errorMessage = AuthErrorPolicy.serverConnectionMessage(for: error); return nil }
    }

    func loadDetail(for item: MediaItem) async -> MediaItem? {
        guard let session, let client else { return nil }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            return item
        }
        let requestID = UUID()
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        detailRequestID = requestID
        similarItems = []
        specialFeatures = []
        albumTracks = []
        let loaded = try? await client.item(userID: sessionID, itemID: item.id)
        guard detailRequestID == requestID,
              self.session?.userID == sessionID,
              self.session?.serverURL == sessionServerURL else { return nil }
        let detail = loaded ?? item
        async let similar = client.similarItems(userID: sessionID, itemID: detail.id)
        async let special = client.specialFeatures(userID: sessionID, itemID: detail.id)
        let loadedSimilar = (try? await similar) ?? []
        let loadedSpecial = (try? await special) ?? []
        guard detailRequestID == requestID,
              self.session?.userID == sessionID,
              self.session?.serverURL == sessionServerURL else { return nil }
        similarItems = loadedSimilar
        specialFeatures = loadedSpecial
        if detail.type == "MusicAlbum" {
            let loadedTracks = (try? await client.audioTracks(userID: sessionID, albumID: detail.id)) ?? []
            guard detailRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return nil }
            albumTracks = loadedTracks
        }
        return detail
    }

    func mediaSegments(for itemID: String) async -> [MediaSegment] {
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable),
              let client else { return [] }
        let sessionID = session?.userID
        let sessionServerURL = session?.serverURL
        let loaded = (try? await client.mediaSegments(itemID: itemID)) ?? []
        if let sessionID, let sessionServerURL {
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return [] }
        }
        return loaded
    }

    func externalSubtitleData(itemID: String, streamIndex: Int, mediaSourceID: String?) async throws -> Data {
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable), let client else {
            throw APIError.serverMessage(NetworkRequestPolicy.offlineMessage)
        }
        let sessionID = session?.userID
        let sessionServerURL = session?.serverURL
        let data = try await client.subtitleStreamData(itemID: itemID, streamIndex: streamIndex, mediaSourceID: mediaSourceID)
        if let sessionID, let sessionServerURL {
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else {
                throw APIError.serverMessage("A sessão foi alterada durante o carregamento da legenda.")
            }
        }
        return data
    }

    func chapters(for itemID: String) async -> [Chapter] {
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable),
              let session,
              let client else { return [] }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        let item = try? await client.item(userID: sessionID, itemID: itemID)
        guard self.session?.userID == sessionID,
              self.session?.serverURL == sessionServerURL else { return [] }
        return item?.chapters ?? []
    }

    func loadSeriesContext(for item: MediaItem) async {
        guard let session, let client else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else { return }
        let requestID = UUID()
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        seriesRequestID = requestID
        isSeriesLoading = true
        defer {
            if seriesRequestID == requestID { isSeriesLoading = false }
        }
        let seriesID: String
        switch item.type {
        case "Series": seriesID = item.id
        case "Season", "Episode":
            guard let value = item.seriesId else {
                activeSeriesID = nil
                seriesSeasons = []
                seriesEpisodes = []
                return
            }
            seriesID = value
        default:
            activeSeriesID = nil
            seriesSeasons = []
            seriesEpisodes = []
            return
        }
        activeSeriesID = seriesID
        let loadedSeasons = (try? await client.seasons(userID: sessionID, seriesID: seriesID)) ?? []
        guard seriesRequestID == requestID,
              self.session?.userID == sessionID,
              self.session?.serverURL == sessionServerURL else { return }
        seriesSeasons = loadedSeasons
        if let currentSeasonID = item.type == "Season" ? item.id : item.seasonId,
           let index = seriesSeasons.firstIndex(where: { $0.id == currentSeasonID }) {
            selectedSeasonIndex = index
        } else {
            selectedSeasonIndex = min(selectedSeasonIndex, max(0, seriesSeasons.count - 1))
        }
        let seasonID = seriesSeasons.indices.contains(selectedSeasonIndex) ? seriesSeasons[selectedSeasonIndex].id : nil
        let loadedEpisodes = (try? await client.episodes(userID: sessionID, seriesID: seriesID, seasonID: seasonID)) ?? []
        guard seriesRequestID == requestID,
              self.session?.userID == sessionID,
              self.session?.serverURL == sessionServerURL else { return }
        seriesEpisodes = loadedEpisodes
    }

    func selectSeason(_ index: Int) async {
        guard index >= 0, index < seriesSeasons.count, let session, let client else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else { return }
        let requestID = UUID()
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        seriesRequestID = requestID
        selectedSeasonIndex = index
        guard let seriesID = activeSeriesID else { return }
        let loadedEpisodes = (try? await client.episodes(userID: sessionID, seriesID: seriesID, seasonID: seriesSeasons[index].id)) ?? []
        guard seriesRequestID == requestID,
              self.session?.userID == sessionID,
              self.session?.serverURL == sessionServerURL else { return }
        seriesEpisodes = loadedEpisodes
    }

    func nextEpisode(after item: MediaItem) async -> MediaItem? {
        if let downloaded = completedOfflineNextEpisode(after: item) {
            return downloaded
        }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            return nil
        }
        guard let session, let client,
              let seriesID = item.seriesId,
              let seasonID = item.seasonId,
              let currentNumber = item.indexNumber else { return nil }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL

        let currentEpisodes = (try? await client.episodes(userID: sessionID, seriesID: seriesID, seasonID: seasonID)) ?? []
        guard self.session?.userID == sessionID,
              self.session?.serverURL == sessionServerURL else { return nil }
        if let next = currentEpisodes
            .filter({ ($0.indexNumber ?? Int.max) > currentNumber })
            .sorted(by: episodeOrder)
            .first {
            return next
        }

        let seasons = ((try? await client.seasons(userID: sessionID, seriesID: seriesID)) ?? [])
            .sorted(by: episodeOrder)
        guard self.session?.userID == sessionID,
              self.session?.serverURL == sessionServerURL else { return nil }
        guard let currentSeasonIndex = seasons.firstIndex(where: { $0.id == seasonID }) else { return nil }
        for season in seasons.dropFirst(currentSeasonIndex + 1) {
            let episodes = (try? await client.episodes(userID: sessionID, seriesID: seriesID, seasonID: season.id)) ?? []
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return nil }
            if let first = episodes.sorted(by: episodeOrder).first {
                return first
            }
        }
        return nil
    }

    private func completedOfflineNextEpisode(after item: MediaItem) -> MediaItem? {
        guard let current = offlineDownloads.first(where: { $0.itemID == item.id && $0.state == .completed }),
              let seriesID = current.seriesID,
              let currentSeason = current.seasonNumber,
              let currentEpisode = current.episodeNumber else { return nil }

        let currentDescriptor = OfflineEpisodeDescriptor(
            itemID: current.itemID,
            seriesID: seriesID,
            seasonNumber: currentSeason,
            episodeNumber: currentEpisode
        )
        let descriptors = offlineDownloads.compactMap { entry -> OfflineEpisodeDescriptor? in
            guard entry.state == .completed,
                  let candidateSeriesID = entry.seriesID,
                  let season = entry.seasonNumber,
                  let episode = entry.episodeNumber else { return nil }
            return OfflineEpisodeDescriptor(
                itemID: entry.itemID,
                seriesID: candidateSeriesID,
                seasonNumber: season,
                episodeNumber: episode
            )
        }
        guard let candidateDescriptor = OfflineNextEpisodePolicy.next(
            after: currentDescriptor,
            candidates: descriptors
        ), let candidate = offlineDownloads.first(where: { $0.itemID == candidateDescriptor.itemID }) else {
            return nil
        }

        return MediaItem(
            id: candidate.itemID,
            name: candidate.contextualTitle,
            type: "Episode",
            seriesId: candidate.seriesID,
            seriesName: candidate.seriesName,
            seasonName: candidate.seasonName,
            indexNumber: candidate.episodeNumber
        )
    }

    private func episodeOrder(_ lhs: MediaItem, _ rhs: MediaItem) -> Bool {
        (lhs.indexNumber ?? Int.max) < (rhs.indexNumber ?? Int.max)
    }

    func loadPlaylists() async {
        guard let session, let client else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            playlistItemsError = NetworkRequestPolicy.offlineMessage
            return
        }
        let requestID = UUID()
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        playlistsRequestID = requestID
        isPlaylistLoading = true
        defer {
            if playlistsRequestID == requestID { isPlaylistLoading = false }
        }
        let loadedPlaylists = (try? await client.playlists(userID: sessionID)) ?? []
        guard playlistsRequestID == requestID,
              self.session?.userID == sessionID,
              self.session?.serverURL == sessionServerURL else { return }
        playlists = loadedPlaylists
    }

    func loadPlaylistLibrary() async {
        guard let session else { return }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        await loadPlaylists()
        guard self.session?.userID == sessionID,
              self.session?.serverURL == sessionServerURL else { return }
        guard let playlist = selectedPlaylist.flatMap({ current in playlists.first(where: { $0.id == current.id }) }) ?? playlists.first else {
            selectedPlaylist = nil
            playlistItems = []
            playlistItemsError = nil
            return
        }
        await loadPlaylistItems(playlist)
    }

    func loadPlaylistItems(_ playlist: Playlist) async {
        guard let session, let client else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            playlistItemsError = NetworkRequestPolicy.offlineMessage
            return
        }
        let requestID = UUID()
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        playlistItemsRequestID = requestID
        selectedPlaylist = playlist
        playlistItems = []
        playlistItemsError = nil
        isPlaylistItemsLoading = true
        defer {
            if playlistItemsRequestID == requestID { isPlaylistItemsLoading = false }
        }
        do {
            let items = try await client.allPlaylistItems(userID: sessionID, playlistID: playlist.id)
            guard playlistItemsRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            playlistItems = items
        } catch {
            guard playlistItemsRequestID == requestID,
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            playlistItemsError = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func addToPlaylist(_ playlist: Playlist, item: MediaItem) async {
        guard let session, let client else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            errorMessage = NetworkRequestPolicy.offlineMessage
            return
        }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        do {
            try await client.addToPlaylist(playlistID: playlist.id, itemID: item.id, userID: sessionID)
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
        } catch { errorMessage = AuthErrorPolicy.serverConnectionMessage(for: error) }
    }

    func createPlaylist(name: String, item: MediaItem) async {
        guard let session, let client else { return }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            errorMessage = NetworkRequestPolicy.offlineMessage
            return
        }
        let cleanName = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleanName.isEmpty else { return }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        do {
            let created = try await client.createPlaylist(name: cleanName, userID: sessionID, itemID: item.id)
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return }
            playlists.insert(created, at: 0)
        } catch { errorMessage = AuthErrorPolicy.serverConnectionMessage(for: error) }
    }

    func playbackURL(for item: MediaItem, resume: Bool = true) async -> URL? {
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable), let client else { return nil }
        let sessionID = session?.userID
        let sessionServerURL = session?.serverURL
        if let sessionID, let sessionServerURL,
           let prepared = try? await client.preparedPlaybackURL(
            userID: sessionID,
            item: item,
            maxStreamingBitrate: defaultQualityStreamingBitrate,
            startTimeTicks: resume && item.playbackPositionTicks > 0 ? item.playbackPositionTicks : nil
        ) {
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return nil }
            return prepared
        }
        let fallback = await client.playbackURL(for: item)
        if let sessionID, let sessionServerURL {
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return nil }
        }
        return fallback
    }

    func submitMediaRequest(title: String, mediaType: String, year: Int?, notes: String) async throws {
        guard let client, let session else { throw APIError.serverMessage("Conecte-se ao servidor para enviar a solicitação.") }
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable) else {
            throw APIError.serverMessage(NetworkRequestPolicy.offlineMessage)
        }
        if let year, !(MediaRequestPolicy.minimumYear...MediaRequestPolicy.maximumYear).contains(year) {
            throw APIError.serverMessage("Informe um ano entre 1888 e 2200.")
        }
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        try await client.requestMedia(title: title, mediaType: mediaType, year: year, notes: notes)
        guard self.session?.userID == sessionID,
              self.session?.serverURL == sessionServerURL else {
            throw APIError.serverMessage("A sessão foi alterada durante o envio da solicitação.")
        }
    }

    func updateMediaSuggestions(query: String) {
        mediaSuggestionGeneration += 1
        let generation = mediaSuggestionGeneration
        mediaSuggestionTask?.cancel()

        let normalizedQuery = MediaSuggestionPolicy.normalizedQuery(query)
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable),
              MediaSuggestionPolicy.shouldQuery(query),
              let client,
              let requestSession = session else {
            mediaSuggestions = []
            return
        }

        mediaSuggestionTask = Task { [weak self] in
            try? await Task.sleep(nanoseconds: MediaSuggestionPolicy.debounceNanoseconds)
            guard !Task.isCancelled else { return }
            guard self?.isNetworkAvailable != false else { return }
            let result = try? await client.mediaSuggestions(query: normalizedQuery, limit: MediaSuggestionPolicy.resultLimit)
            guard !Task.isCancelled, let self,
                  self.mediaSuggestionGeneration == generation,
                  self.session == requestSession,
                  self.isNetworkAvailable != false else { return }
            self.mediaSuggestions = result ?? []
        }
    }

    func clearMediaSuggestions() {
        mediaSuggestionGeneration += 1
        mediaSuggestionTask?.cancel()
        mediaSuggestionTask = nil
        mediaSuggestions = []
    }

    func submitPlaybackIssue(item: MediaItem, category: String, description: String) async throws -> PlaybackIssueSubmissionResult {
        guard let client, let session else { throw APIError.serverMessage("Conecte-se ao servidor para enviar o relato.") }
        guard !pendingPlaybackIssuesUnreadable else {
            throw APIError.serverMessage("Os relatos pendentes estão ilegíveis; preserve os dados e tente novamente após recuperar a fila.")
        }
        guard PlaybackIssueQueuePolicy.canEnqueue(currentCount: pendingPlaybackIssues.count) else {
            throw APIError.serverMessage("A fila de relatos está cheia. Envie os relatos pendentes antes de adicionar outro.")
        }
        let normalizedDescription = PlaybackIssueQueuePolicy.normalizedDescription(description)
        let sessionID = session.userID
        let sessionServerURL = session.serverURL
        let ownerKey = Self.ownerKey(for: session)
        do {
            try await client.reportPlaybackIssue(itemID: item.id, category: category, description: normalizedDescription)
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else {
                throw APIError.serverMessage("A sessão foi alterada durante o envio do relato.")
            }
            return .sent
        } catch {
            guard PlaybackIssueQueuePolicy.shouldQueue(error),
                  self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { throw error }
            let entry = QueuedPlaybackIssue(itemID: item.id, category: category, description: normalizedDescription)
            pendingPlaybackIssues.append(entry)
            PlaybackIssueQueueStore.save(pendingPlaybackIssues, ownerKey: ownerKey)
            return .queued
        }
    }

    func lyrics(for item: MediaItem) async -> [LyricLine] {
        guard NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable), let client else { return [] }
        let sessionID = session?.userID
        let sessionServerURL = session?.serverURL
        let loaded = (try? await client.lyrics(itemID: item.id)) ?? []
        if let sessionID, let sessionServerURL {
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return [] }
        }
        return loaded
    }

    func signOut() {
        sessionMutationID = UUID()
        searchRequestID = UUID()
        registrationMutationID = UUID()
        serverVerificationID = UUID()
        publicUsersRequestID = UUID()
        publicUsers = []
        isRegistering = false
        isVerifyingServer = false
        isDiscoveringServers = false
        isSwitchingUser = false
        cancelQuickConnect()
        clearMediaSuggestions()
        cancelSeasonDownload()
        seasonDownloadProgress = nil
        downloadTasks.values.forEach { $0.cancel() }
        downloadTasks.removeAll()
        sessionStore.clear()
        client = nil
        session = nil
        pendingPlaybackIssues = []
        pendingPlaybackIssuesUnreadable = false
        offlineDownloads = []
        profile = nil
        profileError = nil
        profileRequestID = UUID()
        isProfileLoading = false
        favoritesError = nil
        favoritesRequestID = UUID()
        isFavoritesLoading = false
        serverInfo = nil
        serverHealth = nil
        branding = nil
        heroItem = nil
        latestItems = []
        resumeItems = []
        nextUpItems = []
        favoriteItems = []
        resumeItemsFromCache = false
        favoriteItemsFromCache = false
        homeCachedAtEpochMillis = nil
        popularItems = []
        movieItems = []
        seriesItems = []
        libraries = []
        librariesError = nil
        librariesRequestID = UUID()
        isLibrariesLoading = false
        libraryItems = []
        selectedLibrary = nil
        homeError = nil
        homeRequestID = UUID()
        isHomeLoading = false
        libraryError = nil
        libraryRequestID = UUID()
        isLibraryLoading = false
        detailRequestID = UUID()
        seriesRequestID = UUID()
        playlistsRequestID = UUID()
        liveTVTimerMutationID = UUID()
        serverDiscoveryRequestID = UUID()
        searchItems = []
        searchTotalMatching = nil
        searchHints = []
        searchError = nil
        isSearching = false
        clearSearchHistory()
        seriesSeasons = []
        seriesEpisodes = []
        activeSeriesID = nil
        playlists = []
        selectedPlaylist = nil
        playlistItems = []
        playlistItemsRequestID = UUID()
        playlistItemsError = nil
        isPlaylistItemsLoading = false
        liveChannels = []
        livePrograms = []
        liveRecordings = []
        scheduledLiveProgramIDs = []
        scheduledLiveTimerIDs = [:]
        schedulingLiveProgramIDs = []
        cancellingLiveProgramIDs = []
        liveTVError = nil
        liveTVRequestID = UUID()
        syncPlayGroups = []
        syncPlayError = nil
        syncPlayRequestID = UUID()
        activeSyncPlayGroupID = nil
        isSyncPlaySubmitting = false
        stopSyncPlayRealtime()
        remotePlaybackRequestID = UUID()
        remotePlaybackSessions = []
        remotePlaybackError = nil
        remotePlaybackBusySessionIDs.removeAll()
        state = .signedOut
    }

    private func clearUserScopedContent() {
        clearMediaSuggestions()
        cancelSeasonDownload()
        seasonDownloadProgress = nil
        profile = nil
        profileError = nil
        profileRequestID = UUID()
        isProfileLoading = false
        favoritesError = nil
        favoritesRequestID = UUID()
        isFavoritesLoading = false
        heroItem = nil
        latestItems = []
        resumeItems = []
        nextUpItems = []
        favoriteItems = []
        resumeItemsFromCache = false
        favoriteItemsFromCache = false
        homeCachedAtEpochMillis = nil
        popularItems = []
        movieItems = []
        seriesItems = []
        libraries = []
        librariesError = nil
        librariesRequestID = UUID()
        isLibrariesLoading = false
        libraryItems = []
        selectedLibrary = nil
        homeError = nil
        homeRequestID = UUID()
        isHomeLoading = false
        libraryError = nil
        libraryRequestID = UUID()
        isLibraryLoading = false
        detailRequestID = UUID()
        seriesRequestID = UUID()
        playlistsRequestID = UUID()
        liveTVTimerMutationID = UUID()
        serverDiscoveryRequestID = UUID()
        searchItems = []
        searchTotalMatching = nil
        searchHints = []
        searchError = nil
        searchRequestID = UUID()
        isSearching = false
        searchHistory = []
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
        selectedPlaylist = nil
        playlistItems = []
        playlistItemsRequestID = UUID()
        playlistItemsError = nil
        isPlaylistItemsLoading = false
        liveChannels = []
        livePrograms = []
        liveRecordings = []
        scheduledLiveProgramIDs = []
        scheduledLiveTimerIDs = [:]
        schedulingLiveProgramIDs = []
        cancellingLiveProgramIDs = []
        liveTVError = nil
        liveTVRequestID = UUID()
        isLiveTVLoading = false
        syncPlayGroups = []
        syncPlayError = nil
        syncPlayRequestID = UUID()
        activeSyncPlayGroupID = nil
        isSyncPlaySubmitting = false
        stopSyncPlayRealtime()
        isSyncPlayLoading = false
        remotePlaybackRequestID = UUID()
        remotePlaybackSessions = []
        remotePlaybackError = nil
        remotePlaybackBusySessionIDs.removeAll()
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
            wifiAvailable: isWiFiAvailable,
            networkAvailable: isNetworkAvailable
        )
    }

    private var defaultQualityStreamingBitrate: Int64? {
        PlaybackQualityPolicy.effectiveStreamingBitrate(for: defaultQuality, isMetered: isMeteredNetwork)
    }

    private func handleNetworkAvailabilityChange(_ available: Bool, wifi: Bool, metered: Bool) {
        let wasAvailable = previousNetworkAvailability
        previousNetworkAvailability = available
        isNetworkAvailable = available
        isWiFiAvailable = wifi
        isMeteredNetwork = metered
        if canStartOfflineDownloads {
            resumeQueuedOfflineDownloads()
        }
        guard available, wasAvailable != true, state == .signedIn else { return }
        Task { @MainActor [weak self] in
            await self?.refreshAuthenticatedContent()
        }
    }

    func refreshAuthenticatedContent() async {
        guard state == .signedIn,
              NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable),
              !isRefreshingAuthenticatedContent else { return }
        isRefreshingAuthenticatedContent = true
        defer { isRefreshingAuthenticatedContent = false }
        await loadHome()
        await loadLibraries()
        await reloadSelectedLibrary()
        await loadFavorites()
        await loadProfile()
        await loadLiveTV()
        await loadPlaylistLibrary()
        await loadSyncPlay(background: true)
        await loadRemotePlayback(background: true)
        await syncPendingPlaybackIssues()
    }

    private func syncPendingPlaybackIssues() async {
        guard !pendingPlaybackIssuesUnreadable, let client, let session else { return }
        let ownerKey = Self.ownerKey(for: session)
        for entry in pendingPlaybackIssues {
            guard self.session?.userID == session.userID,
                  self.session?.serverURL == session.serverURL else { return }
            do {
                try await client.reportPlaybackIssue(
                    itemID: entry.itemID,
                    category: entry.category,
                    description: entry.description
                )
                guard self.session?.userID == session.userID,
                      self.session?.serverURL == session.serverURL else { return }
                pendingPlaybackIssues.removeAll { $0.id == entry.id }
                PlaybackIssueQueueStore.save(pendingPlaybackIssues, ownerKey: ownerKey)
            } catch {
                return
            }
        }
    }

    private func loadPendingPlaybackIssues(for session: UserSession?) {
        guard let session else {
            pendingPlaybackIssues = []
            pendingPlaybackIssuesUnreadable = false
            return
        }
        let result = PlaybackIssueQueueStore.loadResult(ownerKey: Self.ownerKey(for: session))
        pendingPlaybackIssues = result.entries
        pendingPlaybackIssuesUnreadable = result.isCorrupted
    }

    private func resumeQueuedOfflineDownloads() {
        guard canStartOfflineDownloads else { return }
        offlineDownloads
            .filter { $0.state == .queued && downloadTasks[$0.id] == nil }
            .compactMap { entry in URL(string: entry.sourceURL).map { (entry, $0) } }
            .forEach { entry, url in startOfflineDownload(entry, url: url) }
    }

    private func cacheOfflineArtwork(for item: MediaItem, ownerKey: String) async {
        guard OfflineDownloadScope.acceptsCallback(ownerKey: ownerKey, currentOwnerKey: downloadOwnerKey),
              let client,
              let data = try? await client.imageData(for: item) else { return }
        try? OfflineDownloadStore.saveArtworkData(data, itemID: item.id, ownerKey: ownerKey)
    }

    private func downloadOfflineSubtitles(for entry: OfflineDownload, ownerKey: String) async -> [OfflineSubtitleEntry] {
        guard let client else { return [] }
        var stored: [OfflineSubtitleEntry] = []
        for subtitle in entry.subtitles {
            guard OfflineDownloadScope.acceptsCallback(ownerKey: ownerKey, currentOwnerKey: downloadOwnerKey) else { return stored }
            do {
                let data = try await client.subtitleStreamData(itemID: entry.itemID, streamIndex: subtitle.streamIndex, mediaSourceID: subtitle.mediaSourceID)
                try OfflineDownloadStore.saveSubtitleData(data, fileName: subtitle.fileName, ownerKey: ownerKey)
                stored.append(subtitle)
            } catch {
                OfflineDownloadStore.removeFile(subtitle.fileName, ownerKey: ownerKey)
            }
        }
        return stored
    }

    private func startOfflineDownload(_ entry: OfflineDownload, url: URL, reattachExisting: Bool = false) {
        guard let ownerKey = downloadOwnerKey else { return }
        downloadTasks[entry.id]?.cancel()
        downloadTasks[entry.id] = Task { [weak self] in
            guard let self else { return }
            guard OfflineDownloadScope.acceptsCallback(ownerKey: ownerKey, currentOwnerKey: self.downloadOwnerKey) else { return }
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
                                self?.updateDownloadProgress(ownerKey: ownerKey, id: entry.id, percent: percent, bytesDownloaded: bytesDownloaded, contentLength: contentLength)
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
                        self?.updateDownloadProgress(ownerKey: ownerKey, id: entry.id, percent: percent, bytesDownloaded: bytesDownloaded, contentLength: contentLength)
                    }
                    }
                }
                guard OfflineDownloadScope.acceptsCallback(ownerKey: ownerKey, currentOwnerKey: self.downloadOwnerKey) else { return }
                try OfflineDownloadStore.move(temporaryURL, to: entry.fileName, ownerKey: ownerKey)
                let completed = self.offlineDownloads.first(where: { $0.id == entry.id }) ?? startingEntry
                let storedSubtitles = await self.downloadOfflineSubtitles(for: completed, ownerKey: ownerKey)
                self.upsertDownload(completed.with(state: .completed, percent: 100, error: nil).with(subtitles: storedSubtitles))
            } catch let paused as OfflineDownloadPaused {
                if OfflineDownloadScope.acceptsCallback(ownerKey: ownerKey, currentOwnerKey: self.downloadOwnerKey),
                   let current = self.offlineDownloads.first(where: { $0.id == entry.id }) {
                    self.upsertDownload(current.withResumeData(paused.resumeData))
                }
                return
            } catch is CancellationError {
                return
            } catch {
                guard OfflineDownloadScope.acceptsCallback(ownerKey: ownerKey, currentOwnerKey: self.downloadOwnerKey) else { return }
                let message = OfflineDownloadFailurePolicy.isInsufficientStorage(error)
                    ? OfflineDownloadFailurePolicy.insufficientStorageMessage
                    : AuthErrorPolicy.serverConnectionMessage(for: error)
                self.upsertDownload(entry.with(state: .failed, percent: 0, error: message))
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
        if let ownerKey = downloadOwnerKey { OfflineDownloadStore.save(offlineDownloads, ownerKey: ownerKey) }
    }

    private func updateDownloadProgress(ownerKey: String, id: String, percent: Int, bytesDownloaded: Int64, contentLength: Int64?) {
        guard OfflineDownloadScope.acceptsCallback(ownerKey: ownerKey, currentOwnerKey: downloadOwnerKey) else { return }
        guard let entry = offlineDownloads.first(where: { $0.id == id }), entry.state == .downloading else { return }
        let boundedPercent = min(99, max(0, percent))
        guard entry.percent != boundedPercent || entry.bytesDownloaded != bytesDownloaded || entry.contentLength != contentLength else { return }
        upsertDownload(entry.withProgress(percent: boundedPercent, bytesDownloaded: bytesDownloaded, contentLength: contentLength))
    }

    private func cancelActiveDownloadsForSessionChange() {
        cancelSeasonDownload()
        downloadTasks.values.forEach { $0.cancel() }
        downloadTasks.removeAll()
    }

    private func pollQuickConnect(api: APIClient, secret: String, mutationID: UUID) async {
        do {
            for attempt in 0..<100 {
                guard sessionMutationID == mutationID else { return }
                if attempt > 0 { try await Task.sleep(for: .seconds(3)) }
                try Task.checkCancellation()
                let status: QuickConnectResult
                do {
                    status = try await api.connectQuickConnect(secret: secret)
                } catch {
                    if let message = QuickConnectErrorPolicy.terminalMessage(for: error) {
                        guard sessionMutationID == mutationID else { return }
                        isQuickConnectWaiting = false
                        quickConnectCode = nil
                        quickConnectSecondsRemaining = nil
                        errorMessage = message
                        return
                    }
                    guard QuickConnectErrorPolicy.shouldContinuePolling(after: error) else { throw error }
                    continue
                }
                guard sessionMutationID == mutationID else { return }
                quickConnectSecondsRemaining = max(0, 300 - attempt * 3)
                if status.authenticated {
                    let saved = try await api.authenticateQuickConnect(secret: secret)
                    guard sessionMutationID == mutationID else { return }
                    try sessionStore.save(saved)
                    cancelActiveDownloadsForSessionChange()
                    rememberServer(url: saved.serverURL, info: serverInfo)
                    client = api
                    session = saved
                    offlineDownloads = OfflineDownloadStore.load(ownerKey: Self.ownerKey(for: saved))
                    loadPendingPlaybackIssues(for: saved)
                    password = ""
                    isQuickConnectWaiting = false
                    quickConnectCode = nil
                    quickConnectSecondsRemaining = nil
                    let contentRequestID = UUID()
                    homeRequestID = contentRequestID
                    favoritesRequestID = contentRequestID
                    librariesRequestID = contentRequestID
                    try await loadHome(using: api, userID: saved.userID, serverURL: saved.serverURL, requestID: contentRequestID)
                    guard sessionMutationID == mutationID else { return }
                    try await loadLibraries(using: api, userID: saved.userID, requestID: contentRequestID)
                    guard sessionMutationID == mutationID else { return }
                    let loadedProfile = try? await api.userProfile(userID: saved.userID)
                    guard sessionMutationID == mutationID else { return }
                    applyProfile(loadedProfile)
                    guard sessionMutationID == mutationID else { return }
                    state = .signedIn
                    await syncPendingPlaybackIssues()
                    return
                }
            }
            guard sessionMutationID == mutationID else { return }
            isQuickConnectWaiting = false
            quickConnectCode = nil
            quickConnectSecondsRemaining = nil
            errorMessage = "O código Quick Connect expirou. Gere um novo código."
        } catch is CancellationError {
            return
        } catch {
            guard sessionMutationID == mutationID else { return }
            isQuickConnectWaiting = false
            quickConnectCode = nil
            quickConnectSecondsRemaining = nil
            errorMessage = AuthErrorPolicy.serverConnectionMessage(for: error)
        }
    }

    func imageURL(for item: MediaItem) async -> URL? {
        let sessionID = session?.userID
        let sessionServerURL = session?.serverURL
        let loaded = await client?.imageURL(for: item)
        if let sessionID, let sessionServerURL {
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return nil }
        }
        return loaded
    }

    func profileImageURL() async -> URL? {
        guard let profile else { return nil }
        let sessionID = session?.userID
        let sessionServerURL = session?.serverURL
        let loaded = await client?.userImageURL(userID: profile.id, tag: profile.primaryImageTag)
        if let sessionID, let sessionServerURL {
            guard self.session?.userID == sessionID,
                  self.session?.serverURL == sessionServerURL else { return nil }
        }
        return loaded
    }

    private func loadHome(using api: APIClient, userID: String, serverURL: URL? = nil, requestID: UUID? = nil) async throws {
        async let latest = api.latestItems(userID: userID)
        async let resume = api.resumeItems(userID: userID)
        async let nextUp = api.nextUpItems(userID: userID)
        async let favorites = api.allFavoriteItems(userID: userID)
        async let popular = api.items(userID: userID, parentID: nil, limit: 16, sortBy: "CommunityRating", sortOrder: "Descending")
        async let movies = api.items(userID: userID, parentID: nil, limit: 16, sortBy: "SortName", sortOrder: "Ascending", includeItemTypes: "Movie")
        async let series = api.items(userID: userID, parentID: nil, limit: 16, sortBy: "SortName", sortOrder: "Ascending", includeItemTypes: "Series")
        let loadedLatest = try await latest
        guard requestID == nil || homeRequestID == requestID else { return }
        latestItems = loadedLatest
        heroItem = latestItems.first
        let loadedResume: [MediaItem]
        do {
            loadedResume = try await resume
            if isCurrentSession(userID: userID, serverURL: serverURL), let currentSession = session {
                HomeSnapshotStore.save(resumeItems: loadedResume, favoriteItems: nil, ownerKey: Self.ownerKey(for: currentSession))
            }
        } catch {
            guard HomeSnapshotPolicy.shouldUseCache(for: error),
                  isCurrentSession(userID: userID, serverURL: serverURL),
                  let currentSession = session,
                  let snapshot = HomeSnapshotStore.load(ownerKey: Self.ownerKey(for: currentSession)),
                  snapshot.resumeSavedAtEpochMillis > 0 else { throw error }
            loadedResume = snapshot.resumeItems.map(\.mediaItem)
            resumeItemsFromCache = true
            homeCachedAtEpochMillis = snapshot.resumeSavedAtEpochMillis
        }
        guard requestID == nil || homeRequestID == requestID else { return }
        resumeItems = loadedResume
        guard requestID == nil || homeRequestID == requestID else { return }
        nextUpItems = (try? await nextUp) ?? []
        guard requestID == nil || homeRequestID == requestID else { return }
        let loadedFavorites: [MediaItem]
        do {
            loadedFavorites = try await favorites
            if isCurrentSession(userID: userID, serverURL: serverURL), let currentSession = session {
                HomeSnapshotStore.save(resumeItems: nil, favoriteItems: loadedFavorites, ownerKey: Self.ownerKey(for: currentSession))
            }
        } catch {
            guard HomeSnapshotPolicy.shouldUseCache(for: error),
                  isCurrentSession(userID: userID, serverURL: serverURL),
                  let currentSession = session,
                  let snapshot = HomeSnapshotStore.load(ownerKey: Self.ownerKey(for: currentSession)),
                  snapshot.favoritesSavedAtEpochMillis > 0 else { throw error }
            loadedFavorites = snapshot.favoriteItems.map(\.mediaItem)
            favoriteItemsFromCache = true
            homeCachedAtEpochMillis = snapshot.favoritesSavedAtEpochMillis
        }
        guard requestID == nil || (homeRequestID == requestID && favoritesRequestID == requestID) else { return }
        favoriteItems = loadedFavorites
        guard requestID == nil || homeRequestID == requestID else { return }
        popularItems = (try? await popular)?.items ?? []
        guard requestID == nil || homeRequestID == requestID else { return }
        movieItems = (try? await movies)?.items ?? []
        guard requestID == nil || homeRequestID == requestID else { return }
        seriesItems = (try? await series)?.items ?? []
    }

    @discardableResult
    private func applyHomeSnapshotIfAvailable(ownerKey: String) -> Bool {
        guard let snapshot = HomeSnapshotStore.load(ownerKey: ownerKey) else { return false }
        var hasSnapshot = false
        if snapshot.resumeSavedAtEpochMillis > 0 {
            resumeItems = snapshot.resumeItems.map(\.mediaItem)
            resumeItemsFromCache = true
            hasSnapshot = true
        }
        if snapshot.favoritesSavedAtEpochMillis > 0 {
            favoriteItems = snapshot.favoriteItems.map(\.mediaItem)
            favoriteItemsFromCache = true
            hasSnapshot = true
        }
        homeCachedAtEpochMillis = [snapshot.resumeSavedAtEpochMillis, snapshot.favoritesSavedAtEpochMillis]
            .filter { $0 > 0 }
            .max()
        return hasSnapshot
    }

    private func loadLibraries(using api: APIClient, userID: String, requestID: UUID? = nil) async throws {
        let loadedLibraries = try await api.libraries(userID: userID)
        guard requestID == nil || librariesRequestID == requestID else { return }
        libraries = loadedLibraries
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
        persistSearchHistory()
    }

    private func persistSearchHistory() {
        guard let session else { return }
        SearchHistoryStore.save(searchHistory, ownerKey: Self.ownerKey(for: session))
    }

    private func rememberServer(url: URL, info: ServerInfo?) {
        let value = url.absoluteString.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        let previous = savedServers.first { $0.url.caseInsensitiveCompare(value) == .orderedSame }
        let entry = SavedServer(
            url: value,
            name: info?.displayName ?? previous?.name ?? "Servidor MulletaFlix",
            version: info?.version ?? previous?.version,
            serverID: info?.id ?? previous?.serverID
        )
        savedServers.removeAll { $0.id.caseInsensitiveCompare(entry.id) == .orderedSame }
        savedServers.insert(entry, at: 0)
        if savedServers.count > 8 { savedServers.removeLast(savedServers.count - 8) }
        persistSavedServers()
    }

    private func healthStatus(from api: APIClient) async -> String? {
        guard let values = try? await api.health() else { return nil }
        return values["status"] ?? values["Status"] ?? values.values.first
    }

    private func persistSavedServers() {
        guard let data = try? JSONEncoder().encode(savedServers) else { return }
        UserDefaults.standard.set(data, forKey: Self.savedServersKey)
    }

    private static func loadSavedServers() -> [SavedServer] {
        guard let data = UserDefaults.standard.data(forKey: savedServersKey),
              let servers = try? JSONDecoder().decode([SavedServer].self, from: data) else { return [] }
        return servers
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

struct SeasonDownloadProgress: Equatable {
    let seasonID: String
    let seasonName: String
    let totalEpisodes: Int
    let processedEpisodes: Int
    let queuedEpisodes: Int
    let alreadyAvailableEpisodes: Int
    let failedEpisodes: Int
    let isRunning: Bool
    let isCancelled: Bool

    init(seasonID: String, seasonName: String, totalEpisodes: Int, processedEpisodes: Int = 0, queuedEpisodes: Int = 0, alreadyAvailableEpisodes: Int = 0, failedEpisodes: Int = 0, isRunning: Bool = false, isCancelled: Bool = false) {
        self.seasonID = seasonID
        self.seasonName = seasonName
        self.totalEpisodes = totalEpisodes
        self.processedEpisodes = processedEpisodes
        self.queuedEpisodes = queuedEpisodes
        self.alreadyAvailableEpisodes = alreadyAvailableEpisodes
        self.failedEpisodes = failedEpisodes
        self.isRunning = isRunning
        self.isCancelled = isCancelled
    }
}

struct OfflineSubtitleEntry: Codable, Equatable, Hashable {
    let streamIndex: Int
    let fileName: String
    let mimeType: String
    let mediaSourceID: String?
    let language: String?
    let label: String?
    let isDefault: Bool
    let isForced: Bool

    private enum CodingKeys: String, CodingKey {
        case streamIndex, fileName, mimeType, mediaSourceID, language, label, isDefault, isForced
    }

    init(streamIndex: Int, fileName: String, mimeType: String, mediaSourceID: String? = nil, language: String?, label: String?, isDefault: Bool, isForced: Bool) {
        self.streamIndex = streamIndex
        self.fileName = fileName
        self.mimeType = mimeType
        self.mediaSourceID = mediaSourceID
        self.language = language
        self.label = label
        self.isDefault = isDefault
        self.isForced = isForced
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        streamIndex = try container.decode(Int.self, forKey: .streamIndex)
        fileName = try container.decode(String.self, forKey: .fileName)
        mimeType = try container.decode(String.self, forKey: .mimeType)
        mediaSourceID = try container.decodeIfPresent(String.self, forKey: .mediaSourceID)
        language = try container.decodeIfPresent(String.self, forKey: .language)
        label = try container.decodeIfPresent(String.self, forKey: .label)
        isDefault = try container.decode(Bool.self, forKey: .isDefault)
        isForced = try container.decode(Bool.self, forKey: .isForced)
    }

    var mediaStream: MediaStream {
        MediaStream(
            index: streamIndex,
            type: "Subtitle",
            codec: mimeType,
            language: language,
            displayLanguage: language,
            displayTitle: label,
            isDefault: isDefault,
            isForced: isForced,
            isExternal: true,
            deliveryURL: fileName
        )
    }
}

struct OfflineDownload: Codable, Identifiable, Equatable {
    let itemID: String
    let title: String
    let seriesID: String?
    let seriesName: String?
    let seasonName: String?
    let seasonNumber: Int?
    let episodeNumber: Int?
    let state: OfflineDownloadState
    let percent: Int
    let fileName: String
    let sourceURL: String
    let artworkURL: String?
    let error: String?
    let bytesDownloaded: Int64
    let contentLength: Int64?
    let resumeData: Data?
    let subtitles: [OfflineSubtitleEntry]

    var id: String { itemID }

    var contextualTitle: String {
        guard let seriesName, !seriesName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              let episodeNumber, episodeNumber > 0 else { return title }
        let seasonLabel: String
        if let seasonNumber, seasonNumber > 0 {
            seasonLabel = "T\(seasonNumber)"
        } else {
            seasonLabel = "Especial"
        }
        return "\(seriesName) · \(seasonLabel) · E\(episodeNumber)"
    }

    init(itemID: String, title: String, seriesID: String? = nil, seriesName: String? = nil, seasonName: String? = nil, seasonNumber: Int? = nil, episodeNumber: Int? = nil, state: OfflineDownloadState, percent: Int, fileName: String, sourceURL: String = "", artworkURL: String? = nil, error: String? = nil, bytesDownloaded: Int64 = 0, contentLength: Int64? = nil, resumeData: Data? = nil, subtitles: [OfflineSubtitleEntry] = []) {
        self.itemID = itemID
        self.title = title
        self.seriesID = seriesID
        self.seriesName = seriesName
        self.seasonName = seasonName
        self.seasonNumber = seasonNumber
        self.episodeNumber = episodeNumber
        self.state = state
        self.percent = percent
        self.fileName = fileName
        self.sourceURL = sourceURL
        self.artworkURL = artworkURL
        self.error = error
        self.bytesDownloaded = bytesDownloaded
        self.contentLength = contentLength
        self.resumeData = resumeData
        self.subtitles = subtitles
    }

    private enum CodingKeys: String, CodingKey {
        case itemID, title, seriesID, seriesName, seasonName, seasonNumber, episodeNumber, state, percent, fileName, sourceURL, artworkURL, error, bytesDownloaded, contentLength, resumeData, subtitles
    }

    init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        self.init(
            itemID: try values.decode(String.self, forKey: .itemID),
            title: try values.decode(String.self, forKey: .title),
            seriesID: try values.decodeIfPresent(String.self, forKey: .seriesID),
            seriesName: try values.decodeIfPresent(String.self, forKey: .seriesName),
            seasonName: try values.decodeIfPresent(String.self, forKey: .seasonName),
            seasonNumber: try values.decodeIfPresent(Int.self, forKey: .seasonNumber),
            episodeNumber: try values.decodeIfPresent(Int.self, forKey: .episodeNumber),
            state: try values.decode(OfflineDownloadState.self, forKey: .state),
            percent: try values.decode(Int.self, forKey: .percent),
            fileName: try values.decode(String.self, forKey: .fileName),
            sourceURL: try values.decodeIfPresent(String.self, forKey: .sourceURL) ?? "",
            artworkURL: try values.decodeIfPresent(String.self, forKey: .artworkURL),
            error: try values.decodeIfPresent(String.self, forKey: .error),
            bytesDownloaded: try values.decodeIfPresent(Int64.self, forKey: .bytesDownloaded) ?? 0,
            contentLength: try values.decodeIfPresent(Int64.self, forKey: .contentLength),
            resumeData: try values.decodeIfPresent(Data.self, forKey: .resumeData),
            subtitles: try values.decodeIfPresent([OfflineSubtitleEntry].self, forKey: .subtitles) ?? []
        )
    }

    func with(state: OfflineDownloadState, percent: Int, error: String?) -> OfflineDownload {
        OfflineDownload(itemID: itemID, title: title, seriesID: seriesID, seriesName: seriesName, seasonName: seasonName, seasonNumber: seasonNumber, episodeNumber: episodeNumber, state: state, percent: percent, fileName: fileName, sourceURL: sourceURL, artworkURL: artworkURL, error: error, bytesDownloaded: bytesDownloaded, contentLength: contentLength, resumeData: resumeData, subtitles: subtitles)
    }

    func withProgress(percent: Int, bytesDownloaded: Int64, contentLength: Int64?) -> OfflineDownload {
        OfflineDownload(itemID: itemID, title: title, seriesID: seriesID, seriesName: seriesName, seasonName: seasonName, seasonNumber: seasonNumber, episodeNumber: episodeNumber, state: state, percent: percent, fileName: fileName, sourceURL: sourceURL, artworkURL: artworkURL, error: error, bytesDownloaded: bytesDownloaded, contentLength: contentLength, resumeData: resumeData, subtitles: subtitles)
    }

    func withResumeData(_ value: Data?) -> OfflineDownload {
        OfflineDownload(itemID: itemID, title: title, seriesID: seriesID, seriesName: seriesName, seasonName: seasonName, seasonNumber: seasonNumber, episodeNumber: episodeNumber, state: state, percent: percent, fileName: fileName, sourceURL: sourceURL, artworkURL: artworkURL, error: error, bytesDownloaded: bytesDownloaded, contentLength: contentLength, resumeData: value, subtitles: subtitles)
    }

    func with(subtitles: [OfflineSubtitleEntry]) -> OfflineDownload {
        OfflineDownload(itemID: itemID, title: title, seriesID: seriesID, seriesName: seriesName, seasonName: seasonName, seasonNumber: seasonNumber, episodeNumber: episodeNumber, state: state, percent: percent, fileName: fileName, sourceURL: sourceURL, artworkURL: artworkURL, error: error, bytesDownloaded: bytesDownloaded, contentLength: contentLength, resumeData: resumeData, subtitles: subtitles)
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
    private static var downloadsDirectory: URL {
        let base = fileManager.urls(for: .documentDirectory, in: .userDomainMask)[0]
        let directory = base.appendingPathComponent("Downloads", isDirectory: true)
        try? fileManager.createDirectory(at: directory, withIntermediateDirectories: true)
        return directory
    }

    private static func directory(ownerKey: String) -> URL {
        let directory = downloadsDirectory.appendingPathComponent(OfflineDownloadScope.directoryName(ownerKey: ownerKey), isDirectory: true)
        try? fileManager.createDirectory(at: directory, withIntermediateDirectories: true)
        return directory
    }

    static func load(ownerKey: String) -> [OfflineDownload] {
        let indexURL = directory(ownerKey: ownerKey).appendingPathComponent("index.json")
        guard let data = try? Data(contentsOf: indexURL), let entries = try? JSONDecoder().decode([OfflineDownload].self, from: data) else { return [] }
        return entries
    }

    static func save(_ entries: [OfflineDownload], ownerKey: String) {
        guard let data = try? JSONEncoder().encode(entries) else { return }
        let indexURL = directory(ownerKey: ownerKey).appendingPathComponent("index.json")
        try? data.write(to: indexURL, options: .atomic)
    }

    static func fileName(for item: MediaItem) -> String {
        let safeID = item.id.replacingOccurrences(of: "/", with: "_")
        return "\(safeID).media"
    }

    static func url(for fileName: String, ownerKey: String) -> URL {
        directory(ownerKey: ownerKey).appendingPathComponent(fileName)
    }

    static func existingURL(for fileName: String, ownerKey: String) -> URL? {
        let value = url(for: fileName, ownerKey: ownerKey)
        return fileManager.fileExists(atPath: value.path) ? value : nil
    }

    static func move(_ temporaryURL: URL, to fileName: String, ownerKey: String) throws {
        let destination = url(for: fileName, ownerKey: ownerKey)
        if fileManager.fileExists(atPath: destination.path) { try fileManager.removeItem(at: destination) }
        try fileManager.moveItem(at: temporaryURL, to: destination)
    }

    static func removeFile(_ fileName: String, ownerKey: String) {
        try? fileManager.removeItem(at: url(for: fileName, ownerKey: ownerKey))
    }

    static func artworkFileName(itemID: String) -> String {
        OfflineArtworkPolicy.fileName(for: itemID)
    }

    static func artworkURL(itemID: String, ownerKey: String) -> URL? {
        let destination = url(for: artworkFileName(itemID: itemID), ownerKey: ownerKey)
        return fileManager.fileExists(atPath: destination.path) ? destination : nil
    }

    static func saveArtworkData(_ data: Data, itemID: String, ownerKey: String) throws {
        guard OfflineArtworkPolicy.accepts(data) else { return }
        let destination = url(for: artworkFileName(itemID: itemID), ownerKey: ownerKey)
        let temporary = destination.appendingPathExtension("pending")
        try data.write(to: temporary, options: .atomic)
        if fileManager.fileExists(atPath: destination.path) { try fileManager.removeItem(at: destination) }
        try fileManager.moveItem(at: temporary, to: destination)
    }

    static func removeArtwork(itemID: String, ownerKey: String) {
        removeFile(artworkFileName(itemID: itemID), ownerKey: ownerKey)
    }

    static func subtitleFileName(itemID: String, streamIndex: Int, mimeType: String) -> String {
        let encodedID = Data(itemID.utf8).base64EncodedString()
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "=", with: "")
        let extensionName: String
        switch mimeType.lowercased() {
        case "application/x-subrip": extensionName = "srt"
        case "text/vtt": extensionName = "vtt"
        case "text/x-ssa": extensionName = "ass"
        case "application/ttml+xml": extensionName = "ttml"
        default: extensionName = "txt"
        }
        return "\(encodedID)-subtitle-\(streamIndex).\(extensionName)"
    }

    static func subtitleURL(fileName: String, ownerKey: String) -> URL {
        url(for: fileName, ownerKey: ownerKey)
    }

    static func saveSubtitleData(_ data: Data, fileName: String, ownerKey: String) throws {
        guard !data.isEmpty, data.count <= 4 * 1024 * 1024,
              !data.contains(0),
              let text = String(data: data, encoding: .utf8),
              !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            throw OfflineSubtitleStoreError.invalidContent
        }
        let normalized = text.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        guard !normalized.hasPrefix("<html") && !normalized.hasPrefix("<!doctype html") else {
            throw OfflineSubtitleStoreError.invalidContent
        }
        let destination = subtitleURL(fileName: fileName, ownerKey: ownerKey)
        let temporary = destination.appendingPathExtension("pending")
        try data.write(to: temporary, options: .atomic)
        if fileManager.fileExists(atPath: destination.path) { try fileManager.removeItem(at: destination) }
        try fileManager.moveItem(at: temporary, to: destination)
    }

    static func removeSubtitleFiles(_ subtitles: [OfflineSubtitleEntry], ownerKey: String) {
        subtitles.forEach { removeFile($0.fileName, ownerKey: ownerKey) }
    }
}

private enum OfflineSubtitleStoreError: LocalizedError {
    case invalidContent

    var errorDescription: String? { "O conteúdo offline não é uma legenda de texto válida." }
}

/// UDP discovery compatible with the Android client and Jellyfin's LAN contract.
private final class LocalServerDiscovery: @unchecked Sendable {
    private let port: NWEndpoint.Port = 7359
    private let message = Data("who is MulletaFlixServer?".utf8)

    func discover(timeout: TimeInterval = 2.5) async -> [DiscoveredServer] {
        let boundedTimeout = ServerDiscoveryTimingPolicy.boundedWindow(timeout)
        let startUptime = ProcessInfo.processInfo.systemUptime
        await withTaskGroup(of: [DiscoveredServer].self) { group in
            group.addTask { await self.probe(host: "255.255.255.255", timeout: boundedTimeout) }
            group.addTask {
                try? await Task.sleep(for: .milliseconds(750))
                let elapsed = ProcessInfo.processInfo.systemUptime - startUptime
                let remaining = ServerDiscoveryTimingPolicy.remainingWindow(
                    total: boundedTimeout,
                    elapsed: elapsed,
                )
                return await self.probe(host: "255.255.255.255", timeout: remaining)
            }
            var merged: [String: DiscoveredServer] = [:]
            for await servers in group {
                for server in servers { merged[server.address.absoluteString] = server }
            }
            return Array(merged.values).sorted { $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending }
        }
    }

    private func probe(host: String, timeout: TimeInterval) async -> [DiscoveredServer] {
        guard timeout > 0 else { return [] }
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
