// DIIISPL — a monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DIIISPL
{
    /// <summary>A change line: badge and text apart — the window draws them differently.</summary>
    internal sealed class ChangelogLine
    {
        public string Badge = "";          // NEW, FIX, UPD, DEL, DEP, SEC; empty - no badge
        public string Group = "";          // the "###" heading it sits under; kept for tests/ChangelogCheck.cs, the app never reads it
        public string Text = "";
    }

    /// <summary>One release in the changelog: number, date and change lines.</summary>
    internal sealed class ChangelogEntry
    {
        public string Version = "";
        public string Date = "";
        public string Note = "";           // "First release." and the like — a paragraph before the list
        public List<ChangelogLine> Lines = new List<ChangelogLine>();
    }

    /// <summary>
    /// The changelog kept inside the program.
    ///
    /// `CHANGELOG.ru.md` and `CHANGELOG.md` (English) go into the build as resources — the same way
    /// as the build number is written into `BuildInfo.g.cs`. The reason is simple: until now
    /// there was no way to read "what's new" from inside, except
    /// "something newer is out". A person who has just updated wants to know
    /// what they got — and exactly that case was not covered: the button led
    /// to the browser, to the releases page, where their version may not exist at all.
    ///
    /// Inside the program it always works: without a network, without a GitHub release, and
    /// for a copy built on one's own machine.
    /// </summary>
    internal static class Changelog
    {
        /// <summary>Releases, newest first. Empty — no resource (a build without a changelog).</summary>
        public static List<ChangelogEntry> Read()
        {
            string text = Raw(Text.Code);

            // A language that arrived as a file has no changelog of its own and cannot have one:
            // translating the labels is up to the user, translating the release history is not.
            if (text == null) text = Raw(Lang.Codes[0]);

            return Parse(text);
        }

        /// <summary>
        /// Parse a Keep a Changelog text: "## [0.2.2-dev.1] - 2026-08-27" sections,
        /// each with "### Added" (or "### Добавлено") groups of "- text" lines.
        /// A line without a bold badge takes the badge of its group; "## [Unreleased]" is skipped.
        /// </summary>
        public static List<ChangelogEntry> Parse(string text)
        {
            var list = new List<ChangelogEntry>();
            if (string.IsNullOrEmpty(text)) return list;

            ChangelogEntry current = null;
            string group = "", groupBadge = "";
            foreach (string raw in text.Split((char)10))
            {
                string line = raw.Trim();

                if (line.StartsWith("## "))
                {
                    group = ""; groupBadge = "";
                    Match m = Regex.Match(line.Substring(3).Trim(), @"^\[([^\]]+)\]\s*(?:-\s*(\d{4}-\d{2}-\d{2}))?\s*$");
                    if (!m.Success || m.Groups[1].Value.Equals("Unreleased", StringComparison.OrdinalIgnoreCase))
                    {
                        current = null;
                        continue;
                    }
                    current = new ChangelogEntry();
                    current.Version = m.Groups[1].Value;
                    current.Date = m.Groups[2].Value;
                    list.Add(current);
                    continue;
                }

                if (line.StartsWith("### "))
                {
                    group = line.Substring(4).Trim();
                    groupBadge = BadgeOfGroup(group);
                    continue;
                }

                if (current == null) continue;      // file header and Unreleased
                if (line.Length == 0 || line.StartsWith("#")) continue;

                if (line.StartsWith("- "))
                {
                    ChangelogLine l = Split(line.Substring(2));
                    if (l.Badge.Length == 0) l.Badge = groupBadge;
                    l.Group = group;
                    current.Lines.Add(l);
                }
                else if (current.Lines.Count == 0 && current.Note.Length == 0) current.Note = Plain(line);
            }
            return list;
        }

        /// <summary>Badge of a group (Added/Добавлено is NEW and so on); an unknown group gives "".</summary>
        public static string BadgeOfGroup(string group)
        {
            switch (group.ToLowerInvariant())
            {
                case "added": case "добавлено": return "NEW";
                case "fixed": case "исправлено": return "FIX";
                case "changed": case "изменено": return "UPD";
                case "removed": case "удалено": return "DEL";
                case "deprecated": case "устарело": return "DEP";
                case "security": case "безопасность": return "SEC";
                default: return "";
            }
        }

        /// <summary>
        /// Split a list line into badge and text. "**NEW** Profile applies itself" —
        /// is the badge "NEW" and the rest; asterisks and quotes around commands
        /// are removed, because in the window nothing turns them into formatting.
        /// </summary>
        public static ChangelogLine Split(string bullet)
        {
            var line = new ChangelogLine();
            string s = bullet.Trim();

            if (s.StartsWith("**"))
            {
                int end = s.IndexOf("**", 2);
                if (end > 2)
                {
                    line.Badge = s.Substring(2, end - 2).Trim();
                    s = s.Substring(end + 2).TrimStart();
                }
            }
            line.Text = s.Replace("`", "");
            return line;
        }

        /// <summary>
        /// The same as one line — for places where the badge has nowhere to stand apart:
        /// the hint about a found update and the paragraph before the list. A line without its own
        /// badge takes the group's badge (groupBadge).
        /// </summary>
        public static string Plain(string text, string groupBadge = "")
        {
            ChangelogLine line = Split(text);
            string badge = line.Badge.Length > 0 ? line.Badge : groupBadge;
            return badge.Length > 0 ? badge + " · " + line.Text : line.Text;
        }

        /// <summary>
        /// The list lines of one section of a published release body: the section whose
        /// "## " heading starts with <paramref name="heading"/>, up to the next "## " or "---".
        /// A bullet without a bold badge takes the badge of its "###" group. At most
        /// <paramref name="max"/> lines come back (0 - all); the rest is counted in <paramref name="more"/>.
        /// </summary>
        public static List<string> Highlights(string notes, string heading, int max, out int more)
        {
            more = 0;
            var found = new List<string>();
            if (string.IsNullOrEmpty(notes)) return found;

            bool inside = false;
            string groupBadge = "";                // badge of the current "###" group
            // Split on line feeds; Trim below removes the carriage return.
            foreach (string raw in notes.Split((char)10))
            {
                string line = raw.Trim();
                if (line.StartsWith("## "))
                {
                    if (inside) break;                 // the section is over
                    groupBadge = "";
                    inside = line.Substring(3).TrimStart()
                                 .StartsWith(heading, StringComparison.CurrentCultureIgnoreCase);
                    continue;
                }
                if (!inside) continue;
                if (line.StartsWith("### ")) { groupBadge = BadgeOfGroup(line.Substring(4).Trim()); continue; }
                if (line == "---") break;
                if (!line.StartsWith("- ")) continue;
                found.Add(Plain(line.Substring(2), groupBadge));
            }

            if (max > 0 && found.Count > max)
            {
                more = found.Count - max;
                found.RemoveRange(max, more);
            }
            return found;
        }

        private static string Raw(string code)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("changelog." + code))
                {
                    if (s == null) return null;
                    using (var reader = new StreamReader(s, Encoding.UTF8)) return reader.ReadToEnd();
                }
            }
            catch { return null; }
        }
    }
}
