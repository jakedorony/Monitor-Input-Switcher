// Ddc.cs - monitor enumeration and VCP 0x60 (input select) read/write.

using System;
using System.Collections.Generic;

namespace MonitorSwitch
{
    static class Ddc
    {
        const byte VCP_INPUT = 0x60;

        public static List<PhysMon> GetMonitors()
        {
            var list = new List<PhysMon>();
            Native.MonitorEnumProc cb = delegate(IntPtr hMon, IntPtr hdc, IntPtr rect, IntPtr data)
            {
                uint count = 0;
                if (Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, ref count) && count > 0)
                {
                    var mons = new Native.PHYSICAL_MONITOR[count];
                    if (Native.GetPhysicalMonitorsFromHMONITOR(hMon, count, mons))
                    {
                        foreach (var m in mons)
                        {
                            list.Add(new PhysMon
                            {
                                Handle = m.hPhysicalMonitor,
                                Description = (m.szPhysicalMonitorDescription ?? "").Trim()
                            });
                        }
                    }
                }
                return true;
            };
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero);
            GC.KeepAlive(cb);
            return list;
        }

        public static void Release(List<PhysMon> monitors)
        {
            foreach (var m in monitors)
                Native.DestroyPhysicalMonitor(m.Handle);
        }

        // Returns number of failures, or -1 if no monitors were found.
        public static int ApplyProfile(Profile p)
        {
            var monitors = GetMonitors();
            int failures = 0;
            try
            {
                if (monitors.Count == 0) return -1;
                for (int i = 0; i < monitors.Count; i++)
                {
                    if (i >= p.Values.Count) continue;   // no value configured for this monitor
                    if (!Native.SetVCPFeature(monitors[i].Handle, VCP_INPUT, p.Values[i]))
                        failures++;
                }
            }
            finally { Release(monitors); }
            return failures;
        }

        public static string DetectInputs()
        {
            var monitors = GetMonitors();
            try
            {
                if (monitors.Count == 0) return "No monitors found.";
                var lines = new List<string>();
                for (int i = 0; i < monitors.Count; i++)
                {
                    uint cur = 0, max = 0;
                    bool ok = Native.GetVCPFeatureAndVCPFeatureReply(
                        monitors[i].Handle, VCP_INPUT, IntPtr.Zero, ref cur, ref max);
                    lines.Add(ok
                        ? string.Format("Monitor {0}: input = {1}", i, cur & 0xFF)
                        : string.Format("Monitor {0}: read failed", i));
                }
                return string.Join("\n", lines);
            }
            finally { Release(monitors); }
        }

        // Reads the current input of every monitor into a value list.
        // Returns null if no monitors, or if any monitor's input can't be read
        // (a partial capture would silently produce a broken profile).
        public static List<uint> CaptureCurrent()
        {
            var monitors = GetMonitors();
            try
            {
                if (monitors.Count == 0) return null;
                var vals = new List<uint>();
                for (int i = 0; i < monitors.Count; i++)
                {
                    uint cur = 0, max = 0;
                    if (!Native.GetVCPFeatureAndVCPFeatureReply(
                            monitors[i].Handle, VCP_INPUT, IntPtr.Zero, ref cur, ref max))
                        return null;
                    vals.Add(cur & 0xFF);
                }
                return vals;
            }
            finally { Release(monitors); }
        }

        // Reads the current input of every monitor. One entry per monitor;
        // -1 means that monitor's input couldn't be read. Empty list = no monitors.
        public static List<int> ReadInputs()
        {
            var monitors = GetMonitors();
            try
            {
                var vals = new List<int>();
                for (int i = 0; i < monitors.Count; i++)
                {
                    uint cur = 0, max = 0;
                    bool ok = Native.GetVCPFeatureAndVCPFeatureReply(
                        monitors[i].Handle, VCP_INPUT, IntPtr.Zero, ref cur, ref max);
                    vals.Add(ok ? (int)(cur & 0xFF) : -1);
                }
                return vals;
            }
            finally { Release(monitors); }
        }
    }
}
