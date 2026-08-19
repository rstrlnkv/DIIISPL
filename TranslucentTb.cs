// DIIIX — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DIIIX
{
    /// <summary>
    /// Управление внешним видом панели задач через TranslucentTB.
    ///
    /// Сама Windows наружу такой возможности не даёт: на сборках Windows 11 панель рисует фон
    /// в своём XAML-дереве, и старая политика акцента (SetWindowCompositionAttribute) до него
    /// не достаёт — проверено замерами, цвет не меняется ни в одном режиме. TranslucentTB
    /// решает это иначе: грузит свою библиотеку внутрь проводника и работает с деревом изнутри.
    ///
    /// Поэтому DIIIX не повторяет эту машинерию, а управляет ею: правит документированный
    /// settings.json (схема — https://TranslucentTB.github.io/settings.schema.json) и при
    /// необходимости запускает программу. TranslucentTB перечитывает файл на лету, так что
    /// вид панели становится частью профиля раскладки — чего по отдельности не умеет ни одна
    /// из двух программ.
    /// </summary>
    internal static class TranslucentTb
    {
        public const string PackageFamily = "28017CharlesMilette.TranslucentTB_v826wp6bftszj";
        private const string AppId = "TranslucentTB";

        /// <summary>Виды панели, которые понимает TranslucentTB.</summary>
        public static readonly string[] Accents = { "normal", "opaque", "clear", "blur", "acrylic" };

        public static string Describe(string accent)
        {
            switch (accent)
            {
                case "normal": return "Как в системе";
                case "opaque": return "Непрозрачная";
                case "clear": return "Прозрачная";
                case "blur": return "Размытие";
                case "acrylic": return "Акрил";
                default: return "Не менять";
            }
        }

        public static bool IsKnownAccent(string accent)
        {
            if (string.IsNullOrEmpty(accent)) return false;
            foreach (string a in Accents) if (a == accent) return true;
            return false;
        }

        /// <summary>Путь к настройкам: сначала пакет из Store, затем обычная установка.</summary>
        public static string SettingsPath()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string packaged = Path.Combine(local, "Packages", PackageFamily, "RoamingState", "settings.json");
            if (File.Exists(packaged)) return packaged;

            // на случай другой версии пакета — ищем по имени
            try
            {
                string packages = Path.Combine(local, "Packages");
                if (Directory.Exists(packages))
                {
                    foreach (string dir in Directory.GetDirectories(packages, "*TranslucentTB*"))
                    {
                        string candidate = Path.Combine(dir, "RoamingState", "settings.json");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            catch { }

            string portable = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TranslucentTB", "settings.json");
            if (File.Exists(portable)) return portable;

            return null;
        }

        public static bool Installed { get { return SettingsPath() != null; } }

        public static bool Running
        {
            get
            {
                try { return System.Diagnostics.Process.GetProcessesByName("TranslucentTB").Length > 0; }
                catch { return false; }
            }
        }

        /// <summary>Текущий вид панели рабочего стола или null, если прочитать не удалось.</summary>
        public static string CurrentAccent()
        {
            string path = SettingsPath();
            if (path == null) return null;
            try
            {
                string text = File.ReadAllText(path);
                Match m = Regex.Match(text, "\"desktop_appearance\"\\s*:\\s*\\{\\s*\"accent\"\\s*:\\s*\"(\\w+)\"");
                return m.Success ? m.Groups[1].Value : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Меняет только вид панели рабочего стола, остальные настройки не трогает:
        /// файл принадлежит пользователю, и переписывать его целиком мы не вправе.
        /// Перед первой правкой рядом кладётся копия исходного файла.
        /// </summary>
        public static bool SetAccent(string accent, out string error)
        {
            error = null;
            if (!IsKnownAccent(accent)) { error = "неизвестный вид панели: " + accent; return false; }

            string path = SettingsPath();
            if (path == null) { error = "TranslucentTB не установлен"; return false; }

            try
            {
                string text = File.ReadAllText(path);
                string backup = path + ".before-diiix";
                if (!File.Exists(backup)) File.Copy(path, backup);

                string updated = Regex.Replace(text,
                    "(\"desktop_appearance\"\\s*:\\s*\\{\\s*\"accent\"\\s*:\\s*)\"\\w+\"",
                    "$1\"" + accent + "\"");

                if (updated == text) return true;                    // уже стоит нужный вид
                if (!Regex.IsMatch(updated, "\"accent\"\\s*:\\s*\"" + accent + "\""))
                {
                    error = "не нашёл, что править в settings.json";
                    return false;
                }

                File.WriteAllText(path, updated, new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Запустить TranslucentTB, если он ещё не работает.</summary>
        public static bool Launch(out string error)
        {
            error = null;
            if (Running) return true;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = "explorer.exe";
                psi.Arguments = "shell:AppsFolder\\" + PackageFamily + "!" + AppId;
                psi.UseShellExecute = true;
                System.Diagnostics.Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Задать вид панели и убедиться, что программа работает.</summary>
        public static bool Apply(string accent, List<string> warnings)
        {
            if (string.IsNullOrEmpty(accent)) return true;      // профиль панель не трогает
            if (!Installed)
            {
                warnings.Add("Вид панели задач задан, но TranslucentTB не установлен — пропущено.");
                return false;
            }

            string error;
            if (!SetAccent(accent, out error))
            {
                warnings.Add("Не удалось задать вид панели задач: " + error);
                return false;
            }
            if (!Launch(out error))
                warnings.Add("Не удалось запустить TranslucentTB: " + error);
            return true;
        }
    }
}
