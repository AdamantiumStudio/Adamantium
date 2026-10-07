using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Fonts.Shaping;

namespace Adamantium.Fonts;

/// <summary>
/// The fonts a text falls back on for a character its own font does not have: families tried in order, each in the
/// face nearest to the weight, slant and width of the text's font. Han, kana and Hangul try the family the text's
/// language reads them in first (Japanese, Korean, Traditional or Simplified Chinese), as the same character is drawn
/// differently in each. A family's character map is read without loading the font, so only the font chosen is loaded.
/// </summary>
public sealed class FontFallback
{
    private static readonly Lazy<FontFallback> SystemFallback = new(CreateSystem);

    private readonly object _gate = new();
    private readonly FontCollection _fonts;
    private readonly IReadOnlyList<string> _families;
    private readonly IReadOnlyDictionary<string, string> _cjkByLanguage;
    private readonly string _emojiFamily;
    private readonly Dictionary<(int Codepoint, int Weight, FontStyle Style, double Stretch, string Preferred), IFont> _found = new();

    /// <summary>A fallback over <paramref name="families"/> of <paramref name="fonts"/>, in order; Chinese, Japanese and
    /// Korean characters (Han, kana, Hangul, Bopomofo) try <paramref name="cjkByLanguage"/>'s family for the text's
    /// language (by its primary subtag, or the whole tag for "zh-Hant") first, and pictographs (emoji) try
    /// <paramref name="emojiFamily"/> first.</summary>
    public FontFallback(FontCollection fonts, IReadOnlyList<string> families,
        IReadOnlyDictionary<string, string> cjkByLanguage = null, string emojiFamily = null)
    {
        _fonts = fonts;
        _families = families;
        _cjkByLanguage = cjkByLanguage ?? new Dictionary<string, string>();
        _emojiFamily = emojiFamily;
    }

    /// <summary>The operating system's fallback fonts.</summary>
    public static FontFallback System => SystemFallback.Value;

    /// <summary>The font that draws <paramref name="codepoint"/> in place of <paramref name="like"/>, in its weight,
    /// slant and width; null when none of the families has it.</summary>
    public IFont FontFor(int codepoint, IFont like, string language = null)
    {
        return FontFor(codepoint, like, language, true, out _);
    }

    /// <summary>The font that draws <paramref name="codepoint"/> in place of <paramref name="like"/>, as
    /// <see cref="FontFor(int, IFont, string)"/> finds it, without waiting for a font file: while the character map or
    /// the font it needs is still being read, null with <paramref name="pending"/> set; a worker reads it and raises
    /// <see cref="TypefaceStore.Loaded"/> when done.</summary>
    public IFont FontFor(int codepoint, IFont like, string language, out bool pending)
    {
        return FontFor(codepoint, like, language, false, out pending);
    }

    private IFont FontFor(int codepoint, IFont like, string language, bool wait, out bool pending)
    {
        pending = false;
        var preferred = IsCjk(codepoint) ? CjkFamily(language)
            : UnicodeData.IsExtendedPictographic(codepoint) ? _emojiFamily
            : null;
        var key = (codepoint, like.Weight.Value, like.Style, like.Stretch.Percent, preferred);
        lock (_gate)
        {
            if (_found.TryGetValue(key, out var known))
            {
                return known;
            }
        }

        var font = preferred != null ? Find(preferred, codepoint, like, wait, ref pending) : null;
        if (font == null && !pending)
        {
            foreach (var family in _families)
            {
                font = Find(family, codepoint, like, wait, ref pending);
                if (font != null || pending)
                {
                    break;
                }
            }
        }

        if (!pending)
        {
            lock (_gate)
            {
                _found[key] = font;
            }
        }

        return font;
    }

    private IFont Find(string family, int codepoint, IFont like, bool wait, ref bool pending)
    {
        var face = _fonts.Match(family, like.Weight, like.Style, like.Stretch);
        if (face == null)
        {
            return null;
        }

        bool has;
        if (wait)
        {
            has = face.HasCharacter(codepoint);
        }
        else if (!face.TryHasCharacter(codepoint, out has))
        {
            pending = true;
            return null;
        }

        if (!has)
        {
            return null;
        }

        IFont font;
        if (wait)
        {
            font = FontCollection.Load(face, like.Weight, like.Stretch);
        }
        else if (!FontCollection.TryLoad(face, like.Weight, like.Stretch, out font))
        {
            pending = true;
            return null;
        }

        return font.TryGetGlyphIndex(codepoint, out _) ? font : null;
    }

    private string CjkFamily(string language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return _cjkByLanguage.TryGetValue(string.Empty, out var neutral) ? neutral : null;
        }

        if (_cjkByLanguage.TryGetValue(language, out var family))
        {
            return family;
        }

        var dash = language.IndexOf('-');
        var primary = dash < 0 ? language : language.Substring(0, dash);
        return _cjkByLanguage.TryGetValue(primary, out family) ? family : CjkFamily(null);
    }

    private static bool IsCjk(int codepoint)
    {
        return UnicodeData.GetScript(codepoint) is "Hani" or "Hira" or "Kana" or "Hang" or "Bopo";
    }

    private static FontFallback CreateSystem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new FontFallback(FontCollection.System,
            [
                "Segoe UI", "Segoe UI Symbol", "Segoe UI Emoji", "Segoe UI Historic", "Nirmala UI", "Ebrima",
                "Gadugi", "Leelawadee UI", "Javanese Text", "Myanmar Text", "Mongolian Baiti", "Microsoft Himalaya",
                "Microsoft Yi Baiti", "Microsoft New Tai Lue", "Microsoft Tai Le", "Microsoft PhagsPa", "Sylfaen",
                "Microsoft YaHei", "Yu Gothic", "Malgun Gothic", "Microsoft JhengHei", "SimSun", "Cambria Math",
                "Arial", "Times New Roman",
            ],
            new Dictionary<string, string>
            {
                [string.Empty] = "Microsoft YaHei",
                ["zh"] = "Microsoft YaHei",
                ["zh-Hant"] = "Microsoft JhengHei",
                ["zh-TW"] = "Microsoft JhengHei",
                ["zh-HK"] = "Microsoft JhengHei",
                ["ja"] = "Yu Gothic",
                ["ko"] = "Malgun Gothic",
            },
            "Segoe UI Emoji");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new FontFallback(FontCollection.System,
            [
                "Helvetica Neue", "Apple Symbols", "Apple Color Emoji", "PingFang SC", "Hiragino Sans",
                "Apple SD Gothic Neo", "PingFang TC", "Arial Unicode MS",
            ],
            new Dictionary<string, string>
            {
                [string.Empty] = "PingFang SC",
                ["zh"] = "PingFang SC",
                ["zh-Hant"] = "PingFang TC",
                ["zh-TW"] = "PingFang TC",
                ["zh-HK"] = "PingFang HK",
                ["ja"] = "Hiragino Sans",
                ["ko"] = "Apple SD Gothic Neo",
            },
            "Apple Color Emoji");
        }

        return new FontFallback(FontCollection.System,
        [
            "DejaVu Sans", "Noto Sans", "Noto Sans Symbols", "Noto Sans Symbols 2", "Noto Color Emoji",
            "Noto Sans CJK SC", "Noto Sans CJK JP", "Noto Sans CJK KR", "Noto Sans CJK TC", "Droid Sans Fallback",
        ],
        new Dictionary<string, string>
        {
            [string.Empty] = "Noto Sans CJK SC",
            ["zh"] = "Noto Sans CJK SC",
            ["zh-Hant"] = "Noto Sans CJK TC",
            ["zh-TW"] = "Noto Sans CJK TC",
            ["zh-HK"] = "Noto Sans CJK HK",
            ["ja"] = "Noto Sans CJK JP",
            ["ko"] = "Noto Sans CJK KR",
        },
        "Noto Color Emoji");
    }
}
