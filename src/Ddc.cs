// Ddc.cs - monitor enumeration (with hardware identity) and VCP 0x60
// (input select) read/write.

using System;
using System.Collections.Generic;

namespace MonitorSwitch
{
    // Live input state of one monitor. Value -1 = couldn't read.
    class MonitorInput
    {
        public string Id;
        public int Value;
    }

    static class Ddc
    {
        const byte VCP_INPUT = 0x60;

        public static List<PhysMon> GetMonitors()
        {
            var list = new List<PhysMon>();
            var idCounts = new Dictionary<string, int>();

            Native.MonitorEnumProc cb = delegate(IntPtr hMon, IntPtr hdc, IntPtr rect, IntPtr data)
            {
                uint count = 0;
                if (Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, ref count) && count > 0)
                {
                    var mons = new Native.PHYSICAL_MONITOR[count];
                    if (Native.GetPhysicalMonitorsFromHMONITOR(hMon, count, mons))
                    {
                        string device = GdiDeviceName(hMon);
                        for (uint j = 0; j < mons.Length; j++)
                        {
                            string rawId = HardwareId(device, j);

                            // Disambiguate identical models: first stays bare,
                            // repeats get #2, #3... in enumeration order.
                            string id = rawId;
                            if (rawId.Length > 0)
                            {
                                int seen;
                                idCounts.TryGetValue(rawId, out seen);
                                idCounts[rawId] = seen + 1;
                                if (seen > 0) id = rawId + "#" + (seen + 1);
                            }

                            list.Add(new PhysMon
                            {
                                Handle = mons[j].hPhysicalMonitor,
                                Description = (mons[j].szPhysicalMonitorDescription ?? "").Trim(),
                                Id = id
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

        static string GdiDeviceName(IntPtr hMon)
        {
            var info = new Native.MONITORINFOEX();
            info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.MONITORINFOEX));
            return Native.GetMonitorInfoW(hMon, ref info) ? info.szDevice : null;
        }

        // PnP hardware id for the iDevNum-th monitor on a GDI display device,
        // e.g. "MONITOR\DEL40A8\{...}\0001" -> "DEL40A8". USUALLY the same
        // for a given physical monitor on any PC, but NOT guaranteed: some
        // panels report a different product code per input (a Dell here shows
        // DELA07A on one machine and DELA07B on another), which is why Plan()
        // needs its leftover-pairing fallback. "" if it can't be determined.
        static string HardwareId(string gdiDevice, uint iDevNum)
        {
            if (string.IsNullOrEmpty(gdiDevice)) return "";
            var dd = new Native.DISPLAY_DEVICE();
            dd.cb = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.DISPLAY_DEVICE));
            if (!Native.EnumDisplayDevicesW(gdiDevice, iDevNum, ref dd, 0)) return "";
            string devId = dd.DeviceID ?? "";
            string[] parts = devId.Split('\\');
            return parts.Length >= 2 ? parts[1].Trim() : "";
        }

        public static void Release(List<PhysMon> monitors)
        {
            foreach (var m in monitors)
                Native.DestroyPhysicalMonitor(m.Handle);
        }

        // Result of applying a profile, detailed enough to tell the user the
        // truth (a monitor we could not match is NOT a success).
        public class ApplyOutcome
        {
            public bool NoMonitors;
            public int Applied;         // writes accepted
            public int Failures;        // SetVCPFeature rejected the write
            public int Unmatched;       // connected monitors with nothing to apply
            public int Paired;          // matched by leftover pairing, not by id
        }

        // Decides which saved input goes to which connected monitor.
        //
        // 1. Exact hardware-id match - the normal path.
        // 2. Legacy positional entries (MonitorId null) by position.
        // 3. Leftover pairing: any monitor still unmatched takes the next
        //    unused entry, in order.
        //
        // Step 3 exists because a monitor's PnP id is NOT always stable across
        // PCs: the same Dell panel reports DELA07A on one machine and DELA07B
        // on another. Without it that monitor silently never switches on the
        // second PC. Pairing only ever consumes entries no monitor claimed by
        // id, so exact matches always win.
        public static void Plan(Profile p, List<PhysMon> mons,
            out uint[] values, out bool[] hasValue, out int paired, out int unmatched)
        {
            values = new uint[mons.Count];
            hasValue = new bool[mons.Count];
            var entryUsed = new bool[p.Inputs.Count];
            paired = 0;
            unmatched = 0;

            for (int i = 0; i < mons.Count; i++)
            {
                if (mons[i].Id.Length == 0) continue;
                for (int e = 0; e < p.Inputs.Count; e++)
                {
                    if (entryUsed[e] || p.Inputs[e].MonitorId != mons[i].Id) continue;
                    values[i] = p.Inputs[e].Value;
                    hasValue[i] = true;
                    entryUsed[e] = true;
                    break;
                }
            }

            for (int i = 0; i < mons.Count && i < p.Inputs.Count; i++)
            {
                if (hasValue[i] || entryUsed[i] || p.Inputs[i].MonitorId != null) continue;
                values[i] = p.Inputs[i].Value;
                hasValue[i] = true;
                entryUsed[i] = true;
            }

            int next = 0;
            for (int i = 0; i < mons.Count; i++)
            {
                if (hasValue[i]) continue;
                while (next < p.Inputs.Count && entryUsed[next]) next++;
                if (next >= p.Inputs.Count) { unmatched++; continue; }
                values[i] = p.Inputs[next].Value;
                hasValue[i] = true;
                entryUsed[next] = true;
                paired++;
            }
        }

        public static ApplyOutcome ApplyProfile(Profile p)
        {
            var outcome = new ApplyOutcome();
            var monitors = GetMonitors();
            try
            {
                if (monitors.Count == 0) { outcome.NoMonitors = true; return outcome; }

                uint[] values; bool[] hasValue; int paired, unmatched;
                Plan(p, monitors, out values, out hasValue, out paired, out unmatched);
                outcome.Paired = paired;
                outcome.Unmatched = unmatched;

                for (int i = 0; i < monitors.Count; i++)
                {
                    if (!hasValue[i]) continue;
                    if (Native.SetVCPFeature(monitors[i].Handle, VCP_INPUT, values[i]))
                        outcome.Applied++;
                    else
                        outcome.Failures++;
                }
            }
            finally { Release(monitors); }
            return outcome;
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
                    string label = monitors[i].Id.Length > 0
                        ? "Monitor " + i + " (" + monitors[i].Id + ")"
                        : "Monitor " + i;
                    lines.Add(ok
                        ? label + ": input = " + (cur & 0xFF)
                        : label + ": read failed");
                }
                return string.Join("\n", lines);
            }
            finally { Release(monitors); }
        }

        // Reads the current input of every monitor, keyed by identity.
        // Returns null if no monitors, or if any monitor's input can't be read
        // (a partial capture would silently produce a broken profile).
        public static List<InputSetting> CaptureCurrent()
        {
            var monitors = GetMonitors();
            try
            {
                if (monitors.Count == 0) return null;
                var vals = new List<InputSetting>();
                for (int i = 0; i < monitors.Count; i++)
                {
                    uint cur = 0, max = 0;
                    if (!Native.GetVCPFeatureAndVCPFeatureReply(
                            monitors[i].Handle, VCP_INPUT, IntPtr.Zero, ref cur, ref max))
                        return null;
                    string id = monitors[i].Id.Length > 0 ? monitors[i].Id : null;
                    vals.Add(new InputSetting(id, cur & 0xFF));
                }
                return vals;
            }
            finally { Release(monitors); }
        }

        // Reads the current input of every monitor. One entry per monitor;
        // Value -1 means that monitor couldn't be read. Empty list = no monitors.
        public static List<MonitorInput> ReadInputs()
        {
            var monitors = GetMonitors();
            try
            {
                var vals = new List<MonitorInput>();
                for (int i = 0; i < monitors.Count; i++)
                {
                    uint cur = 0, max = 0;
                    bool ok = Native.GetVCPFeatureAndVCPFeatureReply(
                        monitors[i].Handle, VCP_INPUT, IntPtr.Zero, ref cur, ref max);
                    vals.Add(new MonitorInput
                    {
                        Id = monitors[i].Id,
                        Value = ok ? (int)(cur & 0xFF) : -1
                    });
                }
                return vals;
            }
            finally { Release(monitors); }
        }

        // Fills in ids on legacy positional entries using the monitors
        // connected right now. Returns true if anything was upgraded.
        public static bool UpgradeLegacyEntries(Profile p)
        {
            if (p == null || p.Inputs == null) return false;
            bool any = false;
            foreach (var e in p.Inputs) if (e.MonitorId == null) { any = true; break; }
            if (!any) return false;

            var monitors = GetMonitors();
            try
            {
                bool changed = false;
                for (int i = 0; i < p.Inputs.Count && i < monitors.Count; i++)
                {
                    if (p.Inputs[i].MonitorId == null && monitors[i].Id.Length > 0)
                    {
                        p.Inputs[i].MonitorId = monitors[i].Id;
                        changed = true;
                    }
                }
                return changed;
            }
            finally { Release(monitors); }
        }
    }
}
