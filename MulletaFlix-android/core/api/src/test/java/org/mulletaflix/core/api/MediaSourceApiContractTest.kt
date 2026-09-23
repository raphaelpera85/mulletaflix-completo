package org.mulletaflix.core.api

import com.squareup.moshi.Moshi
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Test
import org.mulletaflix.core.api.dto.MediaSourceDto

/** Guards the casing of stream-selection fields returned by the server. */
class MediaSourceApiContractTest {

    private val moshi = Moshi.Builder().build()

    @Test
    fun `server selected stream indices survive JSON deserialization`() {
        val json = """
            {
              "Id": "source-42",
              "DefaultAudioStreamIndex": 7,
              "DefaultSubtitleStreamIndex": -1
            }
        """.trimIndent()

        val source = moshi.adapter(MediaSourceDto::class.java).fromJson(json)

        assertNotNull(source)
        assertEquals(7, source!!.defaultAudioStreamIndex)
        assertEquals(-1, source.defaultSubtitleStreamIndex)
    }

    @Test
    fun `older payloads without selected indices remain compatible`() {
        val source = moshi.adapter(MediaSourceDto::class.java)
            .fromJson("""{"Id":"legacy-source","Container":"mkv"}""")

        assertNotNull(source)
        assertEquals(null, source!!.defaultAudioStreamIndex)
        assertEquals(null, source.defaultSubtitleStreamIndex)
    }
}
