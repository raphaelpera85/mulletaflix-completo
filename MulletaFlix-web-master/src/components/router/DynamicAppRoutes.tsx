import React, { lazy, Suspense, useMemo } from 'react';
import { type RouteObject, useLocation, useRoutes } from 'react-router-dom';

import Loading from 'components/loading/LoadingComponent';
import layoutManager from 'components/layoutManager';
import { LayoutMode } from 'constants/layoutMode';

type RouteModule = Partial<Record<
    'DASHBOARD_APP_ROUTES' | 'EXPERIMENTAL_APP_ROUTES' | 'STABLE_APP_ROUTES' | 'WIZARD_APP_ROUTES',
    RouteObject[]
>>;
type RouteKind = 'dashboard' | 'wizard' | 'main';

const routeLoaders: Record<RouteKind, () => Promise<RouteModule>> = {
    dashboard: () => import('apps/dashboard/routes/routes') as unknown as Promise<RouteModule>,
    wizard: () => import('apps/wizard/routes/routes') as unknown as Promise<RouteModule>,
    main: loadMainRoutes
};

function loadMainRoutes(): Promise<RouteModule> {
    if (layoutManager.layout === LayoutMode.Experimental) {
        return import('apps/experimental/routes/routes') as unknown as Promise<RouteModule>;
    }

    return import('apps/stable/routes/routes') as unknown as Promise<RouteModule>;
}

function getRoutes(kind: RouteKind, routeModule: RouteModule): RouteObject[] | undefined {
    if (kind === 'dashboard') return routeModule.DASHBOARD_APP_ROUTES;
    if (kind === 'wizard') return routeModule.WIZARD_APP_ROUTES;
    if (routeModule.EXPERIMENTAL_APP_ROUTES) return routeModule.EXPERIMENTAL_APP_ROUTES;
    return routeModule.STABLE_APP_ROUTES;
}

const routeComponents = new Map<RouteKind, ReturnType<typeof lazy>>();

function getRouteKind(pathname: string): RouteKind {
    if (pathname === '/dashboard' || pathname.startsWith('/dashboard/')) return 'dashboard';
    if (pathname === '/wizard' || pathname.startsWith('/wizard/')) return 'wizard';
    return 'main';
}

function getRouteComponent(kind: RouteKind) {
    const existing = routeComponents.get(kind);
    if (existing) return existing;

    const component = lazy(async () => {
        const routeModule = await routeLoaders[kind]();
        const routes = getRoutes(kind, routeModule);

        function RouteTree() {
            return useRoutes(routes || []);
        }

        return {
            default: RouteTree
        };
    });

    routeComponents.set(kind, component);
    return component;
}

export default function DynamicAppRoutes() {
    const { pathname } = useLocation();
    const kind = getRouteKind(pathname);
    const RouteComponent = useMemo(() => getRouteComponent(kind), [kind]);

    return (
        <Suspense fallback={<Loading />}>
            <RouteComponent />
        </Suspense>
    );
}
