using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Common;

/// <summary>An OpenType feature a font offers: its tag and registered name, the table it lives in, and its parameters.</summary>
public sealed class Feature
{
    internal Feature(FeatureInfo info, FeatureKind kind, FeatureParametersTable parameters)
    {
        Info = info;
        Kind = kind;
        FeatureParameters = parameters;
    }

    public FeatureInfo Info { get; }

    /// <summary>Substitution (GSUB) or positioning (GPOS).</summary>
    public FeatureKind Kind { get; }

    /// <summary>Names and characters of a stylistic set or a character variant; null when the font gives none.</summary>
    public FeatureParametersTable FeatureParameters { get; }

    public override string ToString()
    {
        return $"{Info.Tag} ({Kind}): {Info.FriendlyName}";
    }
}
