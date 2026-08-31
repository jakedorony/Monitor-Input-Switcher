// SettingsWindow.cs - account/sync, appearance, startup, monitor matching,
// help. Modeless singleton; rebuilt on theme change like the main window.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorSwitch
{
    static class SettingsWindow
    {
        static SettingsForm window;

        public static void ShowWindow()
        {
            if (window != null && !window.IsDisposed)
            {
                window.Activate();
                return;
            }
            window = new SettingsForm();
            window.Show();
            window.Activate();
        }
    }

    class SettingsForm : Form
    {
        float scale;
        Palette P { get { return Theme.Current; } }
        bool building;
        string status = "";             // transient message under the account section

        int L(float logical) { return (int)Math.Round(logical * scale); }

        public SettingsForm()
        {
            scale = DeviceDpi / 96f;
            Text = "Settings - Monitor Input Switcher";
            Icon = TrayApp.AppIcon(32);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(L(460), L(780));
            Font = Theme.Body;
            DoubleBuffered = true;

            TrayApp.SyncStateChanged += Rebuild;
            TrayApp.ProfilesChanged += Rebuild;
            Theme.Changed += OnThemeChanged;
            FormClosed += delegate
            {
                TrayApp.SyncStateChanged -= Rebuild;
                TrayApp.ProfilesChanged -= Rebuild;
                Theme.Changed -= OnThemeChanged;
            };
            Build();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this);
        }

        void OnThemeChanged() { Theme.ApplyTitleBar(this); Build(); }
        void Rebuild() { if (!IsDisposed) Build(); }

        void Build()
        {
            if (building) return;
            building = true;
            SuspendLayout();
            try
            {
                foreach (Control c in Controls) c.Dispose();
                Controls.Clear();
                BackColor = P.Bg;

                var stack = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
                    AutoScroll = true, BackColor = P.Bg, Padding = new Padding(L(20), L(16), L(20), L(16))
                };
                stack.Controls.Add(Section("Account & sync", BuildAccount()));
                stack.Controls.Add(Section("Appearance", BuildAppearance()));
                stack.Controls.Add(Section("Startup", BuildStartup()));
                stack.Controls.Add(Section("Monitor matching", BuildMatching()));
                stack.Controls.Add(Section("Dock button", BuildDock()));
                stack.Controls.Add(Section("Help", BuildHelp()));
                Controls.Add(stack);
            }
            finally
            {
                ResumeLayout(true);
                building = false;
            }
        }

        Control Section(string title, Control content)
        {
            int inner = L(392);
            var card = new Card
            {
                Fill = P.Card, Border = P.Border, Radius = 8f, Width = L(420),
                Margin = new Padding(0, 0, 0, L(12)), Padding = new Padding(L(14), L(10), L(14), L(12))
            };
            var col = new TableLayoutPanel
            {
                ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent, Margin = Padding.Empty,
                Location = new Point(card.Padding.Left, card.Padding.Top), Width = inner,
                MaximumSize = new Size(inner, 0), MinimumSize = new Size(inner, 0)
            };
            col.Controls.Add(new Label
            {
                Text = title, Font = Theme.Strong, ForeColor = P.Text, AutoSize = true,
                UseMnemonic = false, Margin = new Padding(0, 0, 0, L(8))
            });
            content.Margin = Padding.Empty;
            col.Controls.Add(content);
            card.Controls.Add(col);
            // The card is a plain panel; size it to what the column needs.
            Size pref = col.GetPreferredSize(new Size(inner, 0));
            col.Size = new Size(inner, pref.Height);
            card.Height = pref.Height + card.Padding.Vertical;
            return card;
        }

        Label Note(string text)
        {
            return new Label
            {
                Text = text, Font = Theme.Small, ForeColor = P.Muted, AutoSize = true,
                MaximumSize = new Size(L(390), 0), Margin = new Padding(0, 0, 0, L(8))
            };
        }

        FlatButton Button(string text, bool primary, EventHandler onClick)
        {
            var b = new FlatButton
            {
                Text = text, Primary = primary, Font = Theme.Body,
                Fill = P.Card, Border = P.Border, TextColor = P.Text, HoverFill = P.Hover,
                AccentFill = P.Accent, AccentText = P.AccentText,
                Margin = new Padding(0, 0, L(8), 0)
            };
            int w = TextRenderer.MeasureText(text, Theme.Body).Width + L(28);
            b.Size = new Size(Math.Max(L(110), w), L(30));
            b.Click += onClick;
            return b;
        }

        TextBox Field(string placeholder, bool password)
        {
            var t = new TextBox
            {
                PlaceholderText = placeholder, Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle,
                BackColor = P.Field, ForeColor = P.Text, Width = L(186), Margin = new Padding(0, 0, L(8), L(8)),
                UseSystemPasswordChar = password
            };
            return t;
        }

        FlowLayoutPanel Row()
        {
            return new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent, Margin = Padding.Empty
            };
        }

        TableLayoutPanel Col()
        {
            return new TableLayoutPanel
            {
                ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent, Margin = Padding.Empty
            };
        }

        // ---------------- sections ----------------

        Control BuildAccount()
        {
            var col = Col();
            if (SyncClient.IsSignedIn)
            {
                col.Controls.Add(Note("Signed in as " + SyncClient.Email + ". Your two profiles are shared with every computer signed in to this account. " + TrayApp.SyncStatusText() + "."));
                var row = Row();
                row.Controls.Add(Button("Sync now", true, async delegate(object s, EventArgs e)
                {
                    var b = (FlatButton)s; b.Enabled = false; SetStatus("Syncing...");
                    try { await TrayApp.SyncNowAsync(); SetStatus("Up to date."); }
                    catch (SyncException ex) { SetStatus(ex.Message); }
                    if (!b.IsDisposed) b.Enabled = true;
                }));
                row.Controls.Add(Button("Sign out", false, delegate { SyncClient.SignOut(); status = ""; Build(); }));
                col.Controls.Add(row);

                var danger = new LinkAction
                {
                    Text = "Delete my account...", Font = Theme.Small, TextColor = P.Muted,
                    Margin = new Padding(0, L(12), 0, 0)
                };
                danger.FitToText();
                danger.Click += async delegate
                {
                    var answer = MessageBox.Show(this,
                        "Delete the account " + SyncClient.Email + " and everything it stores online?" + Environment.NewLine + Environment.NewLine +
                        "Your profiles stay on this PC, but they will no longer sync, and other " +
                        "computers signed in to this account will lose the cloud copy." + Environment.NewLine + Environment.NewLine +
                        "This cannot be undone.",
                        "Delete account", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2);
                    if (answer != DialogResult.Yes) return;
                    danger.Enabled = false; SetStatus("Deleting account...");
                    try
                    {
                        await TrayApp.DeleteAccountAsync();
                        status = "";
                        Build();
                    }
                    catch (SyncException ex)
                    {
                        SetStatus("Couldn't delete the account: " + ex.Message);
                        if (!danger.IsDisposed) danger.Enabled = true;
                    }
                };
                col.Controls.Add(danger);
            }
            else
            {
                col.Controls.Add(Note("Sign in to share your two profiles across all the computers plugged into these monitors. Create an account once; then sign in with it on each PC."));
                var email = Field("Email", false);
                var pw = Field("Password", true);
                var fields = Row(); fields.Controls.Add(email); fields.Controls.Add(pw);
                col.Controls.Add(fields);
                var row = Row();
                FlatButton signIn = null, signUp = null;
                signIn = Button("Sign in", true, async delegate
                {
                    signIn.Enabled = signUp.Enabled = false; SetStatus("Signing in...");
                    try
                    {
                        await SyncClient.SignInAsync(email.Text.Trim(), pw.Text);
                        SetStatus("Syncing...");
                        await TrayApp.SyncNowAsync();
                        status = "";
                        Build();
                        return;
                    }
                    catch (SyncException ex) { SetStatus(ex.Message); }
                    if (!signIn.IsDisposed) signIn.Enabled = signUp.Enabled = true;
                });
                signUp = Button("Create account", false, async delegate
                {
                    signIn.Enabled = signUp.Enabled = false; SetStatus("Creating account...");
                    try
                    {
                        bool now = await SyncClient.SignUpAsync(email.Text.Trim(), pw.Text);
                        if (now) { await TrayApp.SyncNowAsync(); status = ""; Build(); return; }
                        SetStatus("Almost done - click the confirmation link we emailed you, then press Sign in.");
                    }
                    catch (SyncException ex) { SetStatus(ex.Message); }
                    if (!signIn.IsDisposed) signIn.Enabled = signUp.Enabled = true;
                });
                row.Controls.Add(signIn); row.Controls.Add(signUp);
                col.Controls.Add(row);
            }
            statusLabel = new Label
            {
                Text = status, Font = Theme.Small, ForeColor = P.WarnFg, AutoSize = true,
                MaximumSize = new Size(L(390), 0), Margin = new Padding(0, L(8), 0, 0)
            };
            col.Controls.Add(statusLabel);
            return col;
        }

        Label statusLabel;
        void SetStatus(string s)
        {
            status = s;
            if (statusLabel != null && !statusLabel.IsDisposed) statusLabel.Text = s;
        }

        Control BuildAppearance()
        {
            var col = Col();
            col.Controls.Add(Note("Choose a look, or follow the Windows light/dark setting."));
            var row = Row();
            foreach (ThemeMode m in new[] { ThemeMode.System, ThemeMode.Light, ThemeMode.Dark })
            {
                ThemeMode mode = m;
                row.Controls.Add(Button(mode == ThemeMode.System ? "Follow Windows" : mode.ToString(),
                    Theme.Mode == mode, delegate { TrayApp.SetTheme(mode); if (Theme.Mode == mode) Build(); }));
            }
            col.Controls.Add(row);
            return col;
        }

        Control BuildStartup()
        {
            var row = Row();
            var toggle = new ToggleSwitch
            {
                On = TrayApp.GetStartupEnabled(), Accent = P.Accent, Track = P.Track, Knob = Color.White,
                Size = new Size(L(34), L(18)), Margin = new Padding(0, L(3), L(10), 0)
            };
            var lbl = new Label { Text = "Start automatically when I sign in to Windows", Font = Theme.Body, ForeColor = P.Text, AutoSize = true };
            toggle.Toggled += delegate
            {
                if (!TrayApp.SetStartupEnabled(toggle.On)) toggle.On = TrayApp.GetStartupEnabled();
            };
            lbl.Cursor = Cursors.Hand;
            lbl.Click += delegate { toggle.On = !toggle.On; };
            row.Controls.Add(toggle); row.Controls.Add(lbl);
            return row;
        }

        Control BuildMatching()
        {
            var col = Col();
            col.Controls.Add(Note("Some monitors identify themselves differently on each computer, so the app works out which ones are the same and remembers it. If a profile ever switches the wrong monitor, clear what it learned and switch again. Your profiles are not affected."));
            int learned = TrayApp.LearnedMatchCount();
            var row = Row();
            var info = new Label
            {
                Text = learned == 0 ? "Nothing learned yet." : learned + " learned match" + (learned == 1 ? "" : "es") + " remembered.",
                Font = Theme.Small, ForeColor = P.Muted, AutoSize = true, Margin = new Padding(0, L(7), 0, 0)
            };
            row.Controls.Add(Button("Clear learned matches", false, delegate
            {
                int removed = TrayApp.ForgetLearnedMatches();
                info.Text = removed == 0 ? "There was nothing learned to clear." : "Cleared " + removed + " learned match" + (removed == 1 ? "" : "es") + ".";
            }));
            row.Controls.Add(info);
            col.Controls.Add(row);
            return col;
        }

        Control BuildDock()
        {
            var col = Col();
            col.Controls.Add(Note("If your dock or KVM switch has a button that moves your keyboard and mouse to another computer, the app can notice the press and switch the monitors to match."));

            var d = ConfigStore.Dock;
            if (d.Signatures.Count == 0)
            {
                var row0 = Row();
                row0.Controls.Add(Button("Set up dock...", true, delegate { RunDockWizard(); }));
                col.Controls.Add(row0);
                return col;
            }

            var model = DockLibrary.Match(d.Signatures);
            string what = model != null ? model.Name : "Custom dock (" + string.Join(", ", d.Signatures) + ")";
            string seen = DockWatch.LastMatchUtc == DateTime.MinValue
                ? "" : "  ·  last activity " + DockWatch.LastMatchUtc.ToLocalTime().ToString("t");
            col.Controls.Add(Note("Watching: " + what + seen));

            var rowEn = Row();
            var toggle = new ToggleSwitch
            {
                On = d.Enabled, Accent = P.Accent, Track = P.Track, Knob = Color.White,
                Size = new Size(L(34), L(18)), Margin = new Padding(0, L(3), L(10), L(8))
            };
            toggle.Toggled += delegate
            {
                ConfigStore.Dock.Enabled = toggle.On;
                TrayApp.SaveConfig();
                DockWatch.Reconfigure();
            };
            rowEn.Controls.Add(toggle);
            rowEn.Controls.Add(new Label { Text = "Switch monitors when the dock button is pressed", Font = Theme.Body, ForeColor = P.Text, AutoSize = true, Margin = new Padding(0, L(2), 0, 0) });
            col.Controls.Add(rowEn);

            col.Controls.Add(DockDirectionRow("When the dock leaves this PC", d.OnDeparted,
                delegate(string v) { ConfigStore.Dock.OnDeparted = v; TrayApp.SaveConfig(); }));
            col.Controls.Add(DockDirectionRow("When the dock comes back", d.OnArrived,
                delegate(string v) { ConfigStore.Dock.OnArrived = v; TrayApp.SaveConfig(); }));

            var rowBtn = Row();
            rowBtn.Controls.Add(Button("Set up again...", false, delegate { RunDockWizard(); }));
            col.Controls.Add(rowBtn);
            return col;
        }

        Control DockDirectionRow(string label, string current, Action<string> save)
        {
            var row = Row();
            row.Controls.Add(new Label
            {
                Text = label, Font = Theme.Small, ForeColor = P.Muted, AutoSize = false,
                Size = new Size(L(190), L(26)), TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, L(8), L(6))
            });
            var picker = new InputPicker
            {
                Fill = P.Field, Border = P.Border, TextColor = P.Text, Muted = P.Muted,
                MenuBack = P.Card, MenuHover = P.Hover, Accent = P.Accent,
                Font = Theme.Small, Size = new Size(L(180), L(26)), Margin = new Padding(0, 0, 0, L(6))
            };
            var items = new System.Collections.Generic.List<InputPicker.Item>
            {
                new InputPicker.Item { Value = 0, Label = "Do nothing" },
                new InputPicker.Item { Value = 1, Label = "Switch to " + TrayApp.ProfileA.Name },
                new InputPicker.Item { Value = 2, Label = "Switch to " + TrayApp.ProfileB.Name }
            };
            uint cur = current == "A" ? 1u : current == "B" ? 2u : 0u;
            picker.SetCustomItems(items, cur);
            picker.ValueChanged += delegate
            {
                uint v = picker.SelectedValue2;
                save(v == 1 ? "A" : v == 2 ? "B" : null);
            };
            row.Controls.Add(picker);
            return row;
        }

        void RunDockWizard()
        {
            using (var w = new DockWizard())
            {
                w.ShowDialog(this);
            }
            DockWatch.Reconfigure();
            Build();
        }

        Control BuildHelp()
        {
            var col = Col();
            var row = Row();
            row.Controls.Add(Button("How to use...", false, delegate { HelpWindow.ShowHelp(); }));
            row.Controls.Add(Button("Check for updates", false, delegate
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = UpdateCheck.DownloadPage, UseShellExecute = true });
                }
                catch { }
            }));
            col.Controls.Add(row);
            string ver = Application.ProductVersion.Split('+')[0];
            col.Controls.Add(new Label
            {
                Text = "Monitor Input Switcher " + ver, Font = Theme.Small, ForeColor = P.Muted, AutoSize = true,
                Margin = new Padding(0, L(10), 0, 0)
            });
            return col;
        }
    }
}
