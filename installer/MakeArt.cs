// DIIIX — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.
//
// Рисует картинки для окон установщика: полосу вверху и боковину приветствия.
// Размеры заданы форматом установщика Windows: 493×58 и 493×312 точек.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace DIIIX.Tools
{
    internal static class MakeArt
    {
        // Те же цвета, что и у значка программы.
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

            Console.WriteLine("Картинки установщика: " + Path.GetFullPath(dir));
            return 0;
        }

        /// <summary>
        /// Полоса вверху окон. Заголовок и пояснение установщик пишет поверх неё слева
        /// тёмным по светлому, поэтому фон светлый, а знак уходит вправо.
        /// </summary>
        private static Bitmap Banner()
        {
            var bmp = new Bitmap(493, 58, PixelFormat.Format24bppRgb);
            using (Graphics g = Prepare(bmp))
            {
                Fill(g, new Rectangle(0, 0, 493, 58), LightTop, LightBottom);

                // тонкая линия у нижнего края — та же, что отделяет шапку в самой программе
                using (var line = new SolidBrush(Color.FromArgb(0xDF, 0xE5, 0xEE)))
                    g.FillRectangle(line, 0, 57, 493, 1);

                // Знак жмётся к правому краю: пояснение под заголовком установщик тянет
                // почти до сюда, и знак покрупнее оказывался прямо поверх текста.
                Mark(g, 398, 16, 26);
                Word(g, 439, 18, 13f, Color.FromArgb(0x1A, 0x1A, 0x1A));
            }
            return bmp;
        }

        /// <summary>
        /// Боковина окна приветствия и последнего окна. Слева тёмная полоса со знаком,
        /// справа светлое поле: туда установщик кладёт свой текст.
        /// </summary>
        private static Bitmap Dialog()
        {
            const int panel = 175;
            var bmp = new Bitmap(493, 312, PixelFormat.Format24bppRgb);
            using (Graphics g = Prepare(bmp))
            {
                // Правая половина ровно белая, без градиента: флажок «Запустить DIIIX»
                // на последнем окне рисует свой фон поверх картинки, и на градиенте
                // от него оставалась светлая полоса.
                using (var white = new SolidBrush(Color.White))
                    g.FillRectangle(white, 0, 0, 493, 312);
                Fill(g, new Rectangle(0, 0, panel, 312), PanelTop, PanelBottom);

                // Мягкое свечение за знаком: тёмная полоса без него выглядит плоской
                // заливкой. Обрезаем по панели — иначе пятно вылезает на светлое поле
                // и читается не свечением, а кляксой.
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

                // Знак и подписи выровнены по центру панели: ширина знака 1,28 его высоты.
                float markW = 88 * 1.28f;
                float left = (panel - markW) / 2;

                Mark(g, left, 92, 88);
                Word(g, left, 198, 20f, Color.White);

                using (var font = new Font("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point))
                using (var brush = new SolidBrush(Color.FromArgb(0x93, 0x9D, 0xAC)))
                    g.DrawString("Раскладки мониторов", font, brush, left, 231);
            }
            return bmp;
        }

        // ------------------------------------------------------------------ детали
        /// <summary>Знак программы: широкий экран посередине, два боковых набок.</summary>
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

        private static void Word(Graphics g, float x, float y, float size, Color color)
        {
            using (var font = new Font("Segoe UI Semibold", size, FontStyle.Regular, GraphicsUnit.Point))
            using (var brush = new SolidBrush(color))
                g.DrawString("DIIIX", font, brush, x, y);
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

        /// <summary>Установщик читает только BMP, причём без прозрачности.</summary>
        private static void Save(Bitmap bmp, string path)
        {
            bmp.Save(path, ImageFormat.Bmp);
        }
    }
}
