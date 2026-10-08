using System.Collections.Generic;

namespace Adamantium.Fonts.Tables;

internal sealed class StandardBitmapTable : ColorBitmapTable
{
    private const int MaxDuplicates = 8;

    private readonly int glyphCount;
    private readonly int[] sizes;
    private readonly int[] strikes;

    private StandardBitmapTable(byte[] data, int glyphCount, int[] strikes, int[] sizes) : base(data)
    {
        this.glyphCount = glyphCount;
        this.strikes = strikes;
        this.sizes = sizes;
    }

    public override IReadOnlyList<int> Sizes => sizes;

    public static StandardBitmapTable Create(byte[] data, int glyphCount)
    {
        if (data == null || data.Length < 8 || glyphCount <= 0)
        {
            return null;
        }

        var count = (int)(data[4] << 24 | data[5] << 16 | data[6] << 8 | data[7]);
        if (count <= 0 || 8L + count * 4L > data.Length)
        {
            return null;
        }

        var strikes = new List<int>();
        var sizes = new List<int>();
        for (var i = 0; i < count; i++)
        {
            var at = data[8 + i * 4] << 24 | data[9 + i * 4] << 16 | data[10 + i * 4] << 8 | data[11 + i * 4];
            if (at < 0 || at + 4L + (glyphCount + 1) * 4L > data.Length || (data[at] << 8 | data[at + 1]) == 0)
            {
                continue;
            }

            strikes.Add(at);
            sizes.Add(data[at] << 8 | data[at + 1]);
        }

        return strikes.Count > 0 ? new StandardBitmapTable(data, glyphCount, strikes.ToArray(), sizes.ToArray()) : null;
    }

    protected override ColorBitmap ReadBitmap(uint glyphIndex, int pixelsPerEm)
    {
        var strike = ChooseStrike(pixelsPerEm);
        for (var duplicates = 0; duplicates < MaxDuplicates; duplicates++)
        {
            if (glyphIndex >= glyphCount)
            {
                return null;
            }

            var at = strikes[strike];
            var start = at + (long)UInt32(at + 4 + (int)glyphIndex * 4);
            var end = at + (long)UInt32(at + 8 + (int)glyphIndex * 4);
            if (end - start <= 8 || !Fits(start, end - start))
            {
                return null;
            }

            var tag = UInt32((int)start + 4);
            if (tag == Tag('d', 'u', 'p', 'e'))
            {
                if (end - start < 10)
                {
                    return null;
                }

                glyphIndex = UInt16((int)start + 8);
                continue;
            }

            if (tag != Tag('p', 'n', 'g', ' '))
            {
                return null;
            }

            var png = Slice(Data, start + 8, end - start - 8);
            if (!IsPng(png))
            {
                return null;
            }

            var left = Int16((int)start);
            var bottom = Int16((int)start + 2);
            var width = PngWidth(png);
            var height = PngHeight(png);
            return new ColorBitmap(png, sizes[strike], left, bottom + height, width, height);
        }

        return null;
    }

    private static uint Tag(char a, char b, char c, char d) => (uint)(a << 24 | b << 16 | c << 8 | d);
}
