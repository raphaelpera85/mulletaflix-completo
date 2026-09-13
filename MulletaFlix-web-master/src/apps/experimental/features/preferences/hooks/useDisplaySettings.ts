import type { Api } from '@jellyfin/sdk';
import type { UserDto } from '@jellyfin/sdk/lib/generated-client';
import { getUserApi } from '@jellyfin/sdk/lib/utils/api/user-api';
import { ApiClient } from 'jellyfin-apiclient';
import { useCallback, useEffect, useState } from 'react';

import { appHost } from 'components/apphost';
import layoutManager from 'components/layoutManager';
import { AppFeature } from 'constants/appFeature';
import { useBrandingTheme } from 'hooks/useBrandingTheme';
import { useApi } from 'hooks/useApi';
import { FALLBACK_THEME_ID } from 'hooks/useUserTheme';
import themeManager from 'scripts/themeManager';
import { currentSettings, UserSettings } from 'scripts/settings/userSettings';

import type { DisplaySettingsValues } from '../types/displaySettingsValues';

interface UseDisplaySettingsParams {
    userId?: string | null;
}

export function useDisplaySettings({ userId }: UseDisplaySettingsParams) {
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<Error | null>(null);
    const [reloadToken, setReloadToken] = useState(0);
    const [userSettings, setUserSettings] = useState<UserSettings>();
    const [displaySettings, setDisplaySettings] = useState<DisplaySettingsValues>();
    const { __legacyApiClient__, api, user: currentUser } = useApi();
    const { defaultThemeId } = useBrandingTheme();

    useEffect(() => {
        if (!currentUser || !api || !__legacyApiClient__) {
            setLoading(false);
            setError(new Error('Display settings dependencies are not available.'));
            return;
        }

        setLoading(true);
        setError(null);
        let isActive = true;

        void (async () => {
            try {
                const loadedSettings = await loadDisplaySettings({
                    api,
                    legacyApiClient: __legacyApiClient__,
                    currentUser,
                    userId,
                    defaultThemeId
                });

                if (!isActive) {
                    return;
                }

                setDisplaySettings(loadedSettings.displaySettings);
                setUserSettings(loadedSettings.userSettings);
                setLoading(false);
            } catch (loadError) {
                if (!isActive) {
                    return;
                }

                console.error('[DisplaySettings] failed to load preferences', loadError);
                setError(loadError instanceof Error ? loadError : new Error('Failed to load display settings.'));
                setLoading(false);
            }
        })();

        return () => {
            isActive = false;
        };
    }, [api, __legacyApiClient__, currentUser, defaultThemeId, reloadToken, userId]);

    const retry = useCallback(() => {
        setReloadToken((value) => value + 1);
    }, []);

    const saveSettings = useCallback(async (newSettings: DisplaySettingsValues) => {
        if (!userId || !userSettings || !api) {
            return;
        }
        return saveDisplaySettings({
            api,
            newDisplaySettings: newSettings,
            userSettings,
            userId
        });
    }, [api, userSettings, userId]);

    return {
        displaySettings,
        error,
        loading,
        retry,
        saveDisplaySettings: saveSettings
    };
}

interface LoadDisplaySettingsParams {
    currentUser: UserDto
    userId?: string | null
    api: Api
    legacyApiClient: ApiClient
    defaultThemeId?: string
}

async function loadDisplaySettings({
    currentUser,
    userId,
    api,
    legacyApiClient,
    defaultThemeId
}: LoadDisplaySettingsParams) {
    const settings = (!userId || userId === currentUser?.Id) ? currentSettings : new UserSettings();
    const user = (!userId || userId === currentUser?.Id) ?
        currentUser :
        (await getUserApi(api).getUserById({ userId })).data;

    await settings.setUserInfo(userId ?? undefined, legacyApiClient);

    const displaySettings = {
        customCss: settings.customCss() || '',
        dashboardTheme: settings.dashboardTheme() || defaultThemeId || FALLBACK_THEME_ID,
        dateTimeLocale: settings.dateTimeLocale() || 'auto',
        disableCustomCss: Boolean(settings.disableCustomCss()),
        displayMissingEpisodes: user?.Configuration?.DisplayMissingEpisodes ?? false,
        enableBlurHash: Boolean(settings.enableBlurhash()),
        enableFasterAnimation: Boolean(settings.enableFastFadein()),
        enableItemDetailsBanner: Boolean(settings.detailsBanner()),
        enableLibraryBackdrops: Boolean(settings.enableBackdrops()),
        enableLibraryThemeSongs: Boolean(settings.enableThemeSongs()),
        enableLibraryThemeVideos: Boolean(settings.enableThemeVideos()),
        enableRewatchingInNextUp: Boolean(settings.enableRewatchingInNextUp()),
        episodeImagesInNextUp: Boolean(settings.useEpisodeImagesInNextUpAndResume()),
        language: settings.language() || 'auto',
        layout: layoutManager.getSavedLayout() || 'auto',
        libraryPageSize: settings.libraryPageSize(),
        maxDaysForNextUp: settings.maxDaysForNextUp(),
        screensaver: settings.screensaver() || 'none',
        screensaverInterval: settings.backdropScreensaverInterval(),
        slideshowInterval: settings.slideshowInterval(),
        theme: settings.theme() || defaultThemeId || FALLBACK_THEME_ID
    };

    return {
        displaySettings,
        userSettings: settings
    };
}

interface SaveDisplaySettingsParams {
    api: Api;
    newDisplaySettings: DisplaySettingsValues
    userSettings: UserSettings;
    userId: string;
}

async function saveDisplaySettings({
    api,
    newDisplaySettings,
    userSettings,
    userId
}: SaveDisplaySettingsParams) {
    const user = await getUserApi(api).getUserById({ userId }).then(response => response.data);

    if (appHost.supports(AppFeature.DisplayLanguage)) {
        userSettings.language(normalizeValue(newDisplaySettings.language));
    }
    userSettings.customCss(normalizeValue(newDisplaySettings.customCss));
    userSettings.dashboardTheme(newDisplaySettings.dashboardTheme);
    userSettings.dateTimeLocale(normalizeValue(newDisplaySettings.dateTimeLocale));
    userSettings.disableCustomCss(newDisplaySettings.disableCustomCss);
    userSettings.enableBlurhash(newDisplaySettings.enableBlurHash);
    userSettings.enableFastFadein(newDisplaySettings.enableFasterAnimation);
    userSettings.detailsBanner(newDisplaySettings.enableItemDetailsBanner);
    userSettings.enableBackdrops(newDisplaySettings.enableLibraryBackdrops);
    userSettings.enableThemeSongs(newDisplaySettings.enableLibraryThemeSongs);
    userSettings.enableThemeVideos(newDisplaySettings.enableLibraryThemeVideos);
    userSettings.enableRewatchingInNextUp(newDisplaySettings.enableRewatchingInNextUp);
    userSettings.useEpisodeImagesInNextUpAndResume(newDisplaySettings.episodeImagesInNextUp);
    userSettings.libraryPageSize(newDisplaySettings.libraryPageSize);
    userSettings.maxDaysForNextUp(newDisplaySettings.maxDaysForNextUp);
    userSettings.screensaver(normalizeValue(newDisplaySettings.screensaver));
    userSettings.backdropScreensaverInterval(newDisplaySettings.screensaverInterval);
    userSettings.theme(newDisplaySettings.theme);

    layoutManager.setLayout(normalizeValue(newDisplaySettings.layout));

    const promises = [
        themeManager.setTheme(userSettings.theme() ?? '')
    ];

    if (user.Id && user.Configuration) {
        user.Configuration.DisplayMissingEpisodes = newDisplaySettings.displayMissingEpisodes;
        promises.push(getUserApi(api).updateUserConfiguration({
            userId: user.Id,
            userConfiguration: user.Configuration
        }).then(() => undefined));
    }

    try {
        await Promise.all(promises);
    } catch (saveError) {
        console.error('[DisplaySettings] failed to save preferences', saveError);
        throw saveError;
    }
}

function normalizeValue(value: string) {
    return /^(auto|none)$/.test(value) ? '' : value;
}
