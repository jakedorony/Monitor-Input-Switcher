// MainWindow.cs - the modeless main window (singleton), themed.
//
// Layout (top to bottom): header (icon, title, sync pill, theme pill, gear),
// the big Personal/Work switch, one tile per connected monitor with a live
// input picker, the two profile cards with per-monitor pickers, and a footer
// (start with Windows, account, Advanced). Everything is rebuilt from the
// current palette on theme change, and refreshed on profile/sync changes and
// whenever the window is activated.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MonitorSwitch
{
    static class MainWindow
    {
        static MainForm window;

        public static void ShowWindow()
        {
            if (window != null && !window.IsDisposed)
            {
                if (window.WindowState == FormWindowState.Minimized)
                    window.WindowState = FormWindowState.Normal;
                window.Activate();
                return;
            }
            window = new MainForm();
            window.Show();
            window.Activate();
        }
    }

    class MainForm : Form
    {
        float scale;
        Palette P { get { return Theme.Current; } }
        bool building;
        Timer settleTimer;                 // re-read inputs a moment after a switch
        readonly HashSet<string> warming = new HashSet<string>();

        int L(float logical) { return (int)Math.Round(logical * scale); }

        public MainForm()
        {
            scale = DeviceDpi / 96f;
            Text = "Monitor Input Switcher";
            Icon = TrayApp.AppIcon(32);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;     // we scale by DeviceDpi ourselves
            ClientSize = new Size(L(520), L(700));
            Font = Theme.Body;
            DoubleBuffered = true;

            settleTimer = new Timer { Interval = 1500 };
            settleTimer.Tick += delegate { settleTimer.Stop(); Refresh(); };

            TrayApp.ProfilesChanged += Refresh;
            TrayApp.SyncStateChanged += Refresh;
            Theme.Changed += OnThemeChanged;
            FormClosed += delegate
            {
                TrayApp.ProfilesChanged -= Refresh;
                TrayApp.SyncStateChanged -= Refresh;
                Theme.Changed -= OnThemeChanged;
                settleTimer.Dispose();
            };
            Activated += delegate { if (!building) Refresh(); };

            BuildUi();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this);
        }

        void OnThemeChanged()
        {
            Theme.ApplyTitleBar(this);
            BuildUi();
        }

        public new void Refresh()
        {
            if (IsDisposed) return;
            BuildUi();
        }

        // ------------------------------------------------------------------
        // Build
        // ------------------------------------------------------------------

        void BuildUi()
        {
            if (building) return;
            building = true;
            SuspendLayout();
            try
            {
                foreach (Control c in Controls) c.Dispose();
                Controls.Clear();
                BackColor = P.Bg;
                ForeColor = P.Text;

                var live = Ddc.ReadInputs();

                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                    BackColor = P.Bg, Margin = Padding.Empty, Padding = Padding.Empty
                };
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, L(52)));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, L(46)));
                root.Controls.Add(BuildHeader(), 0, 0);
                root.Controls.Add(BuildBody(live), 0, 1);
                root.Controls.Add(BuildFooter(), 0, 2);
                Controls.Add(root);

                WarmCapabilities(live);
            }
            finally
            {
                ResumeLayout(true);
                building = false;
            }
        }

        Control BuildHeader()
        {
            var bar = new Panel { Dock = DockStyle.Fill, BackColor = P.Card, Margin = Padding.Empty };
            bar.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(P.Border)) e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
            };

            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, BackColor = Color.Transparent,
                Padding = new Padding(L(16), 0, L(12), L(1))
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, L(34)));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, L(70)));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, L(36)));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var icon = new PictureBox
            {
                Image = TrayApp.AppIcon(24).ToBitmap(), SizeMode = PictureBoxSizeMode.CenterImage,
                Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty
            };
            var title = new Label
            {
                Text = "Monitor Input Switcher", Font = Theme.Title, ForeColor = P.Text,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(L(6), 0, 0, 0)
            };

            bool signedIn = SyncClient.IsSignedIn;
            var pill = new StatusPill
            {
                Text = TrayApp.SyncStatusText(),
                Fill = signedIn ? P.OkBg : P.Track,
                TextColor = signedIn ? P.OkFg : P.Muted,
                Glyph = signedIn ? Theme.GlyphCheck : Theme.GlyphSync,
                Anchor = AnchorStyles.None, Margin = new Padding(L(8), 0, L(8), 0)
            };
            pill.FitToText();
            pill.Cursor = Cursors.Hand;
            pill.Click += delegate { SettingsWindow.ShowWindow(); };

            var theme = new ThemePill
            {
                Dark = P.IsDark, Track = P.Track, Card = P.Card, TextColor = P.Text, Muted = P.Muted,
                Size = new Size(L(58), L(26)), Anchor = AnchorStyles.None, Margin = Padding.Empty
            };
            theme.Clicked += delegate { TrayApp.SetTheme(P.IsDark ? ThemeMode.Light : ThemeMode.Dark); };
            new ToolTip().SetToolTip(theme, "Light / dark. Settings can follow Windows instead.");

            var gear = new GlyphButton
            {
                Glyph = Theme.GlyphGear, TextColor = P.Muted, HoverFill = P.Hover,
                Size = new Size(L(30), L(30)), Anchor = AnchorStyles.None, Margin = Padding.Empty
            };
            gear.Click += delegate { SettingsWindow.ShowWindow(); };

            row.Controls.Add(icon, 0, 0);
            row.Controls.Add(title, 1, 0);
            row.Controls.Add(pill, 2, 0);
            row.Controls.Add(theme, 3, 0);
            row.Controls.Add(gear, 4, 0);
            bar.Controls.Add(row);
            return bar;
        }

        Control BuildBody(List<MonitorInput> live)
        {
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, BackColor = P.Bg,
                Padding = new Padding(L(20), L(14), L(20), 0), Margin = Padding.Empty
            };
            int tiles = Math.Max(1, live.Count);
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, L(22)));                 // caption
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, L(84 + 16)));            // switch
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, L(24)));                 // "your monitors"
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, L(tiles * 62)));         // tiles
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, L(24)));                 // "profiles"
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));                    // cards

            var caption = new Label
            {
                Text = "Your monitors are showing", Font = Theme.Small, ForeColor = P.Muted,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Margin = Padding.Empty
            };
            body.Controls.Add(caption, 0, 0);
            body.Controls.Add(BuildSwitch(live), 0, 1);
            body.Controls.Add(SectionLabel("YOUR MONITORS"), 0, 2);
            body.Controls.Add(BuildTiles(live), 0, 3);
            body.Controls.Add(SectionLabel("PROFILES"), 0, 4);
            body.Controls.Add(BuildProfiles(live), 0, 5);
            return body;
        }

        Label SectionLabel(string text)
        {
            return new Label
            {
                Text = text, Font = Theme.Caption, ForeColor = P.Muted,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, Margin = Padding.Empty
            };
        }

        Control BuildSwitch(List<MonitorInput> live)
        {
            Profile a = TrayApp.ProfileA, b = TrayApp.ProfileB;
            int onA = Ddc.CountOnProfile(a, live), onB = Ddc.CountOnProfile(b, live);
            int active = onA > onB ? 0 : (onB > onA ? 1 : -1);

            var sw = new SegmentedSwitch
            {
                LeftTitle = a.Name, RightTitle = b.Name,
                LeftEnabled = TrayApp.IsSet(a), RightEnabled = TrayApp.IsSet(b),
                Active = active,
                Track = P.Track, Accent = P.Accent, AccentText = P.AccentText, Muted = P.Muted, TextColor = P.Text,
                Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, L(16))
            };
            sw.LeftSub = !sw.LeftEnabled ? "not set up" : (active == 0 ? "showing now" : "click to switch");
            sw.RightSub = !sw.RightEnabled ? "not set up" : (active == 1 ? "showing now" : "click to switch");
            sw.SegmentClicked += delegate(int seg)
            {
                TrayApp.Apply(seg == 0 ? TrayApp.ProfileA : TrayApp.ProfileB);
                Refresh();
                settleTimer.Stop(); settleTimer.Start();
            };
            return sw;
        }

        Control BuildTiles(List<MonitorInput> live)
        {
            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty
            };
            if (live.Count == 0)
            {
                stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                var card = NewCard();
                card.Controls.Add(new Label
                {
                    Text = "No monitors responding. Check that DDC/CI is enabled in the monitor's menu.",
                    Font = Theme.Body, ForeColor = P.Muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
                });
                stack.Controls.Add(card, 0, 0);
                return stack;
            }

            for (int i = 0; i < live.Count; i++)
            {
                stack.RowStyles.Add(new RowStyle(SizeType.Absolute, L(62)));
                stack.Controls.Add(BuildTile(live[i]), 0, i);
            }
            return stack;
        }

        Control BuildTile(MonitorInput m)
        {
            var card = NewCard();
            card.Margin = new Padding(0, 0, 0, L(8));
            card.Padding = new Padding(L(12), L(8), L(12), L(8));

            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, L(34)));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, L(150)));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var glyph = new Label
            {
                Text = Theme.GlyphMonitor, Font = Theme.GlyphLarge, ForeColor = P.Text,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty
            };
            var text = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty };
            text.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            text.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            text.Controls.Add(new Label
            {
                Text = MonitorNames.Friendly(m.Id), Font = Theme.Strong, ForeColor = P.Text,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, Margin = Padding.Empty, AutoEllipsis = true
            }, 0, 0);
            text.Controls.Add(new Label
            {
                Text = m.Value < 0 ? "Couldn't read this monitor" : "Showing " + TrayApp.InputName(m.Value),
                Font = Theme.Small, ForeColor = P.Muted,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft, Margin = Padding.Empty, AutoEllipsis = true
            }, 0, 1);

            var picker = NewPicker();
            picker.Anchor = AnchorStyles.Right;
            picker.Width = L(146);
            uint[] choices = Ddc.CachedInputs(m.Id) ?? Ddc.FallbackInputs;
            picker.SetItems(choices, m.Value < 0 ? 0 : (uint)m.Value);
            string id = m.Id;
            picker.ValueChanged += delegate
            {
                if (building) return;
                uint v = picker.SelectedValue2;
                if (v == 0 || (int)v == m.Value) return;
                TrayApp.SetMonitorInput(id, v);
                settleTimer.Stop(); settleTimer.Start();
            };
            new ToolTip().SetToolTip(picker, "Switch this monitor right now");

            row.Controls.Add(glyph, 0, 0);
            row.Controls.Add(text, 1, 0);
            row.Controls.Add(picker, 2, 0);
            card.Controls.Add(row);
            return card;
        }

        Control BuildProfiles(List<MonitorInput> live)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, L(12))
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            int onA = Ddc.CountOnProfile(TrayApp.ProfileA, live), onB = Ddc.CountOnProfile(TrayApp.ProfileB, live);
            grid.Controls.Add(BuildProfileCard('A', TrayApp.ProfileA, live, onA > onB), 0, 0);
            grid.Controls.Add(BuildProfileCard('B', TrayApp.ProfileB, live, onB > onA), 1, 0);
            return grid;
        }

        Control BuildProfileCard(char slot, Profile p, List<MonitorInput> live, bool active)
        {
            var card = NewCard();
            card.Margin = new Padding(slot == 'A' ? 0 : L(5), 0, slot == 'A' ? L(5) : 0, 0);
            card.Padding = new Padding(L(12), L(10), L(12), L(10));
            if (active) { card.Border = P.Accent; card.BorderWidth = 2; }

            bool isSet = TrayApp.IsSet(p);
            var col = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, L(24)));          // name + badge

            // name row
            var nameRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
            nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            nameRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            nameRow.Controls.Add(new Label
            {
                Text = p.Name, Font = Theme.Strong, ForeColor = P.Text, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty, AutoEllipsis = true
            }, 0, 0);
            nameRow.Controls.Add(new Label
            {
                Text = active ? "ACTIVE" : "", Font = Theme.Caption, ForeColor = P.Accent,
                AutoSize = true, Anchor = AnchorStyles.Right, Margin = Padding.Empty
            }, 1, 0);
            col.Controls.Add(nameRow, 0, 0);

            int r = 1;
            if (!isSet)
            {
                col.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                col.Controls.Add(new Label
                {
                    Text = "Nothing saved yet. Set the monitors how you want them, then Save current setup.",
                    Font = Theme.Small, ForeColor = P.Muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft,
                    Margin = new Padding(0, L(6), 0, 0)
                }, 0, r++);
            }
            else
            {
                foreach (var m in live)
                {
                    col.RowStyles.Add(new RowStyle(SizeType.Absolute, L(34)));
                    col.Controls.Add(BuildPickerRow(slot, p, m), 0, r++);
                }
                col.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                col.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty }, 0, r++);
            }

            // actions
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, L(22)));
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
                BackColor = Color.Transparent, Margin = Padding.Empty
            };
            actions.Controls.Add(Link(isSet ? "Save current" : "Save current setup", delegate { TrayApp.CaptureToProfile(slot); Refresh(); }));
            if (isSet)
            {
                actions.Controls.Add(Link("Rename", delegate
                {
                    string name = Prompt.ForName("Rename Profile " + slot, "New name for this profile:", p.Name);
                    if (name != null) TrayApp.RenameProfile(slot, name);
                }));
                actions.Controls.Add(Link("Delete", delegate { TrayApp.DeleteProfile(slot); Refresh(); }));
            }
            col.Controls.Add(actions, 0, r);

            card.Controls.Add(col);
            return card;
        }

        Control BuildPickerRow(char slot, Profile p, MonitorInput m)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent,
                Margin = new Padding(0, L(2), 0, L(2))
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, L(132)));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            uint current = 0;
            foreach (var e in p.Inputs) if (e.Matches(m.Id)) { current = e.Value; break; }

            row.Controls.Add(new Label
            {
                Text = ShortName(m.Id), Font = Theme.Small, ForeColor = P.Muted,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty, AutoEllipsis = true
            }, 0, 0);

            var picker = NewPicker();
            picker.Dock = DockStyle.Fill;
            picker.Margin = Padding.Empty;
            picker.Font = Theme.Small;
            uint[] choices = Ddc.CachedInputs(m.Id) ?? Ddc.FallbackInputs;
            if (current == 0)
            {
                // not saved for this monitor yet: lead with a "Not set" entry
                var withUnset = new List<uint> { 0 };
                withUnset.AddRange(choices);
                choices = withUnset.ToArray();
            }
            picker.SetItems(choices, current);
            string id = m.Id;
            picker.ValueChanged += delegate
            {
                if (building) return;
                uint v = picker.SelectedValue2;
                if (v == 0 || v == current) return;
                TrayApp.UpdateProfileInput(slot, id, v);
            };
            row.Controls.Add(picker, 1, 0);
            return row;
        }

        Control BuildFooter()
        {
            var bar = new Panel { Dock = DockStyle.Fill, BackColor = P.Bg, Margin = Padding.Empty };
            bar.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(P.Border)) e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
            };
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.Transparent,
                Padding = new Padding(L(20), 0, L(20), 0)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, L(42)));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var toggle = new ToggleSwitch
            {
                On = TrayApp.GetStartupEnabled(), Accent = P.Accent, Track = P.Track, Knob = Color.White,
                Size = new Size(L(34), L(18)), Anchor = AnchorStyles.Left, Margin = Padding.Empty
            };
            toggle.Toggled += delegate
            {
                if (!TrayApp.SetStartupEnabled(toggle.On))
                {
                    toggle.On = TrayApp.GetStartupEnabled();
                    MessageBox.Show(this, "Couldn't update the startup setting.", "Monitor Switch",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            var startLbl = new Label
            {
                Text = "Start with Windows", Font = Theme.Small, ForeColor = P.Muted,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty
            };
            startLbl.Cursor = Cursors.Hand;
            startLbl.Click += delegate { toggle.On = !toggle.On; };

            var account = Link(SyncClient.IsSignedIn ? SyncClient.Email : "Sign in to sync",
                delegate { SettingsWindow.ShowWindow(); });
            account.TextColor = SyncClient.IsSignedIn ? P.Muted : P.Accent;
            account.Margin = new Padding(0, 0, L(14), 0);
            var advanced = Link("Advanced", delegate { SettingsWindow.ShowWindow(); });

            row.Controls.Add(toggle, 0, 0);
            row.Controls.Add(startLbl, 1, 0);
            row.Controls.Add(account, 2, 0);
            row.Controls.Add(advanced, 3, 0);
            bar.Controls.Add(row);
            return bar;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        Card NewCard()
        {
            return new Card
            {
                Fill = P.Card, Border = P.Border, Radius = 8f, Dock = DockStyle.Fill,
                Margin = Padding.Empty, Padding = new Padding(L(12))
            };
        }

        InputPicker NewPicker()
        {
            return new InputPicker
            {
                Fill = P.Field, Border = P.Border, TextColor = P.Text, Muted = P.Muted,
                MenuBack = P.Card, MenuHover = P.Hover, Accent = P.Accent,
                Font = Theme.Body, Height = L(26)
            };
        }

        LinkAction Link(string text, EventHandler onClick)
        {
            var l = new LinkAction
            {
                Text = text, Font = Theme.Small, TextColor = P.Accent,
                Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, L(12), 0)
            };
            l.FitToText();
            l.Click += onClick;
            return l;
        }

        // "ASUS PG27AQDM" -> "ASUS" for the tight picker rows.
        static string ShortName(string monitorId)
        {
            string full = MonitorNames.Friendly(monitorId);
            int sp = full.IndexOf(' ');
            string first = sp > 0 ? full.Substring(0, sp) : full;
            int hash = monitorId.IndexOf('#');
            return hash > 0 ? first + " (" + monitorId.Substring(hash + 1) + ")" : first;
        }

        // First paint uses the generic input list; read each monitor's real
        // list once in the background and repaint when it arrives.
        void WarmCapabilities(List<MonitorInput> live)
        {
            var missing = new List<string>();
            foreach (var m in live)
                if (!string.IsNullOrEmpty(m.Id) && Ddc.CachedInputs(m.Id) == null && warming.Add(m.Id))
                    missing.Add(m.Id);
            if (missing.Count == 0) return;

            Task.Run(delegate
            {
                foreach (var id in missing) Ddc.SupportedInputs(id);
            }).ContinueWith(delegate
            {
                if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(Refresh));
            });
        }
    }
}
