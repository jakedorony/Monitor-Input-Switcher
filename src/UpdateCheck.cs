// UpdateCheck.cs - once-a-day check against GitHub Releases. Notifies (at
// most once per release) via a balloon tip; clicking it opens the download
// page. Failures are silent - updates are a courtesy, never a nag.

using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MonitorSwitch
{
    static class UpdateCheck
    {
        const string LatestApi =
            "https://api.github.com/repos/jakedorony/Monitor-Input-Switcher/releases/latest";
        public const string DownloadPage =
            "https://github.com/jakedorony/Monitor-Input-Switcher/releases/latest";

        // "lastCheckUtcTicks|lastNotifiedTag"
        static string StatePath { get { return Path.Combine(ConfigStore.Dir, "update-check.txt"); } }

        // Returns the new version tag (e.g. "v2.3.0") if a newer release
        // exists and hasn't been announced yet; null otherwise.
        public static async Task<string> DailyCheckAsync()
        {
            DateTime lastCheck = DateTime.MinValue;
            string lastNotified = "";
            try
            {
                if (File.Exists(StatePath))
                {
                    string[] parts = File.ReadAllText(StatePath).Split('|');
                    long ticks;
                    if (parts.Length >= 1 && long.TryParse(parts[0], out ticks))
                        lastCheck = new DateTime(ticks, DateTimeKind.Utc);
                    if (parts.Length >= 2) lastNotified = parts[1].Trim();
                }
            }
            catch { }

            if (DateTime.UtcNow - lastCheck < TimeSpan.FromHours(24)) return null;

            string tag;
            try
            {
                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(15);
                    // GitHub's API requires a User-Agent.
                    http.DefaultRequestHeaders.Add("User-Agent", "MonitorSwitch-UpdateCheck");
                    string json = await http.GetStringAsync(LatestApi);
                    using (var doc = JsonDocument.Parse(json))
                        tag = doc.RootElement.GetProperty("tag_name").GetString();
                }
            }
            catch
            {
                return null;    // offline, rate-limited, no releases yet - all fine
            }

            SaveState(DateTime.UtcNow, lastNotified);
            if (string.IsNullOrEmpty(tag) || tag == lastNotified) return null;

            Version remote, current;
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out remote)) return null;
            string cur = System.Windows.Forms.Application.ProductVersion.Split('+')[0];
            if (!Version.TryParse(cur, out current)) return null;
            if (remote <= current) return null;

            SaveState(DateTime.UtcNow, tag);
            return tag;
        }

        static void SaveState(DateTime checkUtc, string notifiedTag)
        {
            try
            {
                Directory.CreateDirectory(ConfigStore.Dir);
                File.WriteAllText(StatePath, checkUtc.Ticks + "|" + notifiedTag);
            }
            catch { }
        }
    }
}
