import type { ApiClient } from 'jellyfin-apiclient';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('components/dialogHelper/dialogHelper', () => ({ default: {
    createDialog: () => {
        const dialog = document.createElement('div');
        document.body.append(dialog);
        return dialog;
    },
    open: () => Promise.resolve(),
    close: (dialog: HTMLElement) => dialog.dispatchEvent(new Event('close'))
} }));
vi.mock('components/toast/toast', () => ({ default: vi.fn() }));
vi.mock('lib/globalize', () => ({ default: { translate: (key: string) => key } }));

import { showMediaRequestDialog } from './userFeedback';

describe('media request autocomplete', () => {
    const getJSON = vi.fn();
    const open = () => {
        showMediaRequestDialog({ getJSON, getUrl: (url: string) => url } as unknown as ApiClient);
        return document.querySelector<HTMLInputElement>('input[name="title"]')!;
    };
    const type = (input: HTMLInputElement, title: string) => {
        input.value = title;
        input.dispatchEvent(new Event('input'));
    };

    beforeEach(() => {
        vi.useFakeTimers();
        getJSON.mockReset();
    });
    afterEach(() => {
        vi.clearAllTimers();
        document.body.textContent = '';
        vi.useRealTimers();
    });

    it('clears a stale year and collapses the combobox after selection', async () => {
        getJSON.mockResolvedValue([{ Title: 'Atomic', MediaType: 'Series' }]);
        const input = open();
        document.querySelector<HTMLInputElement>('input[name="year"]')!.value = '2024';
        type(input, 'At');
        await vi.advanceTimersByTimeAsync(250);
        document.querySelector<HTMLButtonElement>('[role="option"]')!.click();

        expect(input.value).toBe('Atomic');
        expect(document.querySelector<HTMLInputElement>('input[name="year"]')!.value).toBe('');
        expect(input.getAttribute('aria-expanded')).toBe('false');
    });

    it('does not reopen suggestions after Escape while lookup is in flight', async () => {
        let resolveLookup!: (value: unknown) => void;
        getJSON.mockReturnValue(new Promise(resolve => {
            resolveLookup = resolve;
        }));
        const input = open();
        type(input, 'Atomic');
        await vi.advanceTimersByTimeAsync(250);
        input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
        resolveLookup([{ Title: 'Atomic', MediaType: 'Series' }]);
        await vi.advanceTimersByTimeAsync(0);

        expect(document.querySelector<HTMLElement>('[role="listbox"]')!.hidden).toBe(true);
        expect(input.getAttribute('aria-expanded')).toBe('false');
    });

    it('hides old options immediately when the search text changes', async () => {
        getJSON.mockResolvedValue([{ Title: 'Atomic', MediaType: 'Series' }]);
        const input = open();
        type(input, 'Atomic');
        await vi.advanceTimersByTimeAsync(250);
        expect(document.querySelector<HTMLElement>('[role="listbox"]')!.hidden).toBe(false);

        type(input, 'Another title');

        expect(document.querySelector<HTMLElement>('[role="listbox"]')!.hidden).toBe(true);
        expect(input.getAttribute('aria-expanded')).toBe('false');
    });

    it('cancels pending debounce when the dialog closes', async () => {
        getJSON.mockResolvedValue([]);
        const input = open();
        type(input, 'Atomic');
        document.querySelector('.formDialog')!.dispatchEvent(new Event('close'));
        await vi.advanceTimersByTimeAsync(250);

        expect(getJSON).not.toHaveBeenCalled();
    });
});
