// DIIISPL — a monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DIIISPL
{
    /// <summary>
    /// Translations from files: a language can be added without rebuilding the program.
    ///
    /// The built-in dictionaries remain the basis; a file only supplements and overrides them.
    /// So a translation that lacks something leaves no blank spots in the window,
    /// and whoever wants to fix one string writes one string in a file.
    ///
    /// Looked up in two places, and the order matters:
    ///   1) `lang` next to the program — translations shipped with it go here;
    ///   2) `%AppData%\DIIISPL\lang` — written by a person without rights to
    ///      Program Files. Their file wins: it is their machine and their choice.
    ///
    /// The format is the same as for profiles and settings: `key = value` lines,
    /// a hash and a semicolon start a comment. Encoding is UTF-8; a BOM is not
    /// required, but without it non-English letters must be saved as UTF-8
    /// and not in the system code page.
    /// </summary>
    internal static class LangFile
    {
        public const string FolderName = "lang";
        public const string Extension = ".lang";

        /// <summary>
        /// Whether a string is acceptable as a language code. Rejects not only characters
        /// forbidden in file names but also device names: `/lang nul` cheerfully answered "sample
        /// written" while the write went nowhere and no file appeared.
        /// </summary>
        public static bool IsUsableCode(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length > 32) return false;
            if (code.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            if (code.Trim('.', ' ').Length == 0) return false;
            if (code == Text.Auto) return false;

            foreach (string device in Devices)
                if (string.Equals(code, device, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static readonly string[] Devices =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>Overlay a language file onto a ready dictionary. No file — the dictionary stays unchanged.</summary>
        public static void Overlay(string code, Dictionary<string, string> table)
        {
            foreach (string path in PathsFor(code))
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    ReadInto(path, table);
                }
                catch { /* unreadable file — the built-in translation stays */ }
            }
        }

        /// <summary>
        /// Which languages exist as files. The picker window needs them: a language that is not
        /// in the built-in list has nowhere else to come from.
        /// </summary>
        public static List<string> Codes()
        {
            var found = new List<string>();
            foreach (string dir in Folders())
            {
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (string f in Directory.GetFiles(dir, "*" + Extension))
                    {
                        string code = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                        if (code.Length == 0 || found.Contains(code)) continue;

                        // "auto" in the settings means "as in Windows". A file with such a
                        // name would become the language for everyone who chose nothing.
                        if (code == Text.Auto) continue;

                        found.Add(code);
                    }
                }
                catch { }
            }
            found.Sort(StringComparer.Ordinal);
            return found;
        }

        /// <summary>
        /// Name of the language from a file — the `language.name` line. The language labels
        /// itself with it; without it the picker shows only the code.
        /// </summary>
        public static string NameOf(string code)
        {
            var one = new Dictionary<string, string>(StringComparer.Ordinal);
            Overlay(code, one);
            string name;
            return one.TryGetValue("language.name", out name) && name.Length > 0 ? name : code;
        }

        /// <summary>
        /// Write a sample for translation: all keys with English values and
        /// a note on what is missing where. Without such a sample a person knows neither
        /// the list of keys nor that anything can be translated at all.
        /// </summary>
        public static string WriteTemplate(string code)
        {
            var full = Lang.Table(Lang.Codes[0]);
            var mine = Lang.Table(code);
            LangFile.Overlay(code, mine);

            // Forms after a number do not exist in all languages at once: ".few" lives only
            // in Russian. Build the sample from English alone — and a Polish
            // translator would never learn about the third form.
            //
            // The value taken is the English one, from the ".many" form of the same key:
            // a Russian string in a Czech sample would confuse no less than
            // a missing string altogether.
            foreach (string other in Lang.Codes)
            {
                if (other == Lang.Codes[0]) continue;
                foreach (var kv in Lang.Table(other))
                {
                    if (full.ContainsKey(kv.Key)) continue;

                    string english;
                    int dot = kv.Key.LastIndexOf('.');
                    if (dot > 0 && full.TryGetValue(kv.Key.Substring(0, dot) + ".many", out english))
                        full[kv.Key] = english;
                }
            }

            var keys = new List<string>(full.Keys);
            keys.Sort(StringComparer.Ordinal);

            var sb = new StringBuilder();
            sb.AppendLine("# DIIISPL translation file");
            sb.AppendLine("# key = text.  Lines starting with # or ; are comments.");
            sb.AppendLine("# {0}, {1} … are values the program puts in — keep them, reorder if the language needs it.");
            sb.AppendLine("# For a literal curly brace write it twice: {{ }}.");
            sb.AppendLine("# Use \\n where the text has to break a line, and \\\\n for a backslash before «n».");
            sb.AppendLine("# A space right after «=» belongs to the format; further spaces are part of the text.");
            sb.AppendLine("# A line still in English has not been translated yet.");
            sb.AppendLine("# Save as UTF-8.");
            sb.AppendLine();
            sb.AppendLine("# The name of the language in the language itself — this is how the About window lists it.");
            sb.AppendLine("language.name = " + Text.NameOf(code));
            sb.AppendLine();

            string section = null;
            foreach (string key in keys)
            {
                int dot = key.IndexOf('.');
                string group = dot > 0 ? key.Substring(0, dot) : key;
                if (group != section) { sb.AppendLine(); section = group; }

                string mineValue;
                bool have = mine.TryGetValue(key, out mineValue) && mineValue != full[key];

                // Show the original only above a translated string: there it is
                // needed for checking. Above an untranslated one it would repeat it word
                // for word and turn the file into a double list.
                if (have) sb.AppendLine("# " + Escape(full[key]));
                sb.AppendLine(key + " = " + Escape(have ? mineValue : full[key]));
            }

            string dir = Path.Combine(Store.DataDir, FolderName);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, code + Extension);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            return path;
        }

        /// <summary>
        /// Newline back to "\n". The format string is single-line: a real
        /// newline in the sample would tear the value in two, and the second half
        /// would be lost on reading.
        ///
        /// A backslash before "n" is doubled — otherwise a path like `C:\notes` after
        /// a round trip through the sample would silently turn into two lines.
        /// </summary>
        private static string Escape(string value)
        {
            return value.Replace("\\n", "\\\\n")
                        .Replace("\r\n", "\\n")
                        .Replace("\n", "\\n");
        }

        /// <summary>Back: "\\n" is two letters, "\n" is a line break.</summary>
        private static string Unescape(string value)
        {
            const string keep = "\u0001";     // marker for the duration of parsing; never occurs in texts
            return value.Replace("\\\\n", keep)
                        .Replace("\\n", "\n")
                        .Replace(keep, "\\n");
        }

        // ------------------------------------------------------------------ paths
        private static IEnumerable<string> Folders()
        {
            string beside = null;
            try
            {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(exe)) beside = Path.Combine(Path.GetDirectoryName(exe), FolderName);
            }
            catch { }
            if (beside != null) yield return beside;

            string mine = null;
            try { mine = Path.Combine(Store.DataDir, FolderName); }
            catch { }
            if (mine != null) yield return mine;
        }

        private static IEnumerable<string> PathsFor(string code)
        {
            foreach (string dir in Folders()) yield return Path.Combine(dir, code + Extension);
        }

        private static void ReadInto(string path, Dictionary<string, string> table)
        {
            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = raw.TrimEnd('\r', '\n');
                string probe = line.Trim();
                if (probe.Length == 0 || probe.StartsWith("#") || probe.StartsWith(";")) continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();
                if (key.Length == 0) continue;

                // The value must not be trimmed: for about eighty strings a space at the edge
                // carries meaning. The whole command-line help is aligned with them, and with them
                // tails such as " 2 more notes" are attached — trimming
                // would glue words together. Exactly one space is removed — the one
                // written around the equals sign.
                string value = line.Substring(eq + 1);
                if (value.Length > 0 && value[0] == ' ') value = value.Substring(1);

                // An erased value is not an empty label but "translate this". Erasing
                // the text after "=" is easier than deleting the line, and a person is entitled
                // to expect the program to take its own.
                if (value.Length == 0) continue;

                // "\n" in a file is a line break: format strings are single-line, but in
                // a couple of messages a break is needed by meaning.
                table[key] = Unescape(value);
            }
        }
    }
}
