// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DIIISPL
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
        public int Order = 0;                 // порядок в списке, задаётся пользователем
        public List<LayoutDisplay> Displays = new List<LayoutDisplay>();

#if DEV
        public TaskbarSettings Taskbar = new TaskbarSettings();   // вид панели задач по состояниям
#endif

        /// <summary>
        /// Секция [taskbar] как она лежит в файле.
        ///
        /// В стабильной сборке панели задач нет, а профиль ей всё равно читать и
        /// писать. Без этого списка первое же сохранение выкинуло бы из файла всё,
        /// чего сборка не знает, — и человек, поработавший в Dev, а потом в Stable,
        /// молча потерял бы настройки вида панели. Поэтому строки сохраняются как
        /// есть и возвращаются на место нетронутыми.
        /// </summary>
        private readonly List<string> _taskbarRaw = new List<string>();

        /// <summary>
        /// Прежний формат: одно поле `taskbar` прямо в секции [profile]. Сборка Dev
        /// разбирает его и переписывает по-новому, а стабильная не знает и про него —
        /// значит, обязана вернуть в файл как было. Иначе профиль, записанный старой
        /// версией, теряет вид панели при первом же сохранении в Stable.
        /// </summary>
        private string _taskbarLegacy;

        /// <summary>
        /// Файл, из которого профиль прочитан. Пусто — профиль ещё нигде не лежит.
        ///
        /// Имя профиля и имя его файла — разные вещи: файл могли скопировать
        /// руками, и тогда «Copy.layout» содержит «Дом». Удаление и переименование,
        /// считавшие путь из имени, в этом случае били мимо — и удаляли чужой
        /// профиль-тёзку, оставляя выбранный на месте. Мимо корзины, без возврата.
        /// </summary>
        public string SourcePath;

        /// <summary>
        /// Налезают ли включённые мониторы друг на друга. Возвращает объяснение
        /// или null.
        ///
        /// Живёт здесь, а не на холсте, потому что спрашивают об этом двое: окно —
        /// чтобы не дать применить, и Applier — чтобы не применить то, что пришло
        /// мимо окна: горячей клавишей, из меню значка, из файла, поправленного
        /// руками. Windows такую раскладку не примет и расставит экраны по-своему.
        /// </summary>
        public string OverlapProblem()
        {
            var on = new List<LayoutDisplay>();
            foreach (var d in Displays) if (d.Enabled) on.Add(d);

            for (int i = 0; i < on.Count; i++)
                for (int j = i + 1; j < on.Count; j++)
                {
                    var a = on[i];
                    var b = on[j];
                    if (a.CloneGroup >= 0 && a.CloneGroup == b.CloneGroup) continue;   // дубль лежит поверх намеренно
                    if (a.X < b.X + b.DesktopWidth && b.X < a.X + a.DesktopWidth &&
                        a.Y < b.Y + b.DesktopHeight && b.Y < a.Y + a.DesktopHeight)
                        return "«" + a.Name + "» и «" + b.Name + "» налезают друг на друга — разведите их: " +
                               "рабочие столы наложенными быть не могут.";
                }
            return null;
        }

        /// <summary>
        /// Описывает ли профиль то, что стоит на экранах прямо сейчас. Этим
        /// отмечается действующая раскладка — точкой в списке и в меню значка.
        ///
        /// Сверка строгая: профиль, который просит монитор, а монитора на месте
        /// нет, действующим не является. Иначе точка загорается сразу у
        /// нескольких профилей — у «Одного монитора», «Двух» и «Трёх» разом, —
        /// потому что раскладка из одного экрана вкладывается в каждый из них.
        ///
        /// HDR не учитываем: его можно переключить мимо программы, и раскладкой
        /// он не является.
        /// </summary>
        public bool MatchesCurrent(List<MonitorInfo> live)
        {
            return Matches(live, false);
        }

        /// <summary>
        /// Изменит ли применение профиля хоть что-нибудь. Отвечает на другой
        /// вопрос, чем <see cref="MatchesCurrent"/>, и потому смотрит мягче:
        /// монитор, которого нет на месте, применение всё равно пропускает — и
        /// требовать его присутствия здесь значит требовать невозможного.
        ///
        /// На этом держится быстрый выход в Applier. Пока обе вещи спрашивались
        /// одним вопросом, он не срабатывал никогда, если хоть один монитор
        /// профиля был отключён: каждое нажатие уже нажатой горячей клавиши
        /// перестраивало дисплеи заново.
        /// </summary>
        public bool ChangesNothing(List<MonitorInfo> live)
        {
            return Matches(live, true);
        }

        private bool Matches(List<MonitorInfo> live, bool ignoreAbsent)
        {
            var active = new Dictionary<string, MonitorInfo>();
            foreach (var m in live) if (m.Active) active[m.Identity] = m;

            var known = new Dictionary<string, MonitorInfo>();
            foreach (var m in live) known[m.Identity] = m;

            var present = new List<LayoutDisplay>();
            int wanted = 0;
            foreach (var d in Displays)
            {
                if (!d.Enabled) continue;
                wanted++;

                MonitorInfo mon;

                // Выдернутый монитор из сверки выпадает: включить его нельзя, и
                // применение его пропустит.
                if (!known.TryGetValue(d.Identity, out mon)) continue;

                // А подключённый, но погашенный — не выпадает ни в коем случае.
                // Это разные вещи, и путать их дорого: телевизор в спящем режиме,
                // монитор с выключенным питанием, выход, который Windows погасила
                // после неудачной попытки, — profile обязан их зажечь. Пока оба
                // случая отсеивались вместе, нажатие горячей клавиши тихо
                // рапортовало «готово» и не делало ничего: до лестницы попыток,
                // которая как раз и умеет будить погашенные выходы, дело не
                // доходило вовсе.
                if (!mon.Active) return false;

                present.Add(d);
            }

            // Строгий вопрос — «это ли стоит на экранах» — требует, чтобы все
            // заказанные мониторы были на месте. Мягкий — «изменит ли что-нибудь
            // применение» — не требует: отсутствующий монитор применение всё
            // равно пропустит.
            if (!ignoreAbsent && wanted != present.Count) return false;
            if (present.Count == 0 || present.Count != active.Count) return false;

            var groupSource = new Dictionary<int, uint>();
            var soloSources = new List<uint>();

            foreach (var d in present)
            {
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

            // Основной экран обязан совпадать: без этой проверки раскладка из двух
            // одинаковых мониторов считалась бы совпавшей и с переставленным основным —
            // позиции-то сверяются относительно него самого.
            //
            // Но сверять надо по группе, а не по каждому выходу. «Основной» здесь
            // означает «начало координат», а Windows помечает им **каждый** выход,
            // стоящий в (0,0), — в дубле это сразу оба. Профиль же хранит ровно один:
            // FromCurrent нормализует, да и переключатель «Основной» в окне ставит один.
            // Пока сверка шла по каждому выходу, профиль с дублем в начале координат
            // не совпадал с живой картиной никогда — а значит не работал быстрый выход
            // выше, и повторное нажатие горячей клавиши каждый раз перестраивало
            // дисплеи заново, моргая экраном и дёргая HDR.
            LayoutDisplay primary = null;
            foreach (var d in present) if (d.Primary) { primary = d; break; }

            if (primary != null)
            {
                if (!active[primary.Identity].Primary) return false;

                // Лишние «основные» допустимы только как спутники той же группы дубля.
                foreach (var d in present)
                {
                    if (ReferenceEquals(d, primary)) continue;
                    if (!active[d.Identity].Primary) continue;
                    if (d.CloneGroup < 0 || d.CloneGroup != primary.CloneGroup) return false;
                }
            }

            // позиции сверяем относительно основного экрана — сам сдвиг значения не имеет
            LayoutDisplay origin = null;
            foreach (var d in present) if (d.Primary) { origin = d; break; }
            if (origin == null) foreach (var d in present) { origin = d; break; }
            if (origin == null) return false;

            MonitorInfo originMon = active[origin.Identity];

            // Начало координат обязано стоять в (0,0) — там его поставит Submit.
            //
            // Без этой проверки сверка была чисто относительной и общего сдвига
            // не видела вовсе. Измерено: профиль, где основным отмечен выдернутый
            // сейчас телевизор, считался «применять нечего», хотя применение
            // перенесло бы начало координат на другой монитор — то есть панель
            // задач и «Пуск» переехали бы на соседний экран.
            if (originMon.X != 0 || originMon.Y != 0) return false;

            int dx = originMon.X - origin.X, dy = originMon.Y - origin.Y;
            foreach (var d in present)
            {
                MonitorInfo mon = active[d.Identity];
                if (mon.X != d.X + dx || mon.Y != d.Y + dy) return false;
            }
            return true;
        }

        /// <summary>
        /// Перенять из старого профиля всё, что не является раскладкой: сочетание
        /// клавиш, место в списке и вид панели задач. Нужно при пересъёмке раскладки
        /// поверх существующего профиля — раскладка новая, остальное чужим не бывает.
        /// </summary>
        public void AdoptSettingsFrom(Layout old)
        {
            Hotkey = old.Hotkey;
            Order = old.Order;
#if DEV
            Taskbar = old.Taskbar.Copy();
#endif
            _taskbarRaw.Clear();
            _taskbarRaw.AddRange(old._taskbarRaw);
            _taskbarLegacy = old._taskbarLegacy;

            // И файл, в котором профиль лежит: он тоже «не раскладка». Без этого
            // пересъёмка профиля, пришедшего из файла с другим именем внутри,
            // плодила двойника.
            SourcePath = old.SourcePath;
        }

        /// <summary>
        /// Вернуть содержимое к тому, что лежит в другом профиле. Сам объект при этом
        /// остаётся тем же: на него ссылается список профилей, и подмена ссылки
        /// заставила бы список перестраиваться посреди переключения.
        /// </summary>
        public void RestoreFrom(Layout src)
        {
            Name = src.Name;
            Hotkey = src.Hotkey;
            Order = src.Order;

            Displays.Clear();
            foreach (var d in src.Displays) Displays.Add(d.Clone());

#if DEV
            Taskbar = src.Taskbar.Copy();
#endif
            _taskbarRaw.Clear();
            _taskbarRaw.AddRange(src._taskbarRaw);
            _taskbarLegacy = src._taskbarLegacy;
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
            copy.Order = Order;
#if DEV
            copy.Taskbar = Taskbar.Copy();
#endif
            copy._taskbarRaw.AddRange(_taskbarRaw);
            copy._taskbarLegacy = _taskbarLegacy;
            copy.SourcePath = SourcePath;
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

            layout.DropLoneCloneGroups();
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
            sb.AppendLine("order = " + Order.ToString(CultureInfo.InvariantCulture));
#if !DEV
            // Поле прежнего формата возвращаем на место как было: разобрать его
            // стабильная сборка не умеет, а выбрасывать чужое — не её дело.
            if (!string.IsNullOrEmpty(_taskbarLegacy)) sb.AppendLine("taskbar = " + _taskbarLegacy);
#endif
            sb.AppendLine();
#if DEV
            sb.AppendLine("[taskbar]");
            foreach (string line in Taskbar.Serialize()) sb.AppendLine(line);
#else
            // Стабильная сборка про панель задач ничего не знает и потому ничего
            // о ней не решает: возвращает секцию в файл ровно такой, какой прочла.
            if (_taskbarRaw.Count > 0)
            {
                sb.AppendLine("[taskbar]");
                foreach (string line in _taskbarRaw) sb.AppendLine(line);
            }
#endif
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
            WriteWhole(file, sb.ToString());
        }

        /// <summary>
        /// Записать файл целиком или не тронуть его вовсе.
        ///
        /// `File.WriteAllText` сначала обрезает файл, потом пишет. Если посреди
        /// записи пропадёт питание или программу снимут, на диске останется
        /// половина профиля — а `Layout.Load` на обрывке не падает, он просто
        /// вернёт раскладку без части мониторов, и `Store.LoadAll` покажет её как
        /// исправную. Пишем рядом и подменяем одним движением: Replace на NTFS
        /// либо проходит целиком, либо не проходит.
        /// </summary>
        private static void WriteWhole(string file, string text)
        {
            var encoding = new UTF8Encoding(true);
            if (!File.Exists(file)) { File.WriteAllText(file, text, encoding); return; }

            string temp = file + ".new";
            try
            {
                try
                {
                    File.WriteAllText(temp, text, encoding);
                    // Без запасной копии: она осталась бы лежать рядом с профилями.
                    // Replace без неё — обычная подмена записи в каталоге.
                    File.Replace(temp, file, null);
                }
                catch (IOException)
                {
                    // Replace не работает через границу тома и на сетевых дисках, где
                    // папка профилей вполне может оказаться. Тогда пишем как раньше:
                    // хуже, но лучше, чем не сохранить.
                    File.WriteAllText(file, text, encoding);
                }
                catch (UnauthorizedAccessException)
                {
                    File.WriteAllText(file, text, encoding);
                }
            }
            finally
            {
                // Уборка обязана быть здесь, а не в ветках catch. Прежде она стояла
                // после запасной записи — и на файле «только для чтения» не
                // выполнялась вовсе: запасная запись бросала то же самое
                // исключение, и `.new` оставался лежать навсегда. То есть уборка
                // не работала ровно на том отказе, ради которого catch и написан.
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch { }
            }
        }

        public static Layout Load(string file)
        {
            var layout = new Layout();
            LayoutDisplay cur = null;
            string section = "";

            foreach (string raw in File.ReadAllLines(file, Encoding.UTF8))
            {
                string line = raw.Trim();

                // Секция панели задач запоминается целиком, дословно и раньше всего
                // остального: сборка, которая её не понимает, обязана вернуть в файл
                // всё, а не только то, что похоже на знакомое «ключ = значение».
                //
                // «Раньше всего» — не мелочь. Прежде захват стоял ниже пропуска
                // комментариев и пустых строк, и измерено было так: пометки, которыми
                // человек подписал себе секцию в Dev, после первого же сохранения в
                // Stable исчезали, а отступы съедались. Запоминаем `raw`, а не
                // обрезанное: восстановить отступ иначе неоткуда.
                if (line.StartsWith("["))
                {
                    string name = line.Trim('[', ']').ToLowerInvariant();
                    bool known = name == "profile" || name == "display" || name == "taskbar";

                    // Незнакомая строка в скобках внутри секции панели задач —
                    // это её часть, а не новая секция.
                    //
                    // Прежде такая строка делала две вещи разом: сама в список не
                    // попадала и переводила разбор в выдуманную секцию — то есть
                    // уносила с собой весь хвост секции, молча. Обещание же в том
                    // и состоит, что стабильная сборка про формат этой секции не
                    // знает ничего: любая подсекция, которую заведёт Dev, обязана
                    // вернуться в файл целиком.
                    if (!known && section == "taskbar") { layout._taskbarRaw.Add(raw.TrimEnd()); continue; }

                    section = name;
                    if (section == "display") { cur = new LayoutDisplay(); layout.Displays.Add(cur); }
                    continue;
                }

                if (section == "taskbar") layout._taskbarRaw.Add(raw.TrimEnd());
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string val = line.Substring(eq + 1).Trim();

                if (section == "profile")
                {
                    if (key == "name") layout.Name = val;
                    else if (key == "hotkey") layout.Hotkey = val;
                    else if (key == "order") layout.Order = ParseInt(val);
                    // прежний формат: одно поле на весь профиль
#if DEV
                    else if (key == "taskbar") layout.Taskbar.AdoptLegacy(val);
#else
                    else if (key == "taskbar") layout._taskbarLegacy = val;
#endif
                }
                else if (section == "taskbar")
                {
#if DEV
                    TaskbarState state;
                    if (key == "version") layout.Taskbar.Versioned = true;
                    else if (TaskbarSettings.TryParseKey(key, out state))
                        layout.Taskbar.States[state] = TaskbarAppearance.Parse(val);
#endif
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
            layout.SourcePath = file;

            // Хвостовые пустые строки секции срезаем. Пустая строка перед следующей
            // секцией принадлежит не секции, а разметке файла: Save ставит свою
            // перед каждым [display], и запомненная нарастала поверх неё. Измерено:
            // файл прибавлял по строке за каждое сохранение — а сохраняет он и по
            // кнопке, и внутри «Применить», и при перестановке в списке. Внутренние
            // пустые строки оставляем: ими человек разделяет свои пометки.
            while (layout._taskbarRaw.Count > 0 &&
                   layout._taskbarRaw[layout._taskbarRaw.Count - 1].Length == 0)
                layout._taskbarRaw.RemoveAt(layout._taskbarRaw.Count - 1);

            layout.DropLoneCloneGroups();
#if DEV
            if (!layout.Taskbar.Versioned) layout.Taskbar.DropUntouchedSpecialCases();
#endif
            return layout;
        }

        /// <summary>
        /// Группа дублирования из одного монитора — не дублирование.
        ///
        /// Такое остаётся в профилях, записанных прежними версиями: выход выбывал
        /// из группы, а номер у соседа сохранялся, и одинокий монитор потом всю жизнь
        /// ходил с меткой «Дубль». Чистится и при чтении файла, и при снятии раскладки
        /// с системы — иначе достаточно одного старого профиля, чтобы метка вернулась.
        /// </summary>
        public void DropLoneCloneGroups()
        {
            foreach (var d in Displays) if (!d.Enabled) d.CloneGroup = -1;

            var counts = new Dictionary<int, int>();
            foreach (var d in Displays)
            {
                if (d.CloneGroup < 0) continue;
                int c; counts.TryGetValue(d.CloneGroup, out c); counts[d.CloneGroup] = c + 1;
            }
            foreach (var d in Displays)
                if (d.CloneGroup >= 0 && counts[d.CloneGroup] < 2) d.CloneGroup = -1;
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
