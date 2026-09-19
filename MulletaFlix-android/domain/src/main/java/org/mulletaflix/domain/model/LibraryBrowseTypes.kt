package org.mulletaflix.domain.model

/**
 * Item-type whitelist used when browsing the inside of a media library.
 *
 * A library query is recursive by default, so asking the server for "everything
 * under this library" returns Series **and** their Seasons **and** their
 * Episodes (and Albums **and** their Audio tracks) as flat siblings. Browsing a
 * library must therefore declare which item types belong at the browsing level,
 * exactly like the reference MulletaFlix Web client does
 * (`src/controllers/list.ts`: `CollectionType.Movies -> 'Movie'`,
 * `CollectionType.Tvshows -> 'Series'`).
 *
 * Rules:
 *  - Seasons and Episodes are never browse-level items: they belong inside the
 *    series detail screen (season tabs + episode list).
 *  - Audio tracks are never browse-level items: they belong inside the album.
 */
object LibraryBrowseTypes {

    /** Item types a library root can contain directly when its type is unknown. */
    const val DEFAULT = "Movie,Series,Video,MusicVideo,MusicAlbum,MusicArtist," +
        "BoxSet,Book,Audiobook,Playlist,Photo,PhotoAlbum,LiveTvChannel,Folder"

    /**
     * Maps the server's `CollectionType` (as exposed by `Users/{userId}/Views`)
     * to the `IncludeItemTypes` value used when listing the library contents.
     */
    fun forCollectionType(collectionType: String?): String = when (collectionType?.trim()?.lowercase()) {
        null, "" -> DEFAULT
        "movies" -> "Movie"
        "tvshows" -> "Series"
        "music" -> "MusicAlbum"
        "musicvideos" -> "MusicVideo"
        "books" -> "Book,Audiobook"
        "boxsets" -> "BoxSet"
        "homevideos" -> "Video"
        "photos" -> "Photo,PhotoAlbum"
        "playlists" -> "Playlist"
        "livetv" -> "LiveTvChannel"
        else -> DEFAULT
    }
}
