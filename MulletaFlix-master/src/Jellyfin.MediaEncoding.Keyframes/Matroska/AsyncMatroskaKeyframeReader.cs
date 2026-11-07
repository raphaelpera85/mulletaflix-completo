using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.MediaEncoding.Keyframes.Matroska.Models;

namespace MulletaFlix.MediaEncoding.Keyframes.Matroska;

internal static class AsyncMatroskaKeyframeReader
{
    public static async Task<KeyframeData> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException("Matroska input must be readable and seekable", nameof(stream));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var fileEnd = stream.Length;
        Element? segment = null;
        while (await ReadHeaderAsync(stream, fileEnd, true, cancellationToken).ConfigureAwait(false) is { } header)
        {
            if (header.Id == MatroskaConstants.SegmentContainer)
            {
                segment = header;
                break;
            }

            stream.Position = header.End;
        }

        if (segment is not { } segmentElement)
        {
            throw new InvalidDataException("Matroska Segment element was not found");
        }

        stream.Position = segmentElement.DataStart;
        Element? seekHead = null;
        while (await ReadHeaderAsync(stream, segmentElement.End, false, cancellationToken).ConfigureAwait(false) is { } header)
        {
            if (header.Id == MatroskaConstants.SeekHead)
            {
                seekHead = header;
                break;
            }

            stream.Position = header.End;
        }

        if (seekHead is not { } seekHeadElement)
        {
            throw new InvalidDataException("Matroska SeekHead element was not found");
        }

        var positions = await ReadSeekHeadAsync(stream, seekHeadElement, cancellationToken).ConfigureAwait(false);
        var info = await ReadInfoAsync(stream, AddPosition(segmentElement.DataStart, positions.Info), segmentElement.End, cancellationToken).ConfigureAwait(false);
        var videoTrack = await ReadVideoTrackAsync(stream, AddPosition(segmentElement.DataStart, positions.Tracks), segmentElement.End, cancellationToken).ConfigureAwait(false);
        var keyframes = await ReadCuesAsync(stream, AddPosition(segmentElement.DataStart, positions.Cues), segmentElement.End, videoTrack, info.TimestampScale, cancellationToken).ConfigureAwait(false);

        return new KeyframeData(ScaleToTicks(info.Duration ?? 0, info.TimestampScale), keyframes);
    }

    private static async Task<SeekPositions> ReadSeekHeadAsync(Stream stream, Element container, CancellationToken cancellationToken)
    {
        long? info = null;
        long? tracks = null;
        long? cues = null;
        stream.Position = container.DataStart;
        while (await ReadHeaderAsync(stream, container.End, false, cancellationToken).ConfigureAwait(false) is { } element)
        {
            if (element.Id == MatroskaConstants.Seek)
            {
                ulong? target = null;
                ulong? position = null;
                stream.Position = element.DataStart;
                while (await ReadHeaderAsync(stream, element.End, false, cancellationToken).ConfigureAwait(false) is { } field)
                {
                    if (field.Id == 0x53AB)
                    {
                        target = await ReadBinaryIdAsync(stream, field, cancellationToken).ConfigureAwait(false);
                    }
                    else if (field.Id == 0x53AC)
                    {
                        position = await ReadUIntAsync(stream, field, cancellationToken).ConfigureAwait(false);
                    }

                    stream.Position = field.End;
                }

                if (target.HasValue && position.HasValue && position.Value <= long.MaxValue)
                {
                    switch (target.Value)
                    {
                        case MatroskaConstants.Info: info = (long)position.Value; break;
                        case MatroskaConstants.Tracks: tracks = (long)position.Value; break;
                        case MatroskaConstants.Cues: cues = (long)position.Value; break;
                    }
                }
            }

            stream.Position = element.End;
            if (info.HasValue && tracks.HasValue && cues.HasValue)
            {
                break;
            }
        }

        if (info is null || tracks is null || cues is null)
        {
            throw new InvalidDataException("Matroska SeekHead does not reference Info, Tracks, and Cues");
        }

        return new SeekPositions(info.Value, tracks.Value, cues.Value);
    }

    private static async Task<Info> ReadInfoAsync(Stream stream, long position, long segmentEnd, CancellationToken cancellationToken)
    {
        var container = await ReadElementAtAsync(stream, position, segmentEnd, cancellationToken).ConfigureAwait(false);
        long? scale = null;
        double? duration = null;
        stream.Position = container.DataStart;
        while (await ReadHeaderAsync(stream, container.End, false, cancellationToken).ConfigureAwait(false) is { } element)
        {
            if (element.Id == MatroskaConstants.TimestampScale)
            {
                var value = await ReadUIntAsync(stream, element, cancellationToken).ConfigureAwait(false);
                if (value > long.MaxValue) throw new InvalidDataException("TimestampScale exceeds supported range");
                scale = (long)value;
            }
            else if (element.Id == MatroskaConstants.Duration)
            {
                duration = await ReadFloatAsync(stream, element, cancellationToken).ConfigureAwait(false);
            }

            stream.Position = element.End;
        }

        if (scale is null or 0)
        {
            throw new InvalidDataException("Matroska Info has no valid TimestampScale");
        }

        return new Info(scale.Value, duration);
    }

    private static async Task<ulong> ReadVideoTrackAsync(Stream stream, long position, long segmentEnd, CancellationToken cancellationToken)
    {
        var container = await ReadElementAtAsync(stream, position, segmentEnd, cancellationToken).ConfigureAwait(false);
        stream.Position = container.DataStart;
        while (await ReadHeaderAsync(stream, container.End, false, cancellationToken).ConfigureAwait(false) is { } entry)
        {
            if (entry.Id == MatroskaConstants.TrackEntry)
            {
                ulong? number = null;
                ulong? type = null;
                stream.Position = entry.DataStart;
                while (await ReadHeaderAsync(stream, entry.End, false, cancellationToken).ConfigureAwait(false) is { } field)
                {
                    if (field.Id == MatroskaConstants.TrackNumber)
                    {
                        number = await ReadUIntAsync(stream, field, cancellationToken).ConfigureAwait(false);
                    }
                    else if (field.Id == MatroskaConstants.TrackType)
                    {
                        type = await ReadUIntAsync(stream, field, cancellationToken).ConfigureAwait(false);
                    }

                    stream.Position = field.End;
                }

                if (type == MatroskaConstants.TrackTypeVideo && number is > 0)
                {
                    return number.Value;
                }
            }

            stream.Position = entry.End;
        }

        throw new InvalidDataException("Matroska file has no video track");
    }

    private static async Task<List<long>> ReadCuesAsync(Stream stream, long position, long segmentEnd, ulong videoTrack, long timestampScale, CancellationToken cancellationToken)
    {
        var container = await ReadElementAtAsync(stream, position, segmentEnd, cancellationToken).ConfigureAwait(false);
        var keyframes = new List<long>();
        stream.Position = container.DataStart;
        while (await ReadHeaderAsync(stream, container.End, false, cancellationToken).ConfigureAwait(false) is { } point)
        {
            if (point.Id == MatroskaConstants.CuePoint)
            {
                ulong? cueTime = null;
                var hasVideoTrackPosition = false;
                stream.Position = point.DataStart;
                while (await ReadHeaderAsync(stream, point.End, false, cancellationToken).ConfigureAwait(false) is { } field)
                {
                    if (field.Id == MatroskaConstants.CueTime)
                    {
                        cueTime = await ReadUIntAsync(stream, field, cancellationToken).ConfigureAwait(false);
                    }
                    else if (field.Id == MatroskaConstants.CueTrackPositions)
                    {
                        stream.Position = field.DataStart;
                        while (await ReadHeaderAsync(stream, field.End, false, cancellationToken).ConfigureAwait(false) is { } trackField)
                        {
                            if (trackField.Id == MatroskaConstants.CuePointTrackNumber)
                            {
                                hasVideoTrackPosition |= await ReadUIntAsync(stream, trackField, cancellationToken).ConfigureAwait(false) == videoTrack;
                                break;
                            }

                            stream.Position = trackField.End;
                        }
                    }

                    stream.Position = field.End;
                }

                if (cueTime.HasValue && hasVideoTrackPosition)
                {
                    keyframes.Add(ScaleToTicks(cueTime.Value, timestampScale));
                }
            }

            stream.Position = point.End;
        }

        return keyframes;
    }

    private static async Task<Element> ReadElementAtAsync(Stream stream, long position, long parentEnd, CancellationToken cancellationToken)
    {
        if (position < 0 || position >= parentEnd)
        {
            throw new InvalidDataException("Matroska element position is outside its parent");
        }

        stream.Position = position;
        return await ReadHeaderAsync(stream, parentEnd, false, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Matroska element is missing at the indexed position");
    }

    private static async Task<Element?> ReadHeaderAsync(Stream stream, long parentEnd, bool allowUnknownSize, CancellationToken cancellationToken)
    {
        if (stream.Position == parentEnd)
        {
            return null;
        }

        if (stream.Position < 0 || stream.Position > parentEnd)
        {
            throw new InvalidDataException("Matroska element exceeds its parent");
        }

        var singleByte = new byte[1];
        var idFirst = await ReadByteAsync(stream, singleByte, cancellationToken).ConfigureAwait(false);
        var idLength = VintLength(idFirst, 4);
        ulong id = idFirst;
        for (var i = 1; i < idLength; i++)
        {
            id = (id << 8) | await ReadByteAsync(stream, singleByte, cancellationToken).ConfigureAwait(false);
        }

        var sizeFirst = await ReadByteAsync(stream, singleByte, cancellationToken).ConfigureAwait(false);
        var sizeLength = VintLength(sizeFirst, 8);
        var marker = (byte)(0x80 >> (sizeLength - 1));
        ulong size = (ulong)(sizeFirst & (marker - 1));
        for (var i = 1; i < sizeLength; i++)
        {
            size = (size << 8) | await ReadByteAsync(stream, singleByte, cancellationToken).ConfigureAwait(false);
        }

        if (stream.Position > parentEnd)
        {
            throw new InvalidDataException("Matroska element header exceeds its parent");
        }

        var unknownSize = size == ((1UL << (7 * sizeLength)) - 1);
        if (unknownSize)
        {
            if (!allowUnknownSize) throw new InvalidDataException("Unexpected unknown-sized Matroska element");
            return new Element(id, stream.Position, parentEnd);
        }

        if (size > long.MaxValue || stream.Position > parentEnd - (long)size)
        {
            throw new InvalidDataException("Matroska element size exceeds its parent");
        }

        return new Element(id, stream.Position, stream.Position + (long)size);
    }

    private static async Task<byte> ReadByteAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer[0];
    }

    private static int VintLength(byte first, int maximum)
    {
        for (var length = 1; length <= maximum; length++)
        {
            if ((first & (0x80 >> (length - 1))) != 0) return length;
        }

        throw new InvalidDataException("Invalid EBML variable-length integer");
    }

    private static async Task<ulong> ReadUIntAsync(Stream stream, Element element, CancellationToken cancellationToken)
    {
        var length = element.End - element.DataStart;
        if (length is < 1 or > 8) throw new InvalidDataException("Invalid Matroska unsigned integer width");
        var buffer = new byte[(int)length];
        stream.Position = element.DataStart;
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        ulong value = 0;
        foreach (var item in buffer) value = (value << 8) | item;
        return value;
    }

    private static async Task<ulong> ReadBinaryIdAsync(Stream stream, Element element, CancellationToken cancellationToken)
    {
        var length = element.End - element.DataStart;
        if (length is < 1 or > 4) throw new InvalidDataException("Invalid Matroska SeekID width");
        var buffer = new byte[(int)length];
        stream.Position = element.DataStart;
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        ulong value = 0;
        foreach (var item in buffer) value = (value << 8) | item;
        return value;
    }

    private static async Task<double> ReadFloatAsync(Stream stream, Element element, CancellationToken cancellationToken)
    {
        var length = element.End - element.DataStart;
        if (length is not (4 or 8)) throw new InvalidDataException("Invalid Matroska floating-point width");
        var buffer = new byte[(int)length];
        stream.Position = element.DataStart;
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        var value = length == 4
            ? BinaryPrimitives.ReadSingleBigEndian(buffer)
            : BinaryPrimitives.ReadDoubleBigEndian(buffer);
        if (!double.IsFinite(value)) throw new InvalidDataException("Matroska duration is not finite");
        return value;
    }

    private static long AddPosition(long segmentStart, long relativePosition)
    {
        try { return checked(segmentStart + relativePosition); }
        catch (OverflowException ex) { throw new InvalidDataException("Matroska SeekPosition overflows", ex); }
    }

    private static long ScaleToTicks(ulong value, long timestampScale)
    {
        try
        {
            var ticks = (decimal)value * timestampScale / 100;
            return ticks > long.MaxValue ? throw new InvalidDataException("Matroska timestamp exceeds supported range") : (long)ticks;
        }
        catch (OverflowException ex)
        {
            throw new InvalidDataException("Matroska timestamp exceeds supported range", ex);
        }
    }

    private static long ScaleToTicks(double value, long timestampScale)
    {
        var ticks = value * timestampScale / 100;
        return !double.IsFinite(ticks) || ticks > long.MaxValue || ticks < long.MinValue
            ? throw new InvalidDataException("Matroska duration exceeds supported range")
            : Convert.ToInt64(ticks);
    }

    private readonly record struct Element(ulong Id, long DataStart, long End);
    private readonly record struct SeekPositions(long Info, long Tracks, long Cues);
}
