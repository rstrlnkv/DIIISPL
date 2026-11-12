// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

// Checks that CHANGELOG.md (English) and CHANGELOG.ru.md parse with
// Changelog.Parse and say the same thing: same versions, dates, line counts and
// badges. Run through tests\changelog-check.cmd.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DIIISPL
{
    // Stand-ins for the app types Changelog.Read() refers to; the check calls Parse, Split, Plain, BadgeOfGroup and Highlights only.
    internal static class Text { public static string Code { get { return "en"; } } }
    internal static class Lang { public static readonly string[] Codes = { "en", "ru" }; }

    internal static class ChangelogCheck
    {
        private static readonly List<string> Failures = new List<string>();

        private static void Check(bool ok, string what)
        {
            if (!ok) Failures.Add(what);
        }

        private static int Main()
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            if (!File.Exists(Path.Combine(root, "CHANGELOG.md")))
                root = Directory.GetCurrentDirectory();

            List<ChangelogEntry> en = Load(root, "CHANGELOG.md");
            List<ChangelogEntry> ru = Load(root, "CHANGELOG.ru.md");

            Check(Changelog.Split("**NEW** x").Badge == "NEW", "Split of a bold prefix gives its badge");
            Check(Changelog.BadgeOfGroup("Добавлено") == "NEW" && Changelog.BadgeOfGroup("fixed") == "FIX"
                && Changelog.BadgeOfGroup("Other") == "", "BadgeOfGroup maps both languages, unknown gives empty");
            int more;
            List<string> hl = Changelog.Highlights("## Whats new in 1\n### Added\n- a\n- **UPD** b\n### Fixed\r\n- c\r\n---\n- tail\n", "Whats", 0, out more);
            Check(string.Join("|", hl) == "NEW · a|UPD · b|FIX · c", "Highlights: group badge for plain bullets, bold badge wins, stops at ---");
            hl = Changelog.Highlights("## Whats new in 1\n### Added\n- a\n## Other\n- z\n", "Whats", 0, out more);
            Check(hl.Count == 1, "Highlights: stops at the next ## heading");
            hl = Changelog.Highlights("## Что нового в 1\n### Added\n- a\n## Whats new in 1\n- b\n", "Whats", 0, out more);
            Check(string.Join("|", hl) == "b", "Highlights: the group does not leak into another section");
            hl = Changelog.Highlights("## Whats new in 1\n- **FIX** old\n- **NEW** older\n", "Whats", 0, out more);
            Check(string.Join("|", hl) == "FIX · old|NEW · older", "Highlights: old bodies with bold badges still work");
            hl = Changelog.Highlights("## Whats new in 1\n### Added\n- a\n- b\n- c\n", "Whats", 2, out more);
            Check(hl.Count == 2 && more == 1, "Highlights: max caps the result and counts the rest");
            Check(Changelog.Plain("x", "NEW") == "NEW · x" && Changelog.Plain("**FIX** y", "NEW") == "FIX · y"
                && Changelog.Plain("z") == "z", "Plain takes the group badge only when the line has none");
            Edge();

            if (en != null && ru != null)
            {
                Shape(en, "CHANGELOG.md");
                Shape(ru, "CHANGELOG.ru.md");
                Same(en, ru);
                Groups(en, "CHANGELOG.md", new[] { "Added", "Changed", "Deprecated", "Removed", "Fixed", "Security" });
                Groups(ru, "CHANGELOG.ru.md", new[] { "Добавлено", "Изменено", "Устарело", "Удалено", "Исправлено", "Безопасность" });

                foreach (var list in new[] { en, ru })
                {
                    ChangelogEntry e = list.FirstOrDefault(x => x.Version == "0.2.2-dev.1");
                    Check(e != null
                        && e.Lines.Count(l => l.Badge == "NEW") == 5
                        && e.Lines.Count(l => l.Badge == "FIX") == 1
                        && e.Lines.Count(l => l.Badge == "UPD") == 2,
                        "0.2.2-dev.1 has 5 NEW, 1 FIX, 2 UPD");
                }
            }

            if (Failures.Count == 0) { Console.WriteLine("OK"); return 0; }
            foreach (string f in Failures) Console.WriteLine("FAIL: " + f);
            return 1;
        }

        private static string Dump(List<ChangelogEntry> list)
        {
            var sb = new StringBuilder();
            foreach (ChangelogEntry e in list)
            {
                sb.Append("[" + e.Version + "|" + e.Date + "|" + e.Note + "]");
                foreach (ChangelogLine l in e.Lines)
                    sb.Append("{" + l.Group + "|" + l.Badge + "|" + l.Text + "}");
            }
            return sb.ToString();
        }

        // Inputs the real changelogs never contain, fed to Parse directly.
        private static void Edge()
        {
            // empty input
            Check(Changelog.Parse("").Count == 0, "empty text gives no entries");
            Check(Changelog.Parse(null).Count == 0, "null text gives no entries");
            Check(Changelog.Parse("\n\n  \n").Count == 0, "blank text gives no entries");
            Check(Changelog.Parse("# Changelog\n\nSome intro.\n- stray\n").Count == 0, "text with no release heading gives no entries");

            // every group name maps to its badge, in English and Russian, any case
            string[,] map = {
                { "Added", "NEW" }, { "Добавлено", "NEW" }, { "Changed", "UPD" }, { "Изменено", "UPD" },
                { "Fixed", "FIX" }, { "Исправлено", "FIX" }, { "Removed", "DEL" }, { "Удалено", "DEL" },
                { "Deprecated", "DEP" }, { "Устарело", "DEP" }, { "Security", "SEC" }, { "Безопасность", "SEC" },
                { "FIXED", "FIX" } };
            for (int i = 0; i < map.GetLength(0); i++)
            {
                List<ChangelogEntry> r = Changelog.Parse("## [1.0.0] - 2026-01-01\n### " + map[i, 0] + "\n- x\n");
                Check(r.Count == 1 && r[0].Lines.Count == 1 && r[0].Lines[0].Badge == map[i, 1]
                    && r[0].Lines[0].Group == map[i, 0], "group '" + map[i, 0] + "' gives badge " + map[i, 1]);
            }

            // heading and date
            List<ChangelogEntry> one = Changelog.Parse("## [1.2.3-dev.4] - 2026-05-06\n### Added\n- x\n");
            Check(one.Count == 1 && one[0].Version == "1.2.3-dev.4" && one[0].Date == "2026-05-06", "heading gives version and date");
            List<ChangelogEntry> nodate = Changelog.Parse("## [1.0.0]\n### Added\n- x\n");
            Check(nodate.Count == 1 && nodate[0].Version == "1.0.0" && nodate[0].Date == "" && nodate[0].Lines.Count == 1,
                "heading without a date keeps the entry with an empty date");

            // unknown group: kept, empty badge, group name preserved
            List<ChangelogEntry> unk = Changelog.Parse("## [1.0.0] - 2026-01-01\n### Misc\n- x\n### Added\n- y\n");
            Check(unk.Count == 1 && unk[0].Lines.Count == 2 && unk[0].Lines[0].Badge == "" && unk[0].Lines[0].Group == "Misc"
                && unk[0].Lines[1].Badge == "NEW", "unknown group gives an empty badge and does not leak into the next group");

            // a bullet before any group: empty badge and group; a bold badge wins over the group
            List<ChangelogEntry> bare = Changelog.Parse("## [1.0.0] - 2026-01-01\n- bare\n### Added\n- **FIX** bold\n- plain\n");
            Check(bare.Count == 1 && bare[0].Lines.Count == 3, "bullet before a group is kept");
            if (bare.Count == 1 && bare[0].Lines.Count == 3)
            {
                Check(bare[0].Lines[0].Badge == "" && bare[0].Lines[0].Group == "" && bare[0].Lines[0].Text == "bare", "bullet before any group has no badge and no group");
                Check(bare[0].Lines[1].Badge == "FIX" && bare[0].Lines[1].Text == "bold", "bold badge wins over its group");
                Check(bare[0].Lines[2].Badge == "NEW", "plain bullet takes the group badge");
            }

            // a group must not carry over into the next release
            List<ChangelogEntry> carry = Changelog.Parse("## [2.0.0] - 2026-02-01\n### Added\n- a\n## [1.0.0] - 2026-01-01\n- b\n");
            Check(carry.Count == 2 && carry[1].Lines.Count == 1 && carry[1].Lines[0].Badge == "" && carry[1].Lines[0].Group == "",
                "group does not carry over to the next release");

            // a bullet before any release heading is dropped
            Check(Changelog.Parse("### Added\n- orphan\n## [1.0.0] - 2026-01-01\n### Fixed\n- ok\n")[0].Lines.Count == 1, "bullet before the first release is dropped");

            // Unreleased: skipped in any case, with its bullets, and the next release is intact
            foreach (string head in new[] { "## [Unreleased]", "## [unreleased]", "## [UNRELEASED]", "## [Unreleased] - 2026-01-01" })
            {
                List<ChangelogEntry> u = Changelog.Parse(head + "\n### Added\n- hidden\n## [1.0.0] - 2026-01-01\n### Fixed\n- shown\n");
                Check(u.Count == 1 && u[0].Version == "1.0.0" && u[0].Lines.Count == 1 && u[0].Lines[0].Text == "shown",
                    "'" + head + "' is skipped with its bullets");
            }
            Check(Changelog.Parse("## [unreleased]\n").Count == 0, "lone lower-case Unreleased gives no entries");

            // note paragraph before the list
            List<ChangelogEntry> note = Changelog.Parse("## [1.0.0] - 2026-01-01\nFirst release.\n### Added\n- x\n");
            Check(note.Count == 1 && note[0].Note == "First release.", "paragraph before the list becomes the note");

            // CRLF parses as LF, also without a trailing newline
            string lf = "# Changelog\n\n## [Unreleased]\n\n## [1.1.0] - 2026-02-01\n\nNote.\n\n### Added\n\n- **UPD** one\n- two\n\n### Исправлено\n\n- three\n\n## [1.0.0] - 2026-01-01\n### Fixed\n- four";
            string expect = Dump(Changelog.Parse(lf));
            Check(Changelog.Parse(lf).Count == 2, "reference text has 2 releases");
            Check(Dump(Changelog.Parse(lf.Replace("\n", "\r\n"))) == expect, "CRLF text parses the same as LF");
            List<ChangelogEntry> crlf = Changelog.Parse(lf.Replace("\n", "\r\n"));
            Check(crlf.Count == 2 && crlf[0].Version == "1.1.0" && crlf[0].Date == "2026-02-01" && crlf[0].Lines.Count == 3
                && crlf[0].Lines.All(l => !l.Text.Contains("\r") && !l.Group.Contains("\r")), "CRLF leaves no \\r in versions, dates, groups or text");
        }

        private static List<ChangelogEntry> Load(string root, string name)
        {
            string path = Path.Combine(root, name);
            if (!File.Exists(path)) { Failures.Add(name + " is missing"); return null; }
            List<ChangelogEntry> list = Changelog.Parse(File.ReadAllText(path, Encoding.UTF8));
            Check(list.Count > 0, name + " has releases");
            Check(!list.Any(e => e.Version.ToLowerInvariant() == "unreleased"), name + " has no Unreleased entry");
            return list;
        }

        private static void Shape(List<ChangelogEntry> list, string name)
        {
            string prev = null;
            foreach (ChangelogEntry e in list)
            {
                Check(Regex.IsMatch(e.Version, @"^\d+\.\d+\.\d+(-dev\.\d+)?$"), name + ": bad version '" + e.Version + "'");
                Check(Regex.IsMatch(e.Date, @"^\d{4}-\d{2}-\d{2}$"), name + ": bad date '" + e.Date + "' in " + e.Version);
                Check(prev == null || string.CompareOrdinal(e.Date, prev) <= 0, name + ": dates increase at " + e.Version);
                prev = e.Date;
                Check(e.Lines.Count > 0, name + ": " + e.Version + " has no lines");
                foreach (ChangelogLine l in e.Lines)
                    Check(l.Badge.Length > 0, name + ": " + e.Version + " has a line without a badge: " + l.Text);
            }
        }

        private static void Same(List<ChangelogEntry> en, List<ChangelogEntry> ru)
        {
            Check(en.Count == ru.Count, "en and ru have the same number of releases");
            for (int i = 0; i < Math.Min(en.Count, ru.Count); i++)
            {
                Check(en[i].Version == ru[i].Version, "release " + i + ": versions differ");
                Check(en[i].Date == ru[i].Date, en[i].Version + ": dates differ");
                Check(en[i].Lines.Count == ru[i].Lines.Count, en[i].Version + ": line counts differ");
                Check(string.Join(",", en[i].Lines.Select(l => l.Badge))
                      == string.Join(",", ru[i].Lines.Select(l => l.Badge)), en[i].Version + ": badges differ");
            }
        }

        private static void Groups(List<ChangelogEntry> list, string name, string[] allowed)
        {
            foreach (ChangelogEntry e in list)
                foreach (ChangelogLine l in e.Lines)
                    Check(allowed.Contains(l.Group), name + ": " + e.Version + " has group '" + l.Group + "'");
        }
    }
}
