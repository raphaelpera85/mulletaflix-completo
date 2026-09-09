import type { Api } from '@jellyfin/sdk/lib/api';
import type { UserApiGetUserByIdRequest } from '@jellyfin/sdk/lib/generated-client/api/user-api';
import { getUserApi } from '@jellyfin/sdk/lib/utils/api/user-api';
import { queryOptions } from '@tanstack/react-query';
import type { AxiosRequestConfig } from 'axios';

export const USER_QUERY_KEY = 'User';

interface GetUserByIdParams {
    userId?: string;
}

const fetchUser = async (
    api: Api,
    params: UserApiGetUserByIdRequest,
    options?: AxiosRequestConfig
) => {
    const response = await getUserApi(api).getUserById(params, options);
    return response.data;
};

export const getUserQuery = (
    api?: Api,
    { userId }: GetUserByIdParams = {}
) => queryOptions({
    queryKey: [ USER_QUERY_KEY, api?.basePath, userId ],
    queryFn: ({ signal }) => fetchUser(api!, { userId: userId! }, { signal }),
    enabled: !!api && !!userId
});
