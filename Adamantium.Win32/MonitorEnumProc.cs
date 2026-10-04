using System;

namespace Adamantium.Win32;

/// <summary>Called by <see cref="Win32Interop.EnumDisplayMonitors"/> once per monitor; false stops the enumeration.</summary>
public delegate bool MonitorEnumProc(IntPtr monitor, IntPtr deviceContext, ref RECT bounds, IntPtr data);
