import { useApi } from 'hooks/useApi';
import { useQuery } from '@tanstack/react-query';
import type { PointInTimeRestoreRequestDto } from './types';

export const RESTORE_POINT_IN_TIME_QUERY_KEY = 'RestorePointInTime';

const restorePointInTime = async (api: any, data: PointInTimeRestoreRequestDto) => {
  const response = await api.post('/System/Backup/RestorePointInTime', data);
  return response.data;
};

export const useRestorePointInTime = () => {
  const { api } = useApi();

  return useQuery({
    queryKey: [ RESTORE_POINT_IN_TIME_QUERY_KEY ],
    queryFn: () => restorePointInTime(api!, { TargetDate: new Date().toISOString() }),
    enabled: false // This will be triggered manually
  });
};