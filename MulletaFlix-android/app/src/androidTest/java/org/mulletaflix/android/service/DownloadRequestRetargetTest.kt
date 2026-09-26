package org.mulletaflix.android.service

import androidx.media3.common.util.UnstableApi
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Prova que o pedido de download entregue ao serviço aponta para o endereço em uso.
 *
 * O cenário que isto fecha: um download enfileirado em casa grava a URL da LAN (com o
 * token daquela sessão) no índice do Media3; o app troca sozinho para o endereço
 * público quando a rede some, e "Tentar novamente" reenviava exatamente a URL gravada
 * — falhando para sempre, e sem dizer por quê.
 *
 * O repositório em si não pode ser construído fora de um app com grafo Hilt (ele
 * depende do `DownloadManager` do sistema), então o que se exercita aqui é a função
 * por onde **os dois** caminhos passam. Um teste de fonte
 * (`DownloadRequestRetargetGuardTest`) garante que nenhum outro ponto do arquivo monta
 * um `DownloadRequest` por fora dela.
 */
@UnstableApi
@RunWith(AndroidJUnit4::class)
class DownloadRequestRetargetTest {

    private val lanUrl =
        "http://192.168.15.9:8096/Videos/item-1/stream?MediaSourceId=s1&api_key=OLD&Static=true"

    @Test
    fun aRequestQueuedAtHomeIsRetriedAgainstTheAddressInUse() {
        val request = downloadRequestFor(
            requestId = "user-1:item-1",
            storedUri = lanUrl,
            baseUrl = "http://mulletaflix.duckdns.org:8096",
            accessToken = "NEW",
        )

        assertEquals("mulletaflix.duckdns.org", request.uri.host)
        assertEquals(8096, request.uri.port)
        assertEquals("/Videos/item-1/stream", request.uri.path)
        assertFalse(
            "a URL gravada não pode sobreviver ao endereço atual: ${request.uri}",
            request.uri.toString().contains("192.168.15.9"),
        )
        assertFalse(
            "o token da sessão antiga não pode ser reenviado: ${request.uri}",
            request.uri.toString().contains("OLD"),
        )
        assertTrue(request.uri.toString().contains("api_key=NEW"))
    }

    @Test
    fun theRequestKeepsThePathAndTheOtherParameters() {
        // `MediaSourceId` e `Static` identificam o stream; perder qualquer um deles
        // baixaria outra coisa ou nada.
        val request = downloadRequestFor(
            requestId = "user-1:item-1",
            storedUri = lanUrl,
            baseUrl = "http://mulletaflix.duckdns.org:8096",
            accessToken = "NEW",
        )
        assertEquals(
            "http://mulletaflix.duckdns.org:8096/Videos/item-1/stream?MediaSourceId=s1&api_key=NEW&Static=true",
            request.uri.toString(),
        )
    }

    @Test
    fun theRequestKeepsItsStableId() {
        // O id estável é o que faz o Media3 retomar o download em vez de criar outro.
        val request = downloadRequestFor(
            requestId = "user-1:item-1",
            storedUri = lanUrl,
            baseUrl = "http://mulletaflix.duckdns.org:8096",
            accessToken = "NEW",
        )
        assertEquals("user-1:item-1", request.id)
        assertEquals("http", request.uri.scheme)
    }

    @Test
    fun aSessionThatHasNotBeenReadYetLeavesTheStoredUrlAlone() {
        // Sem endereço lido, chutar é pior do que tentar o que foi gravado.
        val request = downloadRequestFor(
            requestId = "user-1:item-1",
            storedUri = lanUrl,
            baseUrl = "",
            accessToken = null,
        )
        assertEquals(lanUrl, request.uri.toString())
    }
}
