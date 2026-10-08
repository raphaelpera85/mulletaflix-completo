import { describe, expect, it, vi } from 'vitest';

import {
    emitItemDetailsPageState,
    getItemDetailsFailureState,
    ITEM_DETAILS_PAGE_STATE_EVENT,
    requestItemDetailsPageRetry,
    ITEM_DETAILS_PAGE_RETRY_EVENT
} from './itemDetailsPageEvents';

describe('item details page events', () => {
    it('emits request states with the item identity', () => {
        const listener = vi.fn();
        document.addEventListener(ITEM_DETAILS_PAGE_STATE_EVENT, listener);

        emitItemDetailsPageState('item-1', 'success');

        expect(listener).toHaveBeenCalledOnce();
        expect((listener.mock.calls[0][0] as CustomEvent).detail).toEqual({ itemId: 'item-1', state: 'success' });
        document.removeEventListener(ITEM_DETAILS_PAGE_STATE_EVENT, listener);
    });

    it('emits retry requests with the item identity', () => {
        const listener = vi.fn();
        document.addEventListener(ITEM_DETAILS_PAGE_RETRY_EVENT, listener);

        requestItemDetailsPageRetry('item-2');

        expect((listener.mock.calls[0][0] as CustomEvent).detail).toEqual({ itemId: 'item-2' });
        document.removeEventListener(ITEM_DETAILS_PAGE_RETRY_EVENT, listener);
    });

    it('degrades a failed refresh only for the item already rendered', () => {
        expect(getItemDetailsFailureState('item-1', 'item-1')).toBe('degraded');
        expect(getItemDetailsFailureState('item-1', 'item-2')).toBe('error');
        expect(getItemDetailsFailureState(undefined, 'item-1')).toBe('error');
    });
});
