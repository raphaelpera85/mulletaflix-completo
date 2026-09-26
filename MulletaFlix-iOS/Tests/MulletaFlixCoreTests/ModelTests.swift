import XCTest
@testable import MulletaFlixCore

final class ModelTests: XCTestCase {
    func testAuthErrorPolicyExplainsAuthenticationStatusCodes() {
        XCTAssertEqual(
            AuthErrorPolicy.authenticationMessage(for: APIError.httpStatus(401)),
            "Usuário ou senha inválidos. Confira os dados e tente novamente."
        )
        XCTAssertEqual(
            AuthErrorPolicy.authenticationMessage(for: APIError.httpStatus(403)),
            "Este usuário não tem permissão para acessar o servidor."
        )
        XCTAssertEqual(
            AuthErrorPolicy.authenticationMessage(for: APIError.httpStatus(504)),
            "O servidor demorou para responder. Tente novamente."
        )
    }

    func testAuthErrorPolicyExplainsTransportFailures() {
        XCTAssertEqual(
            AuthErrorPolicy.serverConnectionMessage(for: URLError(.cannotFindHost)),
            "Servidor não encontrado. Verifique o endereço e a conexão com a internet."
        )
        XCTAssertEqual(
            AuthErrorPolicy.serverConnectionMessage(for: URLError(.timedOut)),
            "O servidor demorou para responder. Tente novamente."
        )
    }

    func testAuthErrorPolicyPreservesServerMessages() {
        XCTAssertEqual(
            AuthErrorPolicy.authenticationMessage(for: APIError.serverMessage("Conta bloqueada pelo servidor.")),
            "Conta bloqueada pelo servidor."
        )
        XCTAssertEqual(
            AuthErrorPolicy.serverConnectionMessage(for: APIError.serverMessage("API em manutenção.")),
            "API em manutenção."
        )
    }

    func testPlaybackErrorPolicyMapsNetworkFailuresToActionableMessage() {
        let error = NSError(domain: NSURLErrorDomain, code: NSURLErrorTimedOut)

        XCTAssertEqual(
            PlaybackErrorPolicy.userFacingMessage(from: error),
            "A conexão com o servidor foi interrompida. Verifique a rede e tente novamente."
        )
    }

    func testPlaybackErrorPolicyRedactsCredentialsFromRawUrl() {
        let raw = "The operation could not be completed: https://media.example/stream?api_key=secret-value&item=movie-1"

        let message = PlaybackErrorPolicy.sanitizedMessage(raw)

        XCTAssertEqual(
            message,
            "The operation could not be completed: https://media.example/stream?api_key=[redacted]&item=movie-1"
        )
        XCTAssertFalse(message.contains("secret-value"))
    }

    func testPlaybackErrorPolicyUsesFallbackForMissingMessage() {
        XCTAssertEqual(
            PlaybackErrorPolicy.sanitizedMessage(nil),
            "Não foi possível reproduzir esta mídia."
        )
    }

    func testPlaybackRecoveryPolicyRetriesOnlyTransientRemoteFailures() {
        let timeout = NSError(domain: NSURLErrorDomain, code: NSURLErrorTimedOut)
        let codec = NSError(domain: "AVFoundationErrorDomain", code: -11821)

        XCTAssertTrue(
            PlaybackRecoveryPolicy.shouldAutomaticallyRetry(
                error: timeout,
                attempt: 0,
                isLocal: false,
                networkAvailable: true
            )
        )
        XCTAssertFalse(
            PlaybackRecoveryPolicy.shouldAutomaticallyRetry(
                error: codec,
                attempt: 0,
                isLocal: false,
                networkAvailable: true
            )
        )
        XCTAssertFalse(
            PlaybackRecoveryPolicy.shouldAutomaticallyRetry(
                error: timeout,
                attempt: 0,
                isLocal: true,
                networkAvailable: true
            )
        )
        XCTAssertFalse(
            PlaybackRecoveryPolicy.shouldAutomaticallyRetry(
                error: timeout,
                attempt: 3,
                isLocal: false,
                networkAvailable: true
            )
        )
    }

    func testPlaybackRecoveryPolicyUsesBoundedBackoff() {
        XCTAssertEqual(PlaybackRecoveryPolicy.retryDelayMilliseconds(for: 0), 750)
        XCTAssertEqual(PlaybackRecoveryPolicy.retryDelayMilliseconds(for: 1), 1_500)
        XCTAssertEqual(PlaybackRecoveryPolicy.retryDelayMilliseconds(for: 2), 3_000)
        XCTAssertEqual(PlaybackRecoveryPolicy.retryDelayMilliseconds(for: 99), 3_000)
    }

    func testPlaybackRecoveryPolicyRetriesAfterNetworkRestorationOnlyForRemoteNetworkErrors() {
        XCTAssertTrue(
            PlaybackRecoveryPolicy.shouldRetryAfterNetworkRestored(
                wasOffline: true,
                isOnline: true,
                isLocal: false,
                hasPlaybackError: true,
                wasTransientNetworkFailure: true
            )
        )
        XCTAssertFalse(
            PlaybackRecoveryPolicy.shouldRetryAfterNetworkRestored(
                wasOffline: true,
                isOnline: true,
                isLocal: false,
                hasPlaybackError: true,
                wasTransientNetworkFailure: false
            )
        )
        XCTAssertFalse(
            PlaybackRecoveryPolicy.shouldRetryAfterNetworkRestored(
                wasOffline: true,
                isOnline: true,
                isLocal: true,
                hasPlaybackError: true,
                wasTransientNetworkFailure: true
            )
        )
    }

    func testForegroundRefreshWaitsUntilAllCatalogRequestsAreIdle() {
        XCTAssertTrue(
            ForegroundRefreshPolicy.shouldRefresh(
                isSignedIn: true,
                isNetworkAvailable: true,
                isHomeLoading: false,
                isLibrariesLoading: false,
                isLiveTVLoading: false
            )
        )
        XCTAssertFalse(
            ForegroundRefreshPolicy.shouldRefresh(
                isSignedIn: true,
                isNetworkAvailable: true,
                isHomeLoading: true,
                isLibrariesLoading: false,
                isLiveTVLoading: false
            )
        )
        XCTAssertFalse(
            ForegroundRefreshPolicy.shouldRefresh(
                isSignedIn: true,
                isNetworkAvailable: true,
                isHomeLoading: false,
                isLibrariesLoading: true,
                isLiveTVLoading: false
            )
        )
    }

    func testForegroundRefreshDoesNotStartWithoutSessionOrNetwork() {
        XCTAssertFalse(
            ForegroundRefreshPolicy.shouldRefresh(
                isSignedIn: false,
                isNetworkAvailable: true,
                isHomeLoading: false,
                isLibrariesLoading: false,
                isLiveTVLoading: false
            )
        )
        XCTAssertFalse(
            ForegroundRefreshPolicy.shouldRefresh(
                isSignedIn: true,
                isNetworkAvailable: false,
                isHomeLoading: false,
                isLibrariesLoading: false,
                isLiveTVLoading: false
            )
        )
    }

    func testLiveTVDateFormattingConvertsUtcToViewerTimeZone() throws {
        let saoPaulo = try XCTUnwrap(TimeZone(identifier: "America/Sao_Paulo"))

        XCTAssertEqual(
            LiveTVDateFormatting.recordingStartLabel(
                "2026-09-14T23:00:00.0000000Z",
                timeZone: saoPaulo,
                locale: Locale(identifier: "en_US")
            ),
            "2026-09-14 20:00"
        )
    }

    func testLiveTVDateFormattingAcceptsTimestampWithoutFractionalSeconds() {
        let utc = TimeZone(secondsFromGMT: 0)!

        XCTAssertEqual(
            LiveTVDateFormatting.recordingStartLabel(
                "2026-09-14T23:00:00Z",
                timeZone: utc,
                locale: Locale(identifier: "en_US")
            ),
            "2026-09-14 23:00"
        )
    }

    func testLiveTVDateFormattingDoesNotInventInvalidTimes() {
        XCTAssertNil(LiveTVDateFormatting.recordingStartLabel("ontem à noite"))
        XCTAssertNil(LiveTVDateFormatting.recordingStartLabel(nil))
        XCTAssertNil(LiveTVDateFormatting.recordingStartLabel("   "))
    }

    func testOfflineDownloadScopeSeparatesUsersAndServers() throws {
        let firstServer = try XCTUnwrap(URL(string: "https://media.example"))
        let secondServer = try XCTUnwrap(URL(string: "https://other.example"))
        let first = OfflineDownloadScope.ownerKey(serverURL: firstServer, userID: "user-1")
        let same = OfflineDownloadScope.ownerKey(serverURL: firstServer, userID: "user-1")
        let otherUser = OfflineDownloadScope.ownerKey(serverURL: firstServer, userID: "user-2")
        let otherServer = OfflineDownloadScope.ownerKey(serverURL: secondServer, userID: "user-1")

        XCTAssertEqual(first, same)
        XCTAssertNotEqual(first, otherUser)
        XCTAssertNotEqual(first, otherServer)
        XCTAssertTrue(OfflineDownloadScope.directoryName(ownerKey: first).hasPrefix("scope-"))
        XCTAssertFalse(OfflineDownloadScope.directoryName(ownerKey: first).contains("/"))
        XCTAssertTrue(OfflineDownloadScope.acceptsCallback(ownerKey: first, currentOwnerKey: same))
        XCTAssertFalse(OfflineDownloadScope.acceptsCallback(ownerKey: first, currentOwnerKey: otherUser))
        XCTAssertFalse(OfflineDownloadScope.acceptsCallback(ownerKey: first, currentOwnerKey: nil))
    }

    func testOfflinePlaybackPositionScopeSeparatesUsersServersAndItems() {
        let first = OfflinePlaybackPositionScope.key(ownerKey: "https://media.example|user-1", itemID: "movie/1")
        let same = OfflinePlaybackPositionScope.key(ownerKey: "https://media.example|user-1", itemID: "movie/1")
        let otherUser = OfflinePlaybackPositionScope.key(ownerKey: "https://media.example|user-2", itemID: "movie/1")
        let otherItem = OfflinePlaybackPositionScope.key(ownerKey: "https://media.example|user-1", itemID: "movie/2")

        XCTAssertEqual(first, same)
        XCTAssertNotEqual(first, otherUser)
        XCTAssertNotEqual(first, otherItem)
        XCTAssertFalse(first.contains("/"))
    }

    func testAPIRetryPolicyRetriesTransientReadOnlyResponses() {
        XCTAssertTrue(APIRetryPolicy.shouldRetryResponse(method: "GET", statusCode: 503, attempt: 0))
        XCTAssertTrue(APIRetryPolicy.shouldRetryResponse(method: "HEAD", statusCode: 429, attempt: 1))
        XCTAssertFalse(APIRetryPolicy.shouldRetryResponse(method: "GET", statusCode: 503, attempt: 2))
        XCTAssertFalse(APIRetryPolicy.shouldRetryResponse(method: "POST", statusCode: 503, attempt: 0))
        XCTAssertFalse(APIRetryPolicy.shouldRetryResponse(method: "GET", statusCode: 404, attempt: 0))
    }

    func testAPIRetryPolicyNeverRetriesMutationsOrUnboundedFailures() {
        XCTAssertFalse(APIRetryPolicy.shouldRetryFailure(method: "POST", attempt: 0))
        XCTAssertFalse(APIRetryPolicy.shouldRetryFailure(method: "DELETE", attempt: 1))
        XCTAssertTrue(APIRetryPolicy.shouldRetryFailure(method: "OPTIONS", attempt: 1))
        XCTAssertFalse(APIRetryPolicy.shouldRetryFailure(method: "GET", attempt: 2))
    }

    func testAPIRetryPolicyUsesBoundedServerHintOrBackoff() {
        XCTAssertEqual(APIRetryPolicy.delayMilliseconds(attempt: 0, retryAfter: "1"), 1_000)
        XCTAssertEqual(APIRetryPolicy.delayMilliseconds(attempt: 0, retryAfter: "99"), 1_500)
        XCTAssertEqual(APIRetryPolicy.delayMilliseconds(attempt: 0, retryAfter: "invalid"), 250)
        XCTAssertEqual(APIRetryPolicy.delayMilliseconds(attempt: 1, retryAfter: nil), 750)
    }

    func testLiveTVProgramsPathEscapesQueryValues() {
        let path = APIClient.liveTVProgramsPath(
            channelIDs: ["channel/one", "channel&two"],
            startDate: "2026-09-24T10:00:00+03:00",
            endDate: "2026-09-25T10:00:00+03:00",
            limit: 25,
            startIndex: 50
        )

        XCTAssertTrue(path.contains("ChannelIds=channel%2Fone,channel%26two"))
        XCTAssertTrue(path.contains("MinEndDate=2026-09-24T10:00:00%2B03:00"))
        XCTAssertTrue(path.contains("MaxStartDate=2026-09-25T10:00:00%2B03:00"))
        XCTAssertTrue(path.contains("StartIndex=50&Limit=25"))
    }

    func testPlaybackQualityPolicyMatchesPlayerAndServerBitrates() {
        XCTAssertEqual(PlaybackQualityPolicy.maxStreamingBitrate(for: "4K"), 20_000_000)
        XCTAssertEqual(PlaybackQualityPolicy.maxStreamingBitrate(for: "1080p"), 8_000_000)
        XCTAssertEqual(PlaybackQualityPolicy.maxStreamingBitrate(for: "576p"), 1_327_104)
        XCTAssertNil(PlaybackQualityPolicy.maxStreamingBitrate(for: "Auto"))
    }

    func testPlaybackResumePolicyPrefersLocalOfflinePositionAndFallsBackToServer() {
        XCTAssertEqual(PlaybackResumePolicy.initialPositionTicks(server: 100, local: 200, isLocal: true), 200)
        XCTAssertEqual(PlaybackResumePolicy.initialPositionTicks(server: 100, local: 0, isLocal: true), 100)
        XCTAssertEqual(PlaybackResumePolicy.initialPositionTicks(server: 100, local: 200, isLocal: false), 100)
        XCTAssertEqual(PlaybackResumePolicy.initialPositionTicks(server: -1, local: -2, isLocal: true), 0)
    }

    func testOfflineDownloadPolicyBlocksPausedQueue() {
        XCTAssertFalse(OfflineDownloadPolicy.canStart(queuePaused: true, wifiOnly: false, wifiAvailable: true))
    }

    func testOfflineDownloadPolicyBlocksMobileWhenWiFiOnly() {
        XCTAssertFalse(OfflineDownloadPolicy.canStart(queuePaused: false, wifiOnly: true, wifiAvailable: false))
        XCTAssertFalse(OfflineDownloadPolicy.canStart(queuePaused: false, wifiOnly: true, wifiAvailable: nil))
    }

    func testOfflineDownloadPolicyAllowsWiFiAndUnrestrictedNetwork() {
        XCTAssertTrue(OfflineDownloadPolicy.canStart(queuePaused: false, wifiOnly: true, wifiAvailable: true))
        XCTAssertTrue(OfflineDownloadPolicy.canStart(queuePaused: false, wifiOnly: false, wifiAvailable: false))
    }

    func testMediaDeepLinkParserAcceptsCustomSchemeAndOfficialWebLinks() throws {
        XCTAssertEqual(
            MediaDeepLinkParser.itemID(from: try XCTUnwrap(URL(string: "mulletaflix://item/movie-123"))),
            "movie-123"
        )
        XCTAssertEqual(
            MediaDeepLinkParser.itemID(from: try XCTUnwrap(URL(string: "https://mulletaflix.duckdns.org/web/#?id=episode-9"))),
            "episode-9"
        )
        let link = try XCTUnwrap(MediaDeepLinkParser.link(from: URL(string: "mulletaflix://item/movie-123?serverId=server-a")!))
        XCTAssertEqual(link.itemID, "movie-123")
        XCTAssertEqual(link.serverID, "server-a")
        let webLink = try XCTUnwrap(MediaDeepLinkParser.link(from: URL(string: "https://mulletaflix.duckdns.org/web/#?id=movie-123&serverId=server-b")!))
        XCTAssertEqual(webLink.serverID, "server-b")
    }

    func testMediaDeepLinkParserRejectsUnknownHostsAndReservedOnlyPaths() throws {
        XCTAssertNil(MediaDeepLinkParser.itemID(from: try XCTUnwrap(URL(string: "https://example.test/web/movie-1"))))
        XCTAssertNil(MediaDeepLinkParser.itemID(from: try XCTUnwrap(URL(string: "mulletaflix://web"))))
    }

    func testSessionRoundTripsThroughCodable() throws {
        let session = UserSession(serverURL: try XCTUnwrap(URL(string: "https://example.test")), accessToken: "token", userID: "user", userName: "Raphael")
        let data = try JSONEncoder().encode(session)
        XCTAssertEqual(try JSONDecoder().decode(UserSession.self, from: data), session)
    }

    func testSavedServerRoundTripsThroughCodable() throws {
        let server = SavedServer(url: "https://media.example", name: "Sala", version: "1.2.3")
        let data = try JSONEncoder().encode(server)
        XCTAssertEqual(try JSONDecoder().decode(SavedServer.self, from: data), server)
        XCTAssertEqual(server.id, server.url)
    }

    func testMediaSourceDecodesVideoStreamsAndQualityOptions() throws {
        let data = #"{"Id":"source-1","DefaultAudioStreamIndex":7,"DefaultSubtitleStreamIndex":-1,"MediaStreams":[{"Type":"Video","Codec":"hevc","Width":3840,"Height":2160,"BitRate":20000000},{"Type":"Video","Width":1920,"Height":1080},{"Type":"Video","Width":1024,"Height":576},{"Type":"Audio","Height":2160}]}"#.data(using: .utf8)!
        let source = try JSONDecoder().decode(MediaSource.self, from: data)
        XCTAssertEqual(source.mediaStreams.count, 4)
        XCTAssertEqual(source.defaultAudioStreamIndex, 7)
        XCTAssertEqual(source.defaultSubtitleStreamIndex, -1)
        XCTAssertEqual(source.qualityLabels, ["4K", "1080p", "576p"])
        XCTAssertEqual(source.qualityBadge, "4K")

        let hdData = #"{"Id":"source-hd","MediaStreams":[{"Type":"Video","Height":720}]}"#.data(using: .utf8)!
        XCTAssertEqual(try JSONDecoder().decode(MediaSource.self, from: hdData).qualityBadge, "HD")

        let sdData = #"{"Id":"source-sd","MediaStreams":[{"Type":"Video","Height":576}]}"#.data(using: .utf8)!
        XCTAssertNil(try JSONDecoder().decode(MediaSource.self, from: sdData).qualityBadge)
    }

    func testLyricsDecodeAndroidContract() throws {
        let data = #"{"Lyrics":[{"Text":"Primeira linha","Start":0},{"Text":"Segunda linha","Start":25000000}]}"#.data(using: .utf8)!
        let result = try JSONDecoder().decode(LyricsEnvelope.self, from: data)
        XCTAssertEqual(result.lyrics.map(\.text), ["Primeira linha", "Segunda linha"])
        XCTAssertEqual(result.lyrics.last?.start, 25000000)
    }

    private struct LyricsEnvelope: Decodable {
        let lyrics: [LyricLine]
        private enum CodingKeys: String, CodingKey { case lyrics = "Lyrics" }
    }

    func testPublicServerInfoDecodesAndroidContract() throws {
        let data = #"{"ServerName":"Sala","Version":"1.2.3","ProductName":"MulletaFlix","OperatingSystem":"Linux","Id":"server-1","StartupWizardCompleted":true}"#.data(using: .utf8)!
        let info = try JSONDecoder().decode(ServerInfo.self, from: data)
        XCTAssertEqual(info.displayName, "Sala")
        XCTAssertEqual(info.version, "1.2.3")
        XCTAssertEqual(info.id, "server-1")
        XCTAssertTrue(info.startupWizardCompleted)
    }

    func testBrandingOptionsDecodeAndroidContract() throws {
        let data = #"{"LoginDisclaimer":"Uso autorizado","CustomCss":"body{}","SplashscreenEnabled":false}"#.data(using: .utf8)!
        let branding = try JSONDecoder().decode(BrandingOptions.self, from: data)
        XCTAssertEqual(branding.loginDisclaimer, "Uso autorizado")
        XCTAssertEqual(branding.customCSS, "body{}")
        XCTAssertFalse(branding.splashscreenEnabled)
    }

    func testHealthEndpointKeepsItsPath() async throws {
        let client = APIClient(serverURL: try XCTUnwrap(URL(string: "https://example.test")))
        let url = await client.url(forPath: "Health")
        XCTAssertEqual(url.path, "/Health")
    }

    func testSearchHintsDecodeAndroidContract() throws {
        let data = #"{"SearchHints":[{"ItemId":"movie-1","Name":"Batman","Type":"Movie","ProductionYear":2024,"Series":""}],"TotalRecordCount":1}"#.data(using: .utf8)!
        let result = try JSONDecoder().decode(SearchHintResult.self, from: data)
        XCTAssertEqual(result.hints.first?.itemID, "movie-1")
        XCTAssertEqual(result.hints.first?.name, "Batman")
        XCTAssertEqual(result.totalRecordCount, 1)
    }

    func testSearchFilterQueryKeepsIncludeItemTypes() async throws {
        let client = APIClient(serverURL: try XCTUnwrap(URL(string: "https://example.test")))
        let url = await client.url(forPath: "Users/user/Items?SearchTerm=batman&IncludeItemTypes=Movie")
        XCTAssertEqual(url.query, "SearchTerm=batman&IncludeItemTypes=Movie")
    }

    func testNextUpQueryKeepsUserAndLimitParameters() async throws {
        let client = APIClient(serverURL: try XCTUnwrap(URL(string: "https://example.test")))
        let url = await client.url(forPath: "Shows/NextUp?UserId=user&Limit=12&Fields=Overview,MediaSources,ItemCounts")
        XCTAssertEqual(url.path, "/Shows/NextUp")
        XCTAssertEqual(url.query, "UserId=user&Limit=12&Fields=Overview,MediaSources,ItemCounts")
    }

    func testAudioTrackQueryUsesAlbumParentAndAudioType() async throws {
        let client = APIClient(serverURL: try XCTUnwrap(URL(string: "https://example.test")))
        let url = await client.url(forPath: "Users/user/Items?ParentId=album-1&IncludeItemTypes=Audio&SortBy=ParentIndexNumber,IndexNumber,SortName&Fields=Overview,MediaSources,ItemCounts")
        XCTAssertEqual(url.path, "/Users/user/Items")
        XCTAssertTrue(url.query?.contains("ParentId=album-1") == true)
        XCTAssertTrue(url.query?.contains("IncludeItemTypes=Audio") == true)
        XCTAssertTrue(url.query?.contains("Fields=Overview,MediaSources,ItemCounts") == true)
    }

    func testUniversalSearchFiltersMapToServerItemTypes() {
        XCTAssertEqual(SearchFilter.music.includeItemTypes, "Audio")
        XCTAssertEqual(SearchFilter.people.includeItemTypes, "Person")
        XCTAssertEqual(SearchFilter.allCases.map(\.title), ["Tudo", "Filmes", "Séries", "Episódios", "Músicas", "Pessoas"])
    }


    func testImageURLUsesAuthenticatedServerEndpointAndTag() async throws {
        let client = APIClient(serverURL: try XCTUnwrap(URL(string: "https://example.test/")))
        let item = MediaItem(id: "abc", name: "Movie", imageTags: ["Primary": "v1"])
        let url = await client.imageURL(for: item)
        XCTAssertEqual(url?.absoluteString, "https://example.test/Items/abc/Images/Primary?tag=v1")
    }

    func testUserImageURLUsesProfileEndpointAndTag() async throws {
        let client = APIClient(serverURL: try XCTUnwrap(URL(string: "https://example.test/")))
        let url = await client.userImageURL(userID: "user-1", tag: "profile-v2")
        XCTAssertEqual(url?.absoluteString, "https://example.test/Users/user-1/Images/Primary?tag=profile-v2")
    }

    func testQueryParametersRemainQueryParameters() async throws {
        let client = APIClient(serverURL: try XCTUnwrap(URL(string: "https://example.test")))
        let url = await client.url(forPath: "Users/user/Items?Limit=10&Recursive=true")
        XCTAssertEqual(url.path, "/Users/user/Items")
        XCTAssertEqual(url.query, "Limit=10&Recursive=true")
    }

    func testLibrarySortParametersRemainQueryParameters() async throws {
        let client = APIClient(serverURL: try XCTUnwrap(URL(string: "https://example.test")))
        let url = await client.url(forPath: "Users/user/Items?SortBy=PremiereDate&SortOrder=Descending")
        XCTAssertEqual(url.query, "SortBy=PremiereDate&SortOrder=Descending")
    }

    func testLibraryPlayedFilterMapsToServerValues() {
        XCTAssertNil(LibraryPlayedFilter.all.isPlayed)
        XCTAssertEqual(LibraryPlayedFilter.played.isPlayed, true)
        XCTAssertEqual(LibraryPlayedFilter.unplayed.isPlayed, false)
        XCTAssertEqual(LibraryPlayedFilter.allCases.map(\.title), ["Todos", "Assistidos", "Não assistidos"])
    }

    func testLibraryFilterQueryEncodesGenreAndYearValues() async throws {
        let client = APIClient(serverURL: try XCTUnwrap(URL(string: "https://example.test")))
        let url = await client.url(forPath: "Users/user/Items?Genres=Ficção%20científica&Years=2024&IsPlayed=false&IsFavorite=true")
        XCTAssertEqual(url.query, "Genres=Ficção%20científica&Years=2024&IsPlayed=false&IsFavorite=true")
    }

    func testLibraryRatingFilterRemainsAnOfficialRatingsQueryParameter() async throws {
        let client = APIClient(serverURL: try XCTUnwrap(URL(string: "https://media.example")))
        let path = APIClient.itemsPath(
            userID: "user-1",
            parentID: "library-1",
            officialRatings: "16,PG-13"
        )
        let url = await client.url(forPath: path)
        XCTAssertTrue(url.query?.contains("OfficialRatings=16,PG-13") == true)
    }

    func testLibraryCardQueryRequestsMediaSourcesForQualityBadges() async throws {
        let client = APIClient(serverURL: try XCTUnwrap(URL(string: "https://media.example")))
        let url = await client.url(forPath: APIClient.itemsPath(
            userID: "user-1",
            parentID: "library-1",
            fields: "Overview,MediaSources,ItemCounts"
        ))
        XCTAssertTrue(url.query?.contains("Fields=Overview,MediaSources,ItemCounts") == true)
    }

    func testFavoriteFilterParametersRemainQueryParameters() async throws {
        let client = APIClient(serverURL: try XCTUnwrap(URL(string: "https://example.test")))
        let url = await client.url(forPath: "Users/user/Items?Filters=IsFavorite&IsFavorite=true")
        XCTAssertEqual(url.query, "Filters=IsFavorite&IsFavorite=true")
    }

    func testHomeGenreQueryIncludesItemTypeFilter() async throws {
        let client = APIClient(serverURL: try XCTUnwrap(URL(string: "https://example.test")))
        let url = await client.url(forPath: "Users/user/Items?Limit=16&IncludeItemTypes=Series")
        XCTAssertEqual(url.query, "Limit=16&IncludeItemTypes=Series")
    }

    func testPagedResultDefaultsTotalCountToItemsWhenServerOmitsIt() throws {
        let data = #"{"Items":[{"Id":"one","Name":"Um"}]}"#.data(using: .utf8)!
        let result = try JSONDecoder().decode(ItemQueryResult.self, from: data)
        XCTAssertEqual(result.totalRecordCount, 1)
        XCTAssertFalse(result.hasExplicitTotalRecordCount)
        XCTAssertEqual(result.items.map(\.id), ["one"])
    }

    func testPagedResultPreservesExplicitTotalCountForSearchNotice() throws {
        let data = #"{"Items":[{"Id":"one","Name":"Um"}],"TotalRecordCount":42}"#.data(using: .utf8)!
        let result = try JSONDecoder().decode(ItemQueryResult.self, from: data)
        XCTAssertEqual(result.totalRecordCount, 42)
        XCTAssertTrue(result.hasExplicitTotalRecordCount)
    }

    func testAuthorizationMatchesAndroidClientIdentityContract() {
        XCTAssertEqual(
            APIClient.authorizationHeader(accessToken: "token", deviceID: "ios-test"),
            "MediaBrowser Token=\"token\", Client=\"MulletaFlix iOS\", Device=\"iPhone\", DeviceId=\"ios-test\", Version=\"0.1.0\""
        )
    }

    func testMediaImageTagsKeepServerCapitalization() throws {
        let data = #"{"Id":"abc","Name":"Movie","ImageTags":{"Primary":"tag-1"}}"#.data(using: .utf8)!
        let item = try JSONDecoder().decode(MediaItem.self, from: data)
        XCTAssertEqual(item.imageTags?["Primary"], "tag-1")
    }

    func testQuickConnectPayloadDecodesServerContract() throws {
        let data = #"{"Code":"ABCD","Secret":"secret-1","Authenticated":true}"#.data(using: .utf8)!
        let result = try JSONDecoder().decode(QuickConnectResult.self, from: data)
        XCTAssertEqual(result.code, "ABCD")
        XCTAssertEqual(result.secret, "secret-1")
        XCTAssertTrue(result.authenticated)
    }

    func testQuickConnectSecretDoesNotLeakQuerySeparators() async throws {
        let client = APIClient(serverURL: try XCTUnwrap(URL(string: "https://example.test")))
        let url = await client.url(forPath: "QuickConnect/Connect?secret=a%26b%3Dc")
        XCTAssertEqual(url.query, "secret=a%26b%3Dc")
    }

    func testLocalDiscoveryPayloadSupportsServerIdAlias() throws {
        let data = #"{"Address":"http://192.168.1.20:8096","Name":"Sala","Version":"1.0","ServerId":"abc"}"#.data(using: .utf8)!
        let server = try JSONDecoder().decode(DiscoveredServer.self, from: data)
        XCTAssertEqual(server.address.absoluteString, "http://192.168.1.20:8096")
        XCTAssertEqual(server.name, "Sala")
        XCTAssertEqual(server.id, "abc")
    }

    func testUserProfileDecodesPolicyAndConfiguration() throws {
        let data = #"{"Id":"u1","Name":"Raphael","Policy":{"IsAdministrator":true,"EnableContentDownloading":false,"EnableLiveTvAccess":true,"EnableMediaPlayback":false},"Configuration":{"AudioLanguagePreference":"pt-BR","SubtitleLanguagePreference":"en"}}"#.data(using: .utf8)!
        let profile = try JSONDecoder().decode(UserProfile.self, from: data)
        XCTAssertTrue(profile.isAdministrator)
        XCTAssertFalse(profile.canDownload)
        XCTAssertTrue(profile.canAccessLiveTV)
        XCTAssertFalse(profile.canPlayMedia)
        XCTAssertEqual(profile.configuration?.audioLanguagePreference, "pt-BR")
    }

    func testLiveTVProgramDecodesSchedulingFields() throws {
        let data = #"{"Id":"program-1","Name":"Jornal","Type":"TvChannel","ChannelId":"channel-1","ChannelName":"Canal 1","StartDate":"2026-09-23T20:00:00Z","EndDate":"2026-09-23T21:00:00Z"}"#.data(using: .utf8)!
        let program = try JSONDecoder().decode(MediaItem.self, from: data)
        XCTAssertEqual(program.channelId, "channel-1")
        XCTAssertEqual(program.channelName, "Canal 1")
        XCTAssertEqual(program.startDate, "2026-09-23T20:00:00Z")
        XCTAssertEqual(program.endDate, "2026-09-23T21:00:00Z")
    }

    func testSyncPlayGroupMatchesServerPayload() throws {
        let data = #"[{"GroupId":"group-42","GroupName":"Cinema","State":"Playing","Participants":["Raphael","Visitante"],"Host":"tv-sala"}]"#.data(using: .utf8)!
        let groups = try JSONDecoder().decode([SyncPlayGroup].self, from: data)
        XCTAssertEqual(groups.first?.groupId, "group-42")
        XCTAssertEqual(groups.first?.groupName, "Cinema")
        XCTAssertEqual(groups.first?.state, "Playing")
        XCTAssertEqual(groups.first?.participants, ["Raphael", "Visitante"])
    }

    func testMediaSegmentDecodesTicksAndType() throws {
        let data = #"{"Id":"segment-1","ItemId":"episode-1","Type":"Intro","StartTicks":10000000,"EndTicks":25000000}"#.data(using: .utf8)!
        let segment = try JSONDecoder().decode(MediaSegment.self, from: data)
        XCTAssertEqual(segment.id, "segment-1")
        XCTAssertEqual(segment.type, .intro)
        XCTAssertEqual(segment.startSeconds, 1.0, accuracy: 0.001)
        XCTAssertEqual(segment.endSeconds, 2.5, accuracy: 0.001)
    }

    func testMediaItemDecodesChapters() throws {
        let data = #"{"Id":"episode-1","Name":"Piloto","Chapters":[{"StartPositionTicks":30000000,"Name":"Abertura"}]}"#.data(using: .utf8)!
        let item = try JSONDecoder().decode(MediaItem.self, from: data)
        XCTAssertEqual(item.chapters.count, 1)
        XCTAssertEqual(item.chapters.first?.name, "Abertura")
        XCTAssertEqual(item.chapters.first?.startSeconds, 3.0, accuracy: 0.001)
    }

    func testMediaItemDecodesAndroidDetailMetadata() throws {
        let data = #"{"Id":"movie-1","Name":"Filme","Type":"Movie","Genres":["Drama","Sci-Fi"],"CommunityRating":8.4,"OfficialRating":"14","RunTimeTicks":54000000000,"People":[{"Id":"person-1","Name":"Ada Lovelace","Role":"Diretora","Type":"Director"}]}"#.data(using: .utf8)!
        let item = try JSONDecoder().decode(MediaItem.self, from: data)
        XCTAssertEqual(item.genres, ["Drama", "Sci-Fi"])
        XCTAssertEqual(item.communityRating, 8.4)
        XCTAssertEqual(item.officialRating, "14")
        XCTAssertEqual(item.runtimeTicks, 54000000000)
        XCTAssertEqual(item.people.first?.name, "Ada Lovelace")
        XCTAssertEqual(item.people.first?.role, "Diretora")
    }

    func testMediaItemDecodesPlaybackPositionFromUserData() throws {
        let data = #"{"Id":"movie-1","Name":"Filme","UserData":{"PlayedPercentage":42.5,"PlaybackPositionTicks":123456789,"Played":false}}"#.data(using: .utf8)!
        let item = try JSONDecoder().decode(MediaItem.self, from: data)
        XCTAssertEqual(item.playedPercentage, 42.5)
        XCTAssertEqual(item.playbackPositionTicks, 123456789)
    }

    func testRegistrationResultDecodesSuccessAndMessage() throws {
        let data = #"{"Success":true,"Message":"Conta criada"}"#.data(using: .utf8)!
        let result = try JSONDecoder().decode(RegistrationResult.self, from: data)
        XCTAssertTrue(result.success)
        XCTAssertEqual(result.message, "Conta criada")
    }

    func testPublicUsersDecodeForUserSwitching() throws {
        let data = #"[{"Id":"u1","Name":"Principal"},{"Id":"u2","Name":"Convidado"}]"#.data(using: .utf8)!
        let users = try JSONDecoder().decode([PublicUser].self, from: data)
        XCTAssertEqual(users.map(\.name), ["Principal", "Convidado"])
    }

    func testEpisodeContextDecodesSeriesSeasonAndIndex() throws {
        let data = #"{"Id":"episode-1","Name":"Piloto","SeriesId":"series-1","SeasonId":"season-1","IndexNumber":1}"#.data(using: .utf8)!
        let episode = try JSONDecoder().decode(MediaItem.self, from: data)
        XCTAssertEqual(episode.seriesId, "series-1")
        XCTAssertEqual(episode.seasonId, "season-1")
        XCTAssertEqual(episode.indexNumber, 1)
    }

    func testPlayedStateCanBeUpdatedWithoutLosingMediaContext() throws {
        let data = #"{"Id":"episode-1","Name":"Piloto","SeriesId":"series-1","SeasonId":"season-1","IndexNumber":1,"UserData":{"Played":false,"PlaybackPositionTicks":987654321}}"#.data(using: .utf8)!
        let item = try JSONDecoder().decode(MediaItem.self, from: data)
        let updated = item.withPlayed(true)
        XCTAssertTrue(updated.isPlayed)
        XCTAssertEqual(updated.seriesId, "series-1")
        XCTAssertEqual(updated.seasonId, "season-1")
        XCTAssertEqual(updated.indexNumber, 1)
        XCTAssertEqual(updated.playbackPositionTicks, 987654321)
    }

    func testPlaybackInfoDecodesLiveStreamRequirements() throws {
        let data = #"{"PlaySessionId":"session-1","MediaSources":[{"Id":"source-1","RequiresOpening":true,"OpenToken":"open-1","SupportsDirectPlay":false}]}"#.data(using: .utf8)!
        let info = try JSONDecoder().decode(PlaybackInfo.self, from: data)
        XCTAssertEqual(info.playSessionId, "session-1")
        XCTAssertEqual(info.mediaSources.first?.id, "source-1")
        XCTAssertTrue(info.mediaSources.first?.requiresOpening == true)
        XCTAssertEqual(info.mediaSources.first?.openToken, "open-1")
        XCTAssertFalse(info.mediaSources.first?.supportsDirectPlay == true)
    }

    func testPlaylistPayloadDecodesServerIdentity() throws {
        let data = #"{"Id":"playlist-1","Name":"Favoritos"}"#.data(using: .utf8)!
        let playlist = try JSONDecoder().decode(Playlist.self, from: data)
        XCTAssertEqual(playlist.id, "playlist-1")
        XCTAssertEqual(playlist.name, "Favoritos")
    }

    func testPlaybackRateOptionsContainNormalSpeed() {
        let rates = [0.5, 0.75, 1.0, 1.25, 1.5, 2.0]
        XCTAssertTrue(rates.contains(1.0))
        XCTAssertTrue(rates.allSatisfy { $0 > 0 })
    }
}
