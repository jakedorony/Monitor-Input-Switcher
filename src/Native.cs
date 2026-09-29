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

        // ----- DDC/CI capabilities string (which inputs a monitor supports) -----

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetCapabilitiesStringLength(IntPtr hMonitor, ref uint length);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("dxva2.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern bool CapabilitiesRequestAndCapabilitiesReply(
            IntPtr hMonitor, System.Text.StringBuilder buffer, uint length);

        // ----- USB device arrival/removal notifications (dock button) ---------

        public const int WM_DEVICECHANGE = 0x0219;
        public const int DBT_DEVICEARRIVAL = 0x8000;
        public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        public const int DBT_DEVTYP_DEVICEINTERFACE = 5;
        public const int DEVICE_NOTIFY_WINDOW_HANDLE = 0;

        public static readonly Guid GUID_DEVINTERFACE_USB_DEVICE =
            new Guid(0xA5DCBF10, 0x6530, 0x11D2, 0x90, 0x1F, 0x00, 0xC0, 0x4F, 0xB9, 0x51, 0xED);

        // Hubs announce under their own interface class, not USB_DEVICE.
        public static readonly Guid GUID_DEVINTERFACE_USB_HUB =
            new Guid(0xF18A0E88, 0xC30C, 0x11D0, 0x88, 0x15, 0x00, 0xA0, 0xC9, 0x06, 0xBE, 0xD8);

        // Fixed-size head of DEV_BROADCAST_DEVICEINTERFACE_W; the device path
        // string follows in memory at offset 28 and is read with Marshal.
        [StructLayout(LayoutKind.Sequential)]
        public struct DEV_BROADCAST_DEVICEINTERFACE
        {
            public int dbcc_size;
            public int dbcc_devicetype;
            public int dbcc_reserved;
            public Guid dbcc_classguid;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr RegisterDeviceNotificationW(
            IntPtr hRecipient, ref DEV_BROADCAST_DEVICEINTERFACE filter, int flags);

        [DllImport("user32.dll")]
        public static extern bool UnregisterDeviceNotification(IntPtr handle);

        // ----- internal (built-in laptop) panel detection ---------------------
        // QueryDisplayConfig reports each active path's output technology;
        // embedded values (LVDS, eDP, eUDI, INTERNAL) identify the lid panel.

        public const uint QDC_ONLY_ACTIVE_PATHS = 2;
        public const int DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId; public uint id; public uint modeInfoIdx; public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId; public uint id; public uint modeInfoIdx;
            public int outputTechnology; public int rotation; public int scaling;
            public uint refreshNumerator; public uint refreshDenominator;
            public int scanLineOrdering; public int targetAvailable; public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        // 64-byte blob: the mode details are never read, only the array is
        // required by the QueryDisplayConfig signature.
        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_MODE_INFO { public ulong a, b, c, d, e, f, g, h; }

        // DISPLAYCONFIG_DEVICE_INFO_HEADER (type/size/adapterId/id) followed
        // by the source's GDI name (e.g. \\.\DISPLAY1).
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            public int type; public int size; public LUID adapterId; public uint id;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string viewGdiDeviceName;
        }

        [DllImport("user32.dll")]
        public static extern int GetDisplayConfigBufferSizes(
            uint flags, out uint numPaths, out uint numModes);

        [DllImport("user32.dll")]
        public static extern int QueryDisplayConfig(
            uint flags, ref uint numPaths, [Out] DISPLAYCONFIG_PATH_INFO[] paths,
            ref uint numModes, [Out] DISPLAYCONFIG_MODE_INFO[] modes, IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(
            ref DISPLAYCONFIG_SOURCE_DEVICE_NAME request);

        // ----- Dark title bar (dwmapi is not a KnownDLL - keep the attribute) -----

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attribute, ref int value, int size);
    }
}
