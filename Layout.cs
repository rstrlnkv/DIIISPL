using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Triptych
{
    /// <summary>Один монитор внутри профиля. Привязка — по EDID, а не по номеру дисплея Windows.</summary>
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
        public int CloneGroup = -1;      // -1 — отдельный рабочий стол, иначе номер группы дублирования
        public int Hdr = -1;             // -1 не трогать, 0 выключить, 1 включить

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

    /// <summary>Профиль раскладки: собственный формат, независимый от базы дисплеев Windows.</summary>
    internal class Layout
    {
        public string Name = "";
        public string Hotkey = "";
        public List<LayoutDisplay> Displays = new List<LayoutDisplay>();

        /// <summary>
        /// Совпадает ли профиль с тем, что включено прямо сейчас. HDR не учитываем:
        /// его можно переключить мимо программы, и раскладкой он не является.
        /// </summary>
        public bool MatchesCurrent(List<MonitorInfo> live)
        {
            var active = new Dictionary<string, MonitorInfo>();
            foreach (var m in live) if (m.Active) active[m.Identity] = m;

            int wanted = 0;
            foreach (var d in Displays) if (d.Enabled) wanted++;
            if (wanted == 0 || wanted != active.Count) return false;

            var groupSource = new Dictionary<int, uint>();
            var soloSources = new List<uint>();

            foreach (var d in Displays)
            {
                if (!d.Enabled) continue;
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

            // позиции сверяем относительно основного экрана — сам сдвиг значения не имеет
            LayoutDisplay origin = null;
            foreach (var d in Displays) if (d.Enabled && d.Primary) { origin = d; break; }
            if (origin == null) foreach (var d in Displays) if (d.Enabled) { origin = d; break; }
            if (origin == null) return false;

            MonitorInfo originMon = active[origin.Identity];
            int dx = originMon.X - origin.X, dy = originMon.Y - origin.Y;
            foreach (var d in Displays)
            {
                if (!d.Enabled) continue;
                MonitorInfo mon = active[d.Identity];
                if (mon.X != d.X + dx || mon.Y != d.Y + dy) return false;
            }
            return true;
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
            foreach (var d in Displays) copy.Displays.Add(d.Clone());
            return copy;
        }

        /// <summary>Собрать профиль из того, что включено прямо сейчас.</summary>
        public static Layout FromCurrent(string name)
        {
            var layout = new Layout();
            layout.Name = name;

            var monitors = Monitors.Enumerate();
            // определяем группы дублирования: одинаковый source id = один рабочий стол на нескольких выходах
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

                // CCD отдаёт режим источника уже в «родной» ориентации панели — переставлять стороны не нужно
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
                    // один и тот же source id на нескольких выходах = дублирование
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

            // группы, где остался один участник, дублированием не считаются
            var counts = new Dictionary<int, int>();
            foreach (var d in layout.Displays)
            {
                if (d.CloneGroup < 0) continue;
                int c; counts.TryGetValue(d.CloneGroup, out c); counts[d.CloneGroup] = c + 1;
            }
            foreach (var d in layout.Displays)
            {
                if (d.CloneGroup >= 0 && counts[d.CloneGroup] < 2) d.CloneGroup = -1;
            }
            // основной ровно один: при дублировании в (0,0) стоят сразу несколько выходов
            bool primarySeen = false;
            foreach (var d in layout.Displays)
            {
                if (!d.Primary) continue;
                if (primarySeen) d.Primary = false;
                else primarySeen = true;
            }

            // клоны наследуют позицию группы
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

        // ---------------- файл профиля ----------------
        public void Save(string file)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Профиль раскладки мониторов");
            sb.AppendLine("[profile]");
            sb.AppendLine("name = " + Name);
            sb.AppendLine("hotkey = " + (Hotkey ?? ""));
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
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
        }

        public static Layout Load(string file)
        {
            var layout = new Layout();
            LayoutDisplay cur = null;
            string section = "";

            foreach (string raw in File.ReadAllLines(file, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                if (line.StartsWith("["))
                {
                    section = line.Trim('[', ']').ToLowerInvariant();
                    if (section == "display") { cur = new LayoutDisplay(); layout.Displays.Add(cur); }
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string val = line.Substring(eq + 1).Trim();

                if (section == "profile")
                {
                    if (key == "name") layout.Name = val;
                    else if (key == "hotkey") layout.Hotkey = val;
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
            return layout;
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
