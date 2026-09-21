package org.mulletaflix.android

import android.app.Application
import coil.ImageLoader
import coil.ImageLoaderFactory
import coil.disk.DiskCache
import coil.memory.MemoryCache
import com.google.android.gms.cast.framework.CastContext
import dagger.hilt.android.HiltAndroidApp
import javax.inject.Inject
import org.mulletaflix.core.api.ClientIdentityInterceptor
import org.mulletaflix.core.api.buildAuthenticatedImageClient
import org.mulletaflix.core.api.ServerUrlInterceptor

/**
 * Application entry point for MulletaFlix Android.
 *
 * Hilt generates the DI component graph from this class. It also owns the Coil
 * [ImageLoader], so artwork is fetched through the same authenticated client as
 * the API: a cover grid that reaches the server as anonymous is throttled to 30
 * requests per 10 s and loads very slowly.
 */
@HiltAndroidApp
class MulletaFlixApp : Application(), ImageLoaderFactory {

    /**
     * Session-dependent interceptors start as plain instances because Hilt
     * cannot inject into an Application field before `onCreate`.
     */
    @Inject lateinit var clientIdentityInterceptor: ClientIdentityInterceptor

    @Inject lateinit var serverUrlInterceptor: ServerUrlInterceptor

    override fun onCreate() {
        super.onCreate()

        // Initialize Cast before any CastPlayer/MediaRouteButton is composed.
        // Without this, the player can be created but the route chooser is not
        // registered reliably on cold app launches.
        runCatching { CastContext.getSharedInstance(this) }
    }

    override fun newImageLoader(): ImageLoader = ImageLoader.Builder(this)
        .okHttpClient(
            // Same session interceptors as the API client, so artwork is
            // authenticated and identified instead of looking anonymous.
            buildAuthenticatedImageClient(
                serverUrlInterceptor = serverUrlInterceptor,
                clientIdentityInterceptor = clientIdentityInterceptor,
            ),
        )
        .memoryCache {
            MemoryCache.Builder(this)
                .maxSizePercent(0.25)
                .build()
        }
        .diskCache {
            DiskCache.Builder()
                .directory(cacheDir.resolve("image_cache"))
                .maxSizeBytes(256L * 1024 * 1024)
                .build()
        }
        .crossfade(true)
        .build()
}
