import { Api, Jellyfin } from '@jellyfin/sdk';
import { ApiClient } from 'jellyfin-apiclient';

import { safeDecodeURIComponent } from 'utils/url';

/**
 * Returns an SDK Api instance using the same parameters as the provided ApiClient.
 * @param {ApiClient} apiClient The (legacy) ApiClient.
 * @returns {Api} An equivalent SDK Api instance.
 */
export const toApi = (apiClient: ApiClient): Api => {
    const serverUrl = apiClient.serverAddress() || 'http://localhost';
    const api = (new Jellyfin({
        // The SDK encodes these values when creating the authorization header,
        // so we need to decode them here to avoid double encoding.
        clientInfo: {
            name: safeDecodeURIComponent(apiClient.appName()),
            version: safeDecodeURIComponent(apiClient.appVersion())
        },
        deviceInfo: {
            name: safeDecodeURIComponent(apiClient.deviceName()),
            id: safeDecodeURIComponent(apiClient.deviceId())
        }
    })).createApi(
        serverUrl,
        apiClient.accessToken()
    );

    api.axiosInstance.interceptors.request.use((config) => {
        if (!config.headers['Authorization'] && api.authorizationHeader) {
            config.headers['Authorization'] = api.authorizationHeader;
        }
        return config;
    });

    return api;
};

