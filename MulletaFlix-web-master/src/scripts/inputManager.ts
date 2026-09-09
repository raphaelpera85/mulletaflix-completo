import { appHost } from 'components/apphost';
import focusManager from 'components/focusManager';
import { playbackManager } from 'components/playback/playbackmanager';
import { appRouter } from 'components/router/appRouter';
import { AppFeature } from 'constants/appFeature';
import dom from 'utils/dom';

let lastInputTime = new Date().getTime();

export function notify(): void {
    lastInputTime = new Date().getTime();
    handleCommand('unknown');
}

export function notifyMouseMove(): void {
    lastInputTime = new Date().getTime();
}

export function idleTime(): number {
    return new Date().getTime() - lastInputTime;
}

export function select(sourceElement: Element | Window): void {
    (sourceElement as HTMLElement).click();
}

let eventListenerCount = 0;
export function on(scope: EventTarget, fn: EventListenerOrEventListenerObject): void {
    eventListenerCount++;
    dom.addEventListener(scope, 'command', fn, {});
}

export function off(scope: EventTarget, fn: EventListenerOrEventListenerObject): void {
    if (eventListenerCount) {
        eventListenerCount--;
    }

    dom.removeEventListener(scope, 'command', fn, {});
}

const commandTimes: Record<string, number> = {};

function checkCommandTime(command: string): boolean {
    const last = commandTimes[command] || 0;
    const now = new Date().getTime();

    if ((now - last) < 1000) {
        return false;
    }

    commandTimes[command] = now;
    return true;
}

export interface HandleCommandOptions {
    sourceElement?: Element | null;
    [key: string]: unknown;
}

export function handleCommand(commandName: string, options?: HandleCommandOptions): void {
    lastInputTime = new Date().getTime();

    let sourceElement: Element | Window = (options ? options.sourceElement : null) as Element | null as Element | Window;

    if (sourceElement) {
        sourceElement = focusManager.focusableParent(sourceElement as Element);
    }

    if (!sourceElement) {
        sourceElement = document.activeElement || window;

        const dialogs = document.querySelectorAll('.dialogContainer .dialog.opened');

        // Suppose the top open dialog is active
        const dlg: Element | null = dialogs.length ? dialogs[dialogs.length - 1] as Element : null;

        if (dlg && !dlg.contains(sourceElement as Element)) {
            sourceElement = dlg;
        }
    }

    if (eventListenerCount) {
        const customEvent = new CustomEvent('command', {
            detail: {
                command: commandName
            },
            bubbles: true,
            cancelable: true
        });

        const eventResult = (sourceElement as Element).dispatchEvent(customEvent);
        if (!eventResult) {
            // event cancelled
            return;
        }
    }

    const keyActions = (command: string): (() => void) | undefined => ({
        'up': () => {
            focusManager.moveUp(sourceElement);
        },
        'down': () => {
            focusManager.moveDown(sourceElement);
        },
        'left': () => {
            focusManager.moveLeft(sourceElement);
        },
        'right': () => {
            focusManager.moveRight(sourceElement);
        },
        'home': () => {
            void appRouter.goHome().catch((error: unknown) => console.error('Failed to navigate home', error));
        },
        'settings': () => {
            void appRouter.showSettings().catch((error: unknown) => console.error('Failed to open settings', error));
        },
        'back': () => {
            if (appRouter.canGoBack()) {
                void appRouter.back().catch((error: unknown) => console.error('Failed to navigate back', error));
            } else if (appHost.supports(AppFeature.Exit)) {
                appHost.exit();
            }
        },
        'select': () => {
            select(sourceElement);
        },
        'nextchapter': () => {
            playbackManager.nextChapter();
        },
        'next': () => {
            playbackManager.nextTrack();
        },
        'nexttrack': () => {
            playbackManager.nextTrack();
        },
        'previous': () => {
            playbackManager.previousTrack();
        },
        'previoustrack': () => {
            playbackManager.previousTrack();
        },
        'previouschapter': () => {
            playbackManager.previousChapter();
        },
        'guide': () => {
            void appRouter.showGuide().catch((error: unknown) => console.error('Failed to open guide', error));
        },
        'recordedtv': () => {
            void appRouter.showRecordedTV().catch((error: unknown) => console.error('Failed to open recordings', error));
        },
        'livetv': () => {
            void appRouter.showLiveTV().catch((error: unknown) => console.error('Failed to open live TV', error));
        },
        'mute': () => {
            playbackManager.setMute(true);
        },
        'unmute': () => {
            playbackManager.setMute(false);
        },
        'togglemute': () => {
            playbackManager.toggleMute();
        },
        'channelup': () => {
            playbackManager.channelUp();
        },
        'channeldown': () => {
            playbackManager.channelDown();
        },
        'volumedown': () => {
            playbackManager.volumeDown();
        },
        'volumeup': () => {
            playbackManager.volumeUp();
        },
        'play': () => {
            playbackManager.unpause();
        },
        'pause': () => {
            playbackManager.pause();
        },
        'playpause': () => {
            playbackManager.playPause();
        },
        'stop': () => {
            if (checkCommandTime('stop')) {
                playbackManager.stop();
            }
        },
        'changezoom': () => {
            playbackManager.toggleAspectRatio();
        },
        'increaseplaybackrate': () => {
            playbackManager.increasePlaybackRate();
        },
        'decreaseplaybackrate': () => {
            playbackManager.decreasePlaybackRate();
        },
        'changeaudiotrack': () => {
            playbackManager.changeAudioStream();
        },
        'changesubtitletrack': () => {
            playbackManager.changeSubtitleStream();
        },
        'search': () => {
            void appRouter.showSearch().catch((error: unknown) => console.error('Failed to open search', error));
        },
        'favorites': () => {
            void appRouter.showFavorites().catch((error: unknown) => console.error('Failed to open favorites', error));
        },
        'fastforward': () => {
            playbackManager.fastForward();
        },
        'rewind': () => {
            playbackManager.rewind();
        },
        'seek': () => {
            playbackManager.seekMs(options as Record<string, unknown>);
        },
        'togglefullscreen': () => {
            playbackManager.toggleFullscreen();
        },
        'disabledisplaymirror': () => {
            playbackManager.enableDisplayMirroring(false);
        },
        'enabledisplaymirror': () => {
            playbackManager.enableDisplayMirroring(true);
        },
        'toggledisplaymirror': () => {
            playbackManager.toggleDisplayMirroring();
        },
        'nowplaying': () => {
            void appRouter.showNowPlaying().catch((error: unknown) => console.error('Failed to open now playing', error));
        },
        'repeatnone': () => {
            playbackManager.setRepeatMode('RepeatNone');
        },
        'repeatall': () => {
            playbackManager.setRepeatMode('RepeatAll');
        },
        'repeatone': () => {
            playbackManager.setRepeatMode('RepeatOne');
        },
        'unknown': () => {
            // This is the command given by 'notify', it's a no-op
        }
    })[command];

    const action = keyActions(commandName);
    if (action !== undefined) {
        action.call(null);
    } else {
        console.debug(`inputManager: tried to process command with no action assigned: ${commandName}`);
    }
}

dom.addEventListener(document, 'click', notify, {
    passive: true
});

export default {
    handleCommand: handleCommand,
    notify: notify,
    notifyMouseMove: notifyMouseMove,
    idleTime: idleTime,
    on: on,
    off: off
};
