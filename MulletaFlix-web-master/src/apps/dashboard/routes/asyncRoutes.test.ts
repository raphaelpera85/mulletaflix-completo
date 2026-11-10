import { describe, expect, it } from 'vitest';

import { ASYNC_ADMIN_ROUTES } from './_asyncRoutes';

describe('dashboard async routes', () => {
    it('loads the action log page for the logs route', () => {
        const logsRoute = ASYNC_ADMIN_ROUTES.find(route => route.path === 'logs');

        expect(logsRoute?.page).toBe('action-log');
    });
});
