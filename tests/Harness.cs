// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Collections.Generic;

namespace DIIISPL
{
    /// <summary>
    /// Layout autotest: builds a layout, applies it and checks the actual state
    /// of the displays against the declared one. Built together with the program's production sources.
    /// </summary>
    internal static class Harness
    {
        private class Case
        {
            public string Name;
            public Layout Layout;
            public bool Twice;             // apply twice (idempotency check)
            public string ExpectWarning;   // expected substring in the warning
        }

        // Write to a file as we go: if the system crashes again, the report up to the crash survives.
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

        // ---- expected state: the primary screen must sit at (0,0) ----
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
                    if (known && mon.Active) problems.Add(d.Name + ": should be off but is on");
                    continue;
                }
                if (!known) { problems.Add(d.Name + ": vanished from enumeration"); continue; }
                if (!mon.Active) { problems.Add(d.Name + ": did not turn on"); continue; }

                if (mon.Width != d.Width || mon.Height != d.Height)
                    problems.Add(d.Name + ": mode " + mon.Width + "x" + mon.Height +
                                 " instead of " + d.Width + "x" + d.Height);
                if (mon.X != d.X || mon.Y != d.Y)
                    problems.Add(d.Name + ": position " + mon.X + "," + mon.Y + " instead of " + d.X + "," + d.Y);
                if (d.Hz > 0 && Math.Abs(mon.Hz - d.Hz) > 1.5)
                    problems.Add(d.Name + ": refresh rate " + mon.Hz.ToString("0.##") + " instead of " + d.Hz.ToString("0.##"));
                if (mon.Rotation != d.Rotation)
                    problems.Add(d.Name + ": rotation " + mon.Rotation + " instead of " + d.Rotation);

                if (d.CloneGroup >= 0)
                {
                    uint src;
                    if (!groupSource.TryGetValue(d.CloneGroup, out src)) groupSource[d.CloneGroup] = mon.SourceId;
                    else if (src != mon.SourceId) problems.Add(d.Name + ": not cloned (different sources)");
                }
                else
                {
                    if (singleSources.Contains(mon.SourceId))
                        problems.Add(d.Name + ": shares a source with another screen though it should be separate");
                    singleSources.Add(mon.SourceId);
                }

                // check HDR only where it is available at all
                if (d.Hdr >= 0 && d.CloneGroup < 0 && mon.HdrSupported)
                {
                    bool want = d.Hdr == 1;
                    if (mon.HdrEnabled != want)
                        problems.Add(d.Name + ": HDR " + (mon.HdrEnabled ? "on" : "off") +
                                     ", expected " + (want ? "on" : "off"));
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

            // --- single ---
            add("single: LG 4K 144", Make("t", Main_(Hz(D(L, true), 144)), D(M, false), D(T, false)));
            add("single: Mi 4K 60", Make("t", D(L, false), Main_(D(M, true)), D(T, false)));
            add("single: TV 4K 120", Make("t", D(L, false), D(M, false), Main_(Hz(D(T, true), 120))));

            // --- extended pairs ---
            add("extended pair: LG + Mi", Make("t",
                Main_(Hz(D(L, true), 144)), At(D(M, true), 3840, 0), D(T, false)));
            add("extended pair: LG + TV", Make("t",
                Main_(Hz(D(L, true), 120)), D(M, false), At(Hz(D(T, true), 120), 3840, 0)));
            add("extended pair: Mi + TV", Make("t",
                D(L, false), Main_(D(M, true)), At(Hz(D(T, true), 60), 3840, 0)));

            // --- cloned pairs ---
            add("clone: LG = Mi", Make("t",
                Clone(Main_(D(L, true)), 0), Clone(D(M, true), 0), D(T, false)));
            add("clone: LG = TV", Make("t",
                Clone(Main_(Hz(D(L, true), 120)), 0), D(M, false), Clone(Hz(D(T, true), 120), 0)));
            add("clone: Mi = TV", Make("t",
                D(L, false), Clone(Main_(D(M, true)), 0), Clone(D(T, true), 0)));

            // --- triples ---
            add("three extended: LG | Mi | TV", Make("t",
                Main_(Hz(D(L, true), 144)), At(D(M, true), 3840, 0), At(Hz(D(T, true), 120), 7680, 0)));
            add("three extended, primary in the middle", Make("t",
                At(Hz(D(L, true), 144), -3840, 0), Main_(D(M, true)), At(Hz(D(T, true), 120), 3840, 0)));
            add("clone LG=TV + Mi on the side", Make("t",
                Clone(Main_(Hz(D(L, true), 120)), 0), At(D(M, true), 3840, 0), Clone(Hz(D(T, true), 120), 0)));
            add("clone LG=Mi + TV on the side", Make("t",
                Clone(Main_(D(L, true)), 0), Clone(D(M, true), 0), At(Hz(D(T, true), 120), 3840, 0)));
            add("clone Mi=TV + LG on the side", Make("t",
                Main_(Hz(D(L, true), 144)), Clone(At(D(M, true), 3840, 0), 0), Clone(At(D(T, true), 3840, 0), 0)));
            add("all three cloned", Make("t",
                Clone(Main_(D(L, true)), 0), Clone(D(M, true), 0), Clone(D(T, true), 0)));

            // --- geometry ---
            add("vertical stack: LG on top, Mi below", Make("t",
                Main_(Hz(D(L, true), 144)), At(D(M, true), 0, 2160), D(T, false)));
            add("screen left of the primary", Make("t",
                Main_(Hz(D(L, true), 144)), At(D(M, true), -3840, 0), D(T, false)));
            add("corner: Mi top right", Make("t",
                Main_(Hz(D(L, true), 144)), At(D(M, true), 3840, -2160), D(T, false)));

            // --- modes ---
            add("reduced resolution LG 2560x1440", Make("t",
                Main_(Res(Hz(D(L, true), 60), 2560, 1440)), D(M, false), D(T, false)));
            add("mixed refresh rates: LG 144 + Mi 60", Make("t",
                Main_(Hz(D(L, true), 144)), At(D(M, true), 3840, 0), D(T, false)));
            add("rotation 90° on Mi", Make("t",
                Main_(Hz(D(L, true), 144)), At(Rot(D(M, true), Native.DISPLAYCONFIG_ROTATION_ROTATE90), 3840, 0), D(T, false)));
            add("rotation 270° on Mi", Make("t",
                Main_(Hz(D(L, true), 144)), At(Rot(D(M, true), Native.DISPLAYCONFIG_ROTATION_ROTATE270), 3840, 0), D(T, false)));

            // --- primary screen ---
            add("primary: TV", Make("t",
                At(Hz(D(L, true), 120), 3840, 0), D(M, false), Main_(Hz(D(T, true), 120))));
            add("primary: Mi", Make("t",
                At(Hz(D(L, true), 144), 3840, 0), Main_(D(M, true)), D(T, false)));

            // --- HDR ---
            add("HDR on for LG", Make("t",
                Hdr(Main_(Hz(D(L, true), 144)), 1), D(M, false), D(T, false)));
            add("HDR off for LG", Make("t",
                Hdr(Main_(Hz(D(L, true), 144)), 0), D(M, false), D(T, false)));
            add("HDR on all three extended", Make("t",
                Hdr(Main_(Hz(D(L, true), 120)), 1), Hdr(At(D(M, true), 3840, 0), 1), Hdr(At(Hz(D(T, true), 120), 7680, 0), 1)));

            var hdrClone = new Case();
            hdrClone.Name = "HDR in a clone (Windows is expected to refuse)";
            hdrClone.Layout = Make("t",
                Hdr(Clone(Main_(Hz(D(L, true), 120)), 0), 1), D(M, false), Hdr(Clone(Hz(D(T, true), 120), 0), 1));
            hdrClone.ExpectWarning = "дублировании";
            cases.Add(hdrClone);

            // --- robustness ---
            var twice = new Case();
            twice.Name = "applying the same profile again";
            twice.Layout = Make("t", Main_(Hz(D(L, true), 144)), At(D(M, true), 3840, 0), D(T, false));
            twice.Twice = true;
            cases.Add(twice);

            var ghost = new Case();
            var fake = D(T, true);
            fake.Identity = "XXX:НетТакого:000000";
            fake.Name = "Missing monitor";
            ghost.Name = "profile with a disconnected monitor";
            ghost.Layout = Make("t", Main_(Hz(D(L, true), 144)), D(M, false), At(fake, 3840, 0));
            ghost.ExpectWarning = "не подключён";
            cases.Add(ghost);

            return cases;
        }

        private static int Main(string[] args)
        {
            // run-tests.cmd asks for consent and passes --agreed here.
            // On its own selftest.exe must do nothing: the file stays
            // in the folder after the run, and a double click on it would start
            // forty real monitor switches without asking anything.
            bool agreed = false;
            foreach (string a in args) if (a == "--agreed") agreed = true;
            if (!agreed)
            {
                WL("");
                WL("  This file is not run directly: the run switches monitors");
                WL("  about forty times in a row. Run run-tests.cmd, it will ask.");
                return 2;
            }

            var live = Monitors.Enumerate();
            MonitorInfo L = Find(live, "LG"), M = Find(live, "Mi"), T = Find(live, "Q80A");
            if (L == null || M == null || T == null)
            {
                WL("All three monitors are required. Found: " + live.Count);
                foreach (var m in live) WL("  " + m.Name + " — " + (m.Active ? "on" : "off"));
                return 1;
            }

            var cases = BuildCases(L, M, T);
            int pass = 0, fail = 0;
            var failed = new List<string>();
            WL("");
            WL("Checks: " + cases.Count);
            WL("");

            // Next to itself, not in the current directory: running from another folder carried
            // the report off there, while run-tests.cmd promised tests\selftest-log.txt.
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
                // pause between layouts: do not let the driver choke on a run of rebuilds

                var problems = new List<string>();
                if (!res.Success) problems.Add("did not apply: " + res.Error);

                var expected = c.Layout.DeepCopy();
                Normalize(expected);
                if (c.ExpectWarning != null)
                {
                    // expected problem: a screen from the request is known to be unavailable
                    bool found = false;
                    foreach (string w in res.Warnings)
                        if (w.IndexOf(c.ExpectWarning, StringComparison.OrdinalIgnoreCase) >= 0) found = true;
                    if (!found) problems.Add("expected warning about '" + c.ExpectWarning + "' was not raised");
                    foreach (var d in expected.Displays)
                        if (d.Name == "Missing monitor" || (d.CloneGroup >= 0 && d.Hdr == 1)) d.Hdr = -1;
                    if (c.Name.StartsWith("profile with a disconnected"))
                        foreach (var d in expected.Displays)
                            if (d.Name == "Missing monitor") d.Enabled = false;
                }
                problems.AddRange(Check(expected));

                if (problems.Count == 0) { WL("OK"); pass++; }
                else
                {
                    WL("FAIL");
                    foreach (string p in problems) WL("        · " + p);
                    foreach (string w in res.Warnings) WL("        [warn] " + w);
                    foreach (var m in Monitors.Enumerate())
                        WL("        [actual] " + m.Name + " " + (m.Active ? "on" : "off") +
                            " " + m.Width + "x" + m.Height + " @" + m.X + "," + m.Y +
                            " " + m.Hz.ToString("0.##") + "Hz rot=" + m.Rotation + " src=" + m.SourceId +
                            " hdr=" + (m.HdrSupported ? (m.HdrEnabled ? "on" : "off") : "na"));
                    fail++;
                    failed.Add(c.Name);
                }

                // pause between layouts: do not let the graphics stack choke on a run of rebuilds
                System.Threading.Thread.Sleep(PauseBetweenCasesMs);
            }

            WL("");
            WL("Result: " + pass + " of " + cases.Count + " passed, failures " + fail);
            if (fail > 0)
            {
                WL("Failed:");
                foreach (string f in failed) WL("  · " + f);
            }

            // restore the working state: three screens extended
            var restore = Make("restore", Main_(Hz(D(L, true), 144)), At(D(M, true), 3840, 0), At(Hz(D(T, true), 120), 7680, 0));
            Applier.Apply(restore);
            WL("");
            WL("Layout restored: three screens extended.");
            return fail == 0 ? 0 : 1;
        }
    }
}
