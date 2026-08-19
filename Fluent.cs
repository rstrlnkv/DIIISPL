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

        /// <summary>Иконка-триптих: три панели, средняя крупнее.</summary>
        public static Gdi.Icon MakeTrayIcon()
        {
            using (var bmp = new Gdi.Bitmap(32, 32))
            {
                using (Gdi.Graphics g = Gdi.Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = Gdi.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Gdi.Color.Transparent);
                    Color a = Theme.Accent();
                    using (var fill = new Gdi.SolidBrush(Gdi.Color.FromArgb(255, a.R, a.G, a.B)))
                    using (var side = new Gdi.SolidBrush(Gdi.Color.FromArgb(160, 255, 255, 255)))
                    {
                        g.FillRectangle(side, 2, 9, 7, 14);
                        g.FillRectangle(fill, 11, 5, 10, 22);
                        g.FillRectangle(side, 23, 9, 7, 14);
                    }
                }
                IntPtr h = bmp.GetHicon();
                try
                {
                    using (Gdi.Icon tmp = Gdi.Icon.FromHandle(h)) return (Gdi.Icon)tmp.Clone();
                }
                finally { Native.DestroyIcon(h); }
            }
        }

        public static ImageSource MakeWindowIcon()
        {
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                var accent = new SolidColorBrush(Theme.Accent());
                var side = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255));
                dc.DrawRoundedRectangle(side, null, new Rect(2, 9, 7, 14), 1.5, 1.5);
                dc.DrawRoundedRectangle(accent, null, new Rect(11, 5, 10, 22), 2, 2);
                dc.DrawRoundedRectangle(side, null, new Rect(23, 9, 7, 14), 1.5, 1.5);
            }
            var bmp = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }
    }
}
