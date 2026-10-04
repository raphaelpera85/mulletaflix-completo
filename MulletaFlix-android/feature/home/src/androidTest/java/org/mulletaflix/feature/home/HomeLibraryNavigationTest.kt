package org.mulletaflix.feature.home

import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performClick
import androidx.test.platform.app.InstrumentationRegistry
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicReference
import kotlin.math.roundToInt
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.designsystem.components.isTelevisionDevice
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

/** Exercises Home's real library tiles and activation path on phone, tablet and TV AVDs. */
class HomeLibraryNavigationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun activating_regular_library_opens_its_library_id() {
        val openedLibraryId = AtomicReference<String?>()
        val profile = showLibraryTiles(
            onLibraryClick = { openedLibraryId.set(it.id) },
            onLiveTvClick = {},
        )
        val expectedProfile = requireNotNull(
            InstrumentationRegistry.getArguments().getString("expectedDeviceProfile"),
        ) { "Set expectedDeviceProfile to PHONE, TABLET or TV for this AVD run" }
        assertEquals("Home should render for the requested device profile", expectedProfile, profile.name)

        composeRule.onNodeWithContentDescription("Abrir Filmes")
            .assertIsDisplayed()
            .performClick()

        assertEquals("movies-library", openedLibraryId.get())
    }

    @Test
    fun activating_live_tv_library_uses_live_tv_callback_not_library_callback() {
        val openedLibraryId = AtomicReference<String?>()
        val liveTvCalls = AtomicInteger()
        showLibraryTiles(
            onLibraryClick = { openedLibraryId.set(it.id) },
            onLiveTvClick = { liveTvCalls.incrementAndGet() },
        )
        composeRule.onNodeWithContentDescription("Abrir TV ao vivo")
            .assertIsDisplayed()
            .performClick()

        assertEquals("TV ao vivo must not open the regular library route", null, openedLibraryId.get())
        assertEquals(1, liveTvCalls.get())
    }

    private fun showLibraryTiles(
        onLibraryClick: (MediaItem) -> Unit,
        onLiveTvClick: () -> Unit,
    ): HomeDeviceClass {
        val renderedProfile = AtomicReference<HomeDeviceClass>()
        val libraries = listOf(
            MediaItem("movies-library", "Filmes", MediaItemType.CollectionFolder, collectionType = "movies"),
            MediaItem("live-tv-library", "TV ao vivo", MediaItemType.CollectionFolder, collectionType = "livetv"),
        )
        composeRule.setContent {
            BoxWithConstraints(Modifier.fillMaxSize()) {
                val profile = homeDeviceClass(maxWidth.value.roundToInt(), isTelevisionDevice())
                renderedProfile.set(profile)
                MulletaFlixTheme {
                    LibraryTiles(
                        libraries = homeLibrariesForDevice(libraries, profile == HomeDeviceClass.TV),
                        layoutSpec = homeLayoutSpec(profile),
                        onLibraryClick = { library ->
                            dispatchHomeLibraryClick(library, { onLibraryClick(library) }, onLiveTvClick)
                        },
                    )
                }
            }
        }
        composeRule.waitForIdle()
        return checkNotNull(renderedProfile.get())
    }
}
