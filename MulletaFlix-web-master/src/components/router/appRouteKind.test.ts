import { expect, it } from 'vitest';
import { getRouteKind } from './appRouteKind';

it.each([
    [ '/configurationpage?name=NebulaFTP', 'dashboard' ],
    [ '/configurationpage', 'dashboard' ],
    [ '/metadata?itemId=123', 'dashboard' ],
    [ '/dashboard/plugins', 'dashboard' ],
    [ '/dashboard?tab=1', 'dashboard' ],
    [ '/wizard/start', 'wizard' ],
    [ '/home', 'main' ],
    [ '/configurationpage-other', 'main' ]
])('loads the correct route tree for %s', (path, expected) => {
    expect(getRouteKind(path)).toBe(expected);
});
