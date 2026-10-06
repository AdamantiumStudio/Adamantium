namespace Adamantium.Fonts.Shaping;

internal sealed class FeatureMap
{
    public string Tag;
    public int GsubIndex = -1;
    public int GposIndex = -1;
    public int GsubStage;
    public int GposStage;
    public uint Mask;
    public int Shift;
    public bool AutoZwnj;
    public bool AutoZwj;
}
