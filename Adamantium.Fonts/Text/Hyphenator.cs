using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Adamantium.Fonts.Text;

/// <summary>Where a word of a language may break at the end of a line, by Liang's algorithm over TeX's hyphenation
/// patterns: the patterns of hyph-utf8, read as they are, with the word exceptions they list.</summary>
public sealed class Hyphenator
{
    private const int CacheLimit = 20000;

    private static readonly ConcurrentDictionary<string, Hyphenator> Registered = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Hyphenator> Loaded = new(StringComparer.Ordinal);

    private static readonly (string Language, string File)[] BuiltIn =
    [
        ("ru", "hyph-ru.tex"), ("en-gb", "hyph-en-gb.tex"), ("en", "hyph-en-us.tex"), ("de", "hyph-de-1996.tex"),
        ("fr", "hyph-fr.tex"), ("es", "hyph-es.tex"),
    ];

    private static readonly string[] BritishRegions = ["gb", "uk", "au", "nz", "ie", "za", "in"];

    private readonly Dictionary<string, byte[]> _patterns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int[]> _exceptions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int[]> _cache = new(StringComparer.Ordinal);
    private int _longestPattern;

    private Hyphenator(int leftMin, int rightMin)
    {
        LeftMin = leftMin;
        RightMin = rightMin;
    }

    /// <summary>The fewest letters a word keeps before a break.</summary>
    public int LeftMin { get; }

    /// <summary>The fewest letters a word carries after a break onto the next line.</summary>
    public int RightMin { get; }

    /// <summary>The hyphenator of a BCP 47 language: one registered for it, else one built in (Russian, English as
    /// the United States and as Britain write it, German, French, Spanish); null for any other.</summary>
    public static Hyphenator ForLanguage(string language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return null;
        }

        var tag = language.Replace('_', '-').ToLowerInvariant();
        if (Registered.TryGetValue(tag, out var registered))
        {
            return registered;
        }

        var subtags = tag.Split('-');
        var primary = subtags[0];
        var key = primary == "en" && IsBritish(subtags) ? "en-gb" : primary;
        if (Registered.TryGetValue(key, out registered) || Registered.TryGetValue(primary, out registered))
        {
            return registered;
        }

        foreach (var (builtIn, file) in BuiltIn)
        {
            if (builtIn == key)
            {
                return Loaded.GetOrAdd(key, _ => LoadBuiltIn(file));
            }
        }

        return null;
    }

    /// <summary>Makes <paramref name="hyphenator"/> the one <see cref="ForLanguage"/> gives for a BCP 47 language
    /// tag, or for every tag of a primary language when only that is named, as "pt".</summary>
    public static void Register(string language, Hyphenator hyphenator)
    {
        Registered[language.Replace('_', '-').ToLowerInvariant()] = hyphenator;
    }

    /// <summary>Reads a hyph-utf8 pattern file (hyph-*.tex): its \patterns and \hyphenation lists, and the letters a
    /// word keeps at each side of a break from its hyphenmins, 2 and 2 when it names none.</summary>
    public static Hyphenator Load(Stream patterns)
    {
        using var reader = new StreamReader(patterns, Encoding.UTF8);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>The offsets in <paramref name="word"/> where it may break, a hyphen ending the line before each:
    /// none nearer its ends than <see cref="LeftMin"/> and <see cref="RightMin"/> letters.</summary>
    public IReadOnlyList<int> Hyphenate(string word)
    {
        if (string.IsNullOrEmpty(word) || word.Length < LeftMin + RightMin)
        {
            return [];
        }

        return _cache.TryGetValue(word, out var known) ? known : Remember(word, Compute(word));
    }

    private static bool IsBritish(string[] subtags)
    {
        for (var i = 1; i < subtags.Length; i++)
        {
            if (subtags[i].Length == 2 && Array.IndexOf(BritishRegions, subtags[i]) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private int[] Remember(string word, int[] points)
    {
        if (_cache.Count >= CacheLimit)
        {
            _cache.Clear();
        }

        _cache[word] = points;
        return points;
    }

    private int[] Compute(string word)
    {
        var lower = word.ToLowerInvariant();
        if (_exceptions.TryGetValue(lower, out var exception))
        {
            return Inside(exception, word.Length);
        }

        var dotted = "." + lower + ".";
        var weights = new byte[dotted.Length + 1];
        for (var start = 0; start < dotted.Length; start++)
        {
            var longest = Math.Min(_longestPattern, dotted.Length - start);
            for (var length = 1; length <= longest; length++)
            {
                if (!_patterns.TryGetValue(dotted.Substring(start, length), out var values))
                {
                    continue;
                }

                for (var k = 0; k < values.Length; k++)
                {
                    if (values[k] > weights[start + k])
                    {
                        weights[start + k] = values[k];
                    }
                }
            }
        }

        var points = new List<int>();
        for (var offset = LeftMin; offset <= word.Length - RightMin; offset++)
        {
            if ((weights[offset + 1] & 1) == 1)
            {
                points.Add(offset);
            }
        }

        return points.ToArray();
    }

    private int[] Inside(int[] points, int length)
    {
        var inside = new List<int>();
        foreach (var point in points)
        {
            if (point >= LeftMin && point <= length - RightMin)
            {
                inside.Add(point);
            }
        }

        return inside.ToArray();
    }

    private static Hyphenator LoadBuiltIn(string file)
    {
        using var stream = typeof(Hyphenator).Assembly.GetManifestResourceStream($"Adamantium.Fonts.Data.Hyphenation.{file}");
        return Load(stream);
    }

    private static Hyphenator Parse(string source)
    {
        var hyphenator = new Hyphenator(HyphenMin(source, "left:"), HyphenMin(source, "right:"));
        var text = WithoutComments(source);
        foreach (var token in Block(text, "\\patterns{"))
        {
            hyphenator.AddPattern(token);
        }

        foreach (var token in Block(text, "\\hyphenation{"))
        {
            hyphenator.AddException(token);
        }

        return hyphenator;
    }

    private void AddPattern(string token)
    {
        var letters = new StringBuilder(token.Length);
        var values = new List<byte> { 0 };
        foreach (var symbol in token)
        {
            if (symbol is >= '0' and <= '9')
            {
                values[values.Count - 1] = (byte)(symbol - '0');
                continue;
            }

            letters.Append(symbol);
            values.Add(0);
        }

        var key = letters.ToString();
        _patterns[key] = values.ToArray();
        _longestPattern = Math.Max(_longestPattern, key.Length);
    }

    private void AddException(string token)
    {
        var letters = new StringBuilder(token.Length);
        var points = new List<int>();
        foreach (var symbol in token)
        {
            if (symbol == '-')
            {
                points.Add(letters.Length);
                continue;
            }

            letters.Append(symbol);
        }

        _exceptions[letters.ToString().ToLowerInvariant()] = points.ToArray();
    }

    private static int HyphenMin(string source, string side)
    {
        var mins = source.IndexOf("hyphenmins:", StringComparison.Ordinal);
        var at = mins < 0 ? -1 : source.IndexOf(side, mins, StringComparison.Ordinal);
        if (at < 0)
        {
            return 2;
        }

        var end = source.IndexOf('\n', at);
        var value = source.Substring(at + side.Length, (end < 0 ? source.Length : end) - at - side.Length).Trim();
        return int.TryParse(value, out var min) ? min : 2;
    }

    private static string WithoutComments(string source)
    {
        var text = new StringBuilder(source.Length);
        foreach (var line in source.Split('\n'))
        {
            var comment = line.IndexOf('%');
            text.Append(comment < 0 ? line : line.Substring(0, comment)).Append('\n');
        }

        return text.ToString();
    }

    private static IEnumerable<string> Block(string text, string opening)
    {
        var start = text.IndexOf(opening, StringComparison.Ordinal);
        if (start < 0)
        {
            yield break;
        }

        start += opening.Length;
        var end = text.IndexOf('}', start);
        var body = text.Substring(start, (end < 0 ? text.Length : end) - start);
        foreach (var token in body.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            yield return token;
        }
    }
}
