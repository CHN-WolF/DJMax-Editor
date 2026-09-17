using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace DJMaxEditor
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Global safety net: unexpected exceptions are logged locally and shown to the user instead
            // of silently terminating the process.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => HandleGlobal("ui-thread", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => HandleGlobal("app-domain", e.ExceptionObject as Exception);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string[] files = ExtractFileArgs(args);

            // One process for every chart format: a second launch forwards its files to the
            // running instance (which opens them as tabs) and exits. If the current holder is
            // unreachable it is usually a process that is dying right now (its mutex handle is
            // about to close), so re-run the acquisition instead of blindly starting a second
            // editor; only start a parallel instance when every attempt fails.
            Mutex instanceMutex = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                bool isFirstInstance = true;
                Mutex candidate = null;
                try
                {
                    candidate = SingleInstance.TryAcquireFirst(out isFirstInstance);
                }
                catch (Exception ex)
                {
                    DJMaxEditor.Diagnostics.DiagnosticLog.Exception("singleinstance.mutex", ex);
                }

                if (isFirstInstance)
                {
                    instanceMutex = candidate;
                    break;
                }

                using (candidate)
                {
                    if (SingleInstance.TrySendToFirstInstance(files, 10000))
                    {
                        return;
                    }
                    DJMaxEditor.Diagnostics.DiagnosticLog.Write("singleinstance.forward",
                        "could not reach the first instance (attempt " + (attempt + 1) + ")");
                    Thread.Sleep(500);
                }
            }

            // Register the chart formats with this exe (per-user, no admin) so double-clicking
            // any of them reaches this process and opens as a tab.
            FileAssociations.EnsureRegistered();

            // Ask Windows for 1 ms timer resolution so the playback timers tick
            // steadily instead of quantizing to the ~15.6 ms default resolution.
            TimeResolution.Begin(1);
            using (instanceMutex)
            {
                Application.Run(new MainForm(files));
            }
            TimeResolution.End(1);
        }

        private static string[] ExtractFileArgs(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return new string[0];
            }

            var files = new List<string>();
            foreach (var arg in args)
            {
                if (String.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }
                try
                {
                    if (File.Exists(arg))
                    {
                        files.Add(Path.GetFullPath(arg));
                    }
                }
                catch (Exception)
                {
                    // Not a usable file path; ignore.
                }
            }
            return files.ToArray();
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
