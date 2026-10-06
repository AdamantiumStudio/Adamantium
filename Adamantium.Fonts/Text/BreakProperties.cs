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

    private static readonly Lazy<RangeTable<LineBreakClass>> LineBreaks =
        new(() => RangeTable<LineBreakClass>.Load("LineBreak.ucd", Parse<LineBreakClass>, LineBreakClass.XX));

    private static readonly Lazy<RangeTable<bool>> EastAsianWide =
        new(() => RangeTable<bool>.Load("EastAsianWidth.ucd", _ => true, false));

    public static GraphemeBreak Grapheme(int codepoint) => Graphemes.Value[codepoint];

    public static LineBreakClass LineBreak(int codepoint) => LineBreaks.Value[codepoint];

    /// <summary>East_Asian_Width is Fullwidth, Wide or Halfwidth.</summary>
    public static bool IsEastAsian(int codepoint) => EastAsianWide.Value[codepoint];

    public static WordBreak Word(int codepoint) => Words.Value[codepoint];

    public static ConjunctBreak Conjunct(int codepoint) => Conjuncts.Value[codepoint];

    public static bool IsExtendedPictographic(int codepoint) => UnicodeData.IsExtendedPictographic(codepoint);

    private static T Parse<T>(string value) where T : struct => (T)Enum.Parse(typeof(T), value.Replace("_", ""));
}
