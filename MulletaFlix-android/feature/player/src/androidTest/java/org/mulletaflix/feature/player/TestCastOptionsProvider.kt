package org.mulletaflix.feature.player

import android.content.Context
import com.google.android.gms.cast.CastMediaControlIntent
import com.google.android.gms.cast.framework.CastOptions
import com.google.android.gms.cast.framework.OptionsProvider
import com.google.android.gms.cast.framework.SessionProvider

/**
 * `OptionsProvider` de teste, equivalente ao `MulletaFlixCastOptionsProvider` do `:app`.
 *
 * Só existe para o APK de teste conseguir inicializar o Cast e assim **medir** o alvo de
 * toque do `MediaRouteButton` de verdade, em vez de reconstruir um botão parecido dentro
 * do teste — que é o antipadrão que esta suíte já rejeitou uma vez.
 */
class TestCastOptionsProvider : OptionsProvider {
    override fun getCastOptions(context: Context): CastOptions =
        CastOptions.Builder()
            .setReceiverApplicationId(CastMediaControlIntent.DEFAULT_MEDIA_RECEIVER_APPLICATION_ID)
            .build()

    override fun getAdditionalSessionProviders(context: Context): List<SessionProvider>? = null
}
