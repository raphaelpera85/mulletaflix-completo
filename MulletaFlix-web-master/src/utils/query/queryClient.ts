import { QueryClient } from '@tanstack/react-query';

export const queryClient = new QueryClient({
    defaultOptions: {
        mutations: {
            networkMode: 'always' // network connection is not required if running on localhost
        },
        queries: {
            networkMode: 'always', // network connection is not required if running on localhost

            // Without a default staleTime every query is stale the moment it resolves, so the same
            // payload is refetched on every mount (home -> library -> details -> back re-requests
            // the item, its parent, the views and the resume rows). Freshness for the data that
            // actually changes is already driven by the websocket subscriptions in ItemsContainer
            // (UserDataChanged / LibraryChanged) which call invalidateQueries, so a short shared
            // staleTime removes the redundant round trips without showing stale playstate.
            staleTime: 30_000,

            // TVs and desktop clients fire focus changes constantly; refetching every mounted query
            // on each one is the bulk of the per-interaction round trips on remote-control navigation.
            refetchOnWindowFocus: false
        }
    }
});
