// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.
//
// Draws the images for the installer dialogs: the top banner and the welcome side panel.
// Sizes are set by the Windows Installer format: 493×58 and 493×312 pixels.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace DIIISPL.Tools
{
    internal static class MakeArt
    {
        // The same colors as the program icon.
        private static readonly Color MainTop    = Color.FromArgb(0x5C, 0xC8, 0xFF);
        private static readonly Color MainBottom = Color.FromArgb(0x0B, 0x62, 0xCE);
        private static readonly Color SideTop     = Color.FromArgb(0x6F, 0xA8, 0xD8);
        private static readonly Color SideBottom  = Color.FromArgb(0x2E, 0x5F, 0x96);

        private static readonly Color PanelTop    = Color.FromArgb(0x1C, 0x1C, 0x1C);
        private static readonly Color PanelBottom = Color.FromArgb(0x28, 0x2E, 0x38);
        private static readonly Color LightTop    = Color.FromArgb(0xFF, 0xFF, 0xFF);
        private static readonly Color LightBottom = Color.FromArgb(0xEF, 0xF4, 0xFA);

        private static int Main(string[] args)
        {
            string dir = args.Length > 0 ? args[0] : ".";

            using (Bitmap b = Banner()) Save(b, Path.Combine(dir, "banner.bmp"));
            using (Bitmap d = Dialog()) Save(d, Path.Combine(dir, "dialog.bmp"));

            Console.WriteLine("Installer images: " + Path.GetFullPath(dir));
            return 0;
        }

        /// <summary>
        /// Banner at the top of the dialogs. The installer writes the title and description over it on the left,
        /// dark on light, so the background is light and the mark goes to the right.
        /// </summary>
        private static Bitmap Banner()
        {
            var bmp = new Bitmap(493, 58, PixelFormat.Format24bppRgb);
            using (Graphics g = Prepare(bmp))
            {
                Fill(g, new Rectangle(0, 0, 493, 58), LightTop, LightBottom);

                // thin line at the bottom edge, the same one that separates the header in the program itself
                using (var line = new SolidBrush(Color.FromArgb(0xDF, 0xE5, 0xEE)))
                    g.FillRectangle(line, 0, 57, 493, 1);

                // Only the mark here, without the name: the installer stretches the description under the title
                // almost to the right edge, and a caption next to the mark either ran
                // into the text or was cut off by the edge, the more so the longer the name.
                // The name is already in the window title.
                float markH = 28;
                float markW = markH * 1.28f;
                Mark(g, 493 - 18 - markW, (58 - markH) / 2, markH);
            }
            return bmp;
        }

        /// <summary>
        /// Side panel of the welcome dialog and the last dialog. A dark strip with the mark on the left,
        /// a light field on the right: the installer puts its own text there.
        /// </summary>
        private static Bitmap Dialog()
        {
            const int panel = 175;
            var bmp = new Bitmap(493, 312, PixelFormat.Format24bppRgb);
            using (Graphics g = Prepare(bmp))
            {
                // The right half is exactly the same gray the installer paints its dialogs
                // (240,240,240, measured on a live wizard). The "Launch DIIISPL" checkbox
                // on the last dialog draws its own background over the image: on white it
                // left a visible stripe, and on this gray no seam shows. It also
                // keeps the welcome and last dialogs from standing out among the other pages.
                using (var face = new SolidBrush(Color.FromArgb(0xF0, 0xF0, 0xF0)))
                    g.FillRectangle(face, 0, 0, 493, 312);
                Fill(g, new Rectangle(0, 0, panel, 312), PanelTop, PanelBottom);

                // A soft glow behind the mark: without it the dark strip looks like a flat
                // fill. Clipped to the panel, otherwise the spot spills onto the light field
                // and reads as a smudge, not a glow.
                GraphicsState saved = g.Save();
                g.SetClip(new Rectangle(0, 0, panel, 312));
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(-70, 30, 320, 250);
                    using (var glow = new PathGradientBrush(path))
                    {
                        glow.CenterColor = Color.FromArgb(0x2E, 0x3A, 0x4C);
                        glow.SurroundColors = new Color[] { Color.FromArgb(0x1E, 0x22, 0x2A) };
                        glow.CenterPoint = new PointF(88, 140);
                        g.FillPath(glow, path);
                    }
                }
                g.Restore(saved);

                // Each line is centered on its own width, not on the mark's width:
                // otherwise a long name drifts sideways and the block falls apart.
                // Only the mark and the name, with no caption under it. There is one image for
                // all languages: it is drawn before the build and does not enter the difference between the language
                // packages, so any phrase here would stay in
                // one language for everyone. A name has no language, a caption did.
                Mark(g, (panel - 88 * 1.28f) / 2, 92, 88);
                Centered(g, Program, "Segoe UI Semibold", 20f, Color.White, panel, 205);
            }
            return bmp;
        }

        // ------------------------------------------------------------------ details
        /// <summary>The program mark: a wide screen in the middle, two side ones on edge.</summary>
        private static void Mark(Graphics g, float x, float y, float height)
        {
            float sideW = height * 0.26f, sideH = height;
            float mainW = height * 0.62f, mainH = height * 0.50f;
            float gap = height * 0.07f;
            float radius = Math.Max(2f, height * 0.06f);

            Plate(g, new RectangleF(x, y, sideW, sideH), radius, SideTop, SideBottom);
            float cx = x + sideW + gap;
            Plate(g, new RectangleF(cx, y + (sideH - mainH) / 2, mainW, mainH), radius, MainTop, MainBottom);
            Plate(g, new RectangleF(cx + mainW + gap, y, sideW, sideH), radius, SideTop, SideBottom);
        }

        private static void Plate(Graphics g, RectangleF r, float radius, Color top, Color bottom)
        {
            using (var path = Rounded(r, radius))
            using (var brush = new LinearGradientBrush(
                       new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), top, bottom, 55f))
                g.FillPath(brush, path);
        }

        /// <summary>The program name. All captions on the images take it from here.</summary>
        private const string Program = "DIIISPL";

        /// <summary>A line centered in a strip of given width, measured, not by eye.</summary>
        private static void Centered(Graphics g, string text, string face, float size,
                                     Color color, float width, float y)
        {
            using (var font = new Font(face, size, FontStyle.Regular, GraphicsUnit.Point))
            using (var brush = new SolidBrush(color))
            {
                SizeF measured = g.MeasureString(text, font);
                g.DrawString(text, font, brush, (width - measured.Width) / 2, y);
            }
        }

        private static Graphics Prepare(Bitmap bmp)
        {
            Graphics g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            return g;
        }

        private static void Fill(Graphics g, Rectangle r, Color top, Color bottom)
        {
            using (var brush = new LinearGradientBrush(
                       new Rectangle(r.X, r.Y - 1, r.Width, r.Height + 2), top, bottom, 90f))
                g.FillRectangle(brush, r);
        }

        private static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>The installer reads only BMP, and without transparency.</summary>
        private static void Save(Bitmap bmp, string path)
        {
            bmp.Save(path, ImageFormat.Bmp);
        }
    }
}
