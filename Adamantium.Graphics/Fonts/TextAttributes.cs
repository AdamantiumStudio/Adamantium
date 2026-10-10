using System.Collections.Generic;
using System.Linq;
using Adamantium.Fonts;
using Adamantium.Fonts.Shaping;
using Adamantium.Mathematics;

namespace Adamantium.Graphics.Fonts;

/// <summary>How a stretch of text looks. A field left unset takes the value of the text's defaults.</summary>
public sealed class TextAttributes
{
    public static readonly TextAttributes Empty = new();

    /// <summary>OpenType features on top of the script's defaults.</summary>
    public IReadOnlyList<FontFeature> Features { get; init; }

    /// <summary>BCP 47 language tag, such as <c>ru</c>, <c>sr</c> or <c>zh-Hant</c>: picks the font's local forms.</summary>
    public string Language { get; init; }

    /// <summary>The font of the text, such as a bold or italic face of the family; unset takes the layout's.</summary>
    public IFont Font { get; init; }

    /// <summary>The size of the text; unset takes the size the text is laid out at. A line is as tall as its largest
    /// text.</summary>
    public double? FontSize { get; init; }

    /// <summary>What the text is drawn with that its <see cref="Font"/> lacks: thicker for a bold, slanted for an italic
    /// (<see cref="FontSynthesisRules"/>). A thickened glyph advances further.</summary>
    public FontSynthesis? Synthesis { get; init; }

    /// <summary>Space added after each character, in thousandths of an em, as InDesign's tracking; negative draws the
    /// letters closer. Unset takes the layout's <see cref="TextLayout.Tracking"/>.</summary>
    public double? Tracking { get; init; }

    /// <summary>How far the text is raised above its line's baseline, in the layout's units, as InDesign's baseline
    /// shift; negative lowers it. Its size and the line's height stay as they are; its lines follow it.</summary>
    public double? BaselineShift { get; init; }

    /// <summary>The font's palette color glyphs are drawn in ('CPAL', <see cref="IFont.ColorPalettes"/>); unset, or one
    /// the font lacks, draws them in its first.</summary>
    public int? ColorPalette { get; init; }

    public Color? Foreground { get; init; }

    /// <summary>Fill behind the text, as high as the line.</summary>
    public Color? Background { get; init; }

    public TextDecorations? Decorations { get; init; }

    /// <summary>The color of the lines; unset draws them in the text's color.</summary>
    public Color? DecorationColor { get; init; }

    /// <summary>These attributes with every field <paramref name="over"/> sets taken from it.</summary>
    public TextAttributes With(TextAttributes over)
    {
        if (over == null)
        {
            return this;
        }

        return new TextAttributes
        {
            Features = over.Features ?? Features,
            Language = over.Language ?? Language,
            Font = over.Font ?? Font,
            FontSize = over.FontSize ?? FontSize,
            Synthesis = over.Synthesis ?? Synthesis,
            Tracking = over.Tracking ?? Tracking,
            BaselineShift = over.BaselineShift ?? BaselineShift,
            ColorPalette = over.ColorPalette ?? ColorPalette,
            Foreground = over.Foreground ?? Foreground,
            Background = over.Background ?? Background,
            Decorations = over.Decorations ?? Decorations,
            DecorationColor = over.DecorationColor ?? DecorationColor,
        };
    }

    /// <summary>Whether text with these attributes and with <paramref name="other"/> shapes alike: colors, lines and
    /// sizes differ freely; a palette does not, as its color glyphs are drawn anew, and tracking and baseline shift do
    /// not, as they move them.</summary>
    public bool ShapesLike(TextAttributes other)
    {
        return Language == other.Language && ReferenceEquals(Font, other.Font) && Synthesis == other.Synthesis
                                          && ColorPalette == other.ColorPalette && Tracking == other.Tracking
                                          && BaselineShift == other.BaselineShift
                                          && FeaturesEqual(Features, other.Features);
    }

    private static bool FeaturesEqual(IReadOnlyList<FontFeature> left, IReadOnlyList<FontFeature> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return (left ?? []).SequenceEqual(right ?? []);
    }
}
