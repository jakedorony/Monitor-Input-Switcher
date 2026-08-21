// ConfigStore.cs - local settings persistence.
//
// v2.1 stores JSON at %APPDATA%\MonitorSwitch\config.json with inputs keyed
// by monitor hardware id:  "Inputs": [{"Monitor":"DEL40A8","Value":15}, ...]
// Older formats are migrated automatically on load:
//   v2.0 config.json used positional arrays:  "Values": [15, 15]
//   v1.x config.txt used:                     ProfileA = Name | 15, 15
// Positional data becomes entries with a null Monitor id; ids are filled in
// at startup once monitors can be enumerated (Ddc.UpgradeLegacyEntries).
// An empty entry list is valid and means "unset" (deleted slot) - keep
// accepting it or deleted profiles resurrect.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MonitorSwitch
{
    static class ConfigStore
    {
        class InputDto
        {
            public string Monitor { get; set; }
            public uint Value { get; set; }
            public List<string> Aliases { get; set; }   // omitted when empty
        }

        class ProfileDto
        {
            public string Name { get; set; }
            public List<InputDto> Inputs { get; set; }
            public List<uint> Values { get; set; }      // legacy v2.0 positional
            public DateTime UpdatedAtUtc { get; set; }
        }

        class ConfigDto
        {
            public ProfileDto ProfileA { get; set; }
            public ProfileDto ProfileB { get; set; }
            public string Theme { get; set; }            // "System" | "Light" | "Dark"; device-local
        }

        // Device-local settings that ride in config.json next to the profiles.
        public static string Theme = "System";

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
                        if (!string.IsNullOrEmpty(dto.Theme)) Theme = dto.Theme;
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
                    ProfileB = ToDto(profileB),
                    Theme = Theme
                };
                var opts = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
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
            var inputs = new List<InputDto>();
            foreach (var e in p.Inputs)
                inputs.Add(new InputDto
                {
                    Monitor = e.MonitorId,
                    Value = e.Value,
                    Aliases = (e.Aliases != null && e.Aliases.Count > 0)
                        ? new List<string>(e.Aliases) : null
                });
            return new ProfileDto
            {
                Name = p.Name,
                Inputs = inputs,
                UpdatedAtUtc = p.UpdatedAtUtc
            };
        }

        static Profile FromDto(ProfileDto d)
        {
            if (d == null || string.IsNullOrEmpty(d.Name)) return null;
            var p = new Profile(d.Name);
            if (d.Inputs != null)
            {
                foreach (var e in d.Inputs)
                {
                    var entry = new InputSetting(e.Monitor, e.Value);
                    if (e.Aliases != null && e.Aliases.Count > 0)
                        entry.Aliases = new List<string>(e.Aliases);
                    p.Inputs.Add(entry);
                }
            }
            else if (d.Values != null)
            {
                // v2.0 positional format
                foreach (uint v in d.Values)
                    p.Inputs.Add(new InputSetting(null, v));
            }
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

                var vals = new List<InputSetting>();
                bool valid = name.Length > 0;
                if (valuesText.Length > 0)
                {
                    foreach (string part in valuesText.Split(','))
                    {
                        uint v;
                        if (uint.TryParse(part.Trim(), out v)) vals.Add(new InputSetting(null, v));
                        else { valid = false; break; }
                    }
                }
                if (!valid) continue;

                var prof = new Profile(name);
                prof.Inputs = vals;
                prof.UpdatedAtUtc = stampUtc;
                if (key == "profilea") profileA = prof;
                else if (key == "profileb") profileB = prof;
            }
        }
    }
}
