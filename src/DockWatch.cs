// DockWatch.cs - reacts to the dock's KVM button via USB topology changes.
//
// Pressing a KVM dock's switch button detaches its switched USB segment
// from the losing computer and attaches it to the gaining one. Verified on
// the Plugable TBT4-UD5 (2026-08-31): the dock's hub pair (05E3:0610 +
// 05E3:0626) departs ~4s after the press in a burst of 10-25 device events,
// and returns first (peripherals 1-2s later) on switch-back. So: watch for
// arrival/removal of the configured hub signatures, debounce the burst, and
// raise one Departed/Arrived event per press.
//
// A hidden native window receives WM_DEVICECHANGE (registered for the USB
// device interface class); everything runs on the UI thread.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MonitorSwitch
{
    enum DockEvent { None, Departed, Arrived }

    // The debounce/direction core. Pure - no timers, no Windows - so the
    // real captured event timeline can be replayed against it in tests.
    class DockDebounce
    {
        readonly HashSet<string> sigs = new HashSet<string>();
        readonly TimeSpan settle;
        readonly TimeSpan sameDirCooldown;
        bool pendingArrived;
        DateTime pendingAt = DateTime.MinValue;
        DockEvent lastDir = DockEvent.None;
        DateTime lastActionAt = DateTime.MinValue;

        public DockDebounce(IEnumerable<string> signatures, TimeSpan settle, TimeSpan sameDirCooldown)
        {
            foreach (string s in signatures) sigs.Add(s.ToUpperInvariant());
            this.settle = settle;
            this.sameDirCooldown = sameDirCooldown;
        }

        public void Feed(bool arrived, string vidPid, DateTime now)
        {
            if (vidPid == null || !sigs.Contains(vidPid.ToUpperInvariant())) return;
            // Within a burst the LAST matching event decides the direction
            // (a quick away-and-back inside one settle window nets out).
            pendingArrived = arrived;
            pendingAt = now;
        }

        // Call periodically; returns an event once the burst has settled.
        public DockEvent Poll(DateTime now)
        {
            if (pendingAt == DateTime.MinValue || now - pendingAt < settle) return DockEvent.None;
            var dir = pendingArrived ? DockEvent.Arrived : DockEvent.Departed;
            pendingAt = DateTime.MinValue;
            // The same direction twice in quick succession is re-enumeration
            // noise (resume, driver restart) - a real user round-trip
            // alternates directions and is never suppressed.
            if (dir == lastDir && now - lastActionAt < sameDirCooldown) return DockEvent.None;
            lastDir = dir;
            lastActionAt = now;
            return dir;
        }
    }

    static class DockWatch
    {
        // "\\?\USB#VID_05E3&PID_0626#..." -> "05E3:0626"; null if absent.
        public static string ParseVidPid(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath)) return null;
            int v = devicePath.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);
            int p = devicePath.IndexOf("PID_", StringComparison.OrdinalIgnoreCase);
            if (v < 0 || p < 0 || v + 8 > devicePath.Length || p + 8 > devicePath.Length) return null;
            string vid = devicePath.Substring(v + 4, 4), pid = devicePath.Substring(p + 4, 4);
            for (int i = 0; i < 4; i++)
                if (!Uri.IsHexDigit(vid[i]) || !Uri.IsHexDigit(pid[i])) return null;
            return (vid + ":" + pid).ToUpperInvariant();
        }

        // Raw stream for the learn wizard: (arrived, vidPid).
        public static event Action<bool, string> RawEvent;

        // Debounced trigger events for the configured dock.
        public static event Action Departed;
        public static event Action Arrived;

        public static DateTime LastMatchUtc = DateTime.MinValue;

        static NotifyWindow window;
        static Timer pollTimer;
        static DockDebounce debounce;

        // Always safe to call; reconfigures in place. The notification window
        // runs for the app's lifetime once started (the wizard needs RawEvent
        // even while the trigger is disabled).
        public static void Start()
        {
            if (window == null)
            {
                window = new NotifyWindow();
                pollTimer = new Timer { Interval = 400 };
                pollTimer.Tick += delegate
                {
                    if (debounce == null) return;
                    switch (debounce.Poll(DateTime.UtcNow))
                    {
                        case DockEvent.Departed: { var h = Departed; if (h != null) h(); break; }
                        case DockEvent.Arrived: { var h = Arrived; if (h != null) h(); break; }
                    }
                };
                pollTimer.Start();
            }
            Reconfigure();
        }

        // Re-reads ConfigStore.Dock (call after settings change).
        public static void Reconfigure()
        {
            var d = ConfigStore.Dock;
            debounce = (d.Enabled && d.Signatures.Count > 0)
                ? new DockDebounce(d.Signatures, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(10))
                : null;
        }

        static void OnDeviceChange(bool arrived, string devicePath)
        {
            string vidPid = ParseVidPid(devicePath);
            if (vidPid == null) return;
            var raw = RawEvent;
            if (raw != null) raw(arrived, vidPid);
            if (debounce == null) return;
            var d = ConfigStore.Dock;
            foreach (string sig in d.Signatures)
                if (string.Equals(sig, vidPid, StringComparison.OrdinalIgnoreCase))
                { LastMatchUtc = DateTime.UtcNow; break; }
            debounce.Feed(arrived, vidPid, DateTime.UtcNow);
        }

        // Hidden message-only window receiving WM_DEVICECHANGE.
        class NotifyWindow : NativeWindow
        {
            IntPtr registration;

            public NotifyWindow()
            {
                CreateHandle(new CreateParams());
                var filter = new Native.DEV_BROADCAST_DEVICEINTERFACE
                {
                    dbcc_size = Marshal.SizeOf(typeof(Native.DEV_BROADCAST_DEVICEINTERFACE)) + 2,
                    dbcc_devicetype = Native.DBT_DEVTYP_DEVICEINTERFACE,
                    dbcc_classguid = Native.GUID_DEVINTERFACE_USB_DEVICE
                };
                registration = Native.RegisterDeviceNotificationW(
                    Handle, ref filter, Native.DEVICE_NOTIFY_WINDOW_HANDLE);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == Native.WM_DEVICECHANGE && m.LParam != IntPtr.Zero)
                {
                    int evt = (int)m.WParam;
                    if (evt == Native.DBT_DEVICEARRIVAL || evt == Native.DBT_DEVICEREMOVECOMPLETE)
                    {
                        int devType = Marshal.ReadInt32(m.LParam, 4);
                        if (devType == Native.DBT_DEVTYP_DEVICEINTERFACE)
                        {
                            // Path string starts after the 28-byte fixed head.
                            string path = Marshal.PtrToStringUni(
                                (IntPtr)((long)m.LParam + 28)) ?? "";
                            OnDeviceChange(evt == Native.DBT_DEVICEARRIVAL, path);
                        }
                    }
                }
                base.WndProc(ref m);
            }
        }
    }
}
