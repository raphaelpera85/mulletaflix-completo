import Foundation

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
    case all, movies, series, episodes, music, people

    public var id: String { rawValue }

    public var title: String {
        switch self {
        case .all: return "Tudo"
        case .movies: return "Filmes"
        case .series: return "Séries"
        case .episodes: return "Episódios"
        case .music: return "Músicas"
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
        case .people: return "Person"
        }
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

public enum OfflineDownloadPolicy {
    public static func canStart(
        queuePaused: Bool,
        wifiOnly: Bool,
        wifiAvailable: Bool?
    ) -> Bool {
        !queuePaused && (!wifiOnly || wifiAvailable == true)
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

    public var id: String { url }

    public init(url: String, name: String, version: String? = nil) {
        self.url = url
        self.name = name
        self.version = version
    }
}

public struct SearchHint: Decodable, Identifiable, Hashable, Sendable {
    public let itemID: String
    public let name: String
    public let type: String?
    public let productionYear: Int?
    public let series: String?

    public var id: String { itemID }

    private enum CodingKeys: String, CodingKey {
        case itemID = "ItemId"
        case name = "Name"
        case type = "Type"
        case productionYear = "ProductionYear"
        case series = "Series"
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
    public let seasonId: String?
    public let indexNumber: Int?
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
        case seasonId = "SeasonId"
        case indexNumber = "IndexNumber"
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
        seasonId: String? = nil,
        indexNumber: Int? = nil,
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
        self.seasonId = seasonId
        self.indexNumber = indexNumber
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
                  seriesId: seriesId, seasonId: seasonId, indexNumber: indexNumber,
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
                  seriesId: seriesId, seasonId: seasonId, indexNumber: indexNumber,
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
        seasonId = try values.decodeIfPresent(String.self, forKey: .seasonId)
        indexNumber = try values.decodeIfPresent(Int.self, forKey: .indexNumber)
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

public struct MediaStream: Decodable, Hashable, Sendable {
    public let type: String?
    public let codec: String?
    public let width: Int?
    public let height: Int?
    public let bitRate: Int?

    private enum CodingKeys: String, CodingKey {
        case type = "Type"
        case codec = "Codec"
        case width = "Width"
        case height = "Height"
        case bitRate = "BitRate"
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        type = try values.decodeIfPresent(String.self, forKey: .type)
        codec = try values.decodeIfPresent(String.self, forKey: .codec)
        width = try values.decodeIfPresent(Int.self, forKey: .width)
        height = try values.decodeIfPresent(Int.self, forKey: .height)
        bitRate = try values.decodeIfPresent(Int.self, forKey: .bitRate)
    }
}

public enum PlaybackQualityPolicy {
    public static func maxStreamingBitrate(for quality: String) -> Int64? {
        switch quality {
        case "4K": return 20_000_000
        case "1440p": return 12_000_000
        case "1080p": return 8_000_000
        case "720p": return 4_000_000
        case "480p": return 2_000_000
        default:
            guard quality.hasSuffix("p"),
                  let height = Int(quality.dropLast()), height > 0 else { return nil }
            return max(1_000_000, Int64(height) * Int64(height) * 4)
        }
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
    public let programId: String?

    private enum CodingKeys: String, CodingKey { case programId = "ProgramId" }
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
            ?? values.decodeIfPresent(String.self, forKey: .serverId)
    }
}
