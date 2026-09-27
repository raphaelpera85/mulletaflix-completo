package org.mulletaflix.feature.player

import androidx.media3.common.C
import androidx.media3.common.MediaItem
import androidx.media3.common.MediaMetadata
import androidx.media3.common.MimeTypes
import androidx.media3.common.MediaItem.LiveConfiguration
import androidx.media3.cast.DefaultMediaItemConverter
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.google.android.gms.cast.MediaTrack
import com.google.android.gms.cast.MediaInfo
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
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

    @Test
    fun castConverterAddsOnlySupportedSidecarsAndPreservesTheDefaultMediaPayload() {
        val videoUrl = "https://media.example/movie.mp4"
        val vttUrl = "https://media.example/subtitles/14.vtt"
        val ttmlUrl = "https://media.example/subtitles/15.ttml"
        val externalSubtitles = mapOf(
            14 to buildExternalSubtitleConfiguration(14, vttUrl, MimeTypes.TEXT_VTT, "pt-BR", "Português", true, false),
            15 to buildExternalSubtitleConfiguration(15, ttmlUrl, MimeTypes.APPLICATION_TTML, "en", "English", false, true),
            16 to buildExternalSubtitleConfiguration(
                16,
                "https://media.example/subtitles/16.srt",
                MimeTypes.APPLICATION_SUBRIP,
                "es",
                "Español",
                false,
                false,
            ),
        )
        val item = MediaItem.Builder()
            .setUri(videoUrl)
            .setMediaId("movie-42")
            .setMimeType(MimeTypes.VIDEO_MP4)
            .setMediaMetadata(MediaMetadata.Builder().setTitle("Filme de teste").build())
            .setLiveConfiguration(LiveConfiguration.Builder().setTargetOffsetMs(30_000L).build())
            .setSubtitleConfigurations(externalSubtitles.values.toList())
            .build()

        val expectedInfo = checkNotNull(DefaultMediaItemConverter().toMediaQueueItem(item).media)
        val info = checkNotNull(ExternalCastSubtitleMediaItemConverter().toMediaQueueItem(item).media)
        val metadata = checkNotNull(info.metadata)
        val customData = checkNotNull(info.customData)
        val tracks = checkNotNull(info.mediaTracks)

        assertEquals(expectedInfo.contentId, info.contentId)
        assertEquals("movie-42", info.contentId)
        assertEquals(expectedInfo.contentUrl, info.contentUrl)
        assertEquals(videoUrl, info.contentUrl)
        assertEquals(expectedInfo.contentType, info.contentType)
        assertEquals(MimeTypes.VIDEO_MP4, info.contentType)
        assertEquals(expectedInfo.streamType, info.streamType)
        assertEquals(MediaInfo.STREAM_TYPE_LIVE, info.streamType)
        assertEquals("Filme de teste", metadata.getString(com.google.android.gms.cast.MediaMetadata.KEY_TITLE))
        assertEquals(expectedInfo.customData.toString(), customData.toString())
        assertEquals(videoUrl, customData.getJSONObject("mediaItem").getString("uri"))
        assertEquals(2, tracks.size)
        assertEquals(vttUrl, tracks[0].contentId)
        assertEquals("text/vtt", tracks[0].contentType)
        assertEquals("pt-BR", tracks[0].language)
        assertEquals("Português", tracks[0].name)
        assertEquals(MediaTrack.SUBTYPE_SUBTITLES, tracks[0].subtype)
        assertEquals(castSubtitleTrackId(14), tracks[0].id)
        val serverIndicesByCastContentId = castSubtitleServerIndicesByUrl(externalSubtitles)
        assertEquals(mapOf(vttUrl to 14, ttmlUrl to 15), serverIndicesByCastContentId)
        assertEquals(14, externalSubtitleServerIndex(tracks[0].contentId, serverIndicesByCastContentId))
        assertEquals(ttmlUrl, tracks[1].contentId)
        assertEquals("application/ttml+xml", tracks[1].contentType)
        assertEquals("en", tracks[1].language)
        assertEquals(15, externalSubtitleServerIndex(tracks[1].contentId, serverIndicesByCastContentId))
        assertEquals(listOf(MediaTrack.ROLE_FORCED_SUBTITLE), tracks[1].roles)
        assertNull(externalSubtitleServerIndex("https://media.example/subtitles/16.srt", serverIndicesByCastContentId))
    }

    @Test
    fun castConverterOmitsSidecarsWithDuplicateUrlsBecauseTheirIdentityIsAmbiguous() {
        val duplicatedUrl = "https://media.example/subtitles/shared.vtt"
        val uniqueUrl = "https://media.example/subtitles/15.ttml"
        val externalSubtitles = mapOf(
            14 to buildExternalSubtitleConfiguration(14, duplicatedUrl, MimeTypes.TEXT_VTT, "pt-BR", "Português", false, false),
            15 to buildExternalSubtitleConfiguration(15, uniqueUrl, MimeTypes.APPLICATION_TTML, "en", "English", false, false),
            16 to buildExternalSubtitleConfiguration(16, duplicatedUrl, MimeTypes.TEXT_VTT, "es", "Español", false, false),
        )
        val item = MediaItem.Builder()
            .setUri("https://media.example/movie.mp4")
            .setSubtitleConfigurations(externalSubtitles.values.toList())
            .build()

        val indicesByUrl = castSubtitleServerIndicesByUrl(externalSubtitles)
        val castTracks = checkNotNull(
            ExternalCastSubtitleMediaItemConverter().toMediaQueueItem(item).media?.mediaTracks,
        )

        assertEquals(3, item.localConfiguration?.subtitleConfigurations?.size)
        assertEquals(mapOf(uniqueUrl to 15), indicesByUrl)
        assertEquals(1, castTracks.size)
        assertEquals(uniqueUrl, castTracks.single().contentId)
        assertNull(externalSubtitleServerIndex(duplicatedUrl, indicesByUrl))
    }
}
