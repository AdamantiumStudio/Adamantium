using System.Collections.Generic;

namespace Adamantium.Fonts.Shaping;

/// <summary>What the text is written in and which OpenType features apply to it.</summary>
public sealed class ShapingOptions
{
    public static readonly ShapingOptions Default = new();

    public ShapingOptions(string script = null, string language = null, IReadOnlyList<FontFeature> features = null)
    {
        Script = script;
        Language = language;
        Features = features ?? [];
    }

    /// <summary>ISO 15924 script code, such as <c>Latn</c> or <c>Cyrl</c>; null takes it from the text.</summary>
    public string Script { get; }

    /// <summary>BCP 47 language tag, such as <c>tr</c> or <c>sr-Cyrl</c>; null uses the font's default.</summary>
    public string Language { get; }

    /// <summary>Features on top of the defaults for the script; a feature set to 0 turns a default off.</summary>
    public IReadOnlyList<FontFeature> Features { get; }
}
