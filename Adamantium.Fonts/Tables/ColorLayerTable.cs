using System;
using Adamantium.Fonts.Common;

namespace Adamantium.Fonts.Tables;

internal sealed class ColorLayerTable
{
    private const ushort ForegroundEntry = 0xFFFF;

    private readonly ushort[] baseGlyphs;
    private readonly ushort[] firstLayers;
    private readonly ushort[] layerCounts;
    private readonly ushort[] layerGlyphs;
    private readonly ushort[] layerEntries;

    private ColorLayerTable(ushort[] baseGlyphs, ushort[] firstLayers, ushort[] layerCounts, ushort[] layerGlyphs,
        ushort[] layerEntries)
    {
        this.baseGlyphs = baseGlyphs;
        this.firstLayers = firstLayers;
        this.layerCounts = layerCounts;
        this.layerGlyphs = layerGlyphs;
        this.layerEntries = layerEntries;
    }

    public static ColorLayerTable Read(FontStreamReader reader, long offset)
    {
        reader.Position = offset + 2;
        var baseCount = reader.ReadUInt16();
        var baseOffset = reader.ReadUInt32();
        var layerOffset = reader.ReadUInt32();
        var layerCount = reader.ReadUInt16();

        reader.Position = offset + baseOffset;
        var baseGlyphs = new ushort[baseCount];
        var firstLayers = new ushort[baseCount];
        var layerCounts = new ushort[baseCount];
        for (var i = 0; i < baseCount; i++)
        {
            baseGlyphs[i] = reader.ReadUInt16();
            firstLayers[i] = reader.ReadUInt16();
            layerCounts[i] = reader.ReadUInt16();
        }

        reader.Position = offset + layerOffset;
        var layerGlyphs = new ushort[layerCount];
        var layerEntries = new ushort[layerCount];
        for (var i = 0; i < layerCount; i++)
        {
            layerGlyphs[i] = reader.ReadUInt16();
            layerEntries[i] = reader.ReadUInt16();
        }

        return new ColorLayerTable(baseGlyphs, firstLayers, layerCounts, layerGlyphs, layerEntries);
    }

    public ColorLayer[] GetLayers(uint glyphIndex, ColorPaletteTable palettes, int palette)
    {
        var low = 0;
        var high = baseGlyphs.Length - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (baseGlyphs[middle] < glyphIndex)
            {
                low = middle + 1;
            }
            else if (baseGlyphs[middle] > glyphIndex)
            {
                high = middle - 1;
            }
            else
            {
                return Layers(middle, palettes, palette);
            }
        }

        return [];
    }

    private ColorLayer[] Layers(int record, ColorPaletteTable palettes, int palette)
    {
        var first = firstLayers[record];
        var count = Math.Min(layerCounts[record], layerGlyphs.Length - first);
        var layers = new ColorLayer[Math.Max(0, count)];
        for (var i = 0; i < layers.Length; i++)
        {
            var entry = layerEntries[first + i];
            var color = entry == ForegroundEntry ? null : palettes?.GetColor(palette, entry);
            layers[i] = new ColorLayer(layerGlyphs[first + i], color);
        }

        return layers;
    }
}
