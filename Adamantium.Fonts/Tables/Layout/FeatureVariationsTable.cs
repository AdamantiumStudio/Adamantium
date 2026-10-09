using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Extensions;

namespace Adamantium.Fonts.Tables.Layout;

internal sealed class FeatureVariationsTable
{
    private readonly List<((int Axis, float Min, float Max)[] Conditions, Dictionary<int, FeatureTable> Substitutions)> records;

    private FeatureVariationsTable(
        List<((int Axis, float Min, float Max)[] Conditions, Dictionary<int, FeatureTable> Substitutions)> records)
    {
        this.records = records;
    }

    public static FeatureVariationsTable Read(FontStreamReader reader, long offset, FeatureTable[] features)
    {
        reader.Position = offset;
        reader.ReadUInt16();
        reader.ReadUInt16();
        var recordCount = reader.ReadUInt32();
        var offsets = new (uint Conditions, uint Substitutions)[recordCount];
        for (var i = 0; i < recordCount; i++)
        {
            offsets[i] = (reader.ReadUInt32(), reader.ReadUInt32());
        }

        var records = new List<((int, float, float)[], Dictionary<int, FeatureTable>)>();
        foreach (var (conditionsOffset, substitutionsOffset) in offsets)
        {
            records.Add((ReadConditions(reader, conditionsOffset == 0 ? -1 : offset + conditionsOffset),
                ReadSubstitutions(reader, substitutionsOffset == 0 ? -1 : offset + substitutionsOffset, features)));
        }

        return new FeatureVariationsTable(records);
    }

    /// <summary>The first record whose conditions the normalized coordinates meet, or -1.</summary>
    public int Find(float[] coordinates)
    {
        if (coordinates == null)
        {
            return -1;
        }

        for (var i = 0; i < records.Count; i++)
        {
            var met = true;
            foreach (var (axis, min, max) in records[i].Conditions)
            {
                var value = axis < coordinates.Length ? coordinates[axis] : 0;
                if (value < min || value > max)
                {
                    met = false;
                    break;
                }
            }

            if (met)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The feature at <paramref name="index"/> as record <paramref name="record"/> has it.</summary>
    public FeatureTable Substitute(int record, int index, FeatureTable feature) =>
        record >= 0 && records[record].Substitutions.TryGetValue(index, out var alternate) ? alternate : feature;

    private static (int, float, float)[] ReadConditions(FontStreamReader reader, long offset)
    {
        if (offset < 0)
        {
            return [];
        }

        reader.Position = offset;
        var count = reader.ReadUInt16();
        var conditionOffsets = new uint[count];
        for (var i = 0; i < count; i++)
        {
            conditionOffsets[i] = reader.ReadUInt32();
        }

        var conditions = new List<(int, float, float)>();
        foreach (var conditionOffset in conditionOffsets)
        {
            reader.Position = offset + conditionOffset;
            if (reader.ReadUInt16() != 1)
            {
                conditions.Add((0, 1, -1));
                continue;
            }

            var axis = reader.ReadUInt16();
            conditions.Add((axis, reader.ReadInt16() / 16384f, reader.ReadInt16() / 16384f));
        }

        return conditions.ToArray();
    }

    private static Dictionary<int, FeatureTable> ReadSubstitutions(FontStreamReader reader, long offset,
        FeatureTable[] features)
    {
        var substitutions = new Dictionary<int, FeatureTable>();
        if (offset < 0)
        {
            return substitutions;
        }

        reader.Position = offset;
        reader.ReadUInt16();
        reader.ReadUInt16();
        var count = reader.ReadUInt16();
        var entries = new (ushort Index, uint Offset)[count];
        for (var i = 0; i < count; i++)
        {
            entries[i] = (reader.ReadUInt16(), reader.ReadUInt32());
        }

        foreach (var (index, featureOffset) in entries)
        {
            if (index >= features.Length)
            {
                continue;
            }

            reader.Position = offset + featureOffset;
            reader.ReadUInt16();
            var lookupCount = reader.ReadUInt16();
            substitutions[index] = new FeatureTable(features[index].Tag)
            {
                FeatureParameters = features[index].FeatureParameters,
                LookupListIndices = reader.ReadUInt16Array(lookupCount),
            };
        }

        return substitutions;
    }
}
