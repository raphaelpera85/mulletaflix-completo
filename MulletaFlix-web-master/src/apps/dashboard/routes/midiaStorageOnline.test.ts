import { describe, expect, it } from 'vitest';

import { ASYNC_ADMIN_ROUTES } from './_asyncRoutes';
import {
    LEGACY_MIDIA_STORAGE_ONLINE_DASHBOARD_PATH,
    LEGACY_MIDIA_STORAGE_ONLINE_ROUTE,
    MIDIA_STORAGE_ONLINE_DASHBOARD_PATH,
    MIDIA_STORAGE_ONLINE_ROUTE
} from './midiaStorageOnline';

describe('midia storage online dashboard routing', () => {
    it('moves the page under plugins and keeps a legacy redirect path', () => {
        const routes = ASYNC_ADMIN_ROUTES.map(route => route.path);

        expect(MIDIA_STORAGE_ONLINE_ROUTE).toBe('plugins/midia-storage-online');
        expect(MIDIA_STORAGE_ONLINE_DASHBOARD_PATH).toBe('/dashboard/plugins/midia-storage-online');
        expect(LEGACY_MIDIA_STORAGE_ONLINE_ROUTE).toBe('libraries/midia-storage-online');
        expect(LEGACY_MIDIA_STORAGE_ONLINE_DASHBOARD_PATH).toBe('/dashboard/libraries/midia-storage-online');
        expect(routes).toContain(MIDIA_STORAGE_ONLINE_ROUTE);
        expect(routes).not.toContain(LEGACY_MIDIA_STORAGE_ONLINE_ROUTE);
    });
});