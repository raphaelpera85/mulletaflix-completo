import { playbackManager } from 'components/playback/playbackmanager';
import { pluginManager } from 'components/pluginManager';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { PluginType } from 'types/plugin';
import Events from 'utils/events';

import inputManager from './inputManager';
import * as userSettings from './settings/userSettings';

import './screensavermanager.scss';

interface ScreensaverPlugin {
    id: string;
    name: string;
    hideOnClick?: boolean;
    hideOnMouse?: boolean;
    hideOnKey?: boolean;
    show(): void;
    hide(): Promise<void>;
}

interface ScreensaverManagerApi {
    isShowing(): boolean;
    show(): void;
    hide(): void;
}

interface PlaybackStopInfo {
    state?: {
        NowPlayingItem?: {
            MediaType?: string;
        };
    };
}

function getMinIdleTime(): number {
    // Returns the minimum amount of idle time required before the screensaver can be displayed
    //time units used Millisecond
    return userSettings.screensaverTime() * 1000;
}

let lastFunctionalEvent = 0;

function getFunctionalEventIdleTime(): number {
    return new Date().getTime() - lastFunctionalEvent;
}

Events.on(playbackManager, 'playbackstop', function (_e: unknown, stopInfo: unknown) {
    if (!stopInfo || typeof stopInfo !== 'object') return;

    const state = (stopInfo as PlaybackStopInfo).state;
    if (state?.NowPlayingItem?.MediaType === 'Video') {
        lastFunctionalEvent = new Date().getTime();
    }
});

function getScreensaverPlugin(isLoggedIn: boolean | undefined): ScreensaverPlugin | null {
    let option: string | undefined;
    try {
        option = userSettings.get('screensaver', false) as string | undefined;
    } catch {
        option = isLoggedIn ? 'backdropscreensaver' : 'logoscreensaver';
    }

    const plugins = pluginManager.ofType(PluginType.Screensaver);

    for (const plugin of plugins) {
        if (plugin.id === option) {
            return plugin as unknown as ScreensaverPlugin;
        }
    }

    return null;
}

function ScreenSaverManager(this: ScreensaverManagerApi): void {
    let activeScreenSaver: ScreensaverPlugin | null;

    function showScreenSaver(screensaver: ScreensaverPlugin): void {
        if (activeScreenSaver) {
            throw new Error('An existing screensaver is already active.');
        }

        console.debug('Showing screensaver ' + screensaver.name);

        document.body.classList.add('screensaver-noScroll');

        screensaver.show();
        activeScreenSaver = screensaver;

        if (screensaver.hideOnClick !== false) {
            window.addEventListener('click', hide, true);
        }
        if (screensaver.hideOnMouse !== false) {
            window.addEventListener('mousemove', hide, true);
        }
        if (screensaver.hideOnKey !== false) {
            window.addEventListener('keydown', hide, true);
        }
    }

    function hide(): void {
        if (activeScreenSaver) {
            console.debug('Hiding screensaver');
            activeScreenSaver.hide().then(() => {
                document.body.classList.remove('screensaver-noScroll');
            }).catch(() => undefined);
            activeScreenSaver = null;
        }

        window.removeEventListener('click', hide, true);
        window.removeEventListener('mousemove', hide, true);
        window.removeEventListener('keydown', hide, true);
    }

    this.isShowing = (): boolean => {
        return activeScreenSaver != null;
    };

    this.show = function (): void {
        let isLoggedIn: boolean | undefined;
        const apiClient = ServerConnections.currentApiClient();

        const client = apiClient as unknown as { isLoggedIn?: () => boolean } | undefined;
        if (client?.isLoggedIn?.()) {
            isLoggedIn = true;
        }

        const screensaver = getScreensaverPlugin(isLoggedIn);

        if (screensaver) {
            showScreenSaver(screensaver);
        }
    };

    this.hide = function (): void {
        hide();
    };

    const onInterval = (): void => {
        if (this.isShowing()) {
            return;
        }

        if (inputManager.idleTime() < getMinIdleTime()) {
            return;
        }

        if (getFunctionalEventIdleTime() < getMinIdleTime()) {
            return;
        }

        if (playbackManager.isPlayingVideo() && !playbackManager.paused()) {
            return;
        }

        this.show();
    };

    setInterval(onInterval, 5000);
}

const screensaverManager = {} as ScreensaverManagerApi;
ScreenSaverManager.call(screensaverManager);

export default screensaverManager;
