package org.mulletaflix.core.common.update

import java.io.File

/**
 * Resultado de pedir ao sistema a instalação de um APK baixado.
 *
 * **Por que isto existe.** O laço "baixar → instalar" da atualização era mantido em
 * dois lugares: o [org.mulletaflix.android.update.AppUpdateViewModel] da checagem
 * automática da `MainActivity` e o `SettingsViewModel` do Centro de Atualizações. As
 * duas cópias classificavam a instalação do mesmo jeito — inclusive as duas mensagens
 * abaixo, palavra por palavra — e o dia em que a regra mudasse (uma mensagem, um
 * motivo novo de recusa) só uma delas mudaria. A política agora tem **uma** definição;
 * o que permanece nos ViewModels é só como cada tela publica o resultado (o aviso da
 * `MainActivity` fecha o diálogo, o Centro de Atualizações mostra um status).
 */
sealed interface AppUpdateInstallOutcome {

    /** O instalador do sistema abriu; o resto da conversa é com o usuário. */
    data object Started : AppUpdateInstallOutcome

    /**
     * O instalador respondeu `false`: falta a permissão de fontes desconhecidas e o
     * sistema já foi aberto na tela dela. Não é exceção; é a resposta esperada.
     */
    data object Rejected : AppUpdateInstallOutcome

    /** O instalador lançou exceção. [detail] é a mensagem técnica, quando existe. */
    data class Failed(val detail: String?) : AppUpdateInstallOutcome

    companion object {
        /** Mensagem visível quando a instalação é recusada por permissão. */
        const val PERMISSION_MESSAGE =
            "Permita a instalação de fontes desconhecidas e tente novamente."

        /** Mensagem visível quando o instalador lançou exceção sem detalhe. */
        const val FAILED_MESSAGE = "Não foi possível abrir o instalador do APK."
    }
}

/**
 * Executa [install] exatamente uma vez sobre [file] e classifica o resultado.
 *
 * [install] entra como parâmetro para o teste de JVM exercitar os três desfechos —
 * sucesso, recusa e exceção — sem Android e sem `Context`.
 */
fun installDownloadedApk(
    file: File,
    install: (File) -> Boolean,
): AppUpdateInstallOutcome =
    runCatching { install(file) }
        .fold(
            onSuccess = { started ->
                if (started) AppUpdateInstallOutcome.Started else AppUpdateInstallOutcome.Rejected
            },
            onFailure = { error -> AppUpdateInstallOutcome.Failed(error.localizedMessage) },
        )

/**
 * A mensagem que a tela expõe quando o instalador **não** abriu; `null` no sucesso.
 *
 * Recusa tem mensagem própria ([AppUpdateInstallOutcome.PERMISSION_MESSAGE]); exceção
 * preserva o detalhe técnico, se houver, com [AppUpdateInstallOutcome.FAILED_MESSAGE]
 * de reterro — uma exceção sem mensagem não pode chegar à tela como linha em branco.
 */
fun AppUpdateInstallOutcome.errorMessageOrNull(): String? = when (this) {
    AppUpdateInstallOutcome.Started -> null
    AppUpdateInstallOutcome.Rejected -> AppUpdateInstallOutcome.PERMISSION_MESSAGE
    is AppUpdateInstallOutcome.Failed -> detail ?: AppUpdateInstallOutcome.FAILED_MESSAGE
}
