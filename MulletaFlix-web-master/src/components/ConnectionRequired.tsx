import React, { FunctionComponent, useCallback, useEffect, useRef, useState } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import type { ApiClient, ConnectResponse } from 'jellyfin-apiclient';

import { ConnectionState, ServerConnections } from 'lib/jellyfin-apiclient';
import { getServerEndpoint } from 'utils/url';

import ConnectionErrorPage from './ConnectionErrorPage';
import Loading from './loading/LoadingComponent';

enum AccessLevel {
    /** Requires a user with administrator access */
    Admin = 'admin',
    /** No access restrictions */
    Public = 'public',
    /** Requires a valid user session */
    User = 'user',
    /** Requires the startup wizard to NOT be completed */
    Wizard = 'wizard'
};

type AccessLevelValue = `${AccessLevel}`;

enum BounceRoutes {
    Home = '/home',
    Login = '/login',
    SelectServer = '/selectserver',
    StartWizard = '/wizard/start'
}

type ConnectionRequiredProps = {
    level?: AccessLevelValue
};

type ClientAccessApi = {
    isLoggedIn?: () => boolean;
    getCurrentUser?: () => Promise<{ Policy?: { IsAdministrator?: boolean } }>;
};

const isClientLoggedIn = (apiClient: unknown): boolean => {
    const typedClient = apiClient as ClientAccessApi | null | undefined;
    return typeof typedClient?.isLoggedIn === 'function' && Boolean(typedClient.isLoggedIn());
};

const ERROR_STATES = [
    ConnectionState.ServerMismatch,
    ConnectionState.ServerUpdateNeeded,
    ConnectionState.Unavailable
];

const SESSION_VALIDATION_TIMEOUT_MS = 5000;

async function withTimeout<T>(promise: Promise<T>, timeoutMs: number): Promise<T> {
    let timeoutId: ReturnType<typeof setTimeout> | undefined;
    const timeout = new Promise<never>((_, reject) => {
        timeoutId = setTimeout(() => reject(new Error('Session validation timed out')), timeoutMs);
    });

    try {
        return await Promise.race([promise, timeout]);
    } finally {
        if (timeoutId !== undefined) clearTimeout(timeoutId);
    }
}

const fetchPublicSystemInfo = async (apiClient: Pick<ApiClient, 'serverAddress'>) => {
    const infoResponse = await fetch(
        getServerEndpoint(apiClient.serverAddress(), '/System/Info/Public'),
        { cache: 'no-cache' }
    );

    if (!infoResponse.ok) {
        throw new Error('Public system info request failed');
    }

    return infoResponse.json();
};

/**
 * A component that ensures a server connection has been established.
 * Additional parameters exist to verify a user or admin have authenticated.
 * If a condition fails, this component will navigate to the appropriate page.
 */
const ConnectionRequired: FunctionComponent<ConnectionRequiredProps> = ({
    level = 'user'
}) => {
    const navigate = useNavigate();
    const location = useLocation();

    const [ errorState, setErrorState ] = useState<ConnectionState>();
    const [ isLoading, setIsLoading ] = useState(true);
    const isMountedRef = useRef(false);

    const setLoadingState = useCallback((loading: boolean) => {
        if (isMountedRef.current) setIsLoading(loading);
    }, []);

    const setConnectionError = useCallback((state: ConnectionState) => {
        if (isMountedRef.current) {
            setErrorState(state);
            setIsLoading(false);
        }
    }, []);

    const navigateIfNotThere = useCallback(async (route: BounceRoutes) => {
        // If we try to navigate to the current route, just set isLoading = false
        if (location.pathname === route) setLoadingState(false);
        // Otherwise navigate to the route
        else await navigate(route);
    }, [ location.pathname, navigate, setLoadingState ]);

    const bounce = useCallback(async (connectionResponse: ConnectResponse) => {
        switch (connectionResponse.State) {
            case ConnectionState.SignedIn:
                // Already logged in, bounce to the home page
                console.debug('[ConnectionRequired] already logged in, redirecting to home');
                await navigate(BounceRoutes.Home);
                return;
            case ConnectionState.ServerSignIn:
                // Bounce to the login page
                if (location.pathname === BounceRoutes.Login) {
                    setLoadingState(false);
                } else {
                    console.debug('[ConnectionRequired] not logged in, redirecting to login page', location);
                    const url = encodeURIComponent(location.pathname + location.search);
                    await navigate(`${BounceRoutes.Login}?serverid=${connectionResponse.ApiClient.serverId()}&url=${url}`);
                }
                return;
            case ConnectionState.ServerSelection:
                // Bounce to select server page
                console.debug('[ConnectionRequired] redirecting to select server page');
                await navigateIfNotThere(BounceRoutes.SelectServer);
                return;
        }

        console.warn('[ConnectionRequired] unhandled connection state', connectionResponse.State);
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [ navigateIfNotThere, location.pathname, navigate, setLoadingState ]);

    const handleWizard = useCallback(async (firstConnection: ConnectResponse | null) => {
        const apiClient = firstConnection?.ApiClient || ServerConnections.currentApiClient();
        if (!apiClient) {
            throw new Error('No ApiClient available');
        }

        // Register the client before the secondary public-info check. During
        // first startup that check can be delayed while the server finishes
        // booting, which otherwise leaves the wizard permanently blank even
        // though the initial connection already succeeded.
        ServerConnections.setLocalApiClient(apiClient as Parameters<typeof ServerConnections.setLocalApiClient>[0]);
        setLoadingState(false);

        const systemInfo = await fetchPublicSystemInfo(apiClient);
        if (systemInfo?.StartupWizardCompleted) {
            console.info('[ConnectionRequired] startup wizard is complete, redirecting home');
            await navigate(BounceRoutes.Home);
        }
    }, [ navigate, setLoadingState ]);

    const handleIncompleteWizard = useCallback(async (firstConnection: ConnectResponse) => {
        if (firstConnection.State === ConnectionState.ServerSignIn) {
            // Verify the wizard is complete
            try {
                const systemInfo = await fetchPublicSystemInfo(firstConnection.ApiClient);
                if (!systemInfo?.StartupWizardCompleted) {
                    // Update the current ApiClient
                    // TODO: Is there a better place to handle this?
                    ServerConnections.setLocalApiClient(firstConnection.ApiClient as unknown as Parameters<typeof ServerConnections.setLocalApiClient>[0]);
                    // Bounce to the wizard
                    console.info('[ConnectionRequired] startup wizard is not complete, redirecting there');
                    await navigate(BounceRoutes.StartWizard);
                    return;
                }
            } catch (ex) {
                console.error('[ConnectionRequired] checking wizard status failed', ex);
                return;
            }
        }

        // Bounce to the correct page in the login flow
        return bounce(firstConnection)
            .catch(err => {
                console.error('[ConnectionRequired] failed to bounce', err);
                setConnectionError(ConnectionState.Unavailable);
            });
    }, [bounce, navigate, setConnectionError]);

    const validateStoredUserSession = useCallback(async (client: unknown) => {
        try {
            const clientApi = client as ClientAccessApi;
            if (typeof clientApi.getCurrentUser !== 'function') {
                throw new Error('Client cannot validate the current user');
            }

            await withTimeout(clientApi.getCurrentUser(), SESSION_VALIDATION_TIMEOUT_MS);
            setLoadingState(false);
        } catch (ex) {
            console.warn('[ConnectionRequired] stored user session is no longer valid', ex);
            await ServerConnections.logout().catch(err => {
                console.debug('[ConnectionRequired] failed to clear invalid session on server', err);
            });
            await navigateIfNotThere(BounceRoutes.Login);
        }
    }, [navigateIfNotThere, setLoadingState]);

    const redirectUnauthenticatedUser = useCallback(async () => {
        try {
            console.warn('[ConnectionRequired] unauthenticated user attempted to access user route');
            const connection = await ServerConnections.connect();
            if (!connection.ApiClient || connection.State == null) {
                throw new Error('Connection response is incomplete');
            }
            await bounce({ ApiClient: connection.ApiClient, State: connection.State } as unknown as ConnectResponse);
        } catch (ex) {
            console.warn('[ConnectionRequired] error bouncing from user route', ex);
            setConnectionError(ConnectionState.Unavailable);
        }
    }, [bounce, setConnectionError]);

    const validateUserAccess = useCallback(async () => {
        const client = ServerConnections.currentApiClient();

        // The legacy client reports a session as logged in when it still has
        // a token, even if that token was revoked or belongs to a previous
        // server state. Validate the user with the server before mounting a
        // protected route, otherwise the page can render an empty shell while
        // every data request is rejected with 401 Invalid token.
        if (level === AccessLevel.User && isClientLoggedIn(client)) {
            await validateStoredUserSession(client);
            return;
        }

        // If this is a user route, ensure a user is logged in
        if ((level === AccessLevel.Admin || level === AccessLevel.User) && !isClientLoggedIn(client)) {
            await redirectUnauthenticatedUser();
            return;
        }

        // If this is an admin route, ensure the user has access
        if (level === AccessLevel.Admin) {
            try {
                const user = await (client as unknown as ClientAccessApi | null)?.getCurrentUser?.();
                if (!user?.Policy?.IsAdministrator) {
                    console.warn('[ConnectionRequired] normal user attempted to access admin route');
                    const connection = await ServerConnections.connect();
                    if (!connection.ApiClient || connection.State == null) {
                        throw new Error('Connection response is incomplete');
                    }
                    return bounce({ ApiClient: connection.ApiClient, State: connection.State } as unknown as ConnectResponse)
                        .catch(err => {
                            console.error('[ConnectionRequired] failed to bounce', err);
                            setConnectionError(ConnectionState.Unavailable);
                        });
                    return;
                }
            } catch (ex) {
                console.warn('[ConnectionRequired] error bouncing from admin route', ex);
                setConnectionError(ConnectionState.Unavailable);
                return;
            }
        }

        setLoadingState(false);
    }, [bounce, level, redirectUnauthenticatedUser, setConnectionError, setLoadingState, validateStoredUserSession]);

    useEffect(() => {
        isMountedRef.current = true;

        // Check connection status on initial page load
        const apiClient = ServerConnections.currentApiClient();
        const connection = Promise.resolve(ServerConnections.firstConnection ? null : ServerConnections.connect());
        connection.then(firstConnection => {
            console.debug('[ConnectionRequired] connection state', firstConnection?.State);
            ServerConnections.firstConnection = true;

            if (firstConnection && ERROR_STATES.includes(firstConnection.State as ConnectionState)) {
                setConnectionError(firstConnection.State as ConnectionState);
            } else if (level === AccessLevel.Wizard) {
                handleWizard(firstConnection as unknown as ConnectResponse)
                    .catch(err => {
                        console.error('[ConnectionRequired] could not validate wizard status', err);
                        setConnectionError(ConnectionState.Unavailable);
                    });
            } else if (
                firstConnection && firstConnection.State !== ConnectionState.SignedIn && !isClientLoggedIn(apiClient)
            ) {
                handleIncompleteWizard(firstConnection as unknown as ConnectResponse)
                    .catch(err => {
                        console.error('[ConnectionRequired] could not start wizard', err);
                        setConnectionError(ConnectionState.Unavailable);
                    });
            } else {
                validateUserAccess()
                    .catch(err => {
                        console.error('[ConnectionRequired] could not validate user access', err);
                        setConnectionError(ConnectionState.Unavailable);
                    });
            }
        }).catch(err => {
            console.error('[ConnectionRequired] failed to connect', err);
            setConnectionError(ConnectionState.Unavailable);
        });

        return () => {
            isMountedRef.current = false;
        };
    }, [handleIncompleteWizard, handleWizard, level, setConnectionError, validateUserAccess]);

    if (errorState) {
        return <ConnectionErrorPage state={errorState} />;
    }

    // The startup wizard can be opened immediately after the connection
    // manager has established the public API client. In that transition the
    // connection guard may still be completing its second public-info check;
    // keeping the outlet hidden here leaves the legacy ViewManager page with
    // an empty shell and produces a black wizard screen. The wizard itself
    // performs the remaining setup calls, so allow its route tree to mount as
    // soon as a client is available.
    const wizardClientReady = level === AccessLevel.Wizard && Boolean(ServerConnections.currentApiClient());

    if (isLoading && !wizardClientReady) {
        return <Loading />;
    }

    return <Outlet />;
};

export default ConnectionRequired;
