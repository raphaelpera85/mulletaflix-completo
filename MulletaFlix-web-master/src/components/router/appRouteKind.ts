export type AppRouteKind = 'dashboard' | 'wizard' | 'main';

/** Select the lazy route tree, including dashboard pages outside /dashboard. */
export function getRouteKind(path: string): AppRouteKind {
    const pathname = path.split('?')[0];
    if ([ '/dashboard', '/configurationpage', '/metadata' ].some(root => pathname === root || pathname.startsWith(root + '/'))) return 'dashboard';
    if (pathname === '/wizard' || pathname.startsWith('/wizard/')) return 'wizard';
    return 'main';
}
