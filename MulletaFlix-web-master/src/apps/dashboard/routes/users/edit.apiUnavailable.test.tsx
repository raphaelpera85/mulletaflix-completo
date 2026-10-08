import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { useUserMock } = vi.hoisted(() => ({ useUserMock: vi.fn() }));

vi.mock('hooks/api/useUser', () => ({ useUser: useUserMock }));
vi.mock('apps/dashboard/features/users/components/Profile', () => ({ default: () => <div>Perfil do usuário</div> }));
vi.mock('apps/dashboard/features/users/components/Access', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/users/components/ParentalControl', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/users/components/Password', () => ({ default: () => null }));
vi.mock('components/Page', () => ({ default: ({ children }: React.PropsWithChildren) => <main>{children}</main> }));
vi.mock('components/loading/LoadingComponent', () => ({ default: () => <div role='progressbar'>Loading</div> }));
vi.mock('lib/globalize', () => ({ default: { translate: (key: string) => key === 'Profile' ? 'Perfil' : key } }));

import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { UserTab } from 'apps/dashboard/features/users/constants/userTab';
import { ApiContext } from 'hooks/useApi';
import { Component as UserEditPage } from './edit';

describe('user edit route without API connection', () => {
    let container: HTMLDivElement;
    let root: Root;
    let queryClient: QueryClient;

    beforeEach(() => {
        vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
        useUserMock.mockReturnValue({ data: undefined, isPending: true, isError: false, refetch: vi.fn() });
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
            <MemoryRouter initialEntries={[`/dashboard/users/user-1/${UserTab.Profile}`]}>
                <ApiContext.Provider value={connected ? { api: { basePath: 'http://server.test' } as never } : {}}>
                    <Routes><Route path='/dashboard/users/:userId/:tab' element={<UserEditPage />} /></Routes>
                </ApiContext.Provider>
            </MemoryRouter>
        </QueryClientProvider>
    ));

    it('announces missing connection and restores the user profile after reconnecting', () => {
        renderPage(false);

        expect(container.querySelector('[role="status"]')?.textContent).toContain('Servidor indisponível');
        expect(container.querySelector('[role="progressbar"]')).toBeNull();

        useUserMock.mockReturnValue({ data: { Id: 'user-1', Name: 'Raphael' }, isPending: false, isError: false, refetch: vi.fn() });
        renderPage(true);

        expect(container.textContent).toContain('Raphael');
        expect(container.textContent).toContain('Perfil do usuário');
        expect(container.querySelector('[role="status"]')).toBeNull();
    });
});
