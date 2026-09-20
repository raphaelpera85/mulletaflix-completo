interface ServiceWorkerNotificationData {
    serverId?: string;
    id?: string;
}

type ServiceWorkerAction = 'cancel-install' | 'restart';

interface ServiceWorkerScope {
    addEventListener(type: 'notificationclick', listener: (event: ServiceWorkerNotificationClickEvent) => void, options?: boolean): void;
    addEventListener(type: 'activate', listener: (event: ServiceWorkerLifecycleEvent) => void, options?: boolean): void;
    addEventListener(type: 'install', listener: (event: ServiceWorkerLifecycleEvent) => void, options?: boolean): void;
    addEventListener(type: 'fetch', listener: (event: ServiceWorkerFetchEvent) => void, options?: boolean): void;
    clients: {
        openWindow(url: string): Promise<unknown | null>;
        claim(): Promise<void>;
    };
}

interface ServiceWorkerLifecycleEvent {
    waitUntil(promise: Promise<unknown>): void;
}

interface ServiceWorkerFetchEvent {
    request: Request;
    respondWith(promise: Promise<Response>): void;
}

interface ServiceWorkerNotificationClickEvent {
    notification: Notification & { data?: ServiceWorkerNotificationData };
    action: string;
    waitUntil(promise: Promise<unknown>): void;
}

function getApiClient(serverId: string | undefined): Promise<unknown> {
    return Promise.resolve((globalThis as unknown as { connectionManager: { getApiClient(serverId?: string): unknown } }).connectionManager.getApiClient(serverId));
}

function executeAction(action: ServiceWorkerAction, data: ServiceWorkerNotificationData, serverId: string | undefined): Promise<void> {
    return getApiClient(serverId).then((apiClient) => {
        const client = apiClient as {
            cancelPackageInstallation(id: string | undefined): Promise<unknown>;
            restartServer(): Promise<unknown>;
        };

        switch (action) {
            case 'cancel-install':
                return client.cancelPackageInstallation(data.id).then(() => undefined);
            case 'restart':
                return client.restartServer().then(() => undefined);
            default:
                return (globalThis as unknown as ServiceWorkerScope).clients.openWindow('/').then(() => undefined);
        }
    });
}

const serviceWorker = globalThis as unknown as ServiceWorkerScope;
const SHELL_CACHE = 'mulletaflix-shell-v13';
const SHELL_CACHE_PREFIX = 'mulletaflix-shell-';

function isCacheableShellRequest(request: Request): boolean {
    if (request.method !== 'GET') return false;

    const url = new URL(request.url);
    if (url.origin !== globalThis.location.origin) return false;

    return request.mode === 'navigate'
        || /\.(?:css|js|json|html|woff2?|ttf|png|jpe?g|svg|ico)$/i.test(url.pathname);
}

async function cacheShellResponse(request: Request, response: Response): Promise<Response> {
    if (response.ok) {
        const cache = await globalThis.caches.open(SHELL_CACHE);
        await cache.put(request, response.clone());
    }

    return response;
}

serviceWorker.addEventListener('install', (event: ServiceWorkerLifecycleEvent) => {
    event.waitUntil(globalThis.caches.open(SHELL_CACHE).then(cache => cache.add('./')));
});

serviceWorker.addEventListener('fetch', (event: ServiceWorkerFetchEvent) => {
    if (!isCacheableShellRequest(event.request)) return;

    event.respondWith((async () => {
        const cache = await globalThis.caches.open(SHELL_CACHE);
        const cached = await cache.match(event.request);
        const network = fetch(event.request)
            .then(response => cacheShellResponse(event.request, response));

        // Navigations prefer fresh HTML but fall back to the cached shell offline.
        if (event.request.mode === 'navigate') {
            return network.catch(async () => cached ?? await cache.match('./') ?? new Response('Offline shell unavailable', {
                status: 503,
                statusText: 'Offline shell unavailable'
            }));
        }

        // Static assets use cache-first to keep repeat loads fast, while the
        // network refreshes them on the next versioned request.
        return cached ?? network;
    })());
});

serviceWorker.addEventListener('notificationclick', (event: ServiceWorkerNotificationClickEvent) => {
    const notification = event.notification;
    notification.close();

    const data = notification.data as ServiceWorkerNotificationData;
    const serverId = data.serverId;
    const action = event.action as ServiceWorkerAction | '';

    if (!action) {
        serviceWorker.clients.openWindow('/').catch(() => undefined);
        event.waitUntil(Promise.resolve());
        return;
    }

    event.waitUntil(executeAction(action, data, serverId));
}, false);

serviceWorker.addEventListener('activate', (event: ServiceWorkerLifecycleEvent) => {
    event.waitUntil(
        globalThis.caches.keys()
            .then(cacheNames => Promise.all(
                cacheNames
                    .filter(cacheName => cacheName.startsWith(SHELL_CACHE_PREFIX) && cacheName !== SHELL_CACHE)
                    .map(cacheName => globalThis.caches.delete(cacheName))
            ))
            .then(() => serviceWorker.clients.claim())
    );
});
