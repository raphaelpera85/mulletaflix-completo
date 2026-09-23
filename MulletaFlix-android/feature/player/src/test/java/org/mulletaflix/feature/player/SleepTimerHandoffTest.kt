package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.Chapter

/**
 * O timer de sono pertence à sessão, não ao item.
 *
 * Todo este arquivo existe por causa de uma promessa de produto: "pausar em 30
 * minutos" precisa valer quando o usuário volta. O caminho que a quebrava era o
 * mais provável de todos — o avanço automático para o episódio seguinte, que
 * chama `loadMedia` sem ninguém tocar na tela. Na versão anterior, `loadMedia`
 * cancelava o job e zerava os três campos do timer, então o timer sumia no
 * primeiro episódio seguinte e nunca disparava.
 */
class SleepTimerHandoffTest {

    private val armed = PlayerState(
        sleepTimerMode = SleepTimerMode.COUNTDOWN,
        sleepTimerMinutes = 30,
        sleepTimerRemainingMs = 1_500_000L,
    )

    @Test
    fun `an armed countdown survives a change of item`() {
        val after = armed.forNewItem(
            aspectRatio = VideoAspectRatio.FIT,
            subtitleColor = "#FFFFFF",
        )

        assertEquals(SleepTimerMode.COUNTDOWN, after.sleepTimerMode)
        assertEquals(30, after.sleepTimerMinutes)
        assertEquals(1_500_000L, after.sleepTimerRemainingMs)
    }

    @Test
    fun `the timer armed for the end of the media also survives`() {
        val after = PlayerState(sleepTimerMode = SleepTimerMode.AT_MEDIA_END).forNewItem(
            aspectRatio = VideoAspectRatio.FIT,
            subtitleColor = "#FFFFFF",
        )

        assertEquals(SleepTimerMode.AT_MEDIA_END, after.sleepTimerMode)
    }

    @Test
    fun `a disabled timer stays disabled across the change`() {
        val after = PlayerState().forNewItem(
            aspectRatio = VideoAspectRatio.FIT,
            subtitleColor = "#FFFFFF",
        )

        assertEquals(SleepTimerMode.OFF, after.sleepTimerMode)
        assertNull(after.sleepTimerRemainingMs)
    }

    @Test
    fun `the new item still forgets everything that belonged to the previous one`() {
        // O outro lado do contrato: se `forNewItem` parasse de zerar isto, o
        // capítulo do episódio anterior apareceria sobre o novo e o botão
        // "Pular Créditos" buscaria a posição do item antigo.
        val previous = armed.copy(
            title = "Episódio anterior",
            currentPosition = 900_000L,
            duration = 1_800_000L,
            currentChapterName = "Capítulo 3",
            chapters = listOf(Chapter(startPositionTicks = 900_000L, name = "Capítulo 3")),
            showSkipCredits = true,
            skipTargetPosition = 1_700_000L,
            nextEpisodeCountdown = 3,
            nextEpisodePromptDismissed = true,
            error = "falha anterior",
        )

        val after = previous.forNewItem(
            aspectRatio = VideoAspectRatio.FILL,
            subtitleColor = "#000000",
        )

        assertNull(after.title)
        assertEquals(0L, after.currentPosition)
        assertEquals(0L, after.duration)
        assertNull(after.currentChapterName)
        assertTrue(after.chapters.isEmpty())
        assertTrue(!after.showSkipCredits)
        assertNull(after.skipTargetPosition)
        assertNull(after.nextEpisodeCountdown)
        assertTrue(!after.nextEpisodePromptDismissed)
        assertNull(after.error)
        assertEquals(VideoAspectRatio.FILL, after.aspectRatio)
        assertEquals("#000000", after.subtitleColor)
    }

    @Test
    fun `the settings the session is honouring outlive the change`() {
        // Estes já foram perdidos uma vez, quando o estado era reconstruído do
        // zero: o item novo entrava em Picture-in-Picture com a opção desligada.
        val previous = armed.copy(
            subtitleFontSize = 140,
            pictureInPictureEnabled = false,
            isNetworkMetered = true,
            playbackSpeed = 1.5f,
        )

        val after = previous.forNewItem(
            aspectRatio = VideoAspectRatio.FIT,
            subtitleColor = "#FFFFFF",
        )

        assertEquals(140, after.subtitleFontSize)
        assertTrue(!after.pictureInPictureEnabled)
        assertTrue(after.isNetworkMetered)
        assertEquals(1.5f, after.playbackSpeed)
    }

    @Test
    fun `the timer expiring cancels an auto advance that was already scheduled`() {
        // Sem isto o avanço automático desfazia a pausa: o job do próximo
        // episódio seguia rodando e chamava `loadMedia` logo depois do
        // `player.pause()`, então a reprodução continuava no episódio seguinte.
        val expiring = PlayerState(
            sleepTimerMode = SleepTimerMode.COUNTDOWN,
            sleepTimerMinutes = 1,
            sleepTimerRemainingMs = 0L,
            nextEpisode = NextEpisodeInfo(
                id = "ep-2",
                title = "Episódio 2",
                episodeNumber = 2,
                seasonNumber = 1,
            ),
            nextEpisodeCountdown = 3,
        )

        val after = expiring.afterSleepTimerExpiry()

        assertNull(after.nextEpisodeCountdown)
        assertEquals(SleepTimerMode.OFF, after.sleepTimerMode)
        assertNull(after.sleepTimerMinutes)
        assertNull(after.sleepTimerRemainingMs)
        assertTrue(
            "o aviso de próximo episódio precisa sair junto: a reprodução ficou parada",
            after.nextEpisodePromptDismissed,
        )
    }

    @Test
    fun `the offline handoff carries the timer into the new state`() {
        // `loadOffline` troca o estado inteiro em vez de copiá-lo, então os três
        // campos precisam ser passados à mão; o padrão (Off) é o que os perdia.
        val carried = armed.sleepTimerSelection()

        val after = offlinePlaybackState(
            title = "Filme baixado",
            isBuffering = true,
            error = null,
            aspectRatio = VideoAspectRatio.FIT,
            subtitleColor = "#FFFFFF",
            subtitleFontSize = 100,
            pictureInPictureEnabled = true,
            isNetworkMetered = false,
            sleepTimer = carried,
        )

        assertEquals(SleepTimerMode.COUNTDOWN, after.sleepTimerMode)
        assertEquals(30, after.sleepTimerMinutes)
        assertEquals(1_500_000L, after.sleepTimerRemainingMs)
    }

    @Test
    fun `the offline handoff without a timer leaves it off`() {
        val after = offlinePlaybackState(
            title = "Filme baixado",
            isBuffering = true,
            error = null,
            aspectRatio = VideoAspectRatio.FIT,
            subtitleColor = "#FFFFFF",
            subtitleFontSize = 100,
            pictureInPictureEnabled = true,
            isNetworkMetered = false,
        )

        assertEquals(SleepTimerMode.OFF, after.sleepTimerMode)
        assertNull(after.sleepTimerRemainingMs)
    }
}
