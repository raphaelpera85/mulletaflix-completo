import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { useConfigurationMock, useNamedConfigurationMock, useLiveTasksMock, useActionDataMock, useNavigationMock } = vi.hoisted(() => ({
    useConfigurationMock: vi.fn(),
    useNamedConfigurationMock: vi.fn(),
    useLiveTasksMock: vi.fn(),
    useActionDataMock: vi.fn(),
    useNavigationMock: vi.fn()
}));

vi.mock('hooks/useConfiguration', () => ({ ['QUERY_KEY']: 'Configuration', useConfiguration: useConfigurationMock }));
vi.mock('hooks/useNamedConfiguration', () => ({ useNamedConfiguration: useNamedConfigurationMock }));
vi.mock('apps/dashboard/features/tasks/hooks/useLiveTasks', () => ({ default: useLiveTasksMock }));
vi.mock('apps/dashboard/features/tasks/api/useStartTask', () => ({ useStartTask: () => ({ mutate: vi.fn() }) }));
vi.mock('react-router-dom', async (importOriginal) => ({
    ...await importOriginal<typeof import('react-router-dom')>(),
    Form: ({ children }: React.PropsWithChildren) => <form>{children}</form>,
    Link: ({ children, to }: React.PropsWithChildren<{ to: string }>) => <a href={to}>{children}</a>,
    useActionData: useActionDataMock,
    useNavigation: useNavigationMock,
    useSubmit: () => vi.fn()
}));
vi.mock('@jellyfin/sdk/lib/utils/api/configuration-api', () => ({ getConfigurationApi: () => ({
    getConfiguration: vi.fn(),
    getNamedConfiguration: vi.fn(),
    updateConfiguration: vi.fn()
}) }));
vi.mock('components/Page', () => ({ default: ({ children }: React.PropsWithChildren) => <main>{children}</main> }));
vi.mock('components/loading/LoadingComponent', () => ({ default: () => <div role='progressbar'>Loading</div> }));
vi.mock('apps/dashboard/features/tasks/components/TaskProgress', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/livetv/components/Provider', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/livetv/components/TunerDeviceCard', () => ({ default: () => null }));
vi.mock('components/ConfirmDialog', () => ({ default: () => null }));
vi.mock('lib/globalize', () => ({ default: { translate: (key: string) => ({
    HeaderServerUnavailable: 'Servidor indisponível',
    HeaderError: 'Falha ao carregar',
    Retry: 'Tentar novamente',
    ButtonResume: 'Retomar reprodução',
    LabelMinResumePercentage: 'Percentual mínimo',
    LabelMaxResumePercentage: 'Percentual máximo',
    LabelMinAudiobookResume: 'Audiobook mínimo',
    LabelMaxAudiobookResume: 'Audiobook máximo',
    LabelMinResumeDuration: 'Duração mínima',
    LabelMinResumePercentageHelp: 'Ajuda',
    LabelMaxResumePercentageHelp: 'Ajuda',
    LabelMinAudiobookResumeHelp: 'Ajuda',
    LabelMaxAudiobookResumeHelp: 'Ajuda',
    LabelMinResumeDurationHelp: 'Ajuda',
    Save: 'Salvar',
    SettingsSaved: 'Configurações salvas',
    ResumeLoadError: 'Falha ao carregar retomada',
    TabStreaming: 'Streaming',
    StreamingLoadError: 'Falha ao carregar streaming',
    LiveTVPageLoadError: 'Falha ao carregar gravações',
    HeaderDVR: 'Gravações',
    TitlePlayback: 'Reprodução',
    Transcoding: 'Transcodificação',
    Trickplay: 'Trickplay',
    ErrorDefault: 'Falha padrão',
    LabelRemoteClientBitrateLimit: 'Limite de bitrate',
    LabelRemoteClientBitrateLimitHelp: 'Ajuda',
    LiveTV: 'TV ao vivo',
    HeaderTunerDevices: 'Sintonizadores',
    HeaderGuideProviders: 'Provedores de guia',
    ButtonAddTunerDevice: 'Adicionar sintonizador',
    ButtonAddProvider: 'Adicionar provedor',
    ButtonRefreshGuideData: 'Atualizar guia'
}[key] ?? key) } }));

import { MemoryRouter } from 'react-router-dom';
import { ApiContext } from 'hooks/useApi';
import { Component as LiveTvPage } from '../livetv';
import { Component as ResumePage } from './resume';
import { Component as StreamingPage } from './streaming';
import { Component as TranscodingPage } from './transcoding';
import { Component as TrickplayPage } from './trickplay';
import { Component as RecordingSettingsPage } from '../livetv/recordings';

const configuration = {
    MinResumePct: 5,
    MaxResumePct: 90,
    MinAudiobookResume: 5,
    MaxAudiobookResume: 90,
    MinResumeDurationSeconds: 300,
    RemoteClientBitrateLimit: 10_000_000
};

describe('dashboard routes without API connection', () => {
    let container: HTMLDivElement;
    let root: Root;
    let queryClient: QueryClient;

    beforeEach(() => {
        vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
        useActionDataMock.mockReturnValue(undefined);
        useNavigationMock.mockReturnValue({ state: 'idle' });
        useConfigurationMock.mockReturnValue({ isPending: true, isError: false, data: undefined, refetch: vi.fn() });
        useNamedConfigurationMock.mockReturnValue({ isPending: true, isError: false, data: undefined, refetch: vi.fn() });
        useLiveTasksMock.mockReturnValue({ isPending: true, isError: false, data: undefined, refetch: vi.fn() });
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

    const renderRoute = (component: React.ReactNode, connected: boolean) => {
        const view = (
            <QueryClientProvider client={queryClient}>
                <MemoryRouter>
                    <ApiContext.Provider value={connected ? { api: { basePath: 'http://server.test' } as never } : {}}>
                        {component}
                    </ApiContext.Provider>
                </MemoryRouter>
            </QueryClientProvider>
        );
        act(() => root.render(view));
        return {
            container,
            rerender: (next: React.ReactNode) => act(() => root.render(
                <QueryClientProvider client={queryClient}>
                    <MemoryRouter>
                        <ApiContext.Provider value={{ api: { basePath: 'http://server.test' } as never }}>{next}</ApiContext.Provider>
                    </MemoryRouter>
                </QueryClientProvider>
            ))
        };
    };

    it('shows unavailable state for Live TV and renders it after API connection', () => {
        const view = renderRoute(<LiveTvPage />, false);
        const status = view.container.querySelector('[role="status"]');
        expect(status).not.toBeNull();
        expect(status?.textContent).toContain('Servidor indisponível');
        expect(view.container.querySelector('[role="progressbar"]')).toBeNull();
        expect(view.container.textContent).not.toContain('Adicionar sintonizador');

        useNamedConfigurationMock.mockReturnValue({ isPending: false, isError: false, data: { TunerHosts: [], ListingProviders: [] }, refetch: vi.fn() });
        useLiveTasksMock.mockReturnValue({ isPending: false, isError: false, data: [], refetch: vi.fn() });
        view.rerender(<LiveTvPage />);
        expect(view.container.textContent).toContain('Adicionar sintonizador');
    });

    it('shows unavailable state for resume settings and restores the form after API connection', () => {
        const view = renderRoute(<ResumePage />, false);
        const status = view.container.querySelector('[role="status"]');
        expect(status).not.toBeNull();
        expect(status?.textContent).toContain('Servidor indisponível');
        expect(view.container.querySelector('[role="progressbar"]')).toBeNull();
        expect(view.container.textContent).not.toContain('Salvar');

        useConfigurationMock.mockReturnValue({ isPending: false, isError: false, data: configuration, refetch: vi.fn() });
        view.rerender(<ResumePage />);
        expect(view.container.textContent).toContain('Salvar');
    });

    it('shows unavailable state for streaming settings and restores the form after API connection', () => {
        const view = renderRoute(<StreamingPage />, false);
        const status = view.container.querySelector('[role="status"]');
        expect(status).not.toBeNull();
        expect(status?.textContent).toContain('Servidor indisponível');
        expect(view.container.querySelector('[role="progressbar"]')).toBeNull();
        expect(view.container.textContent).not.toContain('Salvar');

        useConfigurationMock.mockReturnValue({ isPending: false, isError: false, data: configuration, refetch: vi.fn() });
        view.rerender(<StreamingPage />);
        expect(view.container.textContent).toContain('Salvar');
    });

    it('shows unavailable state for transcoding and restores the settings after API connection', () => {
        const view = renderRoute(<TranscodingPage />, false);
        const status = view.container.querySelector('[role="status"]');
        expect(status).not.toBeNull();
        expect(status?.textContent).toContain('Servidor indisponível');
        expect(view.container.querySelector('[role="progressbar"]')).toBeNull();
        expect(view.container.textContent).not.toContain('Transcodificação');

        useNamedConfigurationMock.mockReturnValue({
            isPending: false,
            isError: false,
            data: { HardwareAccelerationType: 'none', HardwareDecodingCodecs: [] },
            refetch: vi.fn()
        });
        view.rerender(<TranscodingPage />);
        expect(view.container.textContent).toContain('Transcodificação');
    });

    it('shows unavailable state for trickplay and restores the settings after API connection', () => {
        const view = renderRoute(<TrickplayPage />, false);
        const status = view.container.querySelector('[role="status"]');
        expect(status).not.toBeNull();
        expect(status?.textContent).toContain('Servidor indisponível');
        expect(view.container.querySelector('[role="progressbar"]')).toBeNull();
        expect(view.container.textContent).not.toContain('Salvar');

        useConfigurationMock.mockReturnValue({
            isPending: false,
            isError: false,
            data: { TrickplayOptions: {} },
            refetch: vi.fn()
        });
        view.rerender(<TrickplayPage />);
        expect(view.container.textContent).toContain('Trickplay');
    });

    it('shows unavailable state for recording settings and restores the form after API connection', () => {
        const view = renderRoute(<RecordingSettingsPage />, false);
        const status = view.container.querySelector('[role="status"]');
        expect(status).not.toBeNull();
        expect(status?.textContent).toContain('Servidor indisponível');
        expect(view.container.querySelector('[role="progressbar"]')).toBeNull();
        expect(view.container.textContent).not.toContain('Gravações');

        useNamedConfigurationMock.mockReturnValue({
            isPending: false,
            isError: false,
            data: { GuideDays: 7 },
            refetch: vi.fn()
        });
        view.rerender(<RecordingSettingsPage />);
        expect(view.container.textContent).toContain('Gravações');
    });
});
