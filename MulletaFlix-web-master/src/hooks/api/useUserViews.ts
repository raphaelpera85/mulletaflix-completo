import type { Api } from '@jellyfin/sdk/lib/api';
import type { UserViewsApiGetUserViewsRequest } from '@jellyfin/sdk/lib/generated-client/api/user-views-api';
import { getUserViewsApi } from '@jellyfin/sdk/lib/utils/api/user-views-api';
import { queryOptions, useQuery } from '@tanstack/react-query';
import type { AxiosRequestConfig } from 'axios';

import { useApi } from '../useApi';

const fetchUserViews = async (
    api: Api,
    params?: UserViewsApiGetUserViewsRequest,
    options?: AxiosRequestConfig
) => {
    const response = await getUserViewsApi(api)
        .getUserViews(params, options);
    return response.data;
};

export const getUserViewsQuery = (
    api?: Api,
    params?: UserViewsApiGetUserViewsRequest,
    userId?: string
) => queryOptions({
    queryKey: [ 'User', api?.basePath, userId ?? params?.userId, 'Views', params ],
    queryFn: ({ signal }) => fetchUserViews(api!, params, { signal }),
    // F-4: Cache user views for 60 seconds to avoid repeating requests on every
    // page transition and component mount across toolbar, drawer, and home sections.
    staleTime: 60_000, // 60 seconds
    enabled: !!api && !!(userId ?? params?.userId)
});

export const useUserViews = (
    params?: UserViewsApiGetUserViewsRequest
) => {
    const { api, user } = useApi();
    return useQuery(getUserViewsQuery(api, {
        ...params,
        userId: params?.userId || user?.Id
    }, user?.Id));
};

