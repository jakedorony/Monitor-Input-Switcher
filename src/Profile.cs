// Profile.cs - a named set of input values, one per physical monitor.

using System;
using System.Collections.Generic;

namespace MonitorSwitch
{
    // One monitor's saved input. MonitorId is the PnP hardware id (e.g.
    // "DEL40A8", "GSM5B09#2" for a second identical model), which is stable
    // for the same physical monitor on ANY PC. A null MonitorId is legacy
    // v1.x/v2.0 data and means "the monitor at this list position"; it is
    // upgraded to an id at startup once monitors can be enumerated.
    class InputSetting
    {
        public string MonitorId;
        public uint Value;

        public InputSetting() { }
        public InputSetting(string monitorId, uint value)
        {
            MonitorId = monitorId;
            Value = value;
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
