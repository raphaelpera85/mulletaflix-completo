package org.mulletaflix.feature.auth

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Test

/**
 * O nome anunciado pelo ícone do cartão de servidor.
 *
 * O cartão é reusado para duas seções diferentes — "Encontrados nesta rede" e
 * "Servidores salvos / disponíveis" — e usava o mesmo rótulo nas duas. Um servidor
 * apenas descoberto era anunciado como "Servidor salvo" logo abaixo de um título que
 * dizia o contrário.
 */
class ServerCardOriginLabelTest {

    @Test
    fun `um servidor descoberto nao e anunciado como salvo`() {
        val discovered = serverCardIconDescription(
            isOfficial = false,
            origin = ServerCardOrigin.Discovered,
        )

        assertEquals("Servidor encontrado nesta rede", discovered)
        assertNotEquals("Servidor salvo", discovered)
    }

    @Test
    fun `um servidor guardado continua sendo salvo`() {
        assertEquals(
            "Servidor salvo",
            serverCardIconDescription(isOfficial = false, origin = ServerCardOrigin.Saved),
        )
    }

    @Test
    fun `o servidor oficial tem o proprio nome, venha de onde vier`() {
        // O nome do oficial não depende da seção: ele é o mesmo endereço conhecido.
        assertEquals(
            "Servidor oficial na nuvem",
            serverCardIconDescription(isOfficial = true, origin = ServerCardOrigin.Saved),
        )
        assertEquals(
            "Servidor oficial na nuvem",
            serverCardIconDescription(isOfficial = true, origin = ServerCardOrigin.Discovered),
        )
    }

    @Test
    fun `as tres situacoes tem nomes diferentes`() {
        val names = listOf(
            serverCardIconDescription(isOfficial = true, origin = ServerCardOrigin.Saved),
            serverCardIconDescription(isOfficial = false, origin = ServerCardOrigin.Saved),
            serverCardIconDescription(isOfficial = false, origin = ServerCardOrigin.Discovered),
        )

        assertEquals(3, names.distinct().size)
    }
}
