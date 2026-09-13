import { ItemSortBy } from '@jellyfin/sdk/lib/generated-client/models/item-sort-by';
import { MediaType } from '@jellyfin/sdk/lib/generated-client/models/media-type';
import { getLibraryApi } from '@jellyfin/sdk/lib/utils/api/library-api';

import { getItemQuery } from 'hooks/useItem';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { currentSettings as userSettings } from 'scripts/settings/userSettings';
import { ItemKind } from 'types/base/models/item-kind';
import Events from 'utils/events';
import { toApi } from 'utils/jellyfin-apiclient/compat';
import { queryClient } from 'utils/query/queryClient';

import { playbackManager } from './playback/playbackmanager';

interface ThemeMediaItem {
    Id?: string;
    MediaType?: string;
    playOptions?: {
        fullscreen: boolean;
        enableRemotePlayers: boolean;
    };
    [key: string]: unknown;
}

interface ThemeMediaResult {
    OwnerId?: string;
    Items?: ThemeMediaItem[];
    ThemeVideosResult?: ThemeMediaResult;
    ThemeSongsResult?: ThemeMediaResult;
    [key: string]: unknown;
}

interface ViewShowEvent extends Event {
    detail?: {
        params?: {
            serverId?: string;
            id?: string;
        };
        options?: {
            supportsThemeMedia?: boolean;
        };
    };
}

let currentOwnerId: string | null | undefined;
let currentThemeIds: (string | undefined)[] = [];

function playThemeMedia(items: ThemeMediaItem[], ownerId: string | null): void {
    const currentThemeItems = items.filter(function (i) {
        return enabled(i.MediaType);
    });

    if (currentThemeItems.length) {
        // Stop if a theme song from another ownerId
        // Leave it alone if anything else (e.g user playing a movie)
        if (!currentOwnerId && playbackManager.isPlaying()) {
            return;
        }

        currentThemeIds = currentThemeItems.map(function (i) {
            return i.Id;
        });

        currentThemeItems.forEach((i) => {
            i.playOptions = {
                fullscreen: false,
                enableRemotePlayers: false
            };
        });

        playbackManager.play({
            items: currentThemeItems,
            aspectRatio: 'cover',
            fullscreen: false,
            enableRemotePlayers: false
        }).then(function () {
            currentOwnerId = ownerId;
        }).catch((error: unknown) => {
            currentOwnerId = undefined;
            currentThemeIds = [];
            console.error('[ThemeMediaPlayer] failed to play theme media', error);
        });
    } else {
        stopIfPlaying();
    }
}

function stopIfPlaying(): void {
    if (currentOwnerId || currentThemeIds.length > 0) {
        playbackManager.stop();
    }

    currentOwnerId = undefined;
    currentThemeIds = [];
}

function enabled(mediaType: string | undefined): boolean {
    if (mediaType === MediaType.Video) {
        return userSettings.enableThemeVideos();
    }

    return userSettings.enableThemeSongs();
}

const excludeTypes: string[] = [
    ItemKind.CollectionFolder,
    ItemKind.UserView,
    ItemKind.Person,
    ItemKind.Program,
    ItemKind.TvChannel,
    ItemKind.Channel,
    ItemKind.SeriesTimer
];

async function loadThemeMedia(serverId: string, itemId: string): Promise<void> {
    const apiClient = ServerConnections.getApiClient(serverId);
    const api = toApi(apiClient as never);
    const userId = apiClient.getCurrentUserId();
    if (!userId) return;

    try {
        const item = await queryClient.fetchQuery(getItemQuery(
            api,
            itemId,
            userId
        )) as ThemeMediaItem & { CollectionType?: string; Type?: string; Id?: string };

        if (item.CollectionType) {
            stopIfPlaying();
            return;
        }

        if (item.Type && excludeTypes.includes(item.Type)) {
            stopIfPlaying();
            return;
        }

        const { data: themeMedia } = await getLibraryApi(api).getThemeMedia({
            userId,
            itemId: item.Id!,
            inheritFromParent: true,
            sortBy: [ItemSortBy.Random]
        });

        const result: ThemeMediaResult | undefined = (userSettings.enableThemeVideos() && themeMedia.ThemeVideosResult?.Items?.length ? themeMedia.ThemeVideosResult : themeMedia.ThemeSongsResult) as ThemeMediaResult | undefined;

        if (result && result.OwnerId !== currentOwnerId) {
            playThemeMedia(result.Items || [], result.OwnerId || null);
        }
    } catch (err) {
        console.error('[ThemeMediaPlayer] failed to load theme media', err);
    }
}

document.addEventListener('viewshow', (e: Event) => {
    const viewEvent = e as ViewShowEvent;
    const { serverId, id } = viewEvent.detail?.params || {};
    if (serverId && id) {
        void loadThemeMedia(serverId, id);
        return;
    }

    const viewOptions = viewEvent.detail?.options || {};

    if (viewOptions.supportsThemeMedia) {
        // Do nothing here, allow it to keep playing
    } else {
        playThemeMedia([], null);
    }
}, true);

Events.on(playbackManager, 'playbackstart', (_e: unknown, player: unknown) => {
    const item = playbackManager.currentItem(player) as ThemeMediaItem;
    // User played something manually
    if (currentThemeIds.indexOf(item.Id) == -1) {
        currentOwnerId = undefined;
    }
});
