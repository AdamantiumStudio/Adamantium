using System.Globalization;
using System.Text;

namespace Adamantium.UnicodeDataGenerator;

/// <summary>
/// Writes the Unicode tables the text shaper embeds (Adamantium.Fonts/Data) from the Unicode Character Database of a
/// given version and the ISO 639-3 code table.
/// Usage: Adamantium.UnicodeDataGenerator &lt;unicode version, e.g. 16.0.0&gt; &lt;Adamantium.Fonts/Data folder&gt;
/// </summary>
public static class Program
{
    private const string Iso639Url = "https://iso639-3.sil.org/sites/iso639-3/files/downloads/iso-639-3.tab";

    private static readonly HttpClient Http = new();

    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: Adamantium.UnicodeDataGenerator <unicode version> <output folder>");
            return 1;
        }

        var ucd = $"https://www.unicode.org/Public/{args[0]}/ucd/";
        var output = args[1];

        var aliases = await Download(ucd + "PropertyValueAliases.txt");
        var scriptCodes = new Dictionary<string, string>();
        foreach (var fields in DataLines(aliases).Where(f => f[0] == "sc"))
        {
            scriptCodes[fields[2]] = fields[1];
        }

        var scripts = ParseRanges(await Download(ucd + "Scripts.txt")).Select(r => (r.Start, r.End, scriptCodes[r.Value]));
        Write(output, "Scripts.ucd", FormatRanges(scripts, true));

        var classes = ParseRanges(await Download(ucd + "extracted/DerivedCombiningClass.txt")).Where(r => r.Value != "0");
        Write(output, "CombiningClass.ucd", FormatRanges(classes, true));

        var emojiData = ParseRanges(await Download(ucd + "emoji/emoji-data.txt")).ToList();
        var pictographic = emojiData.Where(r => r.Value == "Extended_Pictographic");
        Write(output, "ExtendedPictographic.ucd", FormatRanges(pictographic, false));
        var emojiPresentation = emojiData.Where(r => r.Value == "Emoji_Presentation");
        Write(output, "EmojiPresentation.ucd", FormatRanges(emojiPresentation, false));

        var exclusions = new HashSet<int>();
        foreach (var range in ParseRanges(await Download(ucd + "DerivedNormalizationProps.txt"))
                     .Where(r => r.Value == "Full_Composition_Exclusion"))
        {
            for (var codepoint = range.Start; codepoint <= range.End; codepoint++)
            {
                exclusions.Add(codepoint);
            }
        }

        Write(output, "Decompositions.ucd", Decompositions(await Download(ucd + "UnicodeData.txt"), exclusions));
        Write(output, "Iso639.dat", TwoLetterLanguages(await Download(Iso639Url)));

        var graphemeBreaks = ParseRanges(await Download(ucd + "auxiliary/GraphemeBreakProperty.txt"));
        Write(output, "GraphemeBreak.ucd", FormatRanges(graphemeBreaks, true));

        var wordBreaks = ParseRanges(await Download(ucd + "auxiliary/WordBreakProperty.txt"));
        Write(output, "WordBreak.ucd", FormatRanges(wordBreaks, true));

        var conjuncts = DataLines(await Download(ucd + "DerivedCoreProperties.txt"))
            .Where(f => f.Length > 2 && f[1] == "InCB")
            .Select(f => ParseRange(f[0], f[2]));
        Write(output, "IndicConjunctBreak.ucd", FormatRanges(conjuncts, true));

        var lineBreaks = ParseRanges(await Download(ucd + "LineBreak.txt"));
        Write(output, "LineBreak.ucd", FormatRanges(lineBreaks, true));

        var eastAsian = ParseRanges(await Download(ucd + "EastAsianWidth.txt")).Where(r => r.Value is "F" or "W" or "H");
        Write(output, "EastAsianWidth.ucd", FormatRanges(eastAsian, true));

        var bidiNames = DataLines(aliases).Where(f => f[0] == "bc").ToDictionary(f => f[2], f => f[1]);
        var bidiClasses = BidiClasses(await Download(ucd + "extracted/DerivedBidiClass.txt"), bidiNames);
        Write(output, "BidiClass.ucd", FormatRanges(bidiClasses, true));

        var brackets = DataLines(await Download(ucd + "BidiBrackets.txt")).Select(f => string.Join(";", f) + "\n");
        Write(output, "BidiBrackets.ucd", string.Concat(brackets));

        var mirrors = DataLines(await Download(ucd + "BidiMirroring.txt")).Select(f => string.Join(";", f) + "\n");
        Write(output, "BidiMirroring.ucd", string.Concat(mirrors));

        var joining = DataLines(await Download(ucd + "ArabicShaping.txt"))
            .Select(f => ParseRange(f[0], f[3] switch { "ALAPH" => "A", "DALATH RISH" => "S", _ => f[2] }));
        Write(output, "JoiningType.ucd", FormatRanges(joining, true));
        return 0;
    }

    private static IEnumerable<(int Start, int End, string Value)> BidiClasses(string derived,
        Dictionary<string, string> shortNames)
    {
        const string Missing = "# @missing:";
        var classes = new string[0x110000];
        foreach (var raw in derived.Split('\n'))
        {
            if (!raw.StartsWith(Missing, StringComparison.Ordinal))
            {
                continue;
            }

            var fields = raw.Substring(Missing.Length).Split(';').Select(f => f.Trim()).ToArray();
            var (start, end, value) = ParseRange(fields[0], fields[1]);
            Array.Fill(classes, shortNames.GetValueOrDefault(value, value), start, end - start + 1);
        }

        foreach (var (start, end, value) in ParseRanges(derived))
        {
            Array.Fill(classes, value, start, end - start + 1);
        }

        var first = 0;
        for (var codepoint = 1; codepoint <= classes.Length; codepoint++)
        {
            if (codepoint < classes.Length && classes[codepoint] == classes[first])
            {
                continue;
            }

            if (classes[first] != "L")
            {
                yield return (first, codepoint - 1, classes[first]);
            }

            first = codepoint;
        }
    }

    private static async Task<string> Download(string url)
    {
        Console.WriteLine(url);
        return await Http.GetStringAsync(url);
    }

    private static IEnumerable<string[]> DataLines(string text)
    {
        foreach (var raw in text.Split('\n'))
        {
            var hash = raw.IndexOf('#');
            var line = (hash >= 0 ? raw.Substring(0, hash) : raw).Trim();
            if (line.Length > 0)
            {
                yield return line.Split(';').Select(f => f.Trim()).ToArray();
            }
        }
    }

    private static IEnumerable<(int Start, int End, string Value)> ParseRanges(string text)
    {
        foreach (var fields in DataLines(text))
        {
            yield return ParseRange(fields[0], fields[1]);
        }
    }

    private static (int Start, int End, string Value) ParseRange(string range, string value)
    {
        var bounds = range.Split("..");
        var start = int.Parse(bounds[0], NumberStyles.HexNumber);
        var end = bounds.Length == 2 ? int.Parse(bounds[1], NumberStyles.HexNumber) : start;
        return (start, end, value);
    }

    private static string FormatRanges(IEnumerable<(int Start, int End, string Value)> ranges, bool withValue)
    {
        var merged = new List<(int Start, int End, string Value)>();
        foreach (var range in ranges.OrderBy(r => r.Start))
        {
            if (merged.Count > 0 && merged[^1].Value == range.Value && merged[^1].End + 1 == range.Start)
            {
                merged[^1] = (merged[^1].Start, range.End, range.Value);
                continue;
            }

            merged.Add(range);
        }

        var text = new StringBuilder();
        foreach (var range in merged)
        {
            text.Append($"{range.Start:X}..{range.End:X}");
            if (withValue)
            {
                text.Append(';').Append(range.Value);
            }

            text.Append('\n');
        }

        return text.ToString();
    }

    private static string Decompositions(string unicodeData, HashSet<int> exclusions)
    {
        var text = new StringBuilder();
        foreach (var line in unicodeData.Split('\n'))
        {
            var fields = line.Split(';');
            if (fields.Length < 6 || fields[5].Length == 0 || fields[5].StartsWith('<'))
            {
                continue;
            }

            var parts = fields[5].Split(' ');
            if (parts.Length == 1)
            {
                text.Append($"{fields[0]};{parts[0]};;1\n");
                continue;
            }

            var excluded = exclusions.Contains(int.Parse(fields[0], NumberStyles.HexNumber)) ? 1 : 0;
            text.Append($"{fields[0]};{parts[0]};{parts[1]};{excluded}\n");
        }

        return text.ToString();
    }

    private static string TwoLetterLanguages(string table)
    {
        var pairs = new List<string>();
        foreach (var line in table.Split('\n').Skip(1))
        {
            var fields = line.TrimEnd('\r').Split('\t');
            if (fields.Length > 3 && fields[3].Length > 0)
            {
                pairs.Add($"{fields[3]};{fields[0]}");
            }
        }

        pairs.Sort(StringComparer.Ordinal);
        return string.Concat(pairs.Select(p => p + "\n"));
    }

    private static void Write(string folder, string name, string content)
    {
        File.WriteAllText(Path.Combine(folder, name), content);
        Console.WriteLine($"  -> {name}");
    }
}
