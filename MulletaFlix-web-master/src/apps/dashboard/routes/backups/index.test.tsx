import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { listBackupsMock, getTasksMock, toApiMock, useActionDataMock, useNavigationMock } = vi.hoisted(() => ({
    listBackupsMock: vi.fn(),
    getTasksMock: vi.fn(),
    toApiMock: vi.fn(),
    useActionDataMock: vi.fn(),
    useNavigationMock: vi.fn()
}));

vi.mock('@jellyfin/sdk/lib/utils/api/backup-api', () => ({
    getBackupApi: () => ({ listBackups: listBackupsMock })
}));
vi.mock('@jellyfin/sdk/lib/utils/api/scheduled-tasks-api', () => ({
    getScheduledTasksApi: () => ({ getTasks: getTasksMock })
}));
vi.mock('utils/jellyfin-apiclient/compat', () => ({ toApi: toApiMock }));
vi.mock('react-router-dom', async (importOriginal) => ({
    ...await importOriginal<typeof import('react-router-dom')>(),
    Form: ({ children }: { children: React.ReactNode }) => React.createElement('form', null, children),
    useActionData: useActionDataMock,
    useNavigation: useNavigationMock
}));
vi.mock('apps/dashboard/features/backups/api/useCreateBackup', () => ({
    useCreateBackup: () => ({ mutate: vi.fn() })
}));
vi.mock('apps/dashboard/features/backups/api/useRestoreBackup', () => ({
    useRestoreBackup: () => ({ mutate: vi.fn() })
}));
vi.mock('apps/dashboard/features/backups/components/BackupProgressDialog', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/backups/components/RestoreProgressDialog', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/backups/components/ConfirmDialog', () => ({ default: () => null }));
vi.mock('components/ConfirmDialog', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/backups/components/CreateBackupForm', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/backups/components/RestoreConfirmationDialog', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/backups/components/ScheduleBackupDialog', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/backups/components/Backup', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/backups/components/BackupHistory', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/backups/components/BackupOperationalSummary', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/backups/components/BackupCoverageSummary', () => ({ default: () => null }));
vi.mock('components/SimpleAlert', () => ({ default: () => null }));
vi.mock('components/Page', () => ({
    default: ({ children }: React.PropsWithChildren) => <main>{children}</main>
}));
vi.mock('components/loading/LoadingComponent', () => ({
    default: () => <div data-testid='loading'>Loading</div>
}));
vi.mock('lib/globalize', () => ({
    default: { translate: (key: string) => ({
        HeaderServerUnavailable: 'Servidor indisponível',
        HeaderBackups: 'Backups',
        BackupsPageLoadError: 'Falha ao carregar backups',
        Retry: 'Tentar novamente',
        HeaderBackupWarning: 'Aviso',
        LabelBackupWarning: 'Existe uma tarefa em andamento',
        Create: 'Criar',
        ButtonCreateBackup: 'Criar backup',
        HeaderScheduleBackup: 'Agendar backup',
        UnknownError: 'Erro',
        Success: 'Sucesso',
        MessageRestoreSuccess: 'Restauração concluída'
    }[key] ?? key) }
}));

import { ApiProvider } from 'hooks/useApi';
import { Component as BackupsPage } from './index';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import events from 'utils/events';

describe('BackupsPage API connection state', () => {
    let container: HTMLDivElement;
    let root: Root;
    let queryClient: QueryClient;
    const api = {
        basePath: 'http://jellyfin.test',
        subscribe: vi.fn(() => vi.fn()),
        axiosInstance: { get: vi.fn().mockResolvedValue({ data: { IsConfigured: false, IsConnected: false } }) }
    };
    const legacyApiClient = {};

    beforeEach(() => {
        vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
        vi.spyOn(ServerConnections, 'currentApiClient').mockReturnValue(undefined);
        vi.spyOn(ServerConnections, 'getApiClient').mockReturnValue(legacyApiClient as never);
        toApiMock.mockReturnValue(api);
        listBackupsMock.mockResolvedValue({ data: [] });
        getTasksMock.mockResolvedValue({ data: [] });
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
            <ApiProvider><BackupsPage /></ApiProvider>
        </QueryClientProvider>
    );

    it('shows accessible server-unavailable state and hides backup actions without API', async () => {
        await act(async () => root.render(renderPage()));

        expect(container.textContent).toContain('Servidor indisponível');
        expect(container.querySelector('[role="status"]')).not.toBeNull();
        expect(container.querySelector('[data-testid="loading"]')).toBeNull();
        expect(container.textContent).not.toContain('Criar backup');
        expect(listBackupsMock).not.toHaveBeenCalled();
        expect(getTasksMock).not.toHaveBeenCalled();
        expect(api.axiosInstance.get).not.toHaveBeenCalled();
    });

    it('loads backup data after ApiProvider receives a sign-in event', async () => {
        await act(async () => root.render(renderPage()));
        expect(container.textContent).toContain('Servidor indisponível');

        await act(async () => {
            events.trigger(ServerConnections, 'localusersignedin', [{ Id: 'user-id', Name: 'test-user', ServerId: 'server-id' }]);
            for (let attempt = 0; attempt < 10; attempt++) await new Promise(resolve => setTimeout(resolve, 0));
        });

        expect(toApiMock).toHaveBeenCalledWith(legacyApiClient);
        expect(listBackupsMock).toHaveBeenCalled();
        expect(getTasksMock).toHaveBeenCalled();
        expect(api.axiosInstance.get).toHaveBeenCalledWith('/NebulaFtp/Supabase/Status', expect.anything());
        await act(async () => new Promise(resolve => setTimeout(resolve, 0)));
        expect(container.textContent).toContain('Criar backup');
        expect(container.querySelector('[data-testid="loading"]')).toBeNull();
    });
});
