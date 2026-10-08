import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { useUsersMock, useLogEntriesMock } = vi.hoisted(() => ({ useUsersMock: vi.fn(), useLogEntriesMock: vi.fn() }));

vi.mock('hooks/useUsers', () => ({ useUsers: useUsersMock }));
vi.mock('apps/dashboard/features/activity/api/useLogEntries', () => ({ useLogEntries: useLogEntriesMock }));
vi.mock('apps/dashboard/features/users/api/useUserLicense', () => ({
    fetchUserLicense: vi.fn(),
    ['USER_LICENSE_QUERY_KEY']: 'UserLicense',
    useRevokeUserLicense: () => ({ mutate: vi.fn(), isPending: false }),
    useSetUserLicense: () => ({ mutate: vi.fn(), isPending: false })
}));
vi.mock('components/Page', () => ({ default: ({ children, title }: React.PropsWithChildren<{ title?: string }>) => <main><h1>{title}</h1>{children}</main> }));
vi.mock('components/loading/LoadingComponent', () => ({ default: () => <div role='progressbar'>Loading</div> }));
vi.mock('components/confirm/confirm', () => ({ default: vi.fn() }));
vi.mock('components/toast/toast', () => ({ default: () => null }));

import { ApiContext } from 'hooks/useApi';
import { Component as UserLicensesPage } from './licenses';

describe('user licenses route without API connection', () => {
    let container: HTMLDivElement;
    let root: Root;
    let queryClient: QueryClient;

    beforeEach(() => {
        vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
        useUsersMock.mockReturnValue({ data: undefined, isPending: true, isError: false, refetch: vi.fn() });
        useLogEntriesMock.mockReturnValue({ data: undefined, isPending: true, isError: false, refetch: vi.fn() });
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
            <ApiContext.Provider value={connected ? { api: { basePath: 'http://server.test' } as never } : {}}>
                <UserLicensesPage />
            </ApiContext.Provider>
        </QueryClientProvider>
    ));

    it('announces missing connection instead of loading forever and restores the licenses page after connection', () => {
        renderPage(false);

        expect(container.querySelector('[role="status"]')?.textContent).toContain('Servidor indisponível');
        expect(container.querySelector('[role="progressbar"]')).toBeNull();

        useUsersMock.mockReturnValue({ data: [], isPending: false, isError: false, refetch: vi.fn() });
        useLogEntriesMock.mockReturnValue({ data: { Items: [] }, isPending: false, isError: false, refetch: vi.fn() });
        renderPage(true);

        expect(container.textContent).toContain('Licenças');
        expect(container.querySelector('[role="status"]')).toBeNull();
    });
});
