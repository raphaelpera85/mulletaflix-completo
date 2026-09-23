package org.mulletaflix.feature.player

import androidx.activity.ComponentActivity
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.test.platform.app.InstrumentationRegistry
import com.google.android.gms.cast.framework.CastContext
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * O alvo de toque do controle de transmissão, **medido no aparelho**.
 *
 * Era a última pendência da lista de acessibilidade da v1.2.73, registrada duas vezes
 * como "precisa de medição no aparelho, não de um palpite": o `MediaRouteButton` do
 * Media3 recebia `Modifier.size(40.dp)` enquanto os `IconButton` ao lado reservam 48 dp
 * pelo `minimumInteractiveComponentSize` do Material3.
 *
 * Três paredes apareceram antes desta funcionar, e ficam registradas porque qualquer
 * tentativa futura bate nas mesmas:
 *
 *  1. `MediaRouteButton` **não compõe** sem o Cast SDK inicializado. O `:app` resolve
 *     isso em `MulletaFlixApp.onCreate`, com o `OPTIONS_PROVIDER_CLASS_NAME` no
 *     manifesto — daí o `TestCastOptionsProvider` e o manifesto de androidTest deste
 *     módulo.
 *  2. `CastContext.getSharedInstance` só pode ser chamado na thread principal.
 *  3. **A regra de teste importa.** Com `createComposeRule` (a variante `v2`), a
 *     composição roda no dispatcher do teste e o serviço de media router recusa:
 *     *"The media router service must only be accessed on the application's main
 *     thread"*. `createAndroidComposeRule<ComponentActivity>()` compõe na thread da
 *     Activity de verdade, e é o que faz a medição acontecer.
 *
 * O teste mede `boundsInRoot` do nó — que é o que o hit-test usa — e, se estiver abaixo
 * do mínimo, **diz o número medido** na mensagem, para o valor vir do dispositivo.
 */
class PlayerCastControlTouchTargetTest {

    @get:Rule
    val composeRule = createAndroidComposeRule<ComponentActivity>()

    @Before
    fun initializeCast() {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        instrumentation.runOnMainSync {
            CastContext.getSharedInstance(instrumentation.targetContext)
        }
    }

    @Test
    fun castControlTouchTargetIsAtLeastTheMinimum() {
        composeRule.setContent {
            MaterialTheme {
                PlayerCastControl(isCasting = false)
            }
        }

        val node = composeRule.onNodeWithTag(PLAYER_CAST_CONTROL_TEST_TAG).fetchSemanticsNode()
        val density = composeRule.density
        val layoutWidthDp = with(density) { node.boundsInRoot.width.toDp().value }
        val layoutHeightDp = with(density) { node.boundsInRoot.height.toDp().value }
        val touchWidthDp = with(density) { node.touchBoundsInRoot.width.toDp().value }
        val touchHeightDp = with(density) { node.touchBoundsInRoot.height.toDp().value }

        // As duas medidas vão para a mensagem: **layout e alvo de toque não são a mesma
        // coisa**, e foi medindo as duas que a diferença apareceu nesta rodada — o
        // layout é 40 dp e o alvo de toque já passa de 48 dp, porque o Compose expande o
        // alvo mínimo sozinho. Medir só o layout teria "provado" um defeito inexistente.
        assertTrue(
            "alvo de toque medido no aparelho: ${touchWidthDp}dp x ${touchHeightDp}dp " +
                "(layout: ${layoutWidthDp}dp x ${layoutHeightDp}dp; mínimo 48dp)",
            touchWidthDp >= 48f && touchHeightDp >= 48f,
        )
    }
}
