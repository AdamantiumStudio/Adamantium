using System.Globalization;
using Adamantium.Fonts.Shaping;
using static Adamantium.Fonts.Text.LineBreakClass;

namespace Adamantium.Fonts.Text;

internal sealed class LineBreaker
{
    private const int DottedCircle = 0x25CC;

    private readonly int[] _index;
    private readonly int[] _codepoint;
    private readonly LineBreakClass[] _raw;
    private readonly LineBreakClass[] _class;
    private readonly bool[] _attached;
    private readonly bool[] _initialQuote;
    private readonly bool[] _finalQuote;
    private readonly bool[] _eastAsian;
    private readonly bool[] _pictographicUnassigned;
    private readonly bool[] _dottedCircle;
    private readonly int _count;

    private LineBreaker(string text)
    {
        var count = 0;
        _index = new int[text.Length];
        _codepoint = new int[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            _index[count] = i;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                _codepoint[count++] = char.ConvertToUtf32(text[i], text[i + 1]);
                i++;
                continue;
            }

            _codepoint[count++] = text[i];
        }

        _count = count;
        _raw = new LineBreakClass[count];
        _class = new LineBreakClass[count];
        _attached = new bool[count];
        _initialQuote = new bool[count];
        _finalQuote = new bool[count];
        _eastAsian = new bool[count];
        _pictographicUnassigned = new bool[count];
        _dottedCircle = new bool[count];
        Classify();
    }

    public static LineBreakKind[] Find(string text)
    {
        var breaks = new LineBreakKind[text.Length + 1];
        if (text.Length == 0)
        {
            return breaks;
        }

        var breaker = new LineBreaker(text);
        for (var k = 1; k < breaker._count; k++)
        {
            breaks[breaker._index[k]] = breaker.Decide(k);
        }

        breaks[text.Length] = LineBreakKind.Mandatory;
        return breaks;
    }

    private void Classify()
    {
        for (var k = 0; k < _count; k++)
        {
            var codepoint = _codepoint[k];
            var category = UnicodeData.GetCategory(codepoint);
            var value = BreakProperties.LineBreak(codepoint);
            value = value switch
            {
                AI or SG or XX => AL,
                SA => category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark ? CM : AL,
                CJ => NS,
                _ => value,
            };

            _raw[k] = value;
            _initialQuote[k] = category == UnicodeCategory.InitialQuotePunctuation;
            _finalQuote[k] = category == UnicodeCategory.FinalQuotePunctuation;
            _eastAsian[k] = BreakProperties.IsEastAsian(codepoint);
            _pictographicUnassigned[k] = BreakProperties.IsExtendedPictographic(codepoint)
                                         && category == UnicodeCategory.OtherNotAssigned;
            _dottedCircle[k] = codepoint == DottedCircle;

            if (value is CM or ZWJ)
            {
                if (k > 0 && _class[k - 1] is not (BK or CR or LF or NL or SP or ZW))
                {
                    _attached[k] = true;
                    _class[k] = _class[k - 1];
                    _initialQuote[k] = _initialQuote[k - 1];
                    _finalQuote[k] = _finalQuote[k - 1];
                    _eastAsian[k] = _eastAsian[k - 1];
                    _pictographicUnassigned[k] = _pictographicUnassigned[k - 1];
                    _dottedCircle[k] = _dottedCircle[k - 1];
                    continue;
                }

                _class[k] = AL;
                continue;
            }

            _class[k] = value;
        }
    }

    private LineBreakKind Decide(int b)
    {
        var a = b - 1;
        var left = _class[a];
        var right = _class[b];

        if (left == BK)
        {
            return LineBreakKind.Mandatory;
        }

        if (left == CR && right == LF)
        {
            return LineBreakKind.None;
        }

        if (left is CR or LF or NL)
        {
            return LineBreakKind.Mandatory;
        }

        if (right is BK or CR or LF or NL or SP or ZW)
        {
            return LineBreakKind.None;
        }

        if (Before(a, skipSpaces: true) is var zw && zw >= 0 && _class[zw] == ZW)
        {
            return LineBreakKind.Allowed;
        }

        if (_raw[a] == ZWJ || _attached[b])
        {
            return LineBreakKind.None;
        }

        return Allowed(a, b, left, right) ? LineBreakKind.Allowed : LineBreakKind.None;
    }

    private bool Allowed(int a, int b, LineBreakClass left, LineBreakClass right)
    {
        if (left == WJ || right == WJ || left == GL)
        {
            return false;
        }

        if (right == GL && left is not (SP or BA or HY))
        {
            return false;
        }

        if (right is CL or CP or EX or SY)
        {
            return false;
        }

        var beforeSpaces = Before(a, skipSpaces: true);
        var leftOverSpaces = beforeSpaces >= 0 ? _class[beforeSpaces] : XX;
        if (leftOverSpaces == OP)
        {
            return false;
        }

        if (beforeSpaces >= 0 && leftOverSpaces == QU && _initialQuote[beforeSpaces])
        {
            var context = Previous(beforeSpaces);
            if (context < 0 || _class[context] is BK or CR or LF or NL or OP or QU or GL or SP or ZW)
            {
                return false;
            }
        }

        var next = Next(b);
        var nextClass = next < _count ? _class[next] : XX;
        if (right == QU && _finalQuote[b]
            && (next >= _count || nextClass is SP or GL or WJ or CL or QU or CP or EX or IS or SY or BK or CR or LF
                or NL or ZW))
        {
            return false;
        }

        if (left == SP && right == IS && next < _count && nextClass == NU)
        {
            return true;
        }

        if (right == IS)
        {
            return false;
        }

        if (right == NS && leftOverSpaces is CL or CP)
        {
            return false;
        }

        if (right == B2 && leftOverSpaces == B2)
        {
            return false;
        }

        if (left == SP)
        {
            return true;
        }

        if (right == QU && !_initialQuote[b] || left == QU && !_finalQuote[a])
        {
            return false;
        }

        if (right == QU && (!_eastAsian[a] || next >= _count || !_eastAsian[next]))
        {
            return false;
        }

        if (left == QU)
        {
            var before = Previous(a);
            if (!_eastAsian[b] || before < 0 || !_eastAsian[before])
            {
                return false;
            }
        }

        if (left == CB || right == CB)
        {
            return true;
        }

        if ((left == HY || _codepoint[Base(a)] == 0x2010) && right == AL)
        {
            var context = Previous(a);
            if (context < 0 || _class[context] is BK or CR or LF or NL or SP or ZW or CB or GL)
            {
                return false;
            }
        }

        if (right is BA or HY or NS || left == BB)
        {
            return false;
        }

        if ((left == HY || left == BA && !_eastAsian[a]) && right != HL)
        {
            var context = Previous(a);
            if (context >= 0 && _class[context] == HL)
            {
                return false;
            }
        }

        if (left == SY && right == HL || right == IN)
        {
            return false;
        }

        if (left is AL or HL && right == NU || left == NU && right is AL or HL)
        {
            return false;
        }

        if (left == PR && right is ID or EB or EM || left is ID or EB or EM && right == PO)
        {
            return false;
        }

        if (left is PR or PO && right is AL or HL || left is AL or HL && right is PR or PO)
        {
            return false;
        }

        if (right is PO or PR && EndsNumber(a, allowClose: true))
        {
            return false;
        }

        if (left is PO or PR)
        {
            if (right == NU)
            {
                return false;
            }

            if (right == OP && next < _count
                && (nextClass == NU || nextClass == IS && Next(next) < _count && _class[Next(next)] == NU))
            {
                return false;
            }
        }

        if (left is HY or IS && right == NU)
        {
            return false;
        }

        if (right == NU && EndsNumber(a, allowClose: false))
        {
            return false;
        }

        if (left == JL && right is JL or JV or H2 or H3 || left is JV or H2 && right is JV or JT
            || left is JT or H3 && right == JT)
        {
            return false;
        }

        if (left is JL or JV or JT or H2 or H3 && right == PO || left == PR && right is JL or JV or JT or H2 or H3)
        {
            return false;
        }

        if (left is AL or HL && right is AL or HL)
        {
            return false;
        }

        if (Aksara(a, b, left, right, next))
        {
            return false;
        }

        if (left == IS && right is AL or HL)
        {
            return false;
        }

        if (left is AL or HL or NU && right == OP && !_eastAsian[b]
            || left == CP && !_eastAsian[a] && right is AL or HL or NU)
        {
            return false;
        }

        if (left == RI && right == RI)
        {
            var count = 0;
            for (var i = Base(a); i >= 0 && _class[i] == RI; i = Previous(i))
            {
                count++;
            }

            return count % 2 == 0;
        }

        if (right == EM && (left == EB || _pictographicUnassigned[a]))
        {
            return false;
        }

        return true;
    }

    private bool Aksara(int a, int b, LineBreakClass left, LineBreakClass right, int next)
    {
        var leftBase = left is AK or AS || _dottedCircle[a];
        var rightBase = right is AK or AS || _dottedCircle[b];
        if (left == AP && rightBase)
        {
            return true;
        }

        if (leftBase && right is VF or VI)
        {
            return true;
        }

        if (left == VI && (right == AK || _dottedCircle[b]))
        {
            var before = Previous(a);
            if (before >= 0 && (_class[before] is AK or AS || _dottedCircle[before]))
            {
                return true;
            }
        }

        return leftBase && rightBase && next < _count && _class[next] == VF;
    }

    private bool EndsNumber(int a, bool allowClose)
    {
        var i = Base(a);
        if (allowClose && i >= 0 && _class[i] is CL or CP)
        {
            i = Previous(i);
        }

        while (i >= 0 && _class[i] is SY or IS)
        {
            i = Previous(i);
        }

        return i >= 0 && _class[i] == NU;
    }

    private int Base(int i)
    {
        while (i > 0 && _attached[i])
        {
            i--;
        }

        return i;
    }

    private int Previous(int i)
    {
        var before = Base(i) - 1;
        return before >= 0 ? Base(before) : -1;
    }

    private int Next(int i)
    {
        var next = i + 1;
        while (next < _count && _attached[next])
        {
            next++;
        }

        return next;
    }

    private int Before(int a, bool skipSpaces)
    {
        var i = Base(a);
        while (skipSpaces && i >= 0 && _class[i] == SP)
        {
            i = Previous(i);
        }

        return i;
    }
}
