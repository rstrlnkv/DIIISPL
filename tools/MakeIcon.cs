// DIIIX — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.
//
// Рисует значок программы и складывает его в многоразмерный DIIIX.ico.
// Значок хранится в репозитории собранным, но собирается отсюда — чтобы его можно было
// поправить, не заводя графический редактор и не гадая, чем именно он был нарисован.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace DIIIX.Tools
{
    internal static class MakeIcon
    {
        // Знак: раскладка из трёх экранов — широкий основной посередине и два боковых,
        // повёрнутых набок. Разные пропорции важнее украшений: три одинаковые полосы
        // читаются как эквалайзер, а экран в альбом рядом с экранами в портрет —
        // ни с чем не спутаешь, и это ровно то, чем программа занимается.
        //
        // Поля узкие: значок должен заполнять отведённое место, иначе рядом с чужими
        // выглядит мелким. На 16 точках выходит 3+1+7+1+3 — всё ещё различимо.
        private const double SideW = 0.185, SideH = 0.720;
        private const double MainW = 0.460, MainH = 0.360;
        private const double Gap   = 0.050;

        private static readonly Color MainTop    = Color.FromArgb(255, 0x5A, 0xB0, 0xFF);
        private static readonly Color MainBottom = Color.FromArgb(255, 0x0B, 0x5C, 0xD6);
        private static readonly Color SideTop    = Color.FromArgb(255, 0x4C, 0x86, 0xC8);
        private static readonly Color SideBottom = Color.FromArgb(255, 0x2C, 0x5B, 0x96);

        private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

        private static int Main(string[] args)
        {
            string outPath = args.Length > 0 ? args[0] : "DIIIX.ico";
            string preview = args.Length > 1 ? args[1] : null;

            var frames = new List<Bitmap>();
            foreach (int size in Sizes) frames.Add(Draw(size));

            using (var file = File.Create(outPath)) Write(file, frames);
            Console.WriteLine("Готово: " + Path.GetFullPath(outPath) + "  размеров: " + Sizes.Length);

            if (preview != null)
            {
                using (Bitmap sheet = Preview(frames)) sheet.Save(preview, ImageFormat.Png);
                Console.WriteLine("Образец: " + Path.GetFullPath(preview));
            }

            foreach (Bitmap b in frames) b.Dispose();
            return 0;
        }

        // ------------------------------------------------------------------ рисование
        private static Bitmap Draw(int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                double s = size;
                double total = SideW + Gap + MainW + Gap + SideW;
                double x = (1.0 - total) / 2.0;

                // На мелких размерах скруглять нечего: радиус меньше точки превращает
                // углы в грязь, поэтому там полосы остаются прямыми.
                float radius = size >= 32 ? (float)(0.038 * s) : 0f;

                Bar(g, s, x, (1.0 - SideH) / 2.0, SideW, SideH, radius, SideTop, SideBottom);
                x += SideW + Gap;
                Bar(g, s, x, (1.0 - MainH) / 2.0, MainW, MainH, radius, MainTop, MainBottom);
                x += MainW + Gap;
                Bar(g, s, x, (1.0 - SideH) / 2.0, SideW, SideH, radius, SideTop, SideBottom);
            }
            return bmp;
        }

        private static void Bar(Graphics g, double s, double x, double y, double w, double h,
                                float radius, Color top, Color bottom)
        {
            // Полосы прижимаются к сетке точек: иначе на 16 точках края размываются
            // на полтона и знак выглядит грязным.
            float left = (float)Math.Round(x * s);
            float width = Math.Max(1f, (float)Math.Round(w * s));
            float topY = (float)Math.Round(y * s);
            float height = Math.Max(1f, (float)Math.Round(h * s));
            var rect = new RectangleF(left, topY, width, height);

            using (var brush = new LinearGradientBrush(
                       new RectangleF(left, topY - 1, width, height + 2), top, bottom, 90f))
            using (GraphicsPath path = Rounded(rect, radius))
                g.FillPath(brush, path);
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

        // ------------------------------------------------------------------ запись .ico
        private static void Write(Stream output, List<Bitmap> frames)
        {
            var payloads = new List<byte[]>();
            foreach (Bitmap frame in frames)
                payloads.Add(frame.Width >= 64 ? AsPng(frame) : AsDib(frame));

            var w = new BinaryWriter(output);
            w.Write((short)0);                    // зарезервировано
            w.Write((short)1);                    // тип: значок
            w.Write((short)frames.Count);

            int offset = 6 + 16 * frames.Count;
            for (int i = 0; i < frames.Count; i++)
            {
                Bitmap f = frames[i];
                w.Write((byte)(f.Width >= 256 ? 0 : f.Width));    // 256 записывается нулём
                w.Write((byte)(f.Height >= 256 ? 0 : f.Height));
                w.Write((byte)0);                 // цветов в палитре: нет палитры
                w.Write((byte)0);
                w.Write((short)1);                // плоскостей
                w.Write((short)32);               // бит на точку
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
        /// Старый формат кадра: заголовок DIB, точки снизу вверх и маска прозрачности.
        /// Мелкие размеры пишутся именно так — PNG в них понимают не все места системы.
        /// </summary>
        private static byte[] AsDib(Bitmap frame)
        {
            int w = frame.Width, h = frame.Height;
            int maskStride = ((w + 31) / 32) * 4;      // строка маски выравнивается на 4 байта

            using (var ms = new MemoryStream())
            {
                var b = new BinaryWriter(ms);
                b.Write(40);                            // размер заголовка
                b.Write(w);
                b.Write(h * 2);                         // высота: точки и маска вместе
                b.Write((short)1);
                b.Write((short)32);
                b.Write(0);                             // без сжатия
                b.Write(w * h * 4 + maskStride * h);
                b.Write(0); b.Write(0); b.Write(0); b.Write(0);

                for (int y = h - 1; y >= 0; y--)
                    for (int x = 0; x < w; x++)
                    {
                        Color c = frame.GetPixel(x, y);
                        b.Write(c.B); b.Write(c.G); b.Write(c.R); b.Write(c.A);
                    }

                // Маска нужна только ради формата: прозрачность уже задана каналом A,
                // поэтому здесь всюду нули — «точка непрозрачна».
                for (int y = 0; y < h; y++) b.Write(new byte[maskStride]);

                b.Flush();
                return ms.ToArray();
            }
        }

        // ------------------------------------------------------------------ образец
        /// <summary>
        /// Лист со всеми размерами на светлом и тёмном фоне — чтобы посмотреть глазами.
        /// Мелкие размеры показаны ещё и увеличенными без сглаживания: именно на них
        /// видно, попали ли полосы в сетку точек.
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

                // мелкие размеры крупным планом, точка в точку
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                int zx = pad, zy = pad + rowH * 2 + pad;
                foreach (Bitmap f in frames)
                {
                    if (f.Width > 24) continue;
                    g.DrawImage(f, new Rectangle(zx, zy, f.Width * zoom, f.Height * zoom));
                    zx += f.Width * zoom + pad;
                }

                // и самый большой размер целиком
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                Bitmap big = frames[frames.Count - 1];
                g.DrawImageUnscaled(big, pad, height - 256 - pad);
            }
            return sheet;
        }
    }
}
