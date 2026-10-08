namespace Adamantium.Graphics.Fonts;

internal sealed record ColorBitmapCell(uint Layer, int X, int Y, int Width, int Height, int CellWidth, int CellHeight,
    int Levels);
