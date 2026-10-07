using System.Collections.Generic;
using System.Linq;
using Adamantium.Fonts;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.TextureGeneration;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.FontTests;

public class SegmentGridTests
{
    [TestCase("TTFFonts/SourceSans3-Regular.ttf", "@&gQW8%")]
    [TestCase("OTFFonts/SourceSans3-Regular.otf", "@&gQW8%")]
    public void FindsTheSameNearestSegmentsAsTryingEveryOne(string path, string characters)
    {
        var font = Typeface.LoadFont(path, 3).GetFont(0);
        foreach (var character in characters)
        {
            var glyph = font.GetGlyphByCharacter(character);
            glyph.Sample(3);
            var segments = glyph.GetMergedOutlineSegments();
            var grid = new SegmentGrid(segments);
            var bounds = glyph.BoundingRectangle;
            var found = new List<LineSegment2D>();

            for (var y = bounds.Y - 100.0; y <= bounds.Bottom + 100; y += 7.3)
            {
                for (var x = bounds.X - 100.0; x <= bounds.Right + 100; x += 7.3)
                {
                    var point = new Vector2(x, y);
                    grid.FindNearest(point, found);

                    Assert.That(found, Is.EqualTo(Nearest(segments, point)), $"'{character}' at {x}, {y}");
                }
            }
        }
    }

    private static List<LineSegment2D> Nearest(List<LineSegment2D> segments, Vector2 point)
    {
        var best = double.MaxValue;
        var nearest = new List<LineSegment2D>();
        foreach (var segment in segments)
        {
            var distance = GlyphSegmentsMath.GetDistanceToSegment(segment, point);
            if (distance < best)
            {
                best = distance;
                nearest.Clear();
                nearest.Add(segment);
            }
            else if (distance == best)
            {
                nearest.Add(segment);
            }
        }

        return nearest;
    }
}
