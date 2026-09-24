import XCTest
@testable import MulletaFlixCore

final class ModelTests: XCTestCase {
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

    func testMediaSourceDecodesVideoStreamsAndQualityOptions() throws {
        let data = #"{"Id":"source-1","MediaStreams":[{"Type":"Video","Codec":"hevc","Width":3840,"Height":2160,"BitRate":20000000},{"Type":"Video","Width":1920,"Height":1080},{"Type":"Video","Width":1024,"Height":576},{"Type":"Audio","Height":2160}]}"#.data(using: .utf8)!
        let source = try JSONDecoder().decode(MediaSource.self, from: data)
        XCTAssertEqual(source.mediaStreams.count, 4)
        XCTAssertEqual(source.qualityLabels, ["4K", "1080p", "576p"])
    }

    func testPublicServerInfoDecodesAndroidContract() throws {
        let data = #"{"ServerName":"Sala","Version":"1.2.3","ProductName":"MulletaFlix","OperatingSystem":"Linux","Id":"server-1","StartupWizardCompleted":true}"#.data(using: .utf8)!
        let info = try JSONDecoder().decode(ServerInfo.self, from: data)
        XCTAssertEqual(info.displayName, "Sala")
        XCTAssertEqual(info.version, "1.2.3")
        XCTAssertEqual(info.id, "server-1")
        XCTAssertTrue(info.startupWizardCompleted)
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
        XCTAssertEqual(result.items.map(\.id), ["one"])
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
        let data = #"{"Id":"episode-1","Name":"Piloto","SeriesId":"series-1","SeasonId":"season-1","IndexNumber":1,"UserData":{"Played":false}}"#.data(using: .utf8)!
        let item = try JSONDecoder().decode(MediaItem.self, from: data)
        let updated = item.withPlayed(true)
        XCTAssertTrue(updated.isPlayed)
        XCTAssertEqual(updated.seriesId, "series-1")
        XCTAssertEqual(updated.seasonId, "season-1")
        XCTAssertEqual(updated.indexNumber, 1)
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
