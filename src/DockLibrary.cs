// DockLibrary.cs - known KVM-capable docks and the USB signatures of their
// switched segment. A match is a labeled suggestion for one-click setup,
// not an identity claim: hub chips (especially Genesys) are generic parts
// used by many products. The learn wizard handles anything not listed.

using System;
using System.Collections.Generic;

namespace MonitorSwitch
{
    class DockModel
    {
        public string Name;
        public string[] Signatures;      // "VVVV:PPPP", all must be present
        public string Notes;
    }

    static class DockLibrary
    {
        public static readonly DockModel[] Models =
        {
            new DockModel
            {
                Name = "Plugable TBT4-UD5",
                // The dock's switched hub pair (Genesys USB2 + USB3). Captured
                // from real hardware 2026-08-31; both depart/arrive on every
                // press of the host-switch button.
                Signatures = new[] { "05E3:0610", "05E3:0626" },
                Notes = "Host-switch button moves the whole peripheral segment."
            }
        };

        // Best library match for a set of signatures (all of the model's
        // signatures must be included). Null if nothing matches.
        public static DockModel Match(IReadOnlyCollection<string> sigs)
        {
            foreach (var m in Models)
            {
                bool all = true;
                foreach (string s in m.Signatures)
                {
                    bool found = false;
                    foreach (string have in sigs)
                        if (string.Equals(have, s, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                    if (!found) { all = false; break; }
                }
                if (all) return m;
            }
            return null;
        }
    }
}
