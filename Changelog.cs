// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace DIIISPL
{
    /// <summary>Строка изменения: бейдж и текст порознь — в окне они рисуются по-разному.</summary>
    internal sealed class ChangelogLine
    {
        public string Badge = "";          // NEW, FIX, UPD; пусто — строка без бейджа
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
    /// `CHANGELOG.md` и `CHANGELOG.en.md` кладутся в сборку ресурсами — так же,
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

            var list = new List<ChangelogEntry>();
            if (string.IsNullOrEmpty(text)) return list;

            ChangelogEntry current = null;
            foreach (string raw in text.Split((char)10))
            {
                string line = raw.Trim();

                if (line.StartsWith("## "))
                {
                    current = new ChangelogEntry();
                    string head = line.Substring(3).Trim();

                    // «0.1.4-dev.1 — 2026-08-26»: номер, длинное тире, дата.
                    int dash = head.IndexOf('—');
                    if (dash > 0)
                    {
                        current.Version = head.Substring(0, dash).Trim();
                        current.Date = head.Substring(dash + 1).Trim();
                    }
                    else current.Version = head;

                    list.Add(current);
                    continue;
                }

                if (current == null) continue;      // шапка файла до первого выпуска
                if (line.Length == 0 || line.StartsWith("#")) continue;

                if (line.StartsWith("- ")) current.Lines.Add(Split(line.Substring(2)));
                else if (current.Lines.Count == 0 && current.Note.Length == 0) current.Note = Plain(line);
            }
            return list;
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
        /// подсказка о найденном обновлении и абзац перед списком.
        /// </summary>
        public static string Plain(string text)
        {
            ChangelogLine line = Split(text);
            return line.Badge.Length > 0 ? line.Badge + " · " + line.Text : line.Text;
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
