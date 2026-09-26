import { clearBackdrop, setBackdropImages, setBackdrops } from '../components/backdrop/backdrop';
import * as userSettings from './settings/userSettings';
import libraryMenu from './libraryMenu';
import { pageClassOn } from '../utils/dashboard';
import { queryClient } from 'utils/query/queryClient';
import { getBrandingOptionsQuery } from 'apps/dashboard/features/branding/api/useBrandingOptions';
import { SPLASHSCREEN_URL } from 'constants/branding';
import { ServerConnections } from 'lib/jellyfin-apiclient';

// F-10: cache was a plain module-level object living in memory for the
// lifetime of the module. Two problems: (1) it survives across totally
// different library/parent contexts within the same SPA session without any
// eviction (the `key` already encodes userId+types+parentId, but a session
// that browses many libraries just keeps accumulating entries forever), and
// (2) a page reload always starts cold, refetching backdrops shown seconds
// earlier. sessionStorage keeps the same per-tab lifetime (cleared when the
// tab closes) while surviving SPA navigation like the previous module object,
// and gives an explicit, inspectable cache instead of an ever-growing object.
const CACHE_PREFIX = 'autoBackdrops-';

function readCache(key: string): string | null {
    try {
        return sessionStorage.getItem(CACHE_PREFIX + key);
    } catch (e) {
        // sessionStorage can throw in private-browsing/quota-exceeded scenarios
        console.debug('[autoBackdrops] sessionStorage read failed', e);
        return null;
    }
}

function writeCache(key: string, value: string): void {
    try {
        sessionStorage.setItem(CACHE_PREFIX + key, value);
    } catch (e) {
        console.debug('[autoBackdrops] sessionStorage write failed', e);
    }
}

function enabled(): boolean {
    return userSettings.enableBackdrops();
}

interface BackdropImage {
    Id: string;
    tag: string;
    ServerId: string;
    BackdropImageTags?: string[];
}

interface BackdropItemOptions {
    SortBy: string;
    Limit: number;
    Recursive: boolean;
    IncludeItemTypes: string | null | undefined;
    ImageTypes: string;
    ParentId: string | null | undefined;
    EnableTotalRecordCount: boolean;
    MaxOfficialRating: string;
}

function getBackdropItemIds(apiClient: any, userId: string, types: string | null | undefined, parentId: string | undefined): Promise<BackdropImage[]> {
    const key = `backdrops2_${userId + (types || '') + (parentId || '')}`;
    const data = readCache(key);

    if (data) {
        console.debug(`Found backdrop id list in cache. Key: ${key}`);
        return Promise.resolve(JSON.parse(data) as BackdropImage[]);
    }

    const options: BackdropItemOptions = {
        SortBy: 'IsFavoriteOrLiked,Random',
        Limit: 20,
        Recursive: true,
        IncludeItemTypes: types,
        ImageTypes: 'Backdrop',
        ParentId: parentId,
        EnableTotalRecordCount: false,
        MaxOfficialRating: parentId ? '' : 'PG-13'
    };
    return apiClient.getItems(apiClient.getCurrentUserId(), options).then(function (result: any) {
        const images: BackdropImage[] = result.Items.map(function (i: any) {
            return {
                Id: i.Id,
                tag: i.BackdropImageTags[0],
                ServerId: i.ServerId
            };
        });
        writeCache(key, JSON.stringify(images));
        return images;
    });
}

function showBackdrop(type: string | null | undefined, parentId: string | undefined): void {
    const apiClient = ServerConnections.currentApiClient();

    if (apiClient) {
        getBackdropItemIds(apiClient, (apiClient as any).getCurrentUserId(), type, parentId).then(function (images: BackdropImage[]) {
            if (images.length) {
                setBackdrops(images.map(function (i: BackdropImage) {
                    i.BackdropImageTags = [i.tag];
                    return i;
                }));
            } else {
                clearBackdrop();
            }
        }).catch(() => clearBackdrop());
    }
}

async function showSplashScreen(): Promise<void> {
    const api = ServerConnections.getCurrentApi();
    if (!api) return;
    const brandingOptions = await queryClient.fetchQuery(getBrandingOptionsQuery(api));
    if (brandingOptions.SplashscreenEnabled) {
        setBackdropImages([
            api.getUri(SPLASHSCREEN_URL, { t: Date.now() })
        ]);
    } else {
        clearBackdrop();
    }
}

pageClassOn('pageshow', 'page', function (this: HTMLElement) {
    const page = this;

    if (!page.classList.contains('selfBackdropPage')) {
        if (page.classList.contains('backdropPage')) {
            const type = page.getAttribute('data-backdroptype');
            if (type === 'splashscreen') {
                showSplashScreen().catch(() => clearBackdrop());
            } else if (enabled()) {
                const parentId = page.classList.contains('globalBackdropPage') ? '' : libraryMenu.getTopParentId();
                showBackdrop(type ?? undefined, parentId ?? undefined);
            } else {
                page.classList.remove('backdropPage');
                clearBackdrop();
            }
        } else {
            clearBackdrop();
        }
    }
});
