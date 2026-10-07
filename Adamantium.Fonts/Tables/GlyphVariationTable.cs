using System;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Extensions;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Tables;

internal sealed class GlyphVariationTable
{
    private const int LongOffsets = 0x0001;
    private const int SharedPointNumbers = 0x8000;
    private const int TupleCountMask = 0x0FFF;
    private const int EmbeddedPeakTuple = 0x8000;
    private const int IntermediateRegion = 0x4000;
    private const int PrivatePointNumbers = 0x2000;
    private const int TupleIndexMask = 0x0FFF;
    private const int PointsAreWords = 0x80;
    private const int PointRunCountMask = 0x7F;
    private const int DeltasAreZero = 0x80;
    private const int DeltasAreWords = 0x40;
    private const int DeltaRunCountMask = 0x3F;

    private readonly byte[] data;
    private readonly int axisCount;
    private readonly float[][] sharedTuples;
    private readonly long dataArrayOffset;
    private readonly uint[] glyphDataOffsets;

    private GlyphVariationTable(byte[] data, int axisCount, float[][] sharedTuples, long dataArrayOffset,
        uint[] glyphDataOffsets)
    {
        this.data = data;
        this.axisCount = axisCount;
        this.sharedTuples = sharedTuples;
        this.dataArrayOffset = dataArrayOffset;
        this.glyphDataOffsets = glyphDataOffsets;
    }

    public static GlyphVariationTable Read(FontStreamReader reader, long offset)
    {
        reader.Position = offset + 4;
        var axisCount = reader.ReadUInt16();
        var sharedTupleCount = reader.ReadUInt16();
        var sharedTuplesOffset = reader.ReadUInt32();
        var glyphCount = reader.ReadUInt16();
        var flags = reader.ReadUInt16();
        var dataArrayOffset = reader.ReadUInt32();
        var glyphDataOffsets = new uint[glyphCount + 1];
        for (var i = 0; i < glyphDataOffsets.Length; i++)
        {
            glyphDataOffsets[i] = (flags & LongOffsets) != 0 ? reader.ReadUInt32() : reader.ReadUInt16() * 2u;
        }

        reader.Position = offset + sharedTuplesOffset;
        var sharedTuples = new float[sharedTupleCount][];
        for (var i = 0; i < sharedTupleCount; i++)
        {
            sharedTuples[i] = ReadTuple(reader, axisCount);
        }

        return new GlyphVariationTable(reader.GetBuffer(), axisCount, sharedTuples, offset + dataArrayOffset,
            glyphDataOffsets);
    }

    public void Apply(uint glyphIndex, float[] coordinates, double[] xs, double[] ys, int[] contourEnds)
    {
        if (glyphIndex + 1 >= glyphDataOffsets.Length || glyphDataOffsets[glyphIndex] == glyphDataOffsets[glyphIndex + 1])
        {
            return;
        }

        using var reader = new FontStreamReader(data);
        var start = dataArrayOffset + glyphDataOffsets[glyphIndex];
        reader.Position = start;
        var countField = reader.ReadUInt16();
        var serialized = start + reader.ReadUInt16();
        var header = reader.Position;

        var pointCount = xs.Length;
        int[] sharedPoints = null;
        if ((countField & SharedPointNumbers) != 0)
        {
            reader.Position = serialized;
            sharedPoints = ReadPointNumbers(reader);
            serialized = reader.Position;
        }

        var totalX = new double[pointCount];
        var totalY = new double[pointCount];
        var tupleX = new double[pointCount];
        var tupleY = new double[pointCount];
        var touched = new bool[pointCount];
        for (var t = 0; t < (countField & TupleCountMask); t++)
        {
            reader.Position = header;
            var size = reader.ReadUInt16();
            var tupleIndex = reader.ReadUInt16();
            var peak = (tupleIndex & EmbeddedPeakTuple) != 0
                ? ReadTuple(reader, axisCount)
                : sharedTuples[tupleIndex & TupleIndexMask];
            float[] intermediateStart = null;
            float[] intermediateEnd = null;
            if ((tupleIndex & IntermediateRegion) != 0)
            {
                intermediateStart = ReadTuple(reader, axisCount);
                intermediateEnd = ReadTuple(reader, axisCount);
            }

            header = reader.Position;
            var scalar = GetScalar(peak, intermediateStart, intermediateEnd, coordinates);
            if (scalar != 0)
            {
                reader.Position = serialized;
                var points = (tupleIndex & PrivatePointNumbers) != 0 ? ReadPointNumbers(reader) : sharedPoints;
                var count = points?.Length ?? pointCount;
                var deltaX = ReadDeltas(reader, count);
                var deltaY = ReadDeltas(reader, count);
                if (points == null)
                {
                    for (var i = 0; i < pointCount; i++)
                    {
                        totalX[i] += deltaX[i] * scalar;
                        totalY[i] += deltaY[i] * scalar;
                    }
                }
                else
                {
                    Array.Clear(tupleX, 0, pointCount);
                    Array.Clear(tupleY, 0, pointCount);
                    Array.Clear(touched, 0, pointCount);
                    for (var i = 0; i < points.Length; i++)
                    {
                        if (points[i] < pointCount)
                        {
                            tupleX[points[i]] += deltaX[i] * scalar;
                            tupleY[points[i]] += deltaY[i] * scalar;
                            touched[points[i]] = true;
                        }
                    }

                    if (contourEnds != null)
                    {
                        InterpolateUntouched(contourEnds, xs, tupleX, touched);
                        InterpolateUntouched(contourEnds, ys, tupleY, touched);
                    }

                    for (var i = 0; i < pointCount; i++)
                    {
                        totalX[i] += tupleX[i];
                        totalY[i] += tupleY[i];
                    }
                }
            }

            serialized += size;
        }

        for (var i = 0; i < pointCount; i++)
        {
            xs[i] += totalX[i];
            ys[i] += totalY[i];
        }
    }

    private static float GetScalar(float[] peak, float[] intermediateStart, float[] intermediateEnd, float[] coordinates)
    {
        var scalar = 1f;
        for (var axis = 0; axis < peak.Length && scalar != 0; axis++)
        {
            var start = intermediateStart?[axis] ?? Math.Min(0, peak[axis]);
            var end = intermediateEnd?[axis] ?? Math.Max(0, peak[axis]);
            var coordinate = axis < coordinates.Length ? coordinates[axis] : 0;
            scalar *= RegionAxisCoordinates.GetScalar(start, peak[axis], end, coordinate);
        }

        return scalar;
    }

    private static void InterpolateUntouched(int[] contourEnds, double[] original, double[] deltas, bool[] touched)
    {
        var first = 0;
        foreach (var last in contourEnds)
        {
            var firstTouched = -1;
            for (var i = first; i <= last; i++)
            {
                if (touched[i])
                {
                    firstTouched = i;
                    break;
                }
            }

            if (firstTouched >= 0)
            {
                var current = firstTouched;
                do
                {
                    var next = current == last ? first : current + 1;
                    while (!touched[next])
                    {
                        next = next == last ? first : next + 1;
                    }

                    for (var i = current == last ? first : current + 1; i != next; i = i == last ? first : i + 1)
                    {
                        deltas[i] = InferDelta(original[i], original[current], original[next], deltas[current],
                            deltas[next]);
                    }

                    current = next;
                }
                while (current != firstTouched);
            }

            first = last + 1;
        }
    }

    private static double InferDelta(double target, double previous, double next, double previousDelta,
        double nextDelta)
    {
        if (previous == next)
        {
            return previousDelta == nextDelta ? previousDelta : 0;
        }

        var (low, high, lowDelta, highDelta) = previous < next
            ? (previous, next, previousDelta, nextDelta)
            : (next, previous, nextDelta, previousDelta);
        if (target <= low)
        {
            return lowDelta;
        }

        if (target >= high)
        {
            return highDelta;
        }

        return lowDelta + (target - low) * (highDelta - lowDelta) / (high - low);
    }

    private static float[] ReadTuple(FontStreamReader reader, int axisCount)
    {
        var tuple = new float[axisCount];
        for (var axis = 0; axis < axisCount; axis++)
        {
            tuple[axis] = reader.ReadInt16().FromF2Dot14();
        }

        return tuple;
    }

    private static int[] ReadPointNumbers(FontStreamReader reader)
    {
        int count = reader.ReadByte();
        if (count == 0)
        {
            return null;
        }

        if ((count & PointsAreWords) != 0)
        {
            count = ((count & PointRunCountMask) << 8) | reader.ReadByte();
        }

        var points = new int[count];
        var point = 0;
        var i = 0;
        while (i < count)
        {
            var control = reader.ReadByte();
            var run = (control & PointRunCountMask) + 1;
            for (var r = 0; r < run && i < count; r++)
            {
                point += (control & PointsAreWords) != 0 ? reader.ReadUInt16() : reader.ReadByte();
                points[i++] = point;
            }
        }

        return points;
    }

    private static int[] ReadDeltas(FontStreamReader reader, int count)
    {
        var deltas = new int[count];
        var i = 0;
        while (i < count)
        {
            var control = reader.ReadByte();
            var run = (control & DeltaRunCountMask) + 1;
            for (var r = 0; r < run && i < count; r++)
            {
                deltas[i++] = (control & (DeltasAreZero | DeltasAreWords)) switch
                {
                    DeltasAreZero | DeltasAreWords => reader.ReadInt32(),
                    DeltasAreZero => 0,
                    DeltasAreWords => reader.ReadInt16(),
                    _ => reader.ReadSignedByte(),
                };
            }
        }

        return deltas;
    }
}
