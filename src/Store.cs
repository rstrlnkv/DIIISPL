// DIIISPL — a monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.IO;

namespace DIIISPL
{
    internal static class Store
    {
        public const string AppFolder = "DIIISPL";

        /// <summary>What the program was called before — profiles are migrated from there.</summary>
        private const string OldAppFolder = "DIIIX";

        private static bool _adoptTried;

        public static string DataDir
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string d = Path.Combine(root, AppFolder);
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);

                // The migration is tried once per launch, not "once in a lifetime". It used to
                // be keyed on the absence of the new folder — yet that was created by the line
                // above, before the migration: one busy copy broke the loop, and there was
                // nothing left to retry with. Measured: of four profiles one was migrated,
                // and a second launch added nothing.
                if (!_adoptTried)
                {
                    _adoptTried = true;
                    AdoptOldFolder(Path.Combine(root, OldAppFolder), d);
                }
                return d;
            }
        }

        /// <summary>
        /// Take the profiles from the folder with the former name. We copy rather than move: if something
        /// goes wrong, the former files stay in place and there is nothing to lose.
        /// Done once — as soon as the first profile appears in the new folder.
        /// </summary>
        private static void AdoptOldFolder(string from, string to)
        {
            try
            {
                if (!Directory.Exists(from)) return;

                // Already migrated — no need a second time.
                string here = Path.Combine(to, "profiles");
                if (Directory.Exists(here) && Directory.GetFiles(here, "*.layout").Length > 0) return;

                foreach (string dir in Directory.GetDirectories(from))
                {
                    string target = Path.Combine(to, Path.GetFileName(dir));
                    if (!Directory.Exists(target)) Directory.CreateDirectory(target);
                    foreach (string file in Directory.GetFiles(dir))
                    {
                        // Catch around each file, not around the whole migration:
                        // one busy file must not take the rest down with it.
                        try { File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true); }
                        catch { }
                    }
                }
                foreach (string file in Directory.GetFiles(from))
                {
                    // no need to migrate the previous version's error log
                    if (string.Equals(Path.GetFileName(file), "error.log", StringComparison.OrdinalIgnoreCase)) continue;
                    try { File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true); }
                    catch { }
                }
            }
            catch { /* could not — the former files stayed in place, they can be restored by hand */ }
        }

        public static string ProfilesDir
        {
            get
            {
                string d = Path.Combine(DataDir, "profiles");
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
        }

        public static string PathFor(string name) { return Path.Combine(ProfilesDir, Sanitize(name) + ".layout"); }

        public static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "profile";
            var chars = name.ToCharArray();
            foreach (char bad in Path.GetInvalidFileNameChars())
                for (int i = 0; i < chars.Length; i++) if (chars[i] == bad) chars[i] = '_';
            string s = new string(chars).Trim();
            if (s.Length == 0) return "profile";

            // DOS device names are steered aside — but not because "CON.layout"
            // cannot be created: measured, it can. Windows 11 serves such a file as
            // an ordinary one, it reads and shows up in enumeration. The reason is simpler: almost
            // everything that works with files avoids these names — archivers,
            // cloud folders, old utilities — and a profile named "CON" risks
            // not surviving a copy to another machine. The profile name inside the file
            // stays what the person chose.
            if (IsReservedName(s)) return "_" + s;
            return s;
        }

        private static readonly string[] ReservedNames =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        private static bool IsReservedName(string s)
        {
            foreach (string bad in ReservedNames)
                if (string.Equals(s, bad, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Whether the name is taken by another profile. Returns its name or null.
        ///
        /// We compare not only names but the files they would land in: "Home/Work"
        /// and "Home_Work" are different names and one and the same "Home_Work.layout".
        /// Saving the second silently overwrote the first.
        /// </summary>
        public static string NameInUse(string name, string exceptName)
        {
            return NameInUse(LoadAll(), name, exceptName);
        }

        private static string NameInUse(List<Layout> all, string name, string exceptName)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string file = Sanitize(name);
            foreach (var l in all)
            {
                if (exceptName != null &&
                    string.Equals(l.Name, exceptName, StringComparison.CurrentCultureIgnoreCase)) continue;
                if (string.Equals(l.Name, name, StringComparison.CurrentCultureIgnoreCase)) return l.Name;
                if (string.Equals(Sanitize(l.Name), file, StringComparison.OrdinalIgnoreCase)) return l.Name;
            }

            // The name inside the file and the name of the file itself are different things, and profiles are meant
            // to be portable: the file may have been copied by hand, and then "Home.layout"
            // holds the name "Work". Checking only by names from inside does not see this,
            // while Save will still land in "Home.layout" and overwrite it. So we ask
            // the directory too.
            try
            {
                string target = PathFor(name);
                if (File.Exists(target))
                {
                    if (exceptName == null) return Path.GetFileNameWithoutExtension(target);
                    if (!string.Equals(target, PathFor(exceptName), StringComparison.OrdinalIgnoreCase))
                        return Path.GetFileNameWithoutExtension(target);
                }
            }
            catch { /* directory unavailable — then collisions cannot be checked either */ }
            return null;
        }

        public static List<Layout> LoadAll()
        {
            var list = new List<Layout>();
            foreach (string f in Directory.GetFiles(ProfilesDir, "*.layout"))
            {
                try { list.Add(Layout.Load(f)); }
                catch { /* a broken file is simply not shown */ }
            }
            // the order is set by the user; profiles without an order (old files) follow alphabetically
            list.Sort(delegate(Layout a, Layout b)
            {
                if (a.Order != b.Order) return a.Order.CompareTo(b.Order);
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            return list;
        }

        /// <summary>Number the profiles by their current position in the list and write to disk.</summary>
        public static void SaveOrder(List<Layout> ordered)
        {
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Order == i) continue;
                ordered[i].Order = i;
                Save(ordered[i]);
            }
        }

        public static Layout LoadByName(string name)
        {
            foreach (var l in LoadAll())
                if (string.Equals(l.Name, name, StringComparison.CurrentCultureIgnoreCase)) return l;
            return null;
        }

        /// <summary>
        /// Write the profile to where it was read from. It used to always compute the path
        /// from the name — and a profile that came from a file with a different name inside, on
        /// the very first "Save" moved to a new file: the old one stayed as a twin
        /// with the old layout, and if the file of the new name was taken by someone else's
        /// profile, that one was overwritten entirely.
        /// </summary>
        public static void Save(Layout layout)
        {
            string path = !string.IsNullOrEmpty(layout.SourcePath) ? layout.SourcePath : PathFor(layout.Name);
            layout.Save(path);
            layout.SourcePath = path;
        }

        /// <summary>
        /// Remove the profile. We take the path it was read from, not the one that
        /// follows from the name: the name inside the file and the file's own name may diverge,
        /// and then deletion by name missed — it removed a namesake and left the chosen
        /// one in place. Bypasses the recycle bin, nothing to bring back.
        /// </summary>
        public static void Delete(Layout layout)
        {
            string p = !string.IsNullOrEmpty(layout.SourcePath) ? layout.SourcePath : PathFor(layout.Name);
            if (File.Exists(p)) File.Delete(p);
        }

        /// <summary>
        /// Write the profile and remove the former file — but only after the
        /// new one has landed on disk.
        ///
        /// The order used to be the reverse: `File.Delete` first, then `Save`. If
        /// the write failed — and there is plenty to fail it: a too long
        /// name exceeds the 260-character path limit, out of space, file busy —
        /// the profile was left with neither new nor old. The whole layout
        /// vanished, along with the key combination.
        /// </summary>
        public static void Rename(string oldName, Layout layout)
        {
            // The name did not change — this is an ordinary save, and the profile has
            // no reason to move out of its file.
            if (string.Equals(oldName, layout.Name, StringComparison.Ordinal)) { Save(layout); return; }

            string oldPath = !string.IsNullOrEmpty(layout.SourcePath) ? layout.SourcePath : PathFor(oldName);
            string newPath = PathFor(layout.Name);

            // The new name gave the same file — nothing to move.
            if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) { Save(layout); return; }

            // First write the new file, then remove the old: the reverse order
            // on a failed write left neither of them.
            layout.SourcePath = newPath;
            layout.Save(newPath);

            try { if (File.Exists(oldPath)) File.Delete(oldPath); }
            catch { /* not deleted — a stray file stays, but the profile is intact */ }
        }

        public static string UniqueName(string baseName)
        {
            // The list is read once: going through a hundred names, reading all the files at
            // each step — is a hundred folder walks for one new profile.
            var all = LoadAll();
            if (NameInUse(all, baseName, null) == null) return baseName;
            for (int i = 2; i < 100; i++)
            {
                string candidate = baseName + " " + i;
                if (NameInUse(all, candidate, null) == null) return candidate;
            }
            return baseName + " " + Guid.NewGuid().ToString("N").Substring(0, 4);
        }
    }
}
