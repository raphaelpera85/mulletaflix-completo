import { appHost } from 'components/apphost';
import viewContainer from 'components/viewContainer';
import { AppFeature } from 'constants/appFeature';
import { ServerConnections } from 'lib/jellyfin-apiclient';

import toast from '../components/toast/toast';
import loading from '../components/loading/loading';
import { appRouter } from '../components/router/appRouter';
import baseAlert from '../components/alert';
import baseConfirm from '../components/confirm/confirm';
import globalize from '../lib/globalize';
import * as webSettings from '../scripts/settings/webSettings';
import datetime from '../scripts/datetime';
import { setBackdropTransparency } from '../components/backdrop/backdrop';
import DirectoryBrowser from '../components/directorybrowser/directorybrowser';
import dialogHelper from '../components/dialogHelper/dialogHelper';
import itemIdentifier from '../components/itemidentifier/itemidentifier';
import { getLocationSearch, getServerEndpoint } from './url';
import { queryClient } from './query/queryClient';
import { getClientCapabilities } from './clientCapabilities';

const SERVER_DISCOVERY_TIMEOUT_MS = 5000;

export function getCurrentUser(): unknown {
    return window.ApiClient.getCurrentUser(false);
}

// TODO: investigate url prefix support for serverAddress function
export async function serverAddress(): Promise<string | undefined> {
    const apiClient = window.ApiClient;

    if (apiClient) {
        return Promise.resolve(apiClient.serverAddress());
    }

    // Use servers specified in config.json
    const urls = await webSettings.getServers();

    if (urls.length === 0) {
        // Otherwise use computed base URL
        let url: string;
        const index = window.location.href.toLowerCase().lastIndexOf('/web');
        if (index != -1) {
            url = window.location.href.substring(0, index);
        } else {
            // fallback to location without path
            url = window.location.origin;
        }

        // Don't use bundled app URL (file:) as server URL
        if (url.startsWith('file:')) {
            return Promise.resolve(undefined);
        }

        urls.push(url);
    }

    console.debug('URL candidates:', urls);

    const promises = urls.map(async url => {
        const controller = new AbortController();
        const timeoutId = window.setTimeout(() => controller.abort(), SERVER_DISCOVERY_TIMEOUT_MS);

        try {
            const resp = await fetch(getServerEndpoint(url, '/System/Info/Public'), {
                cache: 'no-cache',
                signal: controller.signal
            });
            if (!resp.ok) {
                return;
            }

            let config: Record<string, unknown>;
            try {
                config = await resp.json();
            } catch {
                return;
            }

            return {
                url,
                config
            };
        } catch (error) {
            console.error(error);
        } finally {
            window.clearTimeout(timeoutId);
        }
    });

    return Promise.all(promises).then(responses => {
        return responses.filter(obj => obj?.config);
    }).then(configs => {
        const selection = configs.find(obj => !obj?.config?.StartupWizardCompleted) || configs[0];
        return selection?.url;
    }).catch(error => {
        console.error(error);
        return undefined;
    });
}

export function getCurrentUserId(): string | null {
    const apiClient = window.ApiClient;

    if (apiClient) {
        return apiClient.getCurrentUserId();
    }

    return null;
}

export function onServerChanged(_userId: string, _accessToken: string, apiClient: unknown): void {
    ServerConnections.setLocalApiClient(apiClient as Parameters<typeof ServerConnections.setLocalApiClient>[0]);
}

export function logout(): void {
    void ServerConnections.logout().then(function () {
        // Clear the query cache
        queryClient.clear();
        // Reset cached views
        viewContainer.reset();
        const destination = appHost.supports(AppFeature.MultiServer) ? 'selectserver' : 'login';
        return navigate(destination);
    }).catch((error: unknown) => {
        console.error('[Dashboard] failed to log out', error);
    });
}

export function getPluginUrl(name: string): string {
    return 'configurationpage?name=' + encodeURIComponent(name);
}

export function getConfigurationResourceUrl(name: string): string {
    return window.ApiClient.getUrl('web/ConfigurationPage', {
        name: name
    });
}

/**
 * Navigate to a url.
 * @param url - The url to navigate to.
 * @param preserveQueryString - A flag to indicate the current query string should be appended to the new url.
 * @returns Promise resolving to navigation result.
 */
export function navigate(url: string, preserveQueryString?: boolean): Promise<unknown> {
    if (!url) {
        throw new Error('url cannot be null or empty');
    }

    const queryString = getLocationSearch();

    if (preserveQueryString && queryString) {
        url += queryString;
    }

    return appRouter.show(url);
}

export function processPluginConfigurationUpdateResult(): void {
    loading.hide();
    toast(globalize.translate('SettingsSaved'));
}

export function processServerConfigurationUpdateResult(): void {
    loading.hide();
    toast(globalize.translate('SettingsSaved'));
}

export function processErrorResponse(response: { status: number; statusText?: string; headers?: Headers }): void {
    loading.hide();

    let status = '' + response.status;

    if (response.statusText) {
        status = response.statusText;
    }

    void baseAlert({
        title: status,
        text: response.headers?.get('X-Application-Error-Code') ?? undefined
    }).catch((error: unknown) => {
        console.error('[Dashboard] failed to show error response', error);
    });
}

export function alert(options: string | { title?: string; message?: string; callback?: () => void }): void {
    if (typeof options == 'string') {
        toast({
            text: options
        });
    } else {
        void baseAlert({
            title: options.title || globalize.translate('HeaderAlert'),
            text: options.message
        }).then(options.callback || function () { /* no-op */ }).catch((error: unknown) => {
            console.error('[Dashboard] failed to show alert', error);
        });
    }
}

export const capabilities = getClientCapabilities;

export function selectServer(): void {
    if (window.NativeShell && typeof window.NativeShell.selectServer === 'function') {
        window.NativeShell.selectServer();
    } else {
        void navigate('selectserver').catch((error: unknown) => {
            console.error('[Dashboard] failed to navigate to server selection', error);
        });
    }
}

export function hideLoadingMsg(): void {
    loading.hide();
}

export function showLoadingMsg(): void {
    loading.show();
}

export function confirm(message: string, title: string, callback: (result: boolean) => void): void {
    baseConfirm(message, title).then(function() {
        callback(true);
    }).catch(function() {
        callback(false);
    });
}

export const pageClassOn = function(eventName: string, className: string, fn: (this: HTMLElement, event: Event) => void): void {
    document.addEventListener(eventName, function (event) {
        const target = event.target as HTMLElement;

        if (target.classList.contains(className)) {
            fn.call(target, event);
        }
    });
};

export const pageIdOn = function(eventName: string, id: string, fn: (this: HTMLElement, event: Event) => void): void {
    document.addEventListener(eventName, function (event) {
        const target = event.target as HTMLElement;

        if (target.id === id) {
            fn.call(target, event);
        }
    });
};

const Dashboard = {
    alert,
    capabilities,
    confirm,
    getPluginUrl,
    getConfigurationResourceUrl,
    getCurrentUser,
    getCurrentUserId,
    hideLoadingMsg,
    logout,
    navigate,
    onServerChanged,
    processErrorResponse,
    processPluginConfigurationUpdateResult,
    processServerConfigurationUpdateResult,
    selectServer,
    serverAddress,
    showLoadingMsg,
    datetime,
    DirectoryBrowser,
    dialogHelper,
    itemIdentifier,
    setBackdropTransparency
};

// This is used in plugins and templates, so keep it defined for now.
// TODO: Remove once plugins don't need it
(window as unknown as Record<string, unknown>).Dashboard = Dashboard;

export default Dashboard;
