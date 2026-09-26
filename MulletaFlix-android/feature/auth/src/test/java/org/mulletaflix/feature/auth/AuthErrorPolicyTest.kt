package org.mulletaflix.feature.auth

import org.junit.Assert.assertEquals
import org.junit.Test
import retrofit2.HttpException
import retrofit2.Response
import okhttp3.ResponseBody.Companion.toResponseBody

class AuthErrorPolicyTest {
    @Test fun `client authentication errors explain invalid credentials`() {
        assertEquals(
            "Usuário ou senha inválidos. Confira os dados e tente novamente.",
            authenticationErrorMessage(400),
        )
        assertEquals(authenticationErrorMessage(400), authenticationErrorMessage(401))
    }

    @Test fun `forbidden response explains access policy`() {
        assertEquals(
            "Este usuário não tem permissão para acessar o servidor.",
            authenticationErrorMessage(403),
        )
    }

    @Test fun `unknown status uses safe generic message`() {
        assertEquals(
            "Não foi possível entrar no servidor. Verifique a conexão e tente novamente.",
            authenticationErrorMessage(null),
        )
    }

    @Test fun `transport errors explain common connection failures`() {
        assertEquals(
            "Servidor não encontrado. Verifique o endereço e a conexão com a internet.",
            serverConnectionErrorMessage(java.net.UnknownHostException("mulletaflix")),
        )
        assertEquals(
            "A conexão HTTP foi bloqueada pelo Android. Use HTTPS ou atualize o aplicativo.",
            serverConnectionErrorMessage(IllegalStateException("CLEARTEXT communication not permitted")),
        )
    }

    @Test fun `http transport errors remain user actionable`() {
        val error = HttpException(Response.error<Any>(404, "".toResponseBody(null)))
        assertEquals(
            "A API do MulletaFlix não foi encontrada nesse endereço.",
            serverConnectionErrorMessage(error),
        )
    }
}
