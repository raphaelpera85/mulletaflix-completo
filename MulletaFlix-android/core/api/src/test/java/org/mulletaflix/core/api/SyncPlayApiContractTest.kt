package org.mulletaflix.core.api

import com.squareup.moshi.Moshi
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Test
import org.mulletaflix.core.api.dto.GroupInfoDto
import retrofit2.http.GET
import retrofit2.http.POST

/**
 * Prende a sala do SyncPlay ao contrato real do servidor.
 *
 * A tela de salas navegava para o player com `PlayingItemId` e `PositionTicks`,
 * que ela lia do DTO. O servidor **não envia** esses campos:
 * `MediaBrowser.Model/SyncPlay/GroupInfoDto.cs` tem apenas `GroupId`, `GroupName`,
 * `State`, `Participants`, `LastUpdatedAt`, `Ping` e `Host`, e
 * `Emby.Server.Implementations/SyncPlay/Group.cs` constrói o DTO exatamente com
 * esses membros. Como o id era sempre nulo, "Entrar na sessão" nunca abria nada —
 * e como a tela troca para "Sair da sala atual" depois do join, a entrada
 * parecia ter funcionado.
 *
 * O payload abaixo é o que o servidor manda. Se um dia ele passar a mandar o item
 * em reprodução, é este teste que diz, em vez de um usuário descobrir que o botão
 * não faz nada.
 */
class SyncPlayApiContractTest {

    private val moshi = Moshi.Builder().build()

    /** `SyncPlay/List` responde com estes campos, e só estes. */
    private val serverPayload = """
        [
          {
            "GroupId": "group-42",
            "GroupName": "Cinema",
            "State": "Playing",
            "Participants": ["Raphael", "Visitante"],
            "LastUpdatedAt": "2026-09-22T10:00:00.0000000Z",
            "Ping": 0,
            "Host": "tv-sala"
          }
        ]
    """.trimIndent()

    private fun parseGroups(json: String): List<GroupInfoDto> {
        val adapter = moshi.adapter<List<GroupInfoDto>>(
            com.squareup.moshi.Types.newParameterizedType(List::class.java, GroupInfoDto::class.java),
        )
        return requireNotNull(adapter.fromJson(json)) { "o payload do servidor precisa ser legível" }
    }

    @Test
    fun `the group carries what the server actually sends`() {
        val group = parseGroups(serverPayload).single()

        assertEquals("group-42", group.groupId)
        assertEquals("Cinema", group.groupName)
        assertEquals("Playing", group.state)
        assertEquals(listOf("Raphael", "Visitante"), group.participants)
    }

    @Test
    fun `fields this app does not use do not break the parse`() {
        // `LastUpdatedAt`, `Ping` e `Host` existem no servidor e não têm uso aqui.
        // Um DTO estrito demais quebraria a tela inteira por causa deles.
        val group = parseGroups(serverPayload).single()

        assertNotNull(group)
        assertEquals(2, group.participants.size)
    }

    @Test
    fun `a group without the optional state still parses`() {
        val json = """[{"GroupId":"g","GroupName":"Sala","Participants":[]}]"""

        val group = parseGroups(json).single()

        assertEquals(null, group.state)
        assertEquals(emptyList<String>(), group.participants)
    }

    @Test
    fun `the room routes are the ones the server exposes`() {
        assertEquals("SyncPlay/List", routeOf("getSyncPlayGroups") { it.getAnnotation(GET::class.java)?.value })
        assertEquals("SyncPlay/New", routeOf("createSyncPlayGroup") { it.getAnnotation(POST::class.java)?.value })
        assertEquals("SyncPlay/Join", routeOf("joinSyncPlayGroup") { it.getAnnotation(POST::class.java)?.value })
        assertEquals("SyncPlay/Leave", routeOf("leaveSyncPlayGroup") { it.getAnnotation(POST::class.java)?.value })
    }

    private fun routeOf(methodName: String, extract: (java.lang.reflect.Method) -> String?): String {
        val method = MulletaFlixApiService::class.java.methods.single { it.name == methodName }
        val route = extract(method)
        assertNotNull("$methodName precisa declarar a rota", route)
        return route!!
    }
}
