// Prompt.cs - minimal modal text-entry dialog (built in code, no designer files).

using System.Drawing;
using System.Windows.Forms;

namespace MonitorSwitch
{
    static class Prompt
    {
        public static string ForName(string title, string prompt, string defaultText)
        {
            var form = new Form();
            form.Text = title;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.ClientSize = new Size(340, 150);
            form.TopMost = true;
            form.BackColor = Theme.Current.Bg;
            form.ForeColor = Theme.Current.Text;
            form.HandleCreated += delegate { Theme.ApplyTitleBar(form); };

            var label = new Label();
            label.Text = prompt;
            label.SetBounds(12, 12, 316, 70);
            label.AutoSize = false;

            var box = new TextBox();
            box.BackColor = Theme.Current.Field;
            box.ForeColor = Theme.Current.Text;
            box.BorderStyle = BorderStyle.FixedSingle;
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
    }
}
