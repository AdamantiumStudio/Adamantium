using System;
using System.Collections.Generic;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Tables.CMAP
{
    /// <summary>Format 14: the Unicode Variation Sequences (a base character plus a variation selector) the font supports;
    /// used only under platform 0, encoding 5.</summary>
    public class CharacterMapFormat14 : CharacterMap
    {
        public CharacterMapFormat14()
        {
            VarSelectors = new Dictionary<uint, VariationSelector>();
        }
        
        public override UInt16 Format => 14;
        
        public UInt32 Length { get; set; }
        
        public UInt32 NumVarSelectorRecords { get; set; }
        
        public Dictionary<uint, VariationSelector> VarSelectors { get; }

        public override uint GetGlyphIndex(uint character) => 0;

        public uint CharacterPairToGlyphIndex(uint character, ushort defaultGlyphIndex, uint nextCharacter)
        {
            if (!TryGetVariant(character, nextCharacter, out var glyphIndex, out var isDefault))
            {
                return 0;
            }

            return isDefault ? defaultGlyphIndex : glyphIndex;
        }

        /// <summary>Whether the font supports <paramref name="character"/> followed by <paramref name="selector"/>: with a
        /// glyph of its own, or, for a default sequence (<paramref name="isDefault"/>), with the character's usual
        /// glyph.</summary>
        public bool TryGetVariant(uint character, uint selector, out uint glyphIndex, out bool isDefault)
        {
            glyphIndex = 0;
            isDefault = false;
            if (!VarSelectors.TryGetValue(selector, out var record))
            {
                return false;
            }

            if (InDefaultRanges(record, character))
            {
                isDefault = true;
                return true;
            }

            return record.UVSMappings.TryGetValue(character, out glyphIndex) && glyphIndex != 0;
        }

        private static bool InDefaultRanges(VariationSelector record, uint character)
        {
            var low = 0;
            var high = record.DefaultStartCodes.Count - 1;
            while (low <= high)
            {
                var middle = (low + high) / 2;
                if (character < record.DefaultStartCodes[middle])
                {
                    high = middle - 1;
                }
                else if (character > record.DefaultEndCodes[middle])
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

        public override void CollectUnicodeChars(List<uint> unicodes)
        {
            foreach (var selector in VarSelectors)
            {
                
            }
        }

        public override void GetUnicodeToGlyphMappings(Dictionary<uint, uint> unicodeToGlyph)
        {
            foreach (var selector in VarSelectors)
            {
                foreach (var uvsMapping in selector.Value.UVSMappings)
                {
                    unicodeToGlyph[uvsMapping.Key] = uvsMapping.Value;
                }
            }
        }
    }
}