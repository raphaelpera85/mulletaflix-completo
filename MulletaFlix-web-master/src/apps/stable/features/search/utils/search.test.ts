import { describe, expect, it } from 'vitest';
import { BaseItemKind } from '@jellyfin/sdk/lib/generated-client/models/base-item-kind';
import { CollectionType } from '@jellyfin/sdk/lib/generated-client/models/collection-type';

import { getItemTypesFromCollectionType, getSearchScopeLabel, buildSearchScopeHref, buildSearchPageTitle, buildSearchGlobalHref } from './search';

describe('search scope helpers', () => {
    it('labels well-known scopes', () => {
        expect(getSearchScopeLabel(undefined, undefined)).toBeUndefined();
        expect(getSearchScopeLabel('library-id', undefined)).toBe('this library');
        expect(getSearchScopeLabel(undefined, CollectionType.Movies)).toBe('Movies');
        expect(getSearchScopeLabel(undefined, CollectionType.Tvshows)).toBe('TV Shows');
        expect(getSearchScopeLabel(undefined, CollectionType.Music)).toBe('Music');
        expect(getSearchScopeLabel(undefined, CollectionType.Livetv)).toBe('Live TV');
    });

    it('maps collection types to item kinds', () => {
        expect(getItemTypesFromCollectionType(CollectionType.Movies)).toEqual([BaseItemKind.Movie]);
        expect(getItemTypesFromCollectionType(CollectionType.Tvshows)).toEqual([
            BaseItemKind.Series,
            BaseItemKind.Episode
        ]);
        expect(getItemTypesFromCollectionType(CollectionType.Music)).toEqual([
            BaseItemKind.Playlist,
            BaseItemKind.MusicAlbum,
            BaseItemKind.Audio
        ]);
    });

    it('builds a clean global-search href when scope filters are present', () => {
        expect(buildSearchScopeHref(new URLSearchParams('query=batman&parentId=abc&collectionType=Movies'))).toBe('/search?query=batman');
        expect(buildSearchScopeHref(new URLSearchParams('parentId=abc&collectionType=Movies'))).toBe('/search');
    });

    it('builds a scoped search title', () => {
        expect(buildSearchPageTitle('Movies')).toBe('Search - Movies');
        expect(buildSearchPageTitle()).toBe('Search');
    });

    it('builds a global search href', () => {
        expect(buildSearchGlobalHref('batman')).toBe('/search?query=batman');
        expect(buildSearchGlobalHref('batman & robin')).toBe('/search?query=batman%20%26%20robin');
        expect(buildSearchGlobalHref()).toBe('/search');
    });
});
