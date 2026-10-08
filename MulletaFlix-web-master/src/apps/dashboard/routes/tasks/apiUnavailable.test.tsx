import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { useLiveTasksMock } = vi.hoisted(() => ({ useLiveTasksMock: vi.fn() }));

vi.mock('apps/dashboard/features/tasks/hooks/useLiveTasks', () => ({ default: useLiveTasksMock }));
vi.mock('apps/dashboard/features/tasks/utils/tasks', () => ({ getCategories: () => [], getTasksByCategory: () => [] }));
vi.mock('apps/dashboard/features/tasks/components/Tasks', () => ({ default: () => null }));
vi.mock('components/Page', () => ({ default: ({ children, title }: React.PropsWithChildren<{ title?: string }>) => <main><h1>{title}</h1>{children}</main> }));
vi.mock('components/loading/LoadingComponent', () => ({ default: () => <div role='progressbar'>Loading</div> }));
vi.mock('lib/globalize', () => ({ default: { translate: (key: string) => ({
    HeaderServerUnavailable: 'Servidor indisponível',
    TabScheduledTasks: 'Tarefas agendadas',
    ErrorLoadingData: 'Falha ao carregar',
    Retry: 'Tentar novamente'
}[key] ?? key) } }));

import { ApiContext } from 'hooks/useApi';
import { Component as TasksPage } from './index';

describe('scheduled tasks route without API connection', () => {
    let container: HTMLDivElement;
    let root: Root;
    let queryClient: QueryClient;

    beforeEach(() => {
        vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
        useLiveTasksMock.mockReturnValue({ data: undefined, isPending: true, isError: false, refetch: vi.fn() });
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

    const renderPage = (connected: boolean) => act(() => root.render(
        <QueryClientProvider client={queryClient}>
            <ApiContext.Provider value={connected ? { api: { basePath: 'http://server.test' } as never } : {}}>
                <TasksPage />
            </ApiContext.Provider>
        </QueryClientProvider>
    ));

    it('shows an accessible unavailable state instead of an infinite spinner, then restores page content', () => {
        renderPage(false);

        expect(container.querySelector('[role="status"]')?.textContent).toContain('Servidor indisponível');
        expect(container.querySelector('[role="progressbar"]')).toBeNull();

        useLiveTasksMock.mockReturnValue({ data: [], isPending: false, isError: false, refetch: vi.fn() });
        renderPage(true);

        expect(container.textContent).toContain('Tarefas agendadas');
        expect(container.querySelector('[role="status"]')).toBeNull();
    });
});
