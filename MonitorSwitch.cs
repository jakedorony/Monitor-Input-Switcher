// MonitorSwitch.cs
// Standalone system-tray monitor input switcher via DDC/CI (VCP code 0x60).
// Build with build.bat (uses the csc.exe that ships with Windows .NET Framework).
//
// Settings are stored per-user at:
//     %APPDATA%\MonitorSwitch\config.txt
// The app manages this file (Save current setup...). Format, one line each:
//     ProfileA = Name | value-mon0, value-mon1, ...
//     ProfileB = Name | value-mon0, value-mon1, ...

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// Assembly metadata - shows up in the exe's Properties, Add/Remove Programs,
// etc. Edit the company/copyright to taste before building a release.
[assembly: AssemblyTitle("Monitor Input Switcher")]
[assembly: AssemblyProduct("Monitor Input Switcher")]
[assembly: AssemblyDescription("Switches monitor inputs from the system tray via DDC/CI.")]
[assembly: AssemblyCompany("Monitor Input Switcher")]
[assembly: AssemblyCopyright("Copyright \u00A9 2026")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace MonitorSwitch
{
    static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        // DefaultDllImportSearchPaths(System32) prevents DLL search-order
        // hijacking: dxva2.dll is not a KnownDLL, so without this a malicious
        // dxva2.dll planted next to the exe could load instead of the real one.
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor, ref uint count);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor, uint count, [Out] PHYSICAL_MONITOR[] monitors);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool SetVCPFeature(IntPtr hMonitor, byte code, uint value);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetVCPFeatureAndVCPFeatureReply(
            IntPtr hMonitor, byte code, IntPtr pvct,
            ref uint currentValue, ref uint maxValue);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr rect, IntPtr data);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(
            IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }

    class Profile
    {
        public string Name;
        public List<uint> Values;   // index = monitor number

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

    static class Ddc
    {
        const byte VCP_INPUT = 0x60;

        public static List<PhysMon> GetMonitors()
        {
            var list = new List<PhysMon>();
            Native.MonitorEnumProc cb = delegate(IntPtr hMon, IntPtr hdc, IntPtr rect, IntPtr data)
            {
                uint count = 0;
                if (Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, ref count) && count > 0)
                {
                    var mons = new Native.PHYSICAL_MONITOR[count];
                    if (Native.GetPhysicalMonitorsFromHMONITOR(hMon, count, mons))
                    {
                        foreach (var m in mons)
                        {
                            list.Add(new PhysMon
                            {
                                Handle = m.hPhysicalMonitor,
                                Description = (m.szPhysicalMonitorDescription ?? "").Trim()
                            });
                        }
                    }
                }
                return true;
            };
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero);
            GC.KeepAlive(cb);
            return list;
        }

        public static void Release(List<PhysMon> monitors)
        {
            foreach (var m in monitors)
                Native.DestroyPhysicalMonitor(m.Handle);
        }

        // Returns number of failures.
        public static int ApplyProfile(Profile p)
        {
            var monitors = GetMonitors();
            int failures = 0;
            try
            {
                if (monitors.Count == 0) return -1;
                for (int i = 0; i < monitors.Count; i++)
                {
                    if (i >= p.Values.Count) continue;   // no value configured for this monitor
                    if (!Native.SetVCPFeature(monitors[i].Handle, VCP_INPUT, p.Values[i]))
                        failures++;
                }
            }
            finally { Release(monitors); }
            return failures;
        }

        public static string DetectInputs()
        {
            var monitors = GetMonitors();
            try
            {
                if (monitors.Count == 0) return "No monitors found.";
                var lines = new List<string>();
                for (int i = 0; i < monitors.Count; i++)
                {
                    uint cur = 0, max = 0;
                    bool ok = Native.GetVCPFeatureAndVCPFeatureReply(
                        monitors[i].Handle, VCP_INPUT, IntPtr.Zero, ref cur, ref max);
                    lines.Add(ok
                        ? string.Format("Monitor {0}: input = {1}", i, cur & 0xFF)
                        : string.Format("Monitor {0}: read failed", i));
                }
                return string.Join("\n", lines.ToArray());
            }
            finally { Release(monitors); }
        }

        // Reads the current input of every monitor into a value list.
        // Returns null if no monitors, or if any monitor's input can't be read
        // (a partial capture would silently produce a broken profile).
        public static List<uint> CaptureCurrent()
        {
            var monitors = GetMonitors();
            try
            {
                if (monitors.Count == 0) return null;
                var vals = new List<uint>();
                for (int i = 0; i < monitors.Count; i++)
                {
                    uint cur = 0, max = 0;
                    if (!Native.GetVCPFeatureAndVCPFeatureReply(
                            monitors[i].Handle, VCP_INPUT, IntPtr.Zero, ref cur, ref max))
                        return null;
                    vals.Add(cur & 0xFF);
                }
                return vals;
            }
            finally { Release(monitors); }
        }

        // Reads the current input of every monitor. One entry per monitor;
        // -1 means that monitor's input couldn't be read. Empty list = no monitors.
        public static List<int> ReadInputs()
        {
            var monitors = GetMonitors();
            try
            {
                var vals = new List<int>();
                for (int i = 0; i < monitors.Count; i++)
                {
                    uint cur = 0, max = 0;
                    bool ok = Native.GetVCPFeatureAndVCPFeatureReply(
                        monitors[i].Handle, VCP_INPUT, IntPtr.Zero, ref cur, ref max);
                    vals.Add(ok ? (int)(cur & 0xFF) : -1);
                }
                return vals;
            }
            finally { Release(monitors); }
        }
    }

    static class Program
    {
        // Single-instance guard. The SAME name is set as AppMutex in
        // MonitorSwitch.iss so the installer/uninstaller can detect a running
        // copy. Plain (session-local) name - do not add a "Global\" prefix.
        const string MutexName = "MonitorInputSwitcher_SingleInstance_7E04BDB0";
        static Mutex singleInstance;

        static Profile profileA = new Profile("Personal PC (Display Port)", 15, 15);
        static Profile profileB = new Profile("Work PC (DVI/HDMI)", 3, 17);
        static Profile lastProfile = null;
        static NotifyIcon tray;
        static ToolStripItem itemA;
        static ToolStripItem itemB;

        [STAThread]
        static void Main()
        {
            // Only one instance may run. If the mutex already exists, another
            // copy is live - point the user at it and exit quietly.
            bool createdNew;
            singleInstance = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    "Monitor Input Switcher is already running.\n\n" +
                    "Look for its icon in the system tray (bottom-right, near " +
                    "the clock - you may need to click the ^ arrow).",
                    "Already Running",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                RunApp();
            }
            finally
            {
                singleInstance.ReleaseMutex();
                singleInstance.Dispose();
            }
        }

        static void RunApp()
        {
            Application.EnableVisualStyles();

            // First run = no config.txt yet. Used to auto-show help below.
            bool firstRun = !File.Exists(ConfigPath());

            LoadConfig();

            tray = new NotifyIcon();
            tray.Icon = BuildIcon();
            tray.Text = "Monitor Input Switcher";
            tray.Visible = true;

            var menu = new ContextMenuStrip();

            var itemOpen = menu.Items.Add("Open Monitor Switcher");
            itemOpen.Font = new Font(itemOpen.Font, FontStyle.Bold);
            itemOpen.Click += delegate { ShowMainWindow(); };

            var itemToggle = menu.Items.Add("Switch (toggle A/B)");
            itemToggle.Click += delegate { ToggleProfiles(); };

            menu.Items.Add(new ToolStripSeparator());

            itemA = menu.Items.Add(profileA.Name);
            itemA.Click += delegate { Apply(profileA); };

            itemB = menu.Items.Add(profileB.Name);
            itemB.Click += delegate { Apply(profileB); };

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
                tray.ShowBalloonTip(5000, "Current Inputs", Ddc.DetectInputs(), ToolTipIcon.Info);
            };

            var itemHelp = menu.Items.Add("How to use...");
            itemHelp.Click += delegate { ShowHelp(); };

            menu.Items.Add(new ToolStripSeparator());

            var itemExit = menu.Items.Add("Exit");
            itemExit.Click += delegate
            {
                tray.Visible = false;
                tray.Dispose();
                Application.Exit();
            };

            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowMainWindow(); };

            // On the very first launch, greet the user and open the guide so
            // they aren't staring at an invisible tray icon wondering what to do.
            if (firstRun)
            {
                tray.ShowBalloonTip(6000, "Monitor Input Switcher",
                    "Running in the tray (bottom-right, near the clock). " +
                    "Opening the quick guide...", ToolTipIcon.Info);
                ShowHelp();
            }

            Application.Run();
        }

        // A profile with no captured values is "not set" (fresh or deleted).
        static bool IsSet(Profile p)
        {
            return p != null && p.Values != null && p.Values.Count > 0;
        }

        // Toggle between A and B. If the natural target is empty but the other
        // profile is saved, switch to the saved one instead of nagging.
        static void ToggleProfiles()
        {
            Profile next = (lastProfile == profileA) ? profileB : profileA;
            Profile other = (next == profileA) ? profileB : profileA;
            if (!IsSet(next) && IsSet(other)) next = other;
            Apply(next);
        }

        // Human-readable label for a VCP 0x60 input value (MCCS standard).
        static string FriendlyInput(int v)
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
        static string DescribeProfile(Profile p)
        {
            if (!IsSet(p)) return "Nothing saved yet.";
            var lines = new List<string>();
            for (int i = 0; i < p.Values.Count; i++)
                lines.Add("Monitor " + i + ":  " + FriendlyInput((int)p.Values[i]));
            return string.Join("\r\n", lines.ToArray());
        }

        // ----- "Start when I sign in" (HKCU Run) --------------------------------
        // Uses the SAME value name ("MonitorSwitch") as the installer's startup
        // task, so the checkbox reflects and controls the installer's setting.
        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValueName = "MonitorSwitch";

        static bool GetStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                    return key != null && key.GetValue(RunValueName) != null;
            }
            catch { return false; }
        }

        static bool SetStartupEnabled(bool enable)
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

        // ----- Main window -------------------------------------------------------
        static Form mainWindow;

        static void ShowMainWindow()
        {
            if (mainWindow != null && !mainWindow.IsDisposed)
            {
                if (mainWindow.WindowState == FormWindowState.Minimized)
                    mainWindow.WindowState = FormWindowState.Normal;
                mainWindow.Activate();
                return;
            }

            var form = new Form();
            form.Text = "Monitor Input Switcher";
            form.FormBorderStyle = FormBorderStyle.FixedSingle;
            form.MaximizeBox = false;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.ClientSize = new Size(484, 478);
            form.Icon = tray.Icon;

            // --- Monitors right now ---
            var gbStatus = new GroupBox();
            gbStatus.Text = "Monitors right now";
            gbStatus.SetBounds(12, 10, 460, 108);

            var statusLabel = new Label();
            statusLabel.AutoSize = false;
            statusLabel.SetBounds(12, 22, 320, 76);
            gbStatus.Controls.Add(statusLabel);

            var btnRefresh = new Button();
            btnRefresh.Text = "Refresh";
            btnRefresh.SetBounds(354, 22, 92, 28);
            gbStatus.Controls.Add(btnRefresh);

            // --- Profile group builder (A and B are identical layouts) ---
            var nameLabels = new Label[2];
            var valueLabels = new Label[2];
            var groups = new GroupBox[2];

            for (int g = 0; g < 2; g++)
            {
                var gb = new GroupBox();
                gb.SetBounds(12, 128 + g * 128, 460, 118);

                var nameLbl = new Label();
                nameLbl.Font = new Font(form.Font, FontStyle.Bold);
                nameLbl.AutoSize = false;
                nameLbl.SetBounds(12, 22, 434, 18);
                gb.Controls.Add(nameLbl);

                var valLbl = new Label();
                valLbl.AutoSize = false;
                valLbl.SetBounds(12, 42, 434, 36);
                gb.Controls.Add(valLbl);

                groups[g] = gb;
                nameLabels[g] = nameLbl;
                valueLabels[g] = valLbl;
            }

            // --- Refresh logic shared by everything below ---
            Action refreshUi = delegate
            {
                // live monitor status
                List<int> inputs = Ddc.ReadInputs();
                if (inputs.Count == 0)
                {
                    statusLabel.Text = "No monitors responding.\r\n" +
                        "Check that DDC/CI is enabled in the monitor's menu.";
                }
                else
                {
                    var lines = new List<string>();
                    for (int i = 0; i < inputs.Count; i++)
                        lines.Add("Monitor " + i + ":  " +
                            (inputs[i] < 0 ? "couldn't read" : FriendlyInput(inputs[i])));
                    statusLabel.Text = string.Join("\r\n", lines.ToArray());
                }
                // profile cards
                groups[0].Text = "Profile A";
                groups[1].Text = "Profile B";
                nameLabels[0].Text = profileA.Name;
                nameLabels[1].Text = profileB.Name;
                valueLabels[0].Text = DescribeProfile(profileA);
                valueLabels[1].Text = DescribeProfile(profileB);
            };

            btnRefresh.Click += delegate { refreshUi(); };

            // --- Per-profile buttons (created after refreshUi exists) ---
            for (int g = 0; g < 2; g++)
            {
                char slot = (g == 0) ? 'A' : 'B';
                bool isA = (g == 0);

                var btnSwitch = new Button();
                btnSwitch.Text = "Switch";
                btnSwitch.SetBounds(12, 82, 100, 28);
                btnSwitch.Click += delegate
                {
                    Apply(isA ? profileA : profileB);
                    refreshUi();
                };
                groups[g].Controls.Add(btnSwitch);

                var btnSave = new Button();
                btnSave.Text = "Save current setup";
                btnSave.SetBounds(120, 82, 140, 28);
                btnSave.Click += delegate
                {
                    CaptureToProfile(slot);
                    refreshUi();
                };
                groups[g].Controls.Add(btnSave);

                var btnDelete = new Button();
                btnDelete.Text = "Delete";
                btnDelete.SetBounds(268, 82, 80, 28);
                btnDelete.Click += delegate
                {
                    DeleteProfile(slot);
                    refreshUi();
                };
                groups[g].Controls.Add(btnDelete);
            }

            // --- Bottom row: toggle, startup, help, close ---
            var btnToggle = new Button();
            btnToggle.Text = "Switch (toggle A / B)";
            btnToggle.SetBounds(12, 396, 168, 32);
            btnToggle.Click += delegate
            {
                ToggleProfiles();
                refreshUi();
            };

            var chkStartup = new CheckBox();
            chkStartup.Text = "Start automatically when I sign in";
            chkStartup.SetBounds(14, 436, 260, 24);
            chkStartup.Checked = GetStartupEnabled();
            bool startupGuard = false;
            chkStartup.CheckedChanged += delegate
            {
                if (startupGuard) return;
                if (!SetStartupEnabled(chkStartup.Checked))
                {
                    MessageBox.Show(
                        "Couldn't update the startup setting.",
                        "Monitor Switch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    startupGuard = true;
                    chkStartup.Checked = GetStartupEnabled();   // revert silently
                    startupGuard = false;
                }
            };

            var btnHelp = new Button();
            btnHelp.Text = "How to use...";
            btnHelp.SetBounds(280, 396, 100, 32);
            btnHelp.Click += delegate { ShowHelp(); };

            var btnClose = new Button();
            btnClose.Text = "Close";
            btnClose.SetBounds(388, 396, 84, 32);
            btnClose.Click += delegate { form.Close(); };

            form.Controls.Add(gbStatus);
            form.Controls.Add(groups[0]);
            form.Controls.Add(groups[1]);
            form.Controls.Add(btnToggle);
            form.Controls.Add(chkStartup);
            form.Controls.Add(btnHelp);
            form.Controls.Add(btnClose);
            form.CancelButton = btnClose;

            refreshUi();

            mainWindow = form;
            form.Show();      // modeless - tray keeps working alongside
            form.Activate();
        }

        static void Apply(Profile p)
        {
            if (!IsSet(p))
            {
                tray.ShowBalloonTip(3000, "Monitor Switch",
                    p.Name + " has nothing saved yet. Right-click the icon and " +
                    "use \"Save current setup...\" first.", ToolTipIcon.Warning);
                return;
            }
            int failures = Ddc.ApplyProfile(p);
            lastProfile = p;
            if (failures == 0)
                tray.ShowBalloonTip(1500, "Monitor Switch",
                    "Switched to: " + p.Name, ToolTipIcon.Info);
            else if (failures < 0)
                tray.ShowBalloonTip(3000, "Monitor Switch",
                    "No DDC/CI-capable monitors found.", ToolTipIcon.Error);
            else
                tray.ShowBalloonTip(3000, "Monitor Switch",
                    p.Name + ": " + failures + " monitor(s) failed (DDC/CI off?)",
                    ToolTipIcon.Warning);
        }

        // Clears a profile slot back to "(not set)" after confirmation.
        static void DeleteProfile(char slot)
        {
            Profile target = (slot == 'A') ? profileA : profileB;

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
            if (slot == 'A') { profileA = empty; itemA.Text = empty.Name; }
            else             { profileB = empty; itemB.Text = empty.Name; }
            if (lastProfile == target) lastProfile = null;

            if (SaveConfig())
                tray.ShowBalloonTip(2000, "Monitor Switch",
                    "Deleted. Profile " + slot + " is now empty.", ToolTipIcon.Info);
            else
                tray.ShowBalloonTip(3000, "Monitor Switch",
                    "Deleted for this session, but couldn't update the settings " +
                    "file (it may come back after restart).", ToolTipIcon.Warning);
        }

        // Built-in end-user guide, shown from the menu and on first run.
        static void ShowHelp()
        {
            string guide =
"MONITOR INPUT SWITCHER - QUICK GUIDE\r\n" +
"\r\n" +
"This app switches your monitors between two inputs. It lives in\r\n" +
"the tray (bottom-right of the screen, near the clock).\r\n" +
"\r\n" +
"  - DOUBLE-CLICK the tray icon to open the main window, which\r\n" +
"    shows what each monitor is on right now and lets you manage\r\n" +
"    everything with buttons.\r\n" +
"  - RIGHT-CLICK the tray icon for the quick menu.\r\n" +
"\r\n" +
"-----------------------------------------------------------\r\n" +
"FIRST: SAVE YOUR TWO SETUPS\r\n" +
"-----------------------------------------------------------\r\n" +
"You only do this once. It teaches the app your two layouts.\r\n" +
"\r\n" +
"  1. Set both monitors to the inputs you want for your first\r\n" +
"     setup (use the monitors' own buttons if needed).\r\n" +
"  2. Right-click the tray icon and choose\r\n" +
"     \"Save current setup as Profile A...\".\r\n" +
"  3. Type a name you'll recognise (e.g. Work PC) and click Save.\r\n" +
"  4. Switch both monitors to your second setup.\r\n" +
"  5. Right-click again, choose \"Save current setup as\r\n" +
"     Profile B...\", name it (e.g. Personal PC), and click Save.\r\n" +
"\r\n" +
"Your two named buttons are now ready and are remembered even\r\n" +
"after you restart the computer.\r\n" +
"\r\n" +
"To redo a setup, just save over it again. To clear one out\r\n" +
"entirely, right-click the icon and choose \"Delete Profile...\".\r\n" +
"\r\n" +
"-----------------------------------------------------------\r\n" +
"EVERY DAY: SWITCHING\r\n" +
"-----------------------------------------------------------\r\n" +
"  - Right-click the tray icon, then click either named button.\r\n" +
"  - Or use \"Switch (toggle A/B)\" in the menu or main window\r\n" +
"    to flip between the two.\r\n" +
"  - A small popup confirms each switch.\r\n" +
"\r\n" +
"Can't see the icon? Click the small ^ arrow near the clock -\r\n" +
"Windows hides new icons there. Drag it onto the taskbar to\r\n" +
"keep it visible.\r\n" +
"\r\n" +
"-----------------------------------------------------------\r\n" +
"IF SOMETHING DOESN'T WORK\r\n" +
"-----------------------------------------------------------\r\n" +
"  - A monitor switches to the wrong input, or a popup says it\r\n" +
"    \"failed\": that monitor's DDC/CI setting is probably off.\r\n" +
"    Open the monitor's menu with its physical buttons, find\r\n" +
"    DDC/CI (often under \"Other\" or \"System\"), turn it On, then\r\n" +
"    save the profile again.\r\n" +
"\r\n" +
"  - You switched a monitor away to your other computer and\r\n" +
"    can't switch it back here: that's expected. This app only\r\n" +
"    controls monitors while they're showing THIS computer.\r\n" +
"\r\n" +
"-----------------------------------------------------------\r\n" +
"TIP: To have the app start automatically when you turn on the\r\n" +
"computer, ask whoever set it up to add it to startup.\r\n";

            var form = new Form();
            form.Text = "How to use - Monitor Input Switcher";
            form.StartPosition = FormStartPosition.CenterScreen;
            form.ClientSize = new Size(520, 460);
            form.MinimumSize = new Size(420, 320);
            form.TopMost = true;

            var box = new TextBox();
            box.Multiline = true;
            box.ReadOnly = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.WordWrap = true;
            box.Dock = DockStyle.Fill;
            box.Font = new Font("Consolas", 9.5f);
            box.BackColor = Color.White;
            box.Text = guide;
            box.Select(0, 0);

            var panel = new Panel();
            panel.Dock = DockStyle.Bottom;
            panel.Height = 44;

            form.Controls.Add(box);
            form.Controls.Add(panel);   // panel now has its real width

            var ok = new Button();
            ok.Text = "Got it";
            ok.DialogResult = DialogResult.OK;
            ok.Size = new Size(90, 28);
            ok.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            ok.Location = new Point(panel.ClientSize.Width - 102, 8);
            panel.Controls.Add(ok);

            form.AcceptButton = ok;

            form.ShowDialog();
            form.Dispose();
        }

        // Reads current monitor inputs, asks for a name via dialog, stores into
        // the chosen slot, and persists to config.txt.
        static void CaptureToProfile(char slot)
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

            Profile existing = (slot == 'A') ? profileA : profileB;
            string detected = DescribeValues(vals);
            string suggestedName = IsSet(existing) ? existing.Name : ("Profile " + slot);

            string name = PromptForName(
                "Save current setup as Profile " + slot,
                "Detected now:\n" + detected + "\n\nName this profile:",
                suggestedName);

            if (name == null) return;            // cancelled
            name = name.Trim();
            if (name.Length == 0) name = "Profile " + slot;

            var prof = new Profile(name);
            prof.Values = vals;
            if (slot == 'A') { profileA = prof; itemA.Text = prof.Name; }
            else             { profileB = prof; itemB.Text = prof.Name; }

            if (SaveConfig())
                tray.ShowBalloonTip(2000, "Monitor Switch",
                    "Saved \"" + name + "\" (" + detected + ")", ToolTipIcon.Info);
            else
                tray.ShowBalloonTip(3000, "Monitor Switch",
                    "Saved for this session, but couldn't write config.txt " +
                    "(it won't persist after restart).", ToolTipIcon.Warning);
        }

        static string DescribeValues(List<uint> vals)
        {
            var parts = new List<string>();
            for (int i = 0; i < vals.Count; i++)
                parts.Add("Mon " + i + " = " + FriendlyInput((int)vals[i]));
            return string.Join(", ", parts.ToArray());
        }

        // Minimal modal text-entry dialog (built in code, no designer files).
        static string PromptForName(string title, string prompt, string defaultText)
        {
            var form = new Form();
            form.Text = title;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.ClientSize = new Size(340, 150);
            form.TopMost = true;

            var label = new Label();
            label.Text = prompt;
            label.SetBounds(12, 12, 316, 70);
            label.AutoSize = false;

            var box = new TextBox();
            box.Text = defaultText;
            box.SetBounds(12, 85, 316, 24);
            box.SelectAll();

            var ok = new Button();
            ok.Text = "Save";
            ok.DialogResult = DialogResult.OK;
            ok.SetBounds(160, 118, 80, 24);

            var cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(248, 118, 80, 24);

            form.Controls.Add(label);
            form.Controls.Add(box);
            form.Controls.Add(ok);
            form.Controls.Add(cancel);
            form.AcceptButton = ok;
            form.CancelButton = cancel;

            DialogResult result = form.ShowDialog();
            string value = box.Text;
            form.Dispose();
            return (result == DialogResult.OK) ? value : null;
        }

        // Per-user settings path: %APPDATA%\MonitorSwitch\config.txt
        static string ConfigPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MonitorSwitch");
            return Path.Combine(dir, "config.txt");
        }

        // Writes both profiles to config.txt in the format LoadConfig reads.
        static bool SaveConfig()
        {
            try
            {
                string path = ConfigPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var lines = new List<string>();
                lines.Add("# Monitor Switch profiles - edit or let the app manage this.");
                lines.Add("# Format:  ProfileA = Name | value-mon0, value-mon1, ...");
                lines.Add(FormatProfileLine("ProfileA", profileA));
                lines.Add(FormatProfileLine("ProfileB", profileB));
                File.WriteAllLines(path, lines.ToArray());
                return true;
            }
            catch
            {
                return false;
            }
        }

        static string FormatProfileLine(string key, Profile p)
        {
            var nums = new List<string>();
            foreach (uint v in p.Values) nums.Add(v.ToString());
            return key + " = " + p.Name + " | " + string.Join(", ", nums.ToArray());
        }

        // Reads %APPDATA%\MonitorSwitch\config.txt if present:
        //   ProfileA = Name | 15, 15
        //   ProfileB = Name | 3, 17
        static void LoadConfig()
        {
            try
            {
                string path = ConfigPath();
                if (!File.Exists(path)) return;

                foreach (string raw in File.ReadAllLines(path))
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
                    if (valuesText.Length > 0)   // empty = deleted/unset profile
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
                    if (key == "profilea") profileA = prof;
                    else if (key == "profileb") profileB = prof;
                }
            }
            catch
            {
                // Bad config -> silently keep built-in defaults.
            }
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
    }
}
