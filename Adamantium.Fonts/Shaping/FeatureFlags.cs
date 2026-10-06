using System;

namespace Adamantium.Fonts.Shaping;

[Flags]
internal enum FeatureFlags
{
    None = 0,
    Global = 0x01,
    HasFallback = 0x02,
    ManualZwnj = 0x04,
    ManualZwj = 0x08,
    ManualJoiners = ManualZwnj | ManualZwj,
}
