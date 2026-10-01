import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { mockUseSearchSuggestions, mockUseApi } = vi.hoisted(() => ({
    mockUseSearchSuggestions: vi.fn(),
    mockUseApi: vi.fn()
}));

vi.mock('../api/useSearchSuggestions', () => ({
    useSearchSuggestions: mockUseSearchSuggestions
}));

vi.mock('hooks/useApi', () => ({
    useApi: mockUseApi
}));

vi.mock('components/loading/LoadingComponent', () => ({
    default: () => <div data-testid='loading'>Loading...</div>
}));

vi.mock('components/router/appRouter', () => ({
    appRouter: {
        getRouteUrl: (item: { Id: string }) => `/item/${item.Id}`
    }
}));

vi.mock('lib/globalize', () => ({
    default: {
        translate: (key: string, param?: string) => {
            if (key === 'SearchResultsEmpty' && param) {
                return `Sorry! No results found for "${param}"`;
            }
            const translations: Record<string, string> = {
                Suggestions: 'Suggestions',
                Search: 'Search',
                ErrorDefault: 'An error occurred while loading content.',
                Retry: 'Retry',
                MessageNoMovieSuggestionsAvailable: 'No movie suggestions are currently available.'
            };
            return translations[key] || key;
        }
    }
}));

import SearchSuggestions from './SearchSuggestions';

describe('SearchSuggestions', () => {
    beforeEach(() => {
        mockUseApi.mockReturnValue({
            __legacyApiClient__: {
                serverId: () => 'test-server'
            }
        });
        mockUseSearchSuggestions.mockReset();
    });

    it('renders loading state when query is pending', () => {
        mockUseSearchSuggestions.mockReturnValue({
            data: undefined,
            isPending: true,
            isError: false,
            refetch: vi.fn()
        });

        const markup = renderToStaticMarkup(<SearchSuggestions />);
        expect(markup).toContain('data-testid="loading"');
    });

    it('renders error state with retry when query fails', () => {
        mockUseSearchSuggestions.mockReturnValue({
            data: undefined,
            isPending: false,
            isError: true,
            refetch: vi.fn()
        });

        const markup = renderToStaticMarkup(<SearchSuggestions />);
        expect(markup).toContain('An error occurred while loading content.');
        expect(markup).toContain('Retry');
    });

    it('renders empty suggestions state when query is empty and no suggestions exist', () => {
        mockUseSearchSuggestions.mockReturnValue({
            data: [],
            isPending: false,
            isError: false,
            refetch: vi.fn()
        });

        const markup = renderToStaticMarkup(<SearchSuggestions />);
        expect(markup).toContain('Suggestions');
        expect(markup).toContain('No movie suggestions are currently available.');
    });

    it('renders empty search state when query is provided and no results are found', () => {
        mockUseSearchSuggestions.mockReturnValue({
            data: [],
            isPending: false,
            isError: false,
            refetch: vi.fn()
        });

        const markup = renderToStaticMarkup(<SearchSuggestions query='Avatar' />);
        expect(markup).toContain('Search');
        expect(markup).toContain('Sorry! No results found for &quot;Avatar&quot;');
    });

    it('renders list of suggestions when items exist', () => {
        mockUseSearchSuggestions.mockReturnValue({
            data: [
                { Id: '1', Name: 'Breaking Bad' },
                { Id: '2', Name: 'Better Call Saul' }
            ],
            isPending: false,
            isError: false,
            refetch: vi.fn()
        });

        const markup = renderToStaticMarkup(<SearchSuggestions />);
        expect(markup).toContain('Breaking Bad');
        expect(markup).toContain('Better Call Saul');
        expect(markup).toContain('/item/1');
        expect(markup).toContain('/item/2');
    });
});
