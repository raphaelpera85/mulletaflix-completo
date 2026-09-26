package org.mulletaflix.feature.player

/**
 * O que "Tentar novamente" deve fazer no estado atual do player.
 *
 * A decisão estava dentro de `PlayerViewModel.retryPlayback()` como quatro `if`
 * em sequência, e essa função já foi palco de dois defeitos reais: um deles
 * fazia o botão parecer quebrado durante uma queda de rede (retornava em
 * silêncio) e o outro deixava o item seguinte tocando na posição do anterior.
 * Nada disso era alcançável por teste, porque a decisão só existia dentro de um
 * `ViewModel` que constrói o próprio `ExoPlayer`.
 *
 * Como tipo, a decisão vira exaustiva: acrescentar um caso novo passa a exigir
 * tratar o caso novo nos dois lugares que importam — aqui e no `when` do
 * ViewModel.
 */
internal sealed interface PlaybackRetryPlan {

    /** Não há nem item carregado: não existe o que repontar. */
    data object Nothing : PlaybackRetryPlan

    /**
     * O aparelho está sem rede.
     *
     * Retornar em silêncio fazia o botão parecer quebrado; o coletor de rede já
     * reinicia a reprodução sozinho quando a conexão volta, então a tela precisa
     * dizer isso em vez de não fazer nada.
     */
    data object WarnOffline : PlaybackRetryPlan

    /** O item é conhecido, mas o player não tem mídia preparada: buscar de novo. */
    data object Reload : PlaybackRetryPlan

    /**
     * Já existe mídia preparada: preparar outra vez e voltar para [positionMs],
     * que nunca é menor que a última posição boa nem que a posição atual.
     */
    data class Restart(val positionMs: Long) : PlaybackRetryPlan
}

/**
 * A decisão, isolada do player.
 *
 * A ordem das perguntas é a ordem das consequências: um item ausente vence tudo
 * (não há o que tentar), a reprodução offline vence a rede (um arquivo baixado
 * não depende do servidor), a rede vence a preparação (repintar a mídia durante
 * uma queda só troca uma falha por outra).
 */
internal fun playbackRetryPlan(
    itemId: String?,
    isOfflinePlayback: Boolean,
    isNetworkOffline: Boolean,
    hasPreparedMedia: Boolean,
    currentPositionMs: Long,
    positionAtErrorMs: Long,
): PlaybackRetryPlan = when {
    itemId == null -> PlaybackRetryPlan.Nothing
    isOfflinePlayback -> PlaybackRetryPlan.Nothing
    isNetworkOffline -> PlaybackRetryPlan.WarnOffline
    !hasPreparedMedia -> PlaybackRetryPlan.Reload
    else -> PlaybackRetryPlan.Restart(playbackRetryPosition(currentPositionMs, positionAtErrorMs))
}
