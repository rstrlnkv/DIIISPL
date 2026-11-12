// DIIISPL — a monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DIIISPL
{
    /// <summary>
    /// The pure text logic of the updater's versions: no WPF, no network, so
    /// tests\updater-check.cmd compiles this file alone.
    /// </summary>
    internal static class VersionText
    {
        /// <summary>"v1.0.1", "1.0.1", "1.0.1-beta.2" — we take the numbers, discard the rest.</summary>
        public static Version Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string s = text.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            int cut = s.IndexOfAny(new char[] { '-', '+', ' ' });
            if (cut >= 0) s = s.Substring(0, cut);

            string[] parts = s.Split('.');
            var numbers = new List<int>();
            foreach (string p in parts)
            {
                int n;
                if (!int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out n)) break;
                numbers.Add(n);
            }
            // Whether there are numbers must be asked before padding with zeros, not after:
            // the line used to add four zeros first, and the answer was always
            // "yes". A label like "latest" became version 0.0.0.0 — that is, a
            // release to be compared with ours, not garbage.
            if (numbers.Count == 0) return null;
            while (numbers.Count < 4) numbers.Add(0);
            return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
        }

        /// <summary>
        /// Whether a release is newer than the running program. The -dev.N suffix is dropped by
        /// Parse, so Dev builds of one x.y.z compare equal and are never offered to each other.
        /// </summary>
        public static bool IsNewer(Version candidate, Version current)
        {
            return candidate > current;
        }

        /// <summary>
        /// A ProductVersion for display: the "+build" metadata cut off, trimmed. Null when
        /// nothing is left, so the caller falls back to another text.
        /// </summary>
        public static string Display(string productVersion)
        {
            if (productVersion == null) return null;
            string v = productVersion.Split('+')[0].Trim();
            return v.Length == 0 ? null : v;
        }
    }
}
