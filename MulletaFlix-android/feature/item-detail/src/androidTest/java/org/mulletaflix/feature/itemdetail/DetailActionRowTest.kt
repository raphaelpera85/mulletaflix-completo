package org.mulletaflix.feature.itemdetail

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.model.MediaStream
import org.mulletaflix.domain.model.MediaStreamType

/**
 * As ações do cabeçalho de Detalhes precisam continuar nomeadas enquanto trabalham.
 *
 * Enquanto um pedido corria, o ícone era substituído por um
 * `CircularProgressIndicator` **sem descrição**: o nó ficava sem nome nenhum e o
 * leitor de tela anunciava só "botão". O contrato do componente já tinha a
 * resposta — `busy` mostra o spinner e mantém o nome — e é isso que estes testes
 * prendem, com o botão em estado de trabalho.
 */
@RunWith(AndroidJUnit4::class)
class DetailActionRowTest {

    @get:Rule
    val composeRule = createComposeRule()

    private fun show(
        isFavoriteUpdating: Boolean = false,
        isWatchedUpdating: Boolean = false,
        isDownloadPreparing: Boolean = false,
        isFavorite: Boolean = false,
        isPlayed: Boolean = false,
    ) {
        composeRule.setContent {
            MaterialTheme {
                DetailActionRow(
                    item = MediaItem(
                        id = "m1",
                        name = "Filme",
                        type = MediaItemType.Movie,
                        isFavorite = isFavorite,
                        isPlayed = isPlayed,
                    ),
                    playEnabled = true,
                    isDownloadPreparing = isDownloadPreparing,
                    isFavoriteUpdating = isFavoriteUpdating,
                    isWatchedUpdating = isWatchedUpdating,
                    onPlay = {},
                    onFavorite = {},
                    onMarkWatched = {},
                    onDownload = {},
                    onPlaylist = {},
                    onShare = {},
                )
            }
        }
    }

    @Test
    fun favouriteKeepsItsNameWhileUpdating() {
        show(isFavoriteUpdating = true)

        composeRule.onNodeWithContentDescription("Adicionando aos favoritos").assertExists()
    }

    @Test
    fun watchedKeepsItsNameWhileUpdating() {
        show(isWatchedUpdating = true)

        composeRule.onNodeWithContentDescription("Marcando como assistido").assertExists()
    }

    @Test
    fun downloadKeepsItsNameWhilePreparing() {
        show(isDownloadPreparing = true)

        composeRule.onNodeWithContentDescription("Preparando o download").assertExists()
    }

    @Test
    fun theBusyNameSaysWhatIsHappeningToTheItem() {
        // O nome não pode ser genérico: "Adicionando" e "Removendo" são ações
        // opostas sobre o mesmo botão.
        show(isFavoriteUpdating = true, isFavorite = true)
        composeRule.onNodeWithContentDescription("Removendo dos favoritos").assertExists()
        composeRule.onNodeWithContentDescription("Adicionando aos favoritos").assertDoesNotExist()
    }

    @Test
    fun anIdleActionKeepsItsOwnName() {
        show()

        composeRule.onNodeWithContentDescription("Adicionar aos favoritos").assertExists()
        composeRule.onNodeWithContentDescription("Marcar como assistido").assertExists()
        composeRule.onNodeWithContentDescription("Baixar para assistir offline").assertExists()
    }

    @Test
    fun aBusyActionRefusesTheSecondRequest() {
        var clicks = 0
        composeRule.setContent {
            MaterialTheme {
                DetailActionRow(
                    item = MediaItem(id = "m1", name = "Filme", type = MediaItemType.Movie),
                    playEnabled = true,
                    isDownloadPreparing = false,
                    isFavoriteUpdating = true,
                    isWatchedUpdating = false,
                    onPlay = {},
                    onFavorite = { clicks++ },
                    onMarkWatched = {},
                    onDownload = {},
                    onPlaylist = {},
                    onShare = {},
                )
            }
        }

        composeRule.onNodeWithContentDescription("Adicionando aos favoritos").performClick()
        composeRule.waitForIdle()

        // `busy` engole o clique; `enabled` continua verdadeiro de propósito, para
        // o nó não sair da sequência do D-pad.
        org.junit.Assert.assertEquals(0, clicks)
    }

    @Test
    fun theTechnicalSectionSaysWhetherItIsOpen() {
        // O rótulo nunca muda e o chevron é decorativo: sem estado, o leitor de
        // tela anunciava "Informações Técnicas, botão" e nada sobre o conteúdo.
        composeRule.setContent {
            MaterialTheme {
                MediaInfoSection(itemWithOneVideoStream())
            }
        }

        val collapsed = SemanticsMatcher.expectValue(SemanticsProperties.StateDescription, "Recolhido")
        val expanded = SemanticsMatcher.expectValue(SemanticsProperties.StateDescription, "Expandido")

        composeRule.onNode(collapsed).assertExists()
        composeRule.onNodeWithText("Informações Técnicas").performClick()
        composeRule.waitForIdle()
        composeRule.onNode(expanded).assertExists()
    }

    @Test
    fun theTechnicalSectionNamesTheStreamTypesInPortuguese() {
        // `MediaStreamType.name` devolvia "Video"/"Audio" e era lido literalmente.
        composeRule.setContent {
            MaterialTheme {
                MediaInfoSection(itemWithOneVideoStream())
            }
        }

        composeRule.onNodeWithText("Informações Técnicas").performClick()
        composeRule.waitForIdle()

        composeRule.onNodeWithText("Vídeo", substring = true).assertExists()
        composeRule.onNodeWithText("Video", substring = true).assertDoesNotExist()
    }

    private fun itemWithOneVideoStream() = MediaItem(
        id = "m1",
        name = "Filme",
        type = MediaItemType.Movie,
        mediaStreams = listOf(
            MediaStream(
                index = 0,
                type = MediaStreamType.Video,
                codec = "h264",
                displayTitle = "1080p",
            ),
        ),
    )

    @Test
    fun theStreamTypeLabelsAreNotEnumConstants() {
        org.junit.Assert.assertEquals("Vídeo", streamTypeLabel(MediaStreamType.Video))
        org.junit.Assert.assertEquals("Áudio", streamTypeLabel(MediaStreamType.Audio))
        org.junit.Assert.assertEquals("Legenda", streamTypeLabel(MediaStreamType.Subtitle))
    }
}
