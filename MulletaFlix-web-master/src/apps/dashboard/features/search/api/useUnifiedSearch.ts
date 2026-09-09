import { queryOptions, useQuery } from '@tanstack/react-query';
import type { Api } from '@jellyfin/sdk';
import type { AxiosRequestConfig } from 'axios';
import type { BaseItemDto, SearchHint } from '@jellyfin/sdk/lib/generated-client';
import type { CollectionType } from '@jellyfin/sdk/lib/generated-client/models/collection-type';
import { BaseItemKind } from '@jellyfin/sdk/lib/generated-client/models/base-item-kind';
import { ItemSortBy } from '@jellyfin/sdk/lib/generated-client/models/item-sort-by';

import { useApi } from 'hooks/useApi';
import type { CardOptions } from 'types/cardOptions';

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
    cardOptions?: CardOptions;
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

function setDefinedParam(
    params: URLSearchParams,
    name: string,
    value: string | number | boolean | undefined
): void {
    if (value !== undefined) {
        params.set(name, String(value));
    }
}

function setNonEmptyArrayParam(
    params: URLSearchParams,
    name: string,
    value: string[] | undefined
): void {
    if (value?.length) {
        params.set(name, value.join(','));
    }
}

const fetchUnifiedSearch = async (
    api: Api,
    query: UnifiedSearchQuery,
    options?: AxiosRequestConfig
) => {
    const params = new URLSearchParams();
    setDefinedParam(params, 'userId', query.userId);
    setDefinedParam(params, 'searchTerm', query.searchTerm);
    setDefinedParam(params, 'parentId', query.parentId);
    setDefinedParam(params, 'collectionType', query.collectionType);
    setNonEmptyArrayParam(params, 'includeItemTypes', query.includeItemTypes);
    setNonEmptyArrayParam(params, 'excludeItemTypes', query.excludeItemTypes);
    setNonEmptyArrayParam(params, 'mediaTypes', query.mediaTypes);
    setDefinedParam(params, 'limit', query.limit || undefined);
    setDefinedParam(params, 'startIndex', query.startIndex || undefined);
    setDefinedParam(params, 'includePeople', query.includePeople);
    setDefinedParam(params, 'includeMedia', query.includeMedia);
    setDefinedParam(params, 'includeGenres', query.includeGenres);
    setDefinedParam(params, 'includeStudios', query.includeStudios);
    setDefinedParam(params, 'includeArtists', query.includeArtists);
    setNonEmptyArrayParam(params, 'sortBy', query.sortBy);
    setDefinedParam(params, 'sortOrder', query.sortOrder);

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
    queryFn: ({ signal }) => fetchUnifiedSearch(api!, query ?? {}, { signal, headers: { 'Cache-Control': 'no-cache' } }),
    staleTime: 30000,
    enabled: !!api && !!query?.searchTerm
});

export const getSearchStatsQuery = (
    api?: Api,
    userId?: string
) => queryOptions({
    queryKey: ['Search', 'Stats', api?.basePath, userId],
    queryFn: ({ signal }) => fetchSearchStats(api!, userId!, { signal, headers: { 'Cache-Control': 'no-cache' } }),
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
