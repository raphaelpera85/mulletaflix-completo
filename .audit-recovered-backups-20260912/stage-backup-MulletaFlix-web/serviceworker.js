(() => {
  function getApiClient(serverId) {
    return Promise.resolve(globalThis.connectionManager.getApiClient(serverId));
  }
  function executeAction(action, data, serverId) {
    return getApiClient(serverId).then((apiClient) => {
      const client = apiClient;
      switch (action) {
        case "cancel-install":
          return client.cancelPackageInstallation(data.id).then(() => void 0);
        case "restart":
          return client.restartServer().then(() => void 0);
        default:
          return globalThis.clients.openWindow("/").then(() => void 0);
      }
    });
  }
  const serviceWorker = globalThis;
  const SHELL_CACHE = "mulletaflix-shell-v12";
  const SHELL_CACHE_PREFIX = "mulletaflix-shell-";
  function isCacheableShellRequest(request) {
    if (request.method !== "GET") return false;
    const url = new URL(request.url);
    if (url.origin !== globalThis.location.origin) return false;
    return request.mode === "navigate" || /\.(?:css|js|json|html|woff2?|ttf|png|jpe?g|svg|ico)$/i.test(url.pathname);
  }
  async function cacheShellResponse(request, response) {
    if (response.ok) {
      const cache = await globalThis.caches.open(SHELL_CACHE);
      await cache.put(request, response.clone());
    }
    return response;
  }
  serviceWorker.addEventListener("install", (event) => {
    event.waitUntil(globalThis.caches.open(SHELL_CACHE).then((cache) => cache.add("./")));
  });
  serviceWorker.addEventListener("fetch", (event) => {
    if (!isCacheableShellRequest(event.request)) return;
    event.respondWith((async () => {
      const cache = await globalThis.caches.open(SHELL_CACHE);
      const cached = await cache.match(event.request);
      const network = fetch(event.request).then((response) => cacheShellResponse(event.request, response));
      if (event.request.mode === "navigate") {
        return network.catch(async () => cached ?? await cache.match("./") ?? new Response("Offline shell unavailable", {
          status: 503,
          statusText: "Offline shell unavailable"
        }));
      }
      return cached ?? network;
    })());
  });
  serviceWorker.addEventListener("notificationclick", (event) => {
    const notification = event.notification;
    notification.close();
    const data = notification.data;
    const serverId = data.serverId;
    const action = event.action;
    if (!action) {
      serviceWorker.clients.openWindow("/").catch(() => void 0);
      event.waitUntil(Promise.resolve());
      return;
    }
    event.waitUntil(executeAction(action, data, serverId));
  }, false);
  serviceWorker.addEventListener("activate", (event) => {
    event.waitUntil(
      globalThis.caches.keys().then((cacheNames) => Promise.all(
        cacheNames.filter((cacheName) => cacheName.startsWith(SHELL_CACHE_PREFIX) && cacheName !== SHELL_CACHE).map((cacheName) => globalThis.caches.delete(cacheName))
      )).then(() => serviceWorker.clients.claim())
    );
  });
})();
