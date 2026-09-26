package org.mulletaflix.core.common.update

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File

/**
 * A classificação do resultado da instalação é a parte compartilhada entre a checagem
 * automática da `MainActivity` e o Centro de Atualizações dos Ajustes. Estes testes
 * fixam os três desfechos e as duas mensagens canônicas, para que as duas telas
 * continuem dizendo a mesma coisa no mesmo caso.
 */
class AppUpdateInstallOutcomeTest {

    private val apk = File("update.apk")

    @Test
    fun `an installer that opens classifies as started and asks for no message`() {
        var calls = 0

        val outcome = installDownloadedApk(apk) { file ->
            calls++
            assertEquals(apk, file)
            true
        }

        assertEquals(AppUpdateInstallOutcome.Started, outcome)
        assertEquals("o instalador deve ser chamado exatamente uma vez", 1, calls)
        assertNull(outcome.errorMessageOrNull())
    }

    @Test
    fun `an installer answer of false classifies as rejected with the permission message`() {
        val outcome = installDownloadedApk(apk) { false }

        assertEquals(AppUpdateInstallOutcome.Rejected, outcome)
        // Literal, de propósito: a mensagem é o contrato visível das duas telas;
        // comparar com a constante não pegaria ninguém editando o texto dela.
        assertEquals(
            "Permita a instalação de fontes desconhecidas e tente novamente.",
            outcome.errorMessageOrNull(),
        )
    }

    @Test
    fun `an installer exception preserves its detail`() {
        val outcome = installDownloadedApk(apk) { throw IllegalStateException("sem fileprovider") }

        assertEquals(AppUpdateInstallOutcome.Failed("sem fileprovider"), outcome)
        assertEquals("sem fileprovider", outcome.errorMessageOrNull())
    }

    @Test
    fun `an installer exception without detail falls back to the generic message`() {
        val outcome = installDownloadedApk(apk) { throw IllegalStateException() }

        assertEquals(AppUpdateInstallOutcome.Failed(null), outcome)
        assertEquals(
            "Não foi possível abrir o instalador do APK.",
            outcome.errorMessageOrNull(),
        )
    }

    @Test
    fun `rejected and failed never read as a started installation`() {
        assertTrue(
            installDownloadedApk(apk) { false }.errorMessageOrNull() != null,
        )
        assertTrue(
            installDownloadedApk(apk) { throw RuntimeException("x") }.errorMessageOrNull() != null,
        )
    }
}
