package org.mulletaflix.feature.auth

internal fun authenticationErrorMessage(statusCode: Int?): String = when (statusCode) {
    400, 401 -> "Usuário ou senha inválidos. Confira os dados e tente novamente."
    403 -> "Este usuário não tem permissão para acessar o servidor."
    404 -> "Usuário ou servidor não encontrado."
    408, 504 -> "O servidor demorou para responder. Tente novamente."
    else -> "Não foi possível entrar no servidor. Verifique a conexão e tente novamente."
}

/** Converts transport failures into actionable text instead of leaking OkHttp/Android jargon. */
internal fun serverConnectionErrorMessage(error: Throwable?): String {
    val message = error?.message.orEmpty()
    return when {
        message.contains("CLEARTEXT", ignoreCase = true) ->
            "A conexão HTTP foi bloqueada pelo Android. Use HTTPS ou atualize o aplicativo."
        error is java.net.UnknownHostException ->
            "Servidor não encontrado. Verifique o endereço e a conexão com a internet."
        error is java.net.ConnectException ->
            "Não foi possível conectar ao servidor. Verifique se ele está online."
        error is java.net.SocketTimeoutException ->
            "O servidor demorou para responder. Tente novamente."
        error is retrofit2.HttpException && error.code() == 401 ->
            "O servidor recusou a conexão anônima. Verifique se o endereço está correto."
        error is retrofit2.HttpException && error.code() == 404 ->
            "A API do MulletaFlix não foi encontrada nesse endereço."
        error is retrofit2.HttpException ->
            "O servidor respondeu com um erro (${error.code()}). Tente novamente."
        else ->
            "Não foi possível conectar ao servidor. Verifique a conexão e tente novamente."
    }
}
