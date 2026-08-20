import type { AxiosRequestConfig } from 'axios';
import type { Api } from '@jellyfin/sdk';
import type { BaseItemDto, SearchHint } from '@jellyfin/sdk/lib/generated-client';
import type { CollectionType } from '@jellyfin/sdk/lib/generated-client/models/collection-type';
import { BaseItemKind } from '@jellyfin/sdk/lib/generated-client/models/base-item-kind';
import { ItemSortBy } from '@jellyfin/sdk/lib/generated-client/models/item-sort-by';
import { getItemsApi } from '@jellyfin/sdk/lib/utils/api/items-api';
import { useQuery } from '@tanstack/react-query';

import { useApi } from 'hooks/useApi';
import { getItemTypesFromCollectionType } from '../utils/search';

type SearchSuggestionItem = BaseItemDto | SearchHint;

const fetchGetItems = async (
    api: Api,
    userId: string,
    parentId?: string,
    options?: AxiosRequestConfig
) => {
    const response = await getItemsApi(api).getItems(
        {
            userId,
            sortBy: [ItemSortBy.IsFavoriteOrLiked, ItemSortBy.Random],
            includeItemTypes: [
                BaseItemKind.Movie,
                BaseItemKind.Series,
                BaseItemKind.MusicArtist
            ],
            limit: 20,
            recursive: true,
            imageTypeLimit: 0,
            enableImages: false,
            parentId,
            enableTotalRecordCount: false
        },
        options
    );
    return response.data.Items || [];
};

const fetchSearchHints = async (
    legacyApiClient: NonNullable<ReturnType<typeof useApi>['__legacyApiClient__']>,
    userId: string,
    searchTerm: string,
    parentId?: string,
    collectionType?: CollectionType,
    options?: AxiosRequestConfig
) => {
    const response = await legacyApiClient.getSearchHints({
        userId,
        searchTerm,
        parentId,
        includeItemTypes: collectionType ? getItemTypesFromCollectionType(collectionType) : undefined,
        limit: 20,
        includeMedia: true,
        includePeople: true,
        includeGenres: true,
        includeStudios: true,
        includeArtists: true,
        signal: options?.signal
    });

    return response?.SearchHints || [];
};

export const useSearchSuggestions = (
    parentId?: string,
    searchTerm?: string,
    collectionType?: CollectionType
) => {
    const { api, user, __legacyApiClient__: legacyApiClient } = useApi();
    const userId = user?.Id;
    const normalizedSearchTerm = searchTerm?.trim();
    const useHints = !!normalizedSearchTerm;

    return useQuery<SearchSuggestionItem[]>({
        queryKey: ['SearchSuggestions', api?.basePath, userId, parentId, collectionType, normalizedSearchTerm ?? ''],
        queryFn: ({ signal }) => {
            if (useHints) {
                return fetchSearchHints(legacyApiClient!, userId!, normalizedSearchTerm!, parentId, collectionType, { signal });
            }

            return fetchGetItems(api!, userId!, parentId, { signal });
        },
        staleTime: 120_000,
        gcTime: 300_000,
        refetchOnWindowFocus: false,
        enabled: !!api && !!userId && (!useHints || !!legacyApiClient)
    });
};
