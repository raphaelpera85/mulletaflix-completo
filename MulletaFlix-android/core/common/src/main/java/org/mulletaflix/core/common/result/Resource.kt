package org.mulletaflix.core.common.result

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.onStart

/**
 * A sealed interface representing UI/Data state with typed result.
 */
sealed interface Resource<out T> {
    data class Success<T>(val data: T) : Resource<T>
    data class Error(val throwable: Throwable, val message: String? = throwable.localizedMessage) : Resource<Nothing>
    data object Loading : Resource<Nothing>
}

fun <T> Flow<T>.asResource(): Flow<Resource<T>> = this
    .map<T, Resource<T>> { Resource.Success(it) }
    .onStart { emit(Resource.Loading) }
    .catch { emit(Resource.Error(it)) }

inline fun <T, R> Resource<T>.map(transform: (T) -> R): Resource<R> = when (this) {
    is Resource.Success -> Resource.Success(transform(data))
    is Resource.Error -> Resource.Error(throwable, message)
    is Resource.Loading -> Resource.Loading
}

fun <T> Resource<T>.dataOrNull(): T? = when (this) {
    is Resource.Success -> data
    else -> null
}
