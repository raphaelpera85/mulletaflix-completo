declare module 'jellyfin-apiclient' {
    // Legacy callers pass several structurally typed option objects without a
    // shared index signature. `object` removes the unsafe dynamic type while keeping that API
    // compatibility; endpoint-specific DTOs can replace it incrementally.
    type LegacyRequestOptions = object;
    type LegacyRecord = { [key: string]: unknown };
    type LegacyList = LegacyRecord[];
    interface LegacyCredentials {
        Servers: LegacyList;
        [key: string]: unknown;
    }
    interface ConnectionUserResult {
        localUser?: UserDto;
        name: string | null;
        imageUrl: string | null;
        supportsImageParams: boolean;
    }

    import type {
        AllThemeMediaResult,
        AuthenticationResult,
        BaseItemDto,
        BaseItemDtoQueryResult,
        BufferRequestDto,
        ClientCapabilities,
        CountryInfo,
        CultureDto,
        DeviceOptions,
        DisplayPreferencesDto,
        EndPointInfo,
        FileSystemEntryInfo,
        GeneralCommand,
        GroupInfoDto,
        GuideInfo,
        IgnoreWaitRequestDto,
        ImageInfo,
        ImageProviderInfo,
        ImageType,
        ItemCounts,
        LiveTvInfo,
        MovePlaylistItemRequestDto,
        NewGroupRequestDto,
        NextItemRequestDto,
        NotificationResultDto,
        NotificationsSummaryDto,
        ParentalRating,
        PingRequestDto,
        PlaybackInfoResponse,
        PlaybackProgressInfo,
        PlaybackStartInfo,
        PlaybackStopInfo,
        PlayCommand,
        PlaystateCommand,
        PluginInfo,
        PluginSecurityInfo,
        PreviousItemRequestDto,
        QueryFiltersLegacy,
        QueueRequestDto,
        QuickConnectResult,
        QuickConnectState,
        ReadyRequestDto,
        RecommendationDto,
        RemoteImageResult,
        RemoveFromPlaylistRequestDto,
        SearchHintResult,
        SeekRequestDto,
        SeriesTimerInfoDto,
        SeriesTimerInfoDtoQueryResult,
        ServerConfiguration,
        SessionInfo,
        SetPlaylistItemRequestDto,
        SetRepeatModeRequestDto,
        SetShuffleModeRequestDto,
        SystemInfo,
        TaskInfo,
        TaskTriggerInfo,
        TimerInfoDto,
        TimerInfoDtoQueryResult,
        UserConfiguration,
        UserDto,
        UserItemDataDto,
        UserPolicy,
        UtcTimeResponse,
        VirtualFolderInfo
    } from '@jellyfin/sdk/lib/generated-client';
    import type { ConnectionState } from 'lib/jellyfin-apiclient';

    class ApiClient {
        constructor(serverAddress: string, appName: string, appVersion: string, deviceName: string, deviceId: string);

        accessToken(): string;
        addMediaPath(virtualFolderName: string, mediaPath: string, networkSharePath: string, refreshLibrary?: boolean): Promise<void>;
        addVirtualFolder(name: string, type?: string, refreshLibrary?: boolean, libraryOptions?: LegacyRequestOptions): Promise<void>;
        ajax<T = Response>(request: LegacyRequestOptions): Promise<T>;
        appName(): string;
        appVersion(): string;
        authenticateUserByName(name: string, password: string): Promise<AuthenticationResult>;
        cancelLiveTvSeriesTimer(id: string): Promise<void>;
        cancelLiveTvTimer(id: string): Promise<void>;
        cancelSyncItems(itemIds: string[], targetId?: string): Promise<void>;
        clearAuthenticationInfo(): void;
        clearUserItemRating(userId: string, itemId: string): Promise<UserItemDataDto>;
        closeWebSocket(): void;
        createLiveTvSeriesTimer(item: string): Promise<void>;
        createLiveTvTimer(item: string): Promise<void>;
        createPackageReview(review: LegacyRequestOptions): Promise<unknown>;
        createSyncPlayGroup(options?: NewGroupRequestDto): Promise<void>;
        createUser(user: UserDto): Promise<UserDto>;
        deleteDevice(deviceId: string): Promise<void>;
        deleteItemImage(itemId: string, imageType: ImageType, imageIndex?: number): Promise<void>;
        deleteItem(itemId: string): Promise<void>;
        deleteLiveTvRecording(id: string): Promise<void>;
        deleteUserImage(userId: string, imageType: ImageType, imageIndex?: number): Promise<void>;
        deleteUser(userId: string): Promise<void>;
        detectBitrate(force: boolean): Promise<number>;
        deviceId(): string;
        deviceName(): string;
        disablePlugin(id: string, version: string): Promise<void>;
        downloadRemoteImage(options: LegacyRequestOptions): Promise<void>;
        enablePlugin(id: string, version: string): Promise<void>;
        encodeName(name: string): string;
        ensureWebSocket(): void;
        fetch(request: LegacyRequestOptions, includeAuthorization?: boolean): Promise<Response>;
        fetchWithFailover(request: LegacyRequestOptions, enableReconnection?: boolean): Promise<Response>;
        getAdditionalVideoParts(userId?: string, itemId: string): Promise<BaseItemDtoQueryResult>;
        getAlbumArtists(userId: string, options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getAncestorItems(itemId: string, userId?: string): Promise<BaseItemDto[]>;
        getArtist(name: string, userId?: string): Promise<BaseItemDto>;
        getArtists(userId: string, options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getAvailablePlugins(options?: LegacyRequestOptions): Promise<PluginInfo[]>;
        getAvailableRemoteImages(options: LegacyRequestOptions): Promise<RemoteImageResult>;
        getContentUploadHistory(): Promise<unknown>;
        getCountries(): Promise<CountryInfo[]>;
        getCriticReviews(itemId: string, options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getCultures(): Promise<CultureDto[]>;
        getCurrentUser(cache?: boolean): Promise<UserDto>;
        getCurrentUserId(): string;
        getDateParamValue(date: Date): string;
        getDefaultImageQuality(imageType: ImageType): number;
        getDevicesOptions(): Promise<DeviceOptions>;
        getDirectoryContents(path: string, options?: LegacyRequestOptions): Promise<FileSystemEntryInfo[]>;
        getDisplayPreferences(id: string, userId: string, app: string): Promise<DisplayPreferencesDto>;
        getDownloadSpeed(byteSize: number): Promise<number>;
        getDrives(): Promise<FileSystemEntryInfo[]>;
        getEndpointInfo(): Promise<EndPointInfo>;
        getEpisodes(itemId: string, options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getFilters(options?: LegacyRequestOptions): Promise<QueryFiltersLegacy>;
        getGenre(name: string, userId?: string): Promise<BaseItemDto>;
        getGenres(userId: string, options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getImageUrl(itemId: string, options?: LegacyRequestOptions): string;
        getInstalledPlugins(): Promise<PluginInfo[]>;
        getInstantMixFromItem(itemId: string, options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getIntros(itemId: string): Promise<BaseItemDtoQueryResult>;
        getItemCounts(userId?: string): Promise<ItemCounts>;
        /** @deprecated This function returns a URL with a legacy auth parameter.*/
        getItemDownloadUrl(itemId: string): string;
        getItemImageInfos(itemId: string): Promise<ImageInfo[]>;
        getItems(userId: string, options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getItem(userId: string, itemId: string): Promise<BaseItemDto>;
        getJSON<T = unknown>(url: string, includeAuthorization?: boolean): Promise<T>;
        getLatestItems(options?: LegacyRequestOptions): Promise<BaseItemDto[]>;
        getLiveStreamMediaInfo(liveStreamId: string): Promise<{ MediaStreams?: MediaStream[] }>;
        getLiveTvChannel(id: string, userId?: string): Promise<BaseItemDto>;
        getLiveTvChannels(options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getLiveTvGuideInfo(userId: string): Promise<GuideInfo>;
        getLiveTvInfo(userId: string): Promise<LiveTvInfo>;
        getLiveTvProgram(id: string, userId?: string): Promise<BaseItemDto>;
        getLiveTvPrograms(options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getLiveTvRecommendedPrograms(options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getLiveTvRecordingGroup(id: string): Promise<BaseItemDto>;
        getLiveTvRecordingGroups(options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getLiveTvRecording(id: string, userId?: string): Promise<BaseItemDto>;
        getLiveTvRecordingSeries(options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getLiveTvRecordings(options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getLiveTvSeriesTimer(id: string): Promise<SeriesTimerInfoDto>;
        getLiveTvSeriesTimers(options?: LegacyRequestOptions): Promise<SeriesTimerInfoDtoQueryResult>;
        getLiveTvTimer(id: string): Promise<TimerInfoDto>;
        getLiveTvTimers(options?: LegacyRequestOptions): Promise<TimerInfoDtoQueryResult>;
        getLocalTrailers(userId: string, itemId: string): Promise<BaseItemDto[]>;
        getMovieRecommendations(options?: LegacyRequestOptions): Promise<RecommendationDto[]>;
        getMusicGenre(name: string, userId?: string): Promise<BaseItemDto>;
        getMusicGenres(userId: string, options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getNamedConfiguration(name: string): Promise<unknown>;
        getNetworkDevices(): Promise<unknown>;
        getNewLiveTvTimerDefaults(options?: LegacyRequestOptions): Promise<SeriesTimerInfoDto>;
        getNextUpEpisodes(options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getNotificationSummary(userId: string): Promise<NotificationsSummaryDto>;
        getNotifications(userId: string, options?: LegacyRequestOptions): Promise<NotificationResultDto>;
        getPackageInfo(name: string, guid: string): Promise<PackageInfo>;
        getPackageReviews(packageId: string, minRating?: string, maxRating?: string, limit?: string): Promise<unknown>;
        getParentalRatings(): Promise<ParentalRating[]>;
        getParentPath(path: string): Promise<string>;
        getPeople(userId: string, options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getPerson(name: string, userId?: string): Promise<BaseItemDto>;
        getPhysicalPaths(): Promise<string[]>;
        getPlaybackInfo(itemId: string, options: LegacyRequestOptions, deviceProfile: LegacyRequestOptions): Promise<PlaybackInfoResponse>;
        getPluginConfiguration(id: string): Promise<unknown>;
        getPublicSystemInfo(): Promise<PublicSystemInfo>;
        getPublicUsers(): Promise<UserDto[]>;
        getQuickConnect(verb: string): Promise<void | boolean | number | QuickConnectResult | QuickConnectState>;
        getReadySyncItems(deviceId: string): Promise<unknown>;
        getRecordingFolders(userId: string): Promise<BaseItemDtoQueryResult>;
        getRegistrationInfo(feature: string): Promise<unknown>;
        getRemoteImageProviders(options: LegacyRequestOptions): Promise<ImageProviderInfo[]>;
        getResumableItems(userId: string, options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getRootFolder(userId: string): Promise<BaseItemDto>;
        getSavedEndpointInfo(): EndPointInfo;
        getScaledImageUrl(itemId: string, options?: LegacyRequestOptions): string;
        getScheduledTask(id: string): Promise<TaskInfo>;
        getScheduledTasks(options?: LegacyRequestOptions): Promise<TaskInfo[]>;
        getSearchHints(options?: LegacyRequestOptions): Promise<SearchHintResult>;
        getSeasons(itemId: string, options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getServerConfiguration(): Promise<ServerConfiguration>;
        getServerTime(): Promise<UtcTimeResponse>;
        getSessions(options?: LegacyRequestOptions): Promise<SessionInfo[]>;
        getSimilarItems(itemId: string, options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getSpecialFeatures(userId: string, itemId: string): Promise<BaseItemDto[]>;
        getStudio(name: string, userId?: string): Promise<BaseItemDto>;
        getStudios(userId: string, options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getSyncPlayGroups(): Promise<GroupInfoDto[]>;
        getSyncStatus(itemId: string): Promise<unknown>;
        getSystemInfo(): Promise<SystemInfo>;
        getThemeMedia(userId?: string, itemId: string, inherit?: boolean): Promise<AllThemeMediaResult>;
        getThumbImageUrl(item: BaseItemDto, options?: LegacyRequestOptions): string;
        getUpcomingEpisodes(options?: LegacyRequestOptions): Promise<BaseItemDtoQueryResult>;
        getUrl(name: string, params?: LegacyRequestOptions, serverAddress?: string): string;
        get(url: string): Promise<unknown>;
        getUserImageUrl(userId: string, options?: LegacyRequestOptions): string;
        getUsers(options?: LegacyRequestOptions): Promise<UserDto[]>;
        getUser(userId: string): Promise<UserDto>;
        getUserViews(options?: LegacyRequestOptions, userId: string): Promise<BaseItemDtoQueryResult>;
        getVirtualFolders(): Promise<VirtualFolderInfo[]>;
        handleMessageReceived(msg: LegacyRequestOptions): void;
        installPlugin(name: string, guid: string, version?: string): Promise<void>;
        isLoggedIn(): boolean;
        isMessageChannelOpen(): boolean;
        isMinServerVersion(version: string): boolean;
        isWebSocketOpen(): boolean;
        isWebSocketOpenOrConnecting(): boolean;
        isWebSocketSupported(): boolean;
        joinSyncPlayGroup(options?: LegacyRequestOptions): Promise<void>;
        leaveSyncPlayGroup(): Promise<void>;
        logout(): Promise<void>;
        markNotificationsRead(userId: string, idList: string[], isRead: boolean): Promise<void>;
        markPlayed(userId: string, itemId: string, date: Date): Promise<UserItemDataDto>;
        markUnplayed(userId: string, itemId: string, date: Date): Promise<UserItemDataDto>;
        openWebSocket(): void;
        quickConnect(secret: string): Promise<AuthenticationResult>;
        refreshItem(itemId: string, options?: LegacyRequestOptions): Promise<void>;
        removeMediaPath(virtualFolderName: string, mediaPath: string, refreshLibrary?: boolean): Promise<void>;
        removeVirtualFolder(name: string, refreshLibrary?: boolean): Promise<void>;
        renameVirtualFolder(name: string, newName: string, refreshLibrary?: boolean): Promise<void>;
        reportCapabilities(capabilities: ClientCapabilities): Promise<void>;
        reportOfflineActions(actions: LegacyRequestOptions): Promise<unknown>;
        reportPlaybackProgress(options: PlaybackProgressInfo): Promise<void>;
        reportPlaybackStart(options: PlaybackStartInfo): Promise<void>;
        reportPlaybackStopped(options: PlaybackStopInfo): Promise<void>;
        reportSyncJobItemTransferred(syncJobItemId: string): Promise<unknown>;
        requestSyncPlayBuffering(options?: BufferRequestDto): Promise<void>;
        requestSyncPlayMovePlaylistItem(options?: MovePlaylistItemRequestDto): Promise<void>;
        requestSyncPlayNextItem(options?: NextItemRequestDto): Promise<void>;
        requestSyncPlayPause(): Promise<void>;
        requestSyncPlayPreviousItem(options?: PreviousItemRequestDto): Promise<void>;
        requestSyncPlayQueue(options?: QueueRequestDto): Promise<void>;
        requestSyncPlayReady(options?: ReadyRequestDto): Promise<void>;
        requestSyncPlayRemoveFromPlaylist(options?: RemoveFromPlaylistRequestDto): Promise<void>;
        requestSyncPlaySeek(options?: SeekRequestDto): Promise<void>;
        requestSyncPlaySetIgnoreWait(options?: IgnoreWaitRequestDto): Promise<void>;
        requestSyncPlaySetNewQueue(options?: NewGroupRequestDto): Promise<void>;
        requestSyncPlaySetPlaylistItem(options?: SetPlaylistItemRequestDto): Promise<void>;
        requestSyncPlaySetRepeatMode(options?: SetRepeatModeRequestDto): Promise<void>;
        requestSyncPlaySetShuffleMode(options?: SetShuffleModeRequestDto): Promise<void>;
        requestSyncPlayUnpause(): Promise<void>;
        resetLiveTvTuner(id: string): Promise<void>;
        resetUserPassword(userId: string): Promise<void>;
        restartServer(): Promise<void>;
        sendCommand(sessionId: string, command: LegacyRequestOptions): Promise<void>;
        sendMessageCommand(sessionId: string, options: GeneralCommand): Promise<void>;
        sendMessage(name: string, data: LegacyRequestOptions): void;
        sendPlayCommand(sessionId: string, options: PlayCommand): Promise<void>;
        sendPlayStateCommand(sessionId: string, command: PlaystateCommand, options?: LegacyRequestOptions): Promise<void>;
        sendSyncPlayPing(options?: PingRequestDto): Promise<void>;
        sendWebSocketMessage(name: string, data: LegacyRequestOptions): void;
        serverAddress(val?: string): string;
        serverId(): string;
        serverVersion(): string;
        setAuthenticationInfo(accessKey?: string, userId?: string): void;
        setRequestHeaders(headers: LegacyRequestOptions): void;
        setSystemInfo(info: SystemInfo): void;
        shutdownServer(): Promise<void>;
        startScheduledTask(id: string): Promise<void>;
        stopActiveEncodings(playSessionId: string): Promise<void>;
        stopScheduledTask(id: string): Promise<void>;
        syncData(data: LegacyRequestOptions): Promise<unknown>;
        uninstallPluginByVersion(id: string, version: string): Promise<void>;
        uninstallPlugin(id: string): Promise<void>;
        updateDisplayPreferences(id: string, obj: DisplayPreferencesDto, userId: string, app: string): Promise<void>;
        updateFavoriteStatus(userId: string, itemId: string, isFavorite: boolean): Promise<UserItemDataDto>;
        updateItemImageIndex(itemId: string, imageType: ImageType, imageIndex: number, newIndex: number): Promise<unknown>;
        updateItem(item: BaseItemDto): Promise<void>;
        updateLiveTvSeriesTimer(item: SeriesTimerInfoDto): Promise<void>;
        updateLiveTvTimer(item: TimerInfoDto): Promise<void>;
        updateMediaPath(virtualFolderName: string, pathInfo: LegacyRequestOptions): Promise<void>;
        updateNamedConfiguration(name: string, configuration: LegacyRequestOptions): Promise<void>;
        updatePluginConfiguration(id: string, configuration: LegacyRequestOptions): Promise<void>;
        updatePluginSecurityInfo(info: PluginSecurityInfo): Promise<void>;
        updateScheduledTaskTriggers(id: string, triggers: TaskTriggerInfo[]): Promise<void>;
        updateServerConfiguration(configuration: ServerConfiguration): Promise<void>;
        updateServerInfo(server: LegacyRequestOptions, serverUrl: string): void;
        updateUserConfiguration(userId: string, configuration: UserConfiguration): Promise<void>;
        updateUserItemRating(userId: string, itemId: string, likes: boolean): Promise<UserItemDataDto>;
        updateUserPassword(userId: string, currentPassword: string, newPassword: string): Promise<void>;
        updateUserPolicy(userId: string, policy: UserPolicy): Promise<void>;
        updateUser(user: UserDto): Promise<void>;
        updateVirtualFolderOptions(id: string, libraryOptions?: LegacyRequestOptions): Promise<void>;
        uploadItemImage(itemId: string, imageType: ImageType, file: File): Promise<void>;
        uploadItemSubtitle(itemId: string, language: string, isForced: boolean, file: File): Promise<void>;
        uploadUserImage(userId: string, imageType: ImageType, file: File): Promise<void>;
    }

    class AppStore {
        constructor();

        getItem(name: string): string | null;
        removeItem(name: string): void;
        setItem(name: string, value: string): void;
    }

    interface ConnectResponse {
        ApiClient: ApiClient
        Servers: LegacyList
        State: ConnectionState
    }

    class ConnectionManager {
        constructor(credentialProvider: Credentials, appName: string, appVersion: string, deviceName: string, deviceId: string, capabilities: ClientCapabilities);

        addApiClient(apiClient: ApiClient): void;
        clearData(): void;
        connect(options?: LegacyRequestOptions): Promise<ConnectResponse>;
        connectToAddress(address: string, options?: LegacyRequestOptions): Promise<ConnectResponse>;
        connectToServer(server: LegacyRequestOptions, options?: LegacyRequestOptions): Promise<ConnectResponse>;
        connectToServers(servers: LegacyList, options?: LegacyRequestOptions): Promise<ConnectResponse>;
        deleteServer(serverId: string): Promise<void>;
        getApiClient(item: BaseItemDto | string): ApiClient;
        getApiClients(): ApiClient[];
        getAvailableServers(): LegacyList;
        getOrCreateApiClient(serverId: string): ApiClient;
        getSavedServers(): LegacyList;
        handleMessageReceived(msg: LegacyRequestOptions): void;
        logout(): Promise<void>;
        minServerVersion(val?: string): string;
        updateSavedServerId(server: LegacyRequestOptions): Promise<void>;
        user(apiClient: ApiClient): Promise<ConnectionUserResult>;
    }

    class Credentials {
        constructor(key?: string);

        addOrUpdateServer(list: LegacyList, server: LegacyRequestOptions): void;
        clear(): void;
        credentials(data?: LegacyCredentials): LegacyCredentials;
    }

    interface Event {
        type: string;
    }

    const Events: {
        off(obj: object, eventName: string, fn: (e: Event, ...args: unknown[]) => void): void;
        on(obj: object, eventName: string, fn: (e: Event, ...args: unknown[]) => void): void;
        trigger(obj: object, eventName: string, ...args: unknown[]): void;
    };
}
