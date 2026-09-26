package org.mulletaflix.feature.livetv

import android.content.res.Configuration
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assume.assumeTrue

internal fun assumeTelevisionProfile() {
    val context = InstrumentationRegistry.getInstrumentation().targetContext
    val isTelevision =
        (context.resources.configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
            Configuration.UI_MODE_TYPE_TELEVISION
    assumeTrue("Remote-focus behavior is specific to Android TV", isTelevision)
}
