// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.
//
// Draws the program icon and packs it into a multi-size DIIISPL.ico.
// The icon is stored in the repository already built, but it is built from here, so that it can be
// tweaked without a graphics editor and without guessing what it was drawn with.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace DIIISPL.Tools
{
    internal static class MakeIcon
    {
        // The mark: a layout of three screens, a wide main one in the middle and two side ones
        // turned on edge. Different proportions matter more than decoration: three identical bars
        // read as an equalizer, while a landscape screen between portrait screens
        // is unmistakable, and that is exactly what the program does.
        //
        // The margins are narrow: the icon must fill its slot, otherwise next to others' icons it
        // looks small. At 16 pixels it comes out 3+1+7+1+3, still distinguishable.
        private const double SideW = 0.185, SideH = 0.720;
        private const double MainW = 0.440, MainH = 0.360;

        // A gap between the screens is mandatory: once they touch, the mark reads as
        // a single figure, the letter "H", not three screens. The middle one looks closer
        // through shadow and color, not through overlap.
        private const double Gap = 0.050;

        private static readonly Color MainTop    = Color.FromArgb(255, 0x5C, 0xC8, 0xFF);
        private static readonly Color MainBottom = Color.FromArgb(255, 0x0B, 0x62, 0xCE);
        private static readonly Color SideTop    = Color.FromArgb(255, 0x6F, 0xA8, 0xD8);
        private static readonly Color SideBottom = Color.FromArgb(255, 0x2E, 0x5F, 0x96);

        private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: MakeIcon <output.ico> [preview.png]  (tools\\make-icon.cmd passes src\\DIIISPL.ico)");
                return 1;
            }
            string outPath = args[0];
            string preview = args.Length > 1 ? args[1] : null;

            var frames = new List<Bitmap>();
            foreach (int size in Sizes) frames.Add(Draw(size));

            using (var file = File.Create(outPath)) Write(file, frames);
            Console.WriteLine("Done: " + Path.GetFullPath(outPath) + "  sizes: " + Sizes.Length);

            if (preview != null)
            {
                using (Bitmap sheet = Preview(frames)) sheet.Save(preview, ImageFormat.Png);
                Console.WriteLine("Preview: " + Path.GetFullPath(preview));
            }

            foreach (Bitmap b in frames) b.Dispose();
            return 0;
        }

        // ------------------------------------------------------------------ drawing
        /// <summary>
        /// Large sizes are drawn with full styling, small ones flat.
        /// Windows does the same: at 16 pixels a soft shadow and highlight turn
        /// into mud, what is needed there is a silhouette, not a material.
        /// </summary>
        private static Bitmap Draw(int size)
        {
            return size >= 32 ? DrawFluent(size) : DrawFlat(size);
        }

        /// <summary>
        /// Fluent-style styling: layers with a soft shadow, a diagonal gradient,
        /// a glowing edge on top and a highlight on the upper half. There is one light for the whole
        /// mark and it falls from the top left, otherwise the layers stop adding up to volume.
        ///
        /// Drawn four times larger and then scaled down: shadows and gradients become
        /// smooth from this, and rounded corners lose their stair steps.
        /// </summary>
        private static Bitmap DrawFluent(int size)
        {
            const int Ss = 4;
            int s = size * Ss;

            var big = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(big))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                RectangleF[] plates = Plates(s);
                float radius = (float)(0.045 * s);

                // Shadow: the same slabs, shifted down and blurred. The side ones cast their own,
                // the middle one casts its own on top of them, which is why it reads as closer.
                using (Bitmap shadow = Shadow(s, plates, radius))
                    g.DrawImageUnscaled(shadow, 0, 0);

                Plate(g, plates[0], radius, SideTop, SideBottom);
                Plate(g, plates[2], radius, SideTop, SideBottom);
                Plate(g, plates[1], radius, MainTop, MainBottom);
            }

            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(big, new Rectangle(0, 0, size, size));
            }
            big.Dispose();
            return bmp;
        }

        /// <summary>The three slabs of the mark in pixels of a canvas of the given size.</summary>
        private static RectangleF[] Plates(float s)
        {
            double total = SideW + Gap + MainW + Gap + SideW;
            double x = (1.0 - total) / 2.0;

            var left  = new RectangleF((float)(x * s), (float)((1 - SideH) / 2 * s),
                                       (float)(SideW * s), (float)(SideH * s));
            x += SideW + Gap;
            var main  = new RectangleF((float)(x * s), (float)((1 - MainH) / 2 * s),
                                       (float)(MainW * s), (float)(MainH * s));
            x += MainW + Gap;
            var right = new RectangleF((float)(x * s), (float)((1 - SideH) / 2 * s),
                                       (float)(SideW * s), (float)(SideH * s));
            return new RectangleF[] { left, main, right };
        }

        /// <summary>One slab: fill, highlight on the upper half and edge light.</summary>
        private static void Plate(Graphics g, RectangleF r, float radius, Color top, Color bottom)
        {
            using (GraphicsPath path = Rounded(r, radius))
            {
                // The gradient is diagonal, not top to bottom: that way the slab looks lit
                // rather than merely painted.
                using (var fill = new LinearGradientBrush(
                           new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2),
                           top, bottom, 55f))
                    g.FillPath(fill, path);

                // Highlight: a light spot on the upper half, fading to nothing.
                var glossArea = new RectangleF(r.X, r.Y, r.Width, r.Height * 0.62f);
                using (var gloss = new LinearGradientBrush(
                           new RectangleF(glossArea.X, glossArea.Y - 1, glossArea.Width, glossArea.Height + 2),
                           Color.FromArgb(42, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                {
                    GraphicsState saved = g.Save();
                    g.SetClip(path);
                    g.FillRectangle(gloss, glossArea);
                    g.Restore(saved);
                }

                // Edge: nearly white at the top, vanishing downward; light falls on the upper edge.
                float pen = Math.Max(1f, r.Width * 0.035f);
                using (var edge = new LinearGradientBrush(
                           new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2),
                           Color.FromArgb(105, 255, 255, 255), Color.FromArgb(8, 255, 255, 255), 90f))
                using (var p = new Pen(edge, pen))
                {
                    p.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;
                    g.DrawPath(p, path);
                }
            }
        }

        /// <summary>Soft shadow under the slabs: the silhouette shifted down, blurred all around.</summary>
        private static Bitmap Shadow(int s, RectangleF[] plates, float radius)
        {
            float offset = 0.022f * s;
            var layer = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(layer))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
                    foreach (RectangleF r in plates)
                    {
                        var moved = new RectangleF(r.X, r.Y + offset, r.Width, r.Height);
                        using (GraphicsPath path = Rounded(moved, radius)) g.FillPath(brush, path);
                    }
            }
            Blur(layer, (int)Math.Round(0.020 * s));
            return layer;
        }

        /// <summary>
        /// Blur with a box window in three passes: enough for the shadow to
        /// look soft; GDI+ has no real Gaussian blur.
        /// </summary>
        private static void Blur(Bitmap bmp, int radius)
        {
            if (radius < 1) return;
            int w = bmp.Width, h = bmp.Height;

            BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h),
                                           ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var pixels = new byte[Math.Abs(data.Stride) * h];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            for (int pass = 0; pass < 3; pass++)
            {
                BlurPass(pixels, w, h, data.Stride, radius, true);
                BlurPass(pixels, w, h, data.Stride, radius, false);
            }

            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            bmp.UnlockBits(data);
        }

        private static void BlurPass(byte[] px, int w, int h, int stride, int radius, bool horizontal)
        {
            int outer = horizontal ? h : w;
            int inner = horizontal ? w : h;
            var line = new byte[inner * 4];

            for (int o = 0; o < outer; o++)
            {
                for (int i = 0; i < inner; i++)
                {
                    int sum0 = 0, sum1 = 0, sum2 = 0, sum3 = 0, n = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int j = i + k;
                        if (j < 0 || j >= inner) continue;
                        int idx = horizontal ? o * stride + j * 4 : j * stride + o * 4;
                        sum0 += px[idx]; sum1 += px[idx + 1]; sum2 += px[idx + 2]; sum3 += px[idx + 3];
                        n++;
                    }
                    line[i * 4] = (byte)(sum0 / n);
                    line[i * 4 + 1] = (byte)(sum1 / n);
                    line[i * 4 + 2] = (byte)(sum2 / n);
                    line[i * 4 + 3] = (byte)(sum3 / n);
                }
                for (int i = 0; i < inner; i++)
                {
                    int idx = horizontal ? o * stride + i * 4 : i * stride + o * 4;
                    px[idx] = line[i * 4]; px[idx + 1] = line[i * 4 + 1];
                    px[idx + 2] = line[i * 4 + 2]; px[idx + 3] = line[i * 4 + 3];
                }
            }
        }

        /// <summary>
        /// Flat variant for small sizes: just the silhouette, pixel for pixel.
        ///
        /// The layout is computed straight in whole pixels, not by rounding each screen
        /// separately. Otherwise the gaps get eaten: fractions of a pixel accumulate, neighboring
        /// edges converge to one coordinate, and at 16 pixels the mark sticks into a single
        /// figure, and differently on different sides.
        /// </summary>
        private static Bitmap DrawFlat(int size)
        {
            int gap   = Math.Max(1, (int)Math.Ceiling(Gap * size));   // a gap matters more than fractions of a pixel
            int sideW = Math.Max(2, (int)Math.Round(SideW * size));
            int mainW = Math.Max(3, (int)Math.Round(MainW * size));
            int sideH = Math.Max(4, (int)Math.Round(SideH * size));
            int mainH = Math.Max(3, (int)Math.Round(MainH * size));

            // If it does not fit, squeeze the middle screen, then the side ones, but not the gaps:
            // without a gap the mark stops reading, without a couple of pixels of width it does not.
            int total = sideW * 2 + gap * 2 + mainW;
            while (total > size && mainW > 3) { mainW--; total--; }
            while (total > size && sideW > 2) { sideW--; total -= 2; }

            // The remainder is odd, so the mark would sit a pixel left of center. We give the pixel
            // to the middle screen: an extra pixel of its width is unnoticeable, a shift of the whole mark
            // is noticeable. The same for height, for each screen separately.
            if ((size - total) % 2 != 0 && mainW > 3) { mainW--; total--; }
            if ((size - sideH) % 2 != 0) sideH--;
            if ((size - mainH) % 2 != 0) mainH--;

            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);

                int x = (size - total) / 2;
                int sideY = (size - sideH) / 2;
                int mainY = (size - mainH) / 2;

                Bar(g, x, sideY, sideW, sideH, SideTop, SideBottom);
                x += sideW + gap;
                Bar(g, x, mainY, mainW, mainH, MainTop, MainBottom);
                x += mainW + gap;
                Bar(g, x, sideY, sideW, sideH, SideTop, SideBottom);
            }
            return bmp;
        }

        /// <summary>One screen in whole pixels: no smoothing and no rounded corners.</summary>
        private static void Bar(Graphics g, int x, int y, int w, int h, Color top, Color bottom)
        {
            using (var brush = new LinearGradientBrush(
                       new Rectangle(x, y - 1, w, h + 2), top, bottom, 90f))
                g.FillRectangle(brush, new Rectangle(x, y, w, h));
        }

        private static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0.5f) { path.AddRectangle(r); return path; }

            float d = radius * 2;
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ------------------------------------------------------------------ writing .ico
        private static void Write(Stream output, List<Bitmap> frames)
        {
            var payloads = new List<byte[]>();
            foreach (Bitmap frame in frames)
                payloads.Add(frame.Width >= 256 ? AsPng(frame) : AsDib(frame));

            var w = new BinaryWriter(output);
            w.Write((short)0);                    // reserved
            w.Write((short)1);                    // type: icon
            w.Write((short)frames.Count);

            int offset = 6 + 16 * frames.Count;
            for (int i = 0; i < frames.Count; i++)
            {
                Bitmap f = frames[i];
                w.Write((byte)(f.Width >= 256 ? 0 : f.Width));    // 256 is written as zero
                w.Write((byte)(f.Height >= 256 ? 0 : f.Height));
                w.Write((byte)0);                 // colors in palette: no palette
                w.Write((byte)0);
                w.Write((short)1);                // planes
                w.Write((short)32);               // bits per pixel
                w.Write(payloads[i].Length);
                w.Write(offset);
                offset += payloads[i].Length;
            }
            foreach (byte[] p in payloads) w.Write(p);
            w.Flush();
        }

        private static byte[] AsPng(Bitmap frame)
        {
            using (var ms = new MemoryStream())
            {
                frame.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Old frame format: DIB header, pixels bottom to top and a transparency mask.
        /// Everything except 256 is written this way: PNG inside an icon is not understood everywhere, for example
        /// System.Drawing.Icon.ToBitmap simply fails on such a frame. For 256 there is
        /// no other way (the frame would come out a quarter megabyte), and it is read only by those
        /// who know about PNG.
        /// </summary>
        private static byte[] AsDib(Bitmap frame)
        {
            int w = frame.Width, h = frame.Height;
            int maskStride = ((w + 31) / 32) * 4;      // a mask row is aligned to 4 bytes

            using (var ms = new MemoryStream())
            {
                var b = new BinaryWriter(ms);
                b.Write(40);                            // header size
                b.Write(w);
                b.Write(h * 2);                         // height: pixels and mask together
                b.Write((short)1);
                b.Write((short)32);
                b.Write(0);                             // no compression
                b.Write(w * h * 4 + maskStride * h);
                b.Write(0); b.Write(0); b.Write(0); b.Write(0);

                for (int y = h - 1; y >= 0; y--)
                    for (int x = 0; x < w; x++)
                    {
                        Color c = frame.GetPixel(x, y);
                        b.Write(c.B); b.Write(c.G); b.Write(c.R); b.Write(c.A);
                    }

                // The mask is needed only for the format: transparency is already set by the A channel,
                // so it is all zeros here, meaning "the pixel is opaque".
                for (int y = 0; y < h; y++) b.Write(new byte[maskStride]);

                b.Flush();
                return ms.ToArray();
            }
        }

        // ------------------------------------------------------------------ preview
        /// <summary>
        /// A sheet with all sizes on light and dark backgrounds, to look at with your eyes.
        /// Small sizes are also shown enlarged without smoothing: they are the ones where
        /// you can see whether the bars landed on the pixel grid.
        /// </summary>
        private static Bitmap Preview(List<Bitmap> frames)
        {
            const int pad = 20, cell = 72, zoom = 6;
            int cols = frames.Count;
            int width = pad + cols * (cell + pad);
            int rowH = cell + pad;
            int zoomH = 16 * zoom + pad * 2;
            int height = pad + rowH * 2 + zoomH + 260;

            var sheet = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(sheet))
            using (var font = new Font("Segoe UI", 9f))
            using (var gray = new SolidBrush(Color.FromArgb(255, 130, 130, 130)))
            {
                g.Clear(Color.FromArgb(255, 245, 245, 245));
                g.FillRectangle(new SolidBrush(Color.FromArgb(255, 28, 28, 28)),
                                0, pad + rowH, width, rowH);

                for (int row = 0; row < 2; row++)
                {
                    int x = pad, y = pad + row * rowH;
                    foreach (Bitmap f in frames)
                    {
                        if (f.Width > cell) { x += cell + pad; continue; }
                        g.DrawImageUnscaled(f, x + (cell - f.Width) / 2, y + (cell - f.Height) / 2);
                        if (row == 0) g.DrawString(f.Width.ToString(), font, gray, x, y + cell);
                        x += cell + pad;
                    }
                }

                // small sizes close up, pixel for pixel
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                int zx = pad, zy = pad + rowH * 2 + pad;
                foreach (Bitmap f in frames)
                {
                    if (f.Width > 24) continue;
                    g.DrawImage(f, new Rectangle(zx, zy, f.Width * zoom, f.Height * zoom));
                    zx += f.Width * zoom + pad;
                }

                // and the largest size in full
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                Bitmap big = frames[frames.Count - 1];
                g.DrawImageUnscaled(big, pad, height - 256 - pad);
            }
            return sheet;
        }
    }
}
