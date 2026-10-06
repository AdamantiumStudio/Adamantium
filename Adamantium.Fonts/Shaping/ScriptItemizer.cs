using System.Collections.Generic;

namespace Adamantium.Fonts.Shaping;

internal static class ScriptItemizer
{
    public static List<(int Start, int End, string Script)> Itemize(string text)
    {
        var runs = new List<(int Start, int End, string Script)>();
        string current = null;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var codepoint = (int)text[i];
            var length = 1;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                codepoint = char.ConvertToUtf32(text[i], text[i + 1]);
                length = 2;
            }

            var script = UnicodeData.GetScript(codepoint);
            if (script is not ("Zyyy" or "Zinh" or "Zzzz"))
            {
                if (current == null)
                {
                    current = script;
                }
                else if (script != current)
                {
                    runs.Add((start, i, current));
                    start = i;
                    current = script;
                }
            }

            i += length - 1;
        }

        runs.Add((start, text.Length, current));
        return runs;
    }
}
