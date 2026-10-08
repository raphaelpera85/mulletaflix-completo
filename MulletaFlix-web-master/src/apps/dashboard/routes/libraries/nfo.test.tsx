import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { useNamedConfigurationMock, useUsersMock, useActionDataMock, useNavigationMock, toApiMock } = vi.hoisted(() => ({
    useNamedConfigurationMock: vi.fn(),
    useUsersMock: vi.fn(),
    useActionDataMock: vi.fn(),
    useNavigationMock: vi.fn(),
    toApiMock: vi.fn()
}));

vi.mock('hooks/useNamedConfiguration', () => ({
    [ 'QUERY_KEY' ]: 'NamedConfiguration',
    useNamedConfiguration: useNamedConfigurationMock
}));
vi.mock('hooks/useUsers', () => ({ useUsers: useUsersMock }));
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
vi.mock('components/SimpleAlert', () => ({ default: () => null }));
vi.mock('utils/jellyfin-apiclient/compat', () => ({ toApi: toApiMock }));
vi.mock('lib/globalize', () => ({
    default: { translate: (key: string) => ({
        HeaderServerUnavailable: 'Servidor indisponível',
        TabNfoSettings: 'Configurações NFO',
        HeaderKodiMetadataHelp: 'Metadados Kodi',
        None: 'Nenhum',
        Save: 'Salvar'
    }[key] ?? key) }
}));

import { Component as NfoSettingsPage } from './nfo';
import { ApiProvider } from 'hooks/useApi';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import events from 'utils/events';

describe('NFO settings API connection state', () => {
    let container: HTMLDivElement;
    let root: Root;
    const api = { basePath: 'http://jellyfin.test' };
    const legacyApiClient = {};

    beforeEach(() => {
        vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
        vi.spyOn(ServerConnections, 'currentApiClient').mockReturnValue(undefined);
        vi.spyOn(ServerConnections, 'getApiClient').mockReturnValue(legacyApiClient as never);
        toApiMock.mockReturnValue(api);
        useNamedConfigurationMock.mockReturnValue({
            data: undefined,
            isPending: true,
            isError: false,
            refetch: vi.fn()
        });
        useUsersMock.mockReturnValue({ data: undefined, isPending: true, isError: false });
        useActionDataMock.mockReturnValue(undefined);
        useNavigationMock.mockReturnValue({ state: 'idle' });
        container = document.createElement('div');
        document.body.append(container);
        root = createRoot(container);
    });

    afterEach(async () => {
        await act(async () => root.unmount());
        container.remove();
        vi.restoreAllMocks();
        vi.clearAllMocks();
        vi.unstubAllGlobals();
    });

    it('shows server-unavailable status instead of an endless spinner or empty form without API', async () => {
        await act(async () => root.render(
            <ApiProvider><NfoSettingsPage /></ApiProvider>
        ));

        expect(container.textContent).toContain('Servidor indisponível');
        expect(container.querySelector('[role="status"]')).not.toBeNull();
        expect(container.querySelector('[data-testid="loading"]')).toBeNull();
        expect(container.querySelector('form')).toBeNull();
    });

    it('keeps the settings form available after API data loads', async () => {
        await act(async () => root.render(
            <ApiProvider><NfoSettingsPage /></ApiProvider>
        ));

        expect(container.textContent).toContain('Servidor indisponível');

        useNamedConfigurationMock.mockReturnValue({
            data: {},
            isPending: false,
            isError: false,
            refetch: vi.fn()
        });
        useUsersMock.mockReturnValue({ data: [], isPending: false, isError: false });
        await act(async () => {
            events.trigger(ServerConnections, 'localusersignedin', [{
                Id: 'user-id',
                Name: 'test-user',
                ServerId: 'server-id'
            }]);
            await new Promise(resolve => setTimeout(resolve, 0));
            await new Promise(resolve => setTimeout(resolve, 0));
        });

        expect(toApiMock).toHaveBeenCalledWith(legacyApiClient);
        expect(container.textContent).toContain('Configurações NFO');
        expect(container.querySelector('form')).not.toBeNull();
        expect(container.querySelector('[data-testid="loading"]')).toBeNull();
    });
});
