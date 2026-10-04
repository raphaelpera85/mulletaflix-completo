package org.mulletaflix.feature.home

import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.ui.Modifier
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import androidx.test.platform.app.InstrumentationRegistry
import java.util.concurrent.atomic.AtomicReference
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.designsystem.components.isTelevisionDevice
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import kotlin.math.roundToInt
import androidx.compose.foundation.layout.BoxWithConstraints

/** Exercises physical-remote activation on the Android TV profile. */
class HomeLibraryRemoteNavigationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun focused_library_opens_with_one_center_press() {
        val openedLibraryId = AtomicReference<String?>()
        val library = MediaItem(
            "movies-library",
            "Filmes",
            MediaItemType.CollectionFolder,
            collectionType = "movies",
        )
        val renderedProfile = AtomicReference<HomeDeviceClass?>()
        val renderedAsTelevision = AtomicReference(false)
        composeRule.setContent {
            BoxWithConstraints(Modifier.fillMaxSize()) {
                val isTelevision = isTelevisionDevice()
                val profile = homeDeviceClass(maxWidth.value.roundToInt(), isTelevision)
                renderedProfile.set(profile)
                renderedAsTelevision.set(isTelevision)
                MulletaFlixTheme {
                    LibraryTiles(
                        libraries = listOf(library),
                        layoutSpec = homeLayoutSpec(profile),
                        onLibraryClick = {
                            dispatchHomeLibraryClick(it, { openedLibraryId.set(it) }, {})
                        },
                    )
                }
            }
        }
        val expectedProfile = requireNotNull(
            InstrumentationRegistry.getArguments().getString("expectedDeviceProfile"),
        ) { "Set expectedDeviceProfile=TV for the Android TV AVD" }
        assertEquals("Remote key validation must run on Android TV", "TV", expectedProfile)
        assertTrue("The active device must report Android TV mode", renderedAsTelevision.get())
        assertEquals(HomeDeviceClass.TV, renderedProfile.get())

        val tile = composeRule.onNodeWithContentDescription("Abrir Filmes")
        tile.requestFocus()
        tile.performKeyInput { pressKey(Key.DirectionCenter) }

        assertEquals("movies-library", openedLibraryId.get())
    }
}
