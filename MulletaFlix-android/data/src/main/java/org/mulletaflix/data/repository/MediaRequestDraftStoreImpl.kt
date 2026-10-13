package org.mulletaflix.data.repository

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.first
import org.mulletaflix.domain.repository.MediaRequestDraft
import org.mulletaflix.domain.repository.MediaRequestDraftStore
import javax.inject.Inject
import javax.inject.Singleton

internal val Context.mediaRequestDraftDataStore by preferencesDataStore(name = "mulletaflix_media_request_drafts")

@Singleton
class MediaRequestDraftStoreImpl internal constructor(
    private val store: DataStore<Preferences>,
) : MediaRequestDraftStore {
    @Inject
    constructor(@ApplicationContext context: Context) : this(context.mediaRequestDraftDataStore)

    override suspend fun load(scopeHash: String): MediaRequestDraft? {
        requireValidScope(scopeHash)
        val preferences = store.data.first()
        if (scopeHash !in scopes(preferences[scopesKey])) return null
        return MediaRequestDraft(
            title = preferences[key(scopeHash, "title")].orEmpty(),
            mediaType = preferences[key(scopeHash, "type")] ?: "Série",
            year = preferences[key(scopeHash, "year")].orEmpty(),
            notes = preferences[key(scopeHash, "notes")].orEmpty(),
        )
    }

    override suspend fun save(scopeHash: String, draft: MediaRequestDraft) {
        requireValidScope(scopeHash)
        require(draft.title.length <= MAX_TITLE_LENGTH)
        require(draft.mediaType.length <= MAX_TYPE_LENGTH)
        require(draft.year.length <= MAX_YEAR_LENGTH && draft.year.all(Char::isDigit))
        require(draft.notes.length <= MAX_NOTES_LENGTH)

        store.edit { preferences ->
            val updatedScopes = (scopes(preferences[scopesKey]) - scopeHash + scopeHash)
                .takeLast(MAX_SCOPES)
            val evicted = scopes(preferences[scopesKey]) - updatedScopes.toSet()
            evicted.forEach { oldScope -> fields.forEach { preferences.remove(key(oldScope, it)) } }
            preferences[key(scopeHash, "title")] = draft.title
            preferences[key(scopeHash, "type")] = draft.mediaType
            preferences[key(scopeHash, "year")] = draft.year
            preferences[key(scopeHash, "notes")] = draft.notes
            preferences[scopesKey] = updatedScopes.joinToString("\n")
        }
    }

    override suspend fun clear(scopeHash: String) {
        requireValidScope(scopeHash)
        store.edit { preferences ->
            fields.forEach { preferences.remove(key(scopeHash, it)) }
            preferences[scopesKey] = scopes(preferences[scopesKey])
                .filterNot { it == scopeHash }
                .joinToString("\n")
        }
    }

    private fun scopes(encoded: String?): List<String> = encoded.orEmpty()
        .lineSequence()
        .filter(String::isNotBlank)
        .toList()

    private fun key(scopeHash: String, field: String) = stringPreferencesKey("draft_${scopeHash}_$field")

    private fun requireValidScope(scopeHash: String) {
        require(scopeHash.matches(SCOPE_HASH)) { "Escopo do rascunho inválido." }
    }

    private companion object {
        val scopesKey = stringPreferencesKey("draft_scopes_v1")
        val SCOPE_HASH = Regex("[a-f0-9]{64}")
        val fields = listOf("title", "type", "year", "notes")
        const val MAX_SCOPES = 8
        const val MAX_TITLE_LENGTH = 200
        const val MAX_TYPE_LENGTH = 40
        const val MAX_YEAR_LENGTH = 4
        const val MAX_NOTES_LENGTH = 1000
    }
}
