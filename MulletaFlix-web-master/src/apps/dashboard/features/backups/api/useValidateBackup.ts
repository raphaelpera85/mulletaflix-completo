import { useApi } from 'hooks/useApi';
import { useMutation } from '@tanstack/react-query';

export const useValidateBackup = () => {
    const { api } = useApi();

    return useMutation({
        mutationFn: async (path: string) => {
            if (!api) {
                throw new Error('API not available');
            }
            const response = await api.axiosInstance.get<boolean>('/System/Backup/Validate', {
                params: { path }
            });
            return response.data;
        }
    });
};
