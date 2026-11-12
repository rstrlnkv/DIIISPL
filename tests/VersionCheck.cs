// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

// Text-only checks, run through tests\version-check.cmd:
//  1. src\AssemblyInfo.cs has exactly one AssemblyInformationalVersion, x.y.z or x.y.z-dev.N.
//  2. Its x.y.z equals AssemblyVersion and AssemblyFileVersion (the rule release.yml applies).
//  3. The About/Licenses string keys exist in both languages of src\Lang.cs.
//  4. LICENSE and licenses\*.txt exist and hold the expected licence text.
//  5. LicensesWindow is mentioned in other sources only inside #if DEV.
//  6. Each licenses.* resource LicensesWindow reads is embedded by scripts\build.cmd from the right file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DIIISPL
{
    internal static class VersionCheck
    {
        private static readonly List<string> Failures = new List<string>();

        private static void Check(bool ok, string what)
        {
            if (!ok) Failures.Add(what);
        }

        private static string Read(string root, string rel)
        {
            string path = Path.Combine(root, rel);
            if (!File.Exists(path)) { Failures.Add(rel + " is missing"); return ""; }
            string text = File.ReadAllText(path, Encoding.UTF8);
            Check(text.Trim().Length > 0, rel + " is empty");
            return text;
        }

        private static int Main()
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            if (!File.Exists(Path.Combine(root, "src", "AssemblyInfo.cs")))
                root = Directory.GetCurrentDirectory();

            Version(root);
            Strings(root);
            Licences(root);
            Mentions(root);
            Resources(root);

            if (Failures.Count == 0) { Console.WriteLine("OK"); return 0; }
            foreach (string f in Failures) Console.WriteLine("FAIL: " + f);
            return 1;
        }

        private static void Version(string root)
        {
            string asm = Read(root, Path.Combine("src", "AssemblyInfo.cs"));
            MatchCollection info = Regex.Matches(asm, @"AssemblyInformationalVersion\(""([^""]*)""\)");
            Check(info.Count == 1, "AssemblyInfo.cs must have exactly one AssemblyInformationalVersion");
            if (info.Count != 1) return;
            string full = info[0].Groups[1].Value;
            Match m = Regex.Match(full, @"^(\d+\.\d+\.\d+)(-dev\.\d+)?$");
            Check(m.Success, "AssemblyInformationalVersion '" + full + "' is not x.y.z or x.y.z-dev.N");
            if (!m.Success) return;
            foreach (string attr in new[] { "AssemblyVersion", "AssemblyFileVersion" })
            {
                Match a = Regex.Match(asm, attr + @"\(""(\d+\.\d+\.\d+)\.\d+""\)");
                Check(a.Success && a.Groups[1].Value == m.Groups[1].Value,
                    attr + " does not start with " + m.Groups[1].Value);
            }
        }

        private static void Strings(string root)
        {
            string lang = Read(root, Path.Combine("src", "Lang.cs"));
            int ru = lang.IndexOf("Dictionary<string, string> Ru()", StringComparison.Ordinal);
            Check(ru > 0, "Lang.cs has no Ru()");
            if (ru <= 0) return;
            string[] parts = { lang.Substring(0, ru), lang.Substring(ru) };
            string[] names = { "En()", "Ru()" };
            foreach (string key in new[] { "about.licenses", "licenses.title",
                "licenses.note", "licenses.missing", "licenses.sameGpl",
                "licenses.translucentHolder", "licenses.gplOnly" })
                for (int i = 0; i < 2; i++)
                    Check(parts[i].Contains("d[\"" + key + "\"] ="), "key " + key + " is missing in " + names[i]);
        }

        private static void Licences(string root)
        {
            Check(Read(root, "LICENSE").Contains("GNU GENERAL PUBLIC LICENSE"), "LICENSE is not the GNU GPL text");
            foreach (string f in new[] { "detours.txt", "wil.txt", "cppwinrt.txt" })
                Check(Read(root, Path.Combine("licenses", f)).Contains("Permission is hereby granted"),
                    "licenses\\" + f + " is not the MIT text");
        }

        // Each resource LicensesWindow reads must be embedded by the Dev build, from the right file.
        private static void Resources(string root)
        {
            string build = Read(root, Path.Combine("scripts", "build.cmd"));
            string win = Read(root, Path.Combine("src", "LicensesWindow.cs"));
            var map = new[] { new[] { "gpl3", "LICENSE" }, new[] { "detours", "licenses\\detours.txt" },
                new[] { "wil", "licenses\\wil.txt" }, new[] { "cppwinrt", "licenses\\cppwinrt.txt" } };
            foreach (string[] r in map)
            {
                Check(win.Contains("\"licenses." + r[0] + "\""), "LicensesWindow.cs does not read licenses." + r[0]);
                Check(Regex.IsMatch(build, "/resource:\"%ROOT%\\\\" + Regex.Escape(r[1]) + "\",licenses\\." + r[0] + @"(\s|$)"),
                    "scripts\\build.cmd does not embed " + r[1] + " as licenses." + r[0]);
            }
        }

        private static void Mentions(string root)
        {
            foreach (string file in Directory.GetFiles(Path.Combine(root, "src"), "*.cs"))
            {
                if (Path.GetFileName(file) == "LicensesWindow.cs") continue;
                int devDepth = 0;    // nesting of #if DEV blocks
                var stack = new Stack<bool>();
                string[] lines = File.ReadAllLines(file, Encoding.UTF8);
                for (int n = 0; n < lines.Length; n++)
                {
                    string t = lines[n].Trim();
                    if (t.StartsWith("#if ")) { bool dev = t == "#if DEV"; stack.Push(dev); if (dev) devDepth++; }
                    else if (t.StartsWith("#endif") && stack.Count > 0) { if (stack.Pop()) devDepth--; }
                    else if (t.StartsWith("#else") || t.StartsWith("#elif"))
                    {
                        // the other branch of #if DEV is the Stable one
                        if (stack.Count > 0 && stack.Peek()) { stack.Pop(); stack.Push(false); devDepth--; }
                    }
                    else if (lines[n].Contains("LicensesWindow"))
                        Check(devDepth > 0, Path.GetFileName(file) + ":" + (n + 1) + " mentions LicensesWindow outside #if DEV");
                }
            }
        }
    }
}
