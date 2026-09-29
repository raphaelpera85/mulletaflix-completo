import { queryOptions, useQuery } from '@tanstack/react-query';
import type { Api } from '@jellyfin/sdk';
import type { AxiosRequestConfig } from 'axios';

import { useApi } from './useApi';

export type OperationalAlertSeverity = 'Info' | 'Warning' | 'Critical';

export interface OperationalAlertDto {
    kind: string;
    severity: OperationalAlertSeverity;
    message: string;
}

const fetchOperationalAlerts = async (
    api: Api,
    options?: AxiosRequestConfig
) => {
    const response = await api.axiosInstance.request({
        url: '/ServerHealth/Alerts',
        method: 'GET',
        signal: options?.signal as AbortSignal | undefined,
        headers: { 'Cache-Control': 'no-cache', ...options?.headers }
    });
    return response.data as OperationalAlertDto[];
};

export const getOperationalAlertsQuery = (
    api?: Api
) => queryOptions({
    queryKey: ['ServerHealth', 'Alerts', api?.basePath],
    queryFn: ({ signal }) => fetchOperationalAlerts(api!, { signal }),
    staleTime: 30000, // 30 seconds
    enabled: !!api
});

export const useOperationalAlerts = () => {
    const { api } = useApi();
    return useQuery(getOperationalAlertsQuery(api));
};
