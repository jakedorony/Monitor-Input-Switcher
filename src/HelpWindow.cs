// HelpWindow.cs - built-in end-user guide, shown from the menu and on first run.

using System.Drawing;
using System.Windows.Forms;

namespace MonitorSwitch
{
    static class HelpWindow
    {
        public static void ShowHelp()
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
"OPTIONAL: SYNC ACROSS COMPUTERS\r\n" +
"-----------------------------------------------------------\r\n" +
"Open Settings (the gear in the main window) and look at\r\n" +
"\"Account & sync\".\r\n" +
"Create an account once (email + password, then click the\r\n" +
"confirmation link we email you). Sign in with the same account\r\n" +
"on each computer plugged into these monitors.\r\n" +
"\r\n" +
"Your two profiles are SHARED: save them once on any computer\r\n" +
"and every signed-in computer gets them. That's the point -\r\n" +
"each computer can send the monitors to the other one, and the\r\n" +
"app remembers which monitor gets which input even if your\r\n" +
"computers list the monitors in a different order. Everything\r\n" +
"still works fine without an account - syncing is optional.\r\n" +
"\r\n" +
"-----------------------------------------------------------\r\n" +
"IF A MONITOR GOES TO THE WRONG INPUT\r\n" +
"-----------------------------------------------------------\r\n" +
"Some monitors give each computer a slightly different name\r\n" +
"for themselves. When that happens the app works out which\r\n" +
"screens are the same one and remembers it, so your profiles\r\n" +
"keep working on every computer.\r\n" +
"\r\n" +
"Rarely it can get that wrong - usually after you move cables\r\n" +
"between ports, or plug in a different monitor. If a profile\r\n" +
"starts sending a monitor to the wrong input, open the main\r\n" +
"window, open Settings (the gear), find \"Monitor matching\"\r\n" +
"and click \"Clear learned\r\n" +
"matches\". The app forgets what it worked out and starts\r\n" +
"again next time you switch. Your profiles are not affected.\r\n" +
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
"computer, tick \"Start automatically when I sign in\" in the\r\n" +
"main window.\r\n";

            var form = new Form();
            form.Text = "How to use - Monitor Input Switcher";
            form.StartPosition = FormStartPosition.CenterScreen;
            form.ClientSize = new Size(520, 460);
            form.MinimumSize = new Size(420, 320);
            form.TopMost = true;

            var panel = new Panel();
            var box = new TextBox();
            box.Multiline = true;
            box.ReadOnly = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.WordWrap = true;
            box.Dock = DockStyle.Fill;
            box.Font = new Font("Consolas", 9.5f);
            box.BackColor = Theme.Current.Card;
            box.ForeColor = Theme.Current.Text;
            box.BorderStyle = BorderStyle.None;
            form.BackColor = Theme.Current.Bg;
            panel.BackColor = Theme.Current.Bg;
            form.HandleCreated += delegate { Theme.ApplyTitleBar(form); };
            box.Text = guide;
            box.Select(0, 0);

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
    }
}
