namespace Adamantium.Fonts.Text;

internal sealed class ComposerNode
{
    public ComposerNode(int position, int line, int fitness, double total, ComposerNode previous, bool flagged)
    {
        Position = position;
        Line = line;
        Fitness = fitness;
        Total = total;
        Previous = previous;
        Flagged = flagged;
    }

    public int Position { get; }

    public int Line { get; }

    public int Fitness { get; }

    public double Total { get; }

    public ComposerNode Previous { get; }

    public bool Flagged { get; }
}
