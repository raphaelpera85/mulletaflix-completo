package org.mulletaflix.domain.repository

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Quando a sessão guardada precisa ser descartada.
 *
 * O token e o endereço vivem em chaves independentes do mesmo armazenamento, e o
 * endereço é reescrito antes de a verificação terminar. Sem esta decisão, apontar
 * o app para um servidor B deixava o token do servidor A no lugar — e o app
 * passava a mandar essa credencial para B em toda requisição.
 *
 * A identidade que decide é o `serverId`, não o endereço: o mesmo servidor é
 * legitimamente alcançado por dois endereços (LAN e DuckDNS) e trocar entre eles
 * não pode deslogar ninguém.
 */
class ServerSessionScopeTest {

    @Test
    fun `um servidor comprovadamente diferente derruba a sessao`() {
        assertTrue(shouldClearSessionForServerChange("server-a", "server-b"))
    }

    @Test
    fun `o mesmo servidor em outro endereco mantem a sessao`() {
        // LAN e DuckDNS do mesmo servidor: é a troca que o app faz sozinho quando
        // o Wi-Fi cai, e deslogar ali seria um defeito novo.
        assertFalse(shouldClearSessionForServerChange("server-a", "server-a"))
        assertFalse(shouldClearSessionForServerChange("SERVER-A", "server-a"))
        assertFalse(shouldClearSessionForServerChange("  server-a  ", "server-a"))
    }

    @Test
    fun `sem identidade de um dos lados a sessao e mantida`() {
        // Instalação antiga sem `SERVER_ID`, ou servidor que não publica `Id`.
        // Deslogar por não conseguir provar seria pior que o risco evitado — e o
        // próximo login grava a identidade.
        assertFalse(shouldClearSessionForServerChange(null, "server-b"))
        assertFalse(shouldClearSessionForServerChange("server-a", null))
        assertFalse(shouldClearSessionForServerChange(null, null))
        assertFalse(shouldClearSessionForServerChange("", "server-b"))
        assertFalse(shouldClearSessionForServerChange("server-a", "   "))
    }

    @Test
    fun `a decisao nao depende da ordem dos argumentos`() {
        assertTrue(shouldClearSessionForServerChange("a", "b"))
        assertTrue(shouldClearSessionForServerChange("b", "a"))
        assertFalse(shouldClearSessionForServerChange("a", "a"))
        assertFalse(shouldClearSessionForServerChange("a", "a"))
    }
}
