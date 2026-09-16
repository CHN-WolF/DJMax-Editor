using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace DJMaxEditor
{
    // Per-user file associations, refreshed on every startup so double-clicking any chart the
    // editor can open launches this exe (the single-instance forwarder then opens it as a tab).
    // Everything lives under HKCU\Software\Classes: no admin rights needed and other accounts
    // are unaffected. A user's explicit "Always open with" choice (UserChoice) is hash-protected
    // and still wins over the ProgID redirect written here; those choices are never touched.
    static class FileAssociations
    {
        private const string ProgId = "DJMaxEditor.Chart";
        private const string ProgIdDescription = "DJMax Chart";

        // Every format that has an open handler (Files/*/*OpenFile.cs). .bmson is intentionally
        // absent: the editor can only save it, not open it.
        private static readonly string[] Extensions =
            { ".pt", ".bytes", ".xml", ".bms", ".bme", ".bml", ".pms", ".tech" };

        public static void EnsureRegistered()
        {
            try
            {
                bool changed = false;
                string exe = Assembly.GetExecutingAssembly().Location;
                string command = "\"" + exe + "\" \"%1\"";
                string icon = "\"" + exe + "\",0";

                using (var progid = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId))
                {
                    EnsureValue(progid, null, ProgIdDescription, ref changed);
                    using (var defaultIcon = progid.CreateSubKey("DefaultIcon"))
                    {
                        EnsureValue(defaultIcon, null, icon, ref changed);
                    }
                    using (var openCommand = progid.CreateSubKey(@"shell\open\command"))
                    {
                        EnsureValue(openCommand, null, command, ref changed);
                    }
                }

                // The Applications\<exe> app key: SupportedTypes puts this build on the
                // "Open with" list for every format, and the command is what a UserChoice
                // routed to this exe actually runs.
                using (var app = Registry.CurrentUser.CreateSubKey(
                    @"Software\Classes\Applications\" + Path.GetFileName(exe)))
                {
                    EnsureValue(app, "FriendlyAppName", "DJMax Editor", ref changed);
                    using (var openCommand = app.CreateSubKey(@"shell\open\command"))
                    {
                        EnsureValue(openCommand, null, command, ref changed);
                    }
                    using (var defaultIcon = app.CreateSubKey("DefaultIcon"))
                    {
                        EnsureValue(defaultIcon, null, icon, ref changed);
                    }
                    using (var supported = app.CreateSubKey("SupportedTypes"))
                    {
                        foreach (var ext in Extensions)
                        {
                            if (supported.GetValue(ext) == null)
                            {
                                supported.SetValue(ext, String.Empty, RegistryValueKind.String);
                                changed = true;
                            }
                        }
                    }
                }

                // UserChoices made while the exe was still named DJMaxEditor.exe point at the
                // Applications\DJMaxEditor.exe key, whose command may reference a stale copy of
                // the editor; re-point it at this exe to repair double-click for all of them.
                if (!String.Equals(Path.GetFileName(exe), "DJMaxEditor.exe", StringComparison.OrdinalIgnoreCase))
                {
                    using (var legacy = Registry.CurrentUser.OpenSubKey(
                        @"Software\Classes\Applications\DJMaxEditor.exe", true))
                    {
                        if (legacy != null)
                        {
                            using (var openCommand = legacy.CreateSubKey(@"shell\open\command"))
                            {
                                EnsureValue(openCommand, null, command, ref changed);
                            }
                        }
                    }
                }

                foreach (var ext in Extensions)
                {
                    using (var extKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ext))
                    {
                        EnsureValue(extKey, null, ProgId, ref changed);
                    }
                }

                if (changed)
                {
                    Diagnostics.DiagnosticLog.Write("fileassoc", "associations refreshed for " + exe);
                    SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                // Association registration must never block the editor from starting.
                Diagnostics.DiagnosticLog.Exception("fileassoc", ex);
            }
        }

        private static void EnsureValue(RegistryKey key, string name, string value, ref bool changed)
        {
            object current = key.GetValue(name);
            if (!String.Equals(current as string, value, StringComparison.Ordinal))
            {
                key.SetValue(name, value, RegistryValueKind.String);
                changed = true;
            }
        }

        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}
