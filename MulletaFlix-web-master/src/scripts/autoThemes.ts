import * as userSettings from './settings/userSettings';
import skinManager from './themeManager';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { pageClassOn } from 'utils/dashboard';
import Events from 'utils/events';
import { queryClient } from 'utils/query/queryClient';
import { getBrandingOptionsQuery } from 'apps/dashboard/features/branding/api/useBrandingOptions';
import { getDefaultTheme } from './settings/webSettings';

/**
 * The branding default theme is a server setting, not a per-page value, and it does not change
 * during a session. It used to be re-resolved on every page navigation: `viewbeforeshow` calls
 * applyTheme for each page, and the underlying branding query declares no stale time of its own, so
 * once the global window elapsed every navigation refetched it. Caching the promise here keeps that
 * down to a single lookup per page load.
 */
let brandingDefaultThemePromise: Promise<string | undefined> | undefined;

function getBrandingDefaultThemeId(): Promise<string | undefined> {
    // Only cache once there is an API client to resolve against. Before sign-in there is nothing to
    // ask, and caching that would pin the fallback theme for the rest of the session — the branding
    // default would then never be applied to the user who signs in afterwards.
    if (!ServerConnections.getCurrentApi()) {
        return Promise.resolve(undefined);
    }

    brandingDefaultThemePromise ??= loadBrandingDefaultThemeId();
    return brandingDefaultThemePromise;
}

async function loadBrandingDefaultThemeId(): Promise<string | undefined> {
    const api = ServerConnections.getCurrentApi();

    if (!api) {
        return undefined;
    }

    try {
        const brandingOptions = await queryClient.fetchQuery(getBrandingOptionsQuery(api));
        const options = brandingOptions as unknown as { DefaultTheme?: string };
        return options.DefaultTheme || undefined;
    } catch (error) {
        console.warn('[autoThemes] failed to load branding default theme', error);
        return undefined;
    }
}

async function resolveThemeId(themeId: string | null): Promise<string> {
    if (themeId) {
        return themeId;
    }

    const brandingThemeId = await getBrandingDefaultThemeId();
    return brandingThemeId || getDefaultTheme().id;
}

async function applyTheme(themeId: string | null): Promise<void> {
    await skinManager.setTheme(await resolveThemeId(themeId));
}

// Set the default theme when loading
applyTheme(userSettings.theme())
    /* this keeps the scrollbar always present in all pages, so we avoid clipping while switching between pages
       that need the scrollbar and pages that don't.
     */
    .then(() => document.body.classList.add('force-scroll'))
    .catch(() => undefined);

// set the saved theme once a user authenticates
Events.on(ServerConnections, 'localusersignedin', () => {
    void applyTheme(userSettings.theme());
});

pageClassOn('viewbeforeshow', 'page', function (this: HTMLElement) {
    if (this.classList.contains('type-interior')) {
        void applyTheme(userSettings.dashboardTheme());
    } else {
        void applyTheme(userSettings.theme());
    }
});
