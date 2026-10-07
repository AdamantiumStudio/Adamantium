using System;
using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.TextureGeneration;

internal sealed class SegmentGrid
{
    private readonly LineSegment2D[] segments;
    private readonly List<int>[] cells;
    private readonly int[] visited;
    private readonly List<int> ties = [];
    private readonly double left;
    private readonly double bottom;
    private readonly double cellSize;
    private readonly int columns;
    private readonly int rows;
    private int stamp;

    public SegmentGrid(List<LineSegment2D> segments)
    {
        this.segments = segments.ToArray();
        visited = new int[this.segments.Length];
        if (this.segments.Length == 0)
        {
            cells = [];
            return;
        }

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        foreach (var segment in this.segments)
        {
            minX = Math.Min(minX, Math.Min(segment.Start.X, segment.End.X));
            minY = Math.Min(minY, Math.Min(segment.Start.Y, segment.End.Y));
            maxX = Math.Max(maxX, Math.Max(segment.Start.X, segment.End.X));
            maxY = Math.Max(maxY, Math.Max(segment.Start.Y, segment.End.Y));
        }

        var side = (int)Math.Ceiling(Math.Sqrt(this.segments.Length));
        cellSize = Math.Max(Math.Max(maxX - minX, maxY - minY) / side, 1e-6);
        left = minX;
        bottom = minY;
        columns = Math.Max(1, (int)Math.Ceiling((maxX - minX) / cellSize) + 1);
        rows = Math.Max(1, (int)Math.Ceiling((maxY - minY) / cellSize) + 1);
        cells = new List<int>[columns * rows];

        for (var s = 0; s < this.segments.Length; s++)
        {
            var segment = this.segments[s];
            var x0 = Column(Math.Min(segment.Start.X, segment.End.X));
            var x1 = Column(Math.Max(segment.Start.X, segment.End.X));
            var y0 = Row(Math.Min(segment.Start.Y, segment.End.Y));
            var y1 = Row(Math.Max(segment.Start.Y, segment.End.Y));
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    (cells[y * columns + x] ??= []).Add(s);
                }
            }
        }
    }

    public void FindNearest(Vector2 point, List<LineSegment2D> nearest)
    {
        nearest.Clear();
        if (segments.Length == 0)
        {
            return;
        }

        stamp++;
        ties.Clear();
        var best = double.MaxValue;
        var cx = Column(point.X);
        var cy = Row(point.Y);
        var maxRing = Math.Max(Math.Max(cx, columns - 1 - cx), Math.Max(cy, rows - 1 - cy));
        for (var ring = 0; ring <= maxRing && (ring - 1) * cellSize <= best; ring++)
        {
            for (var y = Math.Max(0, cy - ring); y <= Math.Min(rows - 1, cy + ring); y++)
            {
                var onEdge = y == cy - ring || y == cy + ring;
                var step = onEdge ? 1 : 2 * ring;
                for (var x = cx - ring; x <= cx + ring; x += Math.Max(step, 1))
                {
                    if (x >= 0 && x < columns)
                    {
                        Visit(cells[y * columns + x], point, ref best);
                    }
                }
            }
        }

        ties.Sort();
        foreach (var index in ties)
        {
            nearest.Add(segments[index]);
        }
    }

    private void Visit(List<int> cell, Vector2 point, ref double best)
    {
        if (cell == null)
        {
            return;
        }

        foreach (var index in cell)
        {
            if (visited[index] == stamp)
            {
                continue;
            }

            visited[index] = stamp;
            var distance = GlyphSegmentsMath.GetDistanceToSegment(segments[index], point);
            if (distance < best)
            {
                best = distance;
                ties.Clear();
                ties.Add(index);
            }
            else if (distance == best)
            {
                ties.Add(index);
            }
        }
    }

    private int Column(double x) => Math.Max(0, Math.Min(columns - 1, (int)Math.Floor((x - left) / cellSize)));

    private int Row(double y) => Math.Max(0, Math.Min(rows - 1, (int)Math.Floor((y - bottom) / cellSize)));
}
