using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.Win32;

/// <summary>A window's show state and its restored rectangle (WINDOWPLACEMENT), filled by
/// <see cref="Win32Interop.GetWindowPlacement"/>. <see cref="NormalPosition"/> is in workspace coordinates: offset from
/// screen coordinates by whatever the taskbar takes off the top or left of the monitor. <see cref="Length"/> must be set
/// to the struct's size before the call.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct WINDOWPLACEMENT
{
    public int Length;
    public int Flags;
    public int ShowCommand;
    public NativePoint MinPosition;
    public NativePoint MaxPosition;
    public RECT NormalPosition;
}
