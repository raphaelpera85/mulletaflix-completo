package org.mulletaflix.domain.repository

/**
 * Uma sala SyncPlay como o servidor realmente a descreve.
 *
 * Havia aqui dois campos que o servidor **nunca envia** — `PlayingItemId` e
 * `PositionTicks` — porque `MediaBrowser.Model/SyncPlay/GroupInfoDto.cs` não os tem
 * (o `Group.cs` constrói o DTO sem eles). Eles eram lidos pela tela de salas, que
 * navegava para o player com esse id: como o valor é sempre nulo, "Entrar na
 * sessão" nunca abria nada, e a tela ainda trocava para "Sair da sala atual", o
 * que fazia a entrada parecer bem-sucedida.
 *
 * O que o app faz hoje é o que os quatro endpoints REST permitem: criar, entrar,
 * sair e listar. Seguir a reprodução do grupo exige o WebSocket do SyncPlay, que
 * este app não tem — por isso os campos saíram em vez de ficarem mentindo.
 */
data class SyncPlayGroup(
    val groupId: String,
    val groupName: String,
    val state: String?,
    val participants: List<String>,
)

interface SyncPlayRepository {
    suspend fun getGroups(): Result<List<SyncPlayGroup>>
    suspend fun createGroup(name: String): Result<Unit>
    suspend fun joinGroup(groupId: String): Result<Unit>
    suspend fun leaveGroup(): Result<Unit>
}
