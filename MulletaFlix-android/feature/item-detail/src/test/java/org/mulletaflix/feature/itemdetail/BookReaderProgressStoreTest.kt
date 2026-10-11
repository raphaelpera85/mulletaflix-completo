package org.mulletaflix.feature.itemdetail

import androidx.test.core.app.ApplicationProvider
import android.content.Context
import java.util.UUID
import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.HomeFeedCacheScope
import org.mulletaflix.feature.itemdetail.BookReaderProgressStore.Companion.MAX_BOOKMARKS_JSON_CHARS
import org.mulletaflix.feature.itemdetail.BookReaderProgressStore.Companion.MAX_BOOKMARK_LOCATOR_CHARS
import org.mulletaflix.feature.itemdetail.BookReaderProgressStore.Companion.MAX_READER_LOCATOR_CHARS
import org.readium.r2.shared.publication.Locator
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import kotlinx.coroutines.runBlocking

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class BookReaderProgressStoreTest {
    @Test
    fun `keeps valid persisted bookmarks within the locator size limit`() {
        val decoded = Locator.fromJSON(validLocatorJson())
        assertNotNull(decoded)

        val bookmarks = decode(JSONArray().put(bookmarkRow("valid", validLocatorJson())).toString())

        assertEquals("valid", bookmarks.single().id)
    }

    @Test
    fun `ignores persisted bookmarks whose locator exceeds the write limit`() {
        val oversizedLocator = JSONObject()
            .put("href", "chapter-${"x".repeat(MAX_BOOKMARK_LOCATOR_CHARS)}.xhtml")
            .put("type", "application/xhtml+xml")
            .put("locations", JSONObject())
        val decodedLocator = Locator.fromJSON(oversizedLocator)
        assertNotNull("fixture must be a valid Readium locator", decodedLocator)
        assertTrue(requireNotNull(decodedLocator).toJSON().toString().length > MAX_BOOKMARK_LOCATOR_CHARS)
        val encoded = JSONArray()
            .put(
                JSONObject()
                    .put("id", "oversized")
                    .put("label", "Capítulo")
                    .put("locator", oversizedLocator),
            )
            .toString()
        val bookmarks = decode(encoded)

        assertTrue("oversized persisted locator must be ignored", bookmarks.isEmpty())
    }

    @Test
    fun `rejects persisted bookmark payloads above the aggregate size limit`() {
        val row = bookmarkRow("valid", validLocatorJson()).put("padding", "x".repeat(MAX_BOOKMARKS_JSON_CHARS))

        val bookmarks = decode(JSONArray().put(row).toString())

        assertTrue("oversized bookmark payload must be rejected before JSON parsing", bookmarks.isEmpty())
    }

    @Test
    fun `does not store a progress locator larger than the reader size limit`() = runBlocking {
        val suffix = UUID.randomUUID().toString()
        val scope = HomeFeedCacheScope("progress-$suffix", "https://progress-$suffix.example", "user-$suffix")
        val locator = requireNotNull(Locator.fromJSON(oversizedLocatorJson()))
        assertTrue(locator.toJSON().toString().length > MAX_READER_LOCATOR_CHARS)
        val store = BookReaderProgressStore(ApplicationProvider.getApplicationContext<Context>())

        store.write(scope, "book-$suffix", locator)

        assertNull("oversized reading positions are not restored", store.read(scope, "book-$suffix"))
    }

    @Test
    fun `rejects an oversized progress locator already in storage`() {
        val encoded = oversizedLocatorJson().toString()
        assertTrue(encoded.length > MAX_READER_LOCATOR_CHARS)

        assertNull(decodeBookReaderProgress(encoded))
    }

    private fun decode(encoded: String): List<BookReaderBookmark> {
        val store = BookReaderProgressStore(ApplicationProvider.getApplicationContext<Context>())
        return store.decodeBookmarks(encoded)
    }

    private fun bookmarkRow(id: String, locator: JSONObject) = JSONObject()
        .put("id", id)
        .put("label", "Capítulo")
        .put("locator", locator)

    private fun validLocatorJson() = JSONObject()
        .put("href", "chapter-1.xhtml")
        .put("type", "application/xhtml+xml")
        .put("locations", JSONObject())

    private fun oversizedLocatorJson() = JSONObject()
        .put("href", "chapter-${"x".repeat(MAX_READER_LOCATOR_CHARS)}.xhtml")
        .put("type", "application/xhtml+xml")
        .put("locations", JSONObject())
}
