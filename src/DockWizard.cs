// DockWizard.cs - "teach the app your dock" dialog. The user presses the
// dock's switch button (away, then back) while we record USB arrivals and
// removals; devices that BOTH departed and returned are the dock's switched
// segment and become the trigger signatures. Works for any KVM-style dock.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorSwitch
{
    class DockWizard : Form
    {
        float scale;
        Palette P { get { return Theme.Current; } }
        int L(float v) { return (int)Math.Round(v * scale); }

        readonly HashSet<string> departed = new HashSet<string>();
        readonly HashSet<string> arrived = new HashSet<string>();
        List<string> proposal = new List<string>();
        bool capturing;
        int secondsLeft;

        Label status;
        TextBox log;
        FlatButton btnStart, btnSave;
        Timer countdown;
        Action<bool, string> tap;

        public DockWizard()
        {
            scale = DeviceDpi / 96f;
            Text = "Set up dock button - Monitor Input Switcher";
            Icon = TrayApp.AppIcon(32);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(L(440), L(400));
            BackColor = P.Bg;
            ForeColor = P.Text;
            Font = Theme.Body;
            HandleCreated += delegate { Theme.ApplyTitleBar(this); };

            var intro = new Label
            {
                Text = "1. Click Start.\r\n" +
                       "2. Press your dock's switch button to move the keyboard and mouse to " +
                       "the other computer.\r\n" +
                       "3. Wait about five seconds.\r\n" +
                       "4. Press it again to come back here.\r\n" +
                       "5. Save what was learned.",
                Font = Theme.Body, ForeColor = P.Text, AutoSize = false
            };
            intro.SetBounds(L(16), L(12), L(408), L(96));
            Controls.Add(intro);

            btnStart = MakeButton("Start", true, L(16), L(114));
            btnStart.Click += delegate { if (!capturing) StartCapture(); };
            Controls.Add(btnStart);

            status = new Label
            {
                Text = "Not started.", Font = Theme.Small, ForeColor = P.Muted,
                AutoSize = false, TextAlign = ContentAlignment.MiddleLeft
            };
            status.SetBounds(L(140), L(117), L(284), L(24));
            Controls.Add(status);

            log = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                BackColor = P.Field, ForeColor = P.Muted, BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.5f)
            };
            log.SetBounds(L(16), L(150), L(408), L(190));
            Controls.Add(log);

            btnSave = MakeButton("Save", true, L(16), L(352));
            btnSave.Enabled = false;
            btnSave.Click += delegate { SaveAndClose(); };
            Controls.Add(btnSave);

            var btnCancel = MakeButton("Cancel", false, L(140), L(352));
            btnCancel.Click += delegate { Close(); };
            Controls.Add(btnCancel);

            countdown = new Timer { Interval = 1000 };
            countdown.Tick += delegate { TickCapture(); };

            tap = OnRaw;
            FormClosed += delegate
            {
                DockWatch.RawEvent -= tap;
                countdown.Dispose();
            };

            DockWatch.Start();      // make sure the notification window exists
        }

        FlatButton MakeButton(string text, bool primary, int x, int y)
        {
            var b = new FlatButton
            {
                Text = text, Primary = primary, Font = Theme.Body,
                Fill = P.Card, Border = P.Border, TextColor = P.Text, HoverFill = P.Hover,
                AccentFill = P.Accent, AccentText = P.AccentText
            };
            b.SetBounds(x, y, L(110), L(30));
            return b;
        }

        void StartCapture()
        {
            departed.Clear(); arrived.Clear(); proposal.Clear();
            btnSave.Enabled = false;
            log.Clear();
            capturing = true;
            secondsLeft = 75;
            DockWatch.RawEvent += tap;
            status.Text = "Listening... press the dock button now.";
            countdown.Start();
        }

        void OnRaw(bool cameBack, string vidPid)
        {
            if (!capturing) return;
            if (cameBack) arrived.Add(vidPid); else departed.Add(vidPid);
            log.AppendText((cameBack ? "returned  " : "left      ") + vidPid + "\r\n");
        }

        void TickCapture()
        {
            secondsLeft--;
            // Finish early once we have seen a full round trip and things
            // have likely settled, or when time runs out.
            bool roundTrip = departed.Count > 0 && arrived.Count > 0;
            if (secondsLeft <= 0 || (roundTrip && secondsLeft <= 60))
            {
                FinishCapture();
                return;
            }
            status.Text = "Listening... " + secondsLeft + "s left. Press the button, wait, press again.";
        }

        void FinishCapture()
        {
            countdown.Stop();
            capturing = false;
            DockWatch.RawEvent -= tap;

            proposal = new List<string>();
            foreach (string sig in departed)
                if (arrived.Contains(sig)) proposal.Add(sig);
            proposal.Sort();

            if (proposal.Count == 0)
            {
                status.Text = "Nothing made a round trip. Try Start again.";
                return;
            }
            var model = DockLibrary.Match(proposal);
            status.Text = (model != null ? "Recognised: " + model.Name : "Learned " + proposal.Count + " device(s)") + " - click Save.";
            log.AppendText("\r\nWill watch: " + string.Join(", ", proposal) + "\r\n");
            btnSave.Enabled = true;
        }

        void SaveAndClose()
        {
            var d = ConfigStore.Dock;
            d.Signatures = new List<string>(proposal);
            d.Enabled = true;

            // Sensible defaults: coming back means "the profile the monitors
            // are on right now" (the user is at this PC while setting up);
            // leaving means the other one. Both remain editable in Settings.
            var live = Ddc.ReadInputs();
            int onA = Ddc.CountOnProfile(TrayApp.ProfileA, live);
            int onB = Ddc.CountOnProfile(TrayApp.ProfileB, live);
            string here = onA >= onB ? "A" : "B";
            string away = here == "A" ? "B" : "A";
            if (d.OnArrived == null) d.OnArrived = here;
            if (d.OnDeparted == null) d.OnDeparted = away;

            TrayApp.SaveConfig();
            Close();
        }
    }
}
