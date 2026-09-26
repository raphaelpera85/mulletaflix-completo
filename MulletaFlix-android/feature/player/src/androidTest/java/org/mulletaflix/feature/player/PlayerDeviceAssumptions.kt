package org.mulletaflix.feature.player

import android.content.res.Configuration
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assume.assumeTrue

/** Focus-initialization checks model the remote-control profile and should run on Android TV only. */
internal fun assumeTelevisionProfile() {
    val context = InstrumentationRegistry.getInstrumentation().targetContext
    val isTelevision =
        (context.resources.configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
            Configuration.UI_MODE_TYPE_TELEVISION
    assumeTrue("Remote-focus behavior is specific to Android TV", isTelevision)
}
