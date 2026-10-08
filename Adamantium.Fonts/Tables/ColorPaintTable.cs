using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Fonts.Common;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Tables;

internal sealed class ColorPaintTable
{
    private const ushort ForegroundEntry = 0xFFFF;
    private const byte SourceIn = 5;
    private const int MaxDepth = 64;
    private const int MaxVisits = 65536;
    private const int NoClip = -1;

    private readonly byte[] data;
    private readonly int baseGlyphList;
    private readonly int layerList;

    private ColorPaintTable(byte[] data, int baseGlyphList, int layerList)
    {
        this.data = data;
        this.baseGlyphList = baseGlyphList;
        this.layerList = layerList;
    }

    public static ColorPaintTable Read(FontStreamReader reader, long offset, long length)
    {
        reader.Position = offset;
        var version = reader.ReadUInt16();
        if (version < 1)
        {
            return null;
        }

        reader.Position = offset + 14;
        var baseGlyphList = reader.ReadUInt32();
        var layerList = reader.ReadUInt32();
        if (baseGlyphList == 0 || baseGlyphList + 4 > length || layerList > length)
        {
            return null;
        }

        var data = new byte[length];
        reader.Position = offset;
        var read = 0;
        while (read < length)
        {
            var chunk = reader.Read(data, read, data.Length - read);
            if (chunk == 0)
            {
                return null;
            }

            read += chunk;
        }

        return new ColorPaintTable(data, (int)baseGlyphList, (int)layerList);
    }

    public ColorPaintLayer[] GetLayers(uint glyphIndex, ColorPaletteTable palettes, int palette)
    {
        try
        {
            var paint = FindPaint(glyphIndex);
            if (paint < 0)
            {
                return [];
            }

            var context = new Context(palettes, palette);
            context.Visiting.Add(glyphIndex);
            Paint(paint, Matrix3x2.Identity, NoClip, Matrix3x2.Identity, context, 0);
            return context.Layers.ToArray();
        }
        catch (IndexOutOfRangeException)
        {
            return [];
        }
    }

    private int FindPaint(uint glyphIndex)
    {
        var count = (int)UInt32(baseGlyphList);
        var low = 0;
        var high = count - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            var record = baseGlyphList + 4 + middle * 6;
            var glyph = UInt16(record);
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
                return baseGlyphList + (int)UInt32(record + 2);
            }
        }

        return -1;
    }

    private void Paint(int at, Matrix3x2 transform, int clip, Matrix3x2 mask, Context context, int depth)
    {
        if (depth > MaxDepth || ++context.Visits > MaxVisits)
        {
            return;
        }

        var format = data[at];
        switch (format)
        {
            case 1:
                var count = data[at + 1];
                var first = (int)UInt32(at + 2);
                for (var i = 0; i < count; i++)
                {
                    var layer = layerList + (int)UInt32(layerList + 4 + (first + i) * 4);
                    Paint(layer, transform, clip, mask, context, depth + 1);
                }

                break;
            case >= 2 and <= 9:
                if (clip != NoClip)
                {
                    context.Layers.Add(new ColorPaintLayer((uint)clip, mask, Fill(at, transform, context), 1));
                }

                break;
            case 10:
                Paint(Child(at), transform, UInt16(at + 4), transform, context, depth + 1);
                break;
            case 11:
                var glyph = UInt16(at + 1);
                var paint = FindPaint(glyph);
                if (paint >= 0 && context.Visiting.Add(glyph))
                {
                    Paint(paint, transform, clip, mask, context, depth + 1);
                    context.Visiting.Remove(glyph);
                }

                break;
            case >= 12 and <= 31:
                Paint(Child(at), Matrix3x2.Multiply(Transform(at), transform), clip, mask, context, depth + 1);
                break;
            case 32:
                Composite(at, transform, clip, mask, context, depth);
                break;
        }
    }

    private void Composite(int at, Matrix3x2 transform, int clip, Matrix3x2 mask, Context context, int depth)
    {
        var source = Child(at);
        var mode = data[at + 4];
        var backdrop = at + (int)UInt24(at + 5);
        if (mode != SourceIn)
        {
            Paint(backdrop, transform, clip, mask, context, depth + 1);
            Paint(source, transform, clip, mask, context, depth + 1);
            return;
        }

        var start = context.Layers.Count;
        Paint(source, transform, clip, mask, context, depth + 1);
        var opacity = clip == NoClip ? PlaneAlpha(backdrop, context, depth + 1) : 1;
        for (var i = start; i < context.Layers.Count; i++)
        {
            var layer = context.Layers[i];
            context.Layers[i] = new ColorPaintLayer(layer.GlyphIndex, layer.Transform, layer.Fill, layer.Opacity * opacity);
        }
    }

    private float PlaneAlpha(int at, Context context, int depth)
    {
        if (depth > MaxDepth)
        {
            return 1;
        }

        var format = data[at];
        if (format is >= 12 and <= 31)
        {
            return PlaneAlpha(Child(at), context, depth + 1);
        }

        if (format is not (2 or 3))
        {
            return 1;
        }

        var stop = Stop(0, UInt16(at + 1), F2Dot14(at + 3), context);
        return stop.Color is { } color ? color.A / 255f : stop.Alpha;
    }

    private ColorFill Fill(int at, Matrix3x2 transform, Context context)
    {
        var format = data[at];
        switch (format)
        {
            case 2 or 3:
                return new ColorFill(ColorFillKind.Solid, [Stop(0, UInt16(at + 1), F2Dot14(at + 3), context)],
                    ColorExtend.Pad, transform);
            case 4 or 5:
                return new ColorFill(ColorFillKind.LinearGradient, Stops(Child(at), format == 5, context),
                    Extend(Child(at)), transform)
                {
                    Point0 = new Vector2(Int16(at + 4), Int16(at + 6)),
                    Point1 = new Vector2(Int16(at + 8), Int16(at + 10)),
                    Point2 = new Vector2(Int16(at + 12), Int16(at + 14)),
                };
            case 8 or 9:
                return new ColorFill(ColorFillKind.SweepGradient, Stops(Child(at), format == 9, context),
                    Extend(Child(at)), transform)
                {
                    Point0 = new Vector2(Int16(at + 4), Int16(at + 6)),
                    StartAngle = (F2Dot14(at + 8) + 1) * 180,
                    EndAngle = (F2Dot14(at + 10) + 1) * 180,
                };
            default:
                return new ColorFill(ColorFillKind.RadialGradient, Stops(Child(at), format == 7, context),
                    Extend(Child(at)), transform)
                {
                    Point0 = new Vector2(Int16(at + 4), Int16(at + 6)),
                    Radius0 = UInt16(at + 8),
                    Point1 = new Vector2(Int16(at + 10), Int16(at + 12)),
                    Radius1 = UInt16(at + 14),
                };
        }
    }

    private Matrix3x2 Transform(int at)
    {
        var format = data[at];
        switch (format)
        {
            case 12 or 13:
                var affine = at + (int)UInt24(at + 4);
                return new Matrix3x2(Fixed(affine), Fixed(affine + 4), Fixed(affine + 8), Fixed(affine + 12),
                    Fixed(affine + 16), Fixed(affine + 20));
            case 14 or 15:
                return Matrix3x2.Translation(Int16(at + 4), Int16(at + 6));
            case 16 or 17:
                return Matrix3x2.Scaling(F2Dot14(at + 4), F2Dot14(at + 6));
            case 18 or 19:
                return AroundCenter(Matrix3x2.Scaling(F2Dot14(at + 4), F2Dot14(at + 6)), at + 8);
            case 20 or 21:
                return Matrix3x2.Scaling(F2Dot14(at + 4));
            case 22 or 23:
                return AroundCenter(Matrix3x2.Scaling(F2Dot14(at + 4)), at + 6);
            case 24 or 25:
                return Rotation(F2Dot14(at + 4));
            case 26 or 27:
                return AroundCenter(Rotation(F2Dot14(at + 4)), at + 6);
            case 28 or 29:
                return Skew(F2Dot14(at + 4), F2Dot14(at + 6));
            default:
                return AroundCenter(Skew(F2Dot14(at + 4), F2Dot14(at + 6)), at + 8);
        }
    }

    private Matrix3x2 AroundCenter(Matrix3x2 transform, int center)
    {
        var x = Int16(center);
        var y = Int16(center + 2);
        return Matrix3x2.Multiply(Matrix3x2.Multiply(Matrix3x2.Translation(-x, -y), transform),
            Matrix3x2.Translation(x, y));
    }

    private static Matrix3x2 Rotation(double halfTurns)
    {
        var cos = Math.Cos(halfTurns * Math.PI);
        var sin = Math.Sin(halfTurns * Math.PI);
        return new Matrix3x2(cos, sin, -sin, cos, 0, 0);
    }

    private static Matrix3x2 Skew(double x, double y)
    {
        return new Matrix3x2(1, Math.Tan(y * Math.PI), -Math.Tan(x * Math.PI), 1, 0, 0);
    }

    private ColorExtend Extend(int colorLine)
    {
        return data[colorLine] switch
        {
            1 => ColorExtend.Repeat,
            2 => ColorExtend.Reflect,
            _ => ColorExtend.Pad,
        };
    }

    private ColorStop[] Stops(int colorLine, bool variable, Context context)
    {
        var count = UInt16(colorLine + 1);
        var size = variable ? 10 : 6;
        var stops = new ColorStop[count];
        for (var i = 0; i < count; i++)
        {
            var stop = colorLine + 3 + i * size;
            stops[i] = Stop(F2Dot14(stop), UInt16(stop + 2), F2Dot14(stop + 4), context);
        }

        return stops.OrderBy(s => s.Offset).ToArray();
    }

    private static ColorStop Stop(float offset, ushort entry, float alpha, Context context)
    {
        if (entry == ForegroundEntry)
        {
            return new ColorStop(offset, null, alpha);
        }

        var color = context.Palettes?.GetColor(context.Palette, entry);
        if (color is not { } found)
        {
            return new ColorStop(offset, null, alpha);
        }

        found.A = (byte)Math.Max(0, Math.Min(255, Math.Round(found.A * alpha)));
        return new ColorStop(offset, found, alpha);
    }

    private int Child(int at) => at + (int)UInt24(at + 1);

    private ushort UInt16(int at) => (ushort)(data[at] << 8 | data[at + 1]);

    private short Int16(int at) => (short)UInt16(at);

    private uint UInt24(int at) => (uint)(data[at] << 16 | data[at + 1] << 8 | data[at + 2]);

    private uint UInt32(int at) => (uint)(data[at] << 24 | data[at + 1] << 16 | data[at + 2] << 8 | data[at + 3]);

    private float F2Dot14(int at) => Int16(at) / 16384f;

    private double Fixed(int at) => (int)UInt32(at) / 65536.0;

    private sealed class Context
    {
        public Context(ColorPaletteTable palettes, int palette)
        {
            Palettes = palettes;
            Palette = palette;
        }

        public ColorPaletteTable Palettes { get; }

        public int Palette { get; }

        public List<ColorPaintLayer> Layers { get; } = [];

        public HashSet<uint> Visiting { get; } = [];

        public int Visits { get; set; }
    }
}
