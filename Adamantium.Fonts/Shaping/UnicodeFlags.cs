using System;

namespace Adamantium.Fonts.Shaping;

[Flags]
internal enum UnicodeFlags : byte
{
    None = 0,
    Ignorable = 0x01,
    Zwnj = 0x02,
    Zwj = 0x04,
    Hidden = 0x08,
    Continuation = 0x10,
}
