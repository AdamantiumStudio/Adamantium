using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Extensions;

internal static partial class FontStreamExtensions
{
    private const ushort LongWords = 0x8000;
    private const ushort WordCountMask = 0x7FFF;

    public static VariationStore ReadItemVariationStore(this FontStreamReader reader, long offset)
    {
        reader.Position = offset;
        reader.ReadUInt16();
        var regionListOffset = reader.ReadUInt32();
        var dataCount = reader.ReadUInt16();
        var dataOffsets = new uint[dataCount];
        for (var i = 0; i < dataCount; ++i)
        {
            dataOffsets[i] = reader.ReadUInt32();
        }

        reader.Position = offset + regionListOffset;
        var regionList = new VariationRegionList
        {
            AxisCount = reader.ReadUInt16(),
            RegionCount = reader.ReadUInt16()
        };
        regionList.VariationRegions = new VariationRegion[regionList.RegionCount];
        for (var r = 0; r < regionList.RegionCount; r++)
        {
            var axes = new RegionAxisCoordinates[regionList.AxisCount];
            for (var a = 0; a < axes.Length; a++)
            {
                axes[a] = new RegionAxisCoordinates
                {
                    StartCoord = reader.ReadInt16().FromF2Dot14(),
                    PeakCoord = reader.ReadInt16().FromF2Dot14(),
                    EndCoord = reader.ReadInt16().FromF2Dot14()
                };
            }

            regionList.VariationRegions[r] = new VariationRegion { RegionAxes = axes };
        }

        var data = new ItemVariationDataSubtable[dataCount];
        for (var i = 0; i < dataCount; ++i)
        {
            reader.Position = offset + dataOffsets[i];
            var subtable = new ItemVariationDataSubtable
            {
                ItemCount = reader.ReadUInt16(),
                ShortDeltaCount = reader.ReadUInt16(),
                RegionIndexCount = reader.ReadUInt16()
            };
            subtable.RegionIndices = reader.ReadUInt16Array(subtable.RegionIndexCount);
            subtable.DeltaSets = new DeltaSet[subtable.ItemCount];
            var longWords = (subtable.ShortDeltaCount & LongWords) != 0;
            var wordCount = subtable.ShortDeltaCount & WordCountMask;
            for (var k = 0; k < subtable.ItemCount; ++k)
            {
                var deltas = new int[subtable.RegionIndexCount];
                for (var d = 0; d < deltas.Length; d++)
                {
                    if (d < wordCount)
                    {
                        deltas[d] = longWords ? reader.ReadInt32() : reader.ReadInt16();
                    }
                    else
                    {
                        deltas[d] = longWords ? reader.ReadInt16() : reader.ReadSignedByte();
                    }
                }

                subtable.DeltaSets[k] = new DeltaSet { Deltas = deltas };
            }

            data[i] = subtable;
        }

        return new VariationStore(regionList, data);
    }

    public static DeltaSetIndexMap ReadDeltaSetIndexMap(this FontStreamReader reader, long offset)
    {
        reader.Position = offset;
        var format = reader.ReadByte();
        var entryFormat = reader.ReadByte();
        var count = format == 0 ? reader.ReadUInt16() : reader.ReadUInt32();
        var entrySize = ((entryFormat >> 4) & 0x3) + 1;
        var innerBits = (entryFormat & 0xF) + 1;
        var outer = new ushort[count];
        var inner = new ushort[count];
        for (var i = 0; i < count; i++)
        {
            uint entry = 0;
            for (var b = 0; b < entrySize; b++)
            {
                entry = (entry << 8) | reader.ReadByte();
            }

            outer[i] = (ushort)(entry >> innerBits);
            inner[i] = (ushort)(entry & ((1u << innerBits) - 1));
        }

        return new DeltaSetIndexMap(outer, inner);
    }
}
