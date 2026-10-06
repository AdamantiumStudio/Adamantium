using System.Collections.Generic;
using System.Linq;
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
            Foreground = over.Foreground ?? Foreground,
            Background = over.Background ?? Background,
            Decorations = over.Decorations ?? Decorations,
            DecorationColor = over.DecorationColor ?? DecorationColor,
        };
    }

    /// <summary>Whether text with these attributes and with <paramref name="other"/> shapes alike: colors and lines
    /// differ freely.</summary>
    public bool ShapesLike(TextAttributes other)
    {
        return Language == other.Language && FeaturesEqual(Features, other.Features);
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
