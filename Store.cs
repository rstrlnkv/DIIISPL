// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.IO;

namespace DIIISPL
{
    internal static class Store
    {
        public const string AppFolder = "DIIISPL";

        /// <summary>Как программа называлась раньше — оттуда переносятся профили.</summary>
        private const string OldAppFolder = "DIIIX";

        private static bool _adoptTried;

        public static string DataDir
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string d = Path.Combine(root, AppFolder);
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);

                // Перенос пробуем раз за запуск, а не «один раз в жизни». Прежде
                // признаком служило отсутствие новой папки — а её создавали строкой
                // выше, до переноса: одна занятая копия обрывала цикл, и повторить
                // было уже нечем. Измерено: из четырёх профилей переносился один,
                // и второй запуск ничего не добавлял.
                if (!_adoptTried)
                {
                    _adoptTried = true;
                    AdoptOldFolder(Path.Combine(root, OldAppFolder), d);
                }
                return d;
            }
        }

        /// <summary>
        /// Забрать профили из папки прежнего имени. Копируем, а не переносим: если что-то
        /// пойдёт не так, прежние файлы останутся на месте, и терять будет нечего.
        /// Делается один раз — при следующем запуске новая папка уже есть.
        /// </summary>
        private static void AdoptOldFolder(string from, string to)
        {
            try
            {
                if (!Directory.Exists(from)) return;

                // Уже перенесли — второй раз незачем.
                string here = Path.Combine(to, "profiles");
                if (Directory.Exists(here) && Directory.GetFiles(here, "*.layout").Length > 0) return;

                foreach (string dir in Directory.GetDirectories(from))
                {
                    string target = Path.Combine(to, Path.GetFileName(dir));
                    if (!Directory.Exists(target)) Directory.CreateDirectory(target);
                    foreach (string file in Directory.GetFiles(dir))
                    {
                        // Перехват вокруг каждого файла, а не вокруг всего переноса:
                        // один занятый файл не должен уносить с собой остальные.
                        try { File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true); }
                        catch { }
                    }
                }
                foreach (string file in Directory.GetFiles(from))
                {
                    // журнал ошибок прежней версии переносить незачем
                    if (string.Equals(Path.GetFileName(file), "error.log", StringComparison.OrdinalIgnoreCase)) continue;
                    try { File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true); }
                    catch { }
                }
            }
            catch { /* не смогли — прежние файлы остались на месте, вернуть их можно руками */ }
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
            if (s.Length == 0) return "profile";

            // Имена устройств DOS уводим в сторону — но не потому, что «CON.layout»
            // создать нельзя: измерено, что можно. Windows 11 отдаёт такой файл как
            // обычный, он читается и попадает в перечисление. Причина проще: этих
            // имён избегает почти всё, что работает с файлами, — архиваторы,
            // облачные папки, старые утилиты, — и профиль с именем «CON» рискует
            // не пережить копирования на другую машину. Имя профиля внутри файла
            // при этом остаётся тем, которое выбрал человек.
            if (IsReservedName(s)) return "_" + s;
            return s;
        }

        private static readonly string[] ReservedNames =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        private static bool IsReservedName(string s)
        {
            foreach (string bad in ReservedNames)
                if (string.Equals(s, bad, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Занято ли имя другим профилем. Возвращает его имя или null.
        ///
        /// Сравниваем не только имена, но и файлы, в которые они лягут: «Дом/Работа»
        /// и «Дом_Работа» — разные имена и один и тот же «Дом_Работа.layout».
        /// Сохранение второго молча затирало первый.
        /// </summary>
        public static string NameInUse(string name, string exceptName)
        {
            return NameInUse(LoadAll(), name, exceptName);
        }

        private static string NameInUse(List<Layout> all, string name, string exceptName)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string file = Sanitize(name);
            foreach (var l in all)
            {
                if (exceptName != null &&
                    string.Equals(l.Name, exceptName, StringComparison.CurrentCultureIgnoreCase)) continue;
                if (string.Equals(l.Name, name, StringComparison.CurrentCultureIgnoreCase)) return l.Name;
                if (string.Equals(Sanitize(l.Name), file, StringComparison.OrdinalIgnoreCase)) return l.Name;
            }

            // Имя внутри файла и имя самого файла — разные вещи, и профили задуманы
            // переносимыми: файл могли скопировать руками, и тогда «Дом.layout»
            // содержит имя «Работа». Сверка только по именам изнутри этого не видит,
            // а Save всё равно ляжет в «Дом.layout» и затрёт его. Поэтому спрашиваем
            // и сам каталог.
            try
            {
                string target = PathFor(name);
                if (File.Exists(target))
                {
                    if (exceptName == null) return Path.GetFileNameWithoutExtension(target);
                    if (!string.Equals(target, PathFor(exceptName), StringComparison.OrdinalIgnoreCase))
                        return Path.GetFileNameWithoutExtension(target);
                }
            }
            catch { /* каталог недоступен — тогда и столкновения не проверить */ }
            return null;
        }

        public static List<Layout> LoadAll()
        {
            var list = new List<Layout>();
            foreach (string f in Directory.GetFiles(ProfilesDir, "*.layout"))
            {
                try { list.Add(Layout.Load(f)); }
                catch { /* битый файл просто не показываем */ }
            }
            // порядок задаёт пользователь; профили без порядка (старые файлы) идут следом по алфавиту
            list.Sort(delegate(Layout a, Layout b)
            {
                if (a.Order != b.Order) return a.Order.CompareTo(b.Order);
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            return list;
        }

        /// <summary>Пронумеровать профили по текущему положению в списке и записать на диск.</summary>
        public static void SaveOrder(List<Layout> ordered)
        {
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Order == i) continue;
                ordered[i].Order = i;
                Save(ordered[i]);
            }
        }

        public static Layout LoadByName(string name)
        {
            foreach (var l in LoadAll())
                if (string.Equals(l.Name, name, StringComparison.CurrentCultureIgnoreCase)) return l;
            return null;
        }

        /// <summary>
        /// Записать профиль туда, откуда он прочитан. Прежде путь всегда считался
        /// из имени — и профиль, пришедший из файла с другим именем внутри, при
        /// первом же «Сохранить» уезжал в новый файл: старый оставался двойником
        /// со старой раскладкой, а если файл нового имени был занят чужим
        /// профилем, тот затирался целиком.
        /// </summary>
        public static void Save(Layout layout)
        {
            string path = !string.IsNullOrEmpty(layout.SourcePath) ? layout.SourcePath : PathFor(layout.Name);
            layout.Save(path);
            layout.SourcePath = path;
        }

        /// <summary>
        /// Убрать профиль. Путь берём тот, из которого он прочитан, а не тот, что
        /// следует из имени: имя внутри файла и имя самого файла могут разойтись,
        /// и тогда удаление по имени било мимо — сносило чужого тёзку, а выбранный
        /// оставляло на месте. Мимо корзины, вернуть нечего.
        /// </summary>
        public static void Delete(Layout layout)
        {
            string p = !string.IsNullOrEmpty(layout.SourcePath) ? layout.SourcePath : PathFor(layout.Name);
            if (File.Exists(p)) File.Delete(p);
        }

        /// <summary>
        /// Записать профиль, а прежний файл убрать — но только после того, как
        /// новый лёг на диск.
        ///
        /// Прежде порядок был обратным: сначала `File.Delete`, потом `Save`. Если
        /// запись не удавалась — а не удаться ей есть от чего: слишком длинное
        /// имя перебирает предел пути в 260 знаков, кончилось место, файл занят —
        /// от профиля не оставалось ни нового, ни старого. Пропадала вся
        /// раскладка целиком, вместе с сочетанием клавиш.
        /// </summary>
        public static void Rename(string oldName, Layout layout)
        {
            // Имя не менялось — это обычное сохранение, и уезжать из своего файла
            // профилю незачем.
            if (string.Equals(oldName, layout.Name, StringComparison.Ordinal)) { Save(layout); return; }

            string oldPath = !string.IsNullOrEmpty(layout.SourcePath) ? layout.SourcePath : PathFor(oldName);
            string newPath = PathFor(layout.Name);

            // Новое имя дало тот же файл — переносить нечего.
            if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) { Save(layout); return; }

            // Сначала пишем новый файл, потом убираем старый: обратный порядок
            // на неудачной записи не оставлял ни того, ни другого.
            layout.SourcePath = newPath;
            layout.Save(newPath);

            try { if (File.Exists(oldPath)) File.Delete(oldPath); }
            catch { /* не удалился — останется лишний файл, но профиль цел */ }
        }

        public static string UniqueName(string baseName)
        {
            // Список читаем один раз: перебор ста имён со чтением всех файлов на
            // каждом шаге — это сотня обходов папки ради одного нового профиля.
            var all = LoadAll();
            if (NameInUse(all, baseName, null) == null) return baseName;
            for (int i = 2; i < 100; i++)
            {
                string candidate = baseName + " " + i;
                if (NameInUse(all, candidate, null) == null) return candidate;
            }
            return baseName + " " + Guid.NewGuid().ToString("N").Substring(0, 4);
        }
    }
}
