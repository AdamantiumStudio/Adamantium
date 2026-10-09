using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Extensions;
using Adamantium.Fonts.Tables.CFF;


namespace Adamantium.Fonts.Tables;

internal sealed class MetricsVariationTable
{
    private readonly VariationStore store;
    private readonly Dictionary<string, (int Outer, int Inner)> records;

    private MetricsVariationTable(VariationStore store, Dictionary<string, (int Outer, int Inner)> records)
    {
        this.store = store;
        this.records = records;
    }

    public static MetricsVariationTable Read(FontStreamReader reader, long offset)
    {
        reader.Position = offset;
        reader.ReadUInt16();
        reader.ReadUInt16();
        reader.ReadUInt16();
        var recordSize = reader.ReadUInt16();
        var recordCount = reader.ReadUInt16();
        var storeOffset = reader.ReadUInt16();
        var records = new Dictionary<string, (int Outer, int Inner)>();
        for (var i = 0; i < recordCount; i++)
        {
            reader.Position = offset + 12 + i * recordSize;
            var tag = reader.ReadString(4);
            records[tag] = (reader.ReadUInt16(), reader.ReadUInt16());
        }

        var store = storeOffset == 0 ? null : reader.ReadItemVariationStore(offset + storeOffset);
        return new MetricsVariationTable(store, records);
    }

    public float GetDelta(string tag, float[] coordinates) =>
        store != null && records.TryGetValue(tag, out var record) ? store.GetDelta(record.Outer, record.Inner, coordinates) : 0;
}
