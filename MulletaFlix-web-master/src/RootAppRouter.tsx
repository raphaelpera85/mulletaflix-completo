import { ThemeProvider } from '@mui/material/styles';
import React from 'react';
import {
    RouterProvider,
    createHashRouter,
    Outlet,
    useLocation
} from 'react-router-dom';
import type { RouteObject } from 'react-router-dom';

import AppHeader from 'components/AppHeader';
import AppBody from 'components/AppBody';
import Backdrop from 'components/Backdrop';
import layoutManager from 'components/layoutManager';
import Loading from 'components/loading/LoadingComponent';
import BangRedirect from 'components/router/BangRedirect';
import { createRouterHistory, setRouterHistory } from 'components/router/routerHistory';
import { LayoutMode } from 'constants/layoutMode';
import appTheme from 'themes';
import { ThemeStorageManager } from 'themes/themeStorageManager';

const ROOT_ROUTE_ID = 'root';

type AppRouteKind = 'dashboard' | 'wizard' | 'main';

/**
 * Lazily import each app's route tree instead of pulling every app's routes
 * (dashboard, wizard, experimental/stable) into the initial bundle. This uses
 * React Router's built-in `patchRoutesOnNavigation` ("Fog of War") API so the
 * real data router keeps handling `route.lazy`, `errorElement`, and
 * `HydrateFallback` per route tree, instead of an ad-hoc nested `useRoutes()`
 * shadow router (see the abandoned components/router/DynamicAppRoutes.tsx,
 * which was replaced wholesale by static imports without a documented reason
 * — this rewrite avoids repeating whatever caused that path to be dropped by
 * staying inside the router's own supported route-discovery mechanism).
 */
const routeLoaders: Record<AppRouteKind, () => Promise<RouteObject[]>> = {
    dashboard: () => import('apps/dashboard/routes/routes').then(m => m.DASHBOARD_APP_ROUTES),
    wizard: () => import('apps/wizard/routes/routes').then(m => m.WIZARD_APP_ROUTES),
    main: () => (
        layoutManager.layout === LayoutMode.Experimental
            ? import('apps/experimental/routes/routes').then(m => m.EXPERIMENTAL_APP_ROUTES)
            : import('apps/stable/routes/routes').then(m => m.STABLE_APP_ROUTES)
    )
};

const loadedKinds = new Set<AppRouteKind>();
const pendingLoads = new Map<AppRouteKind, Promise<void>>();

function getRouteKind(pathname: string): AppRouteKind {
    if (pathname === '/dashboard' || pathname.startsWith('/dashboard/')) return 'dashboard';
    if (pathname === '/wizard' || pathname.startsWith('/wizard/')) return 'wizard';
    return 'main';
}

function loadRouteKind(
    kind: AppRouteKind,
    patch: (routeId: string | null, children: RouteObject[]) => void
): Promise<void> {
    const pending = pendingLoads.get(kind);
    if (pending) return pending;

    const promise = routeLoaders[kind]()
        .then(routes => {
            patch(ROOT_ROUTE_ID, routes);
            loadedKinds.add(kind);
        })
        .catch(error => {
            // Allow a retry on the next navigation instead of caching the failure forever.
            pendingLoads.delete(kind);
            console.error(`[RootAppRouter] failed to load route tree "${kind}"`, error);
            throw error;
        });

    pendingLoads.set(kind, promise);
    return promise;
}

const router = createHashRouter([
    {
        id: ROOT_ROUTE_ID,
        element: <RootAppLayout />,
        HydrateFallback: RouterHydrateFallback,
        children: [
            {
                path: '!/*',
                Component: BangRedirect
            }
        ]
    }
], {
    async patchRoutesOnNavigation({ path, patch }) {
        const kind = getRouteKind(path);
        if (loadedKinds.has(kind)) return;
        await loadRouteKind(kind, patch);
    }
});

export const history = createRouterHistory(router);
setRouterHistory(history);

export default function RootAppRouter() {
    return <RouterProvider router={router} />;
}

/**
 * Layout component that renders legacy components required on all pages.
 * NOTE: The app will crash if these get removed from the DOM.
 */
function RootAppLayout() {
    const location = useLocation();
    const isWizardRoute = location.pathname.startsWith('/wizard');

    return (
        <ThemeProvider
            theme={appTheme}
            defaultMode='dark'
            storageManager={ThemeStorageManager}
        >
            <Backdrop />
            <AppHeader isHidden />

            {isWizardRoute ? (
                <AppBody>
                    <Outlet />
                </AppBody>
            ) : <Outlet />}
        </ThemeProvider>
    );
}

function RouterHydrateFallback() {
    return <Loading />;
}
