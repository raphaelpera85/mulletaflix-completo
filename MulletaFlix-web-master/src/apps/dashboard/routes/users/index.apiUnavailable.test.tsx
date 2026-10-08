import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { useUsersMock, useDeleteUserMock } = vi.hoisted(() => ({ useUsersMock: vi.fn(), useDeleteUserMock: vi.fn() }));

vi.mock('hooks/useUsers', () => ({ useUsers: useUsersMock }));
vi.mock('apps/dashboard/features/users/api/useDeleteUser', () => ({ useDeleteUser: useDeleteUserMock }));
vi.mock('../../../../components/dashboard/users/UserCardBox', () => ({ default: () => null }));
vi.mock('../../../../elements/SectionTitleContainer', () => ({ default: ({ btnId }: { btnId: string }) => <button id={btnId}>Adicionar</button> }));
vi.mock('apps/dashboard/components/Toast', () => ({ default: () => null }));
vi.mock('components/Page', () => ({ default: ({ children, title }: React.PropsWithChildren<{ title?: string }>) => <main><h1>{title}</h1>{children}</main> }));
vi.mock('components/loading/LoadingComponent', () => ({ default: () => <div role='progressbar'>Loading</div> }));
vi.mock('components/confirm/confirm', () => ({ default: vi.fn() }));
vi.mock('lib/globalize', () => ({ default: { translate: (key: string) => ({
    HeaderUsers: 'Usuários',
    HeaderServerUnavailable: 'Servidor indisponível'
}[key] ?? key) } }));

import { MemoryRouter } from 'react-router-dom';
import { ApiContext } from 'hooks/useApi';
import UsersPage from './index';

describe('users list route without API connection', () => {
    let container: HTMLDivElement;
    let root: Root;
    let queryClient: QueryClient;

    beforeEach(() => {
        vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
        useUsersMock.mockReturnValue({ data: undefined, isPending: true, isError: false, refetch: vi.fn() });
        useDeleteUserMock.mockReturnValue({ mutate: vi.fn() });
        queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 60_000 } } });
        container = document.createElement('div');
        document.body.append(container);
        root = createRoot(container);
    });

    afterEach(async () => {
        await act(async () => root.unmount());
        queryClient.clear();
        container.remove();
        vi.clearAllMocks();
        vi.unstubAllGlobals();
    });

    const renderPage = (connected: boolean) => act(() => root.render(
        <QueryClientProvider client={queryClient}>
            <MemoryRouter>
                <ApiContext.Provider value={connected ? { api: { basePath: 'http://server.test' } as never } : {}}>
                    <UsersPage />
                </ApiContext.Provider>
            </MemoryRouter>
        </QueryClientProvider>
    ));

    it('does not run list DOM effects while offline and restores the user list after connection', () => {
        renderPage(false);

        expect(container.querySelector('[role="status"]')?.textContent).toContain('Servidor indisponível');
        expect(container.querySelector('[role="progressbar"]')).toBeNull();
        expect(container.querySelector('#btnAddUser')).toBeNull();

        useUsersMock.mockReturnValue({ data: [], isPending: false, isError: false, refetch: vi.fn() });
        renderPage(true);

        expect(container.textContent).toContain('Usuários');
        expect(container.querySelector('#btnAddUser')).not.toBeNull();
        expect(container.querySelector('[role="status"]')).toBeNull();
    });
});
