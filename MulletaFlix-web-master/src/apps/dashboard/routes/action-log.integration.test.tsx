import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { requestMock, useApiMock, useNavigateMock } = vi.hoisted(() => ({
    requestMock: vi.fn(),
    useApiMock: vi.fn(),
    useNavigateMock: vi.fn()
}));

vi.mock('hooks/useApi', () => ({ useApi: useApiMock }));
vi.mock('react-router-dom', async (importOriginal) => ({
    ...await importOriginal<typeof import('react-router-dom')>(),
    useNavigate: () => useNavigateMock
}));
vi.mock('apps/dashboard/components/widgets/Widget', () => ({
    default: ({ children, title }: React.PropsWithChildren<{ title: string }>) => (
        <main><h1>{title}</h1>{children}</main>
    )
}));
vi.mock('lib/globalize', () => ({
    default: {
        translate: (key: string) => ({
            ActionLog: 'Action log',
            Loading: 'Loading records',
            ErrorLoadingData: 'Could not load records',
            Retry: 'Retry',
            NoActionLogsFound: 'No action logs found',
            SearchByUsername: 'Search by username'
        }[key] ?? key)
    }
}));

import ActionLogPage from './action-log';

const entry = (username: string, id: number) => ({
    id,
    actionType: 'UserCreated',
    entityType: 'User',
    entityId: `user-${id}`,
    userId: `user-${id}`,
    username,
    dateCreated: '2026-10-07T00:00:00.000Z',
    details: null,
    oldValues: null,
    newValues: null,
    ipAddress: null,
    userAgent: null,
    isSuccess: true,
    errorMessage: null,
    category: 'UserManagement'
});

const result = (...items: ReturnType<typeof entry>[]) => ({
    items,
    totalRecordCount: items.length,
    startIndex: 0
});

const deferred = <T,>() => {
    let resolve!: (value: T) => void;
    let reject!: (error: Error) => void;
    const promise = new Promise<T>((resolvePromise, rejectPromise) => {
        resolve = resolvePromise;
        reject = rejectPromise;
    });
    return { promise, resolve, reject };
};

describe('ActionLogPage with real TanStack Query transitions', () => {
    let root: Root;
    let container: HTMLDivElement;
    let client: QueryClient;

    beforeEach(() => {
        requestMock.mockReset();
        useNavigateMock.mockReset();
        useApiMock.mockReturnValue({
            api: {
                basePath: 'http://server.test',
                axiosInstance: { request: requestMock }
            }
        });
        container = document.createElement('div');
        document.body.append(container);
        root = createRoot(container);
        client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    });

    afterEach(async () => {
        await act(async () => root.unmount());
        client.clear();
        container.remove();
    });

    const mount = async () => {
        await act(async () => root.render(
            <QueryClientProvider client={client}>
                <ActionLogPage />
            </QueryClientProvider>
        ));
    };

    it('shows initial loading until the first query confirms an empty result', async () => {
        const pending = deferred<{ data: ReturnType<typeof result> }>();
        requestMock.mockReturnValueOnce(pending.promise);

        await mount();
        await vi.waitFor(() => expect(container.textContent).toContain('Loading records'));
        expect(container.textContent).not.toContain('No action logs found');

        await act(async () => pending.resolve({ data: result() }));
        await vi.waitFor(() => expect(container.textContent).toContain('No action logs found'));
        expect(container.textContent).not.toContain('Loading records');
    });

    it('keeps the prior rows visible while a changed filter key is loading, then replaces them', async () => {
        const nextResult = deferred<{ data: ReturnType<typeof result> }>();
        requestMock.mockResolvedValueOnce({ data: result(entry('original-user', 1)) })
            .mockReturnValueOnce(nextResult.promise);

        await mount();
        await vi.waitFor(() => expect(container.textContent).toContain('original-user'));

        const search = container.querySelector<HTMLInputElement>('input[placeholder="Search by username"]');
        expect(search).not.toBeNull();
        await act(async () => {
            const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set;
            setter?.call(search, 'filtered-user');
            search?.dispatchEvent(new Event('input', { bubbles: true }));
        });

        await vi.waitFor(() => expect(requestMock).toHaveBeenCalledTimes(2));
        expect(requestMock.mock.calls[1][0].url).toContain('username=filtered-user');
        expect(container.textContent).toContain('Loading records');
        expect(container.textContent).toContain('original-user');
        expect(container.textContent).not.toContain('No action logs found');

        await act(async () => nextResult.resolve({ data: result(entry('filtered-user', 2)) }));
        await vi.waitFor(() => expect(container.textContent).toContain('filtered-user'));
        expect(container.textContent).not.toContain('original-user');
        expect(container.textContent).not.toContain('Loading records');
    });

    it('preserves cached rows after refresh failure and replaces them after retry succeeds', async () => {
        requestMock.mockResolvedValueOnce({ data: result(entry('cached-user', 3)) })
            .mockRejectedValueOnce(new Error('temporary refresh failure'))
            .mockResolvedValueOnce({ data: result(entry('updated-user', 4)) });

        await mount();
        await vi.waitFor(() => expect(container.textContent).toContain('cached-user'));

        await act(async () => {
            await client.invalidateQueries({ queryKey: ['ActionLog', 'Entries'] });
        });
        await vi.waitFor(() => expect(container.textContent).toContain('Could not load records'));
        expect(container.textContent).toContain('cached-user');

        const retry = Array.from(container.querySelectorAll('button')).find(button => button.textContent === 'Retry');
        expect(retry).toBeDefined();
        await act(async () => retry?.click());

        await vi.waitFor(() => expect(container.textContent).toContain('updated-user'));
        expect(container.textContent).not.toContain('Could not load records');
        expect(container.textContent).not.toContain('cached-user');
        expect(requestMock).toHaveBeenCalledTimes(3);
    });

    it('retries an initial query failure and shows the recovered result', async () => {
        requestMock.mockRejectedValueOnce(new Error('temporary initial failure'))
            .mockResolvedValueOnce({ data: result(entry('recovered-user', 5)) });

        await mount();
        await vi.waitFor(() => expect(container.textContent).toContain('Could not load records'));
        expect(container.textContent).not.toContain('No action logs found');

        const retry = Array.from(container.querySelectorAll('button')).find(button => button.textContent === 'Retry');
        expect(retry).toBeDefined();
        await act(async () => retry?.click());

        await vi.waitFor(() => expect(container.textContent).toContain('recovered-user'));
        expect(container.textContent).not.toContain('Could not load records');
        expect(requestMock).toHaveBeenCalledTimes(2);
    });
});
