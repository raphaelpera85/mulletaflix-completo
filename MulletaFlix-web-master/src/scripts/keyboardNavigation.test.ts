import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

const { browserMock, inputManagerMock, layoutManagerMock } = vi.hoisted(() => ({
    browserMock: { tv: true, edgeUwp: false, hisense: false, vidaa: false },
    inputManagerMock: { handleCommand: vi.fn() },
    layoutManagerMock: { tv: true }
}));

vi.mock('./browser', () => ({ default: browserMock }));
vi.mock('./inputManager', () => ({ default: inputManagerMock }));
vi.mock('../components/layoutManager', () => ({ default: layoutManagerMock }));
vi.mock('./settings/appSettings', () => ({ default: { enableGamepad: () => false } }));

import keyboardNavigation, { getKeyName, isInteractiveElement, isMediaKey, isNavigationKey } from './keyboardNavigation';

function dispatchKey(key: string, target: EventTarget = document.body): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
    target.dispatchEvent(event);
    return event;
}

describe('keyboardNavigation', () => {
    beforeAll(() => {
        keyboardNavigation.enable();
    });

    beforeEach(() => {
        vi.clearAllMocks();
        browserMock.tv = true;
        layoutManagerMock.tv = true;
    });

    it('normalizes TV remote directions and classifies navigation/media keys', () => {
        expect(getKeyName(new KeyboardEvent('keydown', { key: 'NavigationLeft' }))).toBe('ArrowLeft');
        expect(getKeyName(new KeyboardEvent('keydown', { key: 'GamepadDPadRight' }))).toBe('ArrowRight');
        expect(isNavigationKey('ArrowLeft')).toBe(true);
        expect(isMediaKey('MediaPlayPause')).toBe(true);
    });

    it('routes remote arrows through the TV focus manager and prevents browser scrolling', () => {
        const event = dispatchKey('NavigationRight');

        expect(inputManagerMock.handleCommand).toHaveBeenCalledWith('right');
        expect(event.defaultPrevented).toBe(true);
    });

    it('does not steal arrow keys from editable text fields', () => {
        const input = document.createElement('input');
        document.body.append(input);
        input.focus();

        const event = dispatchKey('ArrowLeft', input);

        expect(inputManagerMock.handleCommand).not.toHaveBeenCalled();
        expect(event.defaultPrevented).toBe(false);
        expect(isInteractiveElement(input)).toBe(true);

        input.remove();
    });

    it('leaves desktop arrow navigation to the browser', () => {
        layoutManagerMock.tv = false;

        const event = dispatchKey('ArrowLeft');

        expect(inputManagerMock.handleCommand).not.toHaveBeenCalled();
        expect(event.defaultPrevented).toBe(false);
    });

    it('maps remote back and select actions to their commands on TV', () => {
        const back = dispatchKey('Escape');
        const select = dispatchKey('GamepadA');

        expect(inputManagerMock.handleCommand).toHaveBeenNthCalledWith(1, 'back');
        expect(inputManagerMock.handleCommand).toHaveBeenNthCalledWith(2, 'select');
        expect(back.defaultPrevented).toBe(true);
        expect(select.defaultPrevented).toBe(true);
    });

    it('does not convert Escape into TV back navigation on desktop', () => {
        layoutManagerMock.tv = false;

        const event = dispatchKey('Escape');

        expect(inputManagerMock.handleCommand).not.toHaveBeenCalled();
        expect(event.defaultPrevented).toBe(false);
    });
});
