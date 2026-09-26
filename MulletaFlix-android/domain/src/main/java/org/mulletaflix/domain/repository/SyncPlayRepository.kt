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
 * O que a API REST permite é criar, entrar, sair, listar e enviar comandos. O
 * estado realtime da reprodução chega pelo WebSocket observado pelo player.
 */
data class SyncPlayGroup(
    val groupId: String,
    val groupName: String,
    val state: String?,
    val participants: List<String>,
)

enum class SyncPlayPlaybackCommand { PAUSE, UNPAUSE, STOP }

data class SyncPlayPlaybackStatus(
    val whenUtc: String,
    val positionTicks: Long,
    val isPlaying: Boolean,
    val playlistItemId: String,
)

interface SyncPlayRepository {
    suspend fun getGroups(): Result<List<SyncPlayGroup>>
    suspend fun createGroup(name: String): Result<Unit>
    suspend fun joinGroup(groupId: String): Result<Unit>
    suspend fun leaveGroup(): Result<Unit>
    suspend fun sendPlaybackCommand(command: SyncPlayPlaybackCommand): Result<Unit>
    suspend fun reportBuffering(status: SyncPlayPlaybackStatus): Result<Unit>
    suspend fun reportReady(status: SyncPlayPlaybackStatus): Result<Unit>
}
