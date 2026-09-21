package org.mulletaflix.domain.model

/**
 * Canonical library sort fields, shared by the library screen that writes them
 * and the settings screen that displays them.
 *
 * This existed twice before: the library screen owned an enum with labels like
 * `"Data de Adição"` while the settings screen re-derived the code from a label
 * with its own list using `"Data de adição"`. The two never matched, so the
 * settings screen displayed the wrong sort order and, when the user confirmed
 * that wrong value, **overwrote** the real choice. Only four of the eight fields
 * were recognised at all; `Random`, `PlayCount` and `DatePlayed` collapsed to
 * "Nome".
 *
 * Keeping one list means `code` and `label` cannot drift, because they are the
 * same enum.
 */
enum class LibrarySortField(val code: String, val label: String) {
    Name("SortName", "Nome"),
    DateAdded("DateCreated", "Data de Adição"),
    ReleaseDate("PremiereDate", "Data de Lançamento"),
    Runtime("Runtime", "Duração"),
    CommunityRating("CommunityRating", "Avaliação"),
    Random("Random", "Aleatório"),
    PlayCount("PlayCount", "Mais Assistidos"),
    LastPlayed("DatePlayed", "Assistido Recentemente");

    companion object {
        /** The field used when nothing was stored or the stored value is unknown. */
        val Default: LibrarySortField = Name

        /** Resolves a stored server code, falling back to [Default]. */
        fun fromCode(code: String?): LibrarySortField =
            entries.firstOrNull { it.code.equals(code?.trim(), ignoreCase = true) } ?: Default

        /** Resolves a label shown in the UI, falling back to [Default]. */
        fun fromLabel(label: String?): LibrarySortField =
            entries.firstOrNull { it.label.equals(label?.trim(), ignoreCase = true) } ?: Default
    }
}
