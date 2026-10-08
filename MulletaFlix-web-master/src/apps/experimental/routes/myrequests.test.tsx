import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { useClassifiedRequests } = vi.hoisted(() => ({
    useClassifiedRequests: vi.fn()
}));

vi.mock('hooks/api/useMediaRequests', () => ({
    useMyClassifiedMediaRequests: useClassifiedRequests
}));
vi.mock('components/loading/LoadingComponent', () => ({ default: () => <div>loading</div> }));
vi.mock('components/Page', () => ({ default: ({ children }: React.PropsWithChildren) => <main>{children}</main> }));
vi.mock('components/common/PageStateContainer', () => ({
    default: ({ state, children, emptyState, onRetry }: {
        state: string;
        children: React.ReactNode;
        emptyState?: { title?: string };
        onRetry?: () => void;
    }) => {
        if (state === 'empty') return <div>{emptyState?.title}</div>;
        if (state === 'error') return <div>ErrorDefault<button onClick={onRetry}>Retry</button></div>;
        if (state === 'loading') return <div>loading</div>;
        if (state === 'degraded') {
            return <><div role='status'>OfflineModeWarning<button onClick={onRetry}>Retry</button></div>{children}</>;
        }
        if (state === 'offline') return <div role='alert'>OfflineModeError<button onClick={onRetry}>Retry</button></div>;
        return React.createElement(React.Fragment, null, children);
    }
}));
vi.mock('lib/globalize', () => ({
    default: {
        translate: (key: string) => ({
            MyMediaRequestPriority: 'Prioridade solicitada',
            MyMediaRequestPriorityDescription: 'O pedido foi registrado como prioritário no Nebula. As posições mostram snapshots e podem mudar durante o processamento.',
            MediaRequestQueueDownload: 'Download',
            MediaRequestQueueUpload: 'Envio',
            MediaRequestQueuePosition: 'posição {position} de {count}',
            MediaRequestQueueMatches: 'Itens deste título: {count}',
            MediaRequestQueueAbsentFromSnapshot: 'Não constava na fila',
            MediaRequestQueueAwaiting: 'Ainda não consta na fila',
            MediaRequestQueuePriority: 'Prioridade efetiva',
            MediaRequestQueueUnavailable: 'Fila indisponível',
            MediaRequestQueueLastSnapshot: 'Último snapshot',
            MediaRequestQueueUpdated: 'Atualizado'
        }[key] || key)
    }
}));

import MyMediaRequestsPage from './myrequests';

describe('MyMediaRequestsPage', () => {
    beforeEach(() => {
        useClassifiedRequests.mockReset();
    });

    it('renders requested and included titles in separate grids', () => {
        useClassifiedRequests.mockReturnValue({
            pending: [{ Id: 10, Name: 'Solicitação de mídia: Novo Dorama', Overview: 'Series', ShortOverview: 'Solicitada em 30/09/2026' }],
            included: [{ Id: 11, Name: 'Solicitação de mídia: Atomic', Overview: 'Incluído', ShortOverview: 'Series · 2024' }],
            priorityRequestIds: new Set([10]),
            queueStatuses: new Map([
                [10, {
                    RequestId: 10,
                    Download: { SnapshotAvailable: true, IsRunning: true, IsPriority: true, SnapshotAtUtc: '2026-10-01T12:30:00Z', Position: 2, QueueItemCount: 10, MatchingItemCount: 3, CurrentItemCount: 0 },
                    Upload: { SnapshotAvailable: true, IsRunning: true, SnapshotAtUtc: '2026-10-01T12:31:00Z', Position: 1, QueueItemCount: 1, MatchingItemCount: 1, CurrentItemCount: 0 }
                }]
            ]),
            isPending: false,
            isError: false,
            refetch: vi.fn()
        });

        const markup = renderToStaticMarkup(<MyMediaRequestsPage />);

        expect(markup).toContain('MediaRequestsPendingTitle');
        expect(markup).toContain('MediaRequestsIncludedTitle');
        expect(markup).toContain('Novo Dorama');
        expect(markup).toContain('Atomic');
        expect(markup).toContain('Prioridade solicitada');
        expect(markup).toContain('O pedido foi registrado como prioritário no Nebula. As posições mostram snapshots e podem mudar durante o processamento.');
        expect(markup).toContain('aria-label="Prioridade solicitada.');
        expect(markup).toContain('Download: Prioridade efetiva · posição 2 de 10');
        expect(markup).toContain('Prioridade efetiva');
        expect(markup).toContain('Itens deste título: 3');
        expect(markup).toContain('Envio: posição 1 de 1');
        expect(markup).toContain('Atualizado');
    });

    it('qualifies a missing title against a stopped queue snapshot', () => {
        useClassifiedRequests.mockReturnValue({
            pending: [
                { Id: 12, Name: 'Solicitação de mídia: Missing title', Overview: 'Series' },
                { Id: 13, Name: 'Solicitação de mídia: Missing status', Overview: 'Series' }
            ],
            included: [],
            priorityRequestIds: new Set<number>(),
            queueStatuses: new Map([[12, {
                RequestId: 12,
                Download: { SnapshotAvailable: true, IsRunning: false, SnapshotAtUtc: '2026-10-01T12:30:00Z', QueueItemCount: 4, MatchingItemCount: 0, CurrentItemCount: 0 },
                Upload: { SnapshotAvailable: true, IsRunning: true, SnapshotAtUtc: '2026-10-01T12:31:00Z', QueueItemCount: 4, MatchingItemCount: 0, CurrentItemCount: 0 }
            }]]),
            isPending: false,
            isError: false,
            refetch: vi.fn()
        });

        const markup = renderToStaticMarkup(<MyMediaRequestsPage />);

        expect(markup).toContain('Download: Último snapshot: Não constava na fila');
        expect(markup).toContain('Envio: Ainda não consta na fila');
        expect(markup).toContain('Download: Fila indisponível');
        expect(markup).toContain('Envio: Fila indisponível');
    });

    it('shows retry when request or catalog loading fails', () => {
        useClassifiedRequests.mockReturnValue({
            pending: [],
            included: [],
            priorityRequestIds: new Set<number>(),
            isPending: false,
            isError: true,
            refetch: vi.fn()
        });

        const markup = renderToStaticMarkup(<MyMediaRequestsPage />);

        expect(markup).toContain('ErrorDefault');
        expect(markup).toContain('Retry');
    });

    it('shows a loading state while either source is pending', () => {
        useClassifiedRequests.mockReturnValue({
            pending: [],
            included: [],
            priorityRequestIds: new Set<number>(),
            isPending: true,
            isError: false,
            refetch: vi.fn()
        });

        expect(renderToStaticMarkup(<MyMediaRequestsPage />)).toContain('loading');
    });

    it('shows the empty state when the user has no requests', () => {
        useClassifiedRequests.mockReturnValue({
            pending: [],
            included: [],
            priorityRequestIds: new Set<number>(),
            isPending: false,
            isError: false,
            refetch: vi.fn()
        });

        expect(renderToStaticMarkup(<MyMediaRequestsPage />)).toContain('MyMediaRequestsEmpty');
    });

    it('preserves request grids and exposes a retry warning when refresh fails', () => {
        useClassifiedRequests.mockReturnValue({
            pending: [{ Id: 20, Name: 'Solicitação de mídia: Resultado salvo', Overview: 'Series' }],
            included: [],
            priorityRequestIds: new Set<number>(),
            queueStatuses: new Map(),
            isPending: false,
            isError: true,
            hasData: true,
            refetch: vi.fn()
        });

        const markup = renderToStaticMarkup(<MyMediaRequestsPage />);

        expect(markup).toContain('Resultado salvo');
        expect(markup).toContain('OfflineModeWarning');
        expect(markup).toContain('Retry');
    });
});
