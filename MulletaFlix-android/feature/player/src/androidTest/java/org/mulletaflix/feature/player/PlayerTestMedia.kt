package org.mulletaflix.feature.player

import android.content.Context
import java.io.File
import java.io.OutputStream

internal object PlayerTestMedia {
    fun createSilentWav(context: Context): File {
        val file = File.createTempFile("player-test-", ".wav", context.cacheDir)
        try {
            val sampleRate = 8_000
            val dataSize = sampleRate * 120
            file.outputStream().buffered().use { output ->
                output.write("RIFF".toByteArray(Charsets.US_ASCII))
                output.writeIntLittleEndian(dataSize + 36)
                output.write("WAVEfmt ".toByteArray(Charsets.US_ASCII))
                output.writeIntLittleEndian(16)
                output.writeShortLittleEndian(1)
                output.writeShortLittleEndian(1)
                output.writeIntLittleEndian(sampleRate)
                output.writeIntLittleEndian(sampleRate)
                output.writeShortLittleEndian(1)
                output.writeShortLittleEndian(8)
                output.write("data".toByteArray(Charsets.US_ASCII))
                output.writeIntLittleEndian(dataSize)

                val silence = ByteArray(8_192) { 0x80.toByte() }
                var remaining = dataSize
                while (remaining > 0) {
                    val chunkSize = minOf(remaining, silence.size)
                    output.write(silence, 0, chunkSize)
                    remaining -= chunkSize
                }
            }
            return file
        } catch (exception: Exception) {
            file.delete()
            throw exception
        }
    }

    private fun OutputStream.writeIntLittleEndian(value: Int) {
        write(value and 0xFF)
        write(value ushr 8 and 0xFF)
        write(value ushr 16 and 0xFF)
        write(value ushr 24 and 0xFF)
    }

    private fun OutputStream.writeShortLittleEndian(value: Int) {
        write(value and 0xFF)
        write(value ushr 8 and 0xFF)
    }
}
