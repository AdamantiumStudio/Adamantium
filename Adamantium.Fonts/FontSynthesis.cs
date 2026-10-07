using System;

namespace Adamantium.Fonts;

/// <summary>
/// What may be drawn for a face a family lacks, as CSS font-synthesis has it: a bold weight by thickening the face that
/// is there, an italic by slanting it. None by default: the nearest face is drawn as it is. A face the family has always
/// wins over a synthesized one.
/// </summary>
[Flags]
public enum FontSynthesis
{
    None = 0,

    /// <summary>A weight of 600 or more on a face lighter than 600 is drawn thicker.</summary>
    Weight = 1,

    /// <summary>An italic or oblique style on an upright face is drawn slanted.</summary>
    Style = 2,
}
