// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Collections.Generic;

namespace DIIISPL
{
    /// <summary>
    /// Choosing a profile by the set of screens — "turned the TV on, the layout set itself".
    ///
    /// The decision lives here, apart from whoever carries it out, for one reason:
    /// the program and the `DIIISPL.exe /auto` command must both see it the same way.
    /// A command that retells the decision in its own words will sooner or later
    /// tell something other than what the program will actually do.
    ///
    /// The trigger is the **set**, not an event. Windows sends `WM_DISPLAYCHANGE`
    /// on any movement, including our own layout apply,
    /// and "applied, got an event, applied again" is exactly the dense
    /// queue of rebuilds that sent the machine into a BSOD on 19.08.2026. Applying
    /// a layout does not change the set: the same monitors stay connected. So
    /// there will be no loop even if all other precautions are removed.
    /// </summary>
    internal static class AutoSwitch
    {
        /// <summary>
        /// Who is connected now — the keys of all monitors the system sees,
        /// sorted so that two sets can be compared line by line.
        ///
        /// Connected, not enabled in the layout: a TV switched off over HDMI
        /// disappears from the system completely, and that is the very signal
        /// this is all for. A blanked but connected monitor does not
        /// drop out of the set — its profile must light it up.
        /// </summary>
        public static List<string> Composition(List<MonitorInfo> live)
        {
            var keys = new List<string>();
            foreach (var m in live)
                if (!string.IsNullOrEmpty(m.Identity) && !keys.Contains(m.Identity))
                    keys.Add(m.Identity);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        /// <summary>Whether this is the same set. An empty list and "unknown" are different things, so null is not equal to anything.</summary>
        public static bool Same(List<string> a, List<string> b)
        {
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>
        /// The profile for this set — or null if there is none.
        ///
        /// One is applicable if all of its enabled monitors are connected. Requiring
        /// **exact equality** is not possible, and this was measured on live profiles
        /// on 26.08.2026: the "TV" profile deliberately leaves two screens dark
        /// although nobody unplugged their cables — with exact equality it would
        /// never fire. A profile lists all known monitors and
        /// says which to light; connected but blanked by it is its
        /// intent, not a mismatch.
        ///
        /// Of the applicable ones we take the one that uses more screens: otherwise,
        /// with everything connected, a person would get a one-monitor profile — it
        /// is always applicable. On a tie the first in the list wins: the order
        /// is set by the person, moved with `Alt+↑` and `Alt+↓` and seen with their own eyes.
        /// A rule that is visible in the window needs no explaining.
        /// </summary>
        public static Layout Pick(List<string> composition, List<Layout> profiles)
        {
            if (composition == null || composition.Count == 0 || profiles == null) return null;

            Layout best = null;
            int bestUsed = 0;
            foreach (Layout l in profiles)
            {
                if (!l.Auto) continue;                // excluded by the person — applied by hotkey, never picks itself

                List<string> wanted = Wanted(l);
                if (wanted.Count == 0) continue;      // a profile that blanks everything is never chosen

                bool all = true;
                foreach (string id in wanted)
                    if (!composition.Contains(id)) { all = false; break; }
                if (!all) continue;

                // Strictly greater — so on a tie the one found earlier stays,
                // that is, the first in the list.
                if (wanted.Count > bestUsed) { best = l; bestUsed = wanted.Count; }
            }
            return best;
        }

        /// <summary>Which monitors the profile wants enabled.</summary>
        public static List<string> Wanted(Layout layout)
        {
            var keys = new List<string>();
            foreach (LayoutDisplay d in layout.Displays)
                if (d.Enabled && !string.IsNullOrEmpty(d.Identity) && !keys.Contains(d.Identity))
                    keys.Add(d.Identity);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }
    }
}
