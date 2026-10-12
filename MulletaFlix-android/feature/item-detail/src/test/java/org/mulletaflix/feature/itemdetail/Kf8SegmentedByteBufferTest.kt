package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertArrayEquals
import org.junit.Test

class Kf8SegmentedByteBufferTest {
    @Test
    fun `inserts fragments at byte offsets without changing existing bytes`() {
        val buffer = Kf8SegmentedByteBuffer("abcdef".encodeToByteArray())

        buffer.insert(2, "XY".encodeToByteArray())
        buffer.insert(6, "Z".encodeToByteArray())

        assertArrayEquals("abXYcdZef".encodeToByteArray(), buffer.toByteArray())
    }

    @Test
    fun `inserts a later fragment before earlier fragments at the same offset`() {
        val buffer = Kf8SegmentedByteBuffer("abcd".encodeToByteArray())

        buffer.insert(2, "first".encodeToByteArray())
        buffer.insert(2, "second".encodeToByteArray())

        assertArrayEquals("absecondfirstcd".encodeToByteArray(), buffer.toByteArray())
    }

    @Test
    fun `supports insertions at the beginning and end`() {
        val buffer = Kf8SegmentedByteBuffer("body".encodeToByteArray())

        buffer.insert(0, "start-".encodeToByteArray())
        buffer.insert(buffer.size, "-end".encodeToByteArray())

        assertArrayEquals("start-body-end".encodeToByteArray(), buffer.toByteArray())
    }
}
