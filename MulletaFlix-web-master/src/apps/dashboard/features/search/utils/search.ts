import { BaseItemKind, CollectionType } from '@jellyfin/sdk/lib/generated-client';

export const getItemTypesFromCollectionType = (collectionType?: CollectionType): BaseItemKind[] => {
    switch (collectionType) {
        case CollectionType.Movies:
            return [BaseItemKind.Movie];
        case CollectionType.Tvshows:
            return [BaseItemKind.Series];
        case CollectionType.Music:
            return [BaseItemKind.MusicArtist, BaseItemKind.MusicAlbum, BaseItemKind.Audio];
        case CollectionType.Boxsets:
            return [BaseItemKind.BoxSet];
        case CollectionType.Livetv:
            return [BaseItemKind.Channel, BaseItemKind.LiveTvProgram];
        case CollectionType.Homevideos:
            return [BaseItemKind.Movie];
        default:
            return [];
    }
};