package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PdfOcrAvailabilityTest {
    @Test
    fun `local bundled OCR enables PDF text features across supported Android versions`() {
        assertFalse(supportsPdfTextExtraction(23, 0))
        for (sdk in 24..37) {
            assertTrue("PDF text features should be available on API $sdk", supportsPdfTextExtraction(sdk, 0))
        }
    }
}
