using System;
using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.Tables.CMAP
{
    public class CharacterMapFormat13 : CharacterMap
    {
        public override UInt16 Format => 13;
        
        public UInt16 Reserved { get; set; }

        public UInt32 Length { get; set; }

        public UInt32 Language { get; set; }
        
        public UInt32 NumGroups { get; set; }
        
        public ConstantMapGroup[] Groups { get; set; }
        
        public override uint GetGlyphIndex(uint character)
        {
            var low = 0;
            var high = Groups.Length - 1;
            while (low <= high)
            {
                var middle = (low + high) / 2;
                if (character < Groups[middle].StartCharCode)
                {
                    high = middle - 1;
                }
                else if (character > Groups[middle].EndCharCode)
                {
                    low = middle + 1;
                }
                else
                {
                    return Groups[middle].GlyphId;
                }
            }

            return 0;
        }

        public override void CollectUnicodeChars(List<uint> unicodes)
        {
            foreach (var group in Groups)
            {
                var start = group.StartCharCode;
                var stop = group.EndCharCode;
                for (uint u = start; u <= stop; ++u)
                {
                    unicodes.Add(u);
                }
            }
        }

        public override void GetUnicodeToGlyphMappings(Dictionary<uint, uint> unicodeToGlyph)
        {
            foreach (var group in Groups)
            {
                for (var character = group.StartCharCode; character <= group.EndCharCode; character++)
                {
                    unicodeToGlyph[character] = group.GlyphId;
                    if (character == uint.MaxValue)
                    {
                        break;
                    }
                }
            }
        }
    }
}