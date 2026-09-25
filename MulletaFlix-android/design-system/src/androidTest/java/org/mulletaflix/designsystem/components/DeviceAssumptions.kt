package org.mulletaflix.designsystem.components

import android.content.res.Configuration
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assume

/** D-pad focus and centre-key routing are meaningful only on the Android TV surface. */
internal fun assumeTelevisionSurface() {
    val context = InstrumentationRegistry.getInstrumentation().targetContext
    Assume.assumeTrue(
        "requires an Android TV remote surface",
        (context.resources.configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
            Configuration.UI_MODE_TYPE_TELEVISION,
    )
}
