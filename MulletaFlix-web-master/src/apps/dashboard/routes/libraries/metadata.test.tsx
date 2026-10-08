import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const {
    getConfigurationMock,
    getCulturesMock,
    getCountriesMock,
    toApiMock,
    useActionDataMock,
    useNavigationMock
} = vi.hoisted(() => ({
    getConfigurationMock: vi.fn(),
    getCulturesMock: vi.fn(),
    getCountriesMock: vi.fn(),
    toApiMock: vi.fn(),
    useActionDataMock: vi.fn(),
    useNavigationMock: vi.fn()
}));

vi.mock('@jellyfin/sdk/lib/utils/api/configuration-api', () => ({
    getConfigurationApi: () => ({
        getConfiguration: getConfigurationMock
    })
}));
vi.mock('@jellyfin/sdk/lib/utils/api/localization-api', () => ({
    getLocalizationApi: () => ({
        getCultures: getCulturesMock,
        getCountries: getCountriesMock
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
        LabelMetadata: 'Metadados',
        HeaderPreferredMetadataLanguage: 'Idioma preferido',
        DefaultMetadataLangaugeDescription: 'Idioma usado nos metadados',
        HeaderDummyChapter: 'Capítulos',
        LabelLanguage: 'Idioma',
        LabelCountry: 'País',
        LabelDummyChapterDuration: 'Duração',
        LabelDummyChapterDurationHelp: 'Duração do capítulo',
        LabelChapterImageResolution: 'Resolução da imagem',
        LabelChapterImageResolutionHelp: 'Resolução',
        Save: 'Salvar'
    }[key] ?? key) }
}));

import { ApiProvider } from 'hooks/useApi';
import { Component as MetadataSettingsPage } from './metadata';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import events from 'utils/events';

describe('MetadataSettingsPage API connection state', () => {
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
            PreferredMetadataLanguage: 'pt-BR',
            MetadataCountryCode: 'BR',
            DummyChapterDuration: 5,
            ChapterImageResolution: 'MatchSource'
        } });
        getCulturesMock.mockResolvedValue({ data: [{ Name: 'pt-BR', DisplayName: 'Português' }] });
        getCountriesMock.mockResolvedValue({ data: [{ DisplayName: 'Brasil', TwoLetterISORegionName: 'BR' }] });
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
            <ApiProvider><MetadataSettingsPage /></ApiProvider>
        </QueryClientProvider>
    );

    it('shows an accessible unavailable state instead of an endless spinner or empty form without API', async () => {
        await act(async () => root.render(renderPage()));

        expect(container.textContent).toContain('Servidor indisponível');
        expect(container.querySelector('[role="status"]')).not.toBeNull();
        expect(container.querySelector('[data-testid="loading"]')).toBeNull();
        expect(container.querySelector('form')).toBeNull();
        expect(getConfigurationMock).not.toHaveBeenCalled();
        expect(getCulturesMock).not.toHaveBeenCalled();
        expect(getCountriesMock).not.toHaveBeenCalled();
    });

    it('loads the metadata form after ApiProvider receives a sign-in event', async () => {
        await act(async () => root.render(renderPage()));
        expect(container.textContent).toContain('Servidor indisponível');

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

        expect(toApiMock).toHaveBeenCalledWith(legacyApiClient);
        expect(getConfigurationMock).toHaveBeenCalled();
        expect(getCulturesMock).toHaveBeenCalled();
        expect(getCountriesMock).toHaveBeenCalled();
        await act(async () => new Promise(resolve => setTimeout(resolve, 0)));
        expect(container.textContent).toContain('Idioma preferido');
        expect(container.querySelector('[data-testid="loading"]')).toBeNull();
        expect(container.querySelector('form')).not.toBeNull();
    });
});
