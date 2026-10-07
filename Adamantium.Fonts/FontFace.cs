using System;
using System.Collections.Generic;
using System.IO;

namespace Adamantium.Fonts;

/// <summary>
/// One face of a font family as a <see cref="FontCollection"/> knows it from the file's headers alone: its family, weight,
/// slant and width, and where it lives. A variable font is one face that covers a range of weights or widths.
/// </summary>
public sealed class FontFace
{
    private readonly object _coverageGate = new();
    private List<(int Start, int End)> _coverage;

    public FontFace(string family, string legacyFamily, string faceName, string fullName, FontWeight weight,
        FontStyle style, FontStretch stretch, string path, int collectionIndex,
        FontWeight minWeight = default, FontWeight maxWeight = default,
        FontStretch minStretch = default, FontStretch maxStretch = default)
    {
        Family = family;
        LegacyFamily = legacyFamily ?? family;
        FaceName = faceName;
        FullName = fullName;
        Weight = weight;
        Style = style;
        Stretch = stretch;
        Path = path;
        CollectionIndex = collectionIndex;
        MinWeight = minWeight == default && maxWeight == default ? weight : minWeight;
        MaxWeight = minWeight == default && maxWeight == default ? weight : maxWeight;
        MinStretch = minStretch == default && maxStretch == default ? stretch : minStretch;
        MaxStretch = minStretch == default && maxStretch == default ? stretch : maxStretch;
    }

    /// <summary>The typographic family ('name' 16, else 1): "Segoe UI" for every one of its weights.</summary>
    public string Family { get; }

    /// <summary>The family the older four-style grouping uses ('name' 1), such as "Segoe UI Semibold".</summary>
    public string LegacyFamily { get; }

    /// <summary>The face within its family ('name' 17, else 2), such as "Semibold Italic".</summary>
    public string FaceName { get; }

    public string FullName { get; }

    public FontWeight Weight { get; }

    public FontStyle Style { get; }

    public FontStretch Stretch { get; }

    /// <summary>The lightest weight the face draws: below <see cref="Weight"/> only for a variable font.</summary>
    public FontWeight MinWeight { get; }

    public FontWeight MaxWeight { get; }

    public FontStretch MinStretch { get; }

    public FontStretch MaxStretch { get; }

    public bool IsVariable => MinWeight != MaxWeight || MinStretch != MaxStretch;

    public string Path { get; }

    /// <summary>Which font of a collection file (.ttc) this is; 0 for a single font.</summary>
    public int CollectionIndex { get; }

    /// <summary>Whether the face maps <paramref name="codepoint"/> to a glyph, by its character map alone: answered
    /// without loading the font, which costs a large face hundreds of milliseconds.</summary>
    public bool HasCharacter(int codepoint)
    {
        List<(int Start, int End)> coverage;
        lock (_coverageGate)
        {
            if (_coverage == null)
            {
                try
                {
                    _coverage = FontFaceReader.ReadCoverage(Path, CollectionIndex);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or EndOfStreamException)
                {
                    _coverage = [];
                }
            }

            coverage = _coverage;
        }

        var low = 0;
        var high = coverage.Count - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (codepoint < coverage[middle].Start)
            {
                high = middle - 1;
            }
            else if (codepoint > coverage[middle].End)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    public override string ToString() => $"{Family} {FaceName} ({Weight}, {Style}, {Stretch})";
}
