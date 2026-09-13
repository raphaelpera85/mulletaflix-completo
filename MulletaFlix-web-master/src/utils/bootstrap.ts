import { getServerEndpoint } from './url';

const SERVER_DISCOVERY_TIMEOUT_MS = 5000;

export const pageClassOn = function(eventName: string, className: string, fn: (this: HTMLElement, event: Event) => void): void {
    document.addEventListener(eventName, function (event) {
        const target = event.target as HTMLElement;

        if (target.classList.contains(className)) {
            fn.call(target, event);
        }
    });
};

/** Discover a configured Jellyfin server without importing the legacy dashboard facade. */
export async function serverAddress(): Promise<string | undefined> {
    const apiClient = window.ApiClient;

    if (apiClient) {
        return apiClient.serverAddress();
    }

    const { getServers } = await import('../scripts/settings/webSettings');
    const urls = await getServers();

    if (urls.length === 0) {
        let url: string;
        const index = window.location.href.toLowerCase().lastIndexOf('/web');
        if (index !== -1) {
            url = window.location.href.substring(0, index);
        } else {
            url = window.location.origin;
        }

        if (url.startsWith('file:')) return undefined;
        urls.push(url);
    }

    console.debug('URL candidates:', urls);

    const responses = await Promise.all(urls.map(async url => {
        const controller = new AbortController();
        const timeoutId = window.setTimeout(() => controller.abort(), SERVER_DISCOVERY_TIMEOUT_MS);

        try {
            const response = await fetch(getServerEndpoint(url, '/System/Info/Public'), {
                cache: 'no-cache',
                signal: controller.signal
            });
            if (!response.ok) return undefined;

            let config: Record<string, unknown>;
            try {
                config = await response.json() as Record<string, unknown>;
            } catch {
                return undefined;
            }

            return { url, config };
        } catch (error) {
            console.error(error);
            return undefined;
        } finally {
            window.clearTimeout(timeoutId);
        }
    }));

    const configs = responses.filter((response): response is { url: string; config: Record<string, unknown> } => Boolean(response?.config));
    const selection = configs.find(({ config }) => !config.StartupWizardCompleted) || configs[0];
    return selection?.url;
}
