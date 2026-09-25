import Foundation

enum APIRetryPolicy {
    static let maxRetries = 2

    static func isRetryableMethod(_ method: String) -> Bool {
        ["GET", "HEAD", "OPTIONS"].contains(method.uppercased())
    }

    static func shouldRetryResponse(method: String, statusCode: Int, attempt: Int) -> Bool {
        isRetryableMethod(method)
            && attempt < maxRetries
            && (statusCode == 408 || statusCode == 425 || statusCode == 429 || (500...599).contains(statusCode))
    }

    static func shouldRetryFailure(method: String, attempt: Int) -> Bool {
        isRetryableMethod(method) && attempt < maxRetries
    }

    static func delayMilliseconds(attempt: Int, retryAfter: String?) -> UInt64 {
        if let seconds = retryAfter.flatMap({ Int($0.trimmingCharacters(in: .whitespacesAndNewlines)) }), seconds >= 0 {
            return min(UInt64(seconds) * 1_000, 1_500)
        }
        return attempt == 0 ? 250 : 750
    }
}

public enum APIError: LocalizedError, Equatable {
    case invalidServerURL
    case invalidResponse
    case httpStatus(Int)
    case serverMessage(String)

    public var errorDescription: String? {
        switch self {
        case .invalidServerURL: return "Informe uma URL HTTP ou HTTPS válida."
        case .invalidResponse: return "O servidor retornou uma resposta inválida."
        case .httpStatus(let code): return "O servidor respondeu com HTTP \(code)."
        case .serverMessage(let message): return message
        }
    }
}

public actor APIClient {
    private let session: URLSession
    private let decoder: JSONDecoder
    private let encoder: JSONEncoder
    private let deviceID: String
    public private(set) var serverURL: URL
    public private(set) var accessToken: String?

    public init(serverURL: URL, urlSession: URLSession = .shared, deviceID: String = "ios-unknown") {
        self.serverURL = serverURL
        self.session = urlSession
        self.deviceID = deviceID
        self.decoder = JSONDecoder()
        self.encoder = JSONEncoder()
    }

    public func update(serverURL: URL, accessToken: String? = nil) {
        self.serverURL = serverURL
        self.accessToken = accessToken
    }

    public func authenticate(username: String, password: String) async throws -> UserSession {
        struct Credentials: Encodable {
            let username: String
            let password: String?
            enum CodingKeys: String, CodingKey { case username = "Username"; case password = "Pw" }
        }
        let result: AuthenticationResult = try await request(path: "Users/AuthenticateByName", method: "POST", body: Credentials(username: username, password: password))
        guard let token = result.accessToken, let user = result.user else { throw APIError.serverMessage("O servidor não retornou uma sessão válida.") }
        accessToken = token
        return UserSession(serverURL: serverURL, accessToken: token, userID: user.id, userName: user.name)
    }

    public func register(username: String, password: String) async throws -> RegistrationResult {
        struct RegistrationBody: Encodable {
            let name: String
            let password: String
            enum CodingKeys: String, CodingKey {
                case name = "Name"
                case password = "Password"
            }
        }
        return try await request(
            path: "Users/Register",
            method: "POST",
            body: RegistrationBody(name: username, password: password)
        )
    }

    public func publicUsers() async throws -> [PublicUser] {
        try await request(path: "Users/Public")
    }

    public func isQuickConnectEnabled() async throws -> Bool {
        try await request(path: "QuickConnect/Enabled")
    }

    public func initiateQuickConnect() async throws -> QuickConnectResult {
        try await request(path: "QuickConnect/Initiate", method: "POST")
    }

    public func connectQuickConnect(secret: String) async throws -> QuickConnectResult {
        var allowed = CharacterSet.urlQueryAllowed
        allowed.remove(charactersIn: "&=")
        let encoded = secret.addingPercentEncoding(withAllowedCharacters: allowed) ?? secret
        return try await request(path: "QuickConnect/Connect?secret=\(encoded)")
    }

    public func authenticateQuickConnect(secret: String) async throws -> UserSession {
        struct QuickConnectCredentials: Encodable {
            let secret: String
            enum CodingKeys: String, CodingKey { case secret = "Secret" }
        }
        let result: AuthenticationResult = try await request(
            path: "Users/AuthenticateWithQuickConnect",
            method: "POST",
            body: QuickConnectCredentials(secret: secret)
        )
        guard let token = result.accessToken, let user = result.user else {
            throw APIError.serverMessage("O servidor não retornou uma sessão válida.")
        }
        accessToken = token
        return UserSession(serverURL: serverURL, accessToken: token, userID: user.id, userName: user.name)
    }

    public func latestItems(userID: String, limit: Int = 16) async throws -> [MediaItem] {
        try await request(path: "Users/\(userID)/Items/Latest?Limit=\(limit)&Fields=PrimaryImageAspectRatio,Overview,ItemCounts&EnableImageTypes=Primary,Backdrop&ImageTypeLimit=1")
    }

    public func resumeItems(userID: String, limit: Int = 12) async throws -> [MediaItem] {
        let result: ItemQueryResult = try await request(path: "Users/\(userID)/Items/Resume?Limit=\(limit)&Fields=Overview,MediaSources,ItemCounts&EnableImageTypes=Primary,Backdrop,Thumb&ImageTypeLimit=1")
        return result.items
    }

    public func nextUpItems(userID: String, limit: Int = 12) async throws -> [MediaItem] {
        let result: ItemQueryResult = try await request(path: "Shows/NextUp?UserId=\(userID)&Limit=\(limit)&Fields=Overview,MediaSources,ItemCounts&EnableImageTypes=Primary,Thumb&ImageTypeLimit=1")
        return result.items
    }

    public func libraries(userID: String) async throws -> [MediaItem] {
        let result: ItemQueryResult = try await request(path: "Users/\(userID)/Views")
        return result.items
    }

    public func userProfile(userID: String) async throws -> UserProfile {
        try await request(path: "Users/\(userID)?Fields=Policy,Configuration")
    }

    public func publicSystemInfo() async throws -> ServerInfo {
        try await request(path: "System/Info/Public")
    }

    public func health() async throws -> [String: String] {
        try await request(path: "Health")
    }

    public func brandingConfiguration() async throws -> BrandingOptions {
        try await request(path: "Branding/Configuration")
    }

    public func seasons(userID: String, seriesID: String) async throws -> [MediaItem] {
        let result: ItemQueryResult = try await request(path: "Shows/\(seriesID)/Seasons?UserId=\(userID)&Fields=Overview&EnableImageTypes=Primary,Thumb&ImageTypeLimit=1")
        return result.items
    }

    public func episodes(userID: String, seriesID: String, seasonID: String?) async throws -> [MediaItem] {
        var path = "Shows/\(seriesID)/Episodes?UserId=\(userID)&Fields=Overview,MediaSources&EnableImageTypes=Primary,Thumb&ImageTypeLimit=1"
        if let seasonID { path += "&SeasonId=\(seasonID)" }
        let result: ItemQueryResult = try await request(path: path)
        return result.items
    }

    public func playlists(userID: String) async throws -> [Playlist] {
        let result: ItemQueryResult = try await request(path: "Users/\(userID)/Items?IncludeItemTypes=Playlist&SortBy=SortName&SortOrder=Ascending&Recursive=true")
        return result.items.map { Playlist(id: $0.id, name: $0.name) }
    }

    public func createPlaylist(name: String, userID: String, itemID: String?) async throws -> Playlist {
        var allowed = CharacterSet.urlQueryAllowed
        allowed.remove(charactersIn: "&=")
        let encodedName = name.addingPercentEncoding(withAllowedCharacters: allowed) ?? name
        var path = "Playlists?Name=\(encodedName)&UserId=\(userID)"
        if let itemID { path += "&Ids=\(itemID)" }
        struct Response: Decodable { let id: String?; enum CodingKeys: String, CodingKey { case id = "Id" } }
        let result: Response = try await request(path: path, method: "POST")
        guard let id = result.id else { throw APIError.serverMessage("A playlist foi criada sem um identificador válido.") }
        return Playlist(id: id, name: name)
    }

    public func addToPlaylist(playlistID: String, itemID: String, userID: String) async throws {
        try await perform(path: "Playlists/\(playlistID)/Items?Ids=\(itemID)&UserId=\(userID)", method: "POST")
    }

    public func items(userID: String, parentID: String?, startIndex: Int = 0, limit: Int = 60, sortBy: String? = nil, sortOrder: String? = nil, filters: String? = nil, isFavorite: Bool? = nil, includeItemTypes: String? = nil, genres: String? = nil, years: String? = nil, officialRatings: String? = nil, isPlayed: Bool? = nil, fields: String = "ItemCounts") async throws -> ItemQueryResult {
        let path = Self.itemsPath(userID: userID, parentID: parentID, startIndex: startIndex, limit: limit, sortBy: sortBy, sortOrder: sortOrder, filters: filters, isFavorite: isFavorite, includeItemTypes: includeItemTypes, genres: genres, years: years, officialRatings: officialRatings, isPlayed: isPlayed, fields: fields)
        return try await request(path: path)
    }

    static func itemsPath(userID: String, parentID: String?, startIndex: Int = 0, limit: Int = 60, sortBy: String? = nil, sortOrder: String? = nil, filters: String? = nil, isFavorite: Bool? = nil, includeItemTypes: String? = nil, genres: String? = nil, years: String? = nil, officialRatings: String? = nil, isPlayed: Bool? = nil, fields: String = "ItemCounts") -> String {
        var path = "Users/\(userID)/Items?Limit=\(limit)&StartIndex=\(startIndex)&Recursive=true&Fields=\(fields)&EnableImageTypes=Primary,Backdrop,Thumb&ImageTypeLimit=1"
        if let parentID { path += "&ParentId=\(parentID)" }
        if let sortBy { path += "&SortBy=\(sortBy)" }
        if let sortOrder { path += "&SortOrder=\(sortOrder)" }
        if let filters { path += "&Filters=\(filters)" }
        if let isFavorite { path += "&IsFavorite=\(isFavorite ? "true" : "false")" }
        if let includeItemTypes { path += "&IncludeItemTypes=\(includeItemTypes)" }
        if let genres { path += "&Genres=\(queryValue(genres))" }
        if let years { path += "&Years=\(queryValue(years))" }
        if let officialRatings { path += "&OfficialRatings=\(queryValue(officialRatings))" }
        if let isPlayed { path += "&IsPlayed=\(isPlayed ? "true" : "false")" }
        return path
    }

    public func allItems(userID: String, parentID: String?, sortBy: String? = nil, sortOrder: String? = nil, filters: String? = nil, isFavorite: Bool? = nil, genres: String? = nil, years: String? = nil, officialRatings: String? = nil, isPlayed: Bool? = nil, pageSize: Int = 100, includeItemTypes: String? = nil, fields: String = "ItemCounts") async throws -> [MediaItem] {
        var startIndex = 0
        var resultItems: [MediaItem] = []
        var seenIDs = Set<String>()
        for _ in 0..<100 {
            let page = try await items(userID: userID, parentID: parentID, startIndex: startIndex, limit: pageSize, sortBy: sortBy, sortOrder: sortOrder, filters: filters, isFavorite: isFavorite, includeItemTypes: includeItemTypes, genres: genres, years: years, officialRatings: officialRatings, isPlayed: isPlayed, fields: fields)
            let newItems = page.items.filter { seenIDs.insert($0.id).inserted }
            resultItems.append(contentsOf: newItems)
            if page.items.isEmpty || newItems.isEmpty || (page.hasExplicitTotalRecordCount && resultItems.count >= page.totalRecordCount) || page.items.count < pageSize { break }
            startIndex += page.items.count
        }
        return resultItems
    }

    public func allFavoriteItems(userID: String, pageSize: Int = 100) async throws -> [MediaItem] {
        try await allItems(userID: userID, parentID: nil, sortBy: "SortName", sortOrder: "Ascending", filters: "IsFavorite", isFavorite: true, pageSize: pageSize)
    }

    public func audioTracks(userID: String, albumID: String, pageSize: Int = 100) async throws -> [MediaItem] {
        try await allItems(
            userID: userID,
            parentID: albumID,
            sortBy: "ParentIndexNumber,IndexNumber,SortName",
            pageSize: pageSize,
            includeItemTypes: "Audio",
            fields: "Overview,MediaSources,ItemCounts"
        )
    }

    public func lyrics(itemID: String) async throws -> [LyricLine] {
        let result: LyricsResult = try await request(path: "Audio/\(itemID)/Lyrics")
        return result.lyrics
    }

    public func searchItems(userID: String, term: String, includeItemTypes: String? = nil, startIndex: Int = 0, limit: Int = 30) async throws -> ItemQueryResult {
        var allowed = CharacterSet.urlQueryAllowed
        allowed.remove(charactersIn: "&=")
        let encodedTerm = term.addingPercentEncoding(withAllowedCharacters: allowed) ?? term
        var path = "Users/\(userID)/Items?SearchTerm=\(encodedTerm)&Limit=\(limit)&StartIndex=\(startIndex)&Recursive=true&Fields=ItemCounts&EnableImageTypes=Primary,Backdrop,Thumb&ImageTypeLimit=1"
        if let includeItemTypes { path += "&IncludeItemTypes=\(includeItemTypes)" }
        return try await request(path: path)
    }

    public func searchHints(userID: String, term: String, limit: Int = 8) async throws -> [SearchHint] {
        var allowed = CharacterSet.urlQueryAllowed
        allowed.remove(charactersIn: "&=")
        let encodedTerm = term.addingPercentEncoding(withAllowedCharacters: allowed) ?? term
        let result: SearchHintResult = try await request(path: "Search/Hints?SearchTerm=\(encodedTerm)&UserId=\(userID)&Limit=\(limit)")
        return result.hints
    }

    public func allSearchItems(userID: String, term: String, includeItemTypes: String? = nil, pageSize: Int = 60) async throws -> SearchItemsResult {
        var startIndex = 0
        var resultItems: [MediaItem] = []
        var seenIDs = Set<String>()
        var totalRecordCount: Int?
        for _ in 0..<100 {
            let page = try await searchItems(userID: userID, term: term, includeItemTypes: includeItemTypes, startIndex: startIndex, limit: pageSize)
            if totalRecordCount == nil, page.hasExplicitTotalRecordCount {
                totalRecordCount = page.totalRecordCount
            }
            let newItems = page.items.filter { seenIDs.insert($0.id).inserted }
            resultItems.append(contentsOf: newItems)
            if page.items.isEmpty || newItems.isEmpty || (page.hasExplicitTotalRecordCount && resultItems.count >= page.totalRecordCount) || page.items.count < pageSize { break }
            startIndex += page.items.count
        }
        return SearchItemsResult(items: resultItems, totalRecordCount: totalRecordCount)
    }

    public func liveTVChannels(userID: String, limit: Int = 100, startIndex: Int = 0) async throws -> [MediaItem] {
        let result: ItemQueryResult = try await request(path: "LiveTv/Channels?UserId=\(userID)&Limit=\(limit)&StartIndex=\(startIndex)&Fields=Overview&EnableImageTypes=Primary&ImageTypeLimit=1")
        return result.items
    }

    public func allLiveTVChannels(userID: String, pageSize: Int = 100) async throws -> [MediaItem] {
        var startIndex = 0
        var resultItems: [MediaItem] = []
        var seenIDs = Set<String>()
        for _ in 0..<100 {
            let page = try await liveTVChannels(userID: userID, limit: pageSize, startIndex: startIndex)
            let newItems = page.filter { seenIDs.insert($0.id).inserted }
            resultItems.append(contentsOf: newItems)
            if page.isEmpty || newItems.isEmpty || page.count < pageSize { break }
            startIndex += page.count
        }
        return resultItems
    }

    public func liveTVPrograms(channelIDs: [String], startDate: String, endDate: String, limit: Int = 100, startIndex: Int = 0) async throws -> [MediaItem] {
        guard !channelIDs.isEmpty else { return [] }
        let result: ItemQueryResult = try await request(path: Self.liveTVProgramsPath(channelIDs: channelIDs, startDate: startDate, endDate: endDate, limit: limit, startIndex: startIndex))
        return result.items
    }

    static func liveTVProgramsPath(channelIDs: [String], startDate: String, endDate: String, limit: Int = 100, startIndex: Int = 0) -> String {
        let ids = queryValue(channelIDs.joined(separator: ","))
        let start = queryValue(startDate)
        let end = queryValue(endDate)
        return "LiveTv/Programs?ChannelIds=\(ids)&StartIndex=\(startIndex)&Limit=\(limit)&MinEndDate=\(start)&MaxStartDate=\(end)"
    }

    public func allLiveTVPrograms(channelIDs: [String], startDate: String, endDate: String, pageSize: Int = 100) async throws -> [MediaItem] {
        var startIndex = 0
        var resultItems: [MediaItem] = []
        var seenIDs = Set<String>()
        for _ in 0..<100 {
            let page = try await liveTVPrograms(channelIDs: channelIDs, startDate: startDate, endDate: endDate, limit: pageSize, startIndex: startIndex)
            let newItems = page.filter { seenIDs.insert($0.id).inserted }
            resultItems.append(contentsOf: newItems)
            if page.isEmpty || newItems.isEmpty || page.count < pageSize { break }
            startIndex += page.count
        }
        return resultItems
    }

    public func liveTVRecordings(userID: String, limit: Int = 100, startIndex: Int = 0) async throws -> [MediaItem] {
        let result: ItemQueryResult = try await request(path: "LiveTv/Recordings?UserId=\(userID)&StartIndex=\(startIndex)&Limit=\(limit)")
        return result.items
    }

    public func allLiveTVRecordings(userID: String, pageSize: Int = 100) async throws -> [MediaItem] {
        var startIndex = 0
        var resultItems: [MediaItem] = []
        var seenIDs = Set<String>()
        for _ in 0..<100 {
            let page = try await liveTVRecordings(userID: userID, limit: pageSize, startIndex: startIndex)
            let newItems = page.filter { seenIDs.insert($0.id).inserted }
            resultItems.append(contentsOf: newItems)
            if page.isEmpty || newItems.isEmpty || page.count < pageSize { break }
            startIndex += page.count
        }
        return resultItems
    }

    public func scheduledLiveTVProgramIDs() async throws -> Set<String> {
        let result: LiveTVTimerQuery = try await request(path: "LiveTv/Timers?IsScheduled=true")
        return Set(result.items.compactMap(\.programId))
    }

    public func scheduleLiveTV(program: MediaItem) async throws {
        guard let channelID = program.channelId, let startDate = program.startDate, let endDate = program.endDate else {
            throw APIError.serverMessage("O programa não possui informações suficientes para gravação.")
        }
        let defaults: LiveTVTimerDefaults = try await request(path: "LiveTv/Timers/Defaults?programId=\(program.id)")
        guard let serviceName = defaults.serviceName, !serviceName.isEmpty else {
            throw APIError.serverMessage("O servidor não informou o serviço de TV para este programa.")
        }
        struct TimerBody: Encodable {
            let type = "Timer"
            let programId: String
            let channelId: String
            let name: String
            let overview: String?
            let startDate: String
            let endDate: String
            let serviceName: String
            let prePaddingSeconds: Int
            let postPaddingSeconds: Int
            enum CodingKeys: String, CodingKey {
                case type = "Type", programId = "ProgramId", channelId = "ChannelId", name = "Name", overview = "Overview"
                case startDate = "StartDate", endDate = "EndDate", serviceName = "ServiceName"
                case prePaddingSeconds = "PrePaddingSeconds", postPaddingSeconds = "PostPaddingSeconds"
            }
        }
        let body = TimerBody(
            programId: program.id,
            channelId: defaults.channelId ?? channelID,
            name: defaults.name ?? program.name,
            overview: defaults.overview ?? program.overview,
            startDate: defaults.startDate ?? startDate,
            endDate: defaults.endDate ?? endDate,
            serviceName: serviceName,
            prePaddingSeconds: defaults.prePaddingSeconds ?? 0,
            postPaddingSeconds: defaults.postPaddingSeconds ?? 0
        )
        try await perform(path: "LiveTv/Timers", method: "POST", body: body)
    }

    public func syncPlayGroups() async throws -> [SyncPlayGroup] {
        try await request(path: "SyncPlay/List")
    }

    public func createSyncPlayGroup(name: String) async throws {
        struct Body: Encodable {
            let groupName: String
            enum CodingKeys: String, CodingKey { case groupName = "GroupName" }
        }
        try await perform(path: "SyncPlay/New", method: "POST", body: Body(groupName: name))
    }

    public func joinSyncPlayGroup(id: String) async throws {
        struct Body: Encodable {
            let groupID: String
            enum CodingKeys: String, CodingKey { case groupID = "GroupId" }
        }
        try await perform(path: "SyncPlay/Join", method: "POST", body: Body(groupID: id))
    }

    public func leaveSyncPlayGroup() async throws {
        try await perform(path: "SyncPlay/Leave", method: "POST")
    }

    public func sendSyncPlayCommand(_ command: SyncPlayPlaybackCommand) async throws {
        try await perform(path: command.route, method: "POST")
    }

    public func reportPlaybackStart(itemID: String, mediaSourceID: String?, positionTicks: Int64 = 0) async throws {
        try await perform(path: "Sessions/Playing", method: "POST", body: PlaybackStartBody(itemID: itemID, mediaSourceID: mediaSourceID, positionTicks: positionTicks))
    }

    public func reportPlaybackProgress(itemID: String, mediaSourceID: String?, positionTicks: Int64, isPaused: Bool) async throws {
        try await perform(path: "Sessions/Playing/Progress", method: "POST", body: PlaybackProgressBody(itemID: itemID, mediaSourceID: mediaSourceID, positionTicks: positionTicks, isPaused: isPaused))
    }

    public func reportPlaybackStopped(itemID: String, mediaSourceID: String?, positionTicks: Int64) async throws {
        try await perform(path: "Sessions/Playing/Stopped", method: "POST", body: PlaybackStoppedBody(itemID: itemID, mediaSourceID: mediaSourceID, positionTicks: positionTicks))
    }

    public func item(userID: String, itemID: String) async throws -> MediaItem {
        try await request(path: "Users/\(userID)/Items/\(itemID)?Fields=Overview,MediaSources,ItemCounts,Genres,People,Chapters&EnableImageTypes=Primary,Backdrop,Thumb&ImageTypeLimit=1")
    }

    public func similarItems(userID: String, itemID: String, limit: Int = 12) async throws -> [MediaItem] {
        let result: ItemQueryResult = try await request(path: "Items/\(itemID)/Similar?UserId=\(userID)&Limit=\(limit)&Fields=Overview,PrimaryImageAspectRatio&EnableImageTypes=Primary,Backdrop,Thumb&ImageTypeLimit=1")
        return result.items
    }

    public func specialFeatures(userID: String, itemID: String) async throws -> [MediaItem] {
        try await request(path: "Items/\(itemID)/SpecialFeatures?UserId=\(userID)&Fields=Overview,MediaSources&EnableImageTypes=Primary,Backdrop,Thumb&ImageTypeLimit=1")
    }

    public func mediaSegments(itemID: String) async throws -> [MediaSegment] {
        let result: MediaSegmentQueryResult = try await request(path: "MediaSegments/\(itemID)")
        return result.items
    }

    public func playbackURL(for item: MediaItem) -> URL? {
        guard let sourceID = item.mediaSources.first?.id else { return nil }
        var components = URLComponents(url: serverURL.appendingPathComponent("Videos/\(item.id)/stream"), resolvingAgainstBaseURL: false)
        components?.queryItems = [
            URLQueryItem(name: "MediaSourceId", value: sourceID),
            URLQueryItem(name: "Static", value: "true"),
            URLQueryItem(name: "api_key", value: accessToken),
        ]
        return components?.url
    }

    public func preparedPlaybackURL(userID: String, item: MediaItem, maxStreamingBitrate: Int64? = nil, startTimeTicks: Int64? = nil) async throws -> URL? {
        struct PlaybackInfoBody: Encodable {
            let userID: String
            let mediaSourceID: String?
            let audioStreamIndex: Int?
            let subtitleStreamIndex: Int?
            let maxStreamingBitrate: Int64?
            let startTimeTicks: Int64?
            let enableDirectPlay = true
            let enableDirectStream = true
            let enableTranscoding = true
            enum CodingKeys: String, CodingKey {
                case userID = "UserId", mediaSourceID = "MediaSourceId"
                case audioStreamIndex = "AudioStreamIndex", subtitleStreamIndex = "SubtitleStreamIndex"
                case maxStreamingBitrate = "MaxStreamingBitrate"
                case startTimeTicks = "StartTimeTicks"
                case enableDirectPlay = "EnableDirectPlay", enableDirectStream = "EnableDirectStream", enableTranscoding = "EnableTranscoding"
            }
        }
        let requestedSource = item.mediaSources.first
        let info: PlaybackInfo = try await request(
            path: "Items/\(item.id)/PlaybackInfo?UserId=\(userID)",
            method: "POST",
            body: PlaybackInfoBody(
                userID: userID,
                mediaSourceID: requestedSource?.id,
                audioStreamIndex: requestedSource?.defaultAudioStreamIndex,
                subtitleStreamIndex: requestedSource?.defaultSubtitleStreamIndex,
                maxStreamingBitrate: maxStreamingBitrate,
                startTimeTicks: startTimeTicks
            )
        )
        guard var source = info.mediaSources.first, let sourceID = source.id else {
            throw APIError.serverMessage(info.errorCode ?? "O servidor não encontrou uma fonte de reprodução.")
        }
        if source.requiresOpening {
            struct OpenBody: Encodable {
                let userID: String
                let itemID: String
                let openToken: String?
                let enableDirectPlay = true
                let enableDirectStream = true
                enum CodingKeys: String, CodingKey {
                    case userID = "UserId", itemID = "ItemId", openToken = "OpenToken"
                    case enableDirectPlay = "EnableDirectPlay", enableDirectStream = "EnableDirectStream"
                }
            }
            struct OpenResponse: Decodable {
                let mediaSource: MediaSource?
                enum CodingKeys: String, CodingKey { case mediaSource = "MediaSource" }
                init(from decoder: Decoder) throws {
                    let values = try decoder.container(keyedBy: CodingKeys.self)
                    mediaSource = try values.decodeIfPresent(MediaSource.self, forKey: .mediaSource)
                }
            }
            let opened: OpenResponse = try await request(path: "LiveStreams/Open?UserId=\(userID)&ItemId=\(item.id)&PlaySessionId=\(info.playSessionId ?? "")", method: "POST", body: OpenBody(userID: userID, itemID: item.id, openToken: source.openToken))
            if let openedSource = opened.mediaSource { source = openedSource }
        }
        var components = URLComponents(url: serverURL.appendingPathComponent("Videos/\(item.id)/stream"), resolvingAgainstBaseURL: false)
        components?.queryItems = [
            URLQueryItem(name: "MediaSourceId", value: source.id ?? sourceID),
            URLQueryItem(name: "PlaySessionId", value: info.playSessionId),
            URLQueryItem(name: "LiveStreamId", value: source.liveStreamId),
            URLQueryItem(name: "Static", value: "true"),
            URLQueryItem(name: "api_key", value: accessToken),
        ]
        return components?.url
    }

    public func setFavorite(userID: String, itemID: String, isFavorite: Bool) async throws {
        let method = isFavorite ? "POST" : "DELETE"
        try await perform(path: "Users/\(userID)/FavoriteItems/\(itemID)", method: method)
    }

    public func setPlayed(userID: String, itemID: String, isPlayed: Bool) async throws {
        let method = isPlayed ? "POST" : "DELETE"
        try await perform(path: "Users/\(userID)/PlayedItems/\(itemID)", method: method)
    }

    public func imageURL(for item: MediaItem, type: String = "Primary") -> URL? {
        guard let tag = item.imageTags?[type] ?? item.primaryImageTag,
              var components = URLComponents(url: serverURL.appendingPathComponent("Items/\(item.id)/Images/\(type)"), resolvingAgainstBaseURL: false) else { return nil }
        components.queryItems = [URLQueryItem(name: "tag", value: tag)]
        return components.url
    }

    public func userImageURL(userID: String, tag: String?) -> URL? {
        guard let tag, !tag.isEmpty,
              var components = URLComponents(url: serverURL.appendingPathComponent("Users/\(userID)/Images/Primary"), resolvingAgainstBaseURL: false) else { return nil }
        components.queryItems = [URLQueryItem(name: "tag", value: tag)]
        return components.url
    }

    public static func authorizationHeader(accessToken: String?, deviceID: String = "ios-unknown", version: String = "0.1.0") -> String {
        let identity = "Client=\"MulletaFlix iOS\", Device=\"iPhone\", DeviceId=\"\(deviceID)\", Version=\"\(version)\""
        guard let accessToken, !accessToken.isEmpty else { return "MediaBrowser \(identity)" }
        return "MediaBrowser Token=\"\(accessToken)\", \(identity)"
    }

    public func url(forPath path: String) -> URL { makeURL(path: path) }

    private func request<T: Decodable>(path: String, method: String = "GET", body: (any Encodable)? = nil) async throws -> T {
        var request = URLRequest(url: makeURL(path: path))
        request.httpMethod = method
        request.timeoutInterval = 30
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue("MulletaFlix-iOS/0.1.0", forHTTPHeaderField: "User-Agent")
        request.setValue(Self.authorizationHeader(accessToken: accessToken, deviceID: deviceID), forHTTPHeaderField: "Authorization")
        if let accessToken { request.setValue(accessToken, forHTTPHeaderField: "X-Emby-Token") }
        if let body { request.httpBody = try encoder.encode(AnyEncodable(body)); request.setValue("application/json", forHTTPHeaderField: "Content-Type") }
        let (data, http) = try await send(request)
        guard (200..<300).contains(http.statusCode) else { throw APIError.httpStatus(http.statusCode) }
        return try decoder.decode(T.self, from: data)
    }

    private func perform(path: String, method: String, body: (any Encodable)? = nil) async throws {
        var request = URLRequest(url: makeURL(path: path))
        request.httpMethod = method
        request.timeoutInterval = 30
        request.setValue("MulletaFlix-iOS/0.1.0", forHTTPHeaderField: "User-Agent")
        request.setValue(Self.authorizationHeader(accessToken: accessToken, deviceID: deviceID), forHTTPHeaderField: "Authorization")
        if let accessToken { request.setValue(accessToken, forHTTPHeaderField: "X-Emby-Token") }
        if let body { request.httpBody = try encoder.encode(AnyEncodable(body)); request.setValue("application/json", forHTTPHeaderField: "Content-Type") }
        let (_, http) = try await send(request)
        guard (200..<300).contains(http.statusCode) else { throw APIError.httpStatus(http.statusCode) }
    }

    private func send(_ request: URLRequest) async throws -> (Data, HTTPURLResponse) {
        var attempt = 0
        while true {
            do {
                let (data, response) = try await session.data(for: request)
                guard let http = response as? HTTPURLResponse else { throw APIError.invalidResponse }
                if APIRetryPolicy.shouldRetryResponse(method: request.httpMethod ?? "GET", statusCode: http.statusCode, attempt: attempt) {
                    try await waitBeforeRetry(attempt: attempt, retryAfter: http.value(forHTTPHeaderField: "Retry-After"))
                    attempt += 1
                    continue
                }
                return (data, http)
            } catch let error as APIError {
                throw error
            } catch {
                guard APIRetryPolicy.shouldRetryFailure(method: request.httpMethod ?? "GET", attempt: attempt) else {
                    throw error
                }
                try await waitBeforeRetry(attempt: attempt, retryAfter: nil)
                attempt += 1
            }
        }
    }

    private func waitBeforeRetry(attempt: Int, retryAfter: String?) async throws {
        let milliseconds = APIRetryPolicy.delayMilliseconds(attempt: attempt, retryAfter: retryAfter)
        try await Task.sleep(nanoseconds: milliseconds * 1_000_000)
    }

    private func makeURL(path: String) -> URL {
        let parts = path.split(separator: "?", maxSplits: 1, omittingEmptySubsequences: false)
        let base = serverURL.appendingPathComponent(String(parts[0]))
        guard parts.count == 2 else { return base }
        return URL(string: base.absoluteString + "?" + parts[1]) ?? base
    }

    private static func queryValue(_ value: String) -> String {
        var allowed = CharacterSet.urlQueryAllowed
        allowed.remove(charactersIn: "&=+/?#")
        return value.addingPercentEncoding(withAllowedCharacters: allowed) ?? value
    }
}

private struct LyricsResult: Decodable {
    let lyrics: [LyricLine]

    private enum CodingKeys: String, CodingKey {
        case lyrics = "Lyrics"
    }

    init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        lyrics = try values.decodeIfPresent([LyricLine].self, forKey: .lyrics) ?? []
    }
}

private struct AnyEncodable: Encodable {
    let encodeValue: (Encoder) throws -> Void
    init(_ value: any Encodable) { encodeValue = value.encode }
    func encode(to encoder: Encoder) throws { try encodeValue(encoder) }
}

private struct PlaybackStartBody: Encodable {
    let itemID: String
    let mediaSourceID: String?
    let positionTicks: Int64
    let canSeek = true
    let isPaused = false
    let isMuted = false
    let volumeLevel = 100
    enum CodingKeys: String, CodingKey {
        case itemID = "ItemId", mediaSourceID = "MediaSourceId", positionTicks = "PositionTicks"
        case canSeek = "CanSeek", isPaused = "IsPaused", isMuted = "IsMuted", volumeLevel = "VolumeLevel"
    }
}

private struct PlaybackProgressBody: Encodable {
    let itemID: String
    let mediaSourceID: String?
    let positionTicks: Int64
    let isPaused: Bool
    let canSeek = true
    let isMuted = false
    let volumeLevel = 100
    let eventName = "TimeUpdate"
    enum CodingKeys: String, CodingKey {
        case itemID = "ItemId", mediaSourceID = "MediaSourceId", positionTicks = "PositionTicks", isPaused = "IsPaused"
        case canSeek = "CanSeek", isMuted = "IsMuted", volumeLevel = "VolumeLevel", eventName = "EventName"
    }
}

private struct PlaybackStoppedBody: Encodable {
    let itemID: String
    let mediaSourceID: String?
    let positionTicks: Int64
    let failed = false
    enum CodingKeys: String, CodingKey {
        case itemID = "ItemId", mediaSourceID = "MediaSourceId", positionTicks = "PositionTicks", failed = "Failed"
    }
}
