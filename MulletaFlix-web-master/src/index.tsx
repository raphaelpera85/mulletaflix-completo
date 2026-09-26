// Import legacy browser polyfills
import 'lib/legacy';

import React from 'react';
import { createRoot } from 'react-dom/client';

// NOTE: We need to import this first to initialize the connection
import { ServerConnections } from 'lib/jellyfin-apiclient';

import { appHost } from './components/apphost';
import autoFocuser from './components/autoFocuser';
import loading from 'components/loading/loading';
import { pluginManager } from './components/pluginManager';
import { AppFeature } from 'constants/appFeature';
import globalize from './lib/globalize';
import { loadCoreDictionary } from 'lib/globalize/loader';
import browser from './scripts/browser';
import keyboardNavigation from './scripts/keyboardNavigation';
import { getPlugins } from './scripts/settings/webSettings';
import taskButton from './scripts/taskbutton';
import { pageClassOn, serverAddress } from './utils/bootstrap';
import Events, { type Event as JellyfinEvent } from './utils/events';
import { initializeWebVitals } from './utils/webVitals';

import RootApp from './RootApp';

// Import the button webcomponent for use throughout the site
// NOTE: This is a bit of a hack, files should ensure the component is imported before use
import './elements/emby-button/emby-button';
import './lib/legacy/patchCreateElement';

// Import site styles
import './styles/site.scss';
import './styles/livetv.scss';
import './styles/dashboard.scss';
import './styles/detailtable.scss';
import './styles/librarybrowser.scss';

const BOOTSTRAP_TIMEOUT_MS = 10000;

function withBootstrapTimeout<T>(promise: Promise<T>, label: string, timeout = BOOTSTRAP_TIMEOUT_MS): Promise<T | undefined> {
    let timeoutId: number | undefined;

    const timeoutPromise = new Promise<undefined>((resolve) => {
        timeoutId = window.setTimeout(() => {
            console.warn(`[bootstrap] ${label} excedeu ${timeout}ms; continuando sem bloquear a interface.`);
            resolve(undefined);
        }, timeout);
    });

    return Promise.race([
        Promise.resolve(promise).catch((error: unknown) => {
            console.warn(`[bootstrap] ${label} falhou; continuando sem bloquear a interface.`, error);
            return undefined;
        }),
        timeoutPromise
    ]).finally(() => {
        if (timeoutId !== undefined) window.clearTimeout(timeoutId);
    });
}

async function init(): Promise<void> {
    initializeWebVitals();

    console.info(
        `[${__PACKAGE_JSON_NAME__}]
version: ${__PACKAGE_JSON_VERSION__}
commit: ${__COMMIT_SHA__}
build: ${__JF_BUILD_VERSION__}`);

    window.Events = Events;
    (window as Window & { TaskButton?: typeof taskButton }).TaskButton = taskButton;

    pageClassOn('viewshow', 'standalonePage', function () {
        document.querySelector('.skinHeader')?.classList.add('noHeaderRight');
    });
    pageClassOn('viewhide', 'standalonePage', function () {
        document.querySelector('.skinHeader')?.classList.remove('noHeaderRight');
    });

    await withBootstrapTimeout(appHost.init(), 'inicialização do host');

    const serverUrl = await withBootstrapTimeout(serverAddress(), 'descoberta do servidor');
    if (serverUrl) ServerConnections.initApiClient(serverUrl);

    // F-1: since F-3 (RootAppRouter.tsx) stopped statically importing the
    // dashboard/experimental/stable/wizard route trees (they are now loaded
    // via patchRoutesOnNavigation), the RootAppRouter chunk no longer drags
    // the whole app graph with it. That was the reason the earlier attempt at
    // this same optimization was reverted (it raced ~200 chunks against the
    // critical path). Now the chunk is light, so it is started in parallel
    // with loadCoreDictionary instead of after loadPlugins.
    const routerModulePromise = withBootstrapTimeout(import('./RootAppRouter'), 'inicialização do roteador');

    await withBootstrapTimeout(loadCoreDictionary(), 'dicionário principal');
    Events.on(ServerConnections, 'localusersignedin', globalize.updateCurrentCulture);
    Events.on(ServerConnections, 'localusersignedout', globalize.updateCurrentCulture);

    loadFonts();

    if (browser.iOS) void import('./styles/ios.scss');

    await withBootstrapTimeout(loadPlugins(), 'carregamento dos plugins');
    // RootAppRouter initializes the compatibility history consumed by the
    // legacy AppRouter. Await it before React render and deferred globals so
    // the first bootstrap cannot construct AppRouter with an undefined history.
    await routerModulePromise;
    await renderApp();

    void loadDeferredGlobalFeatures();
    loadPlatformFeatures();
    keyboardNavigation.enable();
    autoFocuser.enable();
}

async function loadDeferredGlobalFeatures(): Promise<void> {
    const modules = await Promise.allSettled([
        import('./components/playback/displayMirrorManager'),
        import('./components/themeMediaPlayer'),
        import('./scripts/autoThemes'),
        import('./scripts/mouseManager'),
        import('./scripts/screensavermanager'),
        import('./scripts/autocast').then(({ initialize }) => initialize()),
        import('./scripts/serverNotifications').then(({ initializeServerConnections }) => initializeServerConnections()),
        import('./components/router/appRouter').then(({ appRouter }) => {
            const onRequestFail = appRouter.onRequestFail as unknown as (event: JellyfinEvent, data: { status: number; errorCode?: string }) => void;
            ServerConnections.getApiClients().forEach(apiClient => {
                Events.off(apiClient, 'requestfail', onRequestFail);
                Events.on(apiClient, 'requestfail', onRequestFail);
            });
            Events.on(ServerConnections, 'apiclientcreated', (_e: unknown, apiClient: unknown) => {
                Events.off(apiClient, 'requestfail', onRequestFail);
                Events.on(apiClient, 'requestfail', onRequestFail);
            });
        })
    ]);

    const failures = modules.filter(result => result.status === 'rejected');
    if (failures.length) {
        console.warn(`[bootstrap] falha ao carregar ${failures.length} recurso(s) global(is) adiado(s)`, failures);
    }
}

function loadFonts(): void {
    if (browser.tv && !browser.android) {
        console.debug('using system fonts with explicit sizes');
        void import('./styles/fonts.sized.scss');
    } else if (__USE_SYSTEM_FONTS__) {
        console.debug('using system fonts');
        void import('./styles/fonts.scss');
    } else {
        console.debug('using default fonts');
        void import('./styles/fonts.scss');
        void import('./styles/fonts.noto-base.scss');
        loadCjkFonts();
    }
}

function loadCjkFonts(): void {
    const language = (navigator.language || '').toLowerCase();

    if (language.startsWith('ja')) {
        void import('./styles/fonts.noto-jp.scss');
    } else if (language.startsWith('ko')) {
        void import('./styles/fonts.noto-kr.scss');
    } else if (language.startsWith('zh-cn') || language.startsWith('zh-sg')) {
        void import('./styles/fonts.noto-sc.scss');
    } else if (language.startsWith('zh-tw') || language.startsWith('zh-mo')) {
        void import('./styles/fonts.noto-tc.scss');
    } else if (language.startsWith('zh-hk')) {
        void import('./styles/fonts.noto-hk.scss');
    }
}

async function loadPlugins(): Promise<void> {
    console.groupCollapsed('loading installed plugins');
    console.dir(pluginManager);

    let list = await withBootstrapTimeout(getPlugins(), 'configuração de plugins') || [];
    if (!appHost.supports(AppFeature.RemoteControl)) {
        list = list.filter(plugin => !plugin.startsWith('sessionPlayer') && !plugin.startsWith('chromecastPlayer'));
    } else if (!browser.chrome && !browser.edgeChromium && !browser.opera) {
        list = list.filter(plugin => !plugin.startsWith('chromecastPlayer'));
    }

    if (window.NativeShell) list = list.concat(window.NativeShell.getPlugins());

    try {
        const results = await Promise.allSettled(list.map(plugin =>
            withBootstrapTimeout(pluginManager.loadPlugin(plugin), `plugin ${plugin}`)
        ));
        const failures = results.filter(result => result.status === 'rejected');
        if (failures.length) {
            console.warn(`falha ao carregar ${failures.length} plugin(s); a interface continuará disponível`, failures);
        }
        console.debug('finished loading plugins');
    } catch (error: unknown) {
        console.warn('failed loading plugins', error);
    }

    console.groupEnd();
}

function loadPlatformFeatures(): void {
    if (!browser.tv && !browser.xboxOne && !browser.ps4) void import('./components/nowPlayingBar/nowPlayingBar');
    if (appHost.supports(AppFeature.RemoteControl)) {
        void import('./components/playback/playerSelectionMenu');
        void import('./components/playback/remotecontrolautoplay');
    }
    if (!appHost.supports(AppFeature.PhysicalVolumeControl) || browser.touch) void import('./components/playback/volumeosd');
    if (!browser.tv && !browser.xboxOne) {
        void import('./components/playback/playbackorientation');
        registerServiceWorker();
        if (window.Notification) void import('./components/notifications/notifications');
    }
}

function registerServiceWorker(): void {
    const appMode = (window as Window & { appMode?: string }).appMode;
    if (navigator.serviceWorker && appMode !== 'cordova' && appMode !== 'android') {
        navigator.serviceWorker.register('serviceworker.js').then(() =>
            console.debug('serviceWorker registered')
        ).catch((error: unknown) =>
            console.warn('error registering serviceWorker: ' + error)
        );
    } else {
        console.debug('serviceWorker unsupported');
    }
}

async function renderApp(): Promise<void> {
    const container = document.getElementById('reactRoot');
    if (!container) throw new Error('React root container was not found');
    container.innerHTML = '';

    loading.show();

    const root = createRoot(container);
    root.render(<RootApp />);
}

void init();
