import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { renderToStaticMarkup } from 'react-dom/server';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
    useLogEntries: vi.fn(),
    useUsersDetails: vi.fn(),
    useApi: vi.fn(),
    useQuery: vi.fn(),
    useMaterialReactTable: vi.fn()
}));

vi.mock('@tanstack/react-query', async (importOriginal) => ({
    ...await importOriginal<typeof import('@tanstack/react-query')>(),
    useQuery: mocks.useQuery
}));
vi.mock('material-react-table', () => ({
    MaterialReactTable: ({ table }: { table: { data: unknown[]; state: { isLoading: boolean } } }) => (
        <div data-testid='activity-table' data-loading={table.state.isLoading} data-row-count={table.data.length} />
    ),
    useMaterialReactTable: mocks.useMaterialReactTable
}));
vi.mock('apps/dashboard/components/table/TablePage', () => ({ ['DEFAULT_TABLE_OPTIONS']: {} }));
vi.mock('apps/dashboard/components/table/DateTimeCell', () => ({ default: () => null }));
vi.mock('apps/dashboard/features/activity/api/useLogEntries', () => ({ useLogEntries: mocks.useLogEntries }));
vi.mock('hooks/useUsers', () => ({ useUsersDetails: mocks.useUsersDetails }));
vi.mock('hooks/useApi', () => ({ useApi: mocks.useApi }));
vi.mock('lib/globalize', () => ({
    default: {
        translate: (key: string) => ({
            ActivitiesLoadError: 'Falha ao carregar atividades',
            LabelSystem: 'Sistema',
            LabelTime: 'Horário',
            LabelUser: 'Usuário',
            LabelName: 'Nome',
            LabelType: 'Tipo',
            LabelOverview: 'Resumo',
            MediaRequestsAdminTitle: 'Solicitações de mídias',
            MediaRequestsPendingTitle: 'Fila de solicitações',
            MediaRequestsIncludedTitle: 'Títulos incluídos',
            MessageNoItemsAvailable: 'Nenhum item está disponível no momento.',
            Retry: 'Tentar novamente'
        }[key] || key)
    }
}));

import UserFeedbackListPage from './UserFeedbackListPage';

const renderPage = (type: 'MediaRequest' | 'PlaybackIssue' = 'MediaRequest') => renderToStaticMarkup(
    <UserFeedbackListPage type={type} titleKey={type === 'MediaRequest' ? 'MediaRequestsAdminTitle' : 'PlaybackIssuesAdminTitle'} />
);

describe('UserFeedbackListPage', () => {
    beforeEach(() => {
        mocks.useLogEntries.mockReset();
        mocks.useUsersDetails.mockReset();
        mocks.useApi.mockReset();
        mocks.useQuery.mockReset();
        mocks.useMaterialReactTable.mockReset();

        mocks.useApi.mockReturnValue({ api: {} });
        mocks.useLogEntries.mockReturnValue({ data: { Items: [] }, isLoading: false, isPending: false, isError: false, refetch: vi.fn() });
        mocks.useUsersDetails.mockReturnValue({ usersById: {}, isLoading: false, isError: false, refetch: vi.fn() });
        mocks.useQuery.mockReturnValue({ data: [], isLoading: false, isError: false, refetch: vi.fn() });
        mocks.useMaterialReactTable.mockImplementation((options: { data: unknown[]; state: { isLoading: boolean } }) => options);
    });

    it('shows an explicit empty state for each media-request grid', () => {
        const markup = renderPage();

        expect(markup).toContain('Fila de solicitações (0)');
        expect(markup).toContain('Títulos incluídos (0)');
        expect(markup.match(/role="status"/g)).toHaveLength(2);
        expect(markup.match(/Nenhum item está disponível no momento\./g)).toHaveLength(2);
        expect(markup).not.toContain('data-testid="activity-table"');
    });

    it('keeps tables in loading state instead of showing empty messages before data arrives', () => {
        mocks.useLogEntries.mockReturnValue({ data: undefined, isLoading: false, isPending: true, isError: false, refetch: vi.fn() });
        mocks.useQuery.mockReturnValue({ data: undefined, isLoading: true, isError: false, refetch: vi.fn() });

        const markup = renderPage();

        expect(markup.match(/data-testid="activity-table"/g)).toHaveLength(2);
        expect(markup.match(/data-loading="true"/g)).toHaveLength(2);
        expect(markup).not.toContain('Nenhum item está disponível no momento.');
    });

    it('shows an explicit empty state for playback reports', () => {
        const markup = renderPage('PlaybackIssue');

        expect(markup).toContain('Nenhum item está disponível no momento.');
        expect(markup).not.toContain('data-testid="activity-table"');
    });

    it('shows an error with a retry action when a source fails', () => {
        mocks.useLogEntries.mockReturnValue({ data: undefined, isLoading: false, isError: true, refetch: vi.fn() });

        const markup = renderPage();

        expect(markup).toContain('Falha ao carregar atividades');
        expect(markup).toContain('Tentar novamente');
        expect(markup).not.toContain('data-testid="activity-table"');
    });

    it('does not mistake an inactive query without an API client for an empty result', () => {
        mocks.useApi.mockReturnValue({ api: undefined });
        mocks.useLogEntries.mockReturnValue({ data: undefined, isLoading: false, isPending: true, isError: false, refetch: vi.fn() });

        const markup = renderPage('PlaybackIssue');

        expect(markup).toContain('Falha ao carregar atividades');
        expect(markup).not.toContain('Nenhum item está disponível no momento.');
    });

    it('shows the shared error state when request catalog loading fails', () => {
        mocks.useQuery.mockReturnValue({ data: undefined, isLoading: false, isError: true, refetch: vi.fn() });

        expect(renderPage()).toContain('Falha ao carregar atividades');
    });

    it('shows the shared error state when user lookup fails for playback reports', () => {
        mocks.useUsersDetails.mockReturnValue({ usersById: {}, isLoading: false, isError: true, refetch: vi.fn() });

        expect(renderPage('PlaybackIssue')).toContain('Falha ao carregar atividades');
    });

    it('does not refetch the disabled media catalog when retrying playback reports', async () => {
        const catalogRefetch = vi.fn();
        const container = document.createElement('div');
        const root = createRoot(container);
        mocks.useUsersDetails.mockReturnValue({ usersById: {}, isLoading: false, isError: true, refetch: vi.fn() });
        mocks.useQuery.mockReturnValue({ data: [], isLoading: false, isError: false, refetch: catalogRefetch });

        await act(async () => root.render(<UserFeedbackListPage type='PlaybackIssue' titleKey='PlaybackIssuesAdminTitle' />));
        await act(async () => container.querySelector('button')?.dispatchEvent(new MouseEvent('click', { bubbles: true })));

        expect(catalogRefetch).not.toHaveBeenCalled();
        await act(async () => root.unmount());
    });

    it('refetches the media catalog when retrying media requests', async () => {
        const catalogRefetch = vi.fn();
        const container = document.createElement('div');
        const root = createRoot(container);
        mocks.useQuery.mockReturnValue({ data: undefined, isLoading: false, isError: true, refetch: catalogRefetch });

        await act(async () => root.render(<UserFeedbackListPage type='MediaRequest' titleKey='MediaRequestsAdminTitle' />));
        await act(async () => container.querySelector('button')?.dispatchEvent(new MouseEvent('click', { bubbles: true })));

        expect(catalogRefetch).toHaveBeenCalledOnce();
        await act(async () => root.unmount());
    });
});
