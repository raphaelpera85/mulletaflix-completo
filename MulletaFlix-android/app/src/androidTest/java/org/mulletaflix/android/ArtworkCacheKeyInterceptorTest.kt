package org.mulletaflix.android

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import coil.annotation.ExperimentalCoilApi
import coil.intercept.Interceptor
import coil.request.ImageRequest
import coil.request.ImageResult
import coil.size.Size
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertSame
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Prova a ligação do interceptor que dá às capas uma chave de cache que sobrevive à
 * troca de endereço.
 *
 * A política em si (`canonicalImageCacheKey`) tem testes de JVM. O que só se prova
 * aqui é que o interceptor **entrega** essa chave ao Coil: sem isso, o Coil continua
 * usando a URL inteira como chave — com host e token — e entrar e sair de casa joga
 * fora a grade de capas.
 *
 * A `Chain` é falsa de propósito: o interceptor é exercitado até o `proceed`, e o
 * resultado da imagem não interessa. Um `Interceptor.Chain` tem cinco membros, o que
 * torna a falsificação honesta em vez de uma simulação da biblioteca.
 */
@RunWith(AndroidJUnit4::class)
@OptIn(ExperimentalCoilApi::class)
class ArtworkCacheKeyInterceptorTest {

    private val context = InstrumentationRegistry.getInstrumentation().targetContext

    /** Guarda a requisição que chegou ao fim da cadeia. */
    private class CapturingChain(private val initialRequest: ImageRequest) : Interceptor.Chain {
        var proceeded: ImageRequest? = null

        override val request: ImageRequest get() = initialRequest
        override val size: Size get() = Size.ORIGINAL

        override fun withRequest(request: ImageRequest): Interceptor.Chain = CapturingChain(request)
        override fun withSize(size: Size): Interceptor.Chain = this

        override suspend fun proceed(request: ImageRequest): ImageResult {
            proceeded = request
            throw ProbeReached()
        }
    }

    private class ProbeReached : RuntimeException("o interceptor chamou proceed")

    private fun requestFor(model: Any): ImageRequest =
        ImageRequest.Builder(context).data(model).build()

    /** Roda o interceptor e devolve a requisição que ele entregou ao Coil. */
    private fun intercepted(model: Any, serverId: String? = "server-abc"): ImageRequest {
        val chain = CapturingChain(requestFor(model))
        runBlocking {
            try {
                artworkCacheKeyInterceptor { serverId }.intercept(chain)
                throw AssertionError("o interceptor deveria ter chamado proceed")
            } catch (_: ProbeReached) {
                // esperado: a cadeia falsa para aqui
            }
        }
        return chain.proceeded ?: error("a requisição não chegou ao fim da cadeia")
    }

    @Test
    fun aCoverGetsTheSameCanonicalKeyOnBothCaches() {
        val proceeded = intercepted(
            "http://192.168.15.9:8096/Items/movie-1/Images/Primary?tag=t&api_key=OLD",
        )
        val expected = "mulletaflix|server-abc|/Items/movie-1/Images/Primary|tag=t"
        assertEquals(expected, proceeded.diskCacheKey)
        assertEquals(expected, proceeded.memoryCacheKey?.key)
    }

    @Test
    fun theLanAndThePublicAddressProduceTheSameKey() {
        val lan = intercepted("http://192.168.15.9:8096/Items/movie-1/Images/Primary?tag=t&api_key=OLD")
        val public = intercepted("http://mulletaflix.duckdns.org:8096/Items/movie-1/Images/Primary?tag=t&api_key=NEW")
        // Sem o `assertNotNull` este teste passaria com os dois lados nulos, que é
        // exatamente o estado em que a chave não foi aplicada.
        assertNotNull("a capa deveria ter recebido uma chave canônica", lan.diskCacheKey)
        assertEquals(lan.diskCacheKey, public.diskCacheKey)
    }

    @Test
    fun aLocalDrawableKeepsTheDefaultKey() {
        // O logo e o fundo do login não vêm do servidor; dar-lhes uma chave
        // normalizada juntaria dois drawables diferentes numa entrada só.
        val proceeded = intercepted(android.R.drawable.ic_menu_report_image)
        assertNull(proceeded.diskCacheKey)
        assertNull(proceeded.memoryCacheKey)
    }

    @Test
    fun aRequestWithoutACanonicalKeyIsHandedOnUntouched() {
        val original = requestFor(android.R.drawable.ic_menu_report_image)
        val chain = CapturingChain(original)
        runBlocking {
            try {
                artworkCacheKeyInterceptor { "server-abc" }.intercept(chain)
            } catch (_: ProbeReached) {
                // esperado
            }
        }
        assertSame(
            "uma requisição sem chave canônica deve seguir sem ser reconstruída",
            original,
            chain.proceeded,
        )
    }
}
