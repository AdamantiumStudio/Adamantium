using System.Collections.Generic;
using System.IO;

namespace Adamantium.Fonts.Shaping;

internal static class DataFile
{
    public static IEnumerable<string> ReadLines(string name)
    {
        var assembly = typeof(DataFile).Assembly;
        using var stream = assembly.GetManifestResourceStream($"Adamantium.Fonts.Data.{name}");
        using var reader = new StreamReader(stream);
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length > 0)
            {
                yield return line;
            }
        }
    }
}
