import { useQuery } from '@tanstack/react-query';
import { useApi } from 'hooks/useApi';

export interface SupabaseBackupStatus {
    IsConfigured: boolean;
    IsConnected: boolean;
    SupabaseUrl?: string;
    AutoBackupEnabled?: boolean;
    AutoBackupIntervalHours?: number;
    AutoUsersBackupIntervalHours?: number;
    LastBackupTime?: string | null;
    LastBackupAttemptTime?: string | null;
    LastBackupStatus?: string;
    LastBackupFailed?: boolean | null;
    LastBackupProcessedFilesCount?: number | null;
    LastBackupProcessedUsersCount?: number | null;
    LastUsersBackupTime?: string | null;
    LastUsersBackupStatus?: string;
    LastUsersBackupCount?: number;
    LastUsersBackupFailed?: boolean | null;
    LastRestoreTime?: string | null;
    LastRestoreStatus?: string;
    LastRestoreFailed?: boolean;
    LastRestoreFilesRestored?: number | null;
    LastRestoreUsersRestored?: number | null;
    LastRestoreFtpUsersRestored?: number | null;
    LastRestoreAppUsersRestored?: number | null;
    Message?: string;
}

export const useSupabaseBackupStatus = () => {
    const { api } = useApi();
    return useQuery({
        queryKey: [ 'SupabaseBackupStatus', api?.basePath ],
        queryFn: async ({ signal }) => {
            const response = await api!.axiosInstance.get<SupabaseBackupStatus>('/NebulaFtp/Supabase/Status', { signal });
            return response.data;
        },
        enabled: !!api,
        staleTime: 30_000,
        refetchInterval: 60_000
    });
};
