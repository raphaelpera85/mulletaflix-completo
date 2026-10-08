import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { getVirtualFoldersMock, getTasksMock, toApiMock } = vi.hoisted(() => ({
    getVirtualFoldersMock: vi.fn(),
    getTasksMock: vi.fn(),
    toApiMock: vi.fn()
}));

vi.mock('@jellyfin/sdk/lib/utils/api/library-structure-api', () => ({
    getLibraryStructureApi: () => ({ getVirtualFolders: getVirtualFoldersMock })
}));
vi.mock('@jellyfin/sdk/lib/utils/api/scheduled-tasks-api', () => ({
    getScheduledTasksApi: () => ({ getTasks: getTasksMock })
}));
vi.mock('utils/jellyfin-apiclient/compat', () => ({ toApi: toApiMock }));
vi.mock('apps/dashboard/features/tasks/api/useStartTask', () => ({
    useStartTask: () => ({ mutate: vi.fn() })
}));
vi.mock('apps/dashboard/features/libraries/components/LibraryCard', () => ({
    default: () => null
}));
vi.mock('apps/dashboard/features/tasks/components/TaskProgress', () => ({
    default: () => null
}));
vi.mock('components/mediaLibraryCreator/mediaLibraryCreator', () => ({
    default: class MediaLibraryCreator {}
}));
vi.mock('components/Page', () => ({
    default: ({ children }: React.PropsWithChildren) => <main>{children}</main>
}));
vi.mock('components/loading/LoadingComponent', () => ({
    default: () => <div data-testid='loading'>Loading</div>
}));
vi.mock('lib/globalize', () => ({
    default: { translate: (key: string) => ({
        HeaderServerUnavailable: 'Servidor indisponível',
        HeaderLibraries: 'Bibliotecas',
        LibrariesLoadError: 'Falha ao carregar bibliotecas',
        Retry: 'Tentar novamente',
        ButtonAddMediaLibrary: 'Adicionar biblioteca',
        ButtonScanAllLibraries: 'Verificar bibliotecas'
    }[key] ?? key) }
}));

import { ApiProvider } from 'hooks/useApi';
import { Component as LibrariesPage } from './index';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import events from 'utils/events';

describe('LibrariesPage API connection state', () => {
    let container: HTMLDivElement;
    let root: Root;
    let queryClient: QueryClient;
    const api = { basePath: 'http://jellyfin.test', subscribe: vi.fn(() => vi.fn()) };
    const legacyApiClient = {};

    beforeEach(() => {
        vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
        vi.spyOn(ServerConnections, 'currentApiClient').mockReturnValue(undefined);
        vi.spyOn(ServerConnections, 'getApiClient').mockReturnValue(legacyApiClient as never);
        toApiMock.mockReturnValue(api);
        getVirtualFoldersMock.mockResolvedValue({ data: [] });
        getTasksMock.mockResolvedValue({ data: [] });
        queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 60_000 } } });
        container = document.createElement('div');
        document.body.append(container);
        root = createRoot(container);
    });

    afterEach(async () => {
        await act(async () => root.unmount());
        queryClient.clear();
        container.remove();
        vi.restoreAllMocks();
        vi.clearAllMocks();
        vi.unstubAllGlobals();
    });

    const renderPage = () => (
        <QueryClientProvider client={queryClient}>
            <ApiProvider><LibrariesPage /></ApiProvider>
        </QueryClientProvider>
    );

    it('shows an accessible unavailable state instead of endless loading without API', async () => {
        await act(async () => root.render(renderPage()));

        expect(container.textContent).toContain('Servidor indisponível');
        expect(container.querySelector('[role="status"]')).not.toBeNull();
        expect(container.querySelector('[data-testid="loading"]')).toBeNull();
        expect(container.textContent).not.toContain('Adicionar biblioteca');
        expect(getVirtualFoldersMock).not.toHaveBeenCalled();
        expect(getTasksMock).not.toHaveBeenCalled();
    });

    it('loads the libraries page after ApiProvider receives a sign-in event', async () => {
        await act(async () => root.render(renderPage()));
        expect(container.textContent).toContain('Servidor indisponível');

        await act(async () => {
            events.trigger(ServerConnections, 'localusersignedin', [{ Id: 'user-id', Name: 'test-user', ServerId: 'server-id' }]);
            for (let attempt = 0; attempt < 10; attempt++) await new Promise(resolve => setTimeout(resolve, 0));
        });

        expect(toApiMock).toHaveBeenCalledWith(legacyApiClient);
        expect(getVirtualFoldersMock).toHaveBeenCalled();
        expect(getTasksMock).toHaveBeenCalled();
        await act(async () => new Promise(resolve => setTimeout(resolve, 0)));
        expect(container.textContent).toContain('Adicionar biblioteca');
        expect(container.querySelector('[data-testid="loading"]')).toBeNull();
        expect(container.querySelector('form')).toBeNull();
    });
});
