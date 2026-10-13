package org.mulletaflix.domain.repository

/** Local-only editable fields for a media request; submission state is deliberately excluded. */
data class MediaRequestDraft(
    val title: String = "",
    val mediaType: String = "Série",
    val year: String = "",
    val notes: String = "",
)

/** Stores drafts under a one-way account/server scope. Implementations must never store credentials. */
interface MediaRequestDraftStore {
    suspend fun load(scopeHash: String): MediaRequestDraft?
    suspend fun save(scopeHash: String, draft: MediaRequestDraft)
    suspend fun clear(scopeHash: String)
}
