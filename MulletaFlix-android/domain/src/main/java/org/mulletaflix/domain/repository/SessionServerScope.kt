package org.mulletaflix.domain.repository

/**
 * Se a sessão guardada precisa ser descartada porque o servidor verificado é outro.
 *
 * O token, o `userId` e o endereço vivem em chaves independentes do mesmo
 * armazenamento, e o endereço é reescrito **antes** de a verificação terminar.
 * Sem esta decisão, apontar o app para um servidor B deixava o token do servidor
 * A no lugar: a partir dali o app acreditava estar autenticado e mandava a
 * credencial de A para B em toda requisição — capas, `/Users/Public`, progresso
 * de reprodução e Home.
 *
 * A identidade que decide é o `serverId` (o `Id` de `/System/Info/Public`), **não**
 * o endereço: o mesmo servidor é legitimamente alcançado por dois endereços (LAN
 * e DuckDNS) e trocar entre eles não pode deslogar ninguém — é justamente a troca
 * que o app faz sozinho quando o Wi-Fi cai.
 *
 * Só uma diferença **provada** derruba a sessão. Quando um dos lados não informa
 * identidade — instalação antiga que nunca gravou `SERVER_ID`, ou servidor que não
 * publica `Id` — a resposta é manter, porque deslogar por não conseguir provar é
 * pior que o risco que se quer evitar, e o próximo login grava a identidade.
 */
fun shouldClearSessionForServerChange(
    storedServerId: String?,
    verifiedServerId: String?,
): Boolean {
    val stored = storedServerId?.trim().orEmpty()
    val verified = verifiedServerId?.trim().orEmpty()
    if (stored.isEmpty() || verified.isEmpty()) return false
    return !stored.equals(verified, ignoreCase = true)
}
