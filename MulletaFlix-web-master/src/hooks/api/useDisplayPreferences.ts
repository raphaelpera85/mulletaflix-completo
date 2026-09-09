import { DisplayPreferencesApiGetDisplayPreferencesRequest } from '@jellyfin/sdk/lib/generated-client/api/display-preferences-api';
import { useQuery } from '@tanstack/react-query';

import { useApi } from 'hooks/useApi';
import { getDisplayPreferencesQuery } from './displayPreferencesQuery';

export { getDisplayPreferencesQuery } from './displayPreferencesQuery';

export const useDisplayPreferences = (
    params: DisplayPreferencesApiGetDisplayPreferencesRequest
) => {
    const { api, user } = useApi();
    return useQuery(getDisplayPreferencesQuery(api, {
        ...params,
        userId: params?.userId || user?.Id
    }));
};
