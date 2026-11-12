// DIIISPL — a monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gdi = System.Drawing;

namespace DIIISPL
{
    /// <summary>Small touches that make the window look like a native Windows 11 window.</summary>
    internal static class Fluent
    {
        public static void ApplyWindowStyling(Window window)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                int dark = Theme.IsDark ? 1 : 0;
                Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

                // Request Mica but do not make the client area transparent:
                // otherwise live windows show through unpainted spots instead of the backdrop.
                int backdrop = Native.DWMSBT_MAINWINDOW;
                Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }
            catch { /* without Mica the window just stays a solid colour */ }
        }

        /// <summary>
        /// Tray icon.
        ///
        /// Taken from the program's own icon rather than redrawn: it holds a frame for each
        /// size, tuned to the pixel grid, so unlike an icon drawn on the fly it does not blur
        /// at 16 px or at 32 px on a large display scale. A second copy of the icon inside
        /// the program is not needed: the Windows resource that Explorer reads works here
        /// too.
        /// </summary>
        public static Gdi.Icon MakeTrayIcon()
        {
            Gdi.Size wanted = System.Windows.Forms.SystemInformation.SmallIconSize;   // at 200% scale this is 32

            // 32512 is the number under which the C# compiler stores the program icon.
            IntPtr handle = Native.LoadImage(Native.GetModuleHandle(null), "#32512",
                                             Native.IMAGE_ICON, wanted.Width, wanted.Height, 0);
            if (handle == IntPtr.Zero) return Gdi.SystemIcons.Application;

            try
            {
                using (Gdi.Icon tmp = Gdi.Icon.FromHandle(handle)) return (Gdi.Icon)tmp.Clone();
            }
            finally { Native.DestroyIcon(handle); }
        }
    }
}
