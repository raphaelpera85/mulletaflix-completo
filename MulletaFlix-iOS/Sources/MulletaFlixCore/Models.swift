import Foundation

public enum ServerURLPolicy {
    public static func url(fromQRPayload rawPayload: String?) -> URL? {
        let payload = rawPayload?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        guard !payload.isEmpty, let original = URLComponents(string: payload) else { return nil }

        let candidate: String
        if original.scheme?.caseInsensitiveCompare("mulletaflix") == .orderedSame {
            guard let value = original.queryItems?.first(where: { $0.name.caseInsensitiveCompare("url") == .orderedSame })?.value else {
                return nil
            }
            candidate = value
        } else {
            candidate = payload
        }

        return normalize(candidate)
    }

    public static func normalize(_ rawValue: String) -> URL? {
        guard var components = URLComponents(string: rawValue.trimmingCharacters(in: .whitespacesAndNewlines)),
              let scheme = components.scheme?.lowercased(),
              scheme == "http" || scheme == "https",
              components.user == nil,
              components.password == nil,
              components.query == nil,
              components.fragment == nil,
              components.host?.isEmpty == false else { return nil }
        components.scheme = scheme
        components.host = components.host?.lowercased()
        components.path = components.path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        return components.url
    }
}

public enum OfflineDownloadScope {
    public static func ownerKey(serverURL: URL, userID: String) -> String {
        "\(serverURL.absoluteString)|\(userID)"
    }

    public static func directoryName(ownerKey: String) -> String {
        let encoded = Data(ownerKey.utf8)
            .base64EncodedString()
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "=", with: "")
        return "scope-\(encoded)"
    }

    public static func acceptsCallback(ownerKey: String, currentOwnerKey: String?) -> Bool {
        guard let currentOwnerKey else { return false }
        return ownerKey == currentOwnerKey
    }
}

public enum OfflinePlaybackPositionScope {
    public static func key(ownerKey: String, itemID: String) -> String {
        let owner = Data(ownerKey.utf8).base64EncodedString()
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "=", with: "")
        let item = Data(itemID.utf8).base64EncodedString()
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "=", with: "")
        return "offline-playback-position:\(owner):\(item)"
    }
}

public enum SearchFilter: String, CaseIterable, Identifiable, Sendable {
    case all, movies, series, episodes, music, books, people

    public var id: String { rawValue }

    public var title: String {
        switch self {
        case .all: return "Tudo"
        case .movies: return "Filmes"
        case .series: return "Séries"
        case .episodes: return "Episódios"
        case .music: return "Músicas"
        case .books: return "Livros"
        case .people: return "Pessoas"
        }
    }

    public var includeItemTypes: String? {
        switch self {
        case .all: return nil
        case .movies: return "Movie"
        case .series: return "Series"
        case .episodes: return "Episode"
        case .music: return "Audio"
        case .books: return "Book"
        case .people: return "Person"
        }
    }
}

public enum MediaSuggestionPolicy {
    public static let minimumQueryLength = 2
    public static let debounceNanoseconds: UInt64 = 250_000_000
    public static let resultLimit = 10

    public static func normalizedQuery(_ value: String) -> String {
        value.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    public static func shouldQuery(_ value: String) -> Bool {
        normalizedQuery(value).count >= minimumQueryLength
    }
}

public enum MediaRequestPolicy {
    public static let minimumYear = 1888
    public static let maximumYear = 2200

    public static func normalizedYear(_ rawValue: String) -> Int? {
        let value = rawValue.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !value.isEmpty, let year = Int(value), (minimumYear...maximumYear).contains(year) else {
            return nil
        }
        return year
    }

    public static func isValidYearInput(_ rawValue: String) -> Bool {
        let value = rawValue.trimmingCharacters(in: .whitespacesAndNewlines)
        return value.isEmpty || normalizedYear(value) != nil
    }
}

public enum MediaPlaceholderPolicy {
    public static func symbol(for type: String?) -> String {
        switch type?.lowercased() {
        case "book", "audiobook": return "book.closed"
        case "audio": return "music.note"
        case "person": return "person"
        default: return "film"
        }
    }
}

public enum PlaybackIssueSubmissionResult: Equatable, Sendable {
    case sent
    case queued
}

public struct QueuedPlaybackIssue: Codable, Equatable, Identifiable, Sendable {
    public let id: String
    public let itemID: String
    public let category: String
    public let description: String

    public init(
        id: String = UUID().uuidString,
        itemID: String,
        category: String,
        description: String
    ) {
        self.id = id
        self.itemID = itemID
        self.category = category
        self.description = description
    }
}

public enum PlaybackIssueQueuePolicy {
    public static let maxQueueSize = 50
    public static let maxDescriptionLength = 1_000

    public static func shouldQueue(_ error: Error) -> Bool {
        (error as NSError).domain == NSURLErrorDomain
    }

    public static func normalizedDescription(_ description: String) -> String {
        String(description.trimmingCharacters(in: .whitespacesAndNewlines).prefix(maxDescriptionLength))
    }

    public static func canEnqueue(currentCount: Int) -> Bool {
        currentCount < maxQueueSize
    }
}

public enum PlaybackIssueCategoryPolicy {
    public static let categories = [
        "Não reproduz",
        "Travamentos",
        "Sem áudio",
        "Áudio/legenda",
        "Qualidade",
        "Outro"
    ]
}

public struct PlaybackIssueQueueLoad: Equatable, Sendable {
    public let entries: [QueuedPlaybackIssue]
    public let isCorrupted: Bool

    public init(entries: [QueuedPlaybackIssue], isCorrupted: Bool) {
        self.entries = entries
        self.isCorrupted = isCorrupted
    }
}

public enum PlaybackIssueQueueStore {
    private static let keyPrefix = "feedback.playbackIssues."

    public static func load(ownerKey: String, defaults: UserDefaults = .standard) -> [QueuedPlaybackIssue] {
        loadResult(ownerKey: ownerKey, defaults: defaults).entries
    }

    public static func loadResult(ownerKey: String, defaults: UserDefaults = .standard) -> PlaybackIssueQueueLoad {
        guard let data = defaults.data(forKey: key(ownerKey: ownerKey)) else {
            return PlaybackIssueQueueLoad(entries: [], isCorrupted: false)
        }
        guard let entries = try? JSONDecoder().decode([QueuedPlaybackIssue].self, from: data),
              entries.count <= PlaybackIssueQueuePolicy.maxQueueSize else {
            return PlaybackIssueQueueLoad(entries: [], isCorrupted: true)
        }
        return PlaybackIssueQueueLoad(entries: entries, isCorrupted: false)
    }

    public static func save(
        _ entries: [QueuedPlaybackIssue],
        ownerKey: String,
        defaults: UserDefaults = .standard
    ) {
        let bounded = Array(entries.prefix(PlaybackIssueQueuePolicy.maxQueueSize))
        guard let data = try? JSONEncoder().encode(bounded) else { return }
        defaults.set(data, forKey: key(ownerKey: ownerKey))
    }

    private static func key(ownerKey: String) -> String {
        "\(keyPrefix)\(OfflineDownloadScope.directoryName(ownerKey: ownerKey))"
    }
}

public enum SearchHistoryStore {
    private static let keyPrefix = "search.history."

    public static func load(ownerKey: String, defaults: UserDefaults = .standard) -> [String] {
        defaults.stringArray(forKey: key(ownerKey: ownerKey)) ?? []
    }

    public static func save(_ entries: [String], ownerKey: String, defaults: UserDefaults = .standard) {
        defaults.set(entries, forKey: key(ownerKey: ownerKey))
    }

    public static func clear(ownerKey: String, defaults: UserDefaults = .standard) {
        defaults.removeObject(forKey: key(ownerKey: ownerKey))
    }

    private static func key(ownerKey: String) -> String {
        "\(keyPrefix)\(OfflineDownloadScope.directoryName(ownerKey: ownerKey))"
    }
}

/// Small, versioned Home cards used only for offline fallback.
/// Stream URLs, media sources, tokens and credentials are intentionally excluded.
public struct HomeSnapshotCard: Codable, Equatable, Hashable, Sendable {
    public let id: String
    public let name: String
    public let type: String?
    public let overview: String?
    public let productionYear: Int?
    public let genres: [String]
    public let communityRating: Double?
    public let officialRating: String?
    public let primaryImageTag: String?
    public let backdropImageTags: [String]?
    public let imageTags: [String: String]?
    public let seriesId: String?
    public let seriesName: String?
    public let seasonId: String?
    public let seasonName: String?
    public let indexNumber: Int?
    public let parentIndexNumber: Int?
    public let channelId: String?
    public let channelName: String?
    public let startDate: String?
    public let endDate: String?
    public let playedPercentage: Double?
    public let playbackPositionTicks: Int64
    public let isFavorite: Bool
    public let isPlayed: Bool

    public init(item: MediaItem) {
        id = item.id
        name = item.name
        type = item.type
        overview = item.overview
        productionYear = item.productionYear
        genres = item.genres
        communityRating = item.communityRating
        officialRating = item.officialRating
        primaryImageTag = item.primaryImageTag
        backdropImageTags = item.backdropImageTags
        imageTags = item.imageTags
        seriesId = item.seriesId
        seriesName = item.seriesName
        seasonId = item.seasonId
        seasonName = item.seasonName
        indexNumber = item.indexNumber
        parentIndexNumber = item.parentIndexNumber
        channelId = item.channelId
        channelName = item.channelName
        startDate = item.startDate
        endDate = item.endDate
        playedPercentage = item.playedPercentage
        playbackPositionTicks = item.playbackPositionTicks
        isFavorite = item.isFavorite
        isPlayed = item.isPlayed
    }

    public var mediaItem: MediaItem {
        MediaItem(
            id: id,
            name: name,
            type: type,
            overview: overview,
            productionYear: productionYear,
            genres: genres,
            communityRating: communityRating,
            officialRating: officialRating,
            primaryImageTag: primaryImageTag,
            backdropImageTags: backdropImageTags,
            imageTags: imageTags,
            seriesId: seriesId,
            seriesName: seriesName,
            seasonId: seasonId,
            seasonName: seasonName,
            indexNumber: indexNumber,
            parentIndexNumber: parentIndexNumber,
            channelId: channelId,
            channelName: channelName,
            startDate: startDate,
            endDate: endDate,
            playedPercentage: playedPercentage,
            playbackPositionTicks: playbackPositionTicks,
            isFavorite: isFavorite,
            isPlayed: isPlayed
        )
    }
}

public struct HomeSnapshot: Codable, Equatable, Sendable {
    public let schemaVersion: Int
    public let scope: String
    public let resumeSavedAtEpochMillis: Int64
    public let favoritesSavedAtEpochMillis: Int64
    public let resumeItems: [HomeSnapshotCard]
    public let favoriteItems: [HomeSnapshotCard]

    public init(
        schemaVersion: Int = 1,
        scope: String,
        resumeSavedAtEpochMillis: Int64,
        favoritesSavedAtEpochMillis: Int64,
        resumeItems: [HomeSnapshotCard],
        favoriteItems: [HomeSnapshotCard]
    ) {
        self.schemaVersion = schemaVersion
        self.scope = scope
        self.resumeSavedAtEpochMillis = resumeSavedAtEpochMillis
        self.favoritesSavedAtEpochMillis = favoritesSavedAtEpochMillis
        self.resumeItems = resumeItems
        self.favoriteItems = favoriteItems
    }
}

public enum HomeSnapshotPolicy {
    public static func shouldUseCache(for error: Error) -> Bool {
        PlaybackRecoveryPolicy.isTransientNetworkError(error)
    }
}

public enum HomeSnapshotStore {
    private static let keyPrefix = "home.snapshot.v1."

    public static func load(ownerKey: String, defaults: UserDefaults = .standard) -> HomeSnapshot? {
        let scope = OfflineDownloadScope.directoryName(ownerKey: ownerKey)
        guard let data = defaults.data(forKey: key(ownerKey: ownerKey)),
              let snapshot = try? JSONDecoder().decode(HomeSnapshot.self, from: data),
              snapshot.schemaVersion == 1,
              snapshot.scope == scope else {
            return nil
        }
        return snapshot
    }

    public static func save(
        resumeItems: [MediaItem]?,
        favoriteItems: [MediaItem]?,
        ownerKey: String,
        nowEpochMillis: Int64 = Int64(Date().timeIntervalSince1970 * 1_000),
        defaults: UserDefaults = .standard
    ) {
        let scope = OfflineDownloadScope.directoryName(ownerKey: ownerKey)
        let current = load(ownerKey: ownerKey, defaults: defaults)
        let snapshot = HomeSnapshot(
            scope: scope,
            resumeSavedAtEpochMillis: resumeItems == nil ? current?.resumeSavedAtEpochMillis ?? 0 : nowEpochMillis,
            favoritesSavedAtEpochMillis: favoriteItems == nil ? current?.favoritesSavedAtEpochMillis ?? 0 : nowEpochMillis,
            resumeItems: (resumeItems ?? current?.resumeItems.map(\.mediaItem) ?? []).map(HomeSnapshotCard.init),
            favoriteItems: (favoriteItems ?? current?.favoriteItems.map(\.mediaItem) ?? []).map(HomeSnapshotCard.init)
        )
        guard let data = try? JSONEncoder().encode(snapshot) else { return }
        defaults.set(data, forKey: key(ownerKey: ownerKey))
    }

    private static func key(ownerKey: String) -> String {
        "\(keyPrefix)\(OfflineDownloadScope.directoryName(ownerKey: ownerKey))"
    }
}

/// Formats the server's UTC timestamps for the viewer's local time zone.
///
/// Jellyfin may emit ISO-8601 timestamps with or without fractional seconds.
/// Invalid or missing values return nil so the UI never presents a plausible,
/// but incorrect, recording time.
public enum LiveTVDateFormatting {
    public static func recordingStartLabel(
        _ raw: String?,
        timeZone: TimeZone = .current,
        locale: Locale = .current
    ) -> String? {
        guard let raw, !raw.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              let date = parseServerDate(raw) else { return nil }

        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = locale
        formatter.timeZone = timeZone
        formatter.dateFormat = "yyyy-MM-dd HH:mm"
        return formatter.string(from: date)
    }

    private static func parseServerDate(_ raw: String) -> Date? {
        let options: ISO8601DateFormatter.Options = [.withInternetDateTime, .withFractionalSeconds]
        let fractional = ISO8601DateFormatter()
        fractional.formatOptions = options
        if let date = fractional.date(from: raw) { return date }

        let plain = ISO8601DateFormatter()
        plain.formatOptions = [.withInternetDateTime]
        if let date = plain.date(from: raw) { return date }

        let withoutZone = DateFormatter()
        withoutZone.calendar = Calendar(identifier: .gregorian)
        withoutZone.locale = Locale(identifier: "en_US_POSIX")
        withoutZone.timeZone = TimeZone(secondsFromGMT: 0)
        withoutZone.dateFormat = "yyyy-MM-dd'T'HH:mm:ss.SSSSSSS"
        if let date = withoutZone.date(from: raw) { return date }
        withoutZone.dateFormat = "yyyy-MM-dd'T'HH:mm:ss"
        return withoutZone.date(from: raw)
    }
}

public struct LyricLine: Decodable, Hashable, Sendable {
    public let text: String
    public let start: Int64?

    private enum CodingKeys: String, CodingKey {
        case text = "Text"
        case start = "Start"
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        text = try values.decode(String.self, forKey: .text)
        start = try values.decodeIfPresent(Int64.self, forKey: .start)
    }
}

public struct MediaDeepLink: Equatable, Sendable {
    public let itemID: String
    public let serverID: String?

    public init(itemID: String, serverID: String? = nil) {
        self.itemID = itemID
        self.serverID = serverID
    }
}

public enum MediaDeepLinkParser {
    public static func itemID(from url: URL) -> String? {
        link(from: url)?.itemID
    }

    public static func link(from url: URL) -> MediaDeepLink? {
        guard let components = URLComponents(url: url, resolvingAgainstBaseURL: false),
              let scheme = components.scheme?.lowercased() else { return nil }
        let isCustomLink = scheme == "mulletaflix"
        let isOfficialWebLink = ["http", "https"].contains(scheme)
            && components.host?.caseInsensitiveCompare("mulletaflix.duckdns.org") == .orderedSame
            && (components.path == "/web" || components.path.hasPrefix("/web/"))
        guard isCustomLink || isOfficialWebLink else { return nil }

        let reserved = Set(["web", "details", "item"])
        let pathID = components.path
            .split(separator: "/")
            .map(String.init)
            .first { !reserved.contains($0.lowercased()) }
        let customHostID = isCustomLink ? components.host.flatMap { reserved.contains($0.lowercased()) ? nil : $0 } : nil
        let fragmentComponents = components.fragment
            .flatMap { fragment in
                let query = fragment.hasPrefix("?") ? String(fragment.dropFirst()) : fragment
                return URLComponents(string: "https://mulletaflix.invalid/?\(query)")
            }
        let fragmentID = fragmentComponents?.queryItems?.first(where: { $0.name == "id" })?.value
        guard let itemID = [
            components.queryItems?.first(where: { $0.name == "id" })?.value,
            fragmentID,
            customHostID,
            pathID
        ]
        .compactMap { $0?.trimmingCharacters(in: .whitespacesAndNewlines) }
        .first(where: { !$0.isEmpty && $0.count <= 128 }) else { return nil }
        let serverID = [
            components.queryItems?.first(where: { $0.name == "serverId" })?.value,
            fragmentComponents?.queryItems?.first(where: { $0.name == "serverId" })?.value
        ]
            .compactMap { $0 }
            .first
            .flatMap { value in
                let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
                return trimmed.isEmpty || trimmed.count > 128 ? nil : trimmed
            }
        return MediaDeepLink(itemID: itemID, serverID: serverID)
    }
}

public enum LibraryPlayedFilter: String, CaseIterable, Identifiable, Sendable {
    case all
    case played
    case unplayed

    public var id: String { rawValue }

    public var title: String {
        switch self {
        case .all: return "Todos"
        case .played: return "Assistidos"
        case .unplayed: return "Não assistidos"
        }
    }

    public var isPlayed: Bool? {
        switch self {
        case .all: return nil
        case .played: return true
        case .unplayed: return false
        }
    }
}

public struct LibraryLetterTarget: Hashable, Sendable {
    public let letter: String
    public let itemID: String

    public init(letter: String, itemID: String) {
        self.letter = letter
        self.itemID = itemID
    }
}

public enum LibraryLetterIndexPolicy {
    public static func targets(items: [MediaItem]) -> [LibraryLetterTarget] {
        var seen = Set<String>()
        return items.compactMap { item in
            let normalized = item.name
                .trimmingCharacters(in: .whitespacesAndNewlines)
                .folding(options: [.diacriticInsensitive, .widthInsensitive], locale: .current)
                .uppercased()
            let first = normalized.first.map(String.init).flatMap { $0 >= "A" && $0 <= "Z" ? $0 : nil } ?? "#"
            guard seen.insert(first).inserted else { return nil }
            return LibraryLetterTarget(letter: first, itemID: item.id)
        }
    }
}

public enum OfflineDownloadPolicy {
    public static func canStart(
        queuePaused: Bool,
        wifiOnly: Bool,
        wifiAvailable: Bool?,
        networkAvailable: Bool?
    ) -> Bool {
        networkAvailable != false && !queuePaused && (!wifiOnly || wifiAvailable == true)
    }
}

public enum OfflineDownloadFailurePolicy {
    public static let insufficientStorageMessage =
        "Não há espaço suficiente no dispositivo para concluir este download. Libere espaço e tente novamente."

    public static func isInsufficientStorage(_ error: Error) -> Bool {
        var current: NSError? = error as NSError
        for _ in 0..<8 {
            guard let value = current else { return false }
            if (value.domain == NSPOSIXErrorDomain && value.code == 28) ||
                (value.domain == NSCocoaErrorDomain && value.code == 640) {
                return true
            }
            current = value.userInfo[NSUnderlyingErrorKey] as? NSError
        }
        return false
    }
}

public enum NetworkRequestPolicy {
    public static let offlineMessage = "Você está offline. A atualização será retomada quando a conexão voltar."

    public static func canRequest(isNetworkAvailable: Bool?) -> Bool {
        isNetworkAvailable != false
    }
}

public enum OfflinePlaybackPositionPolicy {
    public static func normalized(positionSeconds: Double, durationSeconds: Double?) -> Double? {
        guard positionSeconds.isFinite, positionSeconds >= 0 else { return nil }
        guard let durationSeconds, durationSeconds.isFinite, durationSeconds > 0 else {
            return positionSeconds
        }
        return min(positionSeconds, durationSeconds)
    }
}

public enum PlaybackProgressPolicy {
    public static func normalized(positionSeconds: Double, durationSeconds: Double?) -> Double? {
        guard positionSeconds.isFinite, positionSeconds >= 0 else { return nil }
        guard let durationSeconds, durationSeconds.isFinite, durationSeconds > 0 else {
            return positionSeconds
        }
        return min(positionSeconds, durationSeconds)
    }
}

public enum PlaybackTicksPolicy {
    private static let ticksPerSecond = 10_000_000.0

    public static func fromSeconds(_ positionSeconds: Double, durationSeconds: Double? = nil) -> Int64 {
        guard let normalized = PlaybackProgressPolicy.normalized(
            positionSeconds: positionSeconds,
            durationSeconds: durationSeconds
        ) else { return 0 }
        let maximumSeconds = Double(Int64.max) / ticksPerSecond
        guard normalized < maximumSeconds else { return Int64.max }
        return Int64(normalized * ticksPerSecond)
    }
}

public struct OfflineEpisodeDescriptor: Equatable, Sendable {
    public let itemID: String
    public let seriesID: String
    public let seasonNumber: Int
    public let episodeNumber: Int

    public init(itemID: String, seriesID: String, seasonNumber: Int, episodeNumber: Int) {
        self.itemID = itemID
        self.seriesID = seriesID
        self.seasonNumber = seasonNumber
        self.episodeNumber = episodeNumber
    }
}

public enum OfflineNextEpisodePolicy {
    public static func next(
        after current: OfflineEpisodeDescriptor,
        candidates: [OfflineEpisodeDescriptor]
    ) -> OfflineEpisodeDescriptor? {
        candidates
            .filter { candidate in
                guard candidate.itemID != current.itemID,
                      candidate.seriesID == current.seriesID else { return false }
                return candidate.seasonNumber > current.seasonNumber ||
                    (candidate.seasonNumber == current.seasonNumber && candidate.episodeNumber > current.episodeNumber)
            }
            .min {
                if $0.seasonNumber != $1.seasonNumber { return $0.seasonNumber < $1.seasonNumber }
                if $0.episodeNumber != $1.episodeNumber { return $0.episodeNumber < $1.episodeNumber }
                return $0.itemID < $1.itemID
            }
    }
}

public enum SearchNetworkPolicy {
    public static let offlineMessage = "Você está offline. A busca será retomada quando a conexão voltar."

    public static func canRequest(isNetworkAvailable: Bool?) -> Bool {
        NetworkRequestPolicy.canRequest(isNetworkAvailable: isNetworkAvailable)
    }
}

public enum OfflineArtworkPolicy {
    public static let maximumBytes = 10 * 1024 * 1024

    public static func fileName(for itemID: String) -> String {
        let encodedID = Data(itemID.utf8).base64EncodedString()
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "=", with: "")
        return "\(encodedID)-artwork.image"
    }

    public static func accepts(_ data: Data) -> Bool {
        !data.isEmpty && data.count <= maximumBytes
    }

    public static func accepts(contentType: String?) -> Bool {
        guard let contentType else { return true }
        let mediaType = contentType
            .split(separator: ";", maxSplits: 1, omittingEmptySubsequences: true)
            .first
            .map(String.init) ?? ""
        return mediaType.trimmingCharacters(in: .whitespacesAndNewlines).lowercased().hasPrefix("image/")
    }
}

/// Decides whether a foreground transition should start a catalog refresh.
///
/// A transition can happen while the initial load is still running. Starting a
/// second request in that window invalidates the first request's result and can
/// leave the UI dependent on whichever response finishes last.
public enum ForegroundRefreshPolicy {
    public static func shouldRefresh(
        isSignedIn: Bool,
        isNetworkAvailable: Bool?,
        isHomeLoading: Bool,
        isLibrariesLoading: Bool,
        isLiveTVLoading: Bool
    ) -> Bool {
        isSignedIn && isNetworkAvailable != false &&
            !isHomeLoading && !isLibrariesLoading && !isLiveTVLoading
    }
}

public struct UserSession: Codable, Sendable, Equatable {
    public let serverURL: URL
    public let accessToken: String
    public let userID: String
    public let userName: String

    public init(serverURL: URL, accessToken: String, userID: String, userName: String) {
        self.serverURL = serverURL
        self.accessToken = accessToken
        self.userID = userID
        self.userName = userName
    }
}

public struct RegistrationResult: Decodable, Sendable {
    public let success: Bool
    public let message: String?

    private enum CodingKeys: String, CodingKey {
        case success = "Success"
        case message = "Message"
    }
}

public struct PublicUser: Decodable, Identifiable, Hashable, Sendable {
    public let id: String
    public let name: String

    private enum CodingKeys: String, CodingKey {
        case id = "Id"
        case name = "Name"
    }
}

public struct ServerInfo: Decodable, Equatable, Sendable {
    public let serverName: String?
    public let version: String?
    public let productName: String?
    public let operatingSystem: String?
    public let id: String?
    public let startupWizardCompleted: Bool

    private enum CodingKeys: String, CodingKey {
        case serverName = "ServerName"
        case version = "Version"
        case productName = "ProductName"
        case operatingSystem = "OperatingSystem"
        case id = "Id"
        case startupWizardCompleted = "StartupWizardCompleted"
    }

    public init(serverName: String? = nil, version: String? = nil, productName: String? = nil, operatingSystem: String? = nil, id: String? = nil, startupWizardCompleted: Bool = true) {
        self.serverName = serverName
        self.version = version
        self.productName = productName
        self.operatingSystem = operatingSystem
        self.id = id
        self.startupWizardCompleted = startupWizardCompleted
    }

    public var displayName: String {
        serverName ?? productName ?? "Servidor MulletaFlix"
    }
}

public struct BrandingOptions: Decodable, Equatable, Sendable {
    public let loginDisclaimer: String?
    public let customCSS: String?
    public let splashscreenEnabled: Bool

    private enum CodingKeys: String, CodingKey {
        case loginDisclaimer = "LoginDisclaimer"
        case customCSS = "CustomCss"
        case splashscreenEnabled = "SplashscreenEnabled"
    }

    public init(loginDisclaimer: String? = nil, customCSS: String? = nil, splashscreenEnabled: Bool = true) {
        self.loginDisclaimer = loginDisclaimer
        self.customCSS = customCSS
        self.splashscreenEnabled = splashscreenEnabled
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        loginDisclaimer = try values.decodeIfPresent(String.self, forKey: .loginDisclaimer)
        customCSS = try values.decodeIfPresent(String.self, forKey: .customCSS)
        splashscreenEnabled = try values.decodeIfPresent(Bool.self, forKey: .splashscreenEnabled) ?? true
    }
}

public struct SavedServer: Codable, Identifiable, Hashable, Sendable {
    public let url: String
    public let name: String
    public let version: String?
    public let serverID: String?

    public var id: String { url }

    public init(url: String, name: String, version: String? = nil, serverID: String? = nil) {
        self.url = url
        self.name = name
        self.version = version
        self.serverID = serverID
    }

    private enum CodingKeys: String, CodingKey {
        case url
        case name
        case version
        case serverID = "serverId"
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        url = try values.decode(String.self, forKey: .url)
        name = try values.decode(String.self, forKey: .name)
        version = try values.decodeIfPresent(String.self, forKey: .version)
        serverID = try values.decodeIfPresent(String.self, forKey: .serverID)
    }
}

public struct SearchHint: Decodable, Identifiable, Hashable, Sendable {
    public let itemID: String
    public let name: String
    public let type: String?
    public let productionYear: Int?
    public let series: String?

    public var id: String { itemID }

    public init(itemID: String, name: String, type: String? = nil, productionYear: Int? = nil, series: String? = nil) {
        self.itemID = itemID
        self.name = name
        self.type = type
        self.productionYear = productionYear
        self.series = series
    }

    private enum CodingKeys: String, CodingKey {
        case itemID = "ItemId"
        case name = "Name"
        case type = "Type"
        case productionYear = "ProductionYear"
        case series = "Series"
    }
}

public struct MediaSuggestion: Decodable, Hashable, Sendable {
    public let title: String
    public let mediaType: String
    public let year: Int?

    private enum CodingKeys: String, CodingKey {
        case title = "Title"
        case mediaType = "MediaType"
        case year = "Year"
    }

    public init(title: String, mediaType: String, year: Int? = nil) {
        self.title = title
        self.mediaType = mediaType
        self.year = year
    }
}

public struct SearchHintResult: Decodable, Sendable {
    public let hints: [SearchHint]
    public let totalRecordCount: Int

    private enum CodingKeys: String, CodingKey {
        case hints = "SearchHints"
        case totalRecordCount = "TotalRecordCount"
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        hints = try values.decodeIfPresent([SearchHint].self, forKey: .hints) ?? []
        totalRecordCount = try values.decodeIfPresent(Int.self, forKey: .totalRecordCount) ?? hints.count
    }
}

public enum SearchHintSelectionPolicy {
    public static func item(from hint: SearchHint) -> MediaItem {
        MediaItem(
            id: hint.itemID,
            name: hint.name,
            type: hint.type,
            productionYear: hint.productionYear,
            seriesName: hint.series
        )
    }
}

public enum MediaSegmentType: Decodable, Sendable, Equatable {
    case intro
    case outro
    case preview
    case recap
    case commercial
    case unknown

    public init(rawValue: String) {
        switch rawValue.lowercased() {
        case "intro": self = .intro
        case "outro": self = .outro
        case "preview": self = .preview
        case "recap": self = .recap
        case "commercial": self = .commercial
        default: self = .unknown
        }
    }

    public init(from decoder: Decoder) throws {
        let value = try decoder.singleValueContainer().decode(String.self)
        self.init(rawValue: value)
    }
}

public struct MediaSegment: Decodable, Identifiable, Sendable {
    public let id: String
    public let itemID: String
    public let type: MediaSegmentType
    public let startTicks: Int64
    public let endTicks: Int64

    public var startSeconds: Double { Double(startTicks) / 10_000_000 }
    public var endSeconds: Double { Double(endTicks) / 10_000_000 }

    private enum CodingKeys: String, CodingKey {
        case id = "Id"
        case itemID = "ItemId"
        case type = "Type"
        case startTicks = "StartTicks"
        case endTicks = "EndTicks"
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        id = try values.decodeIfPresent(String.self, forKey: .id) ?? UUID().uuidString
        itemID = try values.decodeIfPresent(String.self, forKey: .itemID) ?? ""
        type = MediaSegmentType(rawValue: try values.decodeIfPresent(String.self, forKey: .type) ?? "")
        startTicks = try values.decodeIfPresent(Int64.self, forKey: .startTicks) ?? 0
        endTicks = try values.decodeIfPresent(Int64.self, forKey: .endTicks) ?? 0
    }
}

public struct MediaSegmentQueryResult: Decodable, Sendable {
    public let items: [MediaSegment]
    private enum CodingKeys: String, CodingKey { case items = "Items" }
    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        items = try values.decodeIfPresent([MediaSegment].self, forKey: .items) ?? []
    }
}

public struct Chapter: Decodable, Hashable, Sendable {
    public let startPositionTicks: Int64
    public let name: String?
    public let imageTag: String?

    public var startSeconds: Double { Double(startPositionTicks) / 10_000_000 }

    private enum CodingKeys: String, CodingKey {
        case startPositionTicks = "StartPositionTicks"
        case name = "Name"
        case imageTag = "ImageTag"
    }
}

public struct MediaPerson: Decodable, Hashable, Sendable {
    public let id: String?
    public let name: String
    public let role: String?
    public let type: String?
    public let primaryImageTag: String?

    private enum CodingKeys: String, CodingKey {
        case id = "Id"
        case name = "Name"
        case role = "Role"
        case type = "Type"
        case primaryImageTag = "PrimaryImageTag"
    }
}

public struct MediaItem: Decodable, Identifiable, Hashable, Sendable {
    public let id: String
    public let name: String
    public let type: String?
    public let overview: String?
    public let productionYear: Int?
    public let genres: [String]
    public let communityRating: Double?
    public let officialRating: String?
    public let runtimeTicks: Int64?
    public let people: [MediaPerson]
    public let primaryImageTag: String?
    public let backdropImageTags: [String]?
    public let imageTags: [String: String]?
    public let isFolder: Bool
    public let collectionType: String?
    public let seriesId: String?
    public let seriesName: String?
    public let seasonId: String?
    public let seasonName: String?
    public let indexNumber: Int?
    public let parentIndexNumber: Int?
    public let channelId: String?
    public let channelName: String?
    public let startDate: String?
    public let endDate: String?
    public let chapters: [Chapter]
    public let mediaSources: [MediaSource]
    public let playedPercentage: Double?
    public let playbackPositionTicks: Int64
    public let isFavorite: Bool
    public let isPlayed: Bool

    public var qualityBadge: String? {
        mediaSources.compactMap(\.qualityBadge).first
    }

    private enum CodingKeys: String, CodingKey {
        case id = "Id"
        case name = "Name"
        case type = "Type"
        case overview = "Overview"
        case productionYear = "ProductionYear"
        case genres = "Genres"
        case communityRating = "CommunityRating"
        case officialRating = "OfficialRating"
        case runtimeTicks = "RunTimeTicks"
        case people = "People"
        case primaryImageTag = "PrimaryImageTag"
        case backdropImageTags = "BackdropImageTags"
        case imageTags = "ImageTags"
        case isFolder = "IsFolder"
        case collectionType = "CollectionType"
        case seriesId = "SeriesId"
        case seriesName = "SeriesName"
        case seasonId = "SeasonId"
        case seasonName = "SeasonName"
        case indexNumber = "IndexNumber"
        case parentIndexNumber = "ParentIndexNumber"
        case channelId = "ChannelId"
        case channelName = "ChannelName"
        case startDate = "StartDate"
        case endDate = "EndDate"
        case chapters = "Chapters"
        case mediaSources = "MediaSources"
        case userData = "UserData"
    }

    private struct UserData: Decodable {
        let playedPercentage: Double?
        let playbackPositionTicks: Int64
        let isFavorite: Bool
        let played: Bool

        enum CodingKeys: String, CodingKey {
            case playedPercentage = "PlayedPercentage"
            case playbackPositionTicks = "PlaybackPositionTicks"
            case isFavorite = "IsFavorite"
            case played = "Played"
        }

        init(from decoder: Decoder) throws {
            let values = try decoder.container(keyedBy: CodingKeys.self)
            playedPercentage = try values.decodeIfPresent(Double.self, forKey: .playedPercentage)
            playbackPositionTicks = try values.decodeIfPresent(Int64.self, forKey: .playbackPositionTicks) ?? 0
            isFavorite = try values.decodeIfPresent(Bool.self, forKey: .isFavorite) ?? false
            played = try values.decodeIfPresent(Bool.self, forKey: .played) ?? false
        }
    }

    public init(
        id: String,
        name: String,
        type: String? = nil,
        overview: String? = nil,
        productionYear: Int? = nil,
        genres: [String] = [],
        communityRating: Double? = nil,
        officialRating: String? = nil,
        runtimeTicks: Int64? = nil,
        people: [MediaPerson] = [],
        primaryImageTag: String? = nil,
        backdropImageTags: [String]? = nil,
        imageTags: [String: String]? = nil,
        isFolder: Bool = false,
        collectionType: String? = nil,
        seriesId: String? = nil,
        seriesName: String? = nil,
        seasonId: String? = nil,
        seasonName: String? = nil,
        indexNumber: Int? = nil,
        parentIndexNumber: Int? = nil,
        channelId: String? = nil,
        channelName: String? = nil,
        startDate: String? = nil,
        endDate: String? = nil,
        chapters: [Chapter] = [],
        mediaSources: [MediaSource] = [],
        playedPercentage: Double? = nil,
        playbackPositionTicks: Int64 = 0,
        isFavorite: Bool = false,
        isPlayed: Bool = false
    ) {
        self.id = id
        self.name = name
        self.type = type
        self.overview = overview
        self.productionYear = productionYear
        self.genres = genres
        self.communityRating = communityRating
        self.officialRating = officialRating
        self.runtimeTicks = runtimeTicks
        self.people = people
        self.primaryImageTag = primaryImageTag
        self.backdropImageTags = backdropImageTags
        self.imageTags = imageTags
        self.isFolder = isFolder
        self.collectionType = collectionType
        self.seriesId = seriesId
        self.seriesName = seriesName
        self.seasonId = seasonId
        self.seasonName = seasonName
        self.indexNumber = indexNumber
        self.parentIndexNumber = parentIndexNumber
        self.channelId = channelId
        self.channelName = channelName
        self.startDate = startDate
        self.endDate = endDate
        self.chapters = chapters
        self.mediaSources = mediaSources
        self.playedPercentage = playedPercentage
        self.playbackPositionTicks = playbackPositionTicks
        self.isFavorite = isFavorite
        self.isPlayed = isPlayed
    }

    public func withFavorite(_ value: Bool) -> MediaItem {
        MediaItem(id: id, name: name, type: type, overview: overview, productionYear: productionYear,
                  genres: genres, communityRating: communityRating, officialRating: officialRating,
                  runtimeTicks: runtimeTicks, people: people,
                  primaryImageTag: primaryImageTag, backdropImageTags: backdropImageTags,
                  imageTags: imageTags, isFolder: isFolder, collectionType: collectionType,
                  seriesId: seriesId, seriesName: seriesName, seasonId: seasonId, seasonName: seasonName,
                  indexNumber: indexNumber, parentIndexNumber: parentIndexNumber,
                  channelId: channelId, channelName: channelName, startDate: startDate, endDate: endDate,
                  chapters: chapters,
                  mediaSources: mediaSources,
                  playedPercentage: playedPercentage, playbackPositionTicks: playbackPositionTicks,
                  isFavorite: value, isPlayed: isPlayed)
    }

    public func withPlayed(_ value: Bool) -> MediaItem {
        MediaItem(id: id, name: name, type: type, overview: overview, productionYear: productionYear,
                  genres: genres, communityRating: communityRating, officialRating: officialRating,
                  runtimeTicks: runtimeTicks, people: people,
                  primaryImageTag: primaryImageTag, backdropImageTags: backdropImageTags,
                  imageTags: imageTags, isFolder: isFolder, collectionType: collectionType,
                  seriesId: seriesId, seriesName: seriesName, seasonId: seasonId, seasonName: seasonName,
                  indexNumber: indexNumber, parentIndexNumber: parentIndexNumber,
                  channelId: channelId, channelName: channelName, startDate: startDate, endDate: endDate,
                  chapters: chapters,
                  mediaSources: mediaSources,
                  playedPercentage: playedPercentage, playbackPositionTicks: playbackPositionTicks,
                  isFavorite: isFavorite, isPlayed: value)
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        id = try values.decode(String.self, forKey: .id)
        name = try values.decodeIfPresent(String.self, forKey: .name) ?? ""
        type = try values.decodeIfPresent(String.self, forKey: .type)
        overview = try values.decodeIfPresent(String.self, forKey: .overview)
        productionYear = try values.decodeIfPresent(Int.self, forKey: .productionYear)
        genres = try values.decodeIfPresent([String].self, forKey: .genres) ?? []
        communityRating = try values.decodeIfPresent(Double.self, forKey: .communityRating)
        officialRating = try values.decodeIfPresent(String.self, forKey: .officialRating)
        runtimeTicks = try values.decodeIfPresent(Int64.self, forKey: .runtimeTicks)
        people = try values.decodeIfPresent([MediaPerson].self, forKey: .people) ?? []
        primaryImageTag = try values.decodeIfPresent(String.self, forKey: .primaryImageTag)
        backdropImageTags = try values.decodeIfPresent([String].self, forKey: .backdropImageTags)
        imageTags = try values.decodeIfPresent([String: String].self, forKey: .imageTags)
        isFolder = try values.decodeIfPresent(Bool.self, forKey: .isFolder) ?? false
        collectionType = try values.decodeIfPresent(String.self, forKey: .collectionType)
        seriesId = try values.decodeIfPresent(String.self, forKey: .seriesId)
        seriesName = try values.decodeIfPresent(String.self, forKey: .seriesName)
        seasonId = try values.decodeIfPresent(String.self, forKey: .seasonId)
        seasonName = try values.decodeIfPresent(String.self, forKey: .seasonName)
        indexNumber = try values.decodeIfPresent(Int.self, forKey: .indexNumber)
        parentIndexNumber = try values.decodeIfPresent(Int.self, forKey: .parentIndexNumber)
        channelId = try values.decodeIfPresent(String.self, forKey: .channelId)
        channelName = try values.decodeIfPresent(String.self, forKey: .channelName)
        startDate = try values.decodeIfPresent(String.self, forKey: .startDate)
        endDate = try values.decodeIfPresent(String.self, forKey: .endDate)
        chapters = try values.decodeIfPresent([Chapter].self, forKey: .chapters) ?? []
        mediaSources = try values.decodeIfPresent([MediaSource].self, forKey: .mediaSources) ?? []
        let userData = try values.decodeIfPresent(UserData.self, forKey: .userData)
        playedPercentage = userData?.playedPercentage
        playbackPositionTicks = userData?.playbackPositionTicks ?? 0
        isFavorite = userData?.isFavorite ?? false
        isPlayed = userData?.played ?? false
    }
}

/// Keeps season-level offline preparation deterministic and free of duplicate queue entries.
public enum SeasonDownloadPolicy {
    public static func eligibleEpisodes(_ episodes: [MediaItem]) -> [MediaItem] {
        var seenIDs = Set<String>()
        return episodes.filter { episode in
            episode.type == "Episode" && seenIDs.insert(episode.id).inserted
        }
    }

    public static func pendingEpisodes(_ episodes: [MediaItem], activeItemIDs: Set<String>) -> [MediaItem] {
        episodes.filter { !activeItemIDs.contains($0.id) }
    }
}

public struct MediaStream: Decodable, Hashable, Sendable {
    public let index: Int?
    public let type: String?
    public let codec: String?
    public let language: String?
    public let displayLanguage: String?
    public let title: String?
    public let displayTitle: String?
    public let isDefault: Bool
    public let isForced: Bool
    public let isExternal: Bool
    public let deliveryURL: String?
    public let width: Int?
    public let height: Int?
    public let bitRate: Int?

    public init(
        index: Int?,
        type: String?,
        codec: String? = nil,
        language: String? = nil,
        displayLanguage: String? = nil,
        title: String? = nil,
        displayTitle: String? = nil,
        isDefault: Bool = false,
        isForced: Bool = false,
        isExternal: Bool = false,
        deliveryURL: String? = nil,
        width: Int? = nil,
        height: Int? = nil,
        bitRate: Int? = nil
    ) {
        self.index = index
        self.type = type
        self.codec = codec
        self.language = language
        self.displayLanguage = displayLanguage
        self.title = title
        self.displayTitle = displayTitle
        self.isDefault = isDefault
        self.isForced = isForced
        self.isExternal = isExternal
        self.deliveryURL = deliveryURL
        self.width = width
        self.height = height
        self.bitRate = bitRate
    }

    private enum CodingKeys: String, CodingKey {
        case index = "Index"
        case type = "Type"
        case codec = "Codec"
        case language = "Language"
        case displayLanguage = "DisplayLanguage"
        case title = "Title"
        case displayTitle = "DisplayTitle"
        case isDefault = "IsDefault"
        case isForced = "IsForced"
        case isExternal = "IsExternal"
        case deliveryURL = "DeliveryUrl"
        case width = "Width"
        case height = "Height"
        case bitRate = "BitRate"
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        index = try values.decodeIfPresent(Int.self, forKey: .index)
        type = try values.decodeIfPresent(String.self, forKey: .type)
        codec = try values.decodeIfPresent(String.self, forKey: .codec)
        language = try values.decodeIfPresent(String.self, forKey: .language)
        displayLanguage = try values.decodeIfPresent(String.self, forKey: .displayLanguage)
        title = try values.decodeIfPresent(String.self, forKey: .title)
        displayTitle = try values.decodeIfPresent(String.self, forKey: .displayTitle)
        isDefault = try values.decodeIfPresent(Bool.self, forKey: .isDefault) ?? false
        isForced = try values.decodeIfPresent(Bool.self, forKey: .isForced) ?? false
        isExternal = try values.decodeIfPresent(Bool.self, forKey: .isExternal) ?? false
        deliveryURL = try values.decodeIfPresent(String.self, forKey: .deliveryURL)
        width = try values.decodeIfPresent(Int.self, forKey: .width)
        height = try values.decodeIfPresent(Int.self, forKey: .height)
        bitRate = try values.decodeIfPresent(Int.self, forKey: .bitRate)
    }
}

public enum ExternalSubtitlePolicy {
    public static func mimeType(codec: String?, deliveryURL: String?) -> String? {
        let normalizedCodec = codec?.split(separator: ",", maxSplits: 1).first
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() }
        let extensionName = URL(string: deliveryURL ?? "")?.pathExtension.lowercased()
        // The delivery URL describes the bytes received by the player. Jellyfin
        // may convert an SRT source into VTT, so the URL must win over the
        // source codec whenever it exposes a supported format.
        switch extensionName {
        case "srt": return "application/x-subrip"
        case "vtt": return "text/vtt"
        case "ass", "ssa": return "text/x-ssa"
        case "ttml", "dfxp": return "application/ttml+xml"
        default: break
        }
        switch normalizedCodec {
        case "srt", "subrip": return "application/x-subrip"
        case "vtt", "webvtt": return "text/vtt"
        case "ass", "ssa": return "text/x-ssa"
        case "ttml", "dfxp": return "application/ttml+xml"
        default: return nil
        }
    }

    public static func isPlayable(_ stream: MediaStream) -> Bool {
        stream.isExternal
            && stream.type?.caseInsensitiveCompare("Subtitle") == .orderedSame
            && stream.index.map { $0 >= 0 } == true
            && mimeType(codec: stream.codec, deliveryURL: stream.deliveryURL) != nil
    }

    public static func parse(_ data: Data, mimeType: String? = nil) -> [SubtitleCue] {
        _ = mimeType
        guard let raw = String(data: data, encoding: .utf8) else { return [] }
        let normalized = raw.replacingOccurrences(of: "\r\n", with: "\n")
            .replacingOccurrences(of: "\r", with: "\n")
        let blocks = normalized.components(separatedBy: "\n\n")
        return blocks.compactMap { block in
            let lines = block.split(separator: "\n", omittingEmptySubsequences: false).map(String.init)
            guard let timingIndex = lines.firstIndex(where: { $0.contains("-->") }) else { return nil }
            let timing = lines[timingIndex].components(separatedBy: "-->")
            guard timing.count == 2,
                  let start = timestamp(timing[0]),
                  let end = timestamp(timing[1].split(separator: " ", maxSplits: 1).first.map(String.init) ?? ""),
                  end > start else { return nil }
            let text = lines.dropFirst(timingIndex + 1).joined(separator: "\n")
                .trimmingCharacters(in: .whitespacesAndNewlines)
            guard !text.isEmpty else { return nil }
            return SubtitleCue(start: start, end: end, text: text)
        }
    }

    private static func timestamp(_ raw: String) -> Double? {
        let value = raw.trimmingCharacters(in: .whitespacesAndNewlines).replacingOccurrences(of: ",", with: ".")
        let parts = value.split(separator: ":").map(String.init)
        guard parts.count >= 2, let seconds = Double(parts.last ?? "") else { return nil }
        let minuteIndex = parts.count - 2
        guard let minutes = Double(parts[minuteIndex]) else { return nil }
        let hours = parts.dropLast(2).last.flatMap(Double.init) ?? 0
        return hours * 3_600 + minutes * 60 + seconds
    }
}

public struct SubtitleCue: Hashable, Sendable {
    public let start: Double
    public let end: Double
    public let text: String

    public init(start: Double, end: Double, text: String) {
        self.start = start
        self.end = end
        self.text = text
    }
}

public enum SubtitleAppearancePolicy {
    public static let minimumFontSize = 14.0
    public static let maximumFontSize = 36.0

    public static func normalizedFontSize(_ value: Double) -> Double {
        min(max(value, minimumFontSize), maximumFontSize)
    }
}

/// Resolves a persisted language preference to the server stream index space.
public enum TrackPreferencePolicy {
    public static func preferredStreamIndex(
        streams: [MediaStream],
        preferredLanguage: String?,
        serverDefaultIndex: Int?,
        isSubtitle: Bool
    ) -> Int? {
        let preference = MediaLanguage.canonicalize(preferredLanguage)
        if isSubtitle, preference == MediaLanguage.off { return -1 }

        if !preference.isEmpty, preference != MediaLanguage.original,
           let match = streams.first(where: { stream in
               let language = MediaLanguage.canonicalize(stream.language)
               let displayLanguage = MediaLanguage.canonicalize(stream.displayLanguage)
               return language == preference || displayLanguage == preference ||
                   language.hasPrefix("\(preference)-") || displayLanguage.hasPrefix("\(preference)-")
           }), let index = match.index {
            return index
        }

        if let serverDefaultIndex, streams.contains(where: { $0.index == serverDefaultIndex }) {
            return serverDefaultIndex
        }
        return streams.first(where: { $0.isDefault })?.index ?? streams.first?.index
    }
}

public enum PlaybackQualityPolicy {
    public static let presetChoices = ["4K", "1440p", "1080p", "720p", "480p"]
    public static let meteredAutoCap = "720p"

    public static func normalizedPreference(_ value: String?) -> String {
        let raw = value?.trimmingCharacters(in: .whitespacesAndNewlines).uppercased() ?? ""
        switch raw {
        case "", "AUTO", "AUTOMÁTICO", "AUTOMATICO": return "Auto"
        case "4K", "2160", "2160P": return "4K"
        case "1440", "1440P": return "1440p"
        case "1080", "1080P", "FULL HD": return "1080p"
        case "720", "720P", "HD": return "720p"
        case "480", "480P", "SD": return "480p"
        default:
            guard raw.hasSuffix("P"),
                  let height = Int(raw.dropLast()),
                  (144...4320).contains(height) else { return "Auto" }
            return "\(height)p"
        }
    }

    public static func settingsChoices(storedPreference: String?) -> [String] {
        let normalized = normalizedPreference(storedPreference)
        var choices = ["Auto"] + presetChoices
        if normalized != "Auto", !choices.contains(normalized) {
            choices.append(normalized)
        }
        return choices
    }

    public static func maxStreamingBitrate(for quality: String) -> Int64? {
        switch normalizedPreference(quality) {
        case "4K": return 20_000_000
        case "1440p": return 12_000_000
        case "1080p": return 8_000_000
        case "720p": return 4_000_000
        case "480p": return 2_000_000
        default:
            let normalized = normalizedPreference(quality)
            guard normalized.hasSuffix("p"),
                  let height = Int(normalized.dropLast()) else { return nil }
            return max(1_000_000, Int64(height) * Int64(height) * 4)
        }
    }

    /// Mirrors Android: Auto remains persisted as Auto, but is capped on metered networks.
    public static func effectivePreference(for quality: String?, isMetered: Bool) -> String {
        let normalized = normalizedPreference(quality)
        return normalized == "Auto" && isMetered ? meteredAutoCap : normalized
    }

    public static func effectiveStreamingBitrate(for quality: String?, isMetered: Bool) -> Int64? {
        maxStreamingBitrate(for: effectivePreference(for: quality, isMetered: isMetered))
    }

    public static func displayLabel(for quality: String?, isMetered: Bool) -> String {
        let normalized = normalizedPreference(quality)
        if normalized == "Auto" {
            return isMetered ? "Auto (até 720p nesta rede)" : "Auto"
        }
        return normalized
    }
}

public enum PlaybackResumePolicy {
    public static func initialPositionTicks(server: Int64, local: Int64, isLocal: Bool) -> Int64 {
        let serverPosition = max(0, server)
        let localPosition = max(0, local)
        guard isLocal else { return serverPosition }
        return localPosition > 0 ? localPosition : serverPosition
    }
}

/// Converts low-level playback failures into safe, actionable viewer messages.
///
/// Playback URLs contain session credentials. The raw AVPlayer error must never
/// be rendered directly because some URL-loading errors include the full URL.
public enum PlaybackErrorPolicy {
    public static func userFacingMessage(from error: Error?) -> String {
        let nsError = error as NSError?
        switch nsError?.code {
        case NSURLError.timedOut,
             NSURLError.networkConnectionLost,
             NSURLError.cannotConnectToHost,
             NSURLError.notConnectedToInternet:
            return "A conexão com o servidor foi interrompida. Verifique a rede e tente novamente."
        default:
            return sanitizedMessage(nsError?.localizedDescription)
        }
    }

    public static func sanitizedMessage(_ raw: String?) -> String {
        let fallback = "Não foi possível reproduzir esta mídia."
        guard let raw, !raw.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return fallback
        }
        let pattern = "(?i)(api_key|access_token|token|authorization)=([^&\\s]+)"
        guard let expression = try? NSRegularExpression(pattern: pattern) else { return raw }
        let range = NSRange(raw.startIndex..<raw.endIndex, in: raw)
        return expression.stringByReplacingMatches(
            in: raw,
            range: range,
            withTemplate: "$1=[redacted]"
        )
    }
}

/// Bounds automatic recovery of remote playback after transient network loss.
public enum PlaybackRecoveryPolicy {
    private static let retryDelaysMilliseconds: [Int] = [750, 1_500, 3_000]

    public static func shouldAutomaticallyRetry(
        error: Error?,
        attempt: Int,
        isLocal: Bool,
        networkAvailable: Bool?
    ) -> Bool {
        !isLocal && networkAvailable != false && attempt < retryDelaysMilliseconds.count && isTransientNetworkError(error)
    }

    public static func shouldRetryAfterNetworkRestored(
        wasOffline: Bool,
        isOnline: Bool,
        isLocal: Bool,
        hasPlaybackError: Bool,
        wasTransientNetworkFailure: Bool
    ) -> Bool {
        wasOffline && isOnline && !isLocal && hasPlaybackError && wasTransientNetworkFailure
    }

    public static func retryDelayMilliseconds(for attempt: Int) -> Int {
        retryDelaysMilliseconds[min(max(0, attempt), retryDelaysMilliseconds.count - 1)]
    }

    public static func isTransientNetworkError(_ error: Error?) -> Bool {
        guard let error else { return false }
        let nsError = error as NSError
        if isTransientNetworkCode(nsError.code) { return true }
        if let underlying = nsError.userInfo[NSUnderlyingErrorKey] as? Error {
            return isTransientNetworkError(underlying)
        }
        return false
    }

    private static func isTransientNetworkCode(_ code: Int) -> Bool {
        code == NSURLError.timedOut ||
            code == NSURLError.networkConnectionLost ||
            code == NSURLError.cannotConnectToHost ||
            code == NSURLError.notConnectedToInternet ||
            code == NSURLError.cannotFindHost
    }
}

/// Returns an intro target only for active, seekable playback with auto-skip enabled.
public enum AutomaticIntroSkipPolicy {
    public static func target(
        enabled: Bool,
        isPlaying: Bool,
        isSeekable: Bool,
        positionSeconds: Double,
        segment: MediaSegment?,
        pendingTargetSeconds: Double?
    ) -> Double? {
        guard enabled, isPlaying, isSeekable,
              let segment,
              segment.type == .intro,
              positionSeconds >= segment.startSeconds,
              positionSeconds < segment.endSeconds,
              segment.endSeconds > positionSeconds,
              segment.endSeconds != pendingTargetSeconds else { return nil }
        return segment.endSeconds
    }
}

/// Keeps authentication failures actionable without exposing transport jargon.
public enum AuthErrorPolicy {
    public static func authenticationMessage(for error: Error) -> String {
        if let apiError = error as? APIError {
            if case .serverMessage(let message) = apiError,
               !message.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                return message
            }
            if case .httpStatus(let code) = apiError {
                switch code {
                case 400, 401: return "Usuário ou senha inválidos. Confira os dados e tente novamente."
                case 403: return "Este usuário não tem permissão para acessar o servidor."
                case 404: return "Usuário ou servidor não encontrado."
                case 408, 504: return "O servidor demorou para responder. Tente novamente."
                default: break
                }
            }
        }
        return serverConnectionMessage(for: error)
    }

    public static func serverConnectionMessage(for error: Error) -> String {
        if let apiError = error as? APIError {
            switch apiError {
            case .serverMessage(let message):
                let trimmedMessage = message.trimmingCharacters(in: .whitespacesAndNewlines)
                return trimmedMessage.isEmpty ? "Não foi possível conectar ao servidor. Verifique a conexão e tente novamente." : message
            case .httpStatus(401): return "O servidor recusou a conexão anônima. Verifique o endereço."
            case .httpStatus(404): return "A API do MulletaFlix não foi encontrada nesse endereço."
            case .httpStatus(let code): return "O servidor respondeu com um erro (\(code)). Tente novamente."
            case .invalidServerURL: return apiError.localizedDescription
            default: break
            }
        }
        if let urlError = error as? URLError {
            switch urlError.code {
            case .cannotFindHost, .dnsLookupFailed:
                return "Servidor não encontrado. Verifique o endereço e a conexão com a internet."
            case .cannotConnectToHost, .networkConnectionLost, .notConnectedToInternet:
                return "Não foi possível conectar ao servidor. Verifique se ele está online."
            case .timedOut:
                return "O servidor demorou para responder. Tente novamente."
            default: break
            }
        }
        return "Não foi possível conectar ao servidor. Verifique a conexão e tente novamente."
    }

    public static func registrationMessage(for error: Error) -> String {
        if let apiError = error as? APIError,
           case .serverMessage(let message) = apiError,
           !message.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return message
        }
        return serverConnectionMessage(for: error)
    }
}

/// Keeps Quick Connect alive across transient transport failures without exposing
/// raw server or URLSession descriptions to the login surface.
public enum QuickConnectErrorPolicy {
    public static func shouldContinuePolling(after error: Error) -> Bool {
        guard let apiError = error as? APIError else { return true }
        switch apiError {
        case .httpStatus(401), .httpStatus(403), .httpStatus(404), .serverMessage(_):
            return false
        default:
            return true
        }
    }

    public static func terminalMessage(for error: Error) -> String? {
        guard let apiError = error as? APIError else { return nil }
        switch apiError {
        case .httpStatus(401), .httpStatus(403):
            return "Quick Connect está desativado ou requer autorização no servidor."
        case .httpStatus(404):
            return "Quick Connect não está disponível neste servidor."
        case .serverMessage(let message):
            let trimmed = message.trimmingCharacters(in: .whitespacesAndNewlines)
            return trimmed.isEmpty ? "Não foi possível confirmar o Quick Connect no servidor." : trimmed
        default:
            return nil
        }
    }
}

public struct MediaSource: Decodable, Hashable, Sendable {
    public let id: String?
    public let liveStreamId: String?
    public let openToken: String?
    public let requiresOpening: Bool
    public let supportsDirectPlay: Bool
    public let supportsDirectStream: Bool
    public let supportsTranscoding: Bool
    public let defaultAudioStreamIndex: Int?
    public let defaultSubtitleStreamIndex: Int?
    public let mediaStreams: [MediaStream]

    public var qualityBadge: String? {
        let maximumHeight = mediaStreams
            .filter { $0.type?.caseInsensitiveCompare("Video") == .orderedSame }
            .compactMap(\.height)
            .max() ?? 0
        if maximumHeight >= 2160 { return "4K" }
        if maximumHeight >= 720 { return "HD" }
        return nil
    }

    public var qualityLabels: [String] {
        var labels: [String] = []
        for height in mediaStreams
            .filter({ $0.type?.caseInsensitiveCompare("Video") == .orderedSame })
            .compactMap(\.height)
            .filter({ $0 > 0 })
            .sorted(by: >) {
            let label = height.qualityLabel
            if !labels.contains(label) { labels.append(label) }
        }
        return labels
    }

    private enum CodingKeys: String, CodingKey {
        case id = "Id"
        case liveStreamId = "LiveStreamId"
        case openToken = "OpenToken"
        case requiresOpening = "RequiresOpening"
        case supportsDirectPlay = "SupportsDirectPlay"
        case supportsDirectStream = "SupportsDirectStream"
        case supportsTranscoding = "SupportsTranscoding"
        case defaultAudioStreamIndex = "DefaultAudioStreamIndex"
        case defaultSubtitleStreamIndex = "DefaultSubtitleStreamIndex"
        case mediaStreams = "MediaStreams"
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        id = try values.decodeIfPresent(String.self, forKey: .id)
        liveStreamId = try values.decodeIfPresent(String.self, forKey: .liveStreamId)
        openToken = try values.decodeIfPresent(String.self, forKey: .openToken)
        requiresOpening = try values.decodeIfPresent(Bool.self, forKey: .requiresOpening) ?? false
        supportsDirectPlay = try values.decodeIfPresent(Bool.self, forKey: .supportsDirectPlay) ?? true
        supportsDirectStream = try values.decodeIfPresent(Bool.self, forKey: .supportsDirectStream) ?? true
        supportsTranscoding = try values.decodeIfPresent(Bool.self, forKey: .supportsTranscoding) ?? true
        defaultAudioStreamIndex = try values.decodeIfPresent(Int.self, forKey: .defaultAudioStreamIndex)
        defaultSubtitleStreamIndex = try values.decodeIfPresent(Int.self, forKey: .defaultSubtitleStreamIndex)
        mediaStreams = try values.decodeIfPresent([MediaStream].self, forKey: .mediaStreams) ?? []
    }
}

private extension Int {
    var qualityLabel: String {
        switch self {
        case 2160...: return "4K"
        case 1440...: return "1440p"
        default: return "\(self)p"
        }
    }
}

public struct PlaybackInfo: Decodable, Sendable {
    public let mediaSources: [MediaSource]
    public let playSessionId: String?
    public let errorCode: String?

    private enum CodingKeys: String, CodingKey {
        case mediaSources = "MediaSources"
        case playSessionId = "PlaySessionId"
        case errorCode = "ErrorCode"
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        mediaSources = try values.decodeIfPresent([MediaSource].self, forKey: .mediaSources) ?? []
        playSessionId = try values.decodeIfPresent(String.self, forKey: .playSessionId)
        errorCode = try values.decodeIfPresent(String.self, forKey: .errorCode)
    }
}

public struct ItemQueryResult: Decodable, Sendable {
    public let items: [MediaItem]
    public let totalRecordCount: Int
    public let hasExplicitTotalRecordCount: Bool

    private enum CodingKeys: String, CodingKey {
        case items = "Items"
        case totalRecordCount = "TotalRecordCount"
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        items = try values.decodeIfPresent([MediaItem].self, forKey: .items) ?? []
        let explicitTotal = try values.decodeIfPresent(Int.self, forKey: .totalRecordCount)
        hasExplicitTotalRecordCount = explicitTotal != nil
        totalRecordCount = explicitTotal ?? items.count
    }
}

public struct SearchItemsResult: Sendable {
    public let items: [MediaItem]
    public let totalRecordCount: Int?

    public init(items: [MediaItem], totalRecordCount: Int?) {
        self.items = items
        self.totalRecordCount = totalRecordCount
    }
}

public struct AuthenticationResult: Decodable, Sendable {
    public let accessToken: String?
    public let user: AuthenticatedUser?

    private enum CodingKeys: String, CodingKey {
        case accessToken = "AccessToken"
        case user = "User"
    }
}

public struct AuthenticatedUser: Decodable, Sendable {
    public let id: String
    public let name: String

    private enum CodingKeys: String, CodingKey {
        case id = "Id"
        case name = "Name"
    }
}

public struct UserProfile: Decodable, Sendable {
    public let id: String
    public let name: String
    public let serverId: String?
    public let primaryImageTag: String?
    public let policy: UserPolicy?
    public let configuration: UserConfiguration?

    public var isAdministrator: Bool { policy?.isAdministrator ?? false }
    public var canDownload: Bool { policy?.enableContentDownloading ?? true }
    public var canAccessLiveTV: Bool { policy?.enableLiveTVAccess ?? true }
    public var canPlayMedia: Bool { policy?.enableMediaPlayback ?? true }

    private enum CodingKeys: String, CodingKey {
        case id = "Id"
        case name = "Name"
        case serverId = "ServerId"
        case primaryImageTag = "PrimaryImageTag"
        case policy = "Policy"
        case configuration = "Configuration"
    }
}

public struct UserPolicy: Decodable, Sendable {
    public let isAdministrator: Bool
    public let enableContentDownloading: Bool
    public let enableLiveTVAccess: Bool
    public let enableMediaPlayback: Bool

    private enum CodingKeys: String, CodingKey {
        case isAdministrator = "IsAdministrator"
        case enableContentDownloading = "EnableContentDownloading"
        case enableLiveTVAccess = "EnableLiveTvAccess"
        case enableMediaPlayback = "EnableMediaPlayback"
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        isAdministrator = try values.decodeIfPresent(Bool.self, forKey: .isAdministrator) ?? false
        enableContentDownloading = try values.decodeIfPresent(Bool.self, forKey: .enableContentDownloading) ?? true
        enableLiveTVAccess = try values.decodeIfPresent(Bool.self, forKey: .enableLiveTVAccess) ?? true
        enableMediaPlayback = try values.decodeIfPresent(Bool.self, forKey: .enableMediaPlayback) ?? true
    }
}

public struct UserConfiguration: Decodable, Sendable {
    public let audioLanguagePreference: String?
    public let subtitleLanguagePreference: String?

    private enum CodingKeys: String, CodingKey {
        case audioLanguagePreference = "AudioLanguagePreference"
        case subtitleLanguagePreference = "SubtitleLanguagePreference"
    }
}

public struct QuickConnectResult: Decodable, Sendable {
    public let code: String
    public let secret: String
    public let authenticated: Bool

    private enum CodingKeys: String, CodingKey {
        case code = "Code"
        case secret = "Secret"
        case authenticated = "Authenticated"
    }
}

public struct LiveTVTimerDefaults: Decodable, Sendable {
    public let serviceName: String?
    public let channelId: String?
    public let name: String?
    public let overview: String?
    public let startDate: String?
    public let endDate: String?
    public let prePaddingSeconds: Int?
    public let postPaddingSeconds: Int?

    private enum CodingKeys: String, CodingKey {
        case serviceName = "ServiceName"
        case channelId = "ChannelId"
        case name = "Name"
        case overview = "Overview"
        case startDate = "StartDate"
        case endDate = "EndDate"
        case prePaddingSeconds = "PrePaddingSeconds"
        case postPaddingSeconds = "PostPaddingSeconds"
    }
}

public struct LiveTVTimer: Decodable, Sendable {
    public let id: String?
    public let programId: String?

    private enum CodingKeys: String, CodingKey {
        case id = "Id"
        case programId = "ProgramId"
    }
}

public struct LiveTVTimerQuery: Decodable, Sendable {
    public let items: [LiveTVTimer]

    private enum CodingKeys: String, CodingKey { case items = "Items" }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        items = try values.decodeIfPresent([LiveTVTimer].self, forKey: .items) ?? []
    }
}

public struct SyncPlayGroup: Decodable, Identifiable, Sendable {
    public let groupId: String
    public let groupName: String
    public let state: String?
    public let participants: [String]

    public var id: String { groupId }

    private enum CodingKeys: String, CodingKey {
        case groupId = "GroupId"
        case groupName = "GroupName"
        case state = "State"
        case participants = "Participants"
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        groupId = try values.decode(String.self, forKey: .groupId)
        groupName = try values.decode(String.self, forKey: .groupName)
        state = try values.decodeIfPresent(String.self, forKey: .state)
        participants = try values.decodeIfPresent([String].self, forKey: .participants) ?? []
    }
}

public enum RemotePlaybackCommand: String, CaseIterable, Sendable {
    case playPause = "PlayPause"
    case stop = "Stop"
    case seek = "Seek"
}

public enum RemotePlaybackPolicy {
    public static func seekPosition(currentTicks: Int64, deltaTicks: Int64, durationTicks: Int64?) -> Int64 {
        let base = max(0, currentTicks)
        let (sum, overflow) = base.addingReportingOverflow(deltaTicks)
        let target = overflow ? (deltaTicks >= 0 ? Int64.max : 0) : max(0, sum)
        guard let durationTicks, durationTicks > 0 else { return max(0, target) }
        return min(max(0, target), durationTicks)
    }

    public static func progress(positionTicks: Int64, durationTicks: Int64?) -> Double? {
        guard let durationTicks, durationTicks > 0 else { return nil }
        return min(1, max(0, Double(max(0, positionTicks)) / Double(durationTicks)))
    }
}

public enum RemotePlaybackRefreshPolicy {
    public static let intervalSeconds: Double = 5

    public static func shouldStartBackgroundRefresh(isLoading: Bool) -> Bool {
        !isLoading
    }
}

public enum SyncPlayRefreshPolicy {
    public static let intervalSeconds: Double = 5

    public static func shouldStartBackgroundRefresh(isLoading: Bool) -> Bool {
        !isLoading
    }
}

public enum LiveTVRefreshPolicy {
    public static let intervalSeconds: Double = 60

    public static func shouldStartBackgroundRefresh(isLoading: Bool) -> Bool {
        !isLoading
    }
}

public struct RemotePlaybackSession: Decodable, Identifiable, Hashable, Sendable {
    public let id: String
    public let deviceID: String?
    public let deviceName: String
    public let clientName: String
    public let itemName: String
    public let isPaused: Bool
    public let canSeek: Bool
    public let positionTicks: Int64
    public let durationTicks: Int64?

    private enum CodingKeys: String, CodingKey {
        case id = "Id"
        case deviceID = "DeviceId"
        case deviceName = "DeviceName"
        case clientName = "Client"
        case nowPlayingItem = "NowPlayingItem"
        case playState = "PlayState"
    }

    private struct ItemPayload: Decodable {
        let name: String?
        let runTimeTicks: Int64?
        private enum CodingKeys: String, CodingKey { case name = "Name"; case runTimeTicks = "RunTimeTicks" }
    }

    private struct PlayStatePayload: Decodable {
        let isPaused: Bool?
        let canSeek: Bool?
        let positionTicks: Int64?
        private enum CodingKeys: String, CodingKey {
            case isPaused = "IsPaused"
            case canSeek = "CanSeek"
            case positionTicks = "PositionTicks"
        }
    }

    public init(
        id: String,
        deviceID: String? = nil,
        deviceName: String,
        clientName: String,
        itemName: String,
        isPaused: Bool,
        canSeek: Bool,
        positionTicks: Int64,
        durationTicks: Int64? = nil
    ) {
        self.id = id
        self.deviceID = deviceID
        self.deviceName = deviceName
        self.clientName = clientName
        self.itemName = itemName
        self.isPaused = isPaused
        self.canSeek = canSeek
        self.positionTicks = positionTicks
        self.durationTicks = durationTicks
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        id = try values.decode(String.self, forKey: .id)
        deviceID = try values.decodeIfPresent(String.self, forKey: .deviceID)
        deviceName = try values.decodeIfPresent(String.self, forKey: .deviceName) ?? "Dispositivo"
        clientName = try values.decodeIfPresent(String.self, forKey: .clientName) ?? "MulletaFlix"
        let item = try values.decodeIfPresent(ItemPayload.self, forKey: .nowPlayingItem)
        let normalizedItemName = item?.name?.trimmingCharacters(in: .whitespacesAndNewlines)
        if let normalizedItemName, !normalizedItemName.isEmpty {
            itemName = normalizedItemName
        } else {
            itemName = "Reproduzindo mídia"
        }
        durationTicks = item?.runTimeTicks
        let playState = try values.decodeIfPresent(PlayStatePayload.self, forKey: .playState)
        isPaused = playState?.isPaused ?? false
        canSeek = playState?.canSeek ?? false
        positionTicks = playState?.positionTicks ?? 0
    }
}

public struct Playlist: Decodable, Identifiable, Hashable, Sendable {
    public let id: String
    public let name: String

    private enum CodingKeys: String, CodingKey {
        case id = "Id"
        case name = "Name"
    }

    public init(id: String, name: String) {
        self.id = id
        self.name = name
    }
}

public struct DiscoveredServer: Decodable, Equatable, Sendable {
    public let address: URL
    public let name: String
    public let version: String?
    public let id: String?

    public init(address: URL, name: String, version: String? = nil, id: String? = nil) {
        self.address = address
        self.name = name
        self.version = version
        self.id = id
    }

    private enum CodingKeys: String, CodingKey {
        case address = "Address"
        case name = "Name"
        case version = "Version"
        case id = "Id"
        case serverId = "ServerId"
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        guard let addressString = try values.decodeIfPresent(String.self, forKey: .address),
              let discoveredAddress = URL(string: addressString) else {
            throw APIError.invalidServerURL
        }
        self.address = discoveredAddress
        self.name = try values.decodeIfPresent(String.self, forKey: .name) ?? "MulletaFlix"
        self.version = try values.decodeIfPresent(String.self, forKey: .version)
        self.id = try values.decodeIfPresent(String.self, forKey: .id)
            ?? (try values.decodeIfPresent(String.self, forKey: .serverId))
    }
}

public enum ServerDiscoverySelectionPolicy {
    public static func selectedServer(
        from discoveredServers: [DiscoveredServer],
        savedServerID: String?
    ) -> DiscoveredServer? {
        let expectedID = savedServerID?.trimmingCharacters(in: .whitespacesAndNewlines)
        if let expectedID, !expectedID.isEmpty {
            return discoveredServers.first { server in
                guard let serverID = server.id?.trimmingCharacters(in: .whitespacesAndNewlines),
                      !serverID.isEmpty else { return false }
                return serverID.caseInsensitiveCompare(expectedID) == .orderedSame
            }
        }
        return discoveredServers.count == 1 ? discoveredServers.first : nil
    }
}

public enum ServerDiscoveryTimingPolicy {
    public static let maximumWindow: TimeInterval = 10

    public static func boundedWindow(_ requested: TimeInterval) -> TimeInterval {
        min(max(requested, 0), maximumWindow)
    }

    public static func remainingWindow(total: TimeInterval, elapsed: TimeInterval) -> TimeInterval {
        max(0, boundedWindow(total) - max(0, elapsed))
    }
}

public enum OfflineStoragePolicy {
    private static let bytesPerMiB: Int64 = 1024 * 1024
    private static let bytesPerGiB: Int64 = 1024 * bytesPerMiB

    public static func availableLabel(bytes: Int64?) -> String {
        guard let bytes, bytes >= 0 else { return "Espaço indisponível" }
        if bytes < bytesPerGiB {
            return "\(bytes / bytesPerMiB) MB disponíveis"
        }

        let whole = bytes / bytesPerGiB
        let tenths = (bytes % bytesPerGiB) * 10 / bytesPerGiB
        let amount = tenths == 0 ? "\(whole)" : "\(whole),\(tenths)"
        return "\(amount) GB disponíveis"
    }
}
