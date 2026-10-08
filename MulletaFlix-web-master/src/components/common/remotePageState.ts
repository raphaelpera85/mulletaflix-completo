import type { PageState } from './PageStateContainer';

interface RemotePageStateInput {
    isPending: boolean;
    isError: boolean;
    hasData: boolean;
    isOnline: boolean;
}

/** Resolves remote-data states without discarding a previously loaded snapshot. */
export const getRemotePageState = ({ isPending, isError, hasData, isOnline }: RemotePageStateInput): PageState => {
    if (!isOnline) {
        return hasData ? 'degraded' : 'offline';
    }

    if (isError) {
        return hasData ? 'degraded' : 'error';
    }

    if (isPending && !hasData) {
        return 'loading';
    }

    return 'success';
};
