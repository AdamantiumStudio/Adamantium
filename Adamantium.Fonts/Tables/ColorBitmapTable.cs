using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables;

internal abstract class ColorBitmapTable
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    protected ColorBitmapTable(byte[] data)
    {
        Data = data;
    }

    protected byte[] Data { get; }

    public abstract IReadOnlyList<int> Sizes { get; }

    public ColorBitmap GetBitmap(uint glyphIndex, int pixelsPerEm)
    {
        try
        {
            return ReadBitmap(glyphIndex, pixelsPerEm);
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    protected abstract ColorBitmap ReadBitmap(uint glyphIndex, int pixelsPerEm);

    protected int ChooseStrike(int pixelsPerEm)
    {
        var requested = pixelsPerEm > 0 ? pixelsPerEm : int.MaxValue;
        var best = 0;
        var bestSize = Sizes[0];
        for (var i = 1; i < Sizes.Count; i++)
        {
            var size = Sizes[i];
            if ((requested <= size && size < bestSize) || (requested > bestSize && size > bestSize))
            {
                best = i;
                bestSize = size;
            }
        }

        return best;
    }

    protected static byte[] Slice(byte[] source, long at, long length)
    {
        if (at < 0 || length <= 0 || at + length > source.Length)
        {
            return null;
        }

        var slice = new byte[length];
        System.Array.Copy(source, at, slice, 0, length);
        return slice;
    }

    protected bool Fits(long at, long bytes) => at >= 0 && bytes >= 0 && at + bytes <= Data.Length;

    protected static bool IsPng(byte[] image)
    {
        if (image == null || image.Length < 24)
        {
            return false;
        }

        for (var i = 0; i < PngSignature.Length; i++)
        {
            if (image[i] != PngSignature[i])
            {
                return false;
            }
        }

        return true;
    }

    protected static int PngWidth(byte[] png) => (int)ReadUInt32(png, 16);

    protected static int PngHeight(byte[] png) => (int)ReadUInt32(png, 20);

    protected byte UInt8(int at) => Data[at];

    protected sbyte Int8(int at) => (sbyte)Data[at];

    protected ushort UInt16(int at) => (ushort)(Data[at] << 8 | Data[at + 1]);

    protected short Int16(int at) => (short)UInt16(at);

    protected uint UInt32(int at) => ReadUInt32(Data, at);

    private static uint ReadUInt32(byte[] data, int at) =>
        (uint)(data[at] << 24 | data[at + 1] << 16 | data[at + 2] << 8 | data[at + 3]);
}
