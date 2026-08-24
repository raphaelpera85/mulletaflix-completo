import { queryOptions, useQuery } from '@tanstack/react-query';
import type { Api } from '@jellyfin/sdk';
import type { AxiosRequestConfig } from 'axios';

import { useApi } from './useApi';

interface ServerHealthSummaryDto {
  timestamp: string;
  serverName: string;
  version: string;
  hasPendingRestart: boolean;
  isShuttingDown: boolean;
  storage: StorageHealthDto;
  tasks: TaskHealthDto;
  plugins: PluginHealthDto;
  backup: BackupHealthDto;
  system: SystemHealthDto;
  overallStatus: HealthStatus;
}

type HealthStatus = 'Ok' | 'Warning' | 'Critical';

interface StorageHealthDto {
  totalFreeSpace: number;
  totalUsedSpace: number;
  criticalCount: number;
  warningCount: number;
  healthyCount: number;
  criticalPaths: string[];
  warningPaths: string[];
  status: HealthStatus;
}

interface TaskHealthDto {
  totalCount: number;
  runningCount: number;
  failedCount: number;
  overdueCount: number;
  failedTaskNames: string[];
  overdueTaskNames: string[];
  runningTaskNames: string[];
  status: HealthStatus;
}

interface PluginHealthDto {
  totalCount: number;
  enabledCount: number;
  disabledCount: number;
  incompatibleCount: number;
  updateAvailableCount: number;
  incompatibleNames: string[];
  updateAvailableNames: string[];
  status: HealthStatus;
}

interface BackupHealthDto {
  totalBackups: number;
  lastBackupTime: string | null;
  lastSuccessfulBackupTime: string | null;
  lastBackupResult: string | null;
  lastBackupSize: string;
  status: HealthStatus;
}

interface SystemHealthDto {
  hasPendingRestart: boolean;
  hasUpdateAvailable: boolean;
  startupWizardCompleted: boolean;
  status: HealthStatus;
}

const fetchHealthSummary = async (
    api: Api,
    options?: AxiosRequestConfig
) => {
    const response = await api.axiosInstance.request({
        url: '/ServerHealth/Summary',
        method: 'GET',
        signal: options?.signal as AbortSignal | undefined,
        headers: { 'Cache-Control': 'no-cache', ...options?.headers }
    });
    return response.data as ServerHealthSummaryDto;
};

export const getServerHealthQuery = (
    api?: Api
) => queryOptions({
    queryKey: ['ServerHealth', 'Summary', api?.basePath],
    queryFn: ({ signal }) => fetchHealthSummary(api!, { signal, headers: { 'Cache-Control': 'no-cache' }}),
    staleTime: 30000, // 30 seconds
    enabled: !!api
});

export const useServerHealth = () => {
    const { api } = useApi();
    return useQuery(getServerHealthQuery(api));
};