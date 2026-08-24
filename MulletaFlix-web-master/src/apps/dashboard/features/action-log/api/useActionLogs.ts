import { queryOptions, useQuery } from '@tanstack/react-query';
import type { Api } from '@jellyfin/sdk';
import type { AxiosRequestConfig } from 'axios';

import { useApi } from 'hooks/useApi';

interface ActionLogDto {
  id: number;
  actionType: string;
  entityType: string;
  entityId: string | null;
  userId: string;
  username: string;
  dateCreated: string;
  details: string | null;
  oldValues: string | null;
  newValues: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  isSuccess: boolean;
  errorMessage: string | null;
  category: string;
}

interface ActionLogQuery {
  startIndex?: number;
  limit?: number;
  minDate?: string;
  maxDate?: string;
  actionType?: string;
  entityType?: string;
  userId?: string;
  username?: string;
  isSuccess?: boolean;
  category?: string;
}

interface ActionLogQueryResult {
  items: ActionLogDto[];
  totalRecordCount: number;
  startIndex: number;
}

const fetchActionLogs = async (
    api: Api,
    query: ActionLogQuery,
    options?: AxiosRequestConfig
) => {
    const params = new URLSearchParams();
    if (query.startIndex !== undefined) params.set('startIndex', query.startIndex.toString());
    if (query.limit !== undefined) params.set('limit', query.limit.toString());
    if (query.minDate) params.set('minDate', query.minDate);
    if (query.maxDate) params.set('maxDate', query.maxDate);
    if (query.actionType) params.set('actionType', query.actionType);
    if (query.entityType) params.set('entityType', query.entityType);
    if (query.userId) params.set('userId', query.userId);
    if (query.username) params.set('username', query.username);
    if (query.isSuccess !== undefined) params.set('isSuccess', query.isSuccess.toString());
    if (query.category) params.set('category', query.category);

    const response = await api.axiosInstance.request({
        url: `/ActionLog/Entries?${params.toString()}`,
        method: 'GET',
        signal: options?.signal as AbortSignal | undefined,
        headers: { 'Cache-Control': 'no-cache', ...options?.headers }
    });
    return response.data as ActionLogQueryResult;
};

export const getActionLogsQuery = (
    api?: Api,
    query?: ActionLogQuery
) => queryOptions({
    queryKey: ['ActionLog', 'Entries', api?.basePath, JSON.stringify(query ?? {})],
    queryFn: ({ signal }) => fetchActionLogs(api!, query ?? {}, { signal, headers: { 'Cache-Control': 'no-cache' }}),
    staleTime: 10000, // 10 seconds
    enabled: !!api
});

export const useActionLogs = (query?: ActionLogQuery) => {
    const { api } = useApi();
    return useQuery(getActionLogsQuery(api, query));
};