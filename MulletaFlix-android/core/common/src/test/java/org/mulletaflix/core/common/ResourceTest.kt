package org.mulletaflix.core.common

import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.toList
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.common.result.Resource
import org.mulletaflix.core.common.result.asResource
import org.mulletaflix.core.common.result.dataOrNull
import org.mulletaflix.core.common.result.map

class ResourceTest {

    @Test
    fun `map transforms successful resource`() {
        val resource: Resource<Int> = Resource.Success(42)
        val transformed = resource.map { "Value: $it" }
        assertTrue(transformed is Resource.Success)
        assertEquals("Value: 42", (transformed as Resource.Success).data)
    }

    @Test
    fun `map propagates error and loading`() {
        val error: Resource<Int> = Resource.Error(RuntimeException("boom"))
        val transformedError = error.map { "Value: $it" }
        assertTrue(transformedError is Resource.Error)

        val loading: Resource<Int> = Resource.Loading
        val transformedLoading = loading.map { "Value: $it" }
        assertEquals(Resource.Loading, transformedLoading)
    }

    @Test
    fun `dataOrNull returns data only on success`() {
        assertEquals(42, Resource.Success(42).dataOrNull())
        assertEquals(null, Resource.Error(RuntimeException("fail")).dataOrNull())
        assertEquals(null, Resource.Loading.dataOrNull())
    }

    @Test
    fun `asResource emits loading then success on normal flow`() = runTest {
        val source = flow { emit("hello") }
        val result = source.asResource().toList()

        assertEquals(2, result.size)
        assertEquals(Resource.Loading as Resource<String>, result[0])
        assertEquals(Resource.Success("hello") as Resource<String>, result[1])
    }

    @Test
    fun `asResource emits loading then error on failing flow`() = runTest {
        val source = flow<String> { throw IllegalStateException("fail") }
        val result = source.asResource().toList()

        assertEquals(2, result.size)
        assertEquals(Resource.Loading, result[0])
        assertTrue(result[1] is Resource.Error)
        assertEquals("fail", (result[1] as Resource.Error).message)
    }
}
