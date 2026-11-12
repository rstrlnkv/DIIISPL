// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DIIISPL
{
    /// <summary>
    /// Переводы из файлов: язык можно добавить, не пересобирая программу.
    ///
    /// Встроенные словари остаются основой, файл их только дополняет и перекрывает.
    /// Значит перевод, в котором чего-то не хватает, не оставляет в окне пустых
    /// мест, а тот, кто хочет поправить одну строку, пишет в файл одну строку.
    ///
    /// Ищем в двух местах, и порядок важен:
    ///   1) `lang` рядом с программой — сюда кладут переводы, приезжающие с ней;
    ///   2) `%AppData%\DIIISPL\lang` — сюда пишет человек, у которого нет прав на
    ///      Program Files. Его файл сильнее: это его машина и его выбор.
    ///
    /// Формат — тот же, что у профилей и настроек: строки `ключ = значение`,
    /// решётка и точка с запятой начинают комментарий. Кодировка UTF-8; BOM не
    /// обязателен, но без него не-английские буквы придётся сохранять именно в
    /// UTF-8, а не в кодировке системы.
    /// </summary>
    internal static class LangFile
    {
        public const string FolderName = "lang";
        public const string Extension = ".lang";

        /// <summary>
        /// Годится ли строка кодом языка. Отсекает не только запрещённые в именах
        /// файлов знаки, но и имена устройств: `/lang nul` бодро отвечал «образец
        /// записан», а запись уходила в никуда, и файла не появлялось.
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

        /// <summary>Наложить файл языка на готовый словарь. Файла нет — словарь не меняется.</summary>
        public static void Overlay(string code, Dictionary<string, string> table)
        {
            foreach (string path in PathsFor(code))
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    ReadInto(path, table);
                }
                catch { /* нечитаемый файл — остаётся встроенный перевод */ }
            }
        }

        /// <summary>
        /// Какие языки лежат файлами. Нужны окну выбора: язык, которого нет во
        /// встроенном списке, иначе неоткуда взять.
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

                        // «auto» в настройках значит «как в Windows». Файл с таким
                        // именем стал бы языком для всех, кто ничего не выбирал.
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
        /// Название языка из файла — строка `language.name`. Ею язык подписывает
        /// себя сам; без неё в окне выбора остаётся только код.
        /// </summary>
        public static string NameOf(string code)
        {
            var one = new Dictionary<string, string>(StringComparer.Ordinal);
            Overlay(code, one);
            string name;
            return one.TryGetValue("language.name", out name) && name.Length > 0 ? name : code;
        }

        /// <summary>
        /// Записать образец для перевода: все ключи с английскими значениями и
        /// пометкой, где чего не хватает. Без такого образца человек не знает ни
        /// списка ключей, ни того, что вообще можно перевести.
        /// </summary>
        public static string WriteTemplate(string code)
        {
            var full = Lang.Table(Lang.Codes[0]);
            var mine = Lang.Table(code);
            LangFile.Overlay(code, mine);

            // Формы после числа есть не во всех языках сразу: «.few» живёт только
            // в русском. Строй образец по одному английскому — и переводчик на
            // польский о третьей форме не узнал бы вовсе.
            //
            // Значение при этом берём английское, от формы «.many» того же ключа:
            // русская строка в чешском образце сбивала бы с толку не меньше, чем
            // отсутствие строки вовсе.
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

                // Оригинал показываем только над переведённой строкой: там он
                // нужен для сверки. Над непереведённой он повторил бы её слово
                // в слово и превратил бы файл в двойной список.
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
        /// Перенос строки — обратно в «\n». Строка формата однострочная: настоящий
        /// перенос в образце разорвал бы значение надвое, и вторая половина
        /// пропала бы при чтении.
        ///
        /// Обратная косая перед «n» удваивается — иначе путь вида `C:\notes` после
        /// круга через образец молча превратился бы в две строки.
        /// </summary>
        private static string Escape(string value)
        {
            return value.Replace("\\n", "\\\\n")
                        .Replace("\r\n", "\\n")
                        .Replace("\n", "\\n");
        }

        /// <summary>Обратно: «\\n» — это две буквы, «\n» — перенос строки.</summary>
        private static string Unescape(string value)
        {
            const string keep = "\u0001";     // метка на время разбора; в текстах не встречается
            return value.Replace("\\\\n", keep)
                        .Replace("\\n", "\n")
                        .Replace(keep, "\\n");
        }

        // ------------------------------------------------------------------ пути
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

                // Значение обрезать нельзя: у восьми десятков строк пробел с краю
                // несёт смысл. Ими выровнена вся справка командной строки, и ими
                // же приклеиваются хвосты вроде « ещё 2 замечания» — обрезка
                // склеивала бы слова. Снимается ровно один пробел — тот, что
                // ставится при записи вокруг знака равенства.
                string value = line.Substring(eq + 1);
                if (value.Length > 0 && value[0] == ' ') value = value.Substring(1);

                // Стёртое значение — не пустая надпись, а «переведи это». Стереть
                // текст после «=» проще, чем удалить строку, и человек вправе
                // ждать, что программа возьмёт своё.
                if (value.Length == 0) continue;

                // «\n» в файле — перевод строки: строки формата однострочные, а в
                // паре сообщений перенос нужен по смыслу.
                table[key] = Unescape(value);
            }
        }
    }
}
