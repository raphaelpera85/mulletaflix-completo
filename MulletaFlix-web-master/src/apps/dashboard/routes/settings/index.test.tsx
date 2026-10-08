import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { getConfigurationMock, getLocalizationOptionsMock, toApiMock, useActionDataMock, useNavigationMock } = vi.hoisted(() => ({
    getConfigurationMock: vi.fn(),
    getLocalizationOptionsMock: vi.fn(),
    toApiMock: vi.fn(),
    useActionDataMock: vi.fn(),
    useNavigationMock: vi.fn()
}));

vi.mock('@jellyfin/sdk/lib/utils/api/configuration-api', () => ({
    getConfigurationApi: () => ({ getConfiguration: getConfigurationMock })
}));
vi.mock('@jellyfin/sdk/lib/utils/api/localization-api', () => ({
    getLocalizationApi: () => ({ getLocalizationOptions: getLocalizationOptionsMock })
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
vi.mock('components/directorybrowser/directorybrowser', () => ({
    default: class DirectoryBrowser {}
}));
vi.mock('lib/globalize', () => ({
    default: { translate: (key: string) => ({
        HeaderServerUnavailable: 'Servidor indisponível',
        General: 'Geral',
        Settings: 'Configurações',
        LabelServerName: 'Nome do servidor',
        LabelServerNameHelp: 'Nome apresentado',
        LabelPreferredDisplayLanguage: 'Idioma de exibição',
        LabelDisplayLanguageHelp: 'Idioma da interface',
        LearnHowYouCanContribute: 'Contribua',
        HeaderPaths: 'Caminhos',
        LabelCachePath: 'Caminho do cache',
        LabelCachePathHelp: 'Diretório do cache',
        LabelMetadataPath: 'Caminho dos metadados',
        LabelMetadataPathHelp: 'Diretório dos metadados',
        HeaderSelectServerCachePath: 'Selecionar cache',
        HeaderSelectServerCachePathHelp: 'Selecione o cache',
        HeaderSelectMetadataPath: 'Selecionar metadados',
        HeaderSelectMetadataPathHelp: 'Selecione metadados',
        QuickConnect: 'Quick Connect',
        EnableQuickConnect: 'Ativar Quick Connect',
        HeaderPerformance: 'Desempenho',
        LibraryScanFanoutConcurrency: 'Concorrência da biblioteca',
        LabelLibraryScanFanoutConcurrencyHelp: 'Limite de concorrência',
        LabelParallelImageEncodingLimit: 'Limite de imagens',
        LabelParallelImageEncodingLimitHelp: 'Limite de codificação',
        Save: 'Salvar',
        SettingsSaved: 'Configurações salvas'
    }[key] ?? key) }
}));

import { ApiProvider } from 'hooks/useApi';
import { Component as GeneralSettingsPage } from './index';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import events from 'utils/events';

describe('GeneralSettingsPage API connection state', () => {
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
            ServerName: 'MulletaFlix',
            UICulture: 'pt-BR',
            CachePath: 'C:\\Cache',
            MetadataPath: 'C:\\Metadata',
            QuickConnectAvailable: true,
            LibraryScanFanoutConcurrency: 2,
            ParallelImageEncodingLimit: 3
        } });
        getLocalizationOptionsMock.mockResolvedValue({ data: [{ Name: 'Português', Value: 'pt-BR' }] });
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
            <ApiProvider><GeneralSettingsPage /></ApiProvider>
        </QueryClientProvider>
    );

    it('shows accessible server-unavailable state and hides the form without API', async () => {
        await act(async () => root.render(renderPage()));

        expect(container.textContent).toContain('Servidor indisponível');
        expect(container.querySelector('[role="status"]')).not.toBeNull();
        expect(container.querySelector('[data-testid="loading"]')).toBeNull();
        expect(container.querySelector('form')).toBeNull();
        expect(getConfigurationMock).not.toHaveBeenCalled();
        expect(getLocalizationOptionsMock).not.toHaveBeenCalled();
    });

    it('loads general settings after ApiProvider receives a sign-in event', async () => {
        await act(async () => root.render(renderPage()));
        expect(container.textContent).toContain('Servidor indisponível');

        await act(async () => {
            events.trigger(ServerConnections, 'localusersignedin', [{ Id: 'user-id', Name: 'test-user', ServerId: 'server-id' }]);
            for (let attempt = 0; attempt < 10; attempt++) await new Promise(resolve => setTimeout(resolve, 0));
        });

        expect(toApiMock).toHaveBeenCalledWith(legacyApiClient);
        expect(getConfigurationMock).toHaveBeenCalled();
        expect(getLocalizationOptionsMock).toHaveBeenCalled();
        await act(async () => new Promise(resolve => setTimeout(resolve, 0)));
        expect(container.textContent).toContain('Nome do servidor');
        expect(container.querySelector('[data-testid="loading"]')).toBeNull();
        expect(container.querySelector('form')).not.toBeNull();
    });
});
