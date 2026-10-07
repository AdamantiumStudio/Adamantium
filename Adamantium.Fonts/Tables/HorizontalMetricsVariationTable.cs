using Adamantium.Fonts.Common;
using Adamantium.Fonts.Extensions;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Tables;

internal sealed class HorizontalMetricsVariationTable
{
    private readonly VariationStore store;
    private readonly DeltaSetIndexMap advanceMap;

    private HorizontalMetricsVariationTable(VariationStore store, DeltaSetIndexMap advanceMap)
    {
        this.store = store;
        this.advanceMap = advanceMap;
    }

    public static HorizontalMetricsVariationTable Read(FontStreamReader reader, long offset)
    {
        reader.Position = offset + 4;
        var storeOffset = reader.ReadUInt32();
        var advanceMapOffset = reader.ReadUInt32();
        var store = reader.ReadItemVariationStore(offset + storeOffset);
        var advanceMap = advanceMapOffset == 0 ? null : reader.ReadDeltaSetIndexMap(offset + advanceMapOffset);
        return new HorizontalMetricsVariationTable(store, advanceMap);
    }

    public float GetAdvanceDelta(uint glyphIndex, float[] coordinates)
    {
        var (outer, inner) = advanceMap?.Map(glyphIndex) ?? (0, (int)glyphIndex);
        return store.GetDelta(outer, inner, coordinates);
    }
}
