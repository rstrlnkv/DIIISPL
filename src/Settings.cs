// DIIISPL — a monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DIIISPL
{
    /// <summary>
    /// The program's own settings — those that belong to no profile: the update
    /// channel, the time of the last check, the postponed version. They sit next to the profiles
    /// as plain text, because the profiles are stored that way too: one folder, one format,
    /// and all of it can be read and edited with Notepad.
    /// </summary>
    internal static class Settings
    {
        public const string Stable = "stable";
        public const string Dev = "dev";

        private static Dictionary<string, string> _values;

        /// <summary>
        /// The settings are read and written by more than one thread: the update check goes to
        /// the pool and from there sets the last-check time while the window next to it reads
        /// the channel. Dictionary does not survive that — a read during an append
        /// may miss an existing key, or even loop forever inside.
        /// </summary>
        private static readonly object Lock = new object();

        private static string FilePath
        {
            get { return Path.Combine(Store.DataDir, "settings.ini"); }
        }

        /// <summary>Update channel: Stable or Dev.</summary>
        public static string Channel
        {
            get { string v = Get("channel"); return v == Dev ? Dev : Stable; }
            set { Set("channel", value == Dev ? Dev : Stable); }
        }

        /// <summary>When GitHub was last asked. Empty — never asked.</summary>
        public static DateTime? LastCheck
        {
            get
            {
                DateTime parsed;
                if (DateTime.TryParse(Get("lastcheck"), CultureInfo.InvariantCulture,
                                      DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed))
                    return parsed;
                return null;
            }
            set
            {
                Set("lastcheck", value.HasValue
                    ? value.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
                    : "");
            }
        }

        /// <summary>
        /// Whether the "the program stays in the tray" hint has been shown. Once for all
        /// time: the cross hides the window rather than closing the program, and without a word this
        /// reads as "closed" — then the person either wonders why a "closed"
        /// program switches displays, or looks for the exit and cannot find it.
        /// </summary>
        public static bool TrayHintShown
        {
            get { return Get("trayhint") == "1"; }
            set { Set("trayhint", value ? "1" : ""); }
        }

        /// <summary>
        /// Window language: "auto", "ru" or "en". Default is auto — the Windows language.
        /// Stored here rather than derived from the system every time, because the person's
        /// choice must be remembered: an English Windows is no reason to take Russian away.
        /// </summary>
        public static string Language
        {
            get
            {
                string v = Get("language");
                return string.IsNullOrEmpty(v) ? Text.Auto : v;
            }
            set
            {
                // Validating the code against a list is not possible here: a language file may
                // appear later, and a remembered choice must not vanish because of it.
                // A nonexistent language turns into the fallback on read anyway.
                Set("language", value == Text.Auto ? "" : value);
            }
        }

        /// <summary>
        /// Whether to switch the profile by itself when the set of monitors changes.
        /// On by default: that is what the feature was added for, and
        /// the person should not have to hunt for a setting to get the main thing.
        ///
        /// Stored inverted — the key is written only on refusal. That way the default
        /// lives in one place, in the code, and not also in a file
        /// that an earlier version may have written.
        /// </summary>
        public static bool AutoSwitch
        {
            get { return Get("autoswitch") != "0"; }
            set { Set("autoswitch", value ? "" : "0"); }
        }

        /// <summary>The version that was declined: it is no longer reminded about.</summary>
        public static string SkippedVersion
        {
            get { return Get("skip"); }
            set { Set("skip", value); }
        }

        // ------------------------------------------------------------------ reading and writing
        private static string Get(string key)
        {
            lock (Lock)
            {
                Load();
                string v;
                return _values.TryGetValue(key, out v) ? v : "";
            }
        }

        private static void Set(string key, string value)
        {
            lock (Lock)
            {
                Load();
                if (string.IsNullOrEmpty(value)) _values.Remove(key);
                else _values[key] = value;
                Save();
            }
        }

        /// <summary>
        /// What the file was like when we read it. This pair shows that it
        /// was rewritten behind our back.
        /// </summary>
        private static DateTime _stamp;
        private static long _size = -1;

        private static void Load()
        {
            // Call only under the lock.
            //
            // Re-read if the file on disk has changed. Settings used to
            // be read once per launch, which meant two troubles at once:
            // an edit in Notepad while the program ran was not picked up and
            // was wiped by the very first write, and a second copy — `DIIISPL.exe /apply`
            // from the scheduler or a shortcut — wrote its own, and the window, knowing
            // nothing of it, put the old one back.
            DateTime stamp = DateTime.MinValue;
            long size = -1;
            try
            {
                var info = new FileInfo(FilePath);
                if (info.Exists) { stamp = info.LastWriteTimeUtc; size = info.Length; }
            }
            catch { /* cannot ask — assume unchanged */ }

            if (_values != null && stamp == _stamp && size == _size) return;

            _stamp = stamp;
            _size = size;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(FilePath))
                    foreach (string raw in File.ReadAllLines(FilePath))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("[")) continue;
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                    }
            }
            catch { /* unreadable file — start with empty settings */ }
            _values = values;
        }

        private static void Save()
        {
            try
            {
                var lines = new List<string>();
                lines.Add("# DIIISPL settings");   // service file — English only
                foreach (var kv in _values) lines.Add(kv.Key + " = " + kv.Value);
                File.WriteAllLines(FilePath, lines.ToArray(), System.Text.Encoding.UTF8);

                // Remember the file as we left it: otherwise the very next
                // read would take our own write for someone else's edit.
                var info = new FileInfo(FilePath);
                if (info.Exists) { _stamp = info.LastWriteTimeUtc; _size = info.Length; }
            }
            catch { /* nowhere to write — the settings will live until the program exits */ }
        }
    }
}
