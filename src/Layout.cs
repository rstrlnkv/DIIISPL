// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DIIISPL
{
    /// <summary>One monitor within a profile. Bound by EDID, not by the Windows display number.</summary>
    internal class LayoutDisplay
    {
        public string Identity;
        public string Name;
        public bool Enabled = true;
        public int X, Y;
        public int Width, Height;
        public double Hz;
        public uint Rotation = Native.DISPLAYCONFIG_ROTATION_IDENTITY;
        public bool Primary;
        public int CloneGroup = -1;      // -1 — a separate desktop, otherwise the duplication group number
        public int Hdr = -1;             // -1 leave alone, 0 turn off, 1 turn on

        public LayoutDisplay Clone()
        {
            return (LayoutDisplay)MemberwiseClone();
        }

        public int DesktopWidth
        {
            get { return Rotated ? Height : Width; }
        }

        public int DesktopHeight
        {
            get { return Rotated ? Width : Height; }
        }

        public bool Rotated
        {
            get { return Rotation == Native.DISPLAYCONFIG_ROTATION_ROTATE90 || Rotation == Native.DISPLAYCONFIG_ROTATION_ROTATE270; }
        }
    }

    /// <summary>Layout profile: its own format, independent of the Windows display database.</summary>
    internal class Layout
    {
        public string Name = "";
        public string Hotkey = "";
        public int Order = 0;                 // order in the list, set by the user

        /// <summary>
        /// Whether the profile is fit for automatic selection by the set of screens.
        ///
        /// Fit by default: the feature was introduced for that. But a profile
        /// may also be special — captured as a trial, a debug one, "as it was yesterday" —
        /// and such a one must not kick in by itself just because it matched. By hotkey
        /// it is applied as before: the exception concerns only selection without
        /// a person's involvement.
        /// </summary>
        public bool Auto = true;
        public List<LayoutDisplay> Displays = new List<LayoutDisplay>();

#if DEV
        public TaskbarSettings Taskbar = new TaskbarSettings();   // taskbar look by state
#endif

        /// <summary>
        /// The [taskbar] section as it lies in the file.
        ///
        /// The stable build has no taskbar, yet it must still read and write the profile.
        /// Without this list the very first save would throw out of the file everything
        /// the build does not know — and a person who worked in Dev and then in Stable
        /// would silently lose the taskbar look settings. So the lines are kept as
        /// they are and returned to their place untouched.
        /// </summary>
        private readonly List<string> _taskbarRaw = new List<string>();

        /// <summary>
        /// The old format: a single `taskbar` field right in the [profile] section. The Dev build
        /// parses it and rewrites it the new way, while the stable one does not know it either —
        /// so it must return it to the file as it was. Otherwise a profile written by an old
        /// version loses the taskbar look on the very first save in Stable.
        /// </summary>
        private string _taskbarLegacy;

        /// <summary>
        /// The file the profile was read from. Empty — the profile does not lie anywhere yet.
        ///
        /// The profile name and its file name are different things: the file may have been copied
        /// by hand, and then "Copy.layout" contains "Home". Deletion and renaming,
        /// which derived the path from the name, missed in this case — and deleted someone else's
        /// namesake profile, leaving the selected one in place. Bypassing the recycle bin, no way back.
        /// </summary>
        public string SourcePath;

        /// <summary>
        /// Whether enabled monitors overlap each other. Returns an explanation
        /// or null.
        ///
        /// Lives here, not on the canvas, because two parties ask: the window —
        /// to refuse to apply, and Applier — to refuse to apply what came
        /// around the window: by hotkey, from the tray icon menu, from a hand-edited
        /// file. Windows will not accept such a layout and will arrange the screens its own way.
        /// </summary>
        public string OverlapProblem()
        {
            var on = new List<LayoutDisplay>();
            foreach (var d in Displays) if (d.Enabled) on.Add(d);

            for (int i = 0; i < on.Count; i++)
                for (int j = i + 1; j < on.Count; j++)
                {
                    var a = on[i];
                    var b = on[j];
                    if (a.CloneGroup >= 0 && a.CloneGroup == b.CloneGroup) continue;   // a duplicate lies on top deliberately
                    if (a.X < b.X + b.DesktopWidth && b.X < a.X + a.DesktopWidth &&
                        a.Y < b.Y + b.DesktopHeight && b.Y < a.Y + a.DesktopHeight)
                        return Text.Overlap(a.Name, b.Name);
                }
            return null;
        }

        /// <summary>
        /// Whether the profile describes what is on the screens right now. This is how
        /// the active layout is marked — with a dot in the list and in the tray icon menu.
        ///
        /// The check is strict: a profile that asks for a monitor that is not in
        /// place is not the active one. Otherwise the dot lights at once on
        /// several profiles — "One monitor", "Two" and "Three" together —
        /// because a one-screen layout nests inside each of them.
        ///
        /// HDR is not taken into account: it can be switched bypassing the program, and it is not
        /// part of the layout.
        /// </summary>
        public bool MatchesCurrent(List<MonitorInfo> live)
        {
            return Matches(live, false);
        }

        /// <summary>
        /// Whether applying the profile would change anything at all. Answers a different
        /// question than <see cref="MatchesCurrent"/>, and so looks more leniently:
        /// a monitor that is not in place is skipped by the apply anyway — and
        /// to require its presence here is to require the impossible.
        ///
        /// The quick exit in Applier rests on this. While both were asked
        /// as one question, it never fired if even one monitor
        /// of the profile was disconnected: every press of an already pressed hotkey
        /// rebuilt the displays again.
        /// </summary>
        public bool ChangesNothing(List<MonitorInfo> live)
        {
            return Matches(live, true);
        }

        private bool Matches(List<MonitorInfo> live, bool ignoreAbsent)
        {
            var active = new Dictionary<string, MonitorInfo>();
            foreach (var m in live) if (m.Active) active[m.Identity] = m;

            var known = new Dictionary<string, MonitorInfo>();
            foreach (var m in live) known[m.Identity] = m;

            var present = new List<LayoutDisplay>();
            int wanted = 0;
            foreach (var d in Displays)
            {
                if (!d.Enabled) continue;
                wanted++;

                MonitorInfo mon;

                // A pulled-out monitor drops out of the check: it cannot be enabled, and
                // the apply will skip it.
                if (!known.TryGetValue(d.Identity, out mon)) continue;

                // But a connected, blanked one never drops out.
                // These are different things, and confusing them is costly: a TV in sleep mode,
                // a monitor with its power off, an output that Windows blanked
                // after a failed attempt — the profile must light them. While both
                // cases were filtered out together, a hotkey press quietly
                // reported "done" and did nothing: it never
                // got as far as the attempt ladder, which is exactly what can
                // wake blanked outputs.
                if (!mon.Active) return false;

                present.Add(d);
            }

            // The strict question — "is this what is on the screens" — requires all the
            // ordered monitors to be in place. The lenient one — "will applying change anything" —
            // does not: the apply will skip an absent monitor
            // anyway.
            if (!ignoreAbsent && wanted != present.Count) return false;
            if (present.Count == 0 || present.Count != active.Count) return false;

            var groupSource = new Dictionary<int, uint>();
            var soloSources = new List<uint>();

            foreach (var d in present)
            {
                MonitorInfo mon;
                if (!active.TryGetValue(d.Identity, out mon)) return false;
                if (mon.Width != d.Width || mon.Height != d.Height) return false;
                if (mon.Rotation != d.Rotation) return false;
                if (d.Hz > 0 && Math.Abs(mon.Hz - d.Hz) > 1.5) return false;

                if (d.CloneGroup >= 0)
                {
                    uint src;
                    if (!groupSource.TryGetValue(d.CloneGroup, out src)) groupSource[d.CloneGroup] = mon.SourceId;
                    else if (src != mon.SourceId) return false;
                }
                else
                {
                    if (soloSources.Contains(mon.SourceId)) return false;
                    soloSources.Add(mon.SourceId);
                }
            }

            // The primary screen must match: without this check a layout of two
            // identical monitors would count as matching even with the primary swapped —
            // positions are compared relative to it itself.
            //
            // But the comparison must be per group, not per output. "Primary" here
            // means "origin of coordinates", and Windows marks **every** output standing
            // at (0,0) with it — in a duplicate that is both at once. The profile stores exactly one:
            // FromCurrent normalises, and the "Primary" switch in the window sets one too.
            // While the comparison went per output, a profile with a duplicate at the origin
            // never matched the live picture — and so the quick exit above did not work,
            // and a repeated hotkey press rebuilt the displays again each time,
            // blinking the screen and jerking HDR.
            LayoutDisplay primary = null;
            foreach (var d in present) if (d.Primary) { primary = d; break; }

            if (primary != null)
            {
                if (!active[primary.Identity].Primary) return false;

                // Extra "primaries" are allowed only as companions of the same duplicate group.
                foreach (var d in present)
                {
                    if (ReferenceEquals(d, primary)) continue;
                    if (!active[d.Identity].Primary) continue;
                    if (d.CloneGroup < 0 || d.CloneGroup != primary.CloneGroup) return false;
                }
            }

            // we compare positions relative to the primary screen — the shift itself does not matter
            LayoutDisplay origin = null;
            foreach (var d in present) if (d.Primary) { origin = d; break; }
            if (origin == null) foreach (var d in present) { origin = d; break; }
            if (origin == null) return false;

            MonitorInfo originMon = active[origin.Identity];

            // The origin must stand at (0,0) — Submit will put it there.
            //
            // Without this check the comparison was purely relative and did not see a common
            // shift at all. Measured: a profile in which a currently pulled-out
            // TV is marked primary counted as "nothing to apply", although applying
            // would have moved the origin to another monitor — that is, the taskbar
            // and "Start" would have moved to the neighbouring screen.
            if (originMon.X != 0 || originMon.Y != 0) return false;

            int dx = originMon.X - origin.X, dy = originMon.Y - origin.Y;
            foreach (var d in present)
            {
                MonitorInfo mon = active[d.Identity];
                if (mon.X != d.X + dx || mon.Y != d.Y + dy) return false;
            }
            return true;
        }

        /// <summary>
        /// Take over from the old profile everything that is not the layout: the key
        /// combination, the place in the list and the taskbar look. Needed when re-capturing a layout
        /// over an existing profile — the layout is new, the rest is never foreign.
        /// </summary>
        public void AdoptSettingsFrom(Layout old)
        {
            Hotkey = old.Hotkey;
            Auto = old.Auto;
            Order = old.Order;
#if DEV
            Taskbar = old.Taskbar.Copy();
#endif
            _taskbarRaw.Clear();
            _taskbarRaw.AddRange(old._taskbarRaw);
            _taskbarLegacy = old._taskbarLegacy;

            // And the file the profile lies in: it too is "not the layout". Without this
            // re-capturing a profile that came from a file with a different name inside
            // bred a double.
            SourcePath = old.SourcePath;
        }

        /// <summary>
        /// Return the contents to what lies in another profile. The object itself stays
        /// the same: the profile list refers to it, and swapping the reference
        /// would make the list rebuild itself in the middle of a switch.
        /// </summary>
        public void RestoreFrom(Layout src)
        {
            Name = src.Name;
            Hotkey = src.Hotkey;
            Auto = src.Auto;
            Order = src.Order;

            Displays.Clear();
            foreach (var d in src.Displays) Displays.Add(d.Clone());

#if DEV
            Taskbar = src.Taskbar.Copy();
#endif
            _taskbarRaw.Clear();
            _taskbarRaw.AddRange(src._taskbarRaw);
            _taskbarLegacy = src._taskbarLegacy;
        }

        public LayoutDisplay Find(string identity)
        {
            foreach (var d in Displays) if (d.Identity == identity) return d;
            return null;
        }

        public Layout DeepCopy()
        {
            var copy = new Layout();
            copy.Name = Name;
            copy.Hotkey = Hotkey;
            copy.Auto = Auto;
            copy.Order = Order;
#if DEV
            copy.Taskbar = Taskbar.Copy();
#endif
            copy._taskbarRaw.AddRange(_taskbarRaw);
            copy._taskbarLegacy = _taskbarLegacy;
            copy.SourcePath = SourcePath;
            foreach (var d in Displays) copy.Displays.Add(d.Clone());
            return copy;
        }

        /// <summary>Build a profile from what is enabled right now.</summary>
        public static Layout FromCurrent(string name)
        {
            var layout = new Layout();
            layout.Name = name;

            var monitors = Monitors.Enumerate();
            // we determine duplication groups: the same source id = one desktop on several outputs
            var groupBySource = new Dictionary<string, int>();
            int nextGroup = 0;

            foreach (var m in monitors)
            {
                var d = new LayoutDisplay();
                d.Identity = m.Identity;
                d.Name = m.Name;
                d.Enabled = m.Active;
                d.X = m.X; d.Y = m.Y;
                d.Hz = m.Hz;
                d.Rotation = m.Rotation;
                d.Primary = m.Active && m.X == 0 && m.Y == 0;
                d.Hdr = m.HdrSupported ? (m.HdrEnabled ? 1 : 0) : -1;

                // CCD gives the source mode already in the panel's "native" orientation — no need to swap the sides
                d.Width = m.Width;
                d.Height = m.Height;

                if (!m.Active)
                {
                    d.Width = m.PreferredWidth > 0 ? m.PreferredWidth : 1920;
                    d.Height = m.PreferredHeight > 0 ? m.PreferredHeight : 1080;
                    d.Hz = m.PreferredHz > 0 ? m.PreferredHz : 60;
                }

                if (m.Active)
                {
                    // the same source id on several outputs = duplication
                    string key = m.Adapter.Key + "/" + m.SourceId;
                    int g;
                    if (!groupBySource.TryGetValue(key, out g))
                    {
                        g = nextGroup++;
                        groupBySource[key] = g;
                    }
                    d.CloneGroup = g;
                }
                layout.Displays.Add(d);
            }

            layout.DropLoneCloneGroups();
            // exactly one primary: with duplication several outputs stand at (0,0) at once
            bool primarySeen = false;
            foreach (var d in layout.Displays)
            {
                if (!d.Primary) continue;
                if (primarySeen) d.Primary = false;
                else primarySeen = true;
            }

            // clones inherit the group's position
            AssignCloneSourceIds(layout);
            return layout;
        }

        private static void AssignCloneSourceIds(Layout layout)
        {
            var groups = new Dictionary<int, LayoutDisplay>();
            foreach (var d in layout.Displays)
            {
                if (d.CloneGroup < 0) continue;
                LayoutDisplay first;
                if (groups.TryGetValue(d.CloneGroup, out first)) { d.X = first.X; d.Y = first.Y; }
                else groups[d.CloneGroup] = d;
            }
        }

        // ---------------- profile file ----------------
        public void Save(string file)
        {
            var sb = new StringBuilder();
            // The program writes service files in English only: keys, sections and
            // the header do not depend on the window language, and a profile carried to a machine
            // with another language is read there the same way.
            sb.AppendLine("# DIIISPL monitor layout profile");
            sb.AppendLine("[profile]");
            sb.AppendLine("name = " + Name);
            sb.AppendLine("hotkey = " + (Hotkey ?? ""));
            sb.AppendLine("order = " + Order.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("auto = " + (Auto ? "1" : "0"));
#if !DEV
            // We put the old-format field back as it was: the stable build
            // cannot parse it, and throwing out foreign data is not its business.
            if (!string.IsNullOrEmpty(_taskbarLegacy)) sb.AppendLine("taskbar = " + _taskbarLegacy);
#endif
            sb.AppendLine();
#if DEV
            sb.AppendLine("[taskbar]");
            foreach (string line in Taskbar.Serialize()) sb.AppendLine(line);
#else
            // The stable build knows nothing about the taskbar and so decides
            // nothing about it: it returns the section to the file exactly as it read it.
            if (_taskbarRaw.Count > 0)
            {
                sb.AppendLine("[taskbar]");
                foreach (string line in _taskbarRaw) sb.AppendLine(line);
            }
#endif
            foreach (var d in Displays)
            {
                sb.AppendLine();
                sb.AppendLine("[display]");
                sb.AppendLine("id = " + d.Identity);
                sb.AppendLine("name = " + (d.Name ?? ""));
                sb.AppendLine("enabled = " + (d.Enabled ? "1" : "0"));
                sb.AppendLine("x = " + d.X.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("y = " + d.Y.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("width = " + d.Width.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("height = " + d.Height.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("hz = " + d.Hz.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("rotation = " + d.Rotation.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("primary = " + (d.Primary ? "1" : "0"));
                sb.AppendLine("clone = " + d.CloneGroup.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("hdr = " + d.Hdr.ToString(CultureInfo.InvariantCulture));
            }
            WriteWhole(file, sb.ToString());
        }

        /// <summary>
        /// Write the file whole or do not touch it at all.
        ///
        /// `File.WriteAllText` first truncates the file, then writes. If in the middle of the
        /// write the power goes or the program is killed, half a profile stays on disk
        /// — and `Layout.Load` does not fail on a fragment, it just
        /// returns a layout without some monitors, and `Store.LoadAll` shows it as
        /// healthy. We write alongside and swap in one move: Replace on NTFS
        /// either goes through whole or does not go through.
        /// </summary>
        private static void WriteWhole(string file, string text)
        {
            var encoding = new UTF8Encoding(true);
            if (!File.Exists(file)) { File.WriteAllText(file, text, encoding); return; }

            string temp = file + ".new";
            try
            {
                try
                {
                    File.WriteAllText(temp, text, encoding);
                    // Without a backup copy: it would be left lying next to the profiles.
                    // Replace without it is an ordinary swap of a directory entry.
                    File.Replace(temp, file, null);
                }
                catch (IOException)
                {
                    // Replace does not work across a volume boundary and on network drives, where
                    // the profiles folder may well turn out to be. Then we write as before:
                    // worse, but better than not saving.
                    File.WriteAllText(file, text, encoding);
                }
                catch (UnauthorizedAccessException)
                {
                    File.WriteAllText(file, text, encoding);
                }
            }
            finally
            {
                // Cleanup must be here, not in the catch branches. Previously it stood
                // after the fallback write — and on a read-only file it did not
                // run at all: the fallback write threw the same
                // exception, and `.new` stayed lying there forever. That is, cleanup
                // did not work exactly on the failure for which catch was written.
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch { }
            }
        }

        public static Layout Load(string file)
        {
            var layout = new Layout();
            LayoutDisplay cur = null;
            string section = "";

            foreach (string raw in File.ReadAllLines(file, Encoding.UTF8))
            {
                string line = raw.Trim();

                // The taskbar section is remembered whole, verbatim and before everything
                // else: a build that does not understand it must return everything to the file,
                // not only what looks like the familiar "key = value".
                //
                // "Before everything" is no trifle. Previously the capture stood below the skipping of
                // comments and blank lines, and it was measured like this: the notes with which
                // a person labelled the section in Dev vanished after the first save in
                // Stable, and the indents were eaten. We remember `raw`, not the
                // trimmed one: there is no other way to restore the indent.
                if (line.StartsWith("["))
                {
                    string name = line.Trim('[', ']').ToLowerInvariant();
                    bool known = name == "profile" || name == "display" || name == "taskbar";

                    // An unfamiliar line in brackets inside the taskbar section is
                    // part of it, not a new section.
                    //
                    // Previously such a line did two things at once: it did not get into the list itself and it switched parsing
                    // to an invented section — that is, it carried the whole tail of the section away,
                    // silently. The promise is precisely that the stable build knows
                    // nothing about the format of this section: any subsection that Dev introduces must
                    // return to the file whole.
                    if (!known && section == "taskbar") { layout._taskbarRaw.Add(raw.TrimEnd()); continue; }

                    section = name;
                    if (section == "display") { cur = new LayoutDisplay(); layout.Displays.Add(cur); }
                    continue;
                }

                if (section == "taskbar") layout._taskbarRaw.Add(raw.TrimEnd());
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string val = line.Substring(eq + 1).Trim();

                if (section == "profile")
                {
                    if (key == "name") layout.Name = val;
                    else if (key == "hotkey") layout.Hotkey = val;
                    else if (key == "order") layout.Order = ParseInt(val);
                    // No field — so the file was written by an old version, and the profile
                    // takes part: before this feature, all did.
                    else if (key == "auto") layout.Auto = val != "0";
                    // old format: one field for the whole profile
#if DEV
                    else if (key == "taskbar") layout.Taskbar.AdoptLegacy(val);
#else
                    else if (key == "taskbar") layout._taskbarLegacy = val;
#endif
                }
                else if (section == "taskbar")
                {
#if DEV
                    TaskbarState state;
                    if (key == "version") layout.Taskbar.Versioned = true;
                    else if (TaskbarSettings.TryParseKey(key, out state))
                        layout.Taskbar.States[state] = TaskbarAppearance.Parse(val);
#endif
                }
                else if (section == "display" && cur != null)
                {
                    switch (key)
                    {
                        case "id": cur.Identity = val; break;
                        case "name": cur.Name = val; break;
                        case "enabled": cur.Enabled = val == "1"; break;
                        case "x": cur.X = ParseInt(val); break;
                        case "y": cur.Y = ParseInt(val); break;
                        case "width": cur.Width = ParseInt(val); break;
                        case "height": cur.Height = ParseInt(val); break;
                        case "hz": cur.Hz = ParseDouble(val); break;
                        case "rotation": cur.Rotation = (uint)Math.Max(1, ParseInt(val)); break;
                        case "primary": cur.Primary = val == "1"; break;
                        case "clone": cur.CloneGroup = ParseInt(val, -1); break;
                        case "hdr": cur.Hdr = ParseInt(val, -1); break;
                    }
                }
            }
            if (string.IsNullOrEmpty(layout.Name)) layout.Name = Path.GetFileNameWithoutExtension(file);
            layout.SourcePath = file;

            // We trim trailing blank lines of the section. A blank line before the next
            // section belongs not to the section but to the file layout: Save puts its own
            // before every [display], and the remembered one piled up on top of it. Measured:
            // the file gained a line on every save — and it saves both on
            // the button, and inside "Apply", and on reordering in the list. Inner
            // blank lines are kept: a person separates their notes with them.
            while (layout._taskbarRaw.Count > 0 &&
                   layout._taskbarRaw[layout._taskbarRaw.Count - 1].Length == 0)
                layout._taskbarRaw.RemoveAt(layout._taskbarRaw.Count - 1);

            layout.Normalize();
            layout.DropLoneCloneGroups();
#if DEV
            if (!layout.Taskbar.Versioned) layout.Taskbar.DropUntouchedSpecialCases();
#endif
            return layout;
        }

        /// <summary>
        /// Bring the values to those that can exist at all.
        ///
        /// Profiles are plain text, and the README directly invites editing them in Notepad.
        /// So typos get into the file: "rotation = 7", "hz = -60", "width =
        /// 0", two sections with the same monitor. The program survived these
        /// differently: rotation became impossible, and Windows rejected
        /// the whole layout; a negative frequency went into the driver request;
        /// a second section with the same monitor gave two different states of one
        /// screen, and which won depended on the enumeration order.
        ///
        /// We fix silently and toward "as if not set": a person who edited the file
        /// by hand will see the honest state in the window and correct it there.
        /// </summary>
        private void Normalize()
        {
            var seen = new Dictionary<string, LayoutDisplay>(StringComparer.OrdinalIgnoreCase);
            var keep = new List<LayoutDisplay>();

            foreach (LayoutDisplay d in Displays)
            {
                // Rotation comes in only four kinds; everything else is "as usual".
                if (d.Rotation < 1 || d.Rotation > 4) d.Rotation = 1;

                // Size and frequency: zero means "not set", and Applier will substitute
                // what the monitor can do. A negative value means nothing.
                if (d.Width < 0) d.Width = 0;
                if (d.Height < 0) d.Height = 0;
                if (d.Hz < 0 || d.Hz > 1000) d.Hz = 0;

                // HDR: "leave alone", "turn off", "turn on".
                if (d.Hdr < -1 || d.Hdr > 1) d.Hdr = -1;

                // Duplication group number — either -1 or non-negative.
                if (d.CloneGroup < -1) d.CloneGroup = -1;

                if (string.IsNullOrEmpty(d.Identity)) continue;

                // One monitor — one state. The last wins: this way a hand edit
                // works as usual, what is appended below is in force.
                LayoutDisplay before;
                if (seen.TryGetValue(d.Identity, out before)) keep.Remove(before);
                seen[d.Identity] = d;
                keep.Add(d);
            }

            if (keep.Count != Displays.Count)
            {
                Displays.Clear();
                Displays.AddRange(keep);
            }
        }

        /// <summary>
        /// A duplication group of one monitor is not duplication.
        ///
        /// This remains in profiles written by old versions: an output dropped out
        /// of the group while the neighbour kept the number, and the lone monitor then walked all its life
        /// with a "Duplicate" label. It is cleaned both on reading the file and on capturing a layout
        /// from the system — otherwise a single old profile is enough for the label to return.
        /// </summary>
        public void DropLoneCloneGroups()
        {
            foreach (var d in Displays) if (!d.Enabled) d.CloneGroup = -1;

            var counts = new Dictionary<int, int>();
            foreach (var d in Displays)
            {
                if (d.CloneGroup < 0) continue;
                int c; counts.TryGetValue(d.CloneGroup, out c); counts[d.CloneGroup] = c + 1;
            }
            foreach (var d in Displays)
                if (d.CloneGroup >= 0 && counts[d.CloneGroup] < 2) d.CloneGroup = -1;
        }

        private static int ParseInt(string s) { return ParseInt(s, 0); }

        private static int ParseInt(string s, int def)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : def;
        }

        private static double ParseDouble(string s)
        {
            double v;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0;
        }
    }
}
