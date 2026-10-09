using System;
using System.Collections.Generic;
using System.Globalization;
using Adamantium.Fonts.Shaping;

namespace Adamantium.Fonts.Text;

internal static class BidiProperties
{
    private static readonly Lazy<RangeTable<BidiClass>> Classes =
        new(() => RangeTable<BidiClass>.Load("BidiClass.ucd", v => (BidiClass)Enum.Parse(typeof(BidiClass), v), BidiClass.L));

    private static readonly Lazy<Dictionary<int, (int Pair, bool Opens)>> Brackets = new(LoadBrackets);

    private static readonly Lazy<Dictionary<int, int>> Mirrors = new(LoadMirrors);

    public static BidiClass Class(int codepoint) => Classes.Value[codepoint];

    public static bool TryGetBracket(int codepoint, out int pair, out bool opens)
    {
        if (Brackets.Value.TryGetValue(codepoint, out var bracket))
        {
            pair = bracket.Pair;
            opens = bracket.Opens;
            return true;
        }

        pair = 0;
        opens = false;
        return false;
    }

    public static int Mirror(int codepoint) => Mirrors.Value.TryGetValue(codepoint, out var mirror) ? mirror : -1;

    private static Dictionary<int, (int Pair, bool Opens)> LoadBrackets()
    {
        var brackets = new Dictionary<int, (int Pair, bool Opens)>();
        foreach (var line in DataFile.ReadLines("BidiBrackets.ucd"))
        {
            var fields = line.Split(';');
            brackets[Hex(fields[0])] = (Hex(fields[1]), fields[2] == "o");
        }

        return brackets;
    }

    private static Dictionary<int, int> LoadMirrors()
    {
        var mirrors = new Dictionary<int, int>();
        foreach (var line in DataFile.ReadLines("BidiMirroring.ucd"))
        {
            var fields = line.Split(';');
            mirrors[Hex(fields[0])] = Hex(fields[1]);
        }

        return mirrors;
    }

    private static int Hex(string text) => int.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
