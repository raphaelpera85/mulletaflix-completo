import React, { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import globalize from 'lib/globalize';

import { useSearchStats, useUnifiedSearch } from 'apps/dashboard/features/search/api/useUnifiedSearch';
import Loading from 'components/loading/LoadingComponent';
import Page from 'components/Page';
import SectionContainer from 'components/common/SectionContainer';
import SearchInput from 'apps/dashboard/components/SearchInput';
import type { BaseItemDto } from '@jellyfin/sdk/lib/generated-client';
import { BaseItemKind } from '@jellyfin/sdk/lib/generated-client/models/base-item-kind';

type SearchFilter = 'all' | 'movies' | 'series' | 'episodes' | 'music';

const SEARCH_FILTERS: Array<{ id: SearchFilter; labelKey: string; itemTypes?: BaseItemKind[] }> = [
    { id: 'all', labelKey: 'All' },
    { id: 'movies', labelKey: 'Movies', itemTypes: [BaseItemKind.Movie] },
    { id: 'series', labelKey: 'Series', itemTypes: [BaseItemKind.Series] },
    { id: 'episodes', labelKey: 'Episodes', itemTypes: [BaseItemKind.Episode] },
    { id: 'music', labelKey: 'TabMusic', itemTypes: [BaseItemKind.Audio, BaseItemKind.MusicAlbum, BaseItemKind.MusicArtist, BaseItemKind.MusicVideo] }
];

const SEARCH_STAT_CARDS = [
    { id: 'movies', labelKey: 'Movies', stat: 'totalMovies' },
    { id: 'series', labelKey: 'Series', stat: 'totalSeries' },
    { id: 'episodes', labelKey: 'Episodes', stat: 'totalEpisodes' },
    { id: 'music', labelKey: 'TabMusic', stat: 'totalSongs' }
] as const;

const SearchPage = () => {
    const [searchParams, setSearchParams] = useSearchParams();
    const searchTerm = searchParams.get('q') ?? '';
    const userId = searchParams.get('userId');
    const [draftSearchTerm, setDraftSearchTerm] = useState(searchTerm);
    const [recentSearches, setRecentSearches] = useState<string[]>([]);
    const [searchFilter, setSearchFilter] = useState<SearchFilter>('all');
    const historyStorageKey = `mulletaflix.search.history.${userId ?? 'anonymous'}`;

    const handleSearchChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setDraftSearchTerm(event.target.value);
    }, []);

    useEffect(() => {
        setDraftSearchTerm(searchTerm);
    }, [searchTerm]);

    useEffect(() => {
        const normalizedSearchTerm = draftSearchTerm.trim();
        if (normalizedSearchTerm === searchTerm) return;

        const timeoutId = window.setTimeout(() => {
            setSearchParams((currentParams) => {
                const nextParams = new URLSearchParams(currentParams);
                if (normalizedSearchTerm) nextParams.set('q', normalizedSearchTerm);
                else nextParams.delete('q');
                return nextParams;
            }, { replace: true });
        }, 300);

        return () => window.clearTimeout(timeoutId);
    }, [draftSearchTerm, searchTerm, setSearchParams]);

    useEffect(() => {
        try {
            const storedSearches = JSON.parse(window.localStorage.getItem(historyStorageKey) ?? '[]');
            setRecentSearches(
                Array.isArray(storedSearches) ?
                    storedSearches.filter((value): value is string => typeof value === 'string').slice(0, 8) :
                    []
            );
        } catch {
            setRecentSearches([]);
        }
    }, [historyStorageKey]);

    useEffect(() => {
        const normalizedSearchTerm = searchTerm.trim();
        if (normalizedSearchTerm.length < 2) return;

        setRecentSearches((currentSearches) => {
            const nextSearches = [normalizedSearchTerm, ...currentSearches.filter(item => item !== normalizedSearchTerm)].slice(0, 8);
            try {
                window.localStorage.setItem(historyStorageKey, JSON.stringify(nextSearches));
            } catch {
                // Private browsing or storage quotas must not break searching.
            }
            return nextSearches;
        });
    }, [historyStorageKey, searchTerm]);

    const selectRecentSearch = useCallback((term: string) => {
        setSearchParams((currentParams) => {
            const nextParams = new URLSearchParams(currentParams);
            nextParams.set('q', term);
            return nextParams;
        });
    }, [setSearchParams]);

    const handleRecentSearchClick = useCallback((event: React.MouseEvent<HTMLButtonElement>) => {
        selectRecentSearch(event.currentTarget.dataset.searchTerm ?? '');
    }, [selectRecentSearch]);

    const clearRecentSearches = useCallback(() => {
        try {
            window.localStorage.removeItem(historyStorageKey);
        } catch {
            // Storage failures should not prevent the visible state from clearing.
        }
        setRecentSearches([]);
    }, [historyStorageKey]);

    const handleFilterClick = useCallback((event: React.MouseEvent<HTMLButtonElement>) => {
        const nextFilter = event.currentTarget.dataset.filter as SearchFilter | undefined;
        if (nextFilter) setSearchFilter(nextFilter);
    }, []);

    const selectedFilter = SEARCH_FILTERS.find(filter => filter.id === searchFilter);
    const { data: searchStats } = useSearchStats();

    const { data: result, isLoading, isError, error, isRefetching, refetch } = useUnifiedSearch({
        userId: userId ?? undefined,
        searchTerm: searchTerm || undefined,
        limit: 100,
        includeItemTypes: selectedFilter?.itemTypes
    });

    useEffect(() => {
        if (searchTerm) {
            void refetch().catch((refetchError: unknown) => {
                console.error('Failed to refresh search results', refetchError);
            });
        }
    }, [searchTerm, refetch]);

    const handleRetry = useCallback(() => {
        void refetch().catch((refetchError: unknown) => {
            console.error('Failed to retry search', refetchError);
        });
    }, [refetch]);

    if (isLoading) {
        return <Loading />;
    }

    if (isError) {
        return (
            <Page id='searchPage' title={globalize.translate('Search')} className='mainAnimatedPage type-interior'>
                <div className='p-4 text-center text-error' role='alert' aria-live='assertive'>
                    <p>{globalize.translate('SearchFailed')}: {String(error)}</p>
                    <button
                        type='button'
                        className='emby-button raised mt-4'
                        disabled={isRefetching}
                        aria-busy={isRefetching}
                        onClick={handleRetry}
                    >
                        {globalize.translate('Retry')}
                    </button>
                </div>
            </Page>
        );
    }

    const sections = result?.sections ?? [];

    return (
        <Page
            id='searchPage'
            title={globalize.translate('Search')}
            className='mainAnimatedPage type-interior'
        >
            <div className='p-4'>
                <div className='mb-4'>
                    <SearchInput
                        type='search'
                        label={globalize.translate('Search')}
                        className='w-full max-w-md'
                        placeholder={globalize.translate('SearchAllContent')}
                        value={draftSearchTerm}
                        onChange={handleSearchChange}
                    />
                </div>

                <div className='mb-4 flex flex-wrap gap-2' role='group' aria-label={globalize.translate('Search')}>
                    {SEARCH_FILTERS.map((filter) => (
                        <button
                            key={filter.id}
                            type='button'
                            className={`emby-button ${searchFilter === filter.id ? 'raised' : ''}`}
                            data-filter={filter.id}
                            aria-pressed={searchFilter === filter.id}
                            onClick={handleFilterClick}
                        >
                            {globalize.translate(filter.labelKey)}
                        </button>
                    ))}
                </div>

                {searchTerm && sections.length === 0 && (
                    <div className='text-center py-8 text-base-content/60'>
                        {globalize.translate('NoResultsFoundFor')}: &quot;{searchTerm}&quot;
                    </div>
                )}

                {sections.map((section) => (
                    <SectionContainer
                        key={section.name}
                        sectionHeaderProps={{ title: section.name }}
                        items={section.items as BaseItemDto[]}
                        cardOptions={section.cardOptions}
                        className='my-4'
                    >
                        {section.items.length === 0 && (
                            <div className='p-4 text-center text-base-content/60'>
                                {globalize.translate('NoResults')}
                            </div>
                        )}
                    </SectionContainer>
                ))}

                {!searchTerm && (
                    <div className='text-center py-8 text-base-content/60'>
                        {searchStats && (
                            <div className='mb-6' role='group' aria-label={globalize.translate('Suggestions')}>
                                <h2 className='mb-3 text-base-content'>{globalize.translate('Suggestions')}</h2>
                                <div className='flex flex-wrap justify-center gap-2'>
                                    {SEARCH_STAT_CARDS.map((card) => (
                                        <button
                                            key={card.id}
                                            type='button'
                                            className='emby-button raised search-stat-card'
                                            data-filter={card.id}
                                            onClick={handleFilterClick}
                                        >
                                            {globalize.translate(card.labelKey)}: {searchStats[card.stat]}
                                        </button>
                                    ))}
                                </div>
                            </div>
                        )}
                        {recentSearches.length > 0 ? (
                            <>
                                <h2 className='mb-3 text-base-content'>{globalize.translate('Suggestions')}</h2>
                                <div className='flex flex-wrap justify-center gap-2' role='list' aria-label={globalize.translate('Suggestions')}>
                                    {recentSearches.map((recentSearch) => (
                                        <button
                                            key={recentSearch}
                                            type='button'
                                            className='emby-button raised search-history-item'
                                            data-search-term={recentSearch}
                                            onClick={handleRecentSearchClick}
                                        >
                                            {recentSearch}
                                        </button>
                                    ))}
                                </div>
                                <button type='button' className='button-link mt-3' onClick={clearRecentSearches}>
                                    {globalize.translate('Delete')}
                                </button>
                            </>
                        ) : (
                            globalize.translate('EnterSearchTerm')
                        )}
                    </div>
                )}
            </div>
        </Page>
    );
};

export default SearchPage;
