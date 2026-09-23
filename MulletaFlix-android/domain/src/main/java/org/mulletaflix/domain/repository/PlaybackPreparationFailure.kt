package org.mulletaflix.domain.repository

/**
 * O motivo que o servidor já mandava e o app jogava fora.
 *
 * Quando nenhuma fonte é compatível o servidor responde **200** com
 * `MediaSources = []` **e** `ErrorCode` preenchido
 * (`MediaBrowser.Model/MediaInfo/PlaybackInfoResponse.ErrorCode`; o valor é
 * atribuído em `MediaInfoHelper` quando a lista fica vazia). O campo era
 * desserializado no DTO e nunca lido em lugar nenhum do app, então "sua conta não
 * pode reproduzir isto" e "você atingiu o limite de transmissões simultâneas"
 * chegavam ao usuário como a mesma frase: "nenhuma fonte de reprodução está
 * disponível para esta mídia".
 *
 * A frase genérica continua sendo o padrão — é a resposta honesta para um código
 * que o app ainda não aprendeu.
 */
const val DEFAULT_PLAYBACK_PREPARATION_FAILURE =
    "Nenhuma fonte de reprodução está disponível para esta mídia."

/** The three values `PlaybackErrorCode` can take, per the server's own enum. */
private const val PLAYBACK_ERROR_NOT_ALLOWED = "NotAllowed"
private const val PLAYBACK_ERROR_RATE_LIMIT = "RateLimitExceeded"
private const val PLAYBACK_ERROR_NO_COMPATIBLE_STREAM = "NoCompatibleStream"

/**
 * Traduz o `ErrorCode` do servidor. Público porque quem lê o DTO é `:data`.
 */
fun playbackPreparationFailureMessage(errorCode: String?): String = when (errorCode) {
    PLAYBACK_ERROR_NOT_ALLOWED ->
        "Sua conta não tem permissão para reproduzir esta mídia."

    PLAYBACK_ERROR_RATE_LIMIT ->
        "Limite de transmissões simultâneas atingido. Encerre outra reprodução e tente de novo."

    PLAYBACK_ERROR_NO_COMPATIBLE_STREAM ->
        "O servidor não encontrou uma forma de enviar esta mídia para este aparelho. " +
            "Tente uma qualidade menor."

    else -> DEFAULT_PLAYBACK_PREPARATION_FAILURE
}
