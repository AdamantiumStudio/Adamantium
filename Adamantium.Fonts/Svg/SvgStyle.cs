using System;
using System.Globalization;
using System.Xml.Linq;
using Adamantium.Fonts.Tables;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Svg;

internal sealed class SvgStyle
{
    public static readonly SvgStyle Initial = new()
    {
        Fill = SvgPaint.Black,
        FillOpacity = 1,
        Opacity = 1,
    };

    public SvgPaint Fill { get; private set; }

    public double FillOpacity { get; private set; }

    public bool EvenOdd { get; private set; }

    public Color? CurrentColor { get; private set; }

    public double Opacity { get; private set; }

    public SvgStyle Inherit(XElement element, ColorPaletteTable palettes, int palette, bool withOpacity)
    {
        var fill = Property(element, "fill");
        var fillOpacity = Property(element, "fill-opacity");
        var fillRule = Property(element, "fill-rule");
        var color = Property(element, "color");
        var opacity = Property(element, "opacity");
        return new SvgStyle
        {
            Fill = fill != null && fill != "inherit" ? SvgPaint.Parse(fill, palettes, palette) : Fill,
            FillOpacity = Fraction(fillOpacity) ?? FillOpacity,
            EvenOdd = fillRule != null && fillRule != "inherit" ? fillRule == "evenodd" : EvenOdd,
            CurrentColor = color != null && SvgPaint.TryParseColor(color, palettes, palette, out var parsed)
                ? parsed
                : CurrentColor,
            Opacity = withOpacity ? Opacity * (Fraction(opacity) ?? 1) : Opacity,
        };
    }

    public static string Property(XElement element, string name)
    {
        var style = (string)element.Attribute("style");
        if (style != null)
        {
            foreach (var declaration in style.Split(';'))
            {
                var colon = declaration.IndexOf(':');
                if (colon > 0 && declaration.Substring(0, colon).Trim() == name)
                {
                    return declaration.Substring(colon + 1).Replace("!important", string.Empty).Trim();
                }
            }
        }

        return ((string)element.Attribute(name))?.Trim();
    }

    public static double? Fraction(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var percent = text.EndsWith("%", StringComparison.Ordinal);
        if (!double.TryParse(percent ? text.Substring(0, text.Length - 1) : text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return Math.Max(0, Math.Min(1, percent ? value / 100 : value));
    }
}
