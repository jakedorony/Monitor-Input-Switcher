// Profile.cs - a named set of input values, one per monitor.

using System;
using System.Collections.Generic;

namespace MonitorSwitch
{
    class Profile
    {
        public string Name;
        public List<uint> Values;      // index = monitor enumeration order

        // When this slot last changed (save/delete locally, or the timestamp
        // adopted from the cloud). MinValue = untouched built-in default,
        // which is never pushed to the cloud.
        public DateTime UpdatedAtUtc = DateTime.MinValue;

        public Profile(string name, params uint[] values)
        {
            Name = name;
            Values = new List<uint>(values);
        }
    }

    class PhysMon
    {
        public IntPtr Handle;
        public string Description;
    }
}
