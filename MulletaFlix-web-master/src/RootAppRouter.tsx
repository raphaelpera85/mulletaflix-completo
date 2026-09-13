import React, { Suspense } from 'react';
import {
    RouterProvider,
    createHashRouter
} from 'react-router-dom';

import DynamicAppRoutes from 'components/router/DynamicAppRoutes';
import { createRouterHistory, setRouterHistory } from 'components/router/routerHistory';
import ThemedRootAppLayout from 'components/router/ThemedRootAppLayout';

const router = createHashRouter([
    {
        element: (
            <Suspense fallback={null}>
                <ThemedRootAppLayout />
            </Suspense>
        ),
        children: [
            {
                // Keep the remaining pathname available to the dynamically
                // selected application route tree (dashboard, wizard, or
                // stable). A plain '*' consumes the full pathname here and
                // can leave the nested useRoutes tree with no match.
                path: '/*',
                element: <DynamicAppRoutes />
            },
            {
                path: '!/*',
                lazy: async () => ({
                    Component: (await import('components/router/BangRedirect')).default
                })
            }
        ]
    }
]);

export const history = createRouterHistory(router);
setRouterHistory(history);

export default function RootAppRouter() {
    return <RouterProvider router={router} />;
}
