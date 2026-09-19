package org.mulletaflix.android

import android.app.Application
import com.google.android.gms.cast.framework.CastContext
import dagger.hilt.android.HiltAndroidApp

/**
 * Application entry point for MulletaFlix Android.
 *
 * Hilt generates the DI component graph from this class.
 * Global initialization (logging, strict mode, crash reporting) goes here.
 */
@HiltAndroidApp
class MulletaFlixApp : Application() {

    override fun onCreate() {
        super.onCreate()

        // Initialize Cast before any CastPlayer/MediaRouteButton is composed.
        // Without this, the player can be created but the route chooser is not
        // registered reliably on cold app launches.
        runCatching { CastContext.getSharedInstance(this) }
    }
}
