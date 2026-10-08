import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('components/Page', () => ({
    default: ({ children }: React.PropsWithChildren) => <main>{children}</main>
}));

vi.mock('@mui/material/Box', () => ({
    default: (props: React.PropsWithChildren<{ sx?: unknown; [key: string]: unknown }>) => {
        const { children } = props;
        const domProps = Object.fromEntries(Object.entries(props).filter(([key]) => key !== 'sx' && key !== 'children'));
        return <div {...domProps as React.HTMLAttributes<HTMLDivElement>}>{children}</div>;
    }
}));

vi.mock('components/common/DetailPageSkeleton', () => ({
    default: () => <div data-testid='detail-skeleton'>Loading detail</div>
}));

vi.mock('components/viewManager/ViewManagerPage', () => ({
    default: () => <div data-testid='legacy-detail'>Legacy detail content</div>
}));

vi.mock('components/common', () => ({
    getRemotePageState: ({ isPending, isError, hasData, isOnline }: {
        isPending: boolean;
        isError: boolean;
        hasData: boolean;
        isOnline: boolean;
    }) => {
        if (!isOnline) return hasData ? 'degraded' : 'offline';
        if (isError) return hasData ? 'degraded' : 'error';
        if (isPending && !hasData) return 'loading';
        return 'success';
    },
    PageStateContainer: ({ state, loadingComponent, onRetry }: {
        state: string;
        loadingComponent?: React.ReactNode;
        onRetry?: () => void;
    }) => (
        <div data-testid='page-state' data-state={state}>
            {state === 'loading' ? loadingComponent : state}
            {onRetry && <button onClick={onRetry}>Retry</button>}
        </div>
    )
}));

vi.mock('../../../lib/globalize', () => ({ default: { translate: (key: string) => key } }));

import ItemDetailsModern from './ItemDetailsModern';
import {
    emitItemDetailsPageState,
    ITEM_DETAILS_PAGE_RETRY_EVENT,
    type ItemDetailsPageRetryDetail
} from 'components/itemDetails/itemDetailsPageEvents';

Object.defineProperty(globalThis, 'IS_REACT_ACT_ENVIRONMENT', { configurable: true, value: true });

describe('ItemDetailsModern request states', () => {
    let host: HTMLDivElement;
    let root: Root;
    let originalOnline: boolean;

    const render = async (entry = '/details?id=item-1'): Promise<void> => {
        await act(async () => {
            root.render(
                <MemoryRouter initialEntries={[entry]}>
                    <ItemDetailsModern />
                </MemoryRouter>
            );
        });
    };

    beforeEach(() => {
        originalOnline = navigator.onLine;
        Object.defineProperty(navigator, 'onLine', { configurable: true, value: true });
        host = document.createElement('div');
        document.body.append(host);
        root = createRoot(host);
    });

    afterEach(() => {
        act(() => root.unmount());
        host.remove();
        Object.defineProperty(navigator, 'onLine', { configurable: true, value: originalOnline });
    });

    it('waits for a real success event instead of treating elapsed time as success', async () => {
        await render();

        expect(host.querySelector('[data-testid="page-state"]')?.getAttribute('data-state')).toBe('loading');
        expect(host.querySelector('[data-testid="detail-skeleton"]')).not.toBeNull();
        expect(host.querySelector('[data-testid="legacy-detail"]')?.parentElement?.getAttribute('aria-hidden')).toBe('true');

        await act(async () => emitItemDetailsPageState('item-1', 'success'));

        expect(host.querySelector('[data-testid="page-state"]')).toBeNull();
        expect(host.querySelector('[data-testid="legacy-detail"]')?.parentElement?.getAttribute('aria-hidden')).toBe('false');
    });

    it('preserves loaded detail content on degraded refresh and offers retry', async () => {
        await render();
        await act(async () => emitItemDetailsPageState('item-1', 'success'));
        await act(async () => emitItemDetailsPageState('item-1', 'degraded'));

        expect(host.querySelector('[data-testid="page-state"]')?.getAttribute('data-state')).toBe('degraded');
        expect(host.querySelector('[data-testid="legacy-detail"]')?.textContent).toBe('Legacy detail content');

        const retryListener = vi.fn((event: Event) => (event as CustomEvent<ItemDetailsPageRetryDetail>).detail);
        document.addEventListener(ITEM_DETAILS_PAGE_RETRY_EVENT, retryListener);
        await act(async () => host.querySelector<HTMLButtonElement>('button')?.click());

        expect(retryListener).toHaveBeenCalledOnce();
        expect(retryListener.mock.results[0]?.value).toEqual({ itemId: 'item-1' });
        document.removeEventListener(ITEM_DETAILS_PAGE_RETRY_EVENT, retryListener);
    });

    it('shows offline and error states without exposing the unrendered detail view', async () => {
        await render();
        Object.defineProperty(navigator, 'onLine', { configurable: true, value: false });
        await act(async () => window.dispatchEvent(new Event('offline')));

        expect(host.querySelector('[data-testid="page-state"]')?.getAttribute('data-state')).toBe('offline');
        expect(host.querySelector('[data-testid="legacy-detail"]')?.parentElement?.getAttribute('aria-hidden')).toBe('true');

        Object.defineProperty(navigator, 'onLine', { configurable: true, value: true });
        await act(async () => window.dispatchEvent(new Event('online')));
        await act(async () => emitItemDetailsPageState('item-1', 'error'));

        expect(host.querySelector('[data-testid="page-state"]')?.getAttribute('data-state')).toBe('error');
        expect(host.querySelector('[data-testid="legacy-detail"]')?.parentElement?.getAttribute('aria-hidden')).toBe('true');
    });
});
