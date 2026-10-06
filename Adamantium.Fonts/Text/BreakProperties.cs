using System;
using Adamantium.Fonts.Shaping;

namespace Adamantium.Fonts.Text;

internal static class BreakProperties
{
    private static readonly Lazy<RangeTable<GraphemeBreak>> Graphemes =
        new(() => RangeTable<GraphemeBreak>.Load("GraphemeBreak.ucd", Parse<GraphemeBreak>, GraphemeBreak.Other));

    private static readonly Lazy<RangeTable<WordBreak>> Words =
        new(() => RangeTable<WordBreak>.Load("WordBreak.ucd", Parse<WordBreak>, WordBreak.Other));

    private static readonly Lazy<RangeTable<ConjunctBreak>> Conjuncts =
        new(() => RangeTable<ConjunctBreak>.Load("IndicConjunctBreak.ucd", Parse<ConjunctBreak>, ConjunctBreak.None));

    public static GraphemeBreak Grapheme(int codepoint) => Graphemes.Value[codepoint];

    public static WordBreak Word(int codepoint) => Words.Value[codepoint];

    public static ConjunctBreak Conjunct(int codepoint) => Conjuncts.Value[codepoint];

    public static bool IsExtendedPictographic(int codepoint) => UnicodeData.IsExtendedPictographic(codepoint);

    private static T Parse<T>(string value) where T : struct => (T)Enum.Parse(typeof(T), value.Replace("_", ""));
}
