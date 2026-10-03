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

import { showMediaRequestDialog, showPlaybackIssueDialog } from './userFeedback';

describe('media request autocomplete', () => {
    const getJSON = vi.fn();
    const open = (ajax = vi.fn().mockResolvedValue(undefined)) => {
        showMediaRequestDialog({ getJSON, ajax, getUrl: (url: string) => url } as unknown as ApiClient);
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

        expect(document.querySelector<HTMLElement>('.userFeedbackSuggestions')!.hidden).toBe(true);
        expect(input.getAttribute('aria-expanded')).toBe('false');
    });

    it('hides old options immediately when the search text changes', async () => {
        getJSON.mockResolvedValue([{ Title: 'Atomic', MediaType: 'Series' }]);
        const input = open();
        type(input, 'Atomic');
        await vi.advanceTimersByTimeAsync(250);
        expect(document.querySelector<HTMLElement>('.userFeedbackSuggestions')!.hidden).toBe(false);

        type(input, 'Another title');

        expect(document.querySelector<HTMLElement>('.userFeedbackSuggestions')!.hidden).toBe(true);
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

    it('gives request fields visible labels and accessible names independent of placeholders', () => {
        open();

        expect(document.querySelector<HTMLInputElement>('input[name="title"]')?.getAttribute('aria-label')).toBe('MediaRequestName');
        expect(document.querySelector<HTMLInputElement>('input[name="year"]')?.getAttribute('aria-label')).toBe('LabelYear');
        expect(document.querySelector<HTMLTextAreaElement>('textarea[name="notes"]')?.getAttribute('aria-label')).toBe('MediaRequestNotes');
        expect(document.querySelector<HTMLSelectElement>('select[name="mediaType"]')?.getAttribute('aria-label')).toBe('MediaType');
        document.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>('.userFeedbackForm input, .userFeedbackForm select, .userFeedbackForm textarea').forEach(field => {
            expect(document.querySelector(`label[for="${field.id}"]`)?.textContent?.trim()).toBeTruthy();
        });
    });

    it('gives playback issue details an accessible name', () => {
        showPlaybackIssueDialog({ getJSON, getUrl: (url: string) => url } as unknown as ApiClient, { Id: 'item-1', Name: 'Atomic' });

        expect(document.querySelector<HTMLSelectElement>('select[name="category"]')?.getAttribute('aria-label')).toBe('LabelType');
        expect(document.querySelector<HTMLTextAreaElement>('textarea[name="description"]')?.getAttribute('aria-label')).toBe('PlaybackIssueDetails');
        document.querySelectorAll<HTMLSelectElement | HTMLTextAreaElement>('.userFeedbackForm select, .userFeedbackForm textarea').forEach(field => {
            expect(document.querySelector(`label[for="${field.id}"]`)?.textContent?.trim()).toBeTruthy();
        });
    });

    it('requires a playback issue category and explains the missing selection', () => {
        showPlaybackIssueDialog({ getJSON, getUrl: (url: string) => url } as unknown as ApiClient, { Id: 'item-1', Name: 'Atomic' });
        const form = document.querySelector<HTMLFormElement>('.userFeedbackForm')!;
        const category = form.querySelector<HTMLSelectElement>('select[name="category"]')!;

        form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

        const error = category.nextElementSibling as HTMLElement;
        expect(error.hidden).toBe(false);
        expect(error.textContent).toBe('PlaybackIssueCategoryRequired');
        expect(category.getAttribute('aria-invalid')).toBe('true');
        expect(document.activeElement).toBe(category);
    });

    it('shows an inline required-title error, associates it and focuses the field', () => {
        open();
        const form = document.querySelector<HTMLFormElement>('.userFeedbackForm')!;
        const title = form.querySelector<HTMLInputElement>('input[name="title"]')!;
        form.querySelector<HTMLSelectElement>('select[name="mediaType"]')!.value = 'Movie';

        form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

        const error = form.querySelector<HTMLElement>('.userFeedbackFieldError')!;
        expect(error.hidden).toBe(false);
        expect(error.textContent).toBe('MediaRequestTitleRequired');
        expect(title.getAttribute('aria-invalid')).toBe('true');
        expect(title.getAttribute('aria-describedby')).toContain(error.id);
        expect(document.activeElement).toBe(title);

        title.value = 'Atomic';
        title.dispatchEvent(new Event('input', { bubbles: true }));
        expect(error.hidden).toBe(true);
        expect(title.hasAttribute('aria-invalid')).toBe(false);
    });

    it('explains the accepted year range inline', () => {
        open();
        const form = document.querySelector<HTMLFormElement>('.userFeedbackForm')!;
        const title = form.querySelector<HTMLInputElement>('input[name="title"]')!;
        const year = form.querySelector<HTMLInputElement>('input[name="year"]')!;
        form.querySelector<HTMLSelectElement>('select[name="mediaType"]')!.value = 'Movie';
        title.value = 'Atomic';
        year.value = '2201';

        form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

        const yearError = year.nextElementSibling as HTMLElement;
        expect(yearError.hidden).toBe(false);
        expect(yearError.textContent).toBe('MediaRequestYearRangeInvalid');
        expect(year.getAttribute('aria-invalid')).toBe('true');
        expect(document.activeElement).toBe(year);
    });

    it('shows a required media-type error after the user leaves its prompt selected', () => {
        open();
        const form = document.querySelector<HTMLFormElement>('.userFeedbackForm')!;
        const title = form.querySelector<HTMLInputElement>('input[name="title"]')!;
        const mediaType = form.querySelector<HTMLSelectElement>('select[name="mediaType"]')!;
        title.value = 'Atomic';

        form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

        const error = mediaType.nextElementSibling as HTMLElement;
        expect(mediaType.value).toBe('');
        expect(error.hidden).toBe(false);
        expect(error.textContent).toBe('MediaRequestTypeRequired');
        expect(mediaType.getAttribute('aria-invalid')).toBe('true');
        expect(document.activeElement).toBe(mediaType);
    });

    it('announces successful submission outside the closing dialog', async () => {
        const ajax = vi.fn().mockResolvedValue(undefined);
        const title = open(ajax);
        title.value = 'Atomic';
        const form = document.querySelector<HTMLFormElement>('.userFeedbackForm')!;
        form.querySelector<HTMLSelectElement>('select[name="mediaType"]')!.value = 'Movie';
        form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
        await Promise.resolve();
        await Promise.resolve();
        await vi.advanceTimersByTimeAsync(0);

        const status = document.querySelector<HTMLElement>('#userFeedbackLiveStatus');
        expect(status?.getAttribute('role')).toBe('status');
        expect(status?.getAttribute('aria-live')).toBe('polite');
        expect(status?.textContent).toBe('UserFeedbackSent');
        expect(ajax).toHaveBeenCalledOnce();
    });

    it('announces submit failure in the form and clears it when the user edits', async () => {
        const ajax = vi.fn().mockRejectedValue(new Error('offline'));
        const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => undefined);
        try {
            const input = open(ajax);
            input.value = 'Atomic';
            const form = document.querySelector<HTMLFormElement>('.userFeedbackForm')!;
            form.querySelector<HTMLSelectElement>('select[name="mediaType"]')!.value = 'Movie';
            const errorMessage = form.querySelector<HTMLElement>('[role="alert"]')!;
            const submitButton = form.querySelector<HTMLButtonElement>('button[type="submit"]')!;

            expect(form.getAttribute('aria-describedby')).toBe(errorMessage.id);
            form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
            await vi.waitFor(() => expect(errorMessage.hidden).toBe(false));

            expect(errorMessage.hidden).toBe(false);
            expect(errorMessage.textContent).toBe('UserFeedbackSendFailed');
            expect(submitButton.disabled).toBe(false);

            input.dispatchEvent(new Event('input', { bubbles: true }));
            expect(errorMessage.hidden).toBe(true);
            expect(errorMessage.textContent).toBe('');
            expect(ajax).toHaveBeenCalledOnce();
        } finally {
            errorSpy.mockRestore();
        }
    });

    it('announces when the catalog lookup is in progress and when no titles match', async () => {
        let resolveLookup!: (value: unknown) => void;
        getJSON.mockReturnValueOnce(new Promise(resolve => {
            resolveLookup = resolve;
        })).mockResolvedValueOnce({ State: 'Ready', IsIndexing: false, FailedRootCount: 0 }).mockResolvedValueOnce([]);
        const input = open();
        type(input, 'Missing title');
        await vi.advanceTimersByTimeAsync(250);

        expect(document.querySelector('.userFeedbackSuggestions [role="status"]')?.textContent).toBe('MediaRequestSuggestionsLoading');
        expect(input.getAttribute('aria-expanded')).toBe('true');
        resolveLookup([]);
        await vi.advanceTimersByTimeAsync(0);

        expect(document.querySelector('.userFeedbackSuggestions [role="status"]')?.textContent).toBe('MediaRequestSuggestionsEmpty');
        expect(document.querySelector<HTMLElement>('.userFeedbackSuggestions')?.hidden).toBe(false);
    });

    it('shows indexing progress and refreshes matches when the catalog finishes', async () => {
        getJSON.mockResolvedValueOnce([])
            .mockResolvedValueOnce({ State: 'Indexing', IsIndexing: true, FailedRootCount: 0 })
            .mockResolvedValueOnce({ State: 'Ready', IsIndexing: false, FailedRootCount: 0 })
            .mockResolvedValueOnce([{ Title: 'Atomic', MediaType: 'Series' }]);
        const input = open();
        type(input, 'Atomic');
        await vi.advanceTimersByTimeAsync(250);
        await vi.advanceTimersByTimeAsync(0);

        expect(document.querySelector('.userFeedbackSuggestions [role="status"]')?.textContent).toBe('MediaRequestSuggestionsIndexing');
        await vi.advanceTimersByTimeAsync(1000);

        expect(document.querySelector<HTMLElement>('[role="option"]')?.textContent).toBe('Atomic — Series');
        expect(getJSON).toHaveBeenCalledTimes(4);
    });

    it('shows a retry action after lookup failure and recovers', async () => {
        getJSON.mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce([{ Title: 'Atomic', MediaType: 'Series' }]);
        const input = open();
        type(input, 'Atomic');
        await vi.advanceTimersByTimeAsync(250);

        expect(document.querySelector('.userFeedbackSuggestions [role="status"]')?.textContent).toContain('MediaRequestSuggestionsFailed');
        const retry = document.querySelector<HTMLButtonElement>('.userFeedbackSuggestions button');
        expect(retry?.textContent).toBe('Retry');
        retry?.focus();
        input.dispatchEvent(new FocusEvent('blur', { relatedTarget: retry }));
        await vi.advanceTimersByTimeAsync(120);
        expect(document.querySelector<HTMLElement>('.userFeedbackSuggestions')?.hidden).toBe(false);
        retry?.click();
        expect(document.activeElement).toBe(input);
        await vi.advanceTimersByTimeAsync(0);

        expect(getJSON).toHaveBeenCalledTimes(2);
        expect(document.querySelector<HTMLElement>('[role="option"]')?.textContent).toBe('Atomic — Series');
    });
});
