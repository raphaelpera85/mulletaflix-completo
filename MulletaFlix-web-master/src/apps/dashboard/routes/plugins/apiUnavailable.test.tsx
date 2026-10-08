import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { usePluginDetailsMock, useRepositoriesMock } = vi.hoisted(() => ({
    usePluginDetailsMock: vi.fn(),
    useRepositoriesMock: vi.fn()
}));

vi.mock('apps/dashboard/features/plugins/api/usePluginDetails', () => ({ usePluginDetails: usePluginDetailsMock }));
vi.mock('apps/dashboard/features/plugins/api/useRepositories', () => ({ useRepositories: useRepositoriesMock }));
vi.mock('apps/dashboard/features/plugins/api/useSetRepositories', () => ({ useSetRepositories: () => ({ mutate: vi.fn() }) }));
vi.mock('apps/dashboard/features/plugins/components/NewRepositoryForm', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/plugins/components/RepositoryListItem', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/plugins/components/PluginCard', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/plugins/components/NoPluginResults', () => ({ default: () => <div>Sem plugins</div> }));
vi.mock('apps/dashboard/components/SearchInput', () => ({ default: () => null }));
vi.mock('components/Page', () => ({ default: ({ children }: React.PropsWithChildren) => <main>{children}</main> }));
vi.mock('components/loading/LoadingComponent', () => ({ default: () => <div role='progressbar'>Loading</div> }));
vi.mock('lib/globalize', () => ({ default: { translate: (key: string) => ({
    HeaderServerUnavailable: 'Servidor indisponível',
    TabPlugins: 'Plugins',
    TabRepositories: 'Repositórios',
    ManageRepositories: 'Gerenciar repositórios',
    Search: 'Buscar',
    All: 'Todos',
    LabelAvailable: 'Disponíveis',
    LabelInstalled: 'Instalados',
    LabelCompatible: 'Compatíveis',
    LabelIncompatible: 'Incompatíveis',
    PluginsLoadError: 'Falha ao carregar plugins',
    HeaderNewRepository: 'Novo repositório',
    MessageNoRepositories: 'Nenhum repositório',
    MessageAddRepository: 'Adicione um repositório',
    RepositoriesPageLoadError: 'Falha ao carregar repositórios',
    Retry: 'Tentar novamente'
}[key] ?? key) } }));

import { MemoryRouter } from 'react-router-dom';
import { ApiContext } from 'hooks/useApi';
import { Component as PluginsPage } from './index';
import { Component as RepositoriesPage } from './repositories';

describe('plugin dashboard routes without API connection', () => {
    let container: HTMLDivElement;
    let root: Root;
    let queryClient: QueryClient;

    beforeEach(() => {
        vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
        usePluginDetailsMock.mockReturnValue({ data: undefined, isPending: true, isError: false, refetch: vi.fn() });
        useRepositoriesMock.mockReturnValue({ data: undefined, isPending: true, isError: false, refetch: vi.fn() });
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

    const renderRoute = (route: React.ReactNode, connected: boolean) => {
        const render = (nextRoute: React.ReactNode, hasApi: boolean) => act(() => root.render(
            <QueryClientProvider client={queryClient}>
                <MemoryRouter>
                    <ApiContext.Provider value={hasApi ? { api: { basePath: 'http://server.test' } as never } : {}}>
                        {nextRoute}
                    </ApiContext.Provider>
                </MemoryRouter>
            </QueryClientProvider>
        ));
        render(route, connected);
        return { rerender: (nextRoute: React.ReactNode) => render(nextRoute, true) };
    };

    it('shows an accessible unavailable state and recovers on the plugins page', () => {
        const view = renderRoute(<PluginsPage />, false);

        const status = container.querySelector('[role="status"]');
        expect(status).not.toBeNull();
        expect(status?.textContent).toContain('Servidor indisponível');
        expect(container.querySelector('[role="progressbar"]')).toBeNull();

        usePluginDetailsMock.mockReturnValue({ data: [], isPending: false, isError: false, refetch: vi.fn() });
        view.rerender(<PluginsPage />);

        expect(container.textContent).toContain('Plugins');
        expect(container.textContent).toContain('Sem plugins');
    });

    it('shows an accessible unavailable state and recovers on the repositories page', () => {
        const view = renderRoute(<RepositoriesPage />, false);

        const status = container.querySelector('[role="status"]');
        expect(status).not.toBeNull();
        expect(status?.textContent).toContain('Servidor indisponível');
        expect(container.querySelector('[role="progressbar"]')).toBeNull();

        useRepositoriesMock.mockReturnValue({ data: [], isPending: false, isError: false, refetch: vi.fn() });
        view.rerender(<RepositoriesPage />);

        expect(container.textContent).toContain('Repositórios');
        expect(container.textContent).toContain('Novo repositório');
    });
});
