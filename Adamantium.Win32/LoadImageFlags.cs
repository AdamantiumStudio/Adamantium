using System;

namespace Adamantium.Win32;

/// <summary>How <see cref="Win32Interop.LoadImage"/> loads (the LR_* values).</summary>
[Flags]
public enum LoadImageFlags : uint
{
    None = 0,

    /// <summary>A zero width or height takes the system's size for the image type.</summary>
    DefaultSize = 0x40,

    /// <summary>The system keeps the image and hands back the same handle; it must not be destroyed.</summary>
    Shared = 0x8000
}
