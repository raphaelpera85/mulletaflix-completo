package org.mulletaflix.feature.itemdetail

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.width
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.DeviceConfigurationOverride
import androidx.compose.ui.test.FontScale
import androidx.compose.ui.test.WindowSize
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsNotDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTouchInput
import androidx.compose.ui.test.swipeLeft
import androidx.compose.ui.test.swipeUp
import androidx.compose.ui.unit.DpSize
import androidx.compose.ui.unit.dp
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.model.MediaSource
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
        allowDownload: Boolean = true,
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
                    primaryAction = DetailPlaybackTarget.PlayVideo("m1"),
                    isDownloadPreparing = isDownloadPreparing,
                    isFavoriteUpdating = isFavoriteUpdating,
                    isWatchedUpdating = isWatchedUpdating,
                    onPrimaryAction = {},
                    onFavorite = {},
                    onMarkWatched = {},
                    onDownload = {},
                    allowDownload = allowDownload,
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
    fun downloadActionIsHiddenWhenDownloadsAreUnavailable() {
        show(allowDownload = false)

        composeRule.onNodeWithContentDescription("Baixar para assistir offline").assertDoesNotExist()
    }

    @Test
    fun bookUsesReaderActionInsteadOfVideoPlayback() {
        var clicks = 0
        composeRule.setContent {
            MaterialTheme {
                DetailActionRow(
                    item = MediaItem(
                        id = "book-1",
                        name = "Livro",
                        type = MediaItemType.Book,
                        mediaSources = listOf(MediaSource(id = "book-source", container = "epub")),
                    ),
                    primaryAction = DetailPlaybackTarget.ReadBook("book-1"),
                    isDownloadPreparing = false,
                    isFavoriteUpdating = false,
                    isWatchedUpdating = false,
                    onPrimaryAction = { clicks++ },
                    onFavorite = {},
                    onMarkWatched = {},
                    onDownload = {},
                    onPlaylist = {},
                    onShare = {},
                )
            }
        }

        composeRule.onNodeWithText("Ler livro").assertExists().performClick()
        composeRule.onNodeWithText("Reproduzir").assertDoesNotExist()
        composeRule.runOnIdle { org.junit.Assert.assertEquals(1, clicks) }
    }

    @Test
    fun televisionUnavailableBookActionIsNotRendered() {
        composeRule.setContent {
            MaterialTheme {
                DetailActionRow(
                    item = MediaItem(
                        id = "book-1",
                        name = "Livro",
                        type = MediaItemType.Book,
                        mediaSources = listOf(MediaSource(id = "book-source", container = "epub")),
                    ),
                    primaryAction = DetailPlaybackTarget.Unavailable,
                    isDownloadPreparing = false,
                    isFavoriteUpdating = false,
                    isWatchedUpdating = false,
                    onPrimaryAction = {},
                    onFavorite = {},
                    onMarkWatched = {},
                    onDownload = {},
                    onPlaylist = {},
                    onShare = {},
                )
            }
        }

        composeRule.onNodeWithText("Ler livro").assertDoesNotExist()
        composeRule.onNodeWithText("Reproduzir").assertDoesNotExist()
    }

    @Test
    fun narrowHeroKeepsPosterWholeAndActionsReachableAtTwoHundredPercentFontScale() {
        val longTitle = "Título longo para acessibilidade ampliada ".repeat(10)
        var shareClicks = 0
        composeRule.setContent {
            DeviceConfigurationOverride(DeviceConfigurationOverride.FontScale(2f)) {
                MaterialTheme {
                    Box(Modifier.width(340.dp).height(420.dp)) {
                        DetailHero(
                                item = MediaItem(id = "m1", name = longTitle, type = MediaItemType.Movie),
                                onBack = {},
                                onRefresh = {},
                                isRefreshing = false,
                                primaryAction = DetailPlaybackTarget.PlayVideo("m1"),
                                onPrimaryAction = {},
                                onFavorite = {},
                                onMarkWatched = {},
                                onDownload = {},
                                allowDownload = true,
                                isDownloadPreparing = false,
                                isFavoriteUpdating = false,
                                isWatchedUpdating = false,
                                onPlaylist = {},
                                onShare = { shareClicks++ },
                                isLoading = false,
                        )
                    }
                }
            }
        }

        val expectedPosterWidth = with(composeRule.density) { 88.dp.toPx() }
        val posterBounds = composeRule.onNodeWithTag("item-detail-hero-poster")
            .fetchSemanticsNode().boundsInRoot
        val actualPosterWidth = posterBounds.width
        assert(kotlin.math.abs(actualPosterWidth - expectedPosterWidth) < 1f)
        assert(kotlin.math.abs(posterBounds.height / posterBounds.width - 1.5f) < 0.01f)
        composeRule.onNodeWithContentDescription("Compartilhar título").assertIsNotDisplayed()
        composeRule.onNodeWithTag("item-detail-hero-scroll-pane").performTouchInput {
            repeat(3) { swipeUp() }
        }
        composeRule.onNodeWithTag("item-detail-actions-scroll-row").performTouchInput { swipeLeft() }
        composeRule.onNodeWithContentDescription("Compartilhar título").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Compartilhar título").performClick()
        composeRule.runOnIdle { assert(shareClicks == 1) }
    }

    @Test
    fun aBusyActionRefusesTheSecondRequest() {
        var clicks = 0
        composeRule.setContent {
            MaterialTheme {
                DetailActionRow(
                    item = MediaItem(id = "m1", name = "Filme", type = MediaItemType.Movie),
                    primaryAction = DetailPlaybackTarget.PlayVideo("m1"),
                    isDownloadPreparing = false,
                    isFavoriteUpdating = true,
                    isWatchedUpdating = false,
                    onPrimaryAction = {},
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
