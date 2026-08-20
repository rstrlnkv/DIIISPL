// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;

namespace DIIISPL
{
    /// <summary>
    /// Автотест раскладок: строит схему, применяет её и сверяет фактическое состояние
    /// дисплеев с заявленным. Собирается вместе с боевыми исходниками программы.
    /// </summary>
    internal static class Harness
    {
        private class Case
        {
            public string Name;
            public Layout Layout;
            public bool Twice;             // применить дважды (проверка идемпотентности)
            public string ExpectWarning;   // ожидаемая подстрока в предупреждении
        }

        // Пишем в файл по ходу дела: если система снова упадёт, отчёт до момента падения уцелеет.
        private static System.IO.StreamWriter _log;
        private const int PauseBetweenCasesMs = 4000;

        private static void WL(string s)
        {
            Console.Out.WriteLine(s);
            if (_log != null) _log.WriteLine(s);
        }

        private static void W(string s)
        {
            Console.Out.Write(s);
            if (_log != null) _log.Write(s);
        }

        private static MonitorInfo Find(List<MonitorInfo> list, string part)
        {
            foreach (var m in list) if (m.Name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return m;
            return null;
        }

        private static LayoutDisplay D(MonitorInfo m, bool enabled)
        {
            var d = new LayoutDisplay();
            d.Identity = m.Identity;
            d.Name = m.Name;
            d.Enabled = enabled;
            d.Width = 3840; d.Height = 2160;
            d.Hz = 60;
            d.Rotation = Native.DISPLAYCONFIG_ROTATION_IDENTITY;
            d.Primary = false;
            d.CloneGroup = -1;
            d.Hdr = -1;
            return d;
        }

        private static LayoutDisplay At(LayoutDisplay d, int x, int y) { d.X = x; d.Y = y; return d; }
        private static LayoutDisplay Hz(LayoutDisplay d, double hz) { d.Hz = hz; return d; }
        private static LayoutDisplay Res(LayoutDisplay d, int w, int h) { d.Width = w; d.Height = h; return d; }
        private static LayoutDisplay Rot(LayoutDisplay d, uint r) { d.Rotation = r; return d; }
        private static LayoutDisplay Main_(LayoutDisplay d) { d.Primary = true; return d; }
        private static LayoutDisplay Clone(LayoutDisplay d, int g) { d.CloneGroup = g; return d; }
        private static LayoutDisplay Hdr(LayoutDisplay d, int v) { d.Hdr = v; return d; }

        private static Layout Make(string name, params LayoutDisplay[] ds)
        {
            var l = new Layout();
            l.Name = name;
            foreach (var d in ds) l.Displays.Add(d);
            return l;
        }

        // ---- ожидаемое состояние: основной экран обязан стоять в (0,0) ----
        private static void Normalize(Layout l)
        {
            LayoutDisplay primary = null;
            foreach (var d in l.Displays) if (d.Enabled && d.Primary) { primary = d; break; }
            if (primary == null) foreach (var d in l.Displays) if (d.Enabled) { primary = d; break; }
            if (primary == null) return;
            int dx = -primary.X, dy = -primary.Y;
            foreach (var d in l.Displays) { d.X += dx; d.Y += dy; }
        }

        private static List<string> Check(Layout expected)
        {
            var problems = new List<string>();
            var byId = new Dictionary<string, MonitorInfo>();
            foreach (var m in Monitors.Enumerate()) byId[m.Identity] = m;

            var groupSource = new Dictionary<int, uint>();
            var singleSources = new List<uint>();

            foreach (var d in expected.Displays)
            {
                MonitorInfo mon;
                bool known = byId.TryGetValue(d.Identity, out mon);

                if (!d.Enabled)
                {
                    if (known && mon.Active) problems.Add(d.Name + ": должен быть выключен, но включён");
                    continue;
                }
                if (!known) { problems.Add(d.Name + ": пропал из перечисления"); continue; }
                if (!mon.Active) { problems.Add(d.Name + ": не включился"); continue; }

                if (mon.Width != d.Width || mon.Height != d.Height)
                    problems.Add(d.Name + ": режим " + mon.Width + "x" + mon.Height +
                                 " вместо " + d.Width + "x" + d.Height);
                if (mon.X != d.X || mon.Y != d.Y)
                    problems.Add(d.Name + ": позиция " + mon.X + "," + mon.Y + " вместо " + d.X + "," + d.Y);
                if (d.Hz > 0 && Math.Abs(mon.Hz - d.Hz) > 1.5)
                    problems.Add(d.Name + ": частота " + mon.Hz.ToString("0.##") + " вместо " + d.Hz.ToString("0.##"));
                if (mon.Rotation != d.Rotation)
                    problems.Add(d.Name + ": поворот " + mon.Rotation + " вместо " + d.Rotation);

                if (d.CloneGroup >= 0)
                {
                    uint src;
                    if (!groupSource.TryGetValue(d.CloneGroup, out src)) groupSource[d.CloneGroup] = mon.SourceId;
                    else if (src != mon.SourceId) problems.Add(d.Name + ": не в дубле (разные источники)");
                }
                else
                {
                    if (singleSources.Contains(mon.SourceId))
                        problems.Add(d.Name + ": делит источник с другим экраном, хотя должен быть отдельным");
                    singleSources.Add(mon.SourceId);
                }

                // HDR проверяем только там, где он в принципе доступен
                if (d.Hdr >= 0 && d.CloneGroup < 0 && mon.HdrSupported)
                {
                    bool want = d.Hdr == 1;
                    if (mon.HdrEnabled != want)
                        problems.Add(d.Name + ": HDR " + (mon.HdrEnabled ? "включён" : "выключен") +
                                     ", ожидалось " + (want ? "включён" : "выключен"));
                }
            }
            return problems;
        }

        private static List<Case> BuildCases(MonitorInfo L, MonitorInfo M, MonitorInfo T)
        {
            var cases = new List<Case>();
            Action<string, Layout> add = delegate(string name, Layout l) {
                var c = new Case(); c.Name = name; c.Layout = l; cases.Add(c);
            };

            // --- одиночные ---
            add("одиночный: LG 4K 144", Make("t", Main_(Hz(D(L, true), 144)), D(M, false), D(T, false)));
            add("одиночный: Mi 4K 60", Make("t", D(L, false), Main_(D(M, true)), D(T, false)));
            add("одиночный: ТВ 4K 120", Make("t", D(L, false), D(M, false), Main_(Hz(D(T, true), 120))));

            // --- пары врастяжку ---
            add("пара врастяжку: LG + Mi", Make("t",
                Main_(Hz(D(L, true), 144)), At(D(M, true), 3840, 0), D(T, false)));
            add("пара врастяжку: LG + ТВ", Make("t",
                Main_(Hz(D(L, true), 120)), D(M, false), At(Hz(D(T, true), 120), 3840, 0)));
            add("пара врастяжку: Mi + ТВ", Make("t",
                D(L, false), Main_(D(M, true)), At(Hz(D(T, true), 60), 3840, 0)));

            // --- пары дублированием ---
            add("дубль: LG = Mi", Make("t",
                Clone(Main_(D(L, true)), 0), Clone(D(M, true), 0), D(T, false)));
            add("дубль: LG = ТВ", Make("t",
                Clone(Main_(Hz(D(L, true), 120)), 0), D(M, false), Clone(Hz(D(T, true), 120), 0)));
            add("дубль: Mi = ТВ", Make("t",
                D(L, false), Clone(Main_(D(M, true)), 0), Clone(D(T, true), 0)));

            // --- тройки ---
            add("три врастяжку: LG | Mi | ТВ", Make("t",
                Main_(Hz(D(L, true), 144)), At(D(M, true), 3840, 0), At(Hz(D(T, true), 120), 7680, 0)));
            add("три врастяжку, основной в середине", Make("t",
                At(Hz(D(L, true), 144), -3840, 0), Main_(D(M, true)), At(Hz(D(T, true), 120), 3840, 0)));
            add("дубль LG=ТВ + Mi сбоку", Make("t",
                Clone(Main_(Hz(D(L, true), 120)), 0), At(D(M, true), 3840, 0), Clone(Hz(D(T, true), 120), 0)));
            add("дубль LG=Mi + ТВ сбоку", Make("t",
                Clone(Main_(D(L, true)), 0), Clone(D(M, true), 0), At(Hz(D(T, true), 120), 3840, 0)));
            add("дубль Mi=ТВ + LG сбоку", Make("t",
                Main_(Hz(D(L, true), 144)), Clone(At(D(M, true), 3840, 0), 0), Clone(At(D(T, true), 3840, 0), 0)));
            add("все три в дубль", Make("t",
                Clone(Main_(D(L, true)), 0), Clone(D(M, true), 0), Clone(D(T, true), 0)));

            // --- геометрия ---
            add("вертикальный стек: LG сверху, Mi снизу", Make("t",
                Main_(Hz(D(L, true), 144)), At(D(M, true), 0, 2160), D(T, false)));
            add("экран слева от основного", Make("t",
                Main_(Hz(D(L, true), 144)), At(D(M, true), -3840, 0), D(T, false)));
            add("уголок: Mi справа сверху", Make("t",
                Main_(Hz(D(L, true), 144)), At(D(M, true), 3840, -2160), D(T, false)));

            // --- режимы ---
            add("пониженное разрешение LG 2560x1440", Make("t",
                Main_(Res(Hz(D(L, true), 60), 2560, 1440)), D(M, false), D(T, false)));
            add("смешанные частоты: LG 144 + Mi 60", Make("t",
                Main_(Hz(D(L, true), 144)), At(D(M, true), 3840, 0), D(T, false)));
            add("поворот 90° на Mi", Make("t",
                Main_(Hz(D(L, true), 144)), At(Rot(D(M, true), Native.DISPLAYCONFIG_ROTATION_ROTATE90), 3840, 0), D(T, false)));
            add("поворот 270° на Mi", Make("t",
                Main_(Hz(D(L, true), 144)), At(Rot(D(M, true), Native.DISPLAYCONFIG_ROTATION_ROTATE270), 3840, 0), D(T, false)));

            // --- основной экран ---
            add("основной: ТВ", Make("t",
                At(Hz(D(L, true), 120), 3840, 0), D(M, false), Main_(Hz(D(T, true), 120))));
            add("основной: Mi", Make("t",
                At(Hz(D(L, true), 144), 3840, 0), Main_(D(M, true)), D(T, false)));

            // --- HDR ---
            add("HDR включён на LG", Make("t",
                Hdr(Main_(Hz(D(L, true), 144)), 1), D(M, false), D(T, false)));
            add("HDR выключен на LG", Make("t",
                Hdr(Main_(Hz(D(L, true), 144)), 0), D(M, false), D(T, false)));
            add("HDR на всех трёх врастяжку", Make("t",
                Hdr(Main_(Hz(D(L, true), 120)), 1), Hdr(At(D(M, true), 3840, 0), 1), Hdr(At(Hz(D(T, true), 120), 7680, 0), 1)));

            var hdrClone = new Case();
            hdrClone.Name = "HDR в дубле (ожидается отказ Windows)";
            hdrClone.Layout = Make("t",
                Hdr(Clone(Main_(Hz(D(L, true), 120)), 0), 1), D(M, false), Hdr(Clone(Hz(D(T, true), 120), 0), 1));
            hdrClone.ExpectWarning = "дублировании";
            cases.Add(hdrClone);

            // --- устойчивость ---
            var twice = new Case();
            twice.Name = "повторное применение того же профиля";
            twice.Layout = Make("t", Main_(Hz(D(L, true), 144)), At(D(M, true), 3840, 0), D(T, false));
            twice.Twice = true;
            cases.Add(twice);

            var ghost = new Case();
            var fake = D(T, true);
            fake.Identity = "XXX:НетТакого:000000";
            fake.Name = "Отсутствующий монитор";
            ghost.Name = "профиль с неподключённым монитором";
            ghost.Layout = Make("t", Main_(Hz(D(L, true), 144)), D(M, false), At(fake, 3840, 0));
            ghost.ExpectWarning = "не подключён";
            cases.Add(ghost);

            return cases;
        }

        private static int Main(string[] args)
        {
            // Согласие спрашивает run-tests.cmd, он же передаёт сюда --agreed.
            // Сам по себе selftest.exe не должен делать ничего: файл остаётся
            // лежать в папке после прогона, и двойной щелчок по нему запускал бы
            // сорок настоящих переключений мониторов, ни о чём не спросив.
            bool agreed = false;
            foreach (string a in args) if (a == "--agreed") agreed = true;
            if (!agreed)
            {
                WL("");
                WL("  Напрямую этот файл не запускают: прогон переключает мониторы");
                WL("  около сорока раз подряд. Запускайте run-tests.cmd — он спросит.");
                return 2;
            }

            var live = Monitors.Enumerate();
            MonitorInfo L = Find(live, "LG"), M = Find(live, "Mi"), T = Find(live, "Q80A");
            if (L == null || M == null || T == null)
            {
                WL("Нужны все три монитора. Найдено: " + live.Count);
                foreach (var m in live) WL("  " + m.Name + " — " + (m.Active ? "включён" : "выключен"));
                return 1;
            }

            var cases = BuildCases(L, M, T);
            int pass = 0, fail = 0;
            var failed = new List<string>();
            WL("");
            WL("Проверок: " + cases.Count);
            WL("");

            // Рядом с собой, а не в текущем каталоге: запуск из другой папки уносил
            // отчёт туда, а run-tests.cmd при этом обещал tests\selftest-log.txt.
            string logPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "selftest-log.txt");
            _log = new System.IO.StreamWriter(logPath, false, new System.Text.UTF8Encoding(true));
            _log.AutoFlush = true;
            var only = new List<int>();
            foreach (string a in args) { int n; if (int.TryParse(a, out n)) only.Add(n); }

            for (int i = 0; i < cases.Count; i++)
            {
                if (only.Count > 0 && !only.Contains(i + 1)) continue;
                var c = cases[i];
                string label = "[" + (i + 1).ToString().PadLeft(2) + "/" + cases.Count + "] " + c.Name;
                W(label.PadRight(52) + " ");

                var res = Applier.Apply(c.Layout);
                if (c.Twice) res = Applier.Apply(c.Layout);
                System.Threading.Thread.Sleep(900);
                // пауза между схемами: не даём драйверу захлебнуться чередой перестроек

                var problems = new List<string>();
                if (!res.Success) problems.Add("не применилось: " + res.Error);

                var expected = c.Layout.DeepCopy();
                Normalize(expected);
                if (c.ExpectWarning != null)
                {
                    // ожидаемая проблема: экран из заявки заведомо недоступен
                    bool found = false;
                    foreach (string w in res.Warnings)
                        if (w.IndexOf(c.ExpectWarning, StringComparison.OrdinalIgnoreCase) >= 0) found = true;
                    if (!found) problems.Add("не было ожидаемого предупреждения про «" + c.ExpectWarning + "»");
                    foreach (var d in expected.Displays)
                        if (d.Name == "Отсутствующий монитор" || (d.CloneGroup >= 0 && d.Hdr == 1)) d.Hdr = -1;
                    if (c.Name.StartsWith("профиль с неподключённым"))
                        foreach (var d in expected.Displays)
                            if (d.Name == "Отсутствующий монитор") d.Enabled = false;
                }
                problems.AddRange(Check(expected));

                if (problems.Count == 0) { WL("OK"); pass++; }
                else
                {
                    WL("СБОЙ");
                    foreach (string p in problems) WL("        · " + p);
                    foreach (string w in res.Warnings) WL("        [пред] " + w);
                    foreach (var m in Monitors.Enumerate())
                        WL("        [факт] " + m.Name + " " + (m.Active ? "вкл" : "выкл") +
                            " " + m.Width + "x" + m.Height + " @" + m.X + "," + m.Y +
                            " " + m.Hz.ToString("0.##") + "Гц rot=" + m.Rotation + " src=" + m.SourceId +
                            " hdr=" + (m.HdrSupported ? (m.HdrEnabled ? "on" : "off") : "na"));
                    fail++;
                    failed.Add(c.Name);
                }

                // пауза между схемами: не даём графическому стеку захлебнуться чередой перестроек
                System.Threading.Thread.Sleep(PauseBetweenCasesMs);
            }

            WL("");
            WL("Итог: " + pass + " из " + cases.Count + " успешно, сбоев " + fail);
            if (fail > 0)
            {
                WL("Не прошли:");
                foreach (string f in failed) WL("  · " + f);
            }

            // возвращаем рабочее состояние: три экрана врастяжку
            var restore = Make("restore", Main_(Hz(D(L, true), 144)), At(D(M, true), 3840, 0), At(Hz(D(T, true), 120), 7680, 0));
            Applier.Apply(restore);
            WL("");
            WL("Раскладка возвращена: три экрана врастяжку.");
            return fail == 0 ? 0 : 1;
        }
    }
}
