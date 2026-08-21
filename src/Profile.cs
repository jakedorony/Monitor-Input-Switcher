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
