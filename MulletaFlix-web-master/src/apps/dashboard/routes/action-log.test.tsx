import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { renderToStaticMarkup } from 'react-dom/server';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { useQueryMock, useApiMock, useNavigateMock } = vi.hoisted(() => ({
    useQueryMock: vi.fn(),
    useApiMock: vi.fn(),
    useNavigateMock: vi.fn()
}));

vi.mock('@tanstack/react-query', () => ({
    keepPreviousData: (previousData: unknown) => previousData,
    useQuery: useQueryMock
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
            NoActionLogsFound: 'No action logs found'
        }[key] ?? key)
    }
}));

import ActionLogPage from './action-log';

const emptyResult = { items: [], totalRecordCount: 0, startIndex: 0 };
const existingResult = {
    items: [{
        id: 17,
        actionType: 'UserCreated',
        entityType: 'User',
        entityId: 'user-17',
        userId: 'admin',
        username: 'raphael',
        dateCreated: '2026-10-07T00:00:00.000Z',
        details: null,
        oldValues: null,
        newValues: null,
        ipAddress: null,
        userAgent: null,
        isSuccess: true,
        errorMessage: null,
        category: 'UserManagement'
    }],
    totalRecordCount: 1,
    startIndex: 0
};

const renderPage = () => renderToStaticMarkup(<ActionLogPage />);

describe('ActionLogPage query states', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        useApiMock.mockReturnValue({ api: { basePath: 'http://server.test' } });
        useQueryMock.mockReturnValue({
            data: emptyResult,
            isLoading: false,
            isFetching: false,
            isPlaceholderData: false,
            isError: false,
            refetch: vi.fn()
        });
    });

    it('does not present placeholder rows as a confirmed empty result while fetching', () => {
        useQueryMock.mockReturnValue({
            data: emptyResult,
            isLoading: false,
            isFetching: true,
            isPlaceholderData: true,
            isError: false,
            refetch: vi.fn()
        });

        const markup = renderPage();

        expect(markup).toContain('Loading records');
        expect(markup).not.toContain('No action logs found');
    });

    it('does not offer retry when the API is unavailable', () => {
        useApiMock.mockReturnValue({ api: null });
        useQueryMock.mockReturnValue({
            data: undefined,
            isLoading: false,
            isFetching: false,
            isPlaceholderData: false,
            isError: false,
            refetch: vi.fn()
        });

        const markup = renderPage();

        expect(markup).toContain('Could not load records');
        expect(markup).not.toContain('Retry');
    });

    it('keeps the last records visible with a retryable error after refresh fails', () => {
        useQueryMock.mockReturnValue({
            data: existingResult,
            isLoading: false,
            isFetching: false,
            isPlaceholderData: false,
            isError: true,
            refetch: vi.fn()
        });

        const markup = renderPage();

        expect(markup).toContain('Could not load records');
        expect(markup).not.toContain('No action logs found');
        expect(markup).toContain('raphael');
        expect(markup).toContain('Retry');
    });

    it('retries the query when the error action is activated', async () => {
        const refetch = vi.fn().mockResolvedValue({ data: existingResult });
        useQueryMock.mockReturnValue({
            data: existingResult,
            isLoading: false,
            isFetching: false,
            isPlaceholderData: false,
            isError: true,
            refetch
        });
        const container = document.createElement('div');
        const root = createRoot(container);

        await act(async () => root.render(<ActionLogPage />));
        const retryButton = Array.from(container.querySelectorAll('button')).find(button => button.textContent === 'Retry');
        expect(retryButton).toBeDefined();
        await act(async () => retryButton?.click());

        expect(refetch).toHaveBeenCalledOnce();
        await act(async () => root.unmount());
    });
});
