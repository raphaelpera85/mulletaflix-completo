package org.mulletaflix.data.repository

import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.nio.ByteBuffer
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/** Encrypts the session token with a non-exportable key held by Android Keystore. */
internal object SessionTokenCrypto {
    private const val KEY_ALIAS = "mulletaflix.session-token.v1"
    private const val TRANSFORMATION = "AES/GCM/NoPadding"
    private const val VERSION_PREFIX = "v1:"
    private const val GCM_TAG_BITS = 128
    private const val IV_BYTES = 12
    private val keyLock = Any()
    private val associatedData = "org.mulletaflix/access-token/v1".toByteArray(Charsets.UTF_8)

    fun encrypt(token: String): String {
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.ENCRYPT_MODE, getOrCreateKey())
        cipher.updateAAD(associatedData)
        val encrypted = cipher.doFinal(token.toByteArray(Charsets.UTF_8))
        val payload = ByteBuffer.allocate(cipher.iv.size + encrypted.size)
            .put(cipher.iv)
            .put(encrypted)
            .array()
        return VERSION_PREFIX + Base64.encodeToString(payload, Base64.NO_WRAP)
    }

    fun decryptOrNull(value: String): String? = try {
        if (!value.startsWith(VERSION_PREFIX)) return null
        val payload = Base64.decode(value.removePrefix(VERSION_PREFIX), Base64.NO_WRAP)
        if (payload.size <= IV_BYTES + GCM_TAG_BITS / 8) return null
        val buffer = ByteBuffer.wrap(payload)
        val iv = ByteArray(IV_BYTES).also(buffer::get)
        val ciphertext = ByteArray(buffer.remaining()).also(buffer::get)
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.DECRYPT_MODE, getOrCreateKey(), GCMParameterSpec(GCM_TAG_BITS, iv))
        cipher.updateAAD(associatedData)
        cipher.doFinal(ciphertext).toString(Charsets.UTF_8)
    } catch (_: Exception) {
        null
    }

    private fun getOrCreateKey(): SecretKey = synchronized(keyLock) {
        val keyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (keyStore.getKey(KEY_ALIAS, null) as? SecretKey) ?: KeyGenerator
            .getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
            .apply {
                init(
                    KeyGenParameterSpec.Builder(
                        KEY_ALIAS,
                        KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT,
                    )
                        .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                        .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                        .setKeySize(256)
                        .setRandomizedEncryptionRequired(true)
                        .build(),
                )
            }
            .generateKey()
    }
}
