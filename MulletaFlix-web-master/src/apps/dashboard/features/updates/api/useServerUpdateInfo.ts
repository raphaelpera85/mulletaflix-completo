import type { Api } from '@jellyfin/sdk';
import { useQuery } from '@tanstack/react-query';
import { useApi } from 'hooks/useApi';

export interface UpdateInfoDto {
    CurrentVersion: string;
    AvailableVersion?: string;
    UpdateAvailable: boolean;
    Changelog?: string;
    LastCheckedAt?: string;
}

const fetchUpdateInfo = async (api: Api, signal?: AbortSignal) => {
    const response = await api.axiosInstance.get<UpdateInfoDto>('/System/UpdateInfo', { signal });
    return response.data;
};

export const useServerUpdateInfo = () => {
    const { api } = useApi();
    return useQuery({
        queryKey: ['UpdateInfo', api?.basePath],
        queryFn: ({ signal }) => fetchUpdateInfo(api!, signal),
        enabled: !!api
    });
};
