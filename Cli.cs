// DIIIX — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;

namespace DIIIX
{
    internal static class Cli
    {
        public static int Run(string[] args)
        {
            Native.AttachConsole(-1);
            string cmd = args[0].TrimStart('-', '/').ToLowerInvariant();
            try
            {
                switch (cmd)
                {
                    case "monitors": return DumpMonitors();
                    case "hdr": return DumpHdr();
                    case "state": return DumpState();
                    case "taskbar": return SetTaskbar(args);
                    case "taskbar-state": return DumpTaskbarState(args);
                    case "list": return ListProfiles();
                    case "save": return SaveCurrent(args);
                    case "apply":
                    case "switch": return ApplyProfile(args);
                    case "delete": return DeleteProfile(args);
                    default: return Help();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
                return 1;
            }
        }

        private static int Help()
        {
            Console.WriteLine();
            Console.WriteLine("DIIIX — раскладки мониторов");
            Console.WriteLine();
            Console.WriteLine("  DIIIX.exe                 открыть окно менеджера");
            Console.WriteLine("  DIIIX.exe /apply \"Имя\"    применить профиль");
            Console.WriteLine("  DIIIX.exe /save \"Имя\"     записать текущую раскладку в профиль");
            Console.WriteLine("  DIIIX.exe /list           список профилей");
            Console.WriteLine("  DIIIX.exe /delete \"Имя\"   удалить профиль");
            Console.WriteLine("  DIIIX.exe /monitors       что подключено и как опознано");
            Console.WriteLine("  DIIIX.exe /hdr            состояние HDR на каждом выходе");
            Console.WriteLine("  DIIIX.exe /taskbar вид [AARRGGBB] [line]   вид панели задач сейчас");
            Console.WriteLine("  DIIIX.exe /taskbar-state [\"Имя\"]           что видно на экранах и какой вид из этого следует");
            return 0;
        }

        private static int DumpMonitors()
        {
            Console.WriteLine();
            var list = Monitors.Enumerate();
            if (list.Count == 0) { Console.WriteLine("  мониторы не найдены"); return 1; }

            foreach (var m in list)
            {
                Console.WriteLine("  " + m.Name + "   [" + m.ConnectionType + "]");
                Console.WriteLine("    ключ:      " + m.Identity);
                Console.WriteLine("    состояние: " + (m.Active
                    ? ("включён, " + m.Width + "x" + m.Height + " @ " + m.X + "," + m.Y + ", " + m.Hz.ToString("0.###") + " Гц, source " + m.SourceId + (m.Primary ? ", основной" : ""))
                    : "выключен"));
                Console.WriteLine("    HDR:       " + (m.HdrSupported ? (m.HdrEnabled ? "поддерживается, включён" : "поддерживается, выключен") : "недоступен"));
                Console.WriteLine("    предпочт.: " + m.PreferredWidth + "x" + m.PreferredHeight + " " + m.PreferredHz.ToString("0.###") + " Гц");
                Console.WriteLine("    режимов:   " + m.Modes.Count + (m.ModesAreApproximate ? " (список приблизительный)" : ""));
                if (m.Modes.Count > 0)
                {
                    var top = new List<string>();
                    for (int i = 0; i < Math.Min(4, m.Modes.Count); i++)
                    {
                        var r = m.Modes[i];
                        var rates = new List<string>();
                        foreach (double hz in r.Rates) rates.Add(hz.ToString("0.##"));
                        top.Add(r.Width + "x" + r.Height + " (" + string.Join("/", rates.ToArray()) + ")");
                    }
                    Console.WriteLine("               " + string.Join(", ", top.ToArray()));
                }
                var sids = new List<string>();
                foreach (uint s in m.ValidSourceIds) sids.Add(s.ToString());
                Console.WriteLine("    выходы:    source " + string.Join(",", sids.ToArray()));
                Console.WriteLine();
            }
            return 0;
        }

        /// <summary>Что программа видит на экранах и какой вид панели из этого следует.</summary>
        private static int DumpTaskbarState(string[] args)
        {
            Console.WriteLine();
            TaskbarSettings settings = null;
            if (args.Length > 1)
            {
                var layout = Store.LoadByName(args[1]);
                if (layout == null) { Console.WriteLine("  профиль не найден: " + args[1]); return 2; }
                settings = layout.Taskbar;
                Console.WriteLine("  профиль: " + layout.Name);
                foreach (TaskbarState s in TaskbarSettings.Order)
                {
                    var a = settings[s];
                    Console.WriteLine("    " + TaskbarSettings.Describe(s).PadRight(24) +
                                      (a.Enabled ? ExplorerTap.Describe(a.Accent) + ", цвет " + a.Color.ToString("X8") +
                                                   ", линия " + (a.ShowLine ? "да" : "нет")
                                                 : "выключено"));
                }
                Console.WriteLine();
            }
            Console.Write(TaskbarWatcher.Report(settings));
            return 0;
        }

        /// <summary>Вид панели задач: default, opaque, clear, blur, acrylic.</summary>
        private static int SetTaskbar(string[] args)
        {
            Console.WriteLine();
            if (!ExplorerTap.Available)
            {
                Console.WriteLine("  В программе не хватает файла для оформления панели задач.");
                return 1;
            }
            if (args.Length < 2)
            {
                Console.WriteLine("  Укажите вид: " + string.Join(", ", ExplorerTap.Names));
                return 2;
            }

            var bars = ExplorerTap.FindTaskbars();
            Console.WriteLine("  панелей задач: " + bars.Count);

            var look = ExplorerTap.Parse(args[1]);
            uint color = 0x00000000;
            if (args.Length > 2 && !ExplorerTap.TryParseColor(args[2], out color))
            {
                Console.WriteLine("  цвет должен быть в виде AARRGGBB");
                return 2;
            }
            bool showLine = args.Length > 3 && args[3].Trim().ToLowerInvariant() == "line";

            bool ok = ExplorerTap.ApplyAll(look, color, 9.0f, showLine);
            Console.WriteLine(ok
                ? "  применено: " + ExplorerTap.Describe(args[1].ToLowerInvariant())
                : "  не удалось: " + ExplorerTap.LastError);
            return ok ? 0 : 1;
        }

        /// <summary>Состояние в машиночитаемом виде — для скриптов и диагностики.</summary>
        private static int DumpState()
        {
            foreach (var m in Monitors.Enumerate())
            {
                Console.WriteLine(string.Join("\t", new string[] {
                    m.Identity,
                    m.Name,
                    m.Active ? "on" : "off",
                    m.X.ToString(), m.Y.ToString(),
                    m.Width.ToString(), m.Height.ToString(),
                    m.Hz.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                    m.Rotation.ToString(),
                    m.SourceId.ToString(),
                    (m.Active && m.X == 0 && m.Y == 0) ? "primary" : "-",
                    m.HdrSupported ? (m.HdrEnabled ? "hdr-on" : "hdr-off") : "hdr-na"
                }));
            }
            return 0;
        }

        /// <summary>Кто сейчас в HDR и кто вообще может — для разбора полётов с дублированием.</summary>
        private static int DumpHdr()
        {
            Console.WriteLine();
            var list = Monitors.Enumerate();
            var bySource = new Dictionary<string, List<MonitorInfo>>();
            foreach (var m in list)
            {
                if (!m.Active) continue;
                string key = m.Adapter.Key + "/" + m.SourceId;
                if (!bySource.ContainsKey(key)) bySource[key] = new List<MonitorInfo>();
                bySource[key].Add(m);
            }

            foreach (var m in list)
            {
                bool inCloneGroup = m.Active && bySource.ContainsKey(m.Adapter.Key + "/" + m.SourceId)
                                    && bySource[m.Adapter.Key + "/" + m.SourceId].Count > 1;
                string mode = m.Active ? (m.Width + "x" + m.Height + " " + m.Hz.ToString("0.###") + " Гц") : "выключен";
                string hdr = !m.Active ? "—"
                    : (m.HdrSupported ? (m.HdrEnabled ? "ВКЛЮЧЁН" : "поддерживается, выключен")
                                      : (inCloneGroup ? "недоступен: выход в дублировании" : "недоступен на этом режиме"));
                Console.WriteLine("  " + m.Name + "  [" + m.ConnectionType + "]");
                Console.WriteLine("    режим: " + mode);
                Console.WriteLine("    HDR:   " + hdr);
            }

            foreach (var kv in bySource)
            {
                if (kv.Value.Count < 2) continue;
                var names = new List<string>();
                var states = new List<string>();
                foreach (var m in kv.Value)
                {
                    names.Add(m.Name);
                    states.Add(m.HdrEnabled ? "HDR" : "SDR");
                }
                Console.WriteLine();
                Console.WriteLine("  Дублирование: " + string.Join(" + ", names.ToArray()) + "  →  " + string.Join(" / ", states.ToArray()));
                Console.WriteLine("  (в группе дублирования HDR должен быть одинаковым на всех выходах)");
            }
            return 0;
        }

        private static int ListProfiles()
        {
            Console.WriteLine();
            var all = Store.LoadAll();
            if (all.Count == 0) { Console.WriteLine("  профилей нет"); return 0; }
            foreach (var l in all)
            {
                int on = 0;
                foreach (var d in l.Displays) if (d.Enabled) on++;
                Console.WriteLine("  " + l.Name + "   мониторов: " + on + "/" + l.Displays.Count +
                                  (string.IsNullOrEmpty(l.Hotkey) ? "" : "   [" + l.Hotkey + "]"));
            }
            return 0;
        }

        private static int SaveCurrent(string[] args)
        {
            if (args.Length < 2) { Console.WriteLine("Укажите имя профиля."); return 2; }
            var layout = Layout.FromCurrent(args[1]);
            Store.Save(layout);
            Console.WriteLine("Сохранено: " + layout.Name);
            return 0;
        }

        private static int ApplyProfile(string[] args)
        {
            if (args.Length < 2) { Console.WriteLine("Укажите имя профиля."); return 2; }
            var layout = Store.LoadByName(args[1]);
            if (layout == null) { Console.WriteLine("Профиль не найден: " + args[1]); return 2; }
            var res = Applier.Apply(layout);
            foreach (string w in res.Warnings) Console.WriteLine("  ! " + w);
            if (!res.Success) { Console.WriteLine("Ошибка: " + res.Error); return 1; }
            Console.WriteLine("Применено: " + layout.Name);
            return 0;
        }

        private static int DeleteProfile(string[] args)
        {
            if (args.Length < 2) { Console.WriteLine("Укажите имя профиля."); return 2; }
            var layout = Store.LoadByName(args[1]);
            if (layout == null) { Console.WriteLine("Профиль не найден: " + args[1]); return 2; }
            Store.Delete(layout);
            Console.WriteLine("Удалено: " + layout.Name);
            return 0;
        }
    }
}
