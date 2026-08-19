using System;
using System.Collections.Generic;
using System.IO;

namespace Triptych
{
    internal static class Store
    {
        public const string AppFolder = "Triptych";

        public static string DataDir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolder);
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
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
            return s.Length == 0 ? "profile" : s;
        }

        public static List<Layout> LoadAll()
        {
            var list = new List<Layout>();
            foreach (string f in Directory.GetFiles(ProfilesDir, "*.layout"))
            {
                try { list.Add(Layout.Load(f)); }
                catch { /* битый файл просто не показываем */ }
            }
            list.Sort(delegate(Layout a, Layout b) { return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase); });
            return list;
        }

        public static Layout LoadByName(string name)
        {
            foreach (var l in LoadAll())
                if (string.Equals(l.Name, name, StringComparison.CurrentCultureIgnoreCase)) return l;
            return null;
        }

        public static void Save(Layout layout) { layout.Save(PathFor(layout.Name)); }

        public static void Delete(Layout layout)
        {
            string p = PathFor(layout.Name);
            if (File.Exists(p)) File.Delete(p);
        }

        public static void Rename(string oldName, Layout layout)
        {
            string oldPath = PathFor(oldName);
            if (File.Exists(oldPath) && !string.Equals(oldName, layout.Name, StringComparison.Ordinal)) File.Delete(oldPath);
            Save(layout);
        }

        public static string UniqueName(string baseName)
        {
            var taken = new List<string>();
            foreach (var l in LoadAll()) taken.Add(l.Name.ToLowerInvariant());
            if (!taken.Contains(baseName.ToLowerInvariant())) return baseName;
            for (int i = 2; i < 100; i++)
            {
                string candidate = baseName + " " + i;
                if (!taken.Contains(candidate.ToLowerInvariant())) return candidate;
            }
            return baseName + " " + Guid.NewGuid().ToString("N").Substring(0, 4);
        }
    }
}
