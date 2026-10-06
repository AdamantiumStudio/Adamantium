using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.Common;

/// <summary>A language system of a script in a font: the features it offers for text in that language.</summary>
public sealed class FontLanguage
{
    private readonly List<Feature> _features = [];

    internal FontLanguage(string tag, bool isDefault)
    {
        Tag = tag;
        IsDefault = isDefault;
        Info = LanguageTags.GetMsdnLanguage(tag);
    }

    /// <summary>The OpenType language tag, such as <c>TRK </c>; <c>DFLT</c> for the script's default system.</summary>
    public string Tag { get; }

    /// <summary>The script's default language system, used when the text's language has none of its own.</summary>
    public bool IsDefault { get; }

    public LanguageTag Info { get; }

    /// <summary>Substitution and positioning features in the font's order.</summary>
    public IReadOnlyList<Feature> Features => _features;

    public IEnumerable<Feature> GSUBFeatures => _features.Where(f => f.Kind == FeatureKind.GSUB);

    public IEnumerable<Feature> GPOSFeatures => _features.Where(f => f.Kind == FeatureKind.GPOS);

    /// <summary>A feature applied whatever the text asks for; null when there is none.</summary>
    public Feature RequiredGSUBFeature { get; internal set; }

    public Feature RequiredGPOSFeature { get; internal set; }

    public bool HasFeature(string tag) => _features.Any(f => f.Info.Tag == tag);

    internal void AddFeature(Feature feature)
    {
        if (!_features.Contains(feature))
        {
            _features.Add(feature);
        }
    }

    public override string ToString()
    {
        return $"{Tag.TrimEnd()} ({Info.FriendlyName})";
    }
}
