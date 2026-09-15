package org.mulletaflix.feature.auth

internal fun authenticationErrorMessage(statusCode: Int?): String = when (statusCode) {
    400, 401 -> "Usuário ou senha inválidos. Confira os dados e tente novamente."
    403 -> "Este usuário não tem permissão para acessar o servidor."
    404 -> "Usuário ou servidor não encontrado."
    408, 504 -> "O servidor demorou para responder. Tente novamente."
    else -> "Não foi possível entrar no servidor. Verifique a conexão e tente novamente."
}
