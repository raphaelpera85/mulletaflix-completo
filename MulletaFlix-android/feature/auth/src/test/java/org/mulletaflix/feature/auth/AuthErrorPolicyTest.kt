package org.mulletaflix.feature.auth

import org.junit.Assert.assertEquals
import org.junit.Test

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
}
