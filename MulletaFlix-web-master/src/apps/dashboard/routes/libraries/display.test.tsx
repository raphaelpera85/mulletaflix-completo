import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { getConfigurationMock, getNamedConfigurationMock, toApiMock, useActionDataMock, useNavigationMock } = vi.hoisted(() => ({
    getConfigurationMock: vi.fn(),
    getNamedConfigurationMock: vi.fn(),
    toApiMock: vi.fn(),
    useActionDataMock: vi.fn(),
    useNavigationMock: vi.fn()
}));

vi.mock('@jellyfin/sdk/lib/utils/api/configuration-api', () => ({
    getConfigurationApi: () => ({
        getConfiguration: getConfigurationMock,
        getNamedConfiguration: getNamedConfigurationMock
    })
}));
vi.mock('utils/jellyfin-apiclient/compat', () => ({ toApi: toApiMock }));
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
vi.mock('lib/globalize', () => ({
    default: { translate: (key: string) => ({
        HeaderServerUnavailable: 'Servidor indisponível',
        Display: 'Exibição',
        LabelDateAddedBehavior: 'Data de inclusão',
        LabelDateAddedBehaviorHelp: 'Como exibir a data',
        OptionDateAddedImportTime: 'Data de importação',
        OptionDateAddedFileTime: 'Data do arquivo',
        OptionDisplayFolderView: 'Exibir pastas',
        OptionDisplayFolderViewHelp: 'Ajuda pastas',
        LabelDisplaySpecialsWithinSeasons: 'Especiais nas temporadas',
        LabelGroupMoviesIntoCollections: 'Agrupar filmes',
        LabelGroupMoviesIntoCollectionsHelp: 'Ajuda filmes',
        LabelGroupShowsIntoCollections: 'Agrupar séries',
        LabelGroupShowsIntoCollectionsHelp: 'Ajuda séries',
        OptionEnableExternalContentInSuggestions: 'Conteúdo externo',
        Save: 'Salvar'
    }[key] ?? key) }
}));

import { ApiProvider } from 'hooks/useApi';
import { Component as DisplaySettingsPage } from './display';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import events from 'utils/events';

describe('DisplaySettingsPage API connection state', () => {
    let container: HTMLDivElement;
    let root: Root;
    let queryClient: QueryClient;
    const api = { basePath: 'http://jellyfin.test' };
    const legacyApiClient = {};

    beforeEach(() => {
        vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
        vi.spyOn(ServerConnections, 'currentApiClient').mockReturnValue(undefined);
        vi.spyOn(ServerConnections, 'getApiClient').mockReturnValue(legacyApiClient as never);
        toApiMock.mockReturnValue(api);
        getConfigurationMock.mockResolvedValue({ data: {
            EnableFolderView: true,
            DisplaySpecialsWithinSeasons: false,
            EnableGroupingMoviesIntoCollections: false,
            EnableGroupingShowsIntoCollections: false,
            EnableExternalContentInSuggestions: false
        } });
        getNamedConfigurationMock.mockResolvedValue({ data: { UseFileCreationTimeForDateAdded: false } });
        useActionDataMock.mockReturnValue(undefined);
        useNavigationMock.mockReturnValue({ state: 'idle' });
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
            <ApiProvider><DisplaySettingsPage /></ApiProvider>
        </QueryClientProvider>
    );

    it('shows accessible server-unavailable state instead of endless loading without API', async () => {
        await act(async () => root.render(renderPage()));

        expect(container.textContent).toContain('Servidor indisponível');
        expect(container.querySelector('[role="status"]')).not.toBeNull();
        expect(container.querySelector('[data-testid="loading"]')).toBeNull();
        expect(container.querySelector('form')).toBeNull();
        expect(getConfigurationMock).not.toHaveBeenCalled();
        expect(getNamedConfigurationMock).not.toHaveBeenCalled();
    });

    it('loads display settings after ApiProvider receives a sign-in event', async () => {
        await act(async () => root.render(renderPage()));
        expect(container.textContent).toContain('Servidor indisponível');

        await act(async () => {
            events.trigger(ServerConnections, 'localusersignedin', [{ Id: 'user-id', Name: 'test-user', ServerId: 'server-id' }]);
            for (let attempt = 0; attempt < 10; attempt++) await new Promise(resolve => setTimeout(resolve, 0));
        });

        expect(toApiMock).toHaveBeenCalledWith(legacyApiClient);
        expect(getConfigurationMock).toHaveBeenCalled();
        expect(getNamedConfigurationMock).toHaveBeenCalledWith({ key: 'metadata' }, expect.anything());
        await act(async () => new Promise(resolve => setTimeout(resolve, 0)));
        expect(container.textContent).toContain('Data de inclusão');
        expect(container.querySelector('[data-testid="loading"]')).toBeNull();
        expect(container.querySelector('form')).not.toBeNull();
    });
});
