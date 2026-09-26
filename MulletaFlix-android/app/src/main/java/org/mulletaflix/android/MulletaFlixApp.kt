package org.mulletaflix.android

import android.app.Application
import coil.ImageLoader
import coil.ImageLoaderFactory
import coil.disk.DiskCache
import coil.intercept.Interceptor
import coil.memory.MemoryCache
import com.google.android.gms.cast.framework.CastContext
import dagger.hilt.android.HiltAndroidApp
import javax.inject.Inject
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.mulletaflix.core.api.ClientIdentityInterceptor
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.buildAuthenticatedImageClient
import org.mulletaflix.core.api.ServerUrlInterceptor
import org.mulletaflix.designsystem.media.canonicalImageCacheKey

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

    @Inject lateinit var sessionRepository: SessionRepository

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
        .components {
            add(
                artworkCacheKeyInterceptor {
                    // Read per request, the same way ClientIdentityInterceptor reads the
                    // token: a DataStore read that is served from memory after the first.
                    runBlocking { sessionRepository.getServerId().first() }
                },
            )
        }
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

/**
 * Gives every piece of artwork a cache key that survives a change of address.
 *
 * Coil keys its memory and disk caches on the request URL, and that URL carries both
 * the server address and the session token — so the LAN address and the public address
 * of the same server were two different entries for the same picture. Entering and
 * leaving home threw the whole cover grid away and left two copies of each poster on
 * disk until the size evictor got around to them.
 *
 * A top-level function rather than an inline property of the Application so the
 * request it hands on can be asserted by a test: the Application itself cannot be
 * built outside a running app with a Hilt graph.
 */
fun artworkCacheKeyInterceptor(currentServerId: () -> String?): Interceptor = Interceptor { chain ->
    val request = chain.request
    val key = (request.data as? String)?.let { url ->
        canonicalImageCacheKey(url, currentServerId())
    }
    val keyedRequest = if (key == null) {
        request
    } else {
        request.newBuilder()
            .memoryCacheKey(key)
            .diskCacheKey(key)
            .build()
    }
    chain.proceed(keyedRequest)
}
