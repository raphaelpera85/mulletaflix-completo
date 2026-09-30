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
vi.mock('lib/globalize', () => ({
    default: {
        translate: (key: string) => ({
            MyMediaRequestPriority: 'Prioridade solicitada',
            MyMediaRequestPriorityDescription: 'O título foi registrado como prioritário no Nebula. A posição aparece quando um arquivo compatível entra na fila.'
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
        expect(markup).toContain('O título foi registrado como prioritário no Nebula. A posição aparece quando um arquivo compatível entra na fila.');
        expect(markup).toContain('aria-label="Prioridade solicitada.');
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
});
