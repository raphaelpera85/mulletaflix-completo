import { useQuery } from '@tanstack/react-query';

import { useApi } from 'hooks/useApi';

import { getUserQuery, USER_QUERY_KEY } from './userQuery';

/** UserApiGetUserByIdRequest without required userId */
interface GetUserByIdParams {
    userId?: string;
}

export const QUERY_KEY = USER_QUERY_KEY;

export { getUserQuery } from './userQuery';

export const useUser = ({ userId }: GetUserByIdParams) => {
    const { api, user } = useApi();

    return useQuery(getUserQuery(api, { userId: userId || user?.Id }));
};
