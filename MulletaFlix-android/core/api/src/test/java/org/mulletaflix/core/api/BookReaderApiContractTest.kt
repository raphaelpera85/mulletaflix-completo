package org.mulletaflix.core.api

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.http.GET
import retrofit2.http.Path
import retrofit2.http.Streaming

class BookReaderApiContractTest {

    @Test
    fun `epub endpoint is streamed with the item id path contract`() {
        val method = MulletaFlixApiService::class.java.methods.single { it.name == "getBookEpub" }
        val get = method.getAnnotation(GET::class.java)
        val streaming = method.getAnnotation(Streaming::class.java)
        val itemPath = method.parameterAnnotations
            .asSequence()
            .flatMap { it.asSequence() }
            .filterIsInstance<Path>()
            .single()

        assertNotNull(get)
        assertNotNull(streaming)
        assertEquals("BookReader/Items/{itemId}/BookReader/Epub", get!!.value)
        assertEquals("itemId", itemPath.value)
        assertTrue(method.genericParameterTypes.last().typeName.contains("ResponseBody"))
    }
}
