namespace Adamantium.Fonts.Shaping;

internal sealed class FeatureRequest
{
    public string Tag;
    public FeatureFlags Flags;
    public uint MaxValue;
    public uint DefaultValue;
    public int GsubStage;
    public int GposStage;
    public int Order;
}
