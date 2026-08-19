// DIIIX — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gdi = System.Drawing;

namespace DIIIX
{
    /// <summary>Мелочи, которые делают окно похожим на родное окно Windows 11.</summary>
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

                // Mica запрашиваем, но клиентскую область прозрачной не делаем:
                // иначе сквозь незакрашенные места видно живые окна, а не подложку.
                int backdrop = Native.DWMSBT_MAINWINDOW;
                Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }
            catch { /* без Mica окно просто останется однотонным */ }
        }

        /// <summary>
        /// Значок для трея.
        ///
        /// Берётся из значка самой программы, а не рисуется заново: в нём под каждый
        /// размер лежит свой кадр, выверенный по сетке точек, — от нарисованного на лету
        /// значок отличается тем, что не мылится ни в 16 точек, ни в 32 на крупном
        /// масштабе экрана. Вторая копия значка внутри программы для этого не нужна:
        /// ресурс Windows, который читает проводник, годится и здесь.
        /// </summary>
        public static Gdi.Icon MakeTrayIcon()
        {
            Gdi.Size wanted = System.Windows.Forms.SystemInformation.SmallIconSize;   // на 200% масштабе это 32

            // 32512 — номер, под которым компилятор C# кладёт значок программы.
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
