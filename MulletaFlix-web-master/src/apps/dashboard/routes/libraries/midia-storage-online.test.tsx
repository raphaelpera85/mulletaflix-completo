import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { useActionDataMock, useNavigationMock, toApiMock, getNamedConfigurationMock, getTasksMock } = vi.hoisted(() => ({
    useActionDataMock: vi.fn(),
    useNavigationMock: vi.fn(),
    toApiMock: vi.fn(),
    getNamedConfigurationMock: vi.fn(),
    getTasksMock: vi.fn()
}));

vi.mock('react-router-dom', async (importOriginal) => ({
    ...await importOriginal<typeof import('react-router-dom')>(),
    Form: ({ children }: { children: React.ReactNode }) => React.createElement('form', null, children),
    useActionData: useActionDataMock,
    useNavigation: useNavigationMock
}));
vi.mock('components/Page', () => ({
    default: ({ children }: React.PropsWithChildren) => <main>{children}</main>
}));
vi.mock('components/loading/LoadingComponent', () => ({
    default: () => <div data-testid='loading'>Loading</div>
}));
vi.mock('components/toast/toast', () => ({ default: vi.fn() }));
vi.mock('lib/globalize', () => ({
    default: { translate: (key: string) => ({
        HeaderServerUnavailable: 'Servidor indisponível',
        Retry: 'Tentar novamente',
        HeaderError: 'Erro',
        SettingsSaved: 'Configurações salvas'
    }[key] ?? key) }
}));
vi.mock('utils/jellyfin-apiclient/compat', () => ({ toApi: toApiMock }));
vi.mock('@jellyfin/sdk/lib/utils/api/configuration-api', () => ({
    getConfigurationApi: () => ({ getNamedConfiguration: getNamedConfigurationMock })
}));
vi.mock('@jellyfin/sdk/lib/utils/api/scheduled-tasks-api', () => ({
    getScheduledTasksApi: () => ({ getTasks: getTasksMock })
}));

import { ApiProvider } from 'hooks/useApi';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import events from 'utils/events';
import { Component as MidiaStorageOnlinePage } from './midia-storage-online';

describe('MidiaStorageOnlinePage API connection state', () => {
    let container: HTMLDivElement;
    let root: Root;
    let client: QueryClient;
    const api = {
        basePath: 'http://jellyfin.test',
        accessToken: 'test-token',
        subscribe: vi.fn(() => vi.fn())
    };
    const legacyApiClient = {};

    beforeEach(() => {
        vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
        vi.spyOn(ServerConnections, 'currentApiClient').mockReturnValue(undefined);
        vi.spyOn(ServerConnections, 'getApiClient').mockReturnValue(legacyApiClient as never);
        toApiMock.mockReturnValue(api);
        getNamedConfigurationMock.mockResolvedValue({ data: {} });
        getTasksMock.mockResolvedValue({ data: [] });
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
            ok: true,
            json: async () => ({})
        }));
        useActionDataMock.mockReturnValue(undefined);
        useNavigationMock.mockReturnValue({ state: 'idle' });
        container = document.createElement('div');
        document.body.append(container);
        root = createRoot(container);
        client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 60_000 } } });
    });

    afterEach(async () => {
        await act(async () => root.unmount());
        client.clear();
        container.remove();
        vi.restoreAllMocks();
        vi.clearAllMocks();
        vi.unstubAllGlobals();
    });

    it('shows an explicit connection status instead of an infinite spinner or editable empty form', async () => {
        await act(async () => root.render(
            <QueryClientProvider client={client}>
                <ApiProvider><MidiaStorageOnlinePage /></ApiProvider>
            </QueryClientProvider>
        ));

        expect(container.textContent).toContain('Aguardando conexão com o servidor');
        expect(container.querySelector('[role="status"]')).not.toBeNull();
        expect(container.querySelector('[data-testid="loading"]')).toBeNull();
        expect(container.querySelector('form')).toBeNull();
        expect(container.textContent).not.toContain('Pesquisar pasta');
    });

    it('recovers from the connection status after ApiProvider receives a sign-in event', async () => {
        await act(async () => root.render(
            <QueryClientProvider client={client}>
                <ApiProvider><MidiaStorageOnlinePage /></ApiProvider>
            </QueryClientProvider>
        ));

        expect(container.textContent).toContain('Aguardando conexão com o servidor');

        await act(async () => {
            events.trigger(ServerConnections, 'localusersignedin', [{
                Id: 'user-id',
                Name: 'test-user',
                ServerId: 'server-id'
            }]);

            for (let attempt = 0; attempt < 10; attempt++) {
                await new Promise(resolve => setTimeout(resolve, 0));
            }
        });

        await act(async () => new Promise(resolve => setTimeout(resolve, 0)));

        expect(toApiMock).toHaveBeenCalledWith(legacyApiClient);
        expect(getNamedConfigurationMock).toHaveBeenCalled();
        expect(getTasksMock).toHaveBeenCalled();
        expect(globalThis.fetch).toHaveBeenCalledWith(
            'http://jellyfin.test/MidiaStorageOnline/status',
            expect.objectContaining({
                headers: { Authorization: 'MediaBrowser Token="test-token"' }
            })
        );
        expect(container.textContent).not.toContain('Aguardando conexão com o servidor');
        expect(container.querySelector('[data-testid="loading"]')).toBeNull();
        expect(container.querySelector('form')).not.toBeNull();
    });
});
