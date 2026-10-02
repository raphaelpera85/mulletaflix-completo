package org.mulletaflix.domain.repository

/**
 * Se a sessão guardada precisa ser descartada porque o servidor verificado é outro.
 *
 * O token, o `userId` e o endereço vivem em chaves independentes do mesmo
 * armazenamento. Uma troca de endpoint não deve encaminhar credenciais antes de
 * confirmar a identidade do servidor destino.
 *
 * A identidade que decide é o `serverId` (o `Id` de `/System/Info/Public`), **não**
 * o endereço: o mesmo servidor é legitimamente alcançado por dois endereços (LAN
 * e DuckDNS) e trocar entre eles não pode deslogar ninguém — é justamente a troca
 * que o app faz sozinho quando o Wi-Fi cai.
 *
 * Uma diferença de identidade derruba a sessão. Se um dos lados não informa
 * identidade, a sessão também é descartada: nem mesmo o mesmo endereço prova que
 * o backend não foi substituído e portanto não autoriza reutilizar o token.
 */
fun shouldClearSessionForServerChange(
    storedServerId: String?,
    verifiedServerId: String?,
): Boolean {
    val stored = storedServerId?.trim().orEmpty()
    val verified = verifiedServerId?.trim().orEmpty()
    return stored.isEmpty() || verified.isEmpty() || !stored.equals(verified, ignoreCase = true)
}
