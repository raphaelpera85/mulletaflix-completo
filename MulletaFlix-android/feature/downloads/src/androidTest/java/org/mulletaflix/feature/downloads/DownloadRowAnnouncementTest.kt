package org.mulletaflix.feature.downloads

import android.graphics.Bitmap
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithContentDescription
import androidx.compose.ui.test.onAllNodesWithText
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState
import java.io.File

/**
 * O título de um download é anunciado **uma vez**.
 *
 * A arte carregava `contentDescription = entry.title` e o título também é um `Text`
 * ao lado. No celular o leitor de tela parava duas vezes no mesmo título; na TV a
 * linha mescla os descendentes e a frase saía "Reproduzir X offline, X".
 *
 * A imagem é um PNG gerado no cache do próprio app: uma URL de rede faria o Coil
 * tentar resolver o host, e a tentativa de DNS derrubava o processo de
 * instrumentação (medido: `android_getaddrinfo failed: EPERM`).
 */
@RunWith(AndroidJUnit4::class)
class DownloadRowAnnouncementTest {

    @get:Rule
    val composeRule = createComposeRule()

    private val entry = DownloadEntry(
        id = "d1",
        title = "À Beira da Extinção",
        uri = "file:///downloads/d1.mp4",
        state = DownloadState.Completed,
        percent = 100,
        imageUrl = "/Items/d1/Images/Primary",
    )

    private fun localArtwork(): String {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val file = File(context.cacheDir, "download-row-art.png")
        if (!file.exists()) {
            java.io.FileOutputStream(file).use { out ->
                Bitmap.createBitmap(2, 2, Bitmap.Config.ARGB_8888).compress(Bitmap.CompressFormat.PNG, 100, out)
            }
        }
        return file.toURI().toString()
    }

    private fun showRow() {
        val imageModel = localArtwork()
        composeRule.setContent {
            MaterialTheme {
                DownloadRow(
                    entry = entry,
                    imageModel = imageModel,
                    onPlay = {},
                    onRetry = {},
                    onRemove = {},
                )
            }
        }
        composeRule.waitForIdle()
    }

    @Test
    fun theTitleIsTheOnlyDescriptionOfItself() {
        showRow()

        // A arte não pode se anunciar: o título já é texto visível na linha.
        composeRule
            .onAllNodesWithContentDescription(entry.title)
            .assertCountEquals(0)
        composeRule
            .onAllNodesWithText(entry.title)
            .assertCountEquals(1)
    }
}
