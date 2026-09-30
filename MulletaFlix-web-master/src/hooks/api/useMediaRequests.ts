import type { ActivityLogEntry } from '@jellyfin/sdk/lib/generated-client/models/activity-log-entry';
import type { ActivityLogEntryQueryResult } from '@jellyfin/sdk/lib/generated-client/models/activity-log-entry-query-result';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';

import { useApi } from 'hooks/useApi';
import type { ApiClient } from 'jellyfin-apiclient';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { classifyMediaRequests, type MediaCatalogTitle, type MediaRequestActivity } from 'utils/mediaRequests';

interface MediaRequestsResponse extends ActivityLogEntryQueryResult {
    PriorityRequestIds?: number[];
    Catalog?: MediaCatalogTitle[];
}

const getApiClient = (): ApiClient => {
    const apiClient = ServerConnections.currentApiClient() as unknown as ApiClient | null;
    if (!apiClient) throw new Error('Cliente da API indisponível.');
    return apiClient;
};

const fetchMediaCatalog = async (): Promise<MediaCatalogTitle[]> => {
    const apiClient = getApiClient();
    return await apiClient.getJSON(apiClient.getUrl('UserFeedback/MediaRequestCatalog')) as MediaCatalogTitle[];
};

const fetchMyMediaRequests = async (startIndex: number): Promise<MediaRequestsResponse> => {
    const apiClient = getApiClient();
    const url = apiClient.getUrl('UserFeedback/MediaRequests', { limit: 200, startIndex });
    return await apiClient.getJSON(url) as MediaRequestsResponse;
};

export const useMediaRequestCatalog = () => {
    const { user } = useApi();

    return useQuery({
        queryKey: [ 'UserFeedback', 'MediaRequestCatalog' ],
        queryFn: fetchMediaCatalog,
        enabled: !!user,
        staleTime: 60_000
    });
};

export const useMyMediaRequests = () => {
    const { user } = useApi();

    return useInfiniteQuery({
        queryKey: [ 'UserFeedback', 'MediaRequests', 'pages', user?.Id ],
        initialPageParam: 0,
        queryFn: ({ pageParam }) => fetchMyMediaRequests(pageParam),
        getNextPageParam: (lastPage, _pages, lastPageParam) => {
            const itemCount = lastPage.Items?.length || 0;
            const nextIndex = (lastPage.StartIndex ?? lastPageParam) + itemCount;
            return itemCount > 0 && nextIndex < (lastPage.TotalRecordCount || 0) ? nextIndex : undefined;
        },
        enabled: !!user
    });
};

/**
 * Combines the caller's own requests with the indexed catalog to split pending vs. already
 * included titles, using the same normalized title/category/year matching as the admin triage
 * grid (utils/mediaRequests) so both surfaces agree on what counts as "included".
 */
export const useMyClassifiedMediaRequests = () => {
    const requestsQuery = useMyMediaRequests();

    const isPending = requestsQuery.isPending;
    const isError = requestsQuery.isError && !requestsQuery.data;

    const classified = classifyMediaRequests<ActivityLogEntry & MediaRequestActivity>(
        requestsQuery.data?.pages.flatMap(page => page.Items || []) || [],
        requestsQuery.data?.pages.flatMap(page => page.Catalog || []) || []
    );

    const refetch = async () => {
        await requestsQuery.refetch();
    };

    return {
        ...classified,
        priorityRequestIds: new Set(requestsQuery.data?.pages.flatMap(page => page.PriorityRequestIds || []) || []),
        hasNextPage: requestsQuery.hasNextPage,
        fetchNextPage: requestsQuery.fetchNextPage,
        isFetchingNextPage: requestsQuery.isFetchingNextPage,
        isFetchNextPageError: requestsQuery.isFetchNextPageError,
        isPending,
        isError,
        refetch
    };
};
