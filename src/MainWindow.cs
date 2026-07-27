// MainWindow.cs - the modeless main window (singleton). Live monitor
// status, the two profile cards, sync account section, and bottom controls.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorSwitch
{
    static class MainWindow
    {
        static Form mainWindow;

        public static void ShowWindow()
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
            form.ClientSize = new Size(484, 600);
            form.Icon = TrayApp.Tray.Icon;

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
                            (inputs[i] < 0 ? "couldn't read" : TrayApp.FriendlyInput(inputs[i])));
                    statusLabel.Text = string.Join("\r\n", lines);
                }
                // profile cards
                groups[0].Text = "Profile A";
                groups[1].Text = "Profile B";
                nameLabels[0].Text = TrayApp.ProfileA.Name;
                nameLabels[1].Text = TrayApp.ProfileB.Name;
                valueLabels[0].Text = TrayApp.DescribeProfile(TrayApp.ProfileA);
                valueLabels[1].Text = TrayApp.DescribeProfile(TrayApp.ProfileB);
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
                    TrayApp.Apply(isA ? TrayApp.ProfileA : TrayApp.ProfileB);
                    refreshUi();
                };
                groups[g].Controls.Add(btnSwitch);

                var btnSave = new Button();
                btnSave.Text = "Save current setup";
                btnSave.SetBounds(120, 82, 140, 28);
                btnSave.Click += delegate
                {
                    TrayApp.CaptureToProfile(slot);
                    refreshUi();
                };
                groups[g].Controls.Add(btnSave);

                var btnDelete = new Button();
                btnDelete.Text = "Delete";
                btnDelete.SetBounds(268, 82, 80, 28);
                btnDelete.Click += delegate
                {
                    TrayApp.DeleteProfile(slot);
                    refreshUi();
                };
                groups[g].Controls.Add(btnDelete);
            }

            // --- Sync across computers ---
            var gbSync = new GroupBox();
            gbSync.Text = "Sync across computers";
            gbSync.SetBounds(12, 384, 460, 124);

            var lblSyncInfo = new Label();
            lblSyncInfo.AutoSize = false;
            lblSyncInfo.SetBounds(12, 20, 434, 32);
            gbSync.Controls.Add(lblSyncInfo);

            var txtEmail = new TextBox();
            txtEmail.PlaceholderText = "Email";
            txtEmail.SetBounds(12, 56, 190, 24);
            gbSync.Controls.Add(txtEmail);

            var txtPassword = new TextBox();
            txtPassword.PlaceholderText = "Password";
            txtPassword.UseSystemPasswordChar = true;
            txtPassword.SetBounds(208, 56, 150, 24);
            gbSync.Controls.Add(txtPassword);

            var btnSignIn = new Button();
            btnSignIn.Text = "Sign in";
            btnSignIn.SetBounds(12, 88, 90, 26);
            gbSync.Controls.Add(btnSignIn);

            var btnSignUp = new Button();
            btnSignUp.Text = "Create account";
            btnSignUp.SetBounds(108, 88, 110, 26);
            gbSync.Controls.Add(btnSignUp);

            var btnSyncNow = new Button();
            btnSyncNow.Text = "Sync now";
            btnSyncNow.SetBounds(12, 88, 90, 26);
            gbSync.Controls.Add(btnSyncNow);

            var btnSignOut = new Button();
            btnSignOut.Text = "Sign out";
            btnSignOut.SetBounds(108, 88, 90, 26);
            gbSync.Controls.Add(btnSignOut);

            var lblSyncStatus = new Label();
            lblSyncStatus.AutoSize = false;
            lblSyncStatus.SetBounds(226, 88, 220, 28);
            gbSync.Controls.Add(lblSyncStatus);

            Action updateSyncUi = delegate
            {
                bool signedIn = SyncClient.IsSignedIn;

                txtEmail.Visible = !signedIn;
                txtPassword.Visible = !signedIn;
                btnSignIn.Visible = !signedIn;
                btnSignUp.Visible = !signedIn;
                btnSyncNow.Visible = signedIn;
                btnSignOut.Visible = signedIn;

                if (signedIn)
                {
                    lblSyncInfo.Text = "Signed in as " + SyncClient.Email +
                        ". Profiles on this PC (" + TrayApp.MachineName +
                        ") back up automatically.";
                    lblSyncStatus.Text = TrayApp.LastSyncUtc == DateTime.MinValue
                        ? "Not synced yet."
                        : "Last synced: " + TrayApp.LastSyncUtc.ToLocalTime().ToString("g");
                }
                else
                {
                    lblSyncInfo.Text = "Sign in to back up this PC's profiles and " +
                        "sync them across your computers.";
                }
            };

            btnSignIn.Click += async delegate
            {
                btnSignIn.Enabled = false;
                btnSignUp.Enabled = false;
                lblSyncStatus.Text = "Signing in...";
                try
                {
                    await SyncClient.SignInAsync(txtEmail.Text.Trim(), txtPassword.Text);
                    txtPassword.Text = "";
                    lblSyncStatus.Text = "Syncing...";
                    await TrayApp.SyncNowAsync();
                }
                catch (SyncException ex)
                {
                    lblSyncStatus.Text = ex.Message;
                }
                btnSignIn.Enabled = true;
                btnSignUp.Enabled = true;
                updateSyncUi();
                refreshUi();
            };

            btnSignUp.Click += async delegate
            {
                btnSignIn.Enabled = false;
                btnSignUp.Enabled = false;
                lblSyncStatus.Text = "Creating account...";
                try
                {
                    bool sessionNow = await SyncClient.SignUpAsync(
                        txtEmail.Text.Trim(), txtPassword.Text);
                    if (sessionNow)
                    {
                        txtPassword.Text = "";
                        lblSyncStatus.Text = "Syncing...";
                        await TrayApp.SyncNowAsync();
                    }
                    else
                    {
                        lblSyncStatus.Text = "Almost done - click the confirmation " +
                            "link we emailed you, then press Sign in.";
                    }
                }
                catch (SyncException ex)
                {
                    lblSyncStatus.Text = ex.Message;
                }
                btnSignIn.Enabled = true;
                btnSignUp.Enabled = true;
                updateSyncUi();
                refreshUi();
            };

            btnSyncNow.Click += async delegate
            {
                btnSyncNow.Enabled = false;
                lblSyncStatus.Text = "Syncing...";
                try
                {
                    await TrayApp.SyncNowAsync();
                }
                catch (SyncException ex)
                {
                    lblSyncStatus.Text = ex.Message;
                }
                btnSyncNow.Enabled = true;
                updateSyncUi();
                refreshUi();
            };

            btnSignOut.Click += delegate
            {
                SyncClient.SignOut();
                lblSyncStatus.Text = "";
                updateSyncUi();
            };

            // Repaint the sync section whenever background sync activity
            // changes state (e.g. the silent sign-in at startup finishes).
            Action syncChangedHandler = delegate
            {
                updateSyncUi();
                refreshUi();
            };
            TrayApp.SyncStateChanged += syncChangedHandler;
            form.FormClosed += delegate { TrayApp.SyncStateChanged -= syncChangedHandler; };

            // --- Bottom row: toggle, startup, help, close ---
            var btnToggle = new Button();
            btnToggle.Text = "Switch (toggle A / B)";
            btnToggle.SetBounds(12, 520, 168, 32);
            btnToggle.Click += delegate
            {
                TrayApp.ToggleProfiles();
                refreshUi();
            };

            var chkStartup = new CheckBox();
            chkStartup.Text = "Start automatically when I sign in";
            chkStartup.SetBounds(14, 560, 260, 24);
            chkStartup.Checked = TrayApp.GetStartupEnabled();
            bool startupGuard = false;
            chkStartup.CheckedChanged += delegate
            {
                if (startupGuard) return;
                if (!TrayApp.SetStartupEnabled(chkStartup.Checked))
                {
                    MessageBox.Show(
                        "Couldn't update the startup setting.",
                        "Monitor Switch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    startupGuard = true;
                    chkStartup.Checked = TrayApp.GetStartupEnabled();   // revert silently
                    startupGuard = false;
                }
            };

            var btnHelp = new Button();
            btnHelp.Text = "How to use...";
            btnHelp.SetBounds(280, 520, 100, 32);
            btnHelp.Click += delegate { HelpWindow.ShowHelp(); };

            var btnClose = new Button();
            btnClose.Text = "Close";
            btnClose.SetBounds(388, 520, 84, 32);
            btnClose.Click += delegate { form.Close(); };

            form.Controls.Add(gbStatus);
            form.Controls.Add(groups[0]);
            form.Controls.Add(groups[1]);
            form.Controls.Add(gbSync);
            form.Controls.Add(btnToggle);
            form.Controls.Add(chkStartup);
            form.Controls.Add(btnHelp);
            form.Controls.Add(btnClose);
            form.CancelButton = btnClose;

            refreshUi();
            updateSyncUi();

            mainWindow = form;
            form.Show();      // modeless - tray keeps working alongside
            form.Activate();
        }
    }
}
