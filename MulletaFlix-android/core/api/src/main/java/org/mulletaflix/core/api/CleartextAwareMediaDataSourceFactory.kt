package org.mulletaflix.core.api

import androidx.media3.common.util.UnstableApi
import androidx.media3.datasource.DataSource
import androidx.media3.datasource.okhttp.OkHttpDataSource
import java.util.concurrent.TimeUnit
import okhttp3.OkHttpClient
import org.mulletaflix.core.common.network.enforceLocalNetworkCleartextPolicy

/** Creates Media3 HTTP sources that enforce the local-only cleartext policy on every exchange. */
@UnstableApi
fun cleartextAwareMediaDataSourceFactory(
    connectTimeoutMs: Int,
    readTimeoutMs: Int,
): DataSource.Factory {
    require(connectTimeoutMs > 0) { "Connect timeout must be positive." }
    require(readTimeoutMs > 0) { "Read timeout must be positive." }

    val client = OkHttpClient.Builder()
        .connectTimeout(connectTimeoutMs.toLong(), TimeUnit.MILLISECONDS)
        .readTimeout(readTimeoutMs.toLong(), TimeUnit.MILLISECONDS)
        .enforceLocalNetworkCleartextPolicy()
        .build()

    return cleartextAwareMediaDataSourceFactory(client)
}

/** Internal seam for tests that need a trusted local TLS fixture. */
@UnstableApi
internal fun cleartextAwareMediaDataSourceFactory(client: OkHttpClient): DataSource.Factory =
    OkHttpDataSource.Factory(client)
