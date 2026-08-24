import { queryOptions, useQuery } from '@tanstack/react-query';
import type { Api } from '@jellyfin/sdk';
import type { AxiosRequestConfig } from 'axios';
import type { BaseItemDto, SearchHint } from '@jellyfin/sdk/lib/generated-client';
import type { CollectionType } from '@jellyfin/sdk/lib/generated-client/models/collection-type';
import { BaseItemKind } from '@jellyfin/sdk/lib/generated-client/models/base-item-kind';
import { ItemSortBy } from '@jellyfin/sdk/lib/generated-client/models/item-sort-by';
import { getItemsApi } from '@jellyfin/sdk/lib/utils/api/items-api';

import { useApi } from 'hooks/useApi';
import { getItemTypesFromCollectionType } from '../utils/search';

type UnifiedSearchItem = BaseItemDto | SearchHint;

interface UnifiedSearchQuery {
    userId?: string;
    searchTerm?: string;
    parentId?: string;
    collectionType?: CollectionType;
    includeItemTypes?: BaseItemKind[];
    excludeItemTypes?: BaseItemKind[];
    mediaTypes?: string[];
    limit?: number;
    startIndex?: number;
    includePeople?: boolean;
    includeMedia?: boolean;
    includeGenres?: boolean;
    includeStudios?: boolean;
    includeArtists?: boolean;
    sortBy?: ItemSortBy[];
    sortOrder?: 'asc' | 'desc';
}

interface UnifiedSearchResult {
    items: UnifiedSearchItem[];
    totalRecordCount: number;
    sections: UnifiedSearchSection[];
}

interface UnifiedSearchSection {
    name: string;
    items: UnifiedSearchItem[];
    cardOptions?: any;
}

interface SearchStatsDto {
    totalMovies: number;
    totalSeries: number;
    totalEpisodes: number;
    totalArtists: number;
    totalAlbums: number;
    totalSongs: number;
    totalChannels: number;
    totalPrograms: number;
}

const fetchUnifiedSearch = async (
    api: Api,
    query: UnifiedSearchQuery,
    options?: AxiosRequestConfig
) => {
    const params = new URLSearchParams();
    if (query.userId) params.set('userId', query.userId);
    if (query.searchTerm) params.set('searchTerm', query.searchTerm);
    if (query.parentId) params.set('parentId', query.parentId);
    if (query.collectionType) params.set('collectionType', query.collectionType);
    if (query.includeItemTypes?.length) params.set('includeItemTypes', query.includeItemTypes.join(','));
    if (query.excludeItemTypes?.length) params.set('excludeItemTypes', query.excludeItemTypes.join(','));
    if (query.mediaTypes?.length) params.set('mediaTypes', query.mediaTypes.join(','));
    if (query.limit) params.set('limit', query.limit.toString());
    if (query.startIndex) params.set('startIndex', query.startIndex.toString());
    if (query.includePeople !== undefined) params.set('includePeople', query.includePeople.toString());
    if (query.includeMedia !== undefined) params.set('includeMedia', query.includeMedia.toString());
    if (query.includeGenres !== undefined) params.set('includeGenres', query.includeGenres.toString());
    if (query.includeStudios !== undefined) params.set('includeStudios', query.includeStudios.toString());
    if (query.includeArtists !== undefined) params.set('includeArtists', query.includeArtists.toString());
    if (query.sortBy?.length) params.set('sortBy', query.sortBy.join(','));
    if (query.sortOrder) params.set('sortOrder', query.sortOrder);

    const response = await api.axiosInstance.request({
        url: `/Search/Unified?${params.toString()}`,
        method: 'GET',
        signal: options?.signal as AbortSignal | undefined,
        headers: { 'Cache-Control': 'no-cache', ...options?.headers }
    });
    return response.data as UnifiedSearchResult;
};

const fetchSearchStats = async (
    api: Api,
    userId: string,
    options?: AxiosRequestConfig
) => {
    const response = await api.axiosInstance.request({
        url: `/Search/Stats?userId=${userId}`,
        method: 'GET',
        signal: options?.signal as AbortSignal | undefined,
        headers: { 'Cache-Control': 'no-cache', ...options?.headers }
    });
    return response.data as SearchStatsDto;
};

export const getUnifiedSearchQuery = (
    api?: Api,
    query?: UnifiedSearchQuery
) => queryOptions({
    queryKey: ['Search', 'Unified', api?.basePath, JSON.stringify(query ?? {})],
    queryFn: ({ signal }) => fetchUnifiedSearch(api!, query ?? {}, { signal, headers: { 'Cache-Control': 'no-cache' }}),
    staleTime: 30000,
    enabled: !!api && !!query?.searchTerm
});

export const getSearchStatsQuery = (
    api?: Api,
    userId?: string
) => queryOptions({
    queryKey: ['Search', 'Stats', api?.basePath, userId],
    queryFn: ({ signal }) => fetchSearchStats(api!, userId!, { signal, headers: { 'Cache-Control': 'no-cache' }}),
    staleTime: 300000,
    enabled: !!api && !!userId
});

export const useUnifiedSearch = (query: UnifiedSearchQuery) => {
    const { api } = useApi();
    return useQuery(getUnifiedSearchQuery(api, query));
};

export const useSearchStats = () => {
    const { api, user } = useApi();
    return useQuery(getSearchStatsQuery(api, user?.Id));
};