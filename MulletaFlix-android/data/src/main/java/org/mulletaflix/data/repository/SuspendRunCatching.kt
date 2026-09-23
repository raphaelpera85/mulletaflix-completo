package org.mulletaflix.data.repository

import kotlinx.coroutines.CancellationException

/**
 * `runCatching` para código `suspend`: relança [CancellationException] em vez de
 * transformá-la em `Result.failure`.
 *
 * `runCatching` pega `Throwable`, e cancelamento é um `Throwable`. Neste app isso
 * acontece o tempo todo e é intencional: o ViewModel cancela o carregamento
 * anterior antes de lançar o novo (`loadJob?.cancel()`, `refreshJob?.cancel()`), e
 * o usuário troca de tela no meio de uma requisição. Convertido em `Result`, o
 * cancelamento vira `onFailure { error = ... }` e a tela escreve um erro para um
 * trabalho que já foi abandonado — e, no caso do `RunCatching` aninhado de
 * `verifyServer`, pula a restauração do endereço anterior, que é `suspend` e não
 * roda mais numa corrotina cancelada.
 *
 * Cancelamento não é falha de rede: quem cancela já sabe, e o `Result` era o
 * caminho errado para contar isso.
 */
internal inline fun <T> suspendRunCatching(block: () -> T): Result<T> = try {
    Result.success(block())
} catch (cancellation: CancellationException) {
    throw cancellation
} catch (error: Throwable) {
    Result.failure(error)
}
