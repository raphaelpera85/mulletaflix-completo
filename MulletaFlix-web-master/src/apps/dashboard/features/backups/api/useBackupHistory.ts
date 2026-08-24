import { useApi } from 'hooks/useApi';
import { useQuery } from '@tanstack/react-query';
import type { BackupExecutionHistoryDto } from './types';

export const BACKUP_HISTORY_QUERY_KEY = 'BackupHistory';

const fetchBackupHistory = async (api: any) => {
  const response = await api.get('/System/Backup/History');
  return response.data;
};

export const useBackupHistory = () => {
  const { api } = useApi();

  return useQuery({
    queryKey: [ BACKUP_HISTORY_QUERY_KEY, api?.basePath ],
    queryFn: () => fetchBackupHistory(api!),
    enabled: !!api
  });
};