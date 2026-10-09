package org.mulletaflix.data.repository

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.repository.SearchHistoryScope

class SearchHistoryScopeTest {
    @Test
    fun `same server id shares history across lan and public urls`() {
        val lan = SearchHistoryScope("server-1", "http://192.168.1.10:8096", "Raphael")
        val public = SearchHistoryScope("SERVER-1", "https://mulletaflix.example", "Raphael")

        assertEquals(lan.identity, public.identity)
        assertEquals(searchHistoryPreferenceKey(lan), searchHistoryPreferenceKey(public))
    }

    @Test
    fun `url fallback normalizes scheme host default port and trailing slash`() {
        val first = SearchHistoryScope(null, "HTTPS://Media.Example:443/library/", "Raphael")
        val second = SearchHistoryScope(null, "https://media.example/library", "Raphael")

        assertEquals(first.identity, second.identity)
        assertEquals(searchHistoryPreferenceKey(first), searchHistoryPreferenceKey(second))
    }

    @Test
    fun `url fallback keeps query when it distinguishes server endpoints`() {
        val first = SearchHistoryScope(null, "https://media.example/api?tenant=one", "Raphael")
        val second = SearchHistoryScope(null, "https://media.example/api?tenant=two", "Raphael")

        assertNotEquals(first.identity, second.identity)
        assertNotEquals(searchHistoryPreferenceKey(first), searchHistoryPreferenceKey(second))
    }

    @Test
    fun `history key is isolated by server and user without exposing identities`() {
        val base = SearchHistoryScope("server-1", "https://one.example", "Raphael")
        val otherServer = base.copy(serverId = "server-2")
        val otherUser = base.copy(userId = "another-user")

        assertNotEquals(searchHistoryPreferenceKey(base), searchHistoryPreferenceKey(otherServer))
        assertNotEquals(searchHistoryPreferenceKey(base), searchHistoryPreferenceKey(otherUser))
        assertTrue(searchHistoryPreferenceKey(base).length < 160)
        assertTrue(!searchHistoryPreferenceKey(base).contains("Raphael"))
    }

    @Test
    fun `clearing a user prefix matches only that user's server-scoped keys`() {
        val userPrefix = searchHistoryPreferencePrefix("Raphael")
        val sameUser = searchHistoryPreferenceKey(SearchHistoryScope("server-2", "https://two.example", "Raphael"))
        val otherUser = searchHistoryPreferenceKey(SearchHistoryScope("server-2", "https://two.example", "Other"))

        assertTrue(sameUser.startsWith(userPrefix))
        assertTrue(!otherUser.startsWith(userPrefix))
    }

    @Test
    fun `clear selection removes all server keys and legacy key only for requested user`() {
        val userAServerOne = searchHistoryPreferenceKey(SearchHistoryScope("server-1", "https://one.example", "A"))
        val userAServerTwo = searchHistoryPreferenceKey(SearchHistoryScope("server-2", "https://two.example", "A"))
        val userB = searchHistoryPreferenceKey(SearchHistoryScope("server-1", "https://one.example", "B"))
        val allKeys = setOf(userAServerOne, userAServerTwo, userB, "history:A", "history:B", "unrelated")

        assertEquals(setOf(userAServerOne, userAServerTwo, "history:A"), searchHistoryKeysToClear(allKeys, "A"))
    }
}
