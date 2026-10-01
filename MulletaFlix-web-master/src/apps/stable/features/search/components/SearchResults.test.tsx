import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { CollectionType } from '@jellyfin/sdk/lib/generated-client/models/collection-type';

const { mockUseSearchItems } = vi.hoisted(() => ({
    mockUseSearchItems: vi.fn()
}));

vi.mock('../api/useSearchItems', () => ({
    useSearchItems: mockUseSearchItems
}));

vi.mock('components/loading/LoadingComponent', () => ({
    default: () => <div data-testid='loading'>Loading...</div>
}));

vi.mock('react-router-dom', () => ({
    Link: ({ to, children, className }: { to: string; children: React.ReactNode; className?: string }) => (
        <a href={to} className={className}>{children}</a>
    )
}));

vi.mock('./SearchResultsRow', () => ({
    default: ({ title, items }: { title: string; items: unknown[] }) => (
        <div data-testid='search-row'>
            <h3>{title}</h3>
            <span>{items.length} items</span>
        </div>
    )
}));

vi.mock('lib/globalize', () => ({
    default: {
        translate: (key: string, param?: string) => {
            if (key === 'SearchResultsEmpty' && param) {
                return `Sorry! No results found for "${param}"`;
            }
            const translations: Record<string, string> = {
                ErrorDefault: 'An error occurred while loading content.',
                Retry: 'Retry',
                Movies: 'Movies',
                Series: 'Series',
                RetryWithGlobalSearch: 'Search again across all libraries'
            };
            return translations[key] || key;
        }
    }
}));

import SearchResults from './SearchResults';

describe('SearchResults', () => {
    beforeEach(() => {
        mockUseSearchItems.mockReset();
    });

    it('renders loading state when search is pending', () => {
        mockUseSearchItems.mockReturnValue({
            data: undefined,
            isPending: true,
            isError: false,
            refetch: vi.fn()
        });

        const markup = renderToStaticMarkup(<SearchResults query='Inception' />);
        expect(markup).toContain('data-testid="loading"');
    });

    it('renders error state with retry when search fails', () => {
        mockUseSearchItems.mockReturnValue({
            data: undefined,
            isPending: false,
            isError: true,
            refetch: vi.fn()
        });

        const markup = renderToStaticMarkup(<SearchResults query='Inception' />);
        expect(markup).toContain('An error occurred while loading content.');
        expect(markup).toContain('Retry');
    });

    it('renders empty state when search returns no matching sections', () => {
        mockUseSearchItems.mockReturnValue({
            data: [],
            isPending: false,
            isError: false,
            refetch: vi.fn()
        });

        const markup = renderToStaticMarkup(
            <SearchResults query='NonExistentFilm' collectionType={CollectionType.Movies} />
        );
        expect(markup).toContain('Scoped to Movies');
        expect(markup).toContain('Sorry! No results found for &quot;NonExistentFilm&quot;');
        expect(markup).toContain('Search again across all libraries');
        expect(markup).toContain('href="/search?query=NonExistentFilm"');
    });

    it('renders sections and rows when search returns results', () => {
        mockUseSearchItems.mockReturnValue({
            data: [
                {
                    title: 'Movies',
                    items: [{ Id: 'm1', Name: 'Inception' }]
                },
                {
                    title: 'Series',
                    items: [{ Id: 's1', Name: 'Inception Series' }]
                }
            ],
            isPending: false,
            isError: false,
            refetch: vi.fn()
        });

        const markup = renderToStaticMarkup(<SearchResults query='Inception' />);
        expect(markup).toContain('data-testid="search-row"');
        expect(markup).toContain('Movies');
        expect(markup).toContain('Series');
        expect(markup).toContain('1 items');
    });
});
