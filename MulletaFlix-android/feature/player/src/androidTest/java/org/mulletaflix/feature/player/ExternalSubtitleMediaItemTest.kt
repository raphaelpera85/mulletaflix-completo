package org.mulletaflix.feature.player

import androidx.media3.common.C
import androidx.media3.common.MediaItem
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class ExternalSubtitleMediaItemTest {
    @Test
    fun selectedSidecarSubtitleCarriesServerIdentityAndMetadata() {
        val subtitle = buildExternalSubtitleConfiguration(
            serverIndex = 14,
            subtitleUrl = "http://127.0.0.1:8096/subtitles/14.srt?api_key=test-token",
            mimeType = "application/x-subrip",
            language = "pt-BR",
            label = "Português (Brasil)",
            isDefault = true,
            isForced = true,
        )
        val item = MediaItem.Builder()
            .setUri("http://127.0.0.1:8096/video.mp4")
            .setSubtitleConfigurations(listOf(subtitle))
            .build()

        assertEquals(1, item.localConfiguration?.subtitleConfigurations?.size)
        assertEquals("mullet-external:14", item.localConfiguration?.subtitleConfigurations?.single()?.id)
        assertEquals("application/x-subrip", item.localConfiguration?.subtitleConfigurations?.single()?.mimeType)
        assertEquals("pt-BR", item.localConfiguration?.subtitleConfigurations?.single()?.language)
        assertEquals("Português (Brasil)", item.localConfiguration?.subtitleConfigurations?.single()?.label)
        assertEquals(
            C.SELECTION_FLAG_DEFAULT or C.SELECTION_FLAG_FORCED,
            item.localConfiguration?.subtitleConfigurations?.single()?.selectionFlags,
        )
    }

    @Test
    fun clearingSelectedSidecarRemovesAllExternalSubtitleConfigurations() {
        val original = MediaItem.Builder()
            .setUri("http://127.0.0.1:8096/video.mp4")
            .setSubtitleConfigurations(
                listOf(
                    buildExternalSubtitleConfiguration(
                        serverIndex = 3,
                        subtitleUrl = "http://127.0.0.1:8096/subtitles/3.vtt",
                        mimeType = "text/vtt",
                        language = "en",
                        label = "English",
                        isDefault = false,
                        isForced = false,
                    ),
                ),
            )
            .build()

        val cleared = original.buildUpon().setSubtitleConfigurations(emptyList()).build()

        assertEquals(0, cleared.localConfiguration?.subtitleConfigurations?.size)
    }
}
