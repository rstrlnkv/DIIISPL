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

        /// <summary>Версия, от которой отказались: про неё больше не напоминаем.</summary>
        public static string SkippedVersion
        {
            get { return Get("skip"); }
            set { Set("skip", value); }
        }

        // ------------------------------------------------------------------ чтение и запись
        private static string Get(string key)
        {
            Load();
            string v;
            return _values.TryGetValue(key, out v) ? v : "";
        }

        private static void Set(string key, string value)
        {
            Load();
            if (string.IsNullOrEmpty(value)) _values.Remove(key);
            else _values[key] = value;
            Save();
        }

        private static void Load()
        {
            if (_values != null) return;
            _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("[")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    _values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch { /* нечитаемый файл — начинаем с пустых настроек */ }
        }

        private static void Save()
        {
            try
            {
                var lines = new List<string>();
                lines.Add("# Настройки DIIISPL");
                foreach (var kv in _values) lines.Add(kv.Key + " = " + kv.Value);
                File.WriteAllLines(FilePath, lines.ToArray(), System.Text.Encoding.UTF8);
            }
            catch { /* некуда записать — настройки проживут до выхода из программы */ }
        }
    }
}
