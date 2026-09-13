package org.mulletaflix.android

import android.app.Application
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
        // Future: initialize crash reporter, Timber logging, etc.
    }
}
