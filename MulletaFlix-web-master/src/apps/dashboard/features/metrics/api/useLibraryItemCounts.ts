import type { Api } from '@jellyfin/sdk/lib/api';
import type { ItemsApiGetItemsRequest } from '@jellyfin/sdk/lib/generated-client/api/items-api';
import type { ItemCounts } from '@jellyfin/sdk/lib/generated-client/models/item-counts';
import { BaseItemKind } from '@jellyfin/sdk/lib/generated-client/models/base-item-kind';
import { getItemsApi } from '@jellyfin/sdk/lib/utils/api/items-api';
import { queryOptions, useQuery } from '@tanstack/react-query';

import { useApi } from 'hooks/useApi';
import { useUserViews } from 'hooks/api/useUserViews';

const countTypes = [
    BaseItemKind.Movie,
    BaseItemKind.Series,
    BaseItemKind.Episode,
    BaseItemKind.MusicAlbum,
    BaseItemKind.Audio,
    BaseItemKind.MusicVideo,
    BaseItemKind.Book,
    BaseItemKind.BoxSet
];

const emptyCounts = (): ItemCounts => ({
    MovieCount: 0,
    SeriesCount: 0,
    EpisodeCount: 0,
    ArtistCount: 0,
    ProgramCount: 0,
    TrailerCount: 0,
    SongCount: 0,
    AlbumCount: 0,
    MusicVideoCount: 0,
    BoxSetCount: 0,
    BookCount: 0,
    ItemCount: 0
});

const addCount = (counts: ItemCounts, type: BaseItemKind, value: number) => {
    switch (type) {
        case BaseItemKind.Movie:
            counts.MovieCount = (counts.MovieCount ?? 0) + value;
            break;
        case BaseItemKind.Series:
            counts.SeriesCount = (counts.SeriesCount ?? 0) + value;
            break;
        case BaseItemKind.Episode:
            counts.EpisodeCount = (counts.EpisodeCount ?? 0) + value;
            break;
        case BaseItemKind.MusicAlbum:
            counts.AlbumCount = (counts.AlbumCount ?? 0) + value;
            break;
        case BaseItemKind.Audio:
            counts.SongCount = (counts.SongCount ?? 0) + value;
            break;
        case BaseItemKind.MusicVideo:
            counts.MusicVideoCount = (counts.MusicVideoCount ?? 0) + value;
            break;
        case BaseItemKind.Book:
            counts.BookCount = (counts.BookCount ?? 0) + value;
            break;
        case BaseItemKind.BoxSet:
            counts.BoxSetCount = (counts.BoxSetCount ?? 0) + value;
            break;
    }
    counts.ItemCount = (counts.ItemCount ?? 0) + value;
};

const fetchLibraryItemCounts = async (
    api: Api,
    userId: string,
    viewIds: string[],
    signal?: AbortSignal
) => {
    const result = emptyCounts();
    const request: Omit<ItemsApiGetItemsRequest, 'parentId' | 'includeItemTypes'> = {
        recursive: true,
        limit: 0,
        enableTotalRecordCount: true,
        enableImages: false
    };

    const responses = await Promise.all(viewIds.flatMap((parentId) =>
        countTypes.map((includeItemType) => getItemsApi(api).getItems(
            { userId, parentId, includeItemTypes: [includeItemType], ...request },
            { signal }
        ))
    ));

    // One request is made per user-visible library and content type. This keeps
    // dashboard totals aligned with the library pages instead of counting
    // hidden/global records.
    for (const [index, response] of responses.entries()) {
        const type = countTypes[index % countTypes.length];
        addCount(result, type, response.data.TotalRecordCount ?? 0);
    }

    return result;
};

export const useLibraryItemCounts = () => {
    const { api, user } = useApi();
    const viewsQuery = useUserViews({ userId: user?.Id });
    const viewIds = (viewsQuery.data?.Items ?? [])
        .map((view) => view.Id)
        .filter((id): id is string => !!id);

    const countsQuery = useQuery(queryOptions({
        queryKey: [ 'LibraryItemCounts', api?.basePath, user?.Id, viewIds ],
        queryFn: ({ signal }) => fetchLibraryItemCounts(api!, user!.Id!, viewIds, signal),
        enabled: !!api && !!user?.Id && viewsQuery.isSuccess,
        refetchOnWindowFocus: false
    }));

    return {
        ...countsQuery,
        isPending: viewsQuery.isPending || countsQuery.isPending
    };
};
