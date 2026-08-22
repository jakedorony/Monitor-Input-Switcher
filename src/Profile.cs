// Profile.cs - a named set of input values, one per physical monitor.

using System;
using System.Collections.Generic;

namespace MonitorSwitch
{
    // One monitor's saved input. MonitorId is the PnP hardware id (e.g.
    // "DEL40A8", "GSM5B09#2" for a second identical model). It is USUALLY the
    // same for a given panel on any PC, but not always - see Aliases. A null
    // MonitorId is legacy v1.x/v2.0 data meaning "the monitor at this list
    // position"; it is upgraded to an id at startup once monitors enumerate.
    class InputSetting
    {
        public string MonitorId;            // primary id; null = legacy positional
        public uint Value;

        // Other ids the SAME physical panel is known to report. A monitor's
        // PnP product code can differ per PC (one Dell reports DELA07A on one
        // machine and DELA07B on another), so an entry can carry more than one
        // id and still mean one screen. Learned by Ddc.LearnAliases; null or
        // empty for entries that have only ever been seen under one id.
        public List<string> Aliases;

        public InputSetting() { }
        public InputSetting(string monitorId, uint value)
        {
            MonitorId = monitorId;
            Value = value;
        }

        // True if this entry describes the monitor reporting `id`, whether by
        // its primary id or a learned alias.
        public bool Matches(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (MonitorId == id) return true;
            if (Aliases != null)
                foreach (string a in Aliases)
                    if (a == id) return true;
            return false;
        }

        // Returns true if this actually added something new.
        public bool AddAlias(string id)
        {
            if (string.IsNullOrEmpty(id) || id == MonitorId) return false;
            if (Aliases == null) Aliases = new List<string>();
            foreach (string a in Aliases)
                if (a == id) return false;
            Aliases.Add(id);
            return true;
        }
    }

    // Bounds for anything that arrives from config.json or the cloud. The
    // data is the user's own, but a stolen session or a bad actor with DB
    // access must not be able to push megabytes of names or odd ids into
    // the tray menu, the UI, or SetVCPFeature.
    static class Limits
    {
        public const int NameChars = 80;
        public const int IdChars = 32;
        public const int Entries = 16;
        public const int Aliases = 16;
        public const uint MaxInput = 255;      // VCP 0x60 values are one byte

        public static string Name(string s)
        {
            if (s == null) return null;
            s = s.Trim();
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
                if (!char.IsControl(c)) sb.Append(c);
            s = sb.ToString();
            return s.Length > NameChars ? s.Substring(0, NameChars) : s;
        }

        // Hardware ids are "DELA07B", optionally "#2"-suffixed. Anything else
        // (path separators, control chars, oversize) becomes null = unknown.
        public static string Id(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > IdChars) return null;
            foreach (char c in s)
                if (!(char.IsLetterOrDigit(c) || c == '#' || c == '_' || c == '-')) return null;
            return s;
        }

        public static uint Input(uint v) { return v > MaxInput ? 0 : v; }

        // Drops invalid entries, clamps counts, dedupes aliases.
        public static void Apply(Profile p)
        {
            if (p == null) return;
            p.Name = Name(p.Name) ?? "";
            if (p.Inputs == null) { p.Inputs = new List<InputSetting>(); return; }
            var kept = new List<InputSetting>();
            foreach (var e in p.Inputs)
            {
                if (e == null) continue;
                if (e.MonitorId != null) { e.MonitorId = Id(e.MonitorId); if (e.MonitorId == null) continue; }
                e.Value = Input(e.Value);
                if (e.Aliases != null)
                {
                    var al = new List<string>();
                    foreach (var a in e.Aliases)
                    {
                        string ok = Id(a);
                        if (ok != null && ok != e.MonitorId && !al.Contains(ok)) al.Add(ok);
                        if (al.Count >= Aliases) break;
                    }
                    e.Aliases = al.Count > 0 ? al : null;
                }
                kept.Add(e);
                if (kept.Count >= Entries) break;
            }
            p.Inputs = kept;
        }
    }

    class Profile
    {
        public string Name;
        public List<InputSetting> Inputs;

        // When this slot last changed (save/delete locally, or the timestamp
        // adopted from the cloud). MinValue = untouched built-in default,
        // which is never pushed to the cloud.
        public DateTime UpdatedAtUtc = DateTime.MinValue;

        public Profile(string name)
        {
            Name = name;
            Inputs = new List<InputSetting>();
        }

        // Positional default (built-ins): ids unknown until first save.
        public Profile(string name, params uint[] positionalValues)
        {
            Name = name;
            Inputs = new List<InputSetting>();
            foreach (uint v in positionalValues)
                Inputs.Add(new InputSetting(null, v));
        }
    }

    class PhysMon
    {
        public IntPtr Handle;
        public string Description;
        public string Id;               // PnP hardware id, "" if unavailable
    }
}
