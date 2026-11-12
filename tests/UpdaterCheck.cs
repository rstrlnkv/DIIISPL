// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

// Checks the version text logic of the updater (VersionText.cs): Parse, IsNewer and Display.
// Run through tests\updater-check.cmd.

using System;
using System.Collections.Generic;

namespace DIIISPL
{
    internal static class UpdaterCheck
    {
        private static readonly List<string> Failures = new List<string>();

        private static void Check(bool ok, string what)
        {
            if (!ok) Failures.Add(what);
        }

        private static void Parses(string text, string expected)
        {
            Version v = VersionText.Parse(text);
            string got = v == null ? "null" : v.ToString();
            Check(got == expected, "Parse(" + (text ?? "null") + ") = " + got + ", expected " + expected);
        }

        private static void Newer(string a, string b, bool expected)
        {
            bool got = VersionText.IsNewer(VersionText.Parse(a), VersionText.Parse(b));
            Check(got == expected, "IsNewer(" + a + ", " + b + ") = " + got + ", expected " + expected);
        }

        private static void Shows(string raw, string expected)
        {
            string got = VersionText.Display(raw);
            Check(got == expected, "Display(" + (raw ?? "null") + ") = " + (got ?? "null") + ", expected " + (expected ?? "null"));
        }

        private static int Main()
        {
            Parses("v0.2.4", "0.2.4.0");
            Parses("V0.2.4", "0.2.4.0");
            Parses("0.2.4-dev.1", "0.2.4.0");
            Parses("0.2.4+abc", "0.2.4.0");
            Parses("1.2.3.4", "1.2.3.4");
            Parses("1.2.3.4.5", "1.2.3.4");
            Parses("1.2", "1.2.0.0");
            Parses("latest", "null");
            Parses("garbage", "null");
            Parses("", "null");
            // A leading minus must not reach new Version (it would throw).
            Parses("-1.0", "null");
            Parses("v-1", "null");
            Parses("1.-2", "1.0.0.0");
            Parses(null, "null");

            Newer("0.2.5", "0.2.4", true);
            Newer("0.2.4", "0.2.5", false);
            Newer("0.2.4", "0.2.4", false);
            Newer("1.0.0", "0.9.9", true);
            // Dev builds of one x.y.z compare equal: the -dev.N suffix is dropped by Parse.
            Newer("0.2.4-dev.2", "0.2.4-dev.1", false);
            Newer("0.2.4-dev.1", "0.2.4-dev.2", false);
            Newer("v0.2.4", "0.2.4-dev.1", false);
            Newer("0.2.5-dev.1", "0.2.4", true);

            Shows("0.2.4-dev.1+abc123", "0.2.4-dev.1");
            Shows("  0.2.4  ", "0.2.4");
            Shows("0.2.4", "0.2.4");
            Shows("+abc", null);
            Shows("", null);
            Shows(null, null);

            if (Failures.Count == 0) { Console.WriteLine("OK"); return 0; }
            foreach (string f in Failures) Console.WriteLine("FAIL: " + f);
            return 1;
        }
    }
}
