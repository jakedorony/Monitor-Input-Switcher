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

        public enum MatchKind { None, Id, Alias, Positional, Paired }

        // Which saved entry drives one connected monitor, and how it was found.
        public class MonitorMatch
        {
            public int EntryIndex = -1;
            public MatchKind Kind = MatchKind.None;
            public uint Value;
            public bool Has { get { return Kind != MatchKind.None; } }
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
            public int Learned;         // aliases recorded on this run
        }

        // Pure. Decides which saved input drives each connected monitor, in
        // descending order of confidence:
        //   1. exact primary id          - the normal path
        //   2. learned alias             - same panel, different id on this PC
        //   3. legacy positional entries - v1.x/v2.0 data with no id
        //   4. leftover pairing          - whatever is still unclaimed, in order
        //
        // Tiers 2 and 4 exist because a monitor's PnP id is not guaranteed
        // stable across PCs: one Dell panel reports DELA07A on one machine and
        // DELA07B on another. Pairing only ever consumes entries no monitor
        // claimed by id or alias, so exact matches always win.
        public static MonitorMatch[] Plan(Profile p, List<PhysMon> mons)
        {
            var result = new MonitorMatch[mons.Count];
            for (int i = 0; i < mons.Count; i++) result[i] = new MonitorMatch();
            var entryUsed = new bool[p.Inputs.Count];

            // Primary ids are resolved for ALL monitors before aliases get a
            // look-in, so an alias can never steal an entry from an exact match.
            MatchPass(p, mons, result, entryUsed, false);
            MatchPass(p, mons, result, entryUsed, true);

            for (int i = 0; i < mons.Count && i < p.Inputs.Count; i++)
            {
                if (result[i].Has || entryUsed[i] || p.Inputs[i].MonitorId != null) continue;
                Take(result[i], p, i, entryUsed, MatchKind.Positional);
            }

            int next = 0;
            for (int i = 0; i < mons.Count; i++)
            {
                if (result[i].Has) continue;
                while (next < p.Inputs.Count && entryUsed[next]) next++;
                if (next >= p.Inputs.Count) continue;   // nothing left; stays unmatched
                Take(result[i], p, next, entryUsed, MatchKind.Paired);
            }
            return result;
        }

        static void MatchPass(Profile p, List<PhysMon> mons, MonitorMatch[] result,
            bool[] entryUsed, bool allowAlias)
        {
            for (int i = 0; i < mons.Count; i++)
            {
                if (result[i].Has || mons[i].Id.Length == 0) continue;
                for (int e = 0; e < p.Inputs.Count; e++)
                {
                    if (entryUsed[e]) continue;
                    bool hit = allowAlias
                        ? p.Inputs[e].Matches(mons[i].Id)
                        : p.Inputs[e].MonitorId == mons[i].Id;
                    if (!hit) continue;
                    Take(result[i], p, e, entryUsed,
                        allowAlias ? MatchKind.Alias : MatchKind.Id);
                    break;
                }
            }
        }

        static void Take(MonitorMatch m, Profile p, int e, bool[] entryUsed, MatchKind kind)
        {
            m.EntryIndex = e;
            m.Kind = kind;
            m.Value = p.Inputs[e].Value;
            entryUsed[e] = true;
        }

        // Turns a guess into knowledge. A leftover pairing is only trustworthy
        // when it was FORCED - exactly one monitor and one entry left over, so
        // no other assignment was possible. Then the entry's id and the
        // monitor's id provably name the same physical panel, and recording the
        // alias means every later switch matches exactly instead of by order.
        // With two or more leftovers the order could be wrong, so learn nothing.
        // Returns the number of aliases added (0 or 1).
        public static int LearnAliases(Profile p, List<PhysMon> mons, MonitorMatch[] plan)
        {
            int idx = -1, count = 0;
            for (int i = 0; i < plan.Length; i++)
            {
                // A positional match is itself a guess (it assumes enumeration
                // order never changed). Deducing an alias from a plan that
                // leans on one would promote that guess to permanent, synced
                // fact - so learn only from plans built purely on exact ids.
                if (plan[i].Kind == MatchKind.Positional) return 0;
                if (plan[i].Kind == MatchKind.Paired) { count++; idx = i; }
            }
            if (count != 1) return 0;
            if (mons[idx].Id.Length == 0) return 0;

            var entry = p.Inputs[plan[idx].EntryIndex];
            if (entry.MonitorId == null) return 0;   // legacy entry: id is filled in elsewhere
            return entry.AddAlias(mons[idx].Id) ? 1 : 0;
        }

        // How many of the live monitors already show the input this profile
        // would give them. Uses the same matching as ApplyProfile, so "on this
        // profile" means exactly what switching to it would (not) change.
        // Unreadable monitors (-1) never count.
        public static int CountOnProfile(Profile p, List<MonitorInput> live)
        {
            if (p == null || p.Inputs == null || live == null || live.Count == 0) return 0;
            var mons = new List<PhysMon>();
            foreach (var m in live) mons.Add(new PhysMon { Id = m.Id ?? "", Description = "" });
            MonitorMatch[] plan = Plan(p, mons);
            int n = 0;
            for (int i = 0; i < live.Count; i++)
                if (plan[i].Has && live[i].Value >= 0 && (uint)live[i].Value == plan[i].Value) n++;
            return n;
        }

        public static ApplyOutcome ApplyProfile(Profile p)
        {
            var outcome = new ApplyOutcome();
            var monitors = GetMonitors();
            try
            {
                if (monitors.Count == 0) { outcome.NoMonitors = true; return outcome; }

                MonitorMatch[] plan = Plan(p, monitors);
                for (int i = 0; i < monitors.Count; i++)
                {
                    if (!plan[i].Has) { outcome.Unmatched++; continue; }
                    if (plan[i].Kind == MatchKind.Paired) outcome.Paired++;
                    if (Native.SetVCPFeature(monitors[i].Handle, VCP_INPUT, plan[i].Value))
                        outcome.Applied++;
                    else
                        outcome.Failures++;
                }
                outcome.Learned = LearnAliases(p, monitors, plan);
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
