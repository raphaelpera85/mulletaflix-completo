package org.mulletaflix.feature.player

/**
 * User-facing label for the Cast action, shown beside the Media3 button.
 *
 * O app **não** define mais uma contentDescription para esse controle: a frase
 * fixa em pt-BR competia com este rótulo visível e com a descrição localizada que o
 * MediaRouteButton do Media3 já publica (que também conhece o estado da conexão).
 */
internal fun castActionLabel(isCasting: Boolean): String =
    if (isCasting) "Transmitindo" else "Transmitir"