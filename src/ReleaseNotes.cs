// ReleaseNotes.cs - patch notes in the GUI. Fetches a release's notes from
// the GitHub Releases API and shows them in a themed "What's new" window:
// once automatically after an upgrade (TrayApp), and on demand from
// Settings -> Help. Startup failures are silent - notes are a courtesy,
// never a nag. The release body is remote content: it is reduced to
// bounded plain text before it touches the UI, and the only link offered
// is a page URL this code builds itself.

using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MonitorSwitch
{
    static class ReleaseNotes
    {
        const string ApiBase =
            "https://api.github.com/repos/jakedorony/Monitor-Input-Switcher/releases/tags/";
        const string PageBase =
            "https://github.com/jakedorony/Monitor-Input-Switcher/releases/tag/";
        const int MaxChars = 8000;

        // Tidied notes for a tag ("v2.7.0"); null when offline or missing.
        public static async Task<string> FetchAsync(string tag)
        {
            try
            {
                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(15);
                    // GitHub's API requires a User-Agent.
                    http.DefaultRequestHeaders.Add("User-Agent", "MonitorSwitch-ReleaseNotes");
                    string json = await http.GetStringAsync(ApiBase + Uri.EscapeDataString(tag));
                    using (var doc = JsonDocument.Parse(json))
                    {
                        JsonElement body;
                        if (!doc.RootElement.TryGetProperty("body", out body)) return null;
                        return Tidy(body.GetString());
                    }
                }
            }
            catch { return null; }
        }

        // Built here from the tag - never taken from the response.
        public static string PageUrl(string tag)
        {
            return PageBase + Uri.EscapeDataString(tag);
        }

        // Remote markdown -> safe plain text: printable characters only,
        // light markdown cleanup (headers, bullets, emphasis), collapsed
        // blank runs, bounded size.
        public static string Tidy(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var sb = new StringBuilder();
            bool lastBlank = true;                 // swallow leading blanks
            foreach (string raw in s.Replace("\r\n", "\n").Split('\n'))
            {
                var lb = new StringBuilder(raw.Length);
                foreach (char c in raw)
                    if (!char.IsControl(c)) lb.Append(c);
                string line = lb.ToString().TrimEnd();

                string t = line.TrimStart();
                if (t.StartsWith("### ")) line = t.Substring(4).ToUpperInvariant();
                else if (t.StartsWith("## ")) line = t.Substring(3).ToUpperInvariant();
                else if (t.StartsWith("# ")) line = t.Substring(2).ToUpperInvariant();
                else if (t.StartsWith("- ") || t.StartsWith("* ")) line = "  •  " + t.Substring(2);
                line = line.Replace("**", "").Replace("`", "");

                bool blank = line.Length == 0;
                if (blank && lastBlank) continue;
                lastBlank = blank;
                sb.Append(line).Append("\r\n");
                if (sb.Length >= MaxChars) { sb.Append("..."); break; }
            }
            string result = sb.ToString().TrimEnd();
            if (result.Length > MaxChars) result = result.Substring(0, MaxChars) + "...";
            return result.Length == 0 ? null : result;
        }
    }

    // Modeless themed viewer (singleton, like the other windows).
    class NotesWindow : Form
    {
        static NotesWindow open;

        public static void ShowNotes(string title, string text, string pageUrl)
        {
            if (open != null && !open.IsDisposed) open.Close();
            open = new NotesWindow(title, text, pageUrl);
            open.Show();
            open.Activate();
        }

        NotesWindow(string title, string text, string pageUrl)
        {
            float scale = DeviceDpi / 96f;
            Func<float, int> L = v => (int)Math.Round(v * scale);
            Palette P = Theme.Current;

            Text = title + " - Monitor Input Switcher";
            Icon = TrayApp.AppIcon(32);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(L(480), L(420));
            MinimumSize = new Size(L(380), L(280));
            BackColor = P.Bg;
            Font = Theme.Body;
            HandleCreated += delegate { Theme.ApplyTitleBar(this); };
            FormClosed += delegate { if (open == this) open = null; };

            var box = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                WordWrap = true, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
                BackColor = P.Card, ForeColor = P.Text, Font = Theme.Body,
                Text = text
            };
            box.Select(0, 0);

            var bar = new Panel { Dock = DockStyle.Bottom, Height = L(46), BackColor = P.Bg };

            var page = new FlatButton
            {
                Text = "Open release page", Font = Theme.Body,
                Fill = P.Card, Border = P.Border, TextColor = P.Text, HoverFill = P.Hover,
                AccentFill = P.Accent, AccentText = P.AccentText,
                Size = new Size(L(150), L(30)),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Location = new Point(L(12), L(8))
            };
            page.Click += delegate
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = pageUrl, UseShellExecute = true });
                }
                catch { }
            };

            var close = new FlatButton
            {
                Text = "Close", Primary = true, Font = Theme.Body,
                Fill = P.Card, Border = P.Border, TextColor = P.Text, HoverFill = P.Hover,
                AccentFill = P.Accent, AccentText = P.AccentText,
                Size = new Size(L(90), L(30)),
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            close.Click += delegate { Close(); };

            Controls.Add(box);
            Controls.Add(bar);
            bar.Controls.Add(page);
            bar.Controls.Add(close);
            close.Location = new Point(bar.ClientSize.Width - close.Width - L(12), L(8));
        }
    }
}
