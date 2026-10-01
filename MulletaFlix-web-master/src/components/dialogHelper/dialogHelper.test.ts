import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { browserMock, focusManagerMock, historyMock, historyState, inputManagerMock, layoutManagerMock } = vi.hoisted(() => {
    const state = {
        location: { pathname: '/', search: '', state: { dialogs: [] as string[] } },
        stack: [{ pathname: '/', search: '', state: { dialogs: [] as string[] } }],
        listeners: new Set<(update: { action: string; location: unknown }) => void>()
    };

    const history = {
        get location() {
            return state.location;
        },
        push: vi.fn((_path: string, nextState: { dialogs?: string[] }) => {
            state.location = { ...state.location, state: { dialogs: [...(nextState.dialogs ?? [])] } };
            state.stack.push(state.location);
        }),
        replace: vi.fn((_path: string, nextState: { dialogs?: string[] }) => {
            state.location = { ...state.location, state: { dialogs: [...(nextState.dialogs ?? [])] } };
            state.stack[state.stack.length - 1] = state.location;
        }),
        back: vi.fn(() => {
            if (state.stack.length > 1) {
                state.stack.pop();
            }
            state.location = state.stack[state.stack.length - 1];
            for (const listener of [...state.listeners]) {
                listener({ action: 'POP', location: state.location });
            }
        }),
        listen: vi.fn((listener: (update: { action: string; location: unknown }) => void) => {
            state.listeners.add(listener);
            return () => state.listeners.delete(listener);
        })
    };

    const inputManager = {
        on: vi.fn((scope: EventTarget, handler: EventListener) => scope.addEventListener('command', handler)),
        off: vi.fn((scope: EventTarget, handler: EventListener) => scope.removeEventListener('command', handler)),
        handleCommand: vi.fn((command: string) => {
            const dialogs = document.querySelectorAll('.dialogContainer .dialog.opened');
            const dialog = dialogs.item(dialogs.length - 1);
            dialog?.dispatchEvent(new CustomEvent('command', {
                detail: { command },
                bubbles: true,
                cancelable: true
            }));
        })
    };

    return {
        browserMock: { tv: false, supportsCssAnimation: () => false },
        focusManagerMock: {
            autoFocus: vi.fn(),
            focus: vi.fn((element: HTMLElement) => element.focus()),
            popScope: vi.fn(),
            pushScope: vi.fn()
        },
        historyMock: history,
        historyState: state,
        inputManagerMock: inputManager,
        layoutManagerMock: { tv: false }
    };
});

vi.mock('../../scripts/browser', () => ({ default: browserMock }));
vi.mock('../focusManager', () => ({ default: focusManagerMock }));
vi.mock('../layoutManager', () => ({ default: layoutManagerMock }));
vi.mock('../../scripts/inputManager', () => ({ default: inputManagerMock }));
vi.mock('../loading/loading.ts', () => ({ hide: vi.fn() }));
vi.mock('../router/routerHistory', () => ({ history: historyMock }));
vi.mock('../../scripts/settings/appSettings', () => ({ default: { enableGamepad: () => false } }));

import dialogHelper from './dialogHelper';

describe('dialog keyboard dismissal', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        browserMock.tv = false;
        layoutManagerMock.tv = false;
        historyState.location = { pathname: '/', search: '', state: { dialogs: [] } };
        historyState.stack = [historyState.location];
        historyState.listeners.clear();
    });

    afterEach(() => {
        while (document.body.firstChild) {
            document.body.removeChild(document.body.firstChild);
        }
    });

    it('closes a history-enabled dialog with Escape and restores focus to its opener', async () => {
        const opener = document.createElement('button');
        document.body.append(opener);
        opener.focus();

        const dialog = dialogHelper.createDialog({ autoFocus: false, removeOnClose: false });
        const action = document.createElement('button');
        dialog.append(action);
        const closed = dialogHelper.open(dialog);
        action.focus();

        const event = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
        action.dispatchEvent(event);

        expect(event.defaultPrevented).toBe(true);
        expect(dialog.classList.contains('opened')).toBe(false);
        await closed;
        expect(historyMock.back).toHaveBeenCalledOnce();
        expect(document.activeElement).toBe(opener);
    });

    it('closes nested dialogs in order and restores focus to each opener', async () => {
        vi.spyOn(Date.prototype, 'getTime').mockReturnValue(1000);
        const pageOpener = document.createElement('button');
        document.body.append(pageOpener);
        pageOpener.focus();

        const parent = dialogHelper.createDialog({ autoFocus: false, removeOnClose: false });
        const childOpener = document.createElement('button');
        parent.append(childOpener);
        const parentClosed = dialogHelper.open(parent);
        const parentHistoryDialogs = historyState.stack[1].state.dialogs;
        childOpener.focus();

        const child = dialogHelper.createDialog({ autoFocus: false, removeOnClose: false });
        const childAction = document.createElement('button');
        child.append(childAction);
        const childClosed = dialogHelper.open(child);
        childAction.focus();
        childAction.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));

        await childClosed;
        expect(parentHistoryDialogs).toHaveLength(1);
        expect(child.classList.contains('opened')).toBe(false);
        expect(parent.classList.contains('opened')).toBe(true);
        expect(document.activeElement).toBe(childOpener);
        const parentHash = historyMock.push.mock.calls[0][1].dialogs?.[0];
        const childHash = historyMock.push.mock.calls[1][1].dialogs?.[1];
        expect(parentHash).toBeDefined();
        expect(childHash).toBeDefined();
        expect(childHash).not.toBe(parentHash);

        childOpener.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
        await parentClosed;
        expect(parent.classList.contains('opened')).toBe(false);
        expect(document.activeElement).toBe(pageOpener);
        expect(historyMock.back).toHaveBeenCalledTimes(2);
    });

    it('closes a history-enabled dialog from remote Back without leaving it open', async () => {
        browserMock.tv = true;
        layoutManagerMock.tv = true;
        const opener = document.createElement('button');
        document.body.append(opener);
        opener.focus();

        const dialog = dialogHelper.createDialog({ autoFocus: false, removeOnClose: false });
        const closed = dialogHelper.open(dialog);
        inputManagerMock.handleCommand('back');

        expect(dialog.classList.contains('opened')).toBe(false);
        await closed;
        expect(historyMock.back).toHaveBeenCalledOnce();
        expect(historyState.location.pathname).toBe('/');
        expect(document.activeElement).toBe(opener);
    });

    it('leaves the dialog open when a child control consumes Escape', async () => {
        const dialog = dialogHelper.createDialog({ autoFocus: false, removeOnClose: false });
        const action = document.createElement('button');
        action.addEventListener('keydown', event => event.preventDefault());
        dialog.append(action);
        const closed = dialogHelper.open(dialog);

        const event = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
        action.dispatchEvent(event);

        expect(event.defaultPrevented).toBe(true);
        expect(dialog.classList.contains('opened')).toBe(true);
        expect(historyMock.back).not.toHaveBeenCalled();
        await dialogHelper.close(dialog);
        await closed;
    });

    it('closes a non-history dialog from the remote Back command and restores focus', async () => {
        browserMock.tv = true;
        layoutManagerMock.tv = true;
        const opener = document.createElement('button');
        document.body.append(opener);
        opener.focus();

        const dialog = dialogHelper.createDialog({ enableHistory: false, autoFocus: false, removeOnClose: false });
        const closed = dialogHelper.open(dialog);
        inputManagerMock.handleCommand('back');

        expect(dialog.classList.contains('opened')).toBe(false);
        await closed;
        expect(document.activeElement).toBe(opener);
        expect(historyMock.back).not.toHaveBeenCalled();
    });
});
