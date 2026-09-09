import type { Api } from '@jellyfin/sdk/lib/api';
import type { DisplayPreferencesApiGetDisplayPreferencesRequest } from '@jellyfin/sdk/lib/generated-client/api/display-preferences-api';
import { getDisplayPreferencesApi } from '@jellyfin/sdk/lib/utils/api/display-preferences-api';
import { queryOptions } from '@tanstack/react-query';
import type { AxiosRequestConfig } from 'axios';

const fetchDisplayPreferences = async (
    api: Api,
    params: DisplayPreferencesApiGetDisplayPreferencesRequest,
    options?: AxiosRequestConfig
) => {
    const response = await getDisplayPreferencesApi(api).getDisplayPreferences(params, options);
    return response.data;
};

export const getDisplayPreferencesQuery = (
    api?: Api,
    params?: DisplayPreferencesApiGetDisplayPreferencesRequest
) => queryOptions({
    queryKey: [ 'User', api?.basePath, params?.userId, 'DisplayPreferences', params?.displayPreferencesId, params?.client ],
    queryFn: ({ signal }) => fetchDisplayPreferences(api!, params!, { signal }),
    enabled: !!api && !!params
});
