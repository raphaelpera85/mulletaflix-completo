import type { ActivityLogEntry } from '@jellyfin/sdk/lib/generated-client/models/activity-log-entry';
import type { ActivityLogEntryQueryResult } from '@jellyfin/sdk/lib/generated-client/models/activity-log-entry-query-result';
import { useQuery } from '@tanstack/react-query';

import { useApi } from 'hooks/useApi';
import type { ApiClient } from 'jellyfin-apiclient';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { classifyMediaRequests, type MediaCatalogTitle, type MediaRequestActivity } from 'utils/mediaRequests';

const getApiClient = (): ApiClient => {
    const apiClient = ServerConnections.currentApiClient() as unknown as ApiClient | null;
    if (!apiClient) throw new Error('Cliente da API indisponível.');
    return apiClient;
};

const fetchMediaCatalog = async (): Promise<MediaCatalogTitle[]> => {
    const apiClient = getApiClient();
    return await apiClient.getJSON(apiClient.getUrl('UserFeedback/MediaRequestCatalog')) as MediaCatalogTitle[];
};

const fetchMyMediaRequests = async (): Promise<ActivityLogEntryQueryResult> => {
    const apiClient = getApiClient();
    const url = apiClient.getUrl('UserFeedback/MediaRequests', { limit: 200 });
    return await apiClient.getJSON(url) as ActivityLogEntryQueryResult;
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

    return useQuery({
        queryKey: [ 'UserFeedback', 'MediaRequests', user?.Id ],
        queryFn: fetchMyMediaRequests,
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
    const catalogQuery = useMediaRequestCatalog();

    const isPending = requestsQuery.isPending || catalogQuery.isPending;
    const isError = requestsQuery.isError || catalogQuery.isError;

    const classified = classifyMediaRequests<ActivityLogEntry & MediaRequestActivity>(
        requestsQuery.data?.Items || [],
        catalogQuery.data || []
    );

    const refetch = async () => {
        await Promise.all([ requestsQuery.refetch(), catalogQuery.refetch() ]);
    };

    return { ...classified, isPending, isError, refetch };
};
