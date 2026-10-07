using System.Collections.Generic;

namespace Adamantium.Fonts;

public static class TypefaceStore
{
    private static readonly Dictionary<string, Typeface> typefaceMap = new();

    /// <summary>The typeface of a file, or of a system font by name with <paramref name="isSystem"/>; each is parsed
    /// once and shared, whichever thread asks.</summary>
    public static Typeface GetTypeface(string path, bool isSystem = false)
    {
        lock (typefaceMap)
        {
            if (typefaceMap.TryGetValue(path, out var typeface))
            {
                return typeface;
            }

            typeface = isSystem ? Typeface.LoadSystemFont(path) : Typeface.LoadFont(path);
            typefaceMap[path] = typeface;
            return typeface;
        }
    }
}
