namespace Adamantium.Win32;

/// <summary>What <see cref="Win32Interop.LoadImage"/> loads (the IMAGE_* values).</summary>
public enum LoadImageType : uint
{
    Bitmap = 0,
    Icon = 1,
    Cursor = 2
}
