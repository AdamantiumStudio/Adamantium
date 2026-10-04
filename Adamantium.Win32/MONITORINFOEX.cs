using System.Runtime.InteropServices;

namespace Adamantium.Win32;

/// <summary>A monitor's rectangles and device name (MONITORINFOEXW), filled by <see cref="Win32Interop.GetMonitorInfo"/>.
/// <see cref="Size"/> must be set to the struct's size before the call.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct MONITORINFOEX
{
    /// <summary>MONITORINFOF_PRIMARY in <see cref="Flags"/>.</summary>
    public const uint PrimaryFlag = 1;

    public int Size;
    public RECT Monitor;
    public RECT WorkArea;
    public uint Flags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;
}
