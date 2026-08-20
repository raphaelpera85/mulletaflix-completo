import type { Api } from '@jellyfin/sdk';
import { getQuickConnectApi } from '@jellyfin/sdk/lib/utils/api/quick-connect-api';
import { useQuery } from '@tanstack/react-query';
import type { AxiosRequestConfig } from 'axios';

import { useApi } from './useApi';

const fetchQuickConnectEnabled = async (
    api: Api,
    options?: AxiosRequestConfig
) => {
    const response = await getQuickConnectApi(api)
        .getQuickConnectEnabled(options);
    return response.data;
};

export const useQuickConnectEnabled = () => {
    const { api } = useApi();
    return useQuery({
        queryKey: [ 'QuickConnect', 'Enabled', api?.basePath ],
        queryFn: ({ signal }) => fetchQuickConnectEnabled(api!, { signal }),
        enabled: !!api
    });
};

