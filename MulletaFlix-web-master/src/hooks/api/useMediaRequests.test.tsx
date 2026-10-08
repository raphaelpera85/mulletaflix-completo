import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { getJSON } = vi.hoisted(() => ({ getJSON: vi.fn() }));
vi.mock('hooks/useApi', () => ({ useApi: () => ({ user: { Id: 'caller' } }) }));
vi.mock('lib/jellyfin-apiclient', () => ({
    ServerConnections: { currentApiClient: () => ({
        getJSON,
        getUrl: (path: string, params: Record<string, number>) => `${path}?${new URLSearchParams(Object.entries(params).map(([key, value]) => [key, String(value)]))}`
    }) }
}));

import { useMyClassifiedMediaRequests } from './useMediaRequests';

describe('my media requests pagination', () => {
    let root: Root;
    let client: QueryClient;
    let latest: ReturnType<typeof useMyClassifiedMediaRequests>;

    const Probe = () => {
        latest = useMyClassifiedMediaRequests();
        return null;
    };

    beforeEach(() => {
        getJSON.mockReset();
        client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
        root = createRoot(document.createElement('div'));
    });

    afterEach(async () => {
        await act(async () => root.unmount());
        client.clear();
    });

    const mount = async () => {
        await act(async () => root.render(<QueryClientProvider client={client}><Probe /></QueryClientProvider>));
        await vi.waitFor(() => expect(latest.isPending).toBe(false));
    };

    it('loads beyond the first 200 requests and classifies titles without an admin catalog request', async () => {
        getJSON.mockResolvedValueOnce({
            StartIndex: 0, TotalRecordCount: 201,
            Items: Array.from({ length: 200 }, (_, Id) => ({ Id, Name: `Solicitação de mídia: Title ${Id}`, Overview: 'Series' })),
            PriorityRequestIds: [0], Catalog: [],
            QueueStatuses: [{ RequestId: 0, Download: { SnapshotAvailable: true, Position: 2, MatchingItemCount: 4 }, Upload: { SnapshotAvailable: false } }]
        }).mockResolvedValueOnce({
            StartIndex: 200, TotalRecordCount: 201,
            Items: [{ Id: 200, Name: 'Solicitação de mídia: Included title', Overview: 'Series' }],
            PriorityRequestIds: [200], Catalog: [{ Title: 'Included title', MediaType: 'Series' }],
            QueueStatuses: [{ RequestId: 200, Download: { SnapshotAvailable: true, Position: 1, MatchingItemCount: 1 } }]
        });

        await mount();
        expect(latest.pending).toHaveLength(200);
        expect(latest.hasNextPage).toBe(true);
        await act(async () => {
            await latest.fetchNextPage();
        });
        await vi.waitFor(() => expect(latest.hasNextPage).toBe(false));

        expect(latest.pending).toHaveLength(200);
        expect(latest.included.map(entry => entry.Id)).toEqual([200]);
        expect(latest.priorityRequestIds).toEqual(new Set([0, 200]));
        expect(latest.queueStatuses.get(0)?.Download?.Position).toBe(2);
        expect(latest.queueStatuses.get(200)?.Download?.Position).toBe(1);
        expect(getJSON.mock.calls.map(([url]) => url)).toEqual([
            'UserFeedback/MediaRequests?limit=200&startIndex=0',
            'UserFeedback/MediaRequests?limit=200&startIndex=200'
        ]);
    });

    it('preserves loaded titles when the next page fails and retries the same offset', async () => {
        getJSON.mockResolvedValueOnce({ StartIndex: 0, TotalRecordCount: 2, Items: [{ Id: 1, Name: 'Solicitação de mídia: First' }] })
            .mockRejectedValueOnce(new Error('network unavailable'))
            .mockResolvedValueOnce({ StartIndex: 1, TotalRecordCount: 2, Items: [{ Id: 2, Name: 'Solicitação de mídia: Second' }] });

        await mount();
        await act(async () => {
            await latest.fetchNextPage();
        });
        await vi.waitFor(() => expect(latest.isFetchNextPageError).toBe(true));
        expect(latest.isError).toBe(false);
        expect(latest.isDegraded).toBe(false);
        expect(latest.pending).toHaveLength(1);

        await act(async () => {
            await latest.fetchNextPage();
        });
        await vi.waitFor(() => expect(latest.pending).toHaveLength(2));
        expect(getJSON.mock.calls.slice(1).map(([url]) => url)).toEqual([
            'UserFeedback/MediaRequests?limit=200&startIndex=1',
            'UserFeedback/MediaRequests?limit=200&startIndex=1'
        ]);
    });
});
