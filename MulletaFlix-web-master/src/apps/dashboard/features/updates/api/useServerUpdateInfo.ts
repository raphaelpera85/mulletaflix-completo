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

const getHeaders = (api: Api) => {
    const auth = (api as unknown as { authorizationHeader?: string }).authorizationHeader;
    return auth ? { Authorization: auth } : undefined;
};

const fetchUpdateInfo = async (api: Api, signal?: AbortSignal) => {
    const response = await api.axiosInstance.get<UpdateInfoDto>('/System/UpdateInfo', {
        signal,
        headers: getHeaders(api)
    });
    return response.data;
};

const fetchUpdateStatus = async (api: Api, signal?: AbortSignal) => {
    const response = await api.axiosInstance.get<UpdateInfoDto>('/System/Update/Status', {
        signal,
        headers: getHeaders(api)
    });
    const rawData = response.data as any;
    const data = (rawData && rawData.Result) ? rawData.Result : rawData;
    if (data) {
        if (!data.InstallState && data.State) {
            data.InstallState = data.State;
        }
        if (data.InstallProgress === undefined && data.Progress !== undefined) {
            data.InstallProgress = data.Progress;
        }
    }
    return data as UpdateInfoDto;
};

export const useServerUpdateInfo = () => {
    const { api } = useApi();
    return useQuery({
        queryKey: ['UpdateInfo', api?.basePath],
        queryFn: ({ signal }) => fetchUpdateInfo(api!, signal),
        enabled: !!api,
        refetchOnWindowFocus: true,
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
            const response = await api!.axiosInstance.post<UpdateInfoDto>('/System/Update/Install', undefined, {
                headers: getHeaders(api!)
            });
            return response.data;
        }
    });
};

export const useApplyUpdate = () => {
    const { api } = useApi();
    return useMutation({
        mutationFn: async () => {
            const response = await api!.axiosInstance.post('/System/Update/Apply', undefined, {
                headers: getHeaders(api!)
            });
            return response.data;
        }
    });
};
