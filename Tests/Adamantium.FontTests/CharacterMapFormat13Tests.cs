using System.Collections.Generic;
using Adamantium.Fonts.Tables.CMAP;
using NUnit.Framework;

namespace Adamantium.FontTests;

// Format 13 maps whole ranges to ONE glyph each, as a last-resort font draws a whole block with one sign.
public class CharacterMapFormat13Tests
{
    private static CharacterMapFormat13 Map() => new()
    {
        Groups =
        [
            new ConstantMapGroup { StartCharCode = 0x41, EndCharCode = 0x5A, GlyphId = 5 },
            new ConstantMapGroup { StartCharCode = 0x400, EndCharCode = 0x4FF, GlyphId = 7 },
        ],
    };

    [TestCase(0x41u, 5u)]
    [TestCase(0x4Du, 5u)]
    [TestCase(0x5Au, 5u)]
    [TestCase(0x430u, 7u)]
    [TestCase(0x30u, 0u)]
    [TestCase(0x500u, 0u)]
    public void EveryCharacterOfARange_MapsToItsGlyph(uint character, uint glyph)
    {
        Assert.That(Map().GetGlyphIndex(character), Is.EqualTo(glyph));
    }

    [Test]
    public void TheMappingsCoverEveryCharacterOfEachRange()
    {
        var mappings = new Dictionary<uint, uint>();

        Map().GetUnicodeToGlyphMappings(mappings);

        Assert.That(mappings, Has.Count.EqualTo(26 + 256));
        Assert.That(mappings[0x4FF], Is.EqualTo(7));
    }
}
