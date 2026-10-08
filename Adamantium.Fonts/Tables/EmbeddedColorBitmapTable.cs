using System.Collections.Generic;

namespace Adamantium.Fonts.Tables;

internal sealed class EmbeddedColorBitmapTable : ColorBitmapTable
{
    private const int SizeRecordLength = 48;

    private readonly byte[] images;
    private readonly int[] sizes;
    private readonly int[] strikes;

    private EmbeddedColorBitmapTable(byte[] locations, byte[] images, int[] strikes, int[] sizes) : base(locations)
    {
        this.images = images;
        this.strikes = strikes;
        this.sizes = sizes;
    }

    public override IReadOnlyList<int> Sizes => sizes;

    public static EmbeddedColorBitmapTable Create(byte[] locations, byte[] images)
    {
        if (locations == null || images == null || locations.Length < 8)
        {
            return null;
        }

        var count = (int)(locations[4] << 24 | locations[5] << 16 | locations[6] << 8 | locations[7]);
        if (count <= 0 || 8L + (long)count * SizeRecordLength > locations.Length)
        {
            return null;
        }

        var strikes = new int[count];
        var sizes = new int[count];
        for (var i = 0; i < count; i++)
        {
            var at = 8 + i * SizeRecordLength;
            strikes[i] = at;
            sizes[i] = System.Math.Max(locations[at + 44], locations[at + 45]);
        }

        return new EmbeddedColorBitmapTable(locations, images, strikes, sizes);
    }

    protected override ColorBitmap ReadBitmap(uint glyphIndex, int pixelsPerEm)
    {
        var strike = ChooseStrike(pixelsPerEm);
        var record = strikes[strike];
        var list = (int)UInt32(record);
        var subtables = (int)UInt32(record + 8);
        if (list < 0 || subtables < 0)
        {
            return null;
        }

        for (var i = 0; i < subtables; i++)
        {
            var entry = list + i * 8;
            if (entry + 8 > Data.Length)
            {
                return null;
            }

            var first = UInt16(entry);
            var last = UInt16(entry + 2);
            if (glyphIndex < first || glyphIndex > last)
            {
                continue;
            }

            var subtable = list + (long)UInt32(entry + 4);
            return subtable <= int.MaxValue ? ReadSubtable((int)subtable, glyphIndex, first, last, sizes[strike]) : null;
        }

        return null;
    }

    private ColorBitmap ReadSubtable(int at, uint glyphIndex, ushort first, ushort last, int pixelsPerEm)
    {
        if (at + 8 > Data.Length)
        {
            return null;
        }

        var indexFormat = UInt16(at);
        var imageFormat = UInt16(at + 2);
        long imageData = UInt32(at + 4);
        var index = (int)(glyphIndex - first);
        long offset;
        long length;
        var metrics = -1;
        switch (indexFormat)
        {
            case 1:
                if (!Fits(at + 8, (last - first + 2) * 4))
                {
                    return null;
                }

                offset = UInt32(at + 8 + index * 4);
                length = UInt32(at + 12 + index * 4) - offset;
                break;
            case 2:
            {
                if (!Fits(at + 8, 12))
                {
                    return null;
                }

                var imageSize = UInt32(at + 8);
                metrics = at + 12;
                offset = (long)imageSize * index;
                length = imageSize;
                break;
            }
            case 3:
                if (!Fits(at + 8, (last - first + 2) * 2))
                {
                    return null;
                }

                offset = UInt16(at + 8 + index * 2);
                length = UInt16(at + 10 + index * 2) - offset;
                break;
            case 4:
            {
                if (!Fits(at + 8, 4))
                {
                    return null;
                }

                var glyphs = (int)UInt32(at + 8);
                if (glyphs < 0 || !Fits(at + 12, (glyphs + 1) * 4))
                {
                    return null;
                }

                var found = FindSparse(at + 12, glyphs, glyphIndex, 4);
                if (found < 0)
                {
                    return null;
                }

                offset = UInt16(at + 12 + found * 4 + 2);
                length = UInt16(at + 12 + (found + 1) * 4 + 2) - offset;
                break;
            }
            case 5:
            {
                if (!Fits(at + 8, 16))
                {
                    return null;
                }

                var imageSize = UInt32(at + 8);
                metrics = at + 12;
                var glyphs = (int)UInt32(at + 20);
                if (glyphs < 0 || !Fits(at + 24, glyphs * 2))
                {
                    return null;
                }

                var found = FindSparse(at + 24, glyphs, glyphIndex, 2);
                if (found < 0)
                {
                    return null;
                }

                offset = (long)imageSize * found;
                length = imageSize;
                break;
            }
            default:
                return null;
        }

        return length > 0 ? ReadImage(imageFormat, imageData + offset, length, metrics, pixelsPerEm) : null;
    }

    private int FindSparse(int at, int count, uint glyphIndex, int stride)
    {
        var low = 0;
        var high = count - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            var glyph = UInt16(at + middle * stride);
            if (glyph < glyphIndex)
            {
                low = middle + 1;
            }
            else if (glyph > glyphIndex)
            {
                high = middle - 1;
            }
            else
            {
                return middle;
            }
        }

        return -1;
    }

    private ColorBitmap ReadImage(int format, long at, long length, int bigMetrics, int pixelsPerEm)
    {
        if (at < 0 || length < 12 || at + length > images.Length)
        {
            return null;
        }

        int height;
        int width;
        int left;
        int top;
        long png;
        switch (format)
        {
            case 17:
                height = images[at];
                width = images[at + 1];
                left = (sbyte)images[at + 2];
                top = (sbyte)images[at + 3];
                png = at + 9;
                break;
            case 18:
                height = images[at];
                width = images[at + 1];
                left = (sbyte)images[at + 2];
                top = (sbyte)images[at + 3];
                png = at + 12;
                break;
            case 19 when bigMetrics >= 0:
                height = UInt8(bigMetrics);
                width = UInt8(bigMetrics + 1);
                left = Int8(bigMetrics + 2);
                top = Int8(bigMetrics + 3);
                png = at + 4;
                break;
            default:
                return null;
        }

        var dataLength = (long)(images[png - 4] << 24 | images[png - 3] << 16 | images[png - 2] << 8 | images[png - 1]);
        var image = Slice(images, png, System.Math.Min(dataLength, at + length - png));
        return IsPng(image) ? new ColorBitmap(image, pixelsPerEm, left, top, width, height) : null;
    }
}
