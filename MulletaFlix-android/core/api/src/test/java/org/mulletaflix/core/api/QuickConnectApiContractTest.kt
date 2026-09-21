package org.mulletaflix.core.api

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Query
import retrofit2.http.Body

class QuickConnectApiContractTest {
    @Test
    fun `availability uses the server GET contract`() {
        val method = MulletaFlixApiService::class.java.methods.single {
            it.name == "isQuickConnectEnabled"
        }
        val get = method.getAnnotation(GET::class.java)

        assertNotNull(get)
        assertEquals("QuickConnect/Enabled", get!!.value)
    }

    @Test
    fun `initiation uses the server POST contract`() {
        val method = MulletaFlixApiService::class.java.methods.single {
            it.name == "initiateQuickConnect"
        }
        val post = method.getAnnotation(POST::class.java)

        assertNotNull(post)
        assertEquals("QuickConnect/Initiate", post!!.value)
    }

    @Test
    fun `polling uses GET with the secret query contract`() {
        val method = MulletaFlixApiService::class.java.methods.single {
            it.name == "connectQuickConnect"
        }
        val get = method.getAnnotation(GET::class.java)
        val secretQuery = method.parameterAnnotations
            .asSequence()
            .flatMap { it.asSequence() }
            .filterIsInstance<Query>()
            .single()

        assertNotNull(get)
        assertEquals("QuickConnect/Connect", get!!.value)
        assertEquals("secret", secretQuery.value)
        assertTrue(method.genericParameterTypes.last().typeName.contains("QuickConnectResultDto"))
    }

    @Test
    fun `authorized polling exchanges secret at the authentication endpoint`() {
        val method = MulletaFlixApiService::class.java.methods.single {
            it.name == "authenticateWithQuickConnect"
        }
        val post = method.getAnnotation(POST::class.java)
        val body = method.parameterAnnotations
            .asSequence()
            .flatMap { it.asSequence() }
            .filterIsInstance<Body>()
            .single()

        assertNotNull(post)
        assertEquals("Users/AuthenticateWithQuickConnect", post!!.value)
        assertNotNull(body)
        assertTrue(method.genericParameterTypes.last().typeName.contains("AuthenticationResultDto"))
    }
}
