// TrayApp.cs - tray icon, context menu, profile actions, and sync
// orchestration. This is the v1.x Program class split out; all A/B
// behaviors are unchanged.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MonitorSwitch
{
    static class TrayApp
    {
        public static Profile ProfileA = new Profile("Personal PC (Display Port)", 15, 15);
        public static Profile ProfileB = new Profile("Work PC (DVI/HDMI)", 3, 17);
        static Profile lastProfile;

        public static NotifyIcon Tray;
        static ToolStripItem itemA;
        static ToolStripItem itemB;

        // Raised whenever sign-in state or last-sync time changes, so the
        // main window (if open) can repaint its Sync section.
        public static event Action SyncStateChanged;
        public static DateTime LastSyncUtc = DateTime.MinValue;

        public static void Run()
        {
            Application.EnableVisualStyles();

            // First run = no settings yet. Used to auto-show help below.
            bool firstRun = !ConfigStore.Exists();

            var a = ProfileA; var b = ProfileB;
            ConfigStore.Load(ref a, ref b);
            ProfileA = a; ProfileB = b;

            // Upgrade legacy positional entries (v1.x/v2.0 data) to monitor
            // identities using whatever is plugged in right now. Harmless to
            // retry every launch until it succeeds.
            bool upgraded = Ddc.UpgradeLegacyEntries(ProfileA);
            upgraded |= Ddc.UpgradeLegacyEntries(ProfileB);
            if (upgraded) ConfigStore.Save(ProfileA, ProfileB);

            Tray = new NotifyIcon();
            Tray.Icon = AppIcon(16);
            Tray.Text = "Monitor Input Switcher";
            Tray.Visible = true;

            var menu = new ContextMenuStrip();

            var itemOpen = menu.Items.Add("Open Monitor Switcher");
            itemOpen.Font = new Font(itemOpen.Font, FontStyle.Bold);
            itemOpen.Click += delegate { MainWindow.ShowWindow(); };

            var itemToggle = menu.Items.Add("Switch (toggle A/B)");
            itemToggle.Click += delegate { ToggleProfiles(); };

            menu.Items.Add(new ToolStripSeparator());

            itemA = menu.Items.Add(ProfileA.Name);
            itemA.Click += delegate { Apply(ProfileA); };

            itemB = menu.Items.Add(ProfileB.Name);
            itemB.Click += delegate { Apply(ProfileB); };

            menu.Items.Add(new ToolStripSeparator());

            var itemSaveA = menu.Items.Add("Save current setup as Profile A...");
            itemSaveA.Click += delegate { CaptureToProfile('A'); };

            var itemSaveB = menu.Items.Add("Save current setup as Profile B...");
            itemSaveB.Click += delegate { CaptureToProfile('B'); };

            var itemDeleteA = menu.Items.Add("Delete Profile A...");
            itemDeleteA.Click += delegate { DeleteProfile('A'); };

            var itemDeleteB = menu.Items.Add("Delete Profile B...");
            itemDeleteB.Click += delegate { DeleteProfile('B'); };

            menu.Items.Add(new ToolStripSeparator());

            var itemDetect = menu.Items.Add("Detect Current Inputs");
            itemDetect.Click += delegate
            {
                Tray.ShowBalloonTip(5000, "Current Inputs", Ddc.DetectInputs(), ToolTipIcon.Info);
            };

            var itemHelp = menu.Items.Add("How to use...");
            itemHelp.Click += delegate { HelpWindow.ShowHelp(); };

            menu.Items.Add(new ToolStripSeparator());

            var itemExit = menu.Items.Add("Exit");
            itemExit.Click += delegate
            {
                Tray.Visible = false;
                Tray.Dispose();
                Application.Exit();
            };

            Tray.ContextMenuStrip = menu;
            Tray.DoubleClick += delegate { MainWindow.ShowWindow(); };

            // On the very first launch, greet the user and open the guide so
            // they aren't staring at an invisible tray icon wondering what to do.
            if (firstRun)
            {
                Tray.ShowBalloonTip(6000, "Monitor Input Switcher",
                    "Running in the tray (bottom-right, near the clock). " +
                    "Opening the quick guide...", ToolTipIcon.Info);
                HelpWindow.ShowHelp();
            }

            // Silent sign-in + initial sync in the background; the app is
            // fully usable while (and whether or not) this completes.
            InitSyncAsync();
            CheckForUpdatesAsync();

            Application.Run();
        }

        // A profile with no captured values is "not set" (fresh or deleted).
        public static bool IsSet(Profile p)
        {
            return p != null && p.Inputs != null && p.Inputs.Count > 0;
        }

        // Toggle between A and B. If the natural target is empty but the other
        // profile is saved, switch to the saved one instead of nagging.
        public static void ToggleProfiles()
        {
            Profile next = (lastProfile == ProfileA) ? ProfileB : ProfileA;
            Profile other = (next == ProfileA) ? ProfileB : ProfileA;
            if (!IsSet(next) && IsSet(other)) next = other;
            Apply(next);
        }

        // Human-readable label for a VCP 0x60 input value (MCCS standard).
        public static string FriendlyInput(int v)
        {
            string name;
            switch (v)
            {
                case 1:  name = "VGA 1"; break;
                case 2:  name = "VGA 2"; break;
                case 3:  name = "DVI 1"; break;
                case 4:  name = "DVI 2"; break;
                case 15: name = "DisplayPort 1"; break;
                case 16: name = "DisplayPort 2"; break;
                case 17: name = "HDMI 1"; break;
                case 18: name = "HDMI 2"; break;
                default: return "Input " + v;
            }
            return name + " (" + v + ")";
        }

        // Multi-line description of a profile's saved inputs, friendly names included.
        public static string DescribeProfile(Profile p)
        {
            if (!IsSet(p)) return "Nothing saved yet.";
            var lines = new List<string>();
            for (int i = 0; i < p.Inputs.Count; i++)
                lines.Add(MonitorLabel(p.Inputs[i].MonitorId, i) + ":  " +
                    FriendlyInput((int)p.Inputs[i].Value));
            return string.Join("\r\n", lines);
        }

        public static string MonitorLabel(string monitorId, int position)
        {
            return string.IsNullOrEmpty(monitorId)
                ? "Monitor " + position
                : "Monitor " + position + " (" + monitorId + ")";
        }

        // ----- "Start when I sign in" (HKCU Run) -----------------------------
        // Uses the SAME value name ("MonitorSwitch") as the installer's startup
        // task, so the checkbox reflects and controls the installer's setting.
        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValueName = "MonitorSwitch";

        public static bool GetStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                    return key != null && key.GetValue(RunValueName) != null;
            }
            catch { return false; }
        }

        public static bool SetStartupEnabled(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return false;
                    if (enable)
                        key.SetValue(RunValueName,
                            "\"" + Application.ExecutablePath + "\"");
                    else if (key.GetValue(RunValueName) != null)
                        key.DeleteValue(RunValueName);
                }
                return true;
            }
            catch { return false; }
        }

        // ----- profile actions ------------------------------------------------

        public static void Apply(Profile p)
        {
            if (!IsSet(p))
            {
                Tray.ShowBalloonTip(3000, "Monitor Switch",
                    p.Name + " has nothing saved yet. Right-click the icon and " +
                    "use \"Save current setup...\" first.", ToolTipIcon.Warning);
                return;
            }
            Ddc.ApplyOutcome r = Ddc.ApplyProfile(p);
            lastProfile = p;

            if (r.NoMonitors)
            {
                Tray.ShowBalloonTip(3000, "Monitor Switch",
                    "No DDC/CI-capable monitors found.", ToolTipIcon.Error);
                return;
            }
            if (r.Failures > 0)
            {
                Tray.ShowBalloonTip(3000, "Monitor Switch",
                    p.Name + ": " + r.Failures + " monitor(s) refused the switch " +
                    "(DDC/CI off, or that input isn't available on the monitor?)",
                    ToolTipIcon.Warning);
                return;
            }
            if (r.Unmatched > 0)
            {
                // Previously this case silently reported success while leaving
                // a monitor untouched - the "only one monitor switches" bug.
                Tray.ShowBalloonTip(3000, "Monitor Switch",
                    p.Name + ": switched " + r.Applied + " monitor(s), but " +
                    r.Unmatched + " had nothing saved in this profile. " +
                    "Save the setup again to include it.", ToolTipIcon.Warning);
                return;
            }
            Tray.ShowBalloonTip(1500, "Monitor Switch",
                "Switched to: " + p.Name, ToolTipIcon.Info);
        }

        // Reads current monitor inputs, asks for a name via dialog, stores into
        // the chosen slot, and persists (locally, then to the cloud if signed in).
        public static void CaptureToProfile(char slot)
        {
            var vals = Ddc.CaptureCurrent();
            if (vals == null)
            {
                MessageBox.Show(
                    "Couldn't read the current inputs from all monitors.\n\n" +
                    "Make sure DDC/CI is enabled in each monitor's menu, then try again.",
                    "Monitor Switch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Profile existing = (slot == 'A') ? ProfileA : ProfileB;
            string detected = DescribeValues(vals);
            string suggestedName = IsSet(existing) ? existing.Name : ("Profile " + slot);

            string name = Prompt.ForName(
                "Save current setup as Profile " + slot,
                "Detected now:\n" + detected + "\n\nName this profile:",
                suggestedName);

            if (name == null) return;            // cancelled
            name = name.Trim();
            if (name.Length == 0) name = "Profile " + slot;

            var prof = new Profile(name);
            prof.Inputs = vals;
            prof.UpdatedAtUtc = DateTime.UtcNow;
            if (slot == 'A') { ProfileA = prof; itemA.Text = prof.Name; }
            else             { ProfileB = prof; itemB.Text = prof.Name; }

            if (SaveConfig())
                Tray.ShowBalloonTip(2000, "Monitor Switch",
                    "Saved \"" + name + "\" (" + detected + ")", ToolTipIcon.Info);
            else
                Tray.ShowBalloonTip(3000, "Monitor Switch",
                    "Saved for this session, but couldn't write the settings file " +
                    "(it won't persist after restart).", ToolTipIcon.Warning);

            PushAfterLocalChange();
        }

        // Clears a profile slot back to "(not set)" after confirmation.
        public static void DeleteProfile(char slot)
        {
            Profile target = (slot == 'A') ? ProfileA : ProfileB;

            if (!IsSet(target))
            {
                MessageBox.Show(
                    "Profile " + slot + " has nothing saved to delete.",
                    "Monitor Switch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult answer = MessageBox.Show(
                "Delete \"" + target.Name + "\"?\n\n" +
                "The button will stay in the menu, but you'll need to save a " +
                "setup to it again before it can switch anything.",
                "Delete Profile " + slot,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);   // default to No

            if (answer != DialogResult.Yes) return;

            var empty = new Profile("Profile " + slot + " (not set)");
            empty.UpdatedAtUtc = DateTime.UtcNow;
            if (slot == 'A') { ProfileA = empty; itemA.Text = empty.Name; }
            else             { ProfileB = empty; itemB.Text = empty.Name; }
            if (lastProfile == target) lastProfile = null;

            if (SaveConfig())
                Tray.ShowBalloonTip(2000, "Monitor Switch",
                    "Deleted. Profile " + slot + " is now empty.", ToolTipIcon.Info);
            else
                Tray.ShowBalloonTip(3000, "Monitor Switch",
                    "Deleted for this session, but couldn't update the settings " +
                    "file (it may come back after restart).", ToolTipIcon.Warning);

            PushAfterLocalChange();
        }

        public static string DescribeValues(List<InputSetting> vals)
        {
            var parts = new List<string>();
            for (int i = 0; i < vals.Count; i++)
                parts.Add("Mon " + i + " = " + FriendlyInput((int)vals[i].Value));
            return string.Join(", ", parts);
        }

        public static bool SaveConfig()
        {
            return ConfigStore.Save(ProfileA, ProfileB);
        }

        // ----- sync orchestration ----------------------------------------------
        // Last-write-wins per slot, per machine. Timestamps within a second of
        // each other count as "in sync" (avoids ping-pong from precision loss).

        static readonly TimeSpan SyncSlack = TimeSpan.FromSeconds(1);

        static void RaiseSyncStateChanged()
        {
            var h = SyncStateChanged;
            if (h != null) h();
        }

        // Once-a-day update check; balloon opens the download page on click.
        static bool updateBalloonActive;

        static async void CheckForUpdatesAsync()
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15));   // stay out of startup's way
                string tag = await UpdateCheck.DailyCheckAsync();
                if (tag == null) return;

                Tray.BalloonTipClicked += OnUpdateBalloonClicked;
                Tray.BalloonTipClosed += delegate { updateBalloonActive = false; };
                updateBalloonActive = true;
                Tray.ShowBalloonTip(10000, "Monitor Input Switcher",
                    "Version " + tag.TrimStart('v', 'V') +
                    " is available - click here to download.", ToolTipIcon.Info);
            }
            catch { }
        }

        static void OnUpdateBalloonClicked(object sender, EventArgs e)
        {
            if (!updateBalloonActive) return;
            updateBalloonActive = false;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = UpdateCheck.DownloadPage,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        static async void InitSyncAsync()
        {
            try
            {
                if (await SyncClient.TryRestoreAsync())
                    await SyncNowAsync();
            }
            catch (SyncException)
            {
                // Offline at startup or server trouble - stay quiet; the user
                // can hit "Sync now" later. Local behavior is unaffected.
            }
            RaiseSyncStateChanged();
        }

        // Fire-and-forget push after a local save/delete. Local persistence
        // already succeeded; a cloud failure only costs a warning balloon.
        static async void PushAfterLocalChange()
        {
            if (!SyncClient.IsSignedIn) return;
            try
            {
                await SyncClient.UpsertAsync(LocalRows());
                LastSyncUtc = DateTime.UtcNow;
                RaiseSyncStateChanged();
            }
            catch (SyncException ex)
            {
                Tray.ShowBalloonTip(3000, "Monitor Switch",
                    "Saved on this PC, but cloud backup failed: " + ex.Message,
                    ToolTipIcon.Warning);
            }
        }

        // Two-way sync: pull the account's shared profiles, adopt anything
        // newer than local, push anything local that's newer than the cloud.
        public static async Task SyncNowAsync()
        {
            List<ProfileRow> remote = await SyncClient.FetchAsync();

            bool adopted = false;
            var toPush = new List<ProfileRow>();

            Profile a = ProfileA, b = ProfileB;
            adopted |= MergeSlot("A", ref a, remote, toPush);
            adopted |= MergeSlot("B", ref b, remote, toPush);
            ProfileA = a; ProfileB = b;

            if (toPush.Count > 0)
                await SyncClient.UpsertAsync(toPush);

            if (adopted)
            {
                itemA.Text = ProfileA.Name;
                itemB.Text = ProfileB.Name;
                SaveConfig();
            }

            LastSyncUtc = DateTime.UtcNow;
            RaiseSyncStateChanged();
        }

        static bool MergeSlot(string slot, ref Profile local,
            List<ProfileRow> remote, List<ProfileRow> toPush)
        {
            ProfileRow r = null;
            foreach (var row in remote)
                if (row.Slot == slot) { r = row; break; }

            if (r != null && r.UpdatedAtUtc > local.UpdatedAtUtc + SyncSlack)
            {
                var p = new Profile(r.Name);
                p.Inputs = new List<InputSetting>(r.Inputs);
                p.UpdatedAtUtc = r.UpdatedAtUtc;
                local = p;
                return true;
            }

            // MinValue = untouched built-in default; never worth pushing.
            if (local.UpdatedAtUtc != DateTime.MinValue &&
                (r == null || local.UpdatedAtUtc > r.UpdatedAtUtc + SyncSlack))
            {
                toPush.Add(new ProfileRow
                {
                    Slot = slot,
                    Name = local.Name,
                    Inputs = local.Inputs,
                    UpdatedAtUtc = local.UpdatedAtUtc
                });
            }
            return false;
        }

        // Loads the app icon (embedded MonitorSwitch.ico) at the requested
        // size; falls back to a drawn placeholder if the resource is missing.
        public static Icon AppIcon(int size)
        {
            try
            {
                using (var s = typeof(TrayApp).Assembly
                    .GetManifestResourceStream("MonitorSwitch.AppIcon"))
                {
                    if (s != null) return new Icon(s, size, size);
                }
            }
            catch { }
            return BuildIcon();
        }

        static Icon BuildIcon()
        {
            using (var bmp = new Bitmap(16, 16))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.FillRectangle(Brushes.DodgerBlue, 1, 2, 14, 9);  // screen
                    g.FillRectangle(Brushes.Gray, 6, 11, 4, 2);        // neck
                    g.FillRectangle(Brushes.Gray, 4, 13, 8, 2);        // base
                }
                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    // Clone into a managed Icon that owns its own copy, then free
                    // the unmanaged HICON GetHicon() allocated (it isn't auto-freed).
                    using (var tmp = Icon.FromHandle(hIcon))
                        return (Icon)tmp.Clone();
                }
                finally
                {
                    Native.DestroyIcon(hIcon);
                }
            }
        }

        static List<ProfileRow> LocalRows()
        {
            var rows = new List<ProfileRow>();
            AddLocalRow(rows, "A", ProfileA);
            AddLocalRow(rows, "B", ProfileB);
            return rows;
        }

        static void AddLocalRow(List<ProfileRow> rows, string slot, Profile p)
        {
            if (p.UpdatedAtUtc == DateTime.MinValue) return;   // untouched default
            rows.Add(new ProfileRow
            {
                Slot = slot,
                Name = p.Name,
                Inputs = p.Inputs,
                UpdatedAtUtc = p.UpdatedAtUtc
            });
        }
    }
}
