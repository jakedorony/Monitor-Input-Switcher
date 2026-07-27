// ConfigStore.cs - local settings persistence.
//
// v2.x stores JSON at %APPDATA%\MonitorSwitch\config.json.
// v1.x stored     %APPDATA%\MonitorSwitch\config.txt  (ProfileA = Name | v0, v1)
// On first run of v2, an existing config.txt is migrated automatically.
// An empty value list is valid and means "unset" (deleted slot) - keep
// accepting it or deleted profiles resurrect.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MonitorSwitch
{
    static class ConfigStore
    {
        class ProfileDto
        {
            public string Name { get; set; }
            public List<uint> Values { get; set; }
            public DateTime UpdatedAtUtc { get; set; }
        }

        class ConfigDto
        {
            public ProfileDto ProfileA { get; set; }
            public ProfileDto ProfileB { get; set; }
        }

        public static string Dir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MonitorSwitch");
            }
        }

        public static string JsonPath { get { return Path.Combine(Dir, "config.json"); } }
        public static string LegacyPath { get { return Path.Combine(Dir, "config.txt"); } }

        // "First run" = no settings of either generation on disk.
        public static bool Exists()
        {
            return File.Exists(JsonPath) || File.Exists(LegacyPath);
        }

        public static void Load(ref Profile profileA, ref Profile profileB)
        {
            try
            {
                if (File.Exists(JsonPath))
                {
                    var dto = JsonSerializer.Deserialize<ConfigDto>(File.ReadAllText(JsonPath));
                    if (dto != null)
                    {
                        var a = FromDto(dto.ProfileA);
                        var b = FromDto(dto.ProfileB);
                        if (a != null) profileA = a;
                        if (b != null) profileB = b;
                    }
                    return;
                }

                if (File.Exists(LegacyPath))
                {
                    // One-time migration from the v1.x text format. The old
                    // file is left in place (harmless; ignored from now on).
                    DateTime stamp = File.GetLastWriteTimeUtc(LegacyPath);
                    LoadLegacy(ref profileA, ref profileB, stamp);
                    Save(profileA, profileB);
                }
            }
            catch
            {
                // Bad config -> silently keep built-in defaults.
            }
        }

        public static bool Save(Profile profileA, Profile profileB)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var dto = new ConfigDto
                {
                    ProfileA = ToDto(profileA),
                    ProfileB = ToDto(profileB)
                };
                var opts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(JsonPath, JsonSerializer.Serialize(dto, opts));
                return true;
            }
            catch
            {
                return false;
            }
        }

        static ProfileDto ToDto(Profile p)
        {
            return new ProfileDto
            {
                Name = p.Name,
                Values = new List<uint>(p.Values),
                UpdatedAtUtc = p.UpdatedAtUtc
            };
        }

        static Profile FromDto(ProfileDto d)
        {
            if (d == null || string.IsNullOrEmpty(d.Name)) return null;
            var p = new Profile(d.Name);
            p.Values = d.Values ?? new List<uint>();
            p.UpdatedAtUtc = DateTime.SpecifyKind(d.UpdatedAtUtc, DateTimeKind.Utc);
            return p;
        }

        // v1.x parser, unchanged semantics:
        //   ProfileA = Name | 15, 15
        //   ProfileB = Name |            (empty list = deleted/unset)
        static void LoadLegacy(ref Profile profileA, ref Profile profileB, DateTime stampUtc)
        {
            foreach (string raw in File.ReadAllLines(LegacyPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string rest = line.Substring(eq + 1).Trim();

                int pipe = rest.IndexOf('|');
                if (pipe < 0) continue;
                string name = rest.Substring(0, pipe).Trim();
                string valuesText = rest.Substring(pipe + 1).Trim();

                var vals = new List<uint>();
                bool valid = name.Length > 0;
                if (valuesText.Length > 0)
                {
                    foreach (string part in valuesText.Split(','))
                    {
                        uint v;
                        if (uint.TryParse(part.Trim(), out v)) vals.Add(v);
                        else { valid = false; break; }
                    }
                }
                if (!valid) continue;

                var prof = new Profile(name);
                prof.Values = vals;
                prof.UpdatedAtUtc = stampUtc;
                if (key == "profilea") profileA = prof;
                else if (key == "profileb") profileB = prof;
            }
        }
    }
}
