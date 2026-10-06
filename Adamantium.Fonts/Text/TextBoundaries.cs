using System.Collections.Generic;

namespace Adamantium.Fonts.Text;

/// <summary>Where text may be divided, by the rules of Unicode Standard Annex #29: user-perceived characters
/// (graphemes) and words. Indices are UTF-16 offsets.</summary>
public static class TextBoundaries
{
    /// <summary>For each UTF-16 offset from 0 to the text's length, whether a grapheme starts or ends there: a caret may
    /// stand only on these.</summary>
    public static bool[] Graphemes(string text)
    {
        var boundaries = new bool[text.Length + 1];
        boundaries[0] = true;
        boundaries[text.Length] = true;
        var codepoints = Codepoints(text);
        if (codepoints.Count == 0)
        {
            return boundaries;
        }

        var previous = BreakProperties.Grapheme(codepoints[0].Value);
        var state = new GraphemeState();
        state.Advance(codepoints[0].Value, previous);
        for (var i = 1; i < codepoints.Count; i++)
        {
            var codepoint = codepoints[i].Value;
            var current = BreakProperties.Grapheme(codepoint);
            boundaries[codepoints[i].Index] = BreaksBetween(previous, current, codepoint, state);
            state.Advance(codepoint, current);
            previous = current;
        }

        return boundaries;
    }

    /// <summary>For each UTF-16 offset from 0 to the text's length, whether a word, a run of spaces or a mark of
    /// punctuation starts or ends there.</summary>
    public static bool[] Words(string text)
    {
        var boundaries = new bool[text.Length + 1];
        boundaries[0] = true;
        boundaries[text.Length] = true;
        var codepoints = Codepoints(text);
        var classes = new WordBreak[codepoints.Count];
        var pictographic = new bool[codepoints.Count];
        for (var i = 0; i < codepoints.Count; i++)
        {
            classes[i] = BreakProperties.Word(codepoints[i].Value);
            pictographic[i] = BreakProperties.IsExtendedPictographic(codepoints[i].Value);
        }

        for (var i = 1; i < codepoints.Count; i++)
        {
            boundaries[codepoints[i].Index] = WordBreaksBefore(classes, pictographic, i);
        }

        return boundaries;
    }

    /// <summary>For each UTF-16 offset from 0 to the text's length, whether a line may end there, by Unicode Standard
    /// Annex #14: after spaces, after a hyphen, between ideographs, never before closing punctuation.</summary>
    public static LineBreakKind[] LineBreaks(string text) => LineBreaker.Find(text);

    /// <summary>The next offset after <paramref name="index"/> that is a boundary, or the text's length.</summary>
    public static int Next(bool[] boundaries, int index)
    {
        for (var i = index + 1; i < boundaries.Length; i++)
        {
            if (boundaries[i])
            {
                return i;
            }
        }

        return boundaries.Length - 1;
    }

    /// <summary>The last offset before <paramref name="index"/> that is a boundary, or 0.</summary>
    public static int Previous(bool[] boundaries, int index)
    {
        for (var i = index - 1; i > 0; i--)
        {
            if (boundaries[i])
            {
                return i;
            }
        }

        return 0;
    }

    private static List<(int Index, int Value)> Codepoints(string text)
    {
        var codepoints = new List<(int Index, int Value)>(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var value = (int)text[i];
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                value = char.ConvertToUtf32(text[i], text[i + 1]);
                codepoints.Add((i, value));
                i++;
                continue;
            }

            codepoints.Add((i, value));
        }

        return codepoints;
    }

    private static bool BreaksBetween(GraphemeBreak previous, GraphemeBreak current, int codepoint, GraphemeState state)
    {
        if (previous == GraphemeBreak.CR && current == GraphemeBreak.LF)
        {
            return false;
        }

        if (previous is GraphemeBreak.Control or GraphemeBreak.CR or GraphemeBreak.LF
            || current is GraphemeBreak.Control or GraphemeBreak.CR or GraphemeBreak.LF)
        {
            return true;
        }

        if (previous == GraphemeBreak.L && current is GraphemeBreak.L or GraphemeBreak.V or GraphemeBreak.LV
                or GraphemeBreak.LVT
            || previous is GraphemeBreak.LV or GraphemeBreak.V && current is GraphemeBreak.V or GraphemeBreak.T
            || previous is GraphemeBreak.LVT or GraphemeBreak.T && current == GraphemeBreak.T)
        {
            return false;
        }

        if (current is GraphemeBreak.Extend or GraphemeBreak.ZWJ or GraphemeBreak.SpacingMark
            || previous == GraphemeBreak.Prepend)
        {
            return false;
        }

        if (BreakProperties.Conjunct(codepoint) == ConjunctBreak.Consonant && state.ConjunctLinked)
        {
            return false;
        }

        if (previous == GraphemeBreak.ZWJ && state.PictographicZwj && BreakProperties.IsExtendedPictographic(codepoint))
        {
            return false;
        }

        if (previous == GraphemeBreak.RegionalIndicator && current == GraphemeBreak.RegionalIndicator)
        {
            return state.RegionalIndicators % 2 == 0;
        }

        return true;
    }

    private static bool WordBreaksBefore(WordBreak[] classes, bool[] pictographic, int i)
    {
        var previous = classes[i - 1];
        var current = classes[i];
        if (previous == WordBreak.CR && current == WordBreak.LF)
        {
            return false;
        }

        if (previous is WordBreak.Newline or WordBreak.CR or WordBreak.LF
            || current is WordBreak.Newline or WordBreak.CR or WordBreak.LF)
        {
            return true;
        }

        if (previous == WordBreak.ZWJ && pictographic[i])
        {
            return false;
        }

        if (previous == WordBreak.WSegSpace && current == WordBreak.WSegSpace)
        {
            return false;
        }

        if (IsIgnorable(current))
        {
            return false;
        }

        var before = Skip(classes, i - 1, -1);
        if (before < 0)
        {
            return true;
        }

        var left = classes[before];
        var beforeLeft = Skip(classes, before - 1, -1);
        var leftOfLeft = beforeLeft >= 0 ? classes[beforeLeft] : WordBreak.Other;
        var after = Skip(classes, i + 1, 1);
        var right = after >= 0 ? classes[after] : WordBreak.Other;

        if (IsAHLetter(left) && IsAHLetter(current))
        {
            return false;
        }

        if (IsAHLetter(left) && (current == WordBreak.MidLetter || IsMidNumLetQ(current)) && IsAHLetter(right))
        {
            return false;
        }

        if (IsAHLetter(leftOfLeft) && (left == WordBreak.MidLetter || IsMidNumLetQ(left)) && IsAHLetter(current))
        {
            return false;
        }

        if (left == WordBreak.HebrewLetter && current == WordBreak.SingleQuote)
        {
            return false;
        }

        if (left == WordBreak.HebrewLetter && current == WordBreak.DoubleQuote && right == WordBreak.HebrewLetter)
        {
            return false;
        }

        if (leftOfLeft == WordBreak.HebrewLetter && left == WordBreak.DoubleQuote && current == WordBreak.HebrewLetter)
        {
            return false;
        }

        if (left == WordBreak.Numeric && current == WordBreak.Numeric
            || IsAHLetter(left) && current == WordBreak.Numeric
            || left == WordBreak.Numeric && IsAHLetter(current))
        {
            return false;
        }

        if (leftOfLeft == WordBreak.Numeric && (left == WordBreak.MidNum || IsMidNumLetQ(left))
            && current == WordBreak.Numeric)
        {
            return false;
        }

        if (left == WordBreak.Numeric && (current == WordBreak.MidNum || IsMidNumLetQ(current))
            && right == WordBreak.Numeric)
        {
            return false;
        }

        if (left == WordBreak.Katakana && current == WordBreak.Katakana)
        {
            return false;
        }

        if ((IsAHLetter(left) || left is WordBreak.Numeric or WordBreak.Katakana or WordBreak.ExtendNumLet)
            && current == WordBreak.ExtendNumLet)
        {
            return false;
        }

        if (left == WordBreak.ExtendNumLet
            && (IsAHLetter(current) || current is WordBreak.Numeric or WordBreak.Katakana))
        {
            return false;
        }

        if (left == WordBreak.RegionalIndicator && current == WordBreak.RegionalIndicator)
        {
            var count = 0;
            for (var k = before; k >= 0; k = Skip(classes, k - 1, -1))
            {
                if (classes[k] != WordBreak.RegionalIndicator)
                {
                    break;
                }

                count++;
            }

            return count % 2 == 0;
        }

        return true;
    }

    private static int Skip(WordBreak[] classes, int index, int step)
    {
        while (index >= 0 && index < classes.Length && IsIgnorable(classes[index]))
        {
            index += step;
        }

        return index >= 0 && index < classes.Length ? index : -1;
    }

    private static bool IsIgnorable(WordBreak value) => value is WordBreak.Extend or WordBreak.Format or WordBreak.ZWJ;

    private static bool IsAHLetter(WordBreak value) => value is WordBreak.ALetter or WordBreak.HebrewLetter;

    private static bool IsMidNumLetQ(WordBreak value) => value is WordBreak.MidNumLet or WordBreak.SingleQuote;

    private sealed class GraphemeState
    {
        private bool _inPictographic;
        private bool _afterConsonant;

        public bool PictographicZwj { get; private set; }

        public bool ConjunctLinked { get; private set; }

        public int RegionalIndicators { get; private set; }

        public void Advance(int codepoint, GraphemeBreak value)
        {
            PictographicZwj = value == GraphemeBreak.ZWJ && _inPictographic;
            _inPictographic = BreakProperties.IsExtendedPictographic(codepoint)
                              || value == GraphemeBreak.Extend && _inPictographic;

            RegionalIndicators = value == GraphemeBreak.RegionalIndicator ? RegionalIndicators + 1 : 0;

            switch (BreakProperties.Conjunct(codepoint))
            {
                case ConjunctBreak.Consonant:
                    _afterConsonant = true;
                    ConjunctLinked = false;
                    break;
                case ConjunctBreak.Linker when _afterConsonant:
                    ConjunctLinked = true;
                    break;
                case ConjunctBreak.Extend when _afterConsonant:
                    break;
                default:
                    _afterConsonant = false;
                    ConjunctLinked = false;
                    break;
            }
        }
    }
}
