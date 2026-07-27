// Native.cs - P/Invoke declarations.
//
// DefaultDllImportSearchPaths(System32) prevents DLL search-order hijacking:
// dxva2.dll and crypt32-adjacent helpers are loaded strictly from System32,
// so a malicious DLL planted next to the exe can't be picked up instead.
// user32.dll and kernel32.dll are KnownDLLs and don't strictly need it, but
// the attribute is harmless there.

using System;
using System.Runtime.InteropServices;

namespace MonitorSwitch
{
    static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor, ref uint count);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor, uint count, [Out] PHYSICAL_MONITOR[] monitors);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool SetVCPFeature(IntPtr hMonitor, byte code, uint value);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetVCPFeatureAndVCPFeatureReply(
            IntPtr hMonitor, byte code, IntPtr pvct,
            ref uint currentValue, ref uint maxValue);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr rect, IntPtr data);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(
            IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

        // ----- monitor identity (GDI device name -> PnP hardware id) ----------
        // Used to key profile values by physical monitor rather than by
        // enumeration position, so the same profile works on any PC the
        // monitors are plugged into.

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;          // e.g. \\.\DISPLAY1
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;          // e.g. MONITOR\DEL40A8\{...}\0001
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFOEX info);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplayDevicesW(
            string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        // ----- DPAPI (used by AuthStore to encrypt the sync refresh token) -----
        // P/Invoked directly so we don't need the ProtectedData NuGet package.

        [StructLayout(LayoutKind.Sequential)]
        public struct DATA_BLOB
        {
            public int cbData;
            public IntPtr pbData;
        }

        public const uint CRYPTPROTECT_UI_FORBIDDEN = 0x1;

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CryptProtectData(
            ref DATA_BLOB pDataIn, string szDataDescr, IntPtr pOptionalEntropy,
            IntPtr pvReserved, IntPtr pPromptStruct, uint dwFlags, ref DATA_BLOB pDataOut);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CryptUnprotectData(
            ref DATA_BLOB pDataIn, IntPtr ppszDataDescr, IntPtr pOptionalEntropy,
            IntPtr pvReserved, IntPtr pPromptStruct, uint dwFlags, ref DATA_BLOB pDataOut);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll")]
        public static extern IntPtr LocalFree(IntPtr hMem);
    }
}
