// Program.cs - entry point and single-instance guard.

using System;
using System.Threading;
using System.Windows.Forms;

namespace MonitorSwitch
{
    static class Program
    {
        // Single-instance guard. The SAME name is set as AppMutex in
        // MonitorSwitch.iss so the installer/uninstaller can detect a running
        // copy. Plain (session-local) name - do not add a "Global\" prefix.
        const string MutexName = "MonitorInputSwitcher_SingleInstance_7E04BDB0";

        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.SetCompatibleTextRenderingDefault(false);

            // Async continuations (sync client) must land back on this UI
            // thread even before the first window exists.
            SynchronizationContext.SetSynchronizationContext(
                new WindowsFormsSynchronizationContext());

            // Only one instance may run. If the mutex already exists, another
            // copy is live - point the user at it and exit quietly.
            bool createdNew;
            var singleInstance = new Mutex(true, MutexName, out createdNew);
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
                TrayApp.Run();
            }
            finally
            {
                singleInstance.ReleaseMutex();
                singleInstance.Dispose();
            }
        }
    }
}
