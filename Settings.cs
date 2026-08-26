// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DIIISPL
{
    /// <summary>
    /// Настройки самой программы — те, что не принадлежат ни одному профилю: канал
    /// обновлений, время последней проверки, отложенная версия. Лежат рядом с профилями
    /// обычным текстом, потому что и профили лежат так: одна папка, один формат,
    /// и всё это можно прочитать и поправить блокнотом.
    /// </summary>
    internal static class Settings
    {
        public const string Stable = "stable";
        public const string Dev = "dev";

        private static Dictionary<string, string> _values;

        /// <summary>
        /// Настройки читает и пишет не один поток: проверка обновлений уходит в
        /// пул и оттуда ставит время последней проверки, пока окно рядом читает
        /// канал. Dictionary такого не выдерживает — чтение во время дописывания
        /// может не найти существующий ключ, а может и зациклиться внутри.
        /// </summary>
        private static readonly object Lock = new object();

        private static string FilePath
        {
            get { return Path.Combine(Store.DataDir, "settings.ini"); }
        }

        /// <summary>Канал обновлений: Stable или Dev.</summary>
        public static string Channel
        {
            get { string v = Get("channel"); return v == Dev ? Dev : Stable; }
            set { Set("channel", value == Dev ? Dev : Stable); }
        }

        /// <summary>Когда в последний раз спрашивали GitHub. Пусто — не спрашивали ни разу.</summary>
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
        /// Показывали ли уже подсказку «программа осталась в трее». Один раз за всё
        /// время: крестик прячет окно, а не закрывает программу, и молча это
        /// читается как «закрыл» — дальше человек либо удивляется, что «закрытая»
        /// программа переключает экраны, либо ищет выход и не находит.
        /// </summary>
        public static bool TrayHintShown
        {
            get { return Get("trayhint") == "1"; }
            set { Set("trayhint", value ? "1" : ""); }
        }

        /// <summary>
        /// Язык окна: «auto», «ru» или «en». По умолчанию auto — язык Windows.
        /// Хранится здесь, а не выводится каждый раз из системы, потому что выбор
        /// человека надо помнить: английская Windows не повод отбирать русский.
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
                // Проверять код по списку здесь нельзя: языковой файл может
                // появиться позже, и запомненный выбор не должен от этого пропасть.
                // Несуществующий язык всё равно превратится в запасной при чтении.
                Set("language", value == Text.Auto ? "" : value);
            }
        }

        /// <summary>
        /// Переключать ли профиль самому, когда меняется состав мониторов.
        /// По умолчанию включено: ради этого возможность и заводилась, и
        /// человек не должен искать настройку, чтобы получить главное.
        ///
        /// Хранится наоборот — ключ пишется только при отказе. Так значение
        /// по умолчанию живёт в одном месте, в коде, а не ещё и в файле,
        /// который мог быть написан прежней версией.
        /// </summary>
        public static bool AutoSwitch
        {
            get { return Get("autoswitch") != "0"; }
            set { Set("autoswitch", value ? "" : "0"); }
        }

        /// <summary>Версия, от которой отказались: про неё больше не напоминаем.</summary>
        public static string SkippedVersion
        {
            get { return Get("skip"); }
            set { Set("skip", value); }
        }

        // ------------------------------------------------------------------ чтение и запись
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
        /// Каким файл был, когда мы его читали. По этой паре видно, что его
        /// переписали мимо нас.
        /// </summary>
        private static DateTime _stamp;
        private static long _size = -1;

        private static void Load()
        {
            // Звать только под замком.
            //
            // Перечитываем, если файл на диске изменился. Прежде настройки
            // читались один раз за запуск, и это значило две неприятности сразу:
            // правка блокнотом при работающей программе не подхватывалась и
            // затиралась первой же записью, а вторая копия — `DIIISPL.exe /apply`
            // из планировщика или ярлыка — писала своё, и окно, ничего об этом не
            // зная, возвращало старое.
            DateTime stamp = DateTime.MinValue;
            long size = -1;
            try
            {
                var info = new FileInfo(FilePath);
                if (info.Exists) { stamp = info.LastWriteTimeUtc; size = info.Length; }
            }
            catch { /* не спросить — считаем, что не менялся */ }

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
            catch { /* нечитаемый файл — начинаем с пустых настроек */ }
            _values = values;
        }

        private static void Save()
        {
            try
            {
                var lines = new List<string>();
                lines.Add("# DIIISPL settings");   // служебный файл — только по-английски
                foreach (var kv in _values) lines.Add(kv.Key + " = " + kv.Value);
                File.WriteAllLines(FilePath, lines.ToArray(), System.Text.Encoding.UTF8);

                // Запомнить файл таким, каким мы его оставили: иначе следующее же
                // чтение сочтёт свою собственную запись чужой правкой.
                var info = new FileInfo(FilePath);
                if (info.Exists) { _stamp = info.LastWriteTimeUtc; _size = info.Length; }
            }
            catch { /* некуда записать — настройки проживут до выхода из программы */ }
        }
    }
}
