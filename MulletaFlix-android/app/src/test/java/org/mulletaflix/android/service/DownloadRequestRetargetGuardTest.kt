package org.mulletaflix.android.service

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Guarda para o defeito da v1.2.66: "Tentar novamente" reenviava a URL gravada, com o
 * host e o token da sessão em que o download foi enfileirado.
 *
 * A correção vive em `downloadRequestFor`, por onde os dois caminhos passam. O que
 * esta guarda impede é o terceiro caminho: um `DownloadRequest.Builder` novo montado
 * direto com a URL recebida, que voltaria a gravar um endereço que envelhece.
 *
 * É uma varredura de fonte de propósito: nenhuma asserção de runtime prova a
 * **ausência** de um segundo ponto de construção.
 */
class DownloadRequestRetargetGuardTest {

    /** O diretório do módulo é o diretório de trabalho do teste. */
    private val repositoryFile = File(
        "src/main/java/org/mulletaflix/android/service/Media3DownloadRepository.kt",
    )

    private fun sourceLines(): List<String> {
        assertTrue(
            "não encontrei ${repositoryFile.absolutePath}; " +
                "diretório de trabalho é ${File(".").absolutePath}",
            repositoryFile.isFile,
        )
        return repositoryFile.readLines()
    }

    @Test
    fun `only one place builds a download request`() {
        val lines = sourceLines()
        val constructions = lines
            .withIndex()
            .filter { (_, line) -> line.contains("DownloadRequest.Builder(") }
            .map { (index, line) -> "${index + 1}: ${line.trim()}" }

        assertEquals(
            "todo DownloadRequest deve sair de `downloadRequestFor`, que reponta a URL " +
                "para o endereço em uso. Um segundo ponto de construção grava um " +
                "endereço que envelhece:\n" + constructions.joinToString("\n"),
            1,
            constructions.size,
        )

        val source = lines.joinToString("\n")
        val helper = source.indexOf("internal fun downloadRequestFor(")
        assertTrue("downloadRequestFor não encontrado", helper >= 0)
        assertTrue(
            "o único ponto de construção precisa passar pelo repontamento",
            source.substring(helper).contains("retargetMediaUrl("),
        )
    }

    @Test
    fun `both the queue and the retry go through it`() {
        val source = sourceLines().joinToString("\n")
        val enqueueTime = source.indexOf("override fun enqueueWithMetadata")
        val retryTime = source.indexOf("override fun retry(")
        val helperTime = source.indexOf("internal fun downloadRequestFor(")
        assertTrue("enqueueWithMetadata não encontrado", enqueueTime >= 0)
        assertTrue("retry não encontrado", retryTime >= 0)
        assertTrue("downloadRequestFor não encontrado", helperTime >= 0)

        listOf("enqueueWithMetadata" to enqueueTime, "retry" to retryTime).forEach { (name, start) ->
            val body = source.substring(start, helperTime)
            assertTrue(
                "$name precisa construir o pedido por downloadRequestFor, senão volta a " +
                    "enfileirar a URL gravada",
                body.contains("downloadRequestFor("),
            )
        }
    }
}
