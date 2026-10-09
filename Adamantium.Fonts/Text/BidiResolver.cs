using System;
using System.Collections.Generic;
using static Adamantium.Fonts.Text.BidiClass;

namespace Adamantium.Fonts.Text;

/// <summary>The Unicode Bidirectional Algorithm (UAX #9) over one paragraph's characters, by their bidi classes.</summary>
internal sealed class BidiResolver
{
    /// <summary>The deepest embedding level (BD2).</summary>
    public const int MaxDepth = 125;

    private const int MaxBracketPairs = 63;

    private readonly BidiClass[] _initial;
    private readonly BidiClass[] _types;
    private readonly int[] _codepoints;
    private readonly int[] _matchingPdi;
    private readonly int[] _matchingInitiator;
    private readonly byte[] _levels;
    private readonly byte[] _embedding;
    private readonly int _count;

    /// <summary>Resolves the levels of a paragraph: <paramref name="codepoints"/>, when given, pair its brackets (N0);
    /// <paramref name="paragraphLevel"/> is 0 or 1, or -1 to take it from the first strong character (P2, P3).</summary>
    public BidiResolver(BidiClass[] types, int[] codepoints, int paragraphLevel)
    {
        _count = types.Length;
        _initial = types;
        _types = (BidiClass[])types.Clone();
        _codepoints = codepoints;
        _levels = new byte[_count];
        _matchingPdi = new int[_count];
        _matchingInitiator = new int[_count];
        MatchIsolates();
        ParagraphLevel = (byte)(paragraphLevel >= 0 ? paragraphLevel : FirstStrongLevel(0, _count, 0));
        ResolveExplicit();
        _embedding = (byte[])_levels.Clone();
        foreach (var sequence in IsolatingRunSequences())
        {
            ResolveSequence(sequence);
        }

        LevelRemoved();
    }

    /// <summary>The paragraph's embedding level.</summary>
    public byte ParagraphLevel { get; }

    /// <summary>The resolved level of each character, before the line rules; a character X9 removes takes the level
    /// of the one before it.</summary>
    public byte[] Levels => _levels;

    /// <summary>Whether rule X9 removes the character: embedding and override controls and boundary neutrals.</summary>
    public bool IsRemoved(int index) => IsRemovedClass(_initial[index]);

    /// <summary>The levels of the characters from <paramref name="start"/> to <paramref name="end"/> on one line, with
    /// separators and the white space before them and at the line's end at the paragraph's level (L1).</summary>
    public byte[] LineLevels(int start, int end)
    {
        var levels = new byte[end - start];
        Array.Copy(_levels, start, levels, 0, levels.Length);
        var trailing = true;
        for (var i = end - 1; i >= start; i--)
        {
            var type = _initial[i];
            if (type is S or B)
            {
                levels[i - start] = ParagraphLevel;
                trailing = true;
                continue;
            }

            if (trailing && (type is WS or LRI or RLI or FSI or PDI || IsRemovedClass(type)))
            {
                levels[i - start] = ParagraphLevel;
                continue;
            }

            trailing = false;
        }

        return levels;
    }

    /// <summary>The positions of a line's characters in visual order, left to right, from their levels (L2).</summary>
    public static int[] Reorder(IReadOnlyList<byte> levels)
    {
        var order = new int[levels.Count];
        var highest = 0;
        var lowestOdd = MaxDepth + 2;
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
            highest = Math.Max(highest, levels[i]);
            if ((levels[i] & 1) == 1)
            {
                lowestOdd = Math.Min(lowestOdd, levels[i]);
            }
        }

        for (var level = highest; level >= lowestOdd; level--)
        {
            for (var i = 0; i < order.Length; i++)
            {
                if (levels[order[i]] < level)
                {
                    continue;
                }

                var end = i;
                while (end + 1 < order.Length && levels[order[end + 1]] >= level)
                {
                    end++;
                }

                Array.Reverse(order, i, end - i + 1);
                i = end;
            }
        }

        return order;
    }

    private static bool IsRemovedClass(BidiClass type) => type is RLE or LRE or RLO or LRO or PDF or BN;

    private static bool IsIsolateInitiator(BidiClass type) => type is LRI or RLI or FSI;

    private void MatchIsolates()
    {
        var open = new Stack<int>();
        for (var i = 0; i < _count; i++)
        {
            _matchingPdi[i] = -1;
            _matchingInitiator[i] = -1;
            if (IsIsolateInitiator(_initial[i]))
            {
                open.Push(i);
            }
            else if (_initial[i] == PDI && open.Count > 0)
            {
                var initiator = open.Pop();
                _matchingPdi[initiator] = i;
                _matchingInitiator[i] = initiator;
            }
            else if (_initial[i] == B)
            {
                open.Clear();
            }
        }
    }

    private int FirstStrongLevel(int start, int end, int none)
    {
        for (var i = start; i < end; i++)
        {
            switch (_initial[i])
            {
                case L:
                    return 0;
                case R:
                case AL:
                    return 1;
                case LRI:
                case RLI:
                case FSI:
                    if (_matchingPdi[i] < 0)
                    {
                        return none;
                    }

                    i = _matchingPdi[i];
                    break;
                case B:
                    return none;
            }
        }

        return none;
    }

    private void ResolveExplicit()
    {
        var levels = new byte[MaxDepth + 2];
        var overrides = new BidiClass[MaxDepth + 2];
        var isolates = new bool[MaxDepth + 2];
        var depth = 0;
        levels[0] = ParagraphLevel;
        overrides[0] = ON;
        var overflowIsolates = 0;
        var overflowEmbeddings = 0;
        var validIsolates = 0;

        for (var i = 0; i < _count; i++)
        {
            var type = _initial[i];
            switch (type)
            {
                case RLE:
                case LRE:
                case RLO:
                case LRO:
                case RLI:
                case LRI:
                case FSI:
                {
                    var isolate = IsIsolateInitiator(type);
                    var rightToLeft = type is RLE or RLO or RLI;
                    if (type == FSI)
                    {
                        var end = _matchingPdi[i] >= 0 ? _matchingPdi[i] : _count;
                        rightToLeft = FirstStrongLevel(i + 1, end, 0) == 1;
                    }

                    if (isolate)
                    {
                        _levels[i] = levels[depth];
                        if (overrides[depth] != ON)
                        {
                            _types[i] = overrides[depth];
                        }
                    }

                    var level = rightToLeft ? (levels[depth] + 1) | 1 : (levels[depth] + 2) & ~1;
                    if (level <= MaxDepth && overflowIsolates == 0 && overflowEmbeddings == 0)
                    {
                        if (isolate)
                        {
                            validIsolates++;
                        }

                        depth++;
                        levels[depth] = (byte)level;
                        overrides[depth] = type == LRO ? L : type == RLO ? R : ON;
                        isolates[depth] = isolate;
                    }
                    else if (isolate)
                    {
                        overflowIsolates++;
                    }
                    else if (overflowIsolates == 0)
                    {
                        overflowEmbeddings++;
                    }

                    break;
                }

                case PDI:
                    if (overflowIsolates > 0)
                    {
                        overflowIsolates--;
                    }
                    else if (validIsolates > 0)
                    {
                        overflowEmbeddings = 0;
                        while (!isolates[depth])
                        {
                            depth--;
                        }

                        depth--;
                        validIsolates--;
                    }

                    _levels[i] = levels[depth];
                    if (overrides[depth] != ON)
                    {
                        _types[i] = overrides[depth];
                    }

                    break;

                case PDF:
                    if (overflowIsolates == 0 && overflowEmbeddings > 0)
                    {
                        overflowEmbeddings--;
                    }
                    else if (overflowIsolates == 0 && !isolates[depth] && depth > 0)
                    {
                        depth--;
                    }

                    break;

                case B:
                    _levels[i] = ParagraphLevel;
                    break;

                case BN:
                    break;

                default:
                    _levels[i] = levels[depth];
                    if (overrides[depth] != ON)
                    {
                        _types[i] = overrides[depth];
                    }

                    break;
            }
        }
    }

    private List<int[]> IsolatingRunSequences()
    {
        var runs = new List<List<int>>();
        var runOf = new int[_count];
        List<int> run = null;
        var runLevel = -1;
        for (var i = 0; i < _count; i++)
        {
            if (IsRemovedClass(_initial[i]))
            {
                runOf[i] = -1;
                continue;
            }

            if (run == null || _levels[i] != runLevel)
            {
                run = [];
                runs.Add(run);
                runLevel = _levels[i];
            }

            run.Add(i);
            runOf[i] = runs.Count - 1;
        }

        var sequences = new List<int[]>();
        foreach (var start in runs)
        {
            var first = start[0];
            if (_initial[first] == PDI && _matchingInitiator[first] >= 0)
            {
                continue;
            }

            var sequence = new List<int>();
            var current = start;
            while (true)
            {
                sequence.AddRange(current);
                var last = current[current.Count - 1];
                if (!IsIsolateInitiator(_initial[last]) || _matchingPdi[last] < 0)
                {
                    break;
                }

                var next = runOf[_matchingPdi[last]];
                if (next < 0)
                {
                    break;
                }

                current = runs[next];
            }

            sequences.Add(sequence.ToArray());
        }

        return sequences;
    }

    private void ResolveSequence(int[] indices)
    {
        var n = indices.Length;
        var level = _embedding[indices[0]];
        var embedding = (level & 1) == 1 ? R : L;

        var before = indices[0] - 1;
        while (before >= 0 && IsRemovedClass(_initial[before]))
        {
            before--;
        }

        var sos = (Math.Max(level, before >= 0 ? _embedding[before] : ParagraphLevel) & 1) == 1 ? R : L;

        var last = indices[n - 1];
        var after = last + 1;
        while (after < _count && IsRemovedClass(_initial[after]))
        {
            after++;
        }

        var afterLevel = after < _count && !IsIsolateInitiator(_initial[last]) ? _embedding[after] : ParagraphLevel;
        var eos = (Math.Max(level, afterLevel) & 1) == 1 ? R : L;

        var t = new BidiClass[n];
        for (var k = 0; k < n; k++)
        {
            t[k] = _types[indices[k]];
        }

        var beforeW1 = (BidiClass[])t.Clone();

        for (var k = 0; k < n; k++)
        {
            if (t[k] == NSM)
            {
                t[k] = k == 0 ? sos : t[k - 1] is LRI or RLI or FSI or PDI ? ON : t[k - 1];
            }
        }

        for (var k = 0; k < n; k++)
        {
            if (t[k] != EN)
            {
                continue;
            }

            for (var j = k - 1; j >= 0; j--)
            {
                if (t[j] is L or R or AL)
                {
                    if (t[j] == AL)
                    {
                        t[k] = AN;
                    }

                    break;
                }
            }
        }

        for (var k = 0; k < n; k++)
        {
            if (t[k] == AL)
            {
                t[k] = R;
            }
        }

        for (var k = 1; k < n - 1; k++)
        {
            if (t[k] == ES && t[k - 1] == EN && t[k + 1] == EN)
            {
                t[k] = EN;
            }
            else if (t[k] == CS && t[k - 1] == EN && t[k + 1] == EN)
            {
                t[k] = EN;
            }
            else if (t[k] == CS && t[k - 1] == AN && t[k + 1] == AN)
            {
                t[k] = AN;
            }
        }

        for (var k = 0; k < n; k++)
        {
            if (t[k] != ET)
            {
                continue;
            }

            var end = k;
            while (end < n && t[end] == ET)
            {
                end++;
            }

            if ((k > 0 && t[k - 1] == EN) || (end < n && t[end] == EN))
            {
                for (var j = k; j < end; j++)
                {
                    t[j] = EN;
                }
            }

            k = end - 1;
        }

        for (var k = 0; k < n; k++)
        {
            if (t[k] is ES or ET or CS)
            {
                t[k] = ON;
            }
        }

        for (var k = 0; k < n; k++)
        {
            if (t[k] != EN)
            {
                continue;
            }

            var strong = sos;
            for (var j = k - 1; j >= 0; j--)
            {
                if (t[j] is L or R)
                {
                    strong = t[j];
                    break;
                }
            }

            if (strong == L)
            {
                t[k] = L;
            }
        }

        if (_codepoints != null)
        {
            ResolveBrackets(indices, t, beforeW1, sos, embedding);
        }

        for (var k = 0; k < n; k++)
        {
            if (!IsNeutralOrIsolate(t[k]))
            {
                continue;
            }

            var end = k;
            while (end < n && IsNeutralOrIsolate(t[end]))
            {
                end++;
            }

            var leading = k == 0 ? sos : StrongDirection(t[k - 1]);
            var trailing = end == n ? eos : StrongDirection(t[end]);
            var resolved = leading == trailing ? leading : embedding;
            for (var j = k; j < end; j++)
            {
                t[j] = resolved;
            }

            k = end - 1;
        }

        for (var k = 0; k < n; k++)
        {
            var index = indices[k];
            var current = _levels[index];
            if ((current & 1) == 0)
            {
                if (t[k] == R)
                {
                    _levels[index] = (byte)(current + 1);
                }
                else if (t[k] is AN or EN)
                {
                    _levels[index] = (byte)(current + 2);
                }
            }
            else if (t[k] is L or EN or AN)
            {
                _levels[index] = (byte)(current + 1);
            }
        }
    }

    private void ResolveBrackets(int[] indices, BidiClass[] t, BidiClass[] beforeW1, BidiClass sos, BidiClass embedding)
    {
        var pairs = new List<(int Open, int Close)>();
        var stack = new List<(int Pair, int Position)>();
        for (var k = 0; k < indices.Length; k++)
        {
            if (t[k] != ON || !BidiProperties.TryGetBracket(_codepoints[indices[k]], out var pair, out var opens))
            {
                continue;
            }

            if (opens)
            {
                if (stack.Count == MaxBracketPairs)
                {
                    break;
                }

                stack.Add((Canonical(pair), k));
                continue;
            }

            var closing = Canonical(_codepoints[indices[k]]);
            for (var s = stack.Count - 1; s >= 0; s--)
            {
                if (stack[s].Pair != closing)
                {
                    continue;
                }

                pairs.Add((stack[s].Position, k));
                stack.RemoveRange(s, stack.Count - s);
                break;
            }
        }

        pairs.Sort((a, b) => a.Open.CompareTo(b.Open));
        var opposite = embedding == L ? R : L;
        foreach (var (open, close) in pairs)
        {
            var found = false;
            var foundOpposite = false;
            for (var k = open + 1; k < close; k++)
            {
                var direction = StrongDirection(t[k]);
                if (direction == embedding)
                {
                    found = true;
                    break;
                }

                if (direction == opposite)
                {
                    foundOpposite = true;
                }
            }

            BidiClass resolved;
            if (found)
            {
                resolved = embedding;
            }
            else if (foundOpposite)
            {
                var context = sos;
                for (var k = open - 1; k >= 0; k--)
                {
                    var direction = StrongDirection(t[k]);
                    if (direction is L or R)
                    {
                        context = direction;
                        break;
                    }
                }

                resolved = context == opposite ? opposite : embedding;
            }
            else
            {
                continue;
            }

            t[open] = resolved;
            t[close] = resolved;
            for (var k = open + 1; k < t.Length && beforeW1[k] == NSM; k++)
            {
                t[k] = resolved;
            }

            for (var k = close + 1; k < t.Length && beforeW1[k] == NSM; k++)
            {
                t[k] = resolved;
            }
        }
    }

    private static int Canonical(int codepoint) => codepoint switch
    {
        0x2329 => 0x3008,
        0x232A => 0x3009,
        _ => codepoint
    };

    private static bool IsNeutralOrIsolate(BidiClass type) => type is B or S or WS or ON or LRI or RLI or FSI or PDI;

    private static BidiClass StrongDirection(BidiClass type) => type switch
    {
        L => L,
        R or AL or EN or AN => R,
        _ => ON
    };

    private void LevelRemoved()
    {
        for (var i = 0; i < _count; i++)
        {
            if (IsRemovedClass(_initial[i]))
            {
                _levels[i] = i > 0 ? _levels[i - 1] : ParagraphLevel;
            }
        }
    }
}
