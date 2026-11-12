// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DIIISPL
{
    /// <summary>Строка изменения: бейдж и текст порознь — в окне они рисуются по-разному.</summary>
    internal sealed class ChangelogLine
    {
        public string Badge = "";          // NEW, FIX, UPD, DEL, DEP, SEC; empty - no badge
        public string Group = "";          // the "###" heading it sits under; kept for tests/ChangelogCheck.cs, the app never reads it
        public string Text = "";
    }

    /// <summary>Один выпуск в чейнджлоге: номер, дата и строки изменений.</summary>
    internal sealed class ChangelogEntry
    {
        public string Version = "";
        public string Date = "";
        public string Note = "";           // «Первый выпуск.» и подобное — абзац перед списком
        public List<ChangelogLine> Lines = new List<ChangelogLine>();
    }

    /// <summary>
    /// Чейнджлог, лежащий внутри программы.
    ///
    /// `CHANGELOG.ru.md` и `CHANGELOG.md` (английский) кладутся в сборку ресурсами — так же,
    /// как номер сборки вписывается в `BuildInfo.g.cs`. Причина простая: до этого
    /// прочитать «что нового» изнутри было нельзя ни при каких условиях, кроме
    /// «вышло что-то новее». Человек, который только что обновился, хочет знать,
    /// что он получил, — а именно этот случай и не покрывался: кнопка уводила
    /// в браузер, на страницу выпусков, где его версии может не быть вовсе.
    ///
    /// Внутри программы он работает всегда: без сети, без выпуска на GitHub и
    /// у копии, собранной на своей машине.
    /// </summary>
    internal static class Changelog
    {
        /// <summary>Выпуски, свежий первым. Пусто — ресурса нет (сборка без чейнджлога).</summary>
        public static List<ChangelogEntry> Read()
        {
            string text = Raw(Text.Code);

            // Язык, приехавший файлом, своего чейнджлога не имеет и не может иметь:
            // перевод надписей — дело пользователя, перевод истории выпусков — нет.
            if (text == null) text = Raw(Lang.Codes[0]);

            return Parse(text);
        }

        /// <summary>
        /// Parse a Keep a Changelog text: "## [0.2.2-dev.1] - 2026-08-27" sections,
        /// each with "### Added" (or "### Добавлено") groups of "- text" lines.
        /// A line without a bold badge takes the badge of its group; "## [Unreleased]" is skipped.
        /// </summary>
        public static List<ChangelogEntry> Parse(string text)
        {
            var list = new List<ChangelogEntry>();
            if (string.IsNullOrEmpty(text)) return list;

            ChangelogEntry current = null;
            string group = "", groupBadge = "";
            foreach (string raw in text.Split((char)10))
            {
                string line = raw.Trim();

                if (line.StartsWith("## "))
                {
                    group = ""; groupBadge = "";
                    Match m = Regex.Match(line.Substring(3).Trim(), @"^\[([^\]]+)\]\s*(?:-\s*(\d{4}-\d{2}-\d{2}))?\s*$");
                    if (!m.Success || m.Groups[1].Value.Equals("Unreleased", StringComparison.OrdinalIgnoreCase))
                    {
                        current = null;
                        continue;
                    }
                    current = new ChangelogEntry();
                    current.Version = m.Groups[1].Value;
                    current.Date = m.Groups[2].Value;
                    list.Add(current);
                    continue;
                }

                if (line.StartsWith("### "))
                {
                    group = line.Substring(4).Trim();
                    groupBadge = BadgeOfGroup(group);
                    continue;
                }

                if (current == null) continue;      // file header and Unreleased
                if (line.Length == 0 || line.StartsWith("#")) continue;

                if (line.StartsWith("- "))
                {
                    ChangelogLine l = Split(line.Substring(2));
                    if (l.Badge.Length == 0) l.Badge = groupBadge;
                    l.Group = group;
                    current.Lines.Add(l);
                }
                else if (current.Lines.Count == 0 && current.Note.Length == 0) current.Note = Plain(line);
            }
            return list;
        }

        /// <summary>Badge of a group (Added/Добавлено is NEW and so on); an unknown group gives "".</summary>
        public static string BadgeOfGroup(string group)
        {
            switch (group.ToLowerInvariant())
            {
                case "added": case "добавлено": return "NEW";
                case "fixed": case "исправлено": return "FIX";
                case "changed": case "изменено": return "UPD";
                case "removed": case "удалено": return "DEL";
                case "deprecated": case "устарело": return "DEP";
                case "security": case "безопасность": return "SEC";
                default: return "";
            }
        }

        /// <summary>
        /// Разобрать строку списка на бейдж и текст. «**NEW** Профиль встаёт сам» —
        /// это бейдж «NEW» и всё остальное; звёздочки и кавычки вокруг команд
        /// убираются, потому что в окне их некому превратить в оформление.
        /// </summary>
        public static ChangelogLine Split(string bullet)
        {
            var line = new ChangelogLine();
            string s = bullet.Trim();

            if (s.StartsWith("**"))
            {
                int end = s.IndexOf("**", 2);
                if (end > 2)
                {
                    line.Badge = s.Substring(2, end - 2).Trim();
                    s = s.Substring(end + 2).TrimStart();
                }
            }
            line.Text = s.Replace("`", "");
            return line;
        }

        /// <summary>
        /// То же самое одной строкой — для мест, где бейджу негде быть отдельным:
        /// подсказка о найденном обновлении и абзац перед списком. Строка без своего
        /// бейджа берёт бейдж группы (groupBadge).
        /// </summary>
        public static string Plain(string text, string groupBadge = "")
        {
            ChangelogLine line = Split(text);
            string badge = line.Badge.Length > 0 ? line.Badge : groupBadge;
            return badge.Length > 0 ? badge + " · " + line.Text : line.Text;
        }

        /// <summary>
        /// The list lines of one section of a published release body: the section whose
        /// "## " heading starts with <paramref name="heading"/>, up to the next "## " or "---".
        /// A bullet without a bold badge takes the badge of its "###" group. At most
        /// <paramref name="max"/> lines come back (0 - all); the rest is counted in <paramref name="more"/>.
        /// </summary>
        public static List<string> Highlights(string notes, string heading, int max, out int more)
        {
            more = 0;
            var found = new List<string>();
            if (string.IsNullOrEmpty(notes)) return found;

            bool inside = false;
            string groupBadge = "";                // badge of the current "###" group
            // Split on line feeds; Trim below removes the carriage return.
            foreach (string raw in notes.Split((char)10))
            {
                string line = raw.Trim();
                if (line.StartsWith("## "))
                {
                    if (inside) break;                 // the section is over
                    groupBadge = "";
                    inside = line.Substring(3).TrimStart()
                                 .StartsWith(heading, StringComparison.CurrentCultureIgnoreCase);
                    continue;
                }
                if (!inside) continue;
                if (line.StartsWith("### ")) { groupBadge = BadgeOfGroup(line.Substring(4).Trim()); continue; }
                if (line == "---") break;
                if (!line.StartsWith("- ")) continue;
                found.Add(Plain(line.Substring(2), groupBadge));
            }

            if (max > 0 && found.Count > max)
            {
                more = found.Count - max;
                found.RemoveRange(max, more);
            }
            return found;
        }

        private static string Raw(string code)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("changelog." + code))
                {
                    if (s == null) return null;
                    using (var reader = new StreamReader(s, Encoding.UTF8)) return reader.ReadToEnd();
                }
            }
            catch { return null; }
        }
    }
}
