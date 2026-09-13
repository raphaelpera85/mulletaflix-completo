import { describe, expect, it } from 'vitest';
import { BaseItemKind } from '@jellyfin/sdk/lib/generated-client/models/base-item-kind';

import { buildUnifiedSearchParams } from './useUnifiedSearch';

describe('buildUnifiedSearchParams', () => {
    it('serializes server-side type filters and preserves the search contract', () => {
        const params = buildUnifiedSearchParams({
            userId: 'user-1',
            searchTerm: 'batman & robin',
            includeItemTypes: [BaseItemKind.Movie, BaseItemKind.Series],
            limit: 100,
            startIndex: 20,
            includePeople: true,
            includeMedia: true,
            sortBy: ['SortName'],
            sortOrder: 'asc'
        });

        expect(params.get('userId')).toBe('user-1');
        expect(params.get('searchTerm')).toBe('batman & robin');
        expect(params.get('includeItemTypes')).toBe('Movie,Series');
        expect(params.get('limit')).toBe('100');
        expect(params.get('startIndex')).toBe('20');
        expect(params.get('includePeople')).toBe('true');
        expect(params.get('includeMedia')).toBe('true');
        expect(params.get('sortBy')).toBe('SortName');
        expect(params.get('sortOrder')).toBe('asc');
    });

    it('omits optional empty arrays and undefined values', () => {
        const params = buildUnifiedSearchParams({
            searchTerm: 'movie',
            includeItemTypes: [],
            mediaTypes: [],
            sortBy: []
        });

        expect(params.toString()).toBe('searchTerm=movie');
    });
});
