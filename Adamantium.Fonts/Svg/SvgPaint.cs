using System;
using System.Globalization;
using Adamantium.Fonts.Tables;
using Adamantium.Mathematics;
using Adamantium.Mathematics.Svg;

namespace Adamantium.Fonts.Svg;

internal readonly struct SvgPaint
{
    private SvgPaint(SvgPaintKind kind, Color color, string reference, string fallback)
    {
        Kind = kind;
        Color = color;
        Reference = reference;
        Fallback = fallback;
    }

    public SvgPaintKind Kind { get; }

    public Color Color { get; }

    public string Reference { get; }

    public string Fallback { get; }

    public static SvgPaint None => new(SvgPaintKind.None, default, null, null);

    public static SvgPaint Black => new(SvgPaintKind.Color, Colors.Black, null, null);

    public static SvgPaint Parse(string text, ColorPaletteTable palettes, int palette)
    {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text) || text == "none")
        {
            return None;
        }

        if (text == "currentColor")
        {
            return new SvgPaint(SvgPaintKind.CurrentColor, default, null, null);
        }

        if (text.StartsWith("url(", StringComparison.Ordinal))
        {
            var close = text.IndexOf(')');
            return close < 0
                ? None
                : new SvgPaint(SvgPaintKind.Reference, default, text.Substring(0, close + 1), text.Substring(close + 1).Trim());
        }

        return TryParseColor(text, palettes, palette, out var color)
            ? new SvgPaint(SvgPaintKind.Color, color, null, null)
            : None;
    }

    public static bool TryParseColor(string text, ColorPaletteTable palettes, int palette, out Color color)
    {
        color = default;
        text = text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (text.StartsWith("var(", StringComparison.Ordinal))
        {
            return TryParseVariable(text, palettes, palette, out color);
        }

        if (text[0] == '#')
        {
            return TryParseHex(text.Substring(1), out color);
        }

        if (text.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseRgb(text, out color);
        }

        if (text == "transparent")
        {
            color = Color.FromRgba(0, 0, 0, 0);
            return true;
        }

        return Colors.TryGetNamed(text, out color);
    }

    private static bool TryParseVariable(string text, ColorPaletteTable palettes, int palette, out Color color)
    {
        color = default;
        var close = text.LastIndexOf(')');
        if (close < 4)
        {
            return false;
        }

        var inside = text.Substring(4, close - 4);
        var comma = inside.IndexOf(',');
        var name = (comma < 0 ? inside : inside.Substring(0, comma)).Trim();
        if (name.StartsWith("--color", StringComparison.Ordinal) &&
            int.TryParse(name.Substring(7), NumberStyles.None, CultureInfo.InvariantCulture, out var entry) &&
            palettes?.GetColor(palette, entry) is { } found)
        {
            color = found;
            return true;
        }

        return comma >= 0 && TryParseColor(inside.Substring(comma + 1), palettes, palette, out color);
    }

    private static bool TryParseHex(string hex, out Color color)
    {
        color = default;
        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        switch (hex.Length)
        {
            case 3:
            case 4:
                var r = (byte)(Nibble(hex, 0) * 17);
                var g = (byte)(Nibble(hex, 1) * 17);
                var b = (byte)(Nibble(hex, 2) * 17);
                var a = hex.Length == 4 ? (byte)(Nibble(hex, 3) * 17) : (byte)255;
                color = Color.FromRgba(r, g, b, a);
                return true;
            case 6:
                color = Color.FromRgba((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
                return true;
            case 8:
                color = Color.FromRgba((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
                return true;
            default:
                return false;
        }
    }

    private static int Nibble(string hex, int at) =>
        int.Parse(hex.Substring(at, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static bool TryParseRgb(string text, out Color color)
    {
        color = default;
        var open = text.IndexOf('(');
        var close = text.LastIndexOf(')');
        if (open < 0 || close < open)
        {
            return false;
        }

        var parts = text.Substring(open + 1, close - open - 1).Split(',', ' ', '/');
        var channels = new double[4];
        var count = 0;
        foreach (var part in parts)
        {
            var value = part.Trim();
            if (value.Length == 0 || count == 4)
            {
                continue;
            }

            var percent = value.EndsWith("%", StringComparison.Ordinal);
            var numbers = SvgPathData.ReadNumbers(percent ? value.TrimEnd('%') : value);
            if (numbers.Count != 1)
            {
                return false;
            }

            channels[count] = count < 3
                ? (percent ? numbers[0] * 2.55 : numbers[0])
                : (percent ? numbers[0] / 100 : numbers[0]) * 255;
            count++;
        }

        if (count < 3)
        {
            return false;
        }

        color = Color.FromRgba(Channel(channels[0]), Channel(channels[1]), Channel(channels[2]),
            count == 4 ? Channel(channels[3]) : (byte)255);
        return true;
    }

    private static byte Channel(double value) => (byte)Math.Max(0, Math.Min(255, Math.Round(value)));
}
