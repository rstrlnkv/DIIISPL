// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;

namespace DIIISPL
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
#if DEV
                    case "taskbar": return SetTaskbar(args);
                    case "taskbar-state": return DumpTaskbarState(args);
#endif
                    case "lang": return WriteLangTemplate(args);
                    case "list": return ListProfiles();
                    case "save": return SaveCurrent(args);
                    case "apply":
                    case "switch": return ApplyProfile(args);
                    case "delete": return DeleteProfile(args);
                    default:
                        // Опечатка в команде не должна выглядеть удачей: прежде
                        // `/aply "Кино"` печатал справку и возвращал 0, и ни
                        // человек, ни скрипт не узнавали, что ничего не применено.
                        Console.WriteLine();
                        Console.WriteLine(Text.CliUnknownCmd(cmd));
                        Help();
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(Text.CliError(ex.Message));
                return 1;
            }
        }

        private static int Help()
        {
            Console.WriteLine();
            Console.WriteLine(Text.CliTitle);
            Console.WriteLine();
            Console.WriteLine(Text.CliHelpWindow);
            Console.WriteLine(Text.CliHelpApply);
            Console.WriteLine(Text.CliHelpSave);
            Console.WriteLine(Text.CliHelpList);
            Console.WriteLine(Text.CliHelpDelete);
            Console.WriteLine(Text.CliHelpMonitors);
            Console.WriteLine(Text.CliHelpHdr);
            Console.WriteLine(Text.CliHelpLang);
#if DEV
            Console.WriteLine(Text.CliHelpTaskbar);
            Console.WriteLine(Text.CliHelpTaskbarState);
#endif
            return 0;
        }

        private static int DumpMonitors()
        {
            Console.WriteLine();
            var list = Monitors.Enumerate();
            if (list.Count == 0) { Console.WriteLine(Text.CliNoMonitors); return 1; }

            foreach (var m in list)
            {
                Console.WriteLine("  " + m.Name + "   [" + m.ConnectionType + "]");
                Console.WriteLine(Text.CliKey(m.Identity));
                Console.WriteLine(Text.CliState(m.Active
                    ? Text.CliOn(m.Width, m.Height, m.X, m.Y, m.Hz.ToString("0.###"), m.SourceId, m.Primary)
                    : Text.CliOff));
                Console.WriteLine(Text.CliHdrLine(Text.CliHdrState(m.HdrSupported, m.HdrEnabled)));
                Console.WriteLine(Text.CliPreferred(m.PreferredWidth, m.PreferredHeight, m.PreferredHz.ToString("0.###")));
                Console.WriteLine(Text.CliModes(m.Modes.Count, m.ModesAreApproximate));
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
                Console.WriteLine(Text.CliOutputs(string.Join(",", sids.ToArray())));
                Console.WriteLine();
            }
            return 0;
        }

#if DEV
        /// <summary>Что программа видит на экранах и какой вид панели из этого следует.</summary>
        private static int DumpTaskbarState(string[] args)
        {
            Console.WriteLine();
            TaskbarSettings settings = null;
            if (args.Length > 1)
            {
                var layout = Store.LoadByName(args[1]);
                if (layout == null) { Console.WriteLine(Text.CliProfileNotFound(args[1])); return 2; }
                settings = layout.Taskbar;
                Console.WriteLine(Text.CliProfileIs(layout.Name));
                foreach (TaskbarState s in TaskbarSettings.Order)
                {
                    var a = settings[s];
                    Console.WriteLine("    " + TaskbarSettings.Describe(s).PadRight(24) +
                                      (a.Enabled ? Text.CliColorLine(ExplorerTap.Describe(a.Accent), a.Color.ToString("X8"), a.ShowLine)
                                                 : Text.CliStateOff));
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
                Console.WriteLine(Text.CliNoTaskbarFile);
                return 1;
            }
            if (args.Length < 2)
            {
                Console.WriteLine(Text.CliPickLook(string.Join(", ", ExplorerTap.Names)));
                return 2;
            }

            // Опечатка в названии вида не должна выглядеть удачей. Прежде
            // «acrilic» разбиралось в «обычную» панель, команда бодро отвечала
            // «применено», и человек искал причину в чём угодно, кроме буквы.
            string wanted = args[1].Trim().ToLowerInvariant();
            bool known = false;
            foreach (string n in ExplorerTap.Names) if (n == wanted) known = true;
            if (!known)
            {
                Console.WriteLine(Text.CliPickLook(string.Join(", ", ExplorerTap.Names)));
                return 2;
            }

            var bars = ExplorerTap.FindTaskbars();
            Console.WriteLine(Text.CliBarsFound(bars.Count));

            var look = ExplorerTap.Parse(wanted);
            uint color = 0x00000000;
            if (args.Length > 2 && !ExplorerTap.TryParseColor(args[2], out color))
            {
                Console.WriteLine(Text.CliColorFormat);
                return 2;
            }

            // То же и с последним словом: тихо проглоченное «line1» оставляло
            // человека гадать, почему линии у края нет.
            bool showLine = false;
            if (args.Length > 3)
            {
                if (args[3].Trim().ToLowerInvariant() != "line")
                {
                    Console.WriteLine(Text.CliLineOnly);
                    return 2;
                }
                showLine = true;
            }

            bool ok = ExplorerTap.ApplyAll(look, color, 9.0f, showLine);
            Console.WriteLine(ok
                ? Text.CliApplied(ExplorerTap.Describe(wanted))
                : Text.CliFailed(ExplorerTap.LastError));
            return ok ? 0 : 1;
        }
#endif

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
                string mode = m.Active ? (m.Width + "x" + m.Height + " " + m.Hz.ToString("0.###") + " " + Text.Hz) : Text.CliOff;
                string hdr = Text.CliHdrDetail(m.Active, m.HdrSupported, m.HdrEnabled, inCloneGroup);
                Console.WriteLine("  " + m.Name + "  [" + m.ConnectionType + "]");
                Console.WriteLine(Text.CliModeLine(mode));
                Console.WriteLine(Text.CliHdrShort(hdr));
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
                Console.WriteLine(Text.CliCloneLine(string.Join(" + ", names.ToArray()), string.Join(" / ", states.ToArray())));
                Console.WriteLine(Text.CliCloneNote);
            }
            return 0;
        }

        /// <summary>
        /// Записать образец перевода: все ключи с английскими значениями, а строки,
        /// которые уже переведены, — переведёнными. Без такого образца человек не
        /// знает ни списка ключей, ни того, что вообще можно перевести.
        /// </summary>
        private static int WriteLangTemplate(string[] args)
        {
            Console.WriteLine();
            if (args.Length < 2) { Console.WriteLine(Text.CliLangNeedCode); return 2; }

            string code = args[1].Trim().ToLowerInvariant();
            if (!LangFile.IsUsableCode(code))
            {
                Console.WriteLine(Text.CliLangBadCode);
                return 2;
            }

            string path = LangFile.WriteTemplate(code);
            Console.WriteLine(Text.CliLangWritten(path));

            // Сколько строк ещё английские. Без этого числа переводчик узнаёт о
            // пропущенном только увидев его в чужом окне.
            int left = Text.Missing(code).Count;
            if (left > 0) Console.WriteLine(Text.CliLangLeft(left));

            Console.WriteLine(Text.CliLangHowTo);
            return 0;
        }

        private static int ListProfiles()
        {
            Console.WriteLine();
            var all = Store.LoadAll();
            if (all.Count == 0) { Console.WriteLine(Text.CliNoProfiles); return 0; }
            foreach (var l in all)
            {
                int on = 0;
                foreach (var d in l.Displays) if (d.Enabled) on++;
                Console.WriteLine(Text.CliProfileRow(l.Name, on, l.Displays.Count, l.Hotkey));
            }
            return 0;
        }

        private static int SaveCurrent(string[] args)
        {
            if (args.Length < 2) { Console.WriteLine(Text.CliNeedName); return 2; }
            var layout = Layout.FromCurrent(args[1]);

            // Сохранение поверх существующего профиля переснимает **раскладку**, а не
            // весь профиль. Прежде FromCurrent строил чистый объект и Store.Save писал
            // им поверх файла: одна команда молча уносила и назначенное сочетание
            // клавиш, и место в списке, и секцию вида панели задач.
            var existing = Store.LoadByName(args[1]);

            // Профиль ищется по имени, а файл выбирается по очищенному имени — и
            // это разные вещи. `/save "Дом "` с лишним пробелом не находил «Дом»,
            // зато писал ровно в его файл: свежий снимок ложился поверх, унося и
            // сочетание клавиш, и место в списке, и вид панели задач. В консоли
            // при этом бодро писалось «Сохранено».
            if (existing == null)
            {
                string clash = Store.NameInUse(args[1], null);
                if (clash != null)
                {
                    Console.WriteLine(Text.CliSameName(args[1], clash));
                    Console.WriteLine(Text.CliSameNameHint(clash));
                    return 2;
                }
            }
            else layout.AdoptSettingsFrom(existing);

            Store.Save(layout);
            Console.WriteLine(existing != null ? Text.CliRetaken(layout.Name) : Text.CliSaved(layout.Name));
            return 0;
        }

        private static int ApplyProfile(string[] args)
        {
            if (args.Length < 2) { Console.WriteLine(Text.CliNeedName); return 2; }
            var layout = Store.LoadByName(args[1]);
            if (layout == null) { Console.WriteLine(Text.CliNotFound(args[1])); return 2; }
            var res = Applier.Apply(layout);
            foreach (string w in res.Warnings) Console.WriteLine("  ! " + w);
            if (!res.Success) { Console.WriteLine(Text.CliError(res.Error)); return 1; }
            Console.WriteLine(Text.CliAppliedName(layout.Name));
            return 0;
        }

        private static int DeleteProfile(string[] args)
        {
            if (args.Length < 2) { Console.WriteLine(Text.CliNeedName); return 2; }
            var layout = Store.LoadByName(args[1]);
            if (layout == null) { Console.WriteLine(Text.CliNotFound(args[1])); return 2; }
            Store.Delete(layout);
            Console.WriteLine(Text.CliDeleted(layout.Name));
            return 0;
        }
    }
}
