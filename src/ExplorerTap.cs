// DIIISPL — a monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.
//
// This module works with the helper library DIIISPL.ExplorerTAP.dll, based
// on the ExplorerTAP component of the TranslucentTB project (https://github.com/TranslucentTB/TranslucentTB),
// © Charles Milette and contributors, GPLv3. The library publishes a COM taskbar
// appearance service inside Explorer, and here there are only calls to it.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DIIISPL
{
    /// <summary>Taskbar look as the appearance service understands it.</summary>
    internal enum TaskbarLook
    {
        Default,      // restore the system look
        Opaque,       // solid fill
        Clear,        // transparent
        Blur,         // blur
        Acrylic       // acrylic
    }

    /// <summary>
    /// Access to the taskbar appearance service.
    ///
    /// The Windows 11 taskbar cannot be reached from outside: its XAML tree draws the background, and the accent
    /// policy does not reach it. So the appearance is set by a library running inside
    /// Explorer; it hands a COM interface to us here, and we call its methods.
    /// </summary>
    internal static class ExplorerTap
    {
        private const string TapDll = "DIIISPL.ExplorerTAP.dll";
        private static readonly Guid IID_TaskbarAppearanceService = new Guid("d255d70a-b6f7-4b66-9fc6-b290a598a1d3");

        [DllImport(TapDll, CharSet = CharSet.Unicode)]
        private static extern int InjectExplorerTAP(IntPtr window, ref Guid riid, out IntPtr service);

        [ComImport, Guid("d255d70a-b6f7-4b66-9fc6-b290a598a1d3"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarAppearanceService
        {
            void SetTaskbarAppearance(IntPtr taskbar, int brush, uint color);
            void SetTaskbarBlur(IntPtr taskbar, uint color, float blurAmount);
            void ReturnTaskbarToDefaultAppearance(IntPtr taskbar);
            void SetTaskbarBorderVisibility(IntPtr taskbar, int visible);
            void RestoreAllTaskbarsToDefault();
            void RestoreAllTaskbarsToDefaultWhenProcessDies(uint pid);
            void KillExplorerWhenPackageUninstalls([MarshalAs(UnmanagedType.LPWStr)] string packageFullName);
        }

        private const int BrushAcrylic = 0;
        private const int BrushSolidColor = 1;

        /// <summary>Whether to restore the taskbar to the system look on exit — only for the long-lived tray.</summary>
        public static bool RestoreWhenProcessExits;

        private static ITaskbarAppearanceService _service;
        private static string _lastError;

        public static string LastError { get { return _lastError; } }

        public static bool Available
        {
            get
            {
                try { return System.IO.File.Exists(System.IO.Path.Combine(AppFolder(), TapDll)); }
                catch { return false; }
            }
        }

        private static string AppFolder()
        {
            return System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        /// <summary>
        /// Get the service, raising it inside Explorer if necessary. The service lives along with
        /// Explorer: it restarted — the link is lost, so we must connect again.
        /// </summary>
        private static ITaskbarAppearanceService Service()
        {
            if (_service != null) return _service;

            IntPtr taskbar = PrimaryTaskbar();
            if (taskbar == IntPtr.Zero) { _lastError = Text.TbNoTaskbar; return null; }

            try
            {
                // Right after Explorer restarts the first attempt often does not wait for the
                // library inside it to be ready, so we try several times.
                Guid iid = IID_TaskbarAppearanceService;
                IntPtr raw = IntPtr.Zero;
                int hr = 0;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    hr = InjectExplorerTAP(taskbar, ref iid, out raw);
                    if (hr == 0 && raw != IntPtr.Zero) break;
                    if (attempt < 3) System.Threading.Thread.Sleep(1500);
                }
                if (hr != 0 || raw == IntPtr.Zero)
                {
                    _lastError = Text.TbNoExplorer;
                    return null;
                }

                _service = (ITaskbarAppearanceService)Marshal.GetObjectForIUnknown(raw);
                Marshal.Release(raw);

                // Ask to restore the taskbar to the system look when DIIISPL closes — but only if
                // the program is long-lived. For a one-shot command this is suicidal:
                // the process ends at once, and the service immediately rolls back everything we set.
                if (RestoreWhenProcessExits)
                {
                    try { _service.RestoreAllTaskbarsToDefaultWhenProcessDies((uint)System.Diagnostics.Process.GetCurrentProcess().Id); }
                    catch { }
                }

                _lastError = null;
                return _service;
            }
            catch (Exception)
            {
                _lastError = Text.TbLostExplorer;
                _service = null;
                return null;
            }
        }

        /// <summary>The link with Explorer is lost — we will connect again on the next call.</summary>
        public static void Forget()
        {
            _service = null;
        }

        private static IntPtr PrimaryTaskbar()
        {
            var bars = FindTaskbars();
            return bars.Count > 0 ? bars[0] : IntPtr.Zero;
        }

        /// <summary>
        /// The primary screen's taskbar and the taskbars on the other monitors.
        ///
        /// Windows are looked up by class directly, not by enumerating all windows: while "Start",
        /// search or Task View is open, shell windows are quickly created and destroyed,
        /// EnumWindows breaks off midway and the taskbar does not end up in the list at all —
        /// and these are exactly the states the whole thing was started for.
        /// </summary>
        public static List<IntPtr> FindTaskbars()
        {
            var found = new List<IntPtr>();

            IntPtr primary = Native.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
            if (primary != IntPtr.Zero) found.Add(primary);                     // the primary one — first

            IntPtr next = IntPtr.Zero;
            while ((next = Native.FindWindowEx(IntPtr.Zero, next, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
                found.Add(next);

            return found;
        }

        /// <summary>
        /// The service takes a colour as ABGR, but it is more convenient to store settings in the usual
        /// AARRGGBB — so we swap red with blue.
        /// </summary>
        private static uint ToAbgr(uint argb)
        {
            return (argb & 0xFF00FF00) | ((argb & 0x00FF0000) >> 16) | ((argb & 0x000000FF) << 16);
        }

        /// <summary>
        /// Parse a colour of the form AARRGGBB ("#" and "0x" are allowed in front, as well as the short
        /// form RRGGBB — then the colour is opaque). A separate parse is needed because
        /// zeros at the edges must not be trimmed: "00000000" is a perfectly legal transparent colour.
        /// </summary>
        public static bool TryParseColor(string text, out uint color)
        {
            color = 0;
            if (text == null) return false;
            string s = text.Trim();
            if (s.StartsWith("#", StringComparison.Ordinal)) s = s.Substring(1);
            else if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            if (s.Length != 6 && s.Length != 8) return false;
            if (!uint.TryParse(s, System.Globalization.NumberStyles.HexNumber,
                               System.Globalization.CultureInfo.InvariantCulture, out color)) return false;
            if (s.Length == 6) color |= 0xFF000000;
            return true;
        }

        /// <summary>Set the look of one taskbar.</summary>
        public static bool ApplyTo(IntPtr bar, TaskbarLook look, uint colorArgb, float blurRadius, bool showLine)
        {
            var service = Service();
            if (service == null) return false;

            try
            {
                uint color = ToAbgr(colorArgb);
                switch (look)
                {
                    case TaskbarLook.Opaque:
                        service.SetTaskbarAppearance(bar, BrushSolidColor, color | 0xFF000000);
                        break;
                    case TaskbarLook.Clear:
                        service.SetTaskbarAppearance(bar, BrushSolidColor, color & 0x00FFFFFF);
                        break;
                    case TaskbarLook.Acrylic:
                        service.SetTaskbarAppearance(bar, BrushAcrylic, color);
                        break;
                    case TaskbarLook.Blur:
                        service.SetTaskbarBlur(bar, color, blurRadius);
                        break;
                    default:
                        service.ReturnTaskbarToDefaultAppearance(bar);
                        break;
                }
                service.SetTaskbarBorderVisibility(bar, showLine ? 1 : 0);
                _lastError = null;
                return true;
            }
            catch (Exception)
            {
                _lastError = Text.TbLostExplorer;
                _service = null;         // Explorer may have restarted
                return false;
            }
        }

        /// <summary>Restore all taskbars to the system look.</summary>
        public static void RestoreAll()
        {
            var service = Service();
            if (service == null) return;
            try { service.RestoreAllTaskbarsToDefault(); }
            catch { _service = null; }
        }

        /// <summary>
        /// Set the look of all taskbars at once. Deliberately done through ApplyTo rather than with its own
        /// pass over the service: otherwise details easily drift apart — before, the conversion of the
        /// colour to ABGR was lost here, and the same setting from the window and from the command line gave a different colour.
        /// </summary>
        public static bool ApplyAll(TaskbarLook look, uint colorArgb, float blurRadius, bool showLine)
        {
            var bars = FindTaskbars();
            if (bars.Count == 0) { _lastError = Text.TbNoTaskbar; return false; }

            bool all = true;
            foreach (IntPtr bar in bars)
                if (!ApplyTo(bar, look, colorArgb, blurRadius, showLine)) all = false;
            return all;
        }

        /// <summary>Restore the system look to the taskbars.</summary>
        public static void Restore()
        {
            if (_service == null) return;
            try { _service.RestoreAllTaskbarsToDefault(); }
            catch { _service = null; }
        }

        // ---- translation of names from the profile ----
        public static readonly string[] Names = { "default", "opaque", "clear", "blur", "acrylic" };

        public static string Describe(string name)
        {
            switch (name)
            {
                case "default": return Text.TbLook("default");
                case "opaque": return Text.TbLook("opaque");
                case "clear": return Text.TbLook("clear");
                case "blur": return Text.TbLook("blur");
                case "acrylic": return Text.TbLook("acrylic");
                default: return Text.TbLook("keep");
            }
        }

        public static TaskbarLook Parse(string name)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "opaque": return TaskbarLook.Opaque;
                case "clear": return TaskbarLook.Clear;
                case "blur": return TaskbarLook.Blur;
                case "acrylic": return TaskbarLook.Acrylic;
                default: return TaskbarLook.Default;
            }
        }
    }
}
