using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Adamantium.Fonts.Svg;

namespace Adamantium.Fonts.Tables;

internal sealed class SvgDocumentTable
{
    private const int RecordLength = 12;
    private const int MaxDocumentSize = 16 * 1024 * 1024;

    private readonly byte[] data;
    private readonly int list;
    private readonly int count;
    private readonly Dictionary<int, SvgDocument> documents = new();

    private SvgDocumentTable(byte[] data, int list, int count)
    {
        this.data = data;
        this.list = list;
        this.count = count;
    }

    public static SvgDocumentTable Create(byte[] data)
    {
        if (data == null || data.Length < 6)
        {
            return null;
        }

        var list = data[2] << 24 | data[3] << 16 | data[4] << 8 | data[5];
        if (list <= 0 || list + 2 > data.Length)
        {
            return null;
        }

        var count = data[list] << 8 | data[list + 1];
        return list + 2 + count * RecordLength <= data.Length ? new SvgDocumentTable(data, list, count) : null;
    }

    public SvgDocument GetDocument(uint glyphIndex)
    {
        var low = 0;
        var high = count - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            var record = list + 2 + middle * RecordLength;
            var start = data[record] << 8 | data[record + 1];
            var end = data[record + 2] << 8 | data[record + 3];
            if (glyphIndex < start)
            {
                high = middle - 1;
            }
            else if (glyphIndex > end)
            {
                low = middle + 1;
            }
            else
            {
                return Load(middle, record);
            }
        }

        return null;
    }

    private SvgDocument Load(int index, int record)
    {
        lock (documents)
        {
            if (documents.TryGetValue(index, out var known))
            {
                return known;
            }

            var offset = list + (data[record + 4] << 24 | data[record + 5] << 16 | data[record + 6] << 8 | data[record + 7]);
            var length = data[record + 8] << 24 | data[record + 9] << 16 | data[record + 10] << 8 | data[record + 11];
            var document = offset >= list && length > 0 && (long)offset + length <= data.Length
                ? Parse(data, offset, length)
                : null;
            documents[index] = document;
            return document;
        }
    }

    private static SvgDocument Parse(byte[] data, int offset, int length)
    {
        try
        {
            Stream stream = new MemoryStream(data, offset, length, false);
            if (length > 2 && data[offset] == 0x1F && data[offset + 1] == 0x8B)
            {
                stream = Decompress(stream);
                if (stream == null)
                {
                    return null;
                }
            }

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
                IgnoreComments = true,
            };
            using (stream)
            using (var reader = XmlReader.Create(stream, settings))
            {
                return new SvgDocument(XDocument.Load(reader));
            }
        }
        catch (Exception exception) when (exception is XmlException or InvalidDataException or IOException)
        {
            return null;
        }
    }

    private static Stream Decompress(Stream compressed)
    {
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        var result = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (result.Length + read > MaxDocumentSize)
            {
                return null;
            }

            result.Write(buffer, 0, read);
        }

        result.Position = 0;
        return result;
    }
}
