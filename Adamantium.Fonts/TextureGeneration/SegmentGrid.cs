using System;
using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.TextureGeneration;

/// <summary>The segments of an outline in a uniform grid, each cell holding its own copy of the segments it touches,
/// for <see cref="SegmentSearch"/> to find the nearest ones.</summary>
internal sealed class SegmentGrid
{
    /// <summary>How many channels a segment can belong to: red, green, blue and the true distance.</summary>
    public const int ChannelCount = 4;

    /// <summary>The bit of the true distance's channel.</summary>
    public const int TrueDistanceChannel = 8;

    /// <summary>The bits of the red, green and blue channels.</summary>
    public const int ColorChannels = 7;

    /// <summary>The bits of every channel.</summary>
    public const int AllChannels = 15;

    private const double SegmentsPerCell = 4;

    private readonly double left;
    private readonly double bottom;
    private readonly double cellSize;

    /// <summary>Puts segments in the grid; bit c of a segment's channels says it belongs to channel c.</summary>
    public SegmentGrid(IReadOnlyList<LineSegment2D> segments, IReadOnlyList<int> channels)
    {
        var count = segments.Count;
        Segments = new LineSegment2D[count];
        var sets = new List<int>();
        for (var s = 0; s < count; s++)
        {
            Segments[s] = segments[s];
            PresentChannels |= channels[s];
            if (!sets.Contains(channels[s]))
            {
                sets.Add(channels[s]);
            }
        }

        ChannelSets = sets.ToArray();
        if (count == 0)
        {
            Columns = 1;
            Rows = 1;
            CellStart = [0, 0];
            EntrySegment = [];
            EntryChannels = [];
            StartX = [];
            StartY = [];
            DirectionX = [];
            DirectionY = [];
            InverseLengthSquared = [];
            return;
        }

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        foreach (var segment in Segments)
        {
            minX = Math.Min(minX, Math.Min(segment.Start.X, segment.End.X));
            minY = Math.Min(minY, Math.Min(segment.Start.Y, segment.End.Y));
            maxX = Math.Max(maxX, Math.Max(segment.Start.X, segment.End.X));
            maxY = Math.Max(maxY, Math.Max(segment.Start.Y, segment.End.Y));
        }

        var side = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count / SegmentsPerCell)));
        cellSize = Math.Max(Math.Max(maxX - minX, maxY - minY) / side, 1e-6);
        left = minX;
        bottom = minY;
        Columns = Math.Max(1, (int)Math.Ceiling((maxX - minX) / cellSize) + 1);
        Rows = Math.Max(1, (int)Math.Ceiling((maxY - minY) / cellSize) + 1);

        var cells = Columns * Rows;
        CellStart = new int[cells + 1];
        for (var s = 0; s < count; s++)
        {
            CellRange(Segments[s], out var x0, out var x1, out var y0, out var y1);
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    CellStart[y * Columns + x + 1]++;
                }
            }
        }

        for (var cell = 0; cell < cells; cell++)
        {
            CellStart[cell + 1] += CellStart[cell];
        }

        var entries = CellStart[cells];
        EntrySegment = new int[entries];
        EntryChannels = new int[entries];
        StartX = new double[entries];
        StartY = new double[entries];
        DirectionX = new double[entries];
        DirectionY = new double[entries];
        InverseLengthSquared = new double[entries];

        var filled = new int[cells];
        for (var s = 0; s < count; s++)
        {
            var segment = Segments[s];
            var directionX = segment.End.X - segment.Start.X;
            var directionY = segment.End.Y - segment.Start.Y;
            var lengthSquared = directionX * directionX + directionY * directionY;
            CellRange(segment, out var x0, out var x1, out var y0, out var y1);
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var cell = y * Columns + x;
                    var entry = CellStart[cell] + filled[cell]++;
                    EntrySegment[entry] = s;
                    EntryChannels[entry] = channels[s];
                    StartX[entry] = segment.Start.X;
                    StartY[entry] = segment.Start.Y;
                    DirectionX[entry] = directionX;
                    DirectionY[entry] = directionY;
                    InverseLengthSquared[entry] = lengthSquared > 0 ? 1 / lengthSquared : 0;
                }
            }
        }
    }

    /// <summary>The segments, by their index.</summary>
    public LineSegment2D[] Segments { get; }

    /// <summary>The channels any segment belongs to.</summary>
    public int PresentChannels { get; }

    /// <summary>Each different set of channels a segment belongs to.</summary>
    public int[] ChannelSets { get; }

    /// <summary>The grid's width in cells.</summary>
    public int Columns { get; }

    /// <summary>The grid's height in cells.</summary>
    public int Rows { get; }

    /// <summary>Where each cell's entries begin; the next cell's start is where they end.</summary>
    public int[] CellStart { get; }

    /// <summary>Each entry's segment index.</summary>
    public int[] EntrySegment { get; }

    /// <summary>Each entry's channels.</summary>
    public int[] EntryChannels { get; }

    /// <summary>Each entry's start x.</summary>
    public double[] StartX { get; }

    /// <summary>Each entry's start y.</summary>
    public double[] StartY { get; }

    /// <summary>Each entry's direction x: end minus start.</summary>
    public double[] DirectionX { get; }

    /// <summary>Each entry's direction y.</summary>
    public double[] DirectionY { get; }

    /// <summary>One over each entry's squared length, or 0 for a point.</summary>
    public double[] InverseLengthSquared { get; }

    /// <summary>The column of a point's x, clamped to the grid.</summary>
    public int Column(double x) => Math.Max(0, Math.Min(Columns - 1, (int)Math.Floor((x - left) / cellSize)));

    /// <summary>The row of a point's y, clamped to the grid.</summary>
    public int Row(double y) => Math.Max(0, Math.Min(Rows - 1, (int)Math.Floor((y - bottom) / cellSize)));

    private void CellRange(LineSegment2D segment, out int x0, out int x1, out int y0, out int y1)
    {
        x0 = Column(Math.Min(segment.Start.X, segment.End.X));
        x1 = Column(Math.Max(segment.Start.X, segment.End.X));
        y0 = Row(Math.Min(segment.Start.Y, segment.End.Y));
        y1 = Row(Math.Max(segment.Start.Y, segment.End.Y));
    }
}
