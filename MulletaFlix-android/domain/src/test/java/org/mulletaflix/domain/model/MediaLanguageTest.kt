package org.mulletaflix.domain.model

import org.junit.Assert.assertEquals
import org.junit.Test

class MediaLanguageTest {

    @Test
    fun `common server language codes round trip through settings`() {
        mapOf(
            "ita" to "it",
            "jpn" to "ja",
            "nld" to "nl",
            "tur" to "tr",
            "pol" to "pl",
            "hin" to "hi",
        ).forEach { (serverCode, canonical) ->
            assertEquals(canonical, MediaLanguage.canonicalize(serverCode))
            assertEquals(canonical, MediaLanguage.code(MediaLanguage.label(serverCode)))
        }
    }

    @Test
    fun `every selectable language has a stable label and code`() {
        MediaLanguage.selectable.forEach { entry ->
            assertEquals(entry.label, MediaLanguage.label(entry.canonical))
            assertEquals(entry.canonical, MediaLanguage.code(entry.label))
        }
    }
}
