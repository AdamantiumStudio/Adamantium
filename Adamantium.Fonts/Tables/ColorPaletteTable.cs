using Adamantium.Fonts.Common;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Tables;

internal sealed class ColorPaletteTable
{
    private readonly Color[] records;
    private readonly ushort[] firstRecords;
    private readonly int entries;

    private ColorPaletteTable(Color[] records, ushort[] firstRecords, int entries)
    {
        this.records = records;
        this.firstRecords = firstRecords;
        this.entries = entries;
    }

    public int PaletteCount => firstRecords.Length;

    public static ColorPaletteTable Read(FontStreamReader reader, long offset)
    {
        reader.Position = offset + 2;
        var entries = reader.ReadUInt16();
        var palettes = reader.ReadUInt16();
        var recordCount = reader.ReadUInt16();
        var recordsOffset = reader.ReadUInt32();
        var firstRecords = new ushort[palettes];
        for (var i = 0; i < palettes; i++)
        {
            firstRecords[i] = reader.ReadUInt16();
        }

        reader.Position = offset + recordsOffset;
        var records = new Color[recordCount];
        for (var i = 0; i < recordCount; i++)
        {
            var blue = reader.ReadByte();
            var green = reader.ReadByte();
            var red = reader.ReadByte();
            var alpha = reader.ReadByte();
            records[i] = Color.FromRgba(red, green, blue, alpha);
        }

        return new ColorPaletteTable(records, firstRecords, entries);
    }

    public Color? GetColor(int palette, int entry)
    {
        if (palette < 0 || palette >= firstRecords.Length || entry < 0 || entry >= entries)
        {
            return null;
        }

        var index = firstRecords[palette] + entry;
        return index < records.Length ? records[index] : null;
    }
}
