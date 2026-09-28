import XCTest
@testable import MulletaFlixCore

final class ModelTests: XCTestCase {
    func testAppIdentityKeepsInitialProductionVersionAndUserAgentAligned() {
        XCTAssertEqual(AppIdentity.version, "1.0.0")
        XCTAssertEqual(AppIdentity.userAgent, "MulletaFlix-iOS/\(AppIdentity.version)")
    }

    func testTrackPreferencePolicyCanonicalizesLanguagesAndDisablesSubtitles() throws {
        let data = #"{"MediaStreams":[{"Index":0,"Type":"Audio","Language":"eng","IsDefault":true},{"Index":1,"Type":"Subtitle","Language":"por"},{"Index":2,"Type":"Subtitle","Language":"spa","IsDefault":true}]}"#.data(using: .utf8)!
        let source = try JSONDecoder().decode(MediaSource.self, from: data)

        XCTAssertEqual(MediaLanguage.canonicalize("pt-BR"), "pt")
        XCTAssertEqual(MediaLanguage.canonicalize("desativadas"), MediaLanguage.off)
        XCTAssertEqual(
            TrackPreferencePolicy.preferredStreamIndex(
                streams: source.mediaStreams.filter { $0.type == "Subtitle" },
                preferredLanguage: "por",
                serverDefaultIndex: source.defaultSubtitleStreamIndex,
                isSubtitle: true
            ),
            1
        )
        XCTAssertEqual(
            TrackPreferencePolicy.preferredStreamIndex(
                streams: source.mediaStreams.filter { $0.type == "Subtitle" },
                preferredLanguage: "off",
                serverDefaultIndex: 2,
                isSubtitle: true
            ),
            -1
        )
    }

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
        XCTAssertEqual(
            AuthErrorPolicy.registrationMessage(for: APIError.serverMessage("Usuário já existe.")),
            "Usuário já existe."
        )
        XCTAssertEqual(
            AuthErrorPolicy.registrationMessage(for: URLError(.timedOut)),
            "O servidor demorou para responder. Tente novamente."
        )
    }

    func testQuickConnectErrorPolicyKeepsTransientFailuresPolling() {
        XCTAssertTrue(QuickConnectErrorPolicy.shouldContinuePolling(after: URLError(.networkConnectionLost)))
        XCTAssertTrue(QuickConnectErrorPolicy.shouldContinuePolling(after: APIError.httpStatus(500)))
        XCTAssertFalse(QuickConnectErrorPolicy.shouldContinuePolling(after: APIError.httpStatus(403)))
        XCTAssertEqual(
            QuickConnectErrorPolicy.terminalMessage(for: APIError.httpStatus(403)),
            "Quick Connect está desativado ou requer autorização no servidor."
        )
        XCTAssertNil(QuickConnectErrorPolicy.terminalMessage(for: URLError(.timedOut)))
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
        XCTAssertTrue(
            ForegroundRefreshPolicy.shouldRefresh(
                isSignedIn: true,
                isNetworkAvailable: nil,
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
        XCTAssertEqual(APIRetryPolicy.delayMilliseconds(attempt: 0, retryAfter: String(Int.max)), 1_500)
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

    func testLiveTVTimerMapsProgramToTimerAndCancelPath() throws {
        let data = #"{"Items":[{"Id":"timer-1","ProgramId":"program-1"},{"Id":"","ProgramId":"program-2"},{"Id":"timer-3","ProgramId":""}]}"#.data(using: .utf8)!
        let result = try JSONDecoder().decode(LiveTVTimerQuery.self, from: data)
        let valid = result.items.reduce(into: [String: String]()) { timers, timer in
            if let programID = timer.programId?.trimmingCharacters(in: .whitespacesAndNewlines),
               !programID.isEmpty,
               let timerID = timer.id?.trimmingCharacters(in: .whitespacesAndNewlines),
               !timerID.isEmpty {
                timers[programID] = timerID
            }
        }
        XCTAssertEqual(valid, ["program-1": "timer-1"])
        XCTAssertEqual(APIClient.cancelLiveTVTimerPath(timerID: "timer/1"), "LiveTv/Timers/timer%2F1")
    }

    func testPlaylistItemsPathIncludesAuthenticatedPagingContract() {
        XCTAssertEqual(
            APIClient.playlistItemsPath(userID: "user/1", playlistID: "playlist&1", startIndex: 60, limit: 30),
            "Playlists/playlist%261/Items?UserId=user%2F1&StartIndex=60&Limit=30&EnableImages=true&EnableUserData=true"
        )
    }

    func testPlaylistItemsDecodeMediaPayload() throws {
        let data = #"{"Items":[{"Id":"movie-1","Name":"Filme","Type":"Movie"}],"TotalRecordCount":1}"#.data(using: .utf8)!
        let result = try JSONDecoder().decode(ItemQueryResult.self, from: data)
        XCTAssertEqual(result.items.map(\.id), ["movie-1"])
        XCTAssertEqual(result.totalRecordCount, 1)
    }

    func testAPIQueryValueEscapesReservedCharactersWithoutChangingParameterBoundaries() {
        XCTAssertEqual(
            APIClient.queryValue("C++ #1/? &=100%"),
            "C%2B%2B%20%231%2F%3F%20%26%3D100%25"
        )
    }

    func testPlaybackQualityPolicyMatchesPlayerAndServerBitrates() {
        XCTAssertEqual(PlaybackQualityPolicy.maxStreamingBitrate(for: "4K"), 20_000_000)
        XCTAssertEqual(PlaybackQualityPolicy.maxStreamingBitrate(for: "1080p"), 8_000_000)
        XCTAssertEqual(PlaybackQualityPolicy.maxStreamingBitrate(for: "576p"), 1_327_104)
        XCTAssertNil(PlaybackQualityPolicy.maxStreamingBitrate(for: "Auto"))
    }

    func testPlaybackQualityPolicyPreservesValidStoredResolutionsAndRejectsJunk() {
        XCTAssertEqual(PlaybackQualityPolicy.normalizedPreference(" 360P "), "360p")
        XCTAssertEqual(PlaybackQualityPolicy.normalizedPreference("FULL HD"), "1080p")
        XCTAssertEqual(PlaybackQualityPolicy.normalizedPreference("9999p"), "Auto")
        XCTAssertNil(PlaybackQualityPolicy.maxStreamingBitrate(for: "9999p"))
        XCTAssertEqual(
            PlaybackQualityPolicy.settingsChoices(storedPreference: "576p"),
            ["Auto", "4K", "1440p", "1080p", "720p", "480p", "576p"]
        )
    }

    func testPlaybackQualityPolicyCapsOnlyAutoOnMeteredNetworks() {
        XCTAssertEqual(PlaybackQualityPolicy.effectivePreference(for: "Auto", isMetered: true), "720p")
        XCTAssertEqual(PlaybackQualityPolicy.effectiveStreamingBitrate(for: "Auto", isMetered: true), 4_000_000)
        XCTAssertEqual(PlaybackQualityPolicy.effectivePreference(for: "1080p", isMetered: true), "1080p")
        XCTAssertEqual(PlaybackQualityPolicy.effectiveStreamingBitrate(for: "1080p", isMetered: true), 8_000_000)
        XCTAssertNil(PlaybackQualityPolicy.effectiveStreamingBitrate(for: "Auto", isMetered: false))
        XCTAssertEqual(PlaybackQualityPolicy.displayLabel(for: "Auto", isMetered: true), "Auto (até 720p nesta rede)")
        XCTAssertEqual(PlaybackQualityPolicy.displayLabel(for: "1080p", isMetered: true), "1080p")
    }

    func testMediaLanguageCanonicalizesManualTrackPreferences() {
        XCTAssertEqual(MediaLanguage.canonicalize("pt-BR"), "pt")
        XCTAssertEqual(MediaLanguage.canonicalize("English_US"), "en")
        XCTAssertEqual(MediaLanguage.canonicalize("none"), MediaLanguage.off)
    }

    func testPlaybackResumePolicyPrefersLocalOfflinePositionAndFallsBackToServer() {
        XCTAssertEqual(PlaybackResumePolicy.initialPositionTicks(server: 100, local: 200, isLocal: true), 200)
        XCTAssertEqual(PlaybackResumePolicy.initialPositionTicks(server: 100, local: 0, isLocal: true), 100)
        XCTAssertEqual(PlaybackResumePolicy.initialPositionTicks(server: 100, local: 200, isLocal: false), 100)
        XCTAssertEqual(PlaybackResumePolicy.initialPositionTicks(server: -1, local: -2, isLocal: true), 0)
    }

    func testOfflineDownloadPolicyBlocksPausedQueue() {
        XCTAssertFalse(OfflineDownloadPolicy.canStart(queuePaused: true, wifiOnly: false, wifiAvailable: true, networkAvailable: true))
    }

    func testOfflineDownloadPolicyBlocksMobileWhenWiFiOnly() {
        XCTAssertFalse(OfflineDownloadPolicy.canStart(queuePaused: false, wifiOnly: true, wifiAvailable: false, networkAvailable: true))
        XCTAssertFalse(OfflineDownloadPolicy.canStart(queuePaused: false, wifiOnly: true, wifiAvailable: nil, networkAvailable: true))
    }

    func testOfflineDownloadPolicyAllowsWiFiAndUnrestrictedNetwork() {
        XCTAssertTrue(OfflineDownloadPolicy.canStart(queuePaused: false, wifiOnly: true, wifiAvailable: true, networkAvailable: true))
        XCTAssertTrue(OfflineDownloadPolicy.canStart(queuePaused: false, wifiOnly: false, wifiAvailable: false, networkAvailable: true))
    }

    func testOfflineDownloadPolicyBlocksKnownOfflineNetwork() {
        XCTAssertFalse(OfflineDownloadPolicy.canStart(queuePaused: false, wifiOnly: false, wifiAvailable: false, networkAvailable: false))
        XCTAssertFalse(OfflineDownloadPolicy.canStart(queuePaused: false, wifiOnly: true, wifiAvailable: true, networkAvailable: false))
    }

    func testOfflineDownloadPolicyDoesNotBlockUnknownNetworkState() {
        XCTAssertTrue(OfflineDownloadPolicy.canStart(queuePaused: false, wifiOnly: false, wifiAvailable: nil, networkAvailable: nil))
    }

    func testOfflineDownloadFailurePolicyDetectsDirectInsufficientStorage() {
        let error = NSError(domain: NSPOSIXErrorDomain, code: 28)

        XCTAssertTrue(OfflineDownloadFailurePolicy.isInsufficientStorage(error))
    }

    func testOfflineDownloadFailurePolicyDetectsNestedInsufficientStorage() {
        let underlying = NSError(domain: NSPOSIXErrorDomain, code: 28)
        let error = NSError(
            domain: NSURLErrorDomain,
            code: NSURLErrorCannotWriteToFile,
            userInfo: [NSUnderlyingErrorKey: underlying]
        )

        XCTAssertTrue(OfflineDownloadFailurePolicy.isInsufficientStorage(error))
    }

    func testOfflineDownloadFailurePolicyDoesNotInferStorageFromLocalizedText() {
        let error = NSError(
            domain: NSURLErrorDomain,
            code: NSURLErrorCannotWriteToFile,
            userInfo: [NSLocalizedDescriptionKey: "Espaço insuficiente no servidor"]
        )

        XCTAssertFalse(OfflineDownloadFailurePolicy.isInsufficientStorage(error))
    }

    func testSearchNetworkPolicyBlocksOnlyKnownOfflineState() {
        XCTAssertTrue(SearchNetworkPolicy.canRequest(isNetworkAvailable: nil))
        XCTAssertTrue(SearchNetworkPolicy.canRequest(isNetworkAvailable: true))
        XCTAssertFalse(SearchNetworkPolicy.canRequest(isNetworkAvailable: false))
        XCTAssertEqual(
            SearchNetworkPolicy.offlineMessage,
            "Você está offline. A busca será retomada quando a conexão voltar."
        )
    }

    func testNetworkRequestPolicyBlocksOnlyKnownOfflineState() {
        XCTAssertTrue(NetworkRequestPolicy.canRequest(isNetworkAvailable: nil))
        XCTAssertTrue(NetworkRequestPolicy.canRequest(isNetworkAvailable: true))
        XCTAssertFalse(NetworkRequestPolicy.canRequest(isNetworkAvailable: false))
        XCTAssertEqual(
            NetworkRequestPolicy.offlineMessage,
            "Você está offline. A atualização será retomada quando a conexão voltar."
        )
    }

    func testOfflinePlaybackPositionPolicyClampsKnownDuration() {
        XCTAssertEqual(
            OfflinePlaybackPositionPolicy.normalized(positionSeconds: 42, durationSeconds: 30),
            30
        )
        XCTAssertEqual(
            OfflinePlaybackPositionPolicy.normalized(positionSeconds: 12, durationSeconds: 30),
            12
        )
        XCTAssertEqual(
            OfflinePlaybackPositionPolicy.normalized(positionSeconds: 12, durationSeconds: nil),
            12
        )
        XCTAssertNil(OfflinePlaybackPositionPolicy.normalized(positionSeconds: .nan, durationSeconds: 30))
    }

    func testOfflineNextEpisodePolicyPrefersNextEpisodeInOrder() {
        let current = OfflineEpisodeDescriptor(itemID: "e01", seriesID: "series", seasonNumber: 1, episodeNumber: 1)
        let candidates = [
            OfflineEpisodeDescriptor(itemID: "e03", seriesID: "series", seasonNumber: 1, episodeNumber: 3),
            OfflineEpisodeDescriptor(itemID: "other", seriesID: "other", seasonNumber: 1, episodeNumber: 2),
            OfflineEpisodeDescriptor(itemID: "e02", seriesID: "series", seasonNumber: 1, episodeNumber: 2),
            OfflineEpisodeDescriptor(itemID: "s02e01", seriesID: "series", seasonNumber: 2, episodeNumber: 1)
        ]
        XCTAssertEqual(OfflineNextEpisodePolicy.next(after: current, candidates: candidates)?.itemID, "e02")
    }

    func testPlaybackProgressPolicyClampsKnownDuration() {
        XCTAssertEqual(PlaybackProgressPolicy.normalized(positionSeconds: 42, durationSeconds: 30), 30)
        XCTAssertEqual(PlaybackProgressPolicy.normalized(positionSeconds: 12, durationSeconds: nil), 12)
        XCTAssertNil(PlaybackProgressPolicy.normalized(positionSeconds: -.infinity, durationSeconds: 30))
    }

    func testPlaybackTicksPolicySaturatesOverflowAndRejectsInvalidValues() {
        XCTAssertEqual(PlaybackTicksPolicy.fromSeconds(Double.greatestFiniteMagnitude), Int64.max)
        XCTAssertEqual(PlaybackTicksPolicy.fromSeconds(42, durationSeconds: 30), 300_000_000)
        XCTAssertEqual(PlaybackTicksPolicy.fromSeconds(.nan), 0)
        XCTAssertEqual(PlaybackTicksPolicy.fromSeconds(-1), 0)
    }

    func testOfflineArtworkPolicyBoundsDataAndProducesSafeScopedFileNames() {
        XCTAssertTrue(OfflineArtworkPolicy.accepts(Data([1, 2, 3])))
        XCTAssertFalse(OfflineArtworkPolicy.accepts(Data()))
        XCTAssertFalse(OfflineArtworkPolicy.accepts(Data(repeating: 0, count: OfflineArtworkPolicy.maximumBytes + 1)))
        XCTAssertTrue(OfflineArtworkPolicy.accepts(contentType: "image/jpeg; charset=binary"))
        XCTAssertTrue(OfflineArtworkPolicy.accepts(contentType: nil))
        XCTAssertFalse(OfflineArtworkPolicy.accepts(contentType: "text/html"))
        XCTAssertEqual(OfflineArtworkPolicy.fileName(for: "movie/1"), "bW92aWUvMQ-artwork.image")
        XCTAssertFalse(OfflineArtworkPolicy.fileName(for: "movie/1").contains("/"))
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

    func testSearchHintSelectionCreatesDetailItemWithoutChangingIdentity() {
        let hint = SearchHint(itemID: "movie-1", name: "Filme", type: "Movie", productionYear: 2026, series: nil)
        let item = SearchHintSelectionPolicy.item(from: hint)

        XCTAssertEqual(item.id, "movie-1")
        XCTAssertEqual(item.name, "Filme")
        XCTAssertEqual(item.type, "Movie")
        XCTAssertEqual(item.productionYear, 2026)
    }

    func testDiscoveredServerDecodesBothIdentityFieldNames() throws {
        let decoder = JSONDecoder()
        let direct = try decoder.decode(
            DiscoveredServer.self,
            from: Data(#"{"Address":"http://192.168.1.10:8096","Name":"Sala","Version":"1.0.0","Id":"server-a"}"#.utf8)
        )
        let fallback = try decoder.decode(
            DiscoveredServer.self,
            from: Data(#"{"Address":"http://192.168.1.11:8096","Name":"Quarto","ServerId":"server-b"}"#.utf8)
        )

        XCTAssertEqual(direct.id, "server-a")
        XCTAssertEqual(fallback.id, "server-b")
        XCTAssertEqual(direct.name, "Sala")
        XCTAssertEqual(direct.version, "1.0.0")
    }

    func testServerDiscoverySelectionRequiresSavedIdentityMatch() throws {
        let first = DiscoveredServer(address: try XCTUnwrap(URL(string: "http://192.168.1.10:8096")), name: "Sala", id: "server-a")
        let second = DiscoveredServer(address: try XCTUnwrap(URL(string: "http://192.168.1.11:8096")), name: "Quarto", id: "server-b")

        XCTAssertEqual(
            ServerDiscoverySelectionPolicy.selectedServer(from: [first, second], savedServerID: "SERVER-B")?.id,
            "server-b"
        )
        XCTAssertNil(ServerDiscoverySelectionPolicy.selectedServer(from: [first, second], savedServerID: "server-c"))
        XCTAssertNil(ServerDiscoverySelectionPolicy.selectedServer(from: [first, second], savedServerID: nil))
        XCTAssertEqual(ServerDiscoverySelectionPolicy.selectedServer(from: [first], savedServerID: nil)?.id, "server-a")
    }

    func testServerDiscoveryTimingPolicyKeepsOneBoundedWindow() {
        XCTAssertEqual(ServerDiscoveryTimingPolicy.boundedWindow(-1), 0)
        XCTAssertEqual(ServerDiscoveryTimingPolicy.boundedWindow(2.5), 2.5)
        XCTAssertEqual(ServerDiscoveryTimingPolicy.boundedWindow(60), 10)
        XCTAssertEqual(ServerDiscoveryTimingPolicy.remainingWindow(total: 2.5, elapsed: 0.75), 1.75)
        XCTAssertEqual(ServerDiscoveryTimingPolicy.remainingWindow(total: 2.5, elapsed: 3), 0)
    }

    func testOfflineStoragePolicyPreservesSmallAndUnavailableValues() {
        let gib: Int64 = 1024 * 1024 * 1024
        XCTAssertEqual(OfflineStoragePolicy.availableLabel(bytes: 900 * 1024 * 1024), "900 MB disponíveis")
        XCTAssertEqual(OfflineStoragePolicy.availableLabel(bytes: gib + 900 * 1024 * 1024), "1,8 GB disponíveis")
        XCTAssertEqual(OfflineStoragePolicy.availableLabel(bytes: -1), "Espaço indisponível")
        XCTAssertEqual(OfflineStoragePolicy.availableLabel(bytes: nil), "Espaço indisponível")
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

    func testBookSearchResultDecodesAndroidContract() throws {
        let data = #"{"Items":[{"Id":"book-1","Name":"O Hobbit","Type":"Book"}],"TotalRecordCount":1}"#.data(using: .utf8)!
        let result = try JSONDecoder().decode(ItemQueryResult.self, from: data)
        XCTAssertEqual(result.items.first?.id, "book-1")
        XCTAssertEqual(result.items.first?.name, "O Hobbit")
        XCTAssertEqual(result.items.first?.type, "Book")
        XCTAssertEqual(result.totalRecordCount, 1)
    }

    func testMediaSuggestionDecodesAndroidContract() throws {
        let data = #"[{"Title":"A Agência","MediaType":"Series","Year":2024}]"#.data(using: .utf8)!
        XCTAssertEqual(
            try JSONDecoder().decode([MediaSuggestion].self, from: data),
            [MediaSuggestion(title: "A Agência", mediaType: "Series", year: 2024)]
        )
    }

    func testMediaSuggestionPolicyMatchesAndroidDebounceContract() {
        XCTAssertEqual(MediaSuggestionPolicy.normalizedQuery("  Agên "), "Agên")
        XCTAssertFalse(MediaSuggestionPolicy.shouldQuery("a"))
        XCTAssertTrue(MediaSuggestionPolicy.shouldQuery("Ag"))
        XCTAssertEqual(MediaSuggestionPolicy.resultLimit, 10)
        XCTAssertEqual(MediaSuggestionPolicy.debounceNanoseconds, 250_000_000)
    }

    func testMediaRequestPolicyAcceptsOptionalYearAndBoundaries() {
        XCTAssertTrue(MediaRequestPolicy.isValidYearInput(""))
        XCTAssertTrue(MediaRequestPolicy.isValidYearInput("  "))
        XCTAssertEqual(MediaRequestPolicy.normalizedYear("1888"), 1888)
        XCTAssertEqual(MediaRequestPolicy.normalizedYear(" 2200 "), 2200)
    }

    func testMediaRequestPolicyRejectsInvalidYears() {
        XCTAssertFalse(MediaRequestPolicy.isValidYearInput("1887"))
        XCTAssertFalse(MediaRequestPolicy.isValidYearInput("2201"))
        XCTAssertFalse(MediaRequestPolicy.isValidYearInput("abc"))
        XCTAssertNil(MediaRequestPolicy.normalizedYear("2201"))
    }

    func testSearchHistoryStoreIsolatedByServerAndUser() {
        let suite = UserDefaults(suiteName: "SearchHistoryStoreTests")!
        suite.removePersistentDomain(forName: "SearchHistoryStoreTests")
        defer { suite.removePersistentDomain(forName: "SearchHistoryStoreTests") }

        SearchHistoryStore.save(["filme A"], ownerKey: "https://one.example|user-1", defaults: suite)
        SearchHistoryStore.save(["filme B"], ownerKey: "https://one.example|user-2", defaults: suite)

        XCTAssertEqual(SearchHistoryStore.load(ownerKey: "https://one.example|user-1", defaults: suite), ["filme A"])
        XCTAssertEqual(SearchHistoryStore.load(ownerKey: "https://one.example|user-2", defaults: suite), ["filme B"])
        XCTAssertEqual(SearchHistoryStore.load(ownerKey: "https://two.example|user-1", defaults: suite), [])

        SearchHistoryStore.clear(ownerKey: "https://one.example|user-1", defaults: suite)
        XCTAssertEqual(SearchHistoryStore.load(ownerKey: "https://one.example|user-1", defaults: suite), [])
        XCTAssertEqual(SearchHistoryStore.load(ownerKey: "https://one.example|user-2", defaults: suite), ["filme B"])
    }

    func testExternalSubtitleMetadataAndStreamPathMatchServerContract() throws {
        let data = #"{"Index":12,"Type":"Subtitle","Codec":"srt","Language":"por","DisplayLanguage":"Português","DisplayTitle":"Português","Title":"PT-BR","IsDefault":false,"IsForced":true,"IsExternal":true,"DeliveryUrl":"/Items/movie/Subtitles/12/Stream.srt"}"#.data(using: .utf8)!
        let stream = try JSONDecoder().decode(MediaStream.self, from: data)
        XCTAssertTrue(ExternalSubtitlePolicy.isPlayable(stream))
        XCTAssertEqual(ExternalSubtitlePolicy.mimeType(codec: stream.codec, deliveryURL: stream.deliveryURL), "application/x-subrip")
        XCTAssertTrue(stream.isForced)
        XCTAssertEqual(stream.displayTitle, "Português")
        XCTAssertEqual(
            APIClient.subtitleStreamPath(itemID: "movie/1", streamIndex: 12, mediaSourceID: "source&1"),
            "Items/movie%2F1/Subtitles/12/Stream?MediaSourceId=source%261"
        )
    }

    func testExternalSubtitlePolicyRejectsUnsupportedOrInvalidStreams() throws {
        let data = #"{"Index":-1,"Type":"Subtitle","Codec":"image/png","IsExternal":true}"#.data(using: .utf8)!
        let stream = try JSONDecoder().decode(MediaStream.self, from: data)
        XCTAssertFalse(ExternalSubtitlePolicy.isPlayable(stream))
        XCTAssertNil(ExternalSubtitlePolicy.mimeType(codec: "unknown", deliveryURL: "/subtitle.bin"))

        let nonSubtitle = #"{"Index":3,"Type":"Video","Codec":"vtt","IsExternal":true,"DeliveryUrl":"/stream.vtt"}"#.data(using: .utf8)!
        XCTAssertFalse(ExternalSubtitlePolicy.isPlayable(try JSONDecoder().decode(MediaStream.self, from: nonSubtitle)))
    }

    func testExternalSubtitleDeliveryFormatOverridesSourceCodec() {
        XCTAssertEqual(
            ExternalSubtitlePolicy.mimeType(codec: "srt", deliveryURL: "/subtitle/12/Stream.vtt"),
            "text/vtt"
        )
        XCTAssertEqual(
            ExternalSubtitlePolicy.mimeType(codec: "vtt", deliveryURL: "/subtitle/12/Stream.srt"),
            "application/x-subrip"
        )
        XCTAssertEqual(
            ExternalSubtitlePolicy.mimeType(codec: "srt", deliveryURL: "/subtitle/12/Stream.bin"),
            "application/x-subrip"
        )
    }

    func testExternalSubtitleParserSupportsWebVTTAndSRTTimestamps() {
        let data = """
        WEBVTT

        00:00:01.000 --> 00:00:03.500
        Primeira linha
        Segunda linha

        """.data(using: .utf8)!
        let cues = ExternalSubtitlePolicy.parse(data, mimeType: "text/vtt")
        XCTAssertEqual(cues, [SubtitleCue(start: 1, end: 3.5, text: "Primeira linha\nSegunda linha")])

        let srt = "1\n00:00:04,000 --> 00:00:05,250\nOlá\n".data(using: .utf8)!
        XCTAssertEqual(ExternalSubtitlePolicy.parse(srt, mimeType: "application/x-subrip").first, SubtitleCue(start: 4, end: 5.25, text: "Olá"))
    }

    func testSearchHintsPathCarriesActiveTypeFilterAndEscapesIdentity() {
        XCTAssertEqual(
            APIClient.searchHintsPath(userID: "user/1", term: "ação &", limit: 8, includeItemTypes: "Movie,Series"),
            "Search/Hints?SearchTerm=ação%20%26&UserId=user%2F1&Limit=8&IncludeItemTypes=Movie%2CSeries"
        )
        XCTAssertEqual(
            APIClient.searchHintsPath(userID: "user-1", term: "book", includeItemTypes: nil),
            "Search/Hints?SearchTerm=book&UserId=user-1&Limit=8"
        )
    }

    func testSubtitleAppearancePolicyClampsPersistedFontSize() {
        XCTAssertEqual(SubtitleAppearancePolicy.normalizedFontSize(8), 14)
        XCTAssertEqual(SubtitleAppearancePolicy.normalizedFontSize(20), 20)
        XCTAssertEqual(SubtitleAppearancePolicy.normalizedFontSize(60), 36)
    }

    func testLibraryLetterIndexNormalizesAccentsAndGroupsUnknownNames() {
        let items = [
            MediaItem(id: "1", name: "Álbum"),
            MediaItem(id: "2", name: "Aventura"),
            MediaItem(id: "3", name: "Épico"),
            MediaItem(id: "4", name: "123 Filme"),
            MediaItem(id: "5", name: "Zênite")
        ]
        XCTAssertEqual(
            LibraryLetterIndexPolicy.targets(items: items),
            [
                LibraryLetterTarget(letter: "A", itemID: "1"),
                LibraryLetterTarget(letter: "E", itemID: "3"),
                LibraryLetterTarget(letter: "#", itemID: "4"),
                LibraryLetterTarget(letter: "Z", itemID: "5")
            ]
        )
    }

    func testMediaSuggestionsPathEscapesQueryAndKeepsLimit() async throws {
        let client = APIClient(serverURL: try XCTUnwrap(URL(string: "https://example.test")))
        let url = await client.url(forPath: APIClient.mediaSuggestionsPath(query: "agência & ação", limit: 10))
        XCTAssertEqual(url.path, "/UserFeedback/MediaSuggestions")
        XCTAssertEqual(url.query, "query=agência%20%26%20ação&limit=10")
    }

    func testRemotePlaybackSessionDecodesAndroidSessionContract() throws {
        let data = #"{"Id":"session-1","DeviceId":"tv-1","DeviceName":"TV","Client":"Android TV","NowPlayingItem":{"Name":"Filme","RunTimeTicks":3600000000},"PlayState":{"IsPaused":false,"CanSeek":true,"PositionTicks":150000000}}"#.data(using: .utf8)!
        let session = try JSONDecoder().decode(RemotePlaybackSession.self, from: data)
        XCTAssertEqual(session.id, "session-1")
        XCTAssertEqual(session.deviceID, "tv-1")
        XCTAssertEqual(session.itemName, "Filme")
        XCTAssertEqual(session.durationTicks, 3_600_000_000)
        XCTAssertTrue(session.canSeek)

        let idleData = #"{"Id":"idle-1","DeviceName":"TV","NowPlayingItem":null,"PlayState":{"IsPaused":true}}"#.data(using: .utf8)!
        let idleSession = try JSONDecoder().decode(RemotePlaybackSession.self, from: idleData)
        XCTAssertEqual(idleSession.itemName, "Reproduzindo mídia")
        XCTAssertTrue(idleSession.isPaused)
    }

    func testRemotePlaybackPathsMatchAndroidContract() {
        XCTAssertEqual(
            APIClient.remotePlaybackSessionsPath(userID: "user/1", activeWithinSeconds: 300),
            "Sessions?controllableByUserId=user%2F1&activeWithinSeconds=300"
        )
        XCTAssertEqual(
            APIClient.remotePlaybackCommandPath(sessionID: "tv&1", command: .playPause, userID: "user-1"),
            "Sessions/tv%261/Playing/PlayPause?controllingUserId=user-1"
        )
        XCTAssertEqual(
            APIClient.remotePlaybackCommandPath(sessionID: "tv-1", command: .seek, userID: "user-1", seekPositionTicks: 150000000),
            "Sessions/tv-1/Playing/Seek?controllingUserId=user-1&seekPositionTicks=150000000"
        )
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
        XCTAssertEqual(SearchFilter.books.includeItemTypes, "Book")
        XCTAssertEqual(SearchFilter.people.includeItemTypes, "Person")
        XCTAssertEqual(SearchFilter.allCases.map(\.title), ["Tudo", "Filmes", "Séries", "Episódios", "Músicas", "Livros", "Pessoas"])
    }

    func testMediaPlaceholderPolicyKeepsTypeSpecificSymbols() {
        XCTAssertEqual(MediaPlaceholderPolicy.symbol(for: "Book"), "book.closed")
        XCTAssertEqual(MediaPlaceholderPolicy.symbol(for: "AudioBook"), "book.closed")
        XCTAssertEqual(MediaPlaceholderPolicy.symbol(for: "Audio"), "music.note")
        XCTAssertEqual(MediaPlaceholderPolicy.symbol(for: "Person"), "person")
        XCTAssertEqual(MediaPlaceholderPolicy.symbol(for: "Movie"), "film")
        XCTAssertEqual(MediaPlaceholderPolicy.symbol(for: nil), "film")
    }

    func testPlaybackIssueQueueOnlyRetriesTransportFailures() {
        XCTAssertTrue(PlaybackIssueQueuePolicy.shouldQueue(URLError(.notConnectedToInternet)))
        XCTAssertFalse(PlaybackIssueQueuePolicy.shouldQueue(APIError.httpStatus(401)))
        XCTAssertFalse(PlaybackIssueQueuePolicy.shouldQueue(APIError.serverMessage("Conta bloqueada")))
    }

    func testPlaybackIssueQueueNormalizesDescriptionAndEnforcesLimit() {
        let description = "  " + String(repeating: "x", count: 1_200) + "  "
        XCTAssertEqual(PlaybackIssueQueuePolicy.normalizedDescription(description).count, 1_000)
        XCTAssertEqual(PlaybackIssueQueuePolicy.normalizedDescription("  rede caiu  "), "rede caiu")
        XCTAssertTrue(PlaybackIssueQueuePolicy.canEnqueue(currentCount: 49))
        XCTAssertFalse(PlaybackIssueQueuePolicy.canEnqueue(currentCount: 50))
    }

    func testPlaybackIssueQueuePersistsEntriesPerOwner() {
        let suiteName = "MulletaFlixCoreTests.feedbackQueue.\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suiteName)!
        defer { defaults.removePersistentDomain(forName: suiteName) }
        let entry = QueuedPlaybackIssue(itemID: "movie-1", category: "Travamentos", description: "A rede caiu")

        PlaybackIssueQueueStore.save([entry], ownerKey: "https://server.example|user-1", defaults: defaults)

        XCTAssertEqual(
            PlaybackIssueQueueStore.load(ownerKey: "https://server.example|user-1", defaults: defaults),
            [entry]
        )
        XCTAssertTrue(
            PlaybackIssueQueueStore.load(ownerKey: "https://server.example|user-2", defaults: defaults).isEmpty
        )
    }

    func testPlaybackIssueQueueReportsCorruptedPayloadWithoutTreatingItAsEmpty() {
        let suiteName = "MulletaFlixCoreTests.feedbackQueueCorrupt.\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suiteName)!
        defer { defaults.removePersistentDomain(forName: suiteName) }
        let key = "feedback.playbackIssues.\(OfflineDownloadScope.directoryName(ownerKey: "invalid"))"
        defaults.set(Data("{broken".utf8), forKey: key)

        let result = PlaybackIssueQueueStore.loadResult(ownerKey: "invalid", defaults: defaults)

        XCTAssertTrue(result.entries.isEmpty)
        XCTAssertTrue(result.isCorrupted)
        XCTAssertEqual(defaults.data(forKey: key), Data("{broken".utf8))
    }

    func testHomeSnapshotPersistsCardsByOwnerAndSupportsPartialReplacement() throws {
        let suiteName = "MulletaFlixCoreTests.homeSnapshot.\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suiteName)!
        defer { defaults.removePersistentDomain(forName: suiteName) }
        let resume = MediaItem(
            id: "resume-1",
            name: "Continuar",
            type: "Movie",
            imageTags: ["Primary": "tag-1"],
            playbackPositionTicks: 42,
            isPlayed: false
        )
        let favorite = MediaItem(id: "favorite-1", name: "Favorito", type: "Series", isFavorite: true)
        let owner = "https://server.example|user-1"

        HomeSnapshotStore.save(
            resumeItems: [resume],
            favoriteItems: [favorite],
            ownerKey: owner,
            nowEpochMillis: 123,
            defaults: defaults
        )
        HomeSnapshotStore.save(
            resumeItems: [],
            favoriteItems: nil,
            ownerKey: owner,
            nowEpochMillis: 456,
            defaults: defaults
        )

        let snapshot = try XCTUnwrap(HomeSnapshotStore.load(ownerKey: owner, defaults: defaults))
        XCTAssertEqual(snapshot.resumeItems.map(\.mediaItem.id), [])
        XCTAssertEqual(snapshot.favoriteItems.map(\.mediaItem.id), ["favorite-1"])
        XCTAssertEqual(snapshot.resumeSavedAtEpochMillis, 456)
        XCTAssertEqual(snapshot.favoritesSavedAtEpochMillis, 123)
        let persisted = try XCTUnwrap(defaults.data(forKey: "home.snapshot.v1.\(OfflineDownloadScope.directoryName(ownerKey: owner))"))
        let persistedText = String(decoding: persisted, as: UTF8.self)
        XCTAssertFalse(persistedText.contains("MediaSources"))
        XCTAssertFalse(persistedText.contains("accessToken"))
        XCTAssertNil(HomeSnapshotStore.load(ownerKey: "https://server.example|user-2", defaults: defaults))
    }

    func testHomeSnapshotUsesOnlyTransientNetworkFailures() {
        XCTAssertTrue(HomeSnapshotPolicy.shouldUseCache(for: URLError(.notConnectedToInternet)))
        XCTAssertTrue(HomeSnapshotPolicy.shouldUseCache(for: URLError(.timedOut)))
        XCTAssertFalse(HomeSnapshotPolicy.shouldUseCache(for: APIError.httpStatus(401)))
        XCTAssertFalse(HomeSnapshotPolicy.shouldUseCache(for: APIError.serverMessage("Conta bloqueada")))
    }

    func testMediaItemPreservesEpisodeContextForOfflineMetadata() throws {
        let data = #"{"Id":"episode-1","Name":"Episódio","Type":"Episode","SeriesId":"series-1","SeriesName":"Minha série","SeasonId":"season-1","SeasonName":"Temporada 1","IndexNumber":3,"ParentIndexNumber":1}"#.data(using: .utf8)!
        let item = try JSONDecoder().decode(MediaItem.self, from: data)

        XCTAssertEqual(item.seriesId, "series-1")
        XCTAssertEqual(item.seriesName, "Minha série")
        XCTAssertEqual(item.seasonName, "Temporada 1")
        XCTAssertEqual(item.parentIndexNumber, 1)
        XCTAssertEqual(item.indexNumber, 3)
    }

    func testRemotePlaybackPolicyClampsSeekAndHidesUnknownProgress() {
        XCTAssertEqual(
            RemotePlaybackPolicy.seekPosition(currentTicks: 950_000_000, deltaTicks: 300_000_000, durationTicks: 1_000_000_000),
            1_000_000_000
        )
        XCTAssertEqual(
            RemotePlaybackPolicy.seekPosition(currentTicks: -10, deltaTicks: 300, durationTicks: nil),
            290
        )
        XCTAssertEqual(RemotePlaybackPolicy.seekPosition(currentTicks: Int64.max, deltaTicks: 1, durationTicks: nil), Int64.max)
        XCTAssertEqual(RemotePlaybackPolicy.progress(positionTicks: 500, durationTicks: 1_000), 0.5)
        XCTAssertNil(RemotePlaybackPolicy.progress(positionTicks: 500, durationTicks: nil))
        XCTAssertNil(RemotePlaybackPolicy.progress(positionTicks: 500, durationTicks: 0))
    }

    func testRemotePlaybackRefreshPolicySkipsOnlyBackgroundOverlap() {
        XCTAssertTrue(RemotePlaybackRefreshPolicy.shouldStartBackgroundRefresh(isLoading: false))
        XCTAssertFalse(RemotePlaybackRefreshPolicy.shouldStartBackgroundRefresh(isLoading: true))
        XCTAssertEqual(RemotePlaybackRefreshPolicy.intervalSeconds, 5)
    }

    func testPlaybackIssueCategoriesIncludePlayerSupportedRecoveryChoices() {
        XCTAssertEqual(PlaybackIssueCategoryPolicy.categories.count, 6)
        XCTAssertTrue(PlaybackIssueCategoryPolicy.categories.contains("Áudio/legenda"))
        XCTAssertEqual(PlaybackIssueCategoryPolicy.categories.first, "Não reproduz")
    }

    func testSyncPlayRefreshPolicySkipsOnlyBackgroundOverlap() {
        XCTAssertTrue(SyncPlayRefreshPolicy.shouldStartBackgroundRefresh(isLoading: false))
        XCTAssertFalse(SyncPlayRefreshPolicy.shouldStartBackgroundRefresh(isLoading: true))
        XCTAssertEqual(SyncPlayRefreshPolicy.intervalSeconds, 5)
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
            "MediaBrowser Token=\"token\", Client=\"MulletaFlix iOS\", Device=\"iPhone\", DeviceId=\"ios-test\", Version=\"\(AppIdentity.version)\""
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

    func testAutomaticIntroSkipPolicyRequiresPlayingSeekableIntroAndAvoidsRepeat() throws {
        let intro = try JSONDecoder().decode(
            MediaSegment.self,
            from: Data(#"{"Id":"intro","ItemId":"episode","Type":"Intro","StartTicks":10000000,"EndTicks":25000000}"#.utf8)
        )
        let outro = try JSONDecoder().decode(
            MediaSegment.self,
            from: Data(#"{"Id":"outro","ItemId":"episode","Type":"Outro","StartTicks":10000000,"EndTicks":25000000}"#.utf8)
        )

        XCTAssertEqual(
            AutomaticIntroSkipPolicy.target(
                enabled: true,
                isPlaying: true,
                isSeekable: true,
                positionSeconds: 1.5,
                segment: intro,
                pendingTargetSeconds: nil
            ),
            2.5
        )
        XCTAssertNil(AutomaticIntroSkipPolicy.target(enabled: false, isPlaying: true, isSeekable: true, positionSeconds: 1.5, segment: intro, pendingTargetSeconds: nil))
        XCTAssertNil(AutomaticIntroSkipPolicy.target(enabled: true, isPlaying: false, isSeekable: true, positionSeconds: 1.5, segment: intro, pendingTargetSeconds: nil))
        XCTAssertNil(AutomaticIntroSkipPolicy.target(enabled: true, isPlaying: true, isSeekable: false, positionSeconds: 1.5, segment: intro, pendingTargetSeconds: nil))
        XCTAssertNil(AutomaticIntroSkipPolicy.target(enabled: true, isPlaying: true, isSeekable: true, positionSeconds: 1.5, segment: outro, pendingTargetSeconds: nil))
        XCTAssertNil(AutomaticIntroSkipPolicy.target(enabled: true, isPlaying: true, isSeekable: true, positionSeconds: 1.5, segment: intro, pendingTargetSeconds: 2.5))
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

    func testServerURLPolicyAcceptsDirectAndMulletaFlixQRPayloads() throws {
        XCTAssertEqual(
            ServerURLPolicy.url(fromQRPayload: " HTTP://Media.Example.com:8096/mulletaflix/ ")?.absoluteString,
            "http://media.example.com:8096/mulletaflix"
        )
        XCTAssertEqual(
            ServerURLPolicy.url(fromQRPayload: "mulletaflix://server?url=https%3A%2F%2Fmedia.example.com%2Fmulletaflix%2F")?.absoluteString,
            "https://media.example.com/mulletaflix"
        )
    }

    func testServerURLPolicyRejectsUnsafeOrMalformedQRPayloads() {
        let invalid = [
            "javascript:alert(1)",
            "mulletaflix://server?url=not-a-url",
            "https://user:password@example.com",
            "https://example.com?token=secret",
            "https://example.com/#fragment",
            "mulletaflix://server?url=javascript%3Aalert(1)",
            ""
        ]
        XCTAssertTrue(invalid.allSatisfy { ServerURLPolicy.url(fromQRPayload: $0) == nil })
        XCTAssertNil(ServerURLPolicy.url(fromQRPayload: nil))
    }

    func testEpisodeContextDecodesSeriesSeasonAndIndex() throws {
        let data = #"{"Id":"episode-1","Name":"Piloto","SeriesId":"series-1","SeasonId":"season-1","IndexNumber":1}"#.data(using: .utf8)!
        let episode = try JSONDecoder().decode(MediaItem.self, from: data)
        XCTAssertEqual(episode.seriesId, "series-1")
        XCTAssertEqual(episode.seasonId, "season-1")
        XCTAssertEqual(episode.indexNumber, 1)
    }

    func testSeasonDownloadPolicyKeepsOnlyUniqueEpisodesAndSkipsActiveDownloads() {
        let episodes = [
            MediaItem(id: "episode-1", name: "Piloto", type: "Episode"),
            MediaItem(id: "episode-1", name: "Piloto duplicado", type: "Episode"),
            MediaItem(id: "season-1", name: "Temporada", type: "Season"),
            MediaItem(id: "episode-2", name: "Final", type: "Episode"),
        ]

        let eligible = SeasonDownloadPolicy.eligibleEpisodes(episodes)
        XCTAssertEqual(eligible.map(\.id), ["episode-1", "episode-2"])
        XCTAssertEqual(
            SeasonDownloadPolicy.pendingEpisodes(eligible, activeItemIDs: ["episode-1"]),
            [episodes[3]]
        )
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

    func testLiveTVRefreshPolicyUsesOneMinuteAndSkipsBusyLoads() {
        XCTAssertEqual(LiveTVRefreshPolicy.intervalSeconds, 60)
        XCTAssertTrue(LiveTVRefreshPolicy.shouldStartBackgroundRefresh(isLoading: false))
        XCTAssertFalse(LiveTVRefreshPolicy.shouldStartBackgroundRefresh(isLoading: true))
    }
}
