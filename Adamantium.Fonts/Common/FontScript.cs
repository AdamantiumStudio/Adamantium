using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.Common;

/// <summary>A script a font has OpenType features for, with its language systems.</summary>
public sealed class FontScript
{
    private readonly List<FontLanguage> _languages = [];

    internal FontScript(string tag)
    {
        Tag = tag;
    }

    /// <summary>The OpenType script tag, such as <c>latn</c>, <c>cyrl</c> or <c>DFLT</c>.</summary>
    public string Tag { get; }

    public IReadOnlyList<FontLanguage> Languages => _languages;

    public FontLanguage DefaultLanguage => _languages.FirstOrDefault(l => l.IsDefault);

    public FontLanguage GetLanguage(string tag) => _languages.FirstOrDefault(l => l.Tag == tag);

    internal FontLanguage GetOrAddLanguage(string tag, bool isDefault)
    {
        var language = _languages.FirstOrDefault(l => l.Tag == tag && l.IsDefault == isDefault);
        if (language == null)
        {
            language = new FontLanguage(tag, isDefault);
            _languages.Add(language);
        }

        return language;
    }

    public override string ToString() => Tag;
}
