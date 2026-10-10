using Adamantium.Core.TypeParsing;

namespace Adamantium.Graphics.Fonts;

/// <summary>Turns <c>WordSpacing="80% 100% 133%"</c> in markup into a <see cref="SpacingRange"/>.</summary>
public class SpacingRangeParser : ITypeParser<SpacingRange>
{
    public SpacingRange Parse(string value) => SpacingRange.Parse(value);
}
