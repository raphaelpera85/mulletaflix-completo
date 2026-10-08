package org.mulletaflix.feature.itemdetail

import java.io.File
import java.util.Base64

/** Minimal two-page JPEG/PNG CBR fixture from ssokolow/rar-test-files, released under CC0. */
internal object CbrTestArchive {
    private const val FIXTURE_BASE64 =
        "UmFyIRoHAM+QcwAADQAAAAAAAAAMJXQggCwAtgAAANwAAAAAbLFw2gAAISodNQwAIAAAAHRlc3RmaWxlLmpwZ+cYFf7V/ydkeNQh1MKmm6OAexVPlvVHrzqPE5mVHC08ghs85LfafCcldrlGYJPnjkgNzK9t4fZEpePKwmfeq9nqNRVssG8auWw3ppmChio3P4QobbIIu+aDvAnlNhtw7eU/yPyMBuPEssZjwTehsh4DZgC5HXWGFUVxgDrn+6KnVDXP/2B26ds102b/eZa5elob/BycnuXvN92AXobBxHJ4Ebq+7rCITbK7Lz6UAAC/iGf2qf/UUW50IIAsAFQAAABXAAAAAGKssK8AACEqHTUMACAAAAB0ZXN0ZmlsZS5wbmenGIjF+7VC0fPe1feyyXAlT4G/SVtSdAyd7pMHsE3FAkIqbrYBRgyQp7m1pxzv+HOHpfwCaeA8jA41QCxmUCvsyGsqR5gONgQCwAAAAL+IZ/ap/9TEPXsAQAcA"

    fun bytes(): ByteArray = Base64.getDecoder().decode(FIXTURE_BASE64)

    fun writeTo(file: File): File = file.apply { writeBytes(bytes()) }
}
