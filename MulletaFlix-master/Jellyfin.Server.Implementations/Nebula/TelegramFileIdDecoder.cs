using System;
using System.IO;
using TL;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Decodificador de Telegram FileId (formato Pyrogram / Bot API) para MTProto InputDocumentFileLocation.
/// </summary>
public static class TelegramFileIdDecoder
{
    private const int FileReferenceFlag = 1 << 25;
    private const int WebLocationFlag = 1 << 24;

    /// <summary>
    /// Decodifica uma string file_id em um Document para download MTProto direto.
    /// </summary>
    public static Document? DecodeDocument(string? fileId)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return null;
        }

        try
        {
            var rawBytes = Base64UrlDecode(fileId);
            var rleDecoded = RleDecode(rawBytes);

            if (rleDecoded.Length < 10)
            {
                return null;
            }

            var major = rleDecoded[^1];
            var payload = major < 4 ? rleDecoded[..^1] : rleDecoded[..^2];

            using var ms = new MemoryStream(payload);
            using var reader = new BinaryReader(ms);

            var fileType = reader.ReadInt32();
            var dcId = reader.ReadInt32();

            var hasWebLocation = (fileType & WebLocationFlag) != 0;
            var hasFileReference = (fileType & FileReferenceFlag) != 0;

            if (hasWebLocation)
            {
                return null;
            }

            byte[] fileReference = Array.Empty<byte>();
            if (hasFileReference)
            {
                fileReference = ReadTelegramBytes(reader);
            }

            var mediaId = reader.ReadInt64();
            var accessHash = reader.ReadInt64();

            return new Document
            {
                id = mediaId,
                access_hash = accessHash,
                file_reference = fileReference,
                dc_id = dcId,
                mime_type = "application/octet-stream",
                attributes = Array.Empty<DocumentAttribute>()
            };
        }
        catch
        {
            return null;
        }
    }

    private static byte[] ReadTelegramBytes(BinaryReader reader)
    {
        var firstByte = reader.ReadByte();
        int length;
        int padding;

        if (firstByte <= 253)
        {
            length = firstByte;
            padding = -(length + 1) % 4;
        }
        else
        {
            var b1 = reader.ReadByte();
            var b2 = reader.ReadByte();
            var b3 = reader.ReadByte();
            length = b1 | (b2 << 8) | (b3 << 16);
            padding = -length % 4;
        }

        if (padding < 0)
        {
            padding += 4;
        }

        var data = reader.ReadBytes(length);
        if (padding > 0)
        {
            reader.ReadBytes(padding);
        }

        return data;
    }

    private static byte[] RleDecode(byte[] input)
    {
        using var ms = new MemoryStream();
        var zeroFlag = false;

        foreach (var b in input)
        {
            if (b == 0)
            {
                zeroFlag = true;
                continue;
            }

            if (zeroFlag)
            {
                for (var i = 0; i < b; i++)
                {
                    ms.WriteByte(0);
                }

                zeroFlag = false;
            }
            else
            {
                ms.WriteByte(b);
            }
        }

        return ms.ToArray();
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }

        return Convert.FromBase64String(output);
    }
}
