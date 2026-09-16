import type { Api } from '@jellyfin/sdk';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useApi } from 'hooks/useApi';

export interface UpdateInfoDto {
    CurrentVersion: string;
    AvailableVersion?: string;
    UpdateAvailable: boolean;
    Changelog?: string;
    LastCheckedAt?: string;
    ArchiveUrl?: string;
    PackageSize?: number;
    InstallState: 'Idle' | 'Downloading' | 'Extracting' | 'ReadyToApply' | 'Applying' | 'Failed';
    InstallProgress: number;
    ErrorMessage?: string;
}

const fetchUpdateInfo = async (api: Api, signal?: AbortSignal) => {
    const response = await api.axiosInstance.get<UpdateInfoDto>('/System/UpdateInfo', { signal });
    return response.data;
};

const fetchUpdateStatus = async (api: Api, signal?: AbortSignal) => {
    const response = await api.axiosInstance.get<UpdateInfoDto>('/System/Update/Status', { signal });
    return response.data;
};

export const useServerUpdateInfo = () => {
    const { api } = useApi();
    return useQuery({
        queryKey: ['UpdateInfo', api?.basePath],
        queryFn: ({ signal }) => fetchUpdateInfo(api!, signal),
        enabled: !!api,
        retry: 2
    });
};

export const useUpdateStatus = (enabled = true, refetchInterval: number | false = false) => {
    const { api } = useApi();
    return useQuery({
        queryKey: ['UpdateStatus', api?.basePath],
        queryFn: ({ signal }) => fetchUpdateStatus(api!, signal),
        enabled: !!api && enabled,
        refetchInterval,
        retry: 1
    });
};

export const useInstallUpdate = () => {
    const { api } = useApi();
    return useMutation({
        mutationFn: async () => {
            const response = await api!.axiosInstance.post<UpdateInfoDto>('/System/Update/Install');
            return response.data;
        }
    });
};

export const useApplyUpdate = () => {
    const { api } = useApi();
    return useMutation({
        mutationFn: async () => {
            const response = await api!.axiosInstance.post('/System/Update/Apply');
            return response.data;
        }
    });
};

