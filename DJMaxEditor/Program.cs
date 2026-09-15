using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace DJMaxEditor
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Global safety net: unexpected exceptions are logged locally and shown to the user instead
            // of silently terminating the process.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => HandleGlobal("ui-thread", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => HandleGlobal("app-domain", e.ExceptionObject as Exception);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Ask Windows for 1 ms timer resolution so the playback timers tick
            // steadily instead of quantizing to the ~15.6 ms default resolution.
            TimeResolution.Begin(1);
            Application.Run(new MainForm());
            TimeResolution.End(1);
        }

        private static class TimeResolution
        {
            [System.Runtime.InteropServices.DllImport("winmm.dll")]
            private static extern uint timeBeginPeriod(uint uPeriod);

            [System.Runtime.InteropServices.DllImport("winmm.dll")]
            private static extern uint timeEndPeriod(uint uPeriod);

            public static void Begin(uint period)
            {
                timeBeginPeriod(period);
            }

            public static void End(uint period)
            {
                timeEndPeriod(period);
            }
        }

        private static void HandleGlobal(string source, Exception ex)
        {
            DJMaxEditor.Diagnostics.DiagnosticLog.Exception("unhandled." + source, ex);
            try
            {
                MessageBox.Show(
                    "An unexpected error occurred and was logged locally.\n\n" +
                    (ex?.Message ?? "(no details)") +
                    "\n\nLog: " + DJMaxEditor.Diagnostics.DiagnosticLog.LogPath,
                    "Unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // never rethrow from the global handler
            }
        }
    }
}
