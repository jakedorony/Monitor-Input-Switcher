// MonitorNames.cs - human-readable monitor names from the EDID in the
// registry (HKLM\SYSTEM\...\Enum\DISPLAY\<hwid>\*\Device Parameters\EDID,
// readable without elevation). Display-only: identity/matching still uses
// the PnP hardware id.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;

namespace MonitorSwitch
{
    static class MonitorNames
    {
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

        // Three-letter PnP vendor codes worth prettifying. Unknown codes are
        // shown as-is, which is still better than the full hardware id.
        static readonly Dictionary<string, string> vendors = new Dictionary<string, string>
        {
            { "AUS", "ASUS" }, { "ACI", "ASUS" }, { "DEL", "Dell" }, { "SAM", "Samsung" },
            { "GSM", "LG" }, { "LGD", "LG" }, { "ACR", "Acer" }, { "BNQ", "BenQ" },
            { "HWP", "HP" }, { "HPN", "HP" }, { "LEN", "Lenovo" }, { "VSC", "ViewSonic" },
            { "AOC", "AOC" }, { "MSI", "MSI" }, { "GBT", "Gigabyte" }, { "PHL", "Philips" },
            { "SNY", "Sony" }, { "APP", "Apple" }, { "IVM", "iiyama" }, { "NEC", "NEC" },
            { "ENC", "EIZO" }, { "SHP", "Sharp" }, { "CMN", "Innolux" }, { "AUO", "AU Optronics" },
            { "BOE", "BOE" }, { "HSD", "HannStar" }, { "SEC", "Samsung" }, { "MEI", "Panasonic" },
            { "TOS", "Toshiba" }, { "HTC", "HTC" }, { "OVR", "Oculus" }, { "VLV", "Valve" }
        };

        // "DELA07B#2" -> "DELL U2412M (2)", "AUS27FD" -> "ASUS PG27AQDM",
        // unknown -> the id itself.
        public static string Friendly(string monitorId)
        {
            if (string.IsNullOrEmpty(monitorId)) return "Monitor";
            string baseId = monitorId, suffix = "";
            int hash = monitorId.IndexOf('#');
            if (hash > 0) { baseId = monitorId.Substring(0, hash); suffix = " (" + monitorId.Substring(hash + 1) + ")"; }

            string name;
            lock (cache)
            {
                if (!cache.TryGetValue(baseId, out name))
                {
                    name = Lookup(baseId) ?? baseId;
                    cache[baseId] = name;
                }
            }
            return name + suffix;
        }

        static string Lookup(string hwid)
        {
            try
            {
                using (var display = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY\" + hwid, false))
                {
                    if (display == null) return null;
                    foreach (string inst in display.GetSubKeyNames())
                    {
                        using (var par = display.OpenSubKey(inst + @"\Device Parameters", false))
                        {
                            byte[] edid = par == null ? null : par.GetValue("EDID") as byte[];
                            if (edid == null || edid.Length < 128) continue;
                            string n = FromEdid(edid, hwid);
                            if (n != null) return n;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        static string FromEdid(byte[] e, string hwid)
        {
            // Manufacturer: 3 x 5-bit letters packed into bytes 8-9.
            string code = new string(new[]
            {
                (char)(64 + ((e[8] >> 2) & 31)),
                (char)(64 + (((e[8] & 3) << 3) | (e[9] >> 5))),
                (char)(64 + (e[9] & 31))
            });
            string vendor;
            if (!vendors.TryGetValue(code, out vendor)) vendor = code;

            // Descriptor blocks at 54/72/90/108; tag 0xFC = monitor name.
            string model = null;
            for (int i = 54; i <= 108; i += 18)
            {
                if (e[i] == 0 && e[i + 1] == 0 && e[i + 2] == 0 && e[i + 3] == 0xFC)
                {
                    model = Encoding.ASCII.GetString(e, i + 5, 13).Trim('\0', '\n', '\r', ' ');
                    break;
                }
            }
            if (string.IsNullOrEmpty(model)) return vendor + " " + hwid.Substring(Math.Min(3, hwid.Length));

            // Avoid "Dell DELL U2412M".
            if (model.StartsWith(vendor, StringComparison.OrdinalIgnoreCase)) return model;
            return vendor + " " + model;
        }
    }
}
