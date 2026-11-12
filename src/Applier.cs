// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;

namespace DIIISPL
{
    internal class ApplyResult
    {
        public bool Success;
        public string Error;
        public List<string> Warnings = new List<string>();
        /// <summary>Мониторы, которые реально попали в заявку (были подключены на момент сборки).</summary>
        public List<string> Requested = new List<string>();
    }

    /// <summary>
    /// Превращает профиль в конфигурацию дисплеев: сам собирает пути и режимы CCD,
    /// не опираясь на то, что Windows запомнила про эти мониторы.
    /// </summary>
    internal static class Applier
    {
        private class Group
        {
            public string Key;
            public LayoutDisplay Rep;                       // представитель группы (задаёт позицию и режим)
            public List<LayoutDisplay> Members = new List<LayoutDisplay>();
            public List<MonitorInfo> Monitors = new List<MonitorInfo>();
            public uint SourceId;
            public int ModeIndex;
        }

        /// <summary>Пауза между ступенями лестницы попыток внутри одного применения.</summary>
        private const int RungPauseMs = 600;

        public static ApplyResult Apply(Layout layout)
        {
            // Очередь и пауза — общие на все процессы сеанса, см. ApplyGate.
            // Затвор берётся до первого взгляда на дисплеи: иначе двое, начавших
            // одновременно, оба увидят «раскладка уже такая» и оба пойдут дальше.
            using (ApplyGate gate = ApplyGate.Enter())
            {
                // Не пропустили — значит соседняя копия всё ещё перестраивает дисплеи.
                // Идти дальше нельзя: два одновременных SetDisplayConfig это ровно та
                // нагрузка, ради которой затвор и стоит. Раньше по истечении ожидания
                // шли как ни в чём не бывало.
                if (!gate.Held)
                {
                    var busy = new ApplyResult();
                    busy.Error = Text.AlreadyApplying;
                    return busy;
                }

                // Наложенную раскладку Windows не примет: она расставит экраны
                // по-своему, а профиль останется с невозможными координатами.
                // Проверка стоит здесь, а не только в окне: горячая клавиша, меню
                // значка и файл, поправленный руками, идут мимо окна.
                string overlap = layout.OverlapProblem();
                if (overlap != null)
                {
                    var bad = new ApplyResult();
                    bad.Error = overlap;
                    return bad;
                }

                try { return Apply(layout, gate); }
                finally { gate.Stamp(); }
            }
        }

        private static ApplyResult Apply(Layout layout, ApplyGate gate)
        {
            var res = new ApplyResult();

            // Раскладка уже ровно такая — дисплеи не трогаем.
            //
            // Каждый вызов SetDisplayConfig пересогласовывает вывод: экран моргает,
            // а на мониторе с HDR это ещё и заметный скачок яркости. Раньше повторное
            // применение того же профиля проходило полный путь до конца — строгая заявка
            // отклонялась, срабатывал запасной вариант с послаблениями, — и каждое
            // нажатие уже нажатой горячей клавиши мигало экраном на ровном месте.
            //
            // Вид панели задач и HDR при этом всё равно выставляем: они могли разъехаться
            // сами по себе, а стоят дёшево — HDR трогается только там, где он отличается.
            // Здесь спрашиваем именно «изменит ли что-нибудь», а не «это ли стоит
            // на экранах»: вопросы разные. Профиль, просящий отключённый сейчас
            // телевизор, действующим не считается — точка в списке у него не
            // горит, — но применять его всё равно нечего: этот монитор заявка
            // пропустит, а остальное уже стоит как надо.
            if (layout.ChangesNothing(Monitors.Enumerate()))
            {
                res.Success = true;
                ApplyHdr(layout, res, gate);
                ApplyTaskbar(layout, res);
                return res;
            }

            // Перестройка конфигурации дисплеев — тяжёлая операция в ядре графического стека.
            // Частые подряд идущие смены (зажатая горячая клавиша, скрипт в цикле) способны
            // подвесить или уронить видеодрайвер, поэтому выдерживаем паузу между применениями.
            gate.Wait();

            // Выход, входящий в дублирование, получает от драйвера другой target id: у телевизора
            // это 16781568 вместо 4352. Пары «обычных» id не существует, поэтому собранная вручную
            // конфигурация теряет второй экран. Если дубль ещё не собран — пусть его построит
            // Windows, а мы затем наложим точную раскладку по идентификаторам, годным в дубле.
            if (HasClone(layout) && !ClonesFormed(layout))
            {
                Native.SetDisplayTopology(0, IntPtr.Zero, 0, IntPtr.Zero, Native.SDC_APPLY | Native.SDC_TOPOLOGY_CLONE);
                System.Threading.Thread.Sleep(1300);
            }

            int err = Submit(layout, res);
            if (res.Error != null) return res;

            if (err != 0)
            {
                // Из некоторых состояний система отвергает любую заявку целиком.
                // Сбрасываем топологию в заведомо рабочую и пробуем ещё раз.
                Native.SetDisplayTopology(0, IntPtr.Zero, 0, IntPtr.Zero, Native.SDC_APPLY | Native.SDC_TOPOLOGY_EXTEND);
                System.Threading.Thread.Sleep(1400);
                res.Warnings.Clear();
                err = Submit(layout, res);
                if (res.Error != null) return res;
            }
            if (err != 0)
            {
                res.Error = Text.WindowsRefused(Native.ErrorText(err));
                return res;
            }
            res.Success = true;

            // Погашенный выход первая попытка поднимает не всегда: Windows применяет остальное
            // и рапортует успех. Поэтому сверяем результат и, если надо, настаиваем.
            var missing = Missing(layout, res, 1300);
            if (missing.Count > 0)
            {
                res.Warnings.Clear();
                NoteLate(Submit(layout, res), res);
                missing = Missing(layout, res, 900);
            }

            // Отдельная беда — дублирование: некоторые мониторы (телевизоры особенно) получают
            // другой target id, когда входят в дубль, и пары «обычных» id просто не существует.
            // Поэтому просим Windows построить дублирование штатно, а затем накладываем
            // точную раскладку — уже по свежим идентификаторам выходов.
            if (missing.Count > 0 || !ClonesFormed(layout))
            {
                uint topology = HasClone(layout) ? Native.SDC_TOPOLOGY_CLONE : Native.SDC_TOPOLOGY_EXTEND;
                Native.SetDisplayTopology(0, IntPtr.Zero, 0, IntPtr.Zero, Native.SDC_APPLY | topology);
                System.Threading.Thread.Sleep(1500);
                res.Warnings.Clear();
                NoteLate(Submit(layout, res), res);
                System.Threading.Thread.Sleep(600);
            }

            Verify(layout, res);
            ApplyHdr(layout, res, gate);
            ApplyTaskbar(layout, res);
            return res;
        }

        /// <summary>
        /// Уточняющие заявки идут уже после того, как Success выставлен, и их код
        /// возврата прежде не читал никто: раскладка могла остаться расставленной
        /// самой Windows, а `DIIISPL.exe apply X` в скрипте всё равно печатал
        /// «Применено» и возвращал ноль. Опустить Success тут нельзя — часть
        /// раскладки применена, — но промолчать тоже нельзя.
        /// </summary>
        private static void NoteLate(int err, ApplyResult res)
        {
            if (res.Error != null) { res.Warnings.Add(res.Error); res.Error = null; }
            if (err == 0) return;
            res.Warnings.Add(Text.WindowsPartly(Native.ErrorText(err)));
        }

        /// <summary>
        /// Передать наблюдателю настройки панели из профиля. Кеш сбрасывается обязательно:
        /// смена раскладки пересоздаёт панели задач, и всё, что мы помнили об их окнах
        /// и о связи со службой в проводнике, после этого недействительно.
        ///
        /// В стабильной сборке панели задач нет вовсе — ни экрана настроек, ни
        /// наблюдателя, ни библиотеки внутри проводника, — поэтому применять нечего.
        /// </summary>
        private static void ApplyTaskbar(Layout layout, ApplyResult res)
        {
#if DEV
            TaskbarWatcher.ResetCache();

            if (layout.Taskbar == null || !layout.Taskbar.AnyEnabled) { TaskbarWatcher.Use(null); TaskbarWatcher.ResetSoon(); return; }
            if (!ExplorerTap.Available)
            {
                res.Warnings.Add(Text.NoTaskbarLibrary);
                return;
            }

            TaskbarWatcher.Use(layout.Taskbar);

            // И ещё раз, когда панели улягутся: пересоздаёт их Windows не сразу,
            // и всё, что наблюдатель успел о них запомнить между делом, — неправда.
            TaskbarWatcher.ResetSoon();
            if (ExplorerTap.LastError != null)
                res.Warnings.Add(Text.TaskbarPrefix(ExplorerTap.LastError));
#endif
        }

        private static bool HasClone(Layout layout)
        {
            foreach (var d in layout.Displays)
                if (d.Enabled && d.CloneGroup >= 0 && IsCloned(layout, d.Identity)) return true;
            return false;
        }

        /// <summary>Сложились ли группы дублирования: все участники обязаны сидеть на одном источнике.</summary>
        private static bool ClonesFormed(Layout layout)
        {
            if (!HasClone(layout)) return true;
            var byId = new Dictionary<string, MonitorInfo>();
            foreach (var m in Monitors.Enumerate()) byId[m.Identity] = m;

            var groupSource = new Dictionary<int, uint>();
            foreach (var d in layout.Displays)
            {
                if (!d.Enabled || d.CloneGroup < 0) continue;
                MonitorInfo mon;

                // Два разных случая, и путать их нельзя.
                //
                // Монитора нет в перечислении — «складывать не из чего», пропускаем.
                // Прежде здесь стоял отказ, и отказ этот был вечным: монитор не
                // подключён, значит условие не выполнится никогда, и каждое
                // применение гнало систему через два лишних перестроения топологии.
                if (!byId.TryGetValue(d.Identity, out mon)) continue;

                // А вот подключённый, но погашенный участник — это ровно то
                // состояние, ради которого дублирование и строится силами Windows:
                // выход в дубле получает другой target id, вручную собранная
                // конфигурация его не поднимает, и экран остаётся тёмным. Считать
                // это «дубль сложился» нельзя.
                if (!mon.Active) return false;
                uint src;
                if (!groupSource.TryGetValue(d.CloneGroup, out src)) groupSource[d.CloneGroup] = mon.SourceId;
                else if (src != mon.SourceId) return false;
            }
            return true;
        }

        private static int Submit(Layout layout, ApplyResult res)
        {
            var monitors = Monitors.Enumerate();
            var byId = new Dictionary<string, MonitorInfo>();
            foreach (var m in monitors) byId[m.Identity] = m;

            // --- участники ---
            var groups = new List<Group>();
            var groupByKey = new Dictionary<string, Group>();

            foreach (var d in layout.Displays)
            {
                if (!d.Enabled) continue;
                MonitorInfo mon;
                if (!byId.TryGetValue(d.Identity, out mon))
                {
                    res.Warnings.Add(Text.SkippedNotConnected(d.Name ?? d.Identity));
                    continue;
                }
                string key = d.CloneGroup >= 0 ? "g" + d.CloneGroup : "s" + d.Identity;
                Group g;
                if (!groupByKey.TryGetValue(key, out g))
                {
                    g = new Group();
                    g.Key = key;
                    g.Rep = d;
                    groupByKey[key] = g;
                    groups.Add(g);
                }
                g.Members.Add(d);
                g.Monitors.Add(mon);
                if (!res.Requested.Contains(d.Identity)) res.Requested.Add(d.Identity);
            }

            if (groups.Count == 0)
            {
                res.Error = Text.NoneConnected;
                return -1;
            }

            Native.DISPLAYCONFIG_PATH_INFO[] allPaths;
            Native.DISPLAYCONFIG_MODE_INFO[] allModes;
            if (!Monitors.QueryAllPaths(out allPaths, out allModes))
            {
                res.Error = Text.NoPathList;
                return -1;
            }

            // --- раздаём source id: одна группа = один рабочий стол ---
            var usedSources = new Dictionary<string, bool>();
            foreach (var g in groups)
            {
                var candidates = new List<uint>(g.Monitors[0].ValidSourceIds);
                for (int i = 1; i < g.Monitors.Count; i++)
                {
                    var keep = new List<uint>();
                    foreach (uint s in candidates) if (g.Monitors[i].ValidSourceIds.Contains(s)) keep.Add(s);
                    candidates = keep;
                }
                candidates.Sort();

                // Сперва пробуем источник, на котором эти выходы работают прямо сейчас:
                // видеокарта поддерживает не любое сочетание «источник + пара выходов»,
                // и наугад взятый наименьший свободный номер может оказаться нерабочим.
                uint current = 0;
                bool hasCurrent = g.Monitors[0].Active;
                if (hasCurrent)
                {
                    current = g.Monitors[0].SourceId;
                    for (int i = 1; i < g.Monitors.Count && hasCurrent; i++)
                        if (!g.Monitors[i].Active || g.Monitors[i].SourceId != current) hasCurrent = false;
                }
                if (hasCurrent && candidates.Contains(current))
                {
                    candidates.Remove(current);
                    candidates.Insert(0, current);
                }

                bool assigned = false;
                foreach (uint s in candidates)
                {
                    string ukey = g.Monitors[0].Adapter.Key + "/" + s;
                    if (usedSources.ContainsKey(ukey)) continue;
                    g.SourceId = s;
                    usedSources[ukey] = true;
                    assigned = true;
                    break;
                }
                if (!assigned)
                {
                    res.Error = Text.NoFreeDesktop(g.Rep.Name);
                    return -1;
                }
            }

            // --- режимы источников (по одному на группу) ---
            var modes = new List<Native.DISPLAYCONFIG_MODE_INFO>();
            foreach (var g in groups)
            {
                var mode = new Native.DISPLAYCONFIG_MODE_INFO();
                mode.infoType = Native.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE;
                mode.id = g.SourceId;
                mode.adapterId = g.Monitors[0].Adapter;
                // режим источника задаётся в «родной» ориентации панели: поворот Windows
                // применяет поверх него сама, и повёрнутые стороны здесь ей не нужны
                mode.modeInfo.sourceMode.width = (uint)Math.Max(640, g.Rep.Width);
                mode.modeInfo.sourceMode.height = (uint)Math.Max(480, g.Rep.Height);
                mode.modeInfo.sourceMode.pixelFormat = Native.DISPLAYCONFIG_PIXELFORMAT_32BPP;
                mode.modeInfo.sourceMode.position.x = g.Rep.X;
                mode.modeInfo.sourceMode.position.y = g.Rep.Y;
                g.ModeIndex = modes.Count;
                modes.Add(mode);
            }

            // --- основной монитор обязан стоять в (0,0) ---
            // `break` из внутреннего цикла выходит только из него: перебор групп
            // продолжался, и найденной оказывалась последняя группа с основным
            // монитором, а не первая. Сегодня основной ровно один, так что вреда
            // не было, — но написано было не то, что имелось в виду.
            Group primary = null;
            foreach (var g in groups)
            {
                foreach (var d in g.Members)
                    if (d.Primary) { primary = g; break; }
                if (primary != null) break;
            }
            if (primary == null) primary = groups[0];

            int dx = -modes[primary.ModeIndex].modeInfo.sourceMode.position.x;
            int dy = -modes[primary.ModeIndex].modeInfo.sourceMode.position.y;
            if (dx != 0 || dy != 0)
            {
                for (int i = 0; i < modes.Count; i++)
                {
                    var m = modes[i];
                    m.modeInfo.sourceMode.position.x += dx;
                    m.modeInfo.sourceMode.position.y += dy;
                    modes[i] = m;
                }
            }

            // --- пути ---
            var paths = new List<Native.DISPLAYCONFIG_PATH_INFO>();
            foreach (var g in groups)
            {
                for (int i = 0; i < g.Members.Count; i++)
                {
                    LayoutDisplay d = g.Members[i];
                    MonitorInfo mon = g.Monitors[i];

                    Native.DISPLAYCONFIG_PATH_INFO path;
                    if (!FindTemplate(allPaths, mon, g.SourceId, out path))
                    {
                        res.Warnings.Add(Text.CannotAttach(d.Name));
                        continue;
                    }

                    path.sourceInfo.adapterId = mon.Adapter;
                    path.sourceInfo.id = g.SourceId;
                    path.sourceInfo.modeInfoIdx = (uint)g.ModeIndex;
                    path.sourceInfo.statusFlags = 0;

                    path.targetInfo.adapterId = mon.Adapter;
                    path.targetInfo.id = mon.TargetId;
                    path.targetInfo.modeInfoIdx = Native.DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
                    path.targetInfo.rotation = d.Rotation == 0 ? Native.DISPLAYCONFIG_ROTATION_IDENTITY : d.Rotation;
                    path.targetInfo.scaling = Native.DISPLAYCONFIG_SCALING_PREFERRED;
                    path.targetInfo.scanLineOrdering = Native.DISPLAYCONFIG_SCANLINE_ORDERING_PROGRESSIVE;
                    path.targetInfo.refreshRate = ToRational(d.Hz);
                    path.targetInfo.targetAvailable = 1;
                    path.targetInfo.statusFlags = 0;

                    path.flags = Native.DISPLAYCONFIG_PATH_ACTIVE;
                    paths.Add(path);
                }
            }

            if (paths.Count == 0)
            {
                res.Error = Text.NothingUsable;
                return -1;
            }

            // --- применяем, с постепенным ослаблением требований ---
            var pathArr = paths.ToArray();
            var modeArr = modes.ToArray();

            uint baseFlags = Native.SDC_APPLY | Native.SDC_USE_SUPPLIED_DISPLAY_CONFIG | Native.SDC_SAVE_TO_DATABASE;

            // Строго: без SDC_ALLOW_CHANGES Windows либо делает ровно то, что просят, либо отказывает.
            // С этим флагом она вправе «поправить» раскладку — например, молча выкинуть пути
            // к погашенным выходам и вернуть успех, чего мы как раз не хотим.
            // SDC_FORCE_MODE_ENUMERATION заставляет драйвер заново перечислить все режимы —
            // операция тяжёлая, поэтому она не в первой попытке, а в запасной.
            // Между ступенями — пауза. Отказ приходит быстро, поэтому без неё четыре
            // заявки подряд уходят в драйвер почти вплотную: пауза в 1,2 секунды стоит
            // перед всем применением и внутри лестницы не действует. А тяжелее всего
            // как раз ступени с SDC_FORCE_MODE_ENUMERATION — драйвер перечисляет режимы
            // заново, и именно такой поток запросов ронял машину.
            int err = Native.SetDisplayConfig((uint)pathArr.Length, pathArr, (uint)modeArr.Length, modeArr, baseFlags);
            if (err != 0)
            {
                System.Threading.Thread.Sleep(RungPauseMs);
                err = Native.SetDisplayConfig((uint)pathArr.Length, pathArr, (uint)modeArr.Length, modeArr,
                        baseFlags | Native.SDC_FORCE_MODE_ENUMERATION);
            }

            if (err != 0)
            {
                // разрешаем системе подобрать режимы самой
                // расхождения, если они есть, покажет сверка ниже — internals пользователю не нужны
                System.Threading.Thread.Sleep(RungPauseMs);
                err = Native.SetDisplayConfig((uint)pathArr.Length, pathArr, (uint)modeArr.Length, modeArr,
                        baseFlags | Native.SDC_ALLOW_CHANGES | Native.SDC_FORCE_MODE_ENUMERATION);
            }
            if (err != 0)
            {
                // последняя попытка: без явной частоты
                System.Threading.Thread.Sleep(RungPauseMs);
                var relaxed = (Native.DISPLAYCONFIG_PATH_INFO[])pathArr.Clone();
                for (int i = 0; i < relaxed.Length; i++)
                {
                    relaxed[i].targetInfo.refreshRate.Numerator = 0;
                    relaxed[i].targetInfo.refreshRate.Denominator = 0;
                }
                err = Native.SetDisplayConfig((uint)relaxed.Length, relaxed, (uint)modeArr.Length, modeArr,
                        baseFlags | Native.SDC_ALLOW_CHANGES | Native.SDC_FORCE_MODE_ENUMERATION);
                if (err == 0) res.Warnings.Add(Text.DriverPickedRate);
            }
            return err;
        }

        /// <summary>
        /// Кто из заказанных мониторов так и не включился. Сверяем полученное с
        /// заказанным: SetDisplayConfig умеет вернуть успех, применив не то, о чём
        /// просили, — особенно если монитор физически погашен.
        ///
        /// Монитор, пропавший из перечисления,
        /// тоже считается недостающим: после неудачной попытки Windows гасит выход, и он
        /// временно исчезает из списка — раньше это принималось за «всё в порядке».
        /// </summary>
        private static List<string> Missing(Layout layout, ApplyResult res, int settleMs)
        {
            System.Threading.Thread.Sleep(settleMs);
            var byId = new Dictionary<string, MonitorInfo>();
            foreach (var m in Monitors.Enumerate()) byId[m.Identity] = m;

            var missing = new List<string>();
            foreach (var d in layout.Displays)
            {
                if (!d.Enabled || !res.Requested.Contains(d.Identity)) continue;
                MonitorInfo mon;
                if (!byId.TryGetValue(d.Identity, out mon) || !mon.Active) missing.Add(d.Name);
            }
            return missing;
        }

        private static void Verify(Layout layout, ApplyResult res)
        {
            System.Threading.Thread.Sleep(300);

            var byId = new Dictionary<string, MonitorInfo>();
            foreach (var m in Monitors.Enumerate()) byId[m.Identity] = m;

            var notTurnedOn = new List<string>();
            var notTurnedOff = new List<string>();

            foreach (var d in layout.Displays)
            {
                MonitorInfo mon;
                bool known = byId.TryGetValue(d.Identity, out mon);

                if (!d.Enabled)
                {
                    if (known && mon.Active) notTurnedOff.Add(d.Name);
                    continue;
                }
                if (!known) continue;                       // про неподключённые уже сказали выше
                if (!mon.Active) { notTurnedOn.Add(d.Name); continue; }

                if (mon.Width != d.Width || mon.Height != d.Height)
                    res.Warnings.Add(Text.GotModeInstead(d.Name, mon.Width, mon.Height, d.Width, d.Height));
                else if (d.Hz > 0 && Math.Abs(mon.Hz - d.Hz) > 1.5)
                    res.Warnings.Add(Text.GotRateInstead(d.Name, mon.Hz.ToString("0.##"), d.Hz.ToString("0.##")));

                // Поворот сверяем наравне с режимом. Прежде это была единственная
                // величина, которую программа заказывает и не проверяет, — и молчание
                // тут стоило дорого: сверка «применён сейчас» поворот учитывает, значит
                // точка у профиля не загоралась, быстрый выход не срабатывал никогда,
                // и каждое нажатие горячей клавиши шло на полное перестроение. Человек
                // при этом видел «Раскладка применена» без единого замечания и жал ещё
                // раз, потому что точка не загоралась. Это и есть та повторяемая
                // нагрузка, которой 19.08.2026 машину увело в синий экран.
                if (mon.Rotation != d.Rotation)
                    res.Warnings.Add(Text.RotationNotApplied(d.Name));
            }

            // дублирование проверяем по source id: одна группа обязана сидеть на одном источнике
            var groupSource = new Dictionary<int, uint>();
            foreach (var d in layout.Displays)
            {
                if (!d.Enabled || d.CloneGroup < 0) continue;
                MonitorInfo mon;
                if (!byId.TryGetValue(d.Identity, out mon) || !mon.Active) continue;
                uint src;
                if (!groupSource.TryGetValue(d.CloneGroup, out src)) groupSource[d.CloneGroup] = mon.SourceId;
                else if (src != mon.SourceId)
                    res.Warnings.Add(Text.NotCloned(d.Name));
            }

            // Позиции и основной экран — то единственное, что расходится, когда
            // заявку отбило и раскладку расставила сама Windows. Прежде их никто
            // не сверял: в окне писалось «Раскладка применена», а на столе стояло
            // не то, и профиль после этого никогда не отмечался точкой «применён
            // сейчас» — потому что применено было другое.
            // Про невключившиеся и невыключившиеся говорим здесь, до выбора начала
            // координат. Прежде эти две строки стояли ниже, а между ними и набором
            // был выход «сверять нечего» — и профиль, у которого не зажёгся ни один
            // монитор, уходил вообще без предупреждений: «Раскладка применена» и всё.
            if (notTurnedOn.Count > 0)
                res.Warnings.Add(Text.DidNotTurnOn(string.Join(", ", notTurnedOn.ToArray())));
            if (notTurnedOff.Count > 0)
                res.Warnings.Add(Text.StayedOn(string.Join(", ", notTurnedOff.ToArray())));

            // Начало координат выбираем тем же правилом, каким его выбирает Submit:
            // основной, если он подключён и горит, иначе первый горящий. Иначе
            // получаются два разных начала на одну раскладку, и сверка ругается
            // на верно применённый профиль — измерено на профиле, где основным
            // отмечен отключённый телевизор: «Windows расставила по-своему».
            LayoutDisplay origin = null;
            foreach (var d in layout.Displays)
            {
                if (!d.Enabled || !d.Primary) continue;
                MonitorInfo p;
                if (byId.TryGetValue(d.Identity, out p) && p.Active) { origin = d; break; }
            }
            if (origin == null)
                foreach (var d in layout.Displays)
                {
                    if (!d.Enabled) continue;
                    MonitorInfo p;
                    if (byId.TryGetValue(d.Identity, out p) && p.Active) { origin = d; break; }
                }
            if (origin == null) return;                  // сверять нечего: ничего не горит

            MonitorInfo originMon = byId[origin.Identity];
            int shiftX = originMon.X - origin.X;
            int shiftY = originMon.Y - origin.Y;

            // Про основной говорим только тогда, когда заказанный основной на месте
            // и горит, а Windows всё равно поставила началом координат другой.
            if (origin.Primary && (originMon.X != 0 || originMon.Y != 0))
                res.Warnings.Add(Text.NotPrimary(origin.Name));

            var movedNames = new List<string>();
            foreach (var d in layout.Displays)
            {
                if (!d.Enabled) continue;
                MonitorInfo mon;
                if (!byId.TryGetValue(d.Identity, out mon) || !mon.Active) continue;
                if (mon.X - d.X != shiftX || mon.Y - d.Y != shiftY) movedNames.Add(d.Name);
            }
            if (movedNames.Count > 0)
                res.Warnings.Add(Text.WindowsMoved(string.Join(", ", movedNames.ToArray())));

        }

        /// <summary>
        /// HDR ставится отдельно и уже после того, как топология применена: при смене раскладки
        /// (особенно при дублировании) Windows пересобирает цепочку вывода и гасит расширенный цвет.
        /// В группе дублирования состояние HDR должно быть одинаковым на всех выходах, иначе
        /// Windows принудительно переводит всю группу в SDR — поэтому группу выставляем целиком.
        /// </summary>
        public static void ApplyHdr(Layout layout, ApplyResult res, ApplyGate gate)
        {
            var wanted = new Dictionary<string, bool>();     // identity -> нужное состояние
            foreach (var d in layout.Displays)
            {
                if (!d.Enabled || d.Hdr < 0) continue;
                wanted[d.Identity] = d.Hdr == 1;
            }
            if (wanted.Count == 0) return;

            // выравниваем группы дублирования: если HDR просят хотя бы для одного выхода — включаем всей группе
            foreach (var d in layout.Displays)
            {
                if (!d.Enabled || d.CloneGroup < 0) continue;
                bool groupWantsHdr = false;
                foreach (var o in layout.Displays)
                {
                    bool v;
                    if (o.Enabled && o.CloneGroup == d.CloneGroup && wanted.TryGetValue(o.Identity, out v) && v)
                        groupWantsHdr = true;
                }
                if (groupWantsHdr) wanted[d.Identity] = true;
            }

            // мониторы перечитываем: после смены раскладки идентификаторы выходов могли смениться
            var byId = new Dictionary<string, MonitorInfo>();
            foreach (var m in Monitors.Enumerate()) byId[m.Identity] = m;

            var touched = new List<MonitorInfo>();
            var names = new Dictionary<string, string>();
            foreach (var d in layout.Displays) names[d.Identity] = d.Name;

            var cloneWarned = new Dictionary<int, bool>();
            foreach (var kv in wanted)
            {
                MonitorInfo mon;
                if (!byId.TryGetValue(kv.Key, out mon) || !mon.Active) continue;

                if (kv.Value && !mon.HdrSupported)
                {
                    var self = layout.Find(kv.Key);
                    if (self != null && IsCloned(layout, kv.Key))
                    {
                        // проверено: Windows не отдаёт расширенный цвет выходам в дублировании
                        if (!cloneWarned.ContainsKey(self.CloneGroup))
                        {
                            cloneWarned[self.CloneGroup] = true;
                            res.Warnings.Add(Text.HdrNotInClone);
                        }
                    }
                    else res.Warnings.Add(Text.HdrUnavailable(names[kv.Key]));
                    continue;
                }
                // Уже как надо — трогать нечего. Прежде такой монитор всё равно
                // попадал в touched, из-за чего дальше всегда набегала лишняя
                // пауза в 700 мс, а имя списка переставало быть правдой.
                if (mon.HdrEnabled == kv.Value) continue;

                // Переключение расширенного цвета пересогласовывает линк — операция
                // того же класса, что и смена раскладки. Быстрый путь «раскладка уже
                // такая» доходит сюда, минуя паузу, поэтому выдерживаем её здесь и
                // только перед настоящим первым изменением.
                if (gate != null) { gate.Wait(); gate = null; }

                int err = Monitors.SetHdr(mon.Adapter, mon.TargetId, kv.Value);
                if (err != 0)
                {
                    res.Warnings.Add(Text.HdrFailed(names[kv.Key], kv.Value, Native.ErrorText(err)));
                    continue;
                }
                touched.Add(mon);
            }

            if (touched.Count == 0) return;

            // Windows пересогласовывает линк не мгновенно — проверяем результат с паузой.
            // Заодно повторяем: система любит вернуть запомненное состояние HDR после смены режима.
            System.Threading.Thread.Sleep(700);
            foreach (var mon in touched)
            {
                bool want;
                if (!wanted.TryGetValue(mon.Identity, out want)) continue;

                var check = new MonitorInfo();
                check.Adapter = mon.Adapter;
                check.TargetId = mon.TargetId;
                Monitors.ReadHdr(check);

                if (check.HdrEnabled != want && check.HdrSupported)
                {
                    Monitors.SetHdr(mon.Adapter, mon.TargetId, want);
                    System.Threading.Thread.Sleep(600);
                    Monitors.ReadHdr(check);
                }

                if (!want && check.HdrEnabled)
                    res.Warnings.Add(Text.HdrComesBack(names[mon.Identity]));

                if (want && !check.HdrEnabled)
                {
                    // Совета «задайте группе 4K60» здесь больше нет. Он обещал то,
                    // чего не бывает: выходам, объединённым в дублирование, Windows
                    // расширенный цвет не отдаёт вовсе — проверено и на 60 Гц, и на
                    // 120. Об этом же говорит и окно, и автотест; одна эта строка
                    // отправляла человека подбирать режим, который не поможет.
                    res.Warnings.Add(Text.HdrDidNotTurnOn(names[mon.Identity], IsCloned(layout, mon.Identity)));
                }
            }
        }

        private static bool IsCloned(Layout layout, string identity)
        {
            var self = layout.Find(identity);
            if (self == null || self.CloneGroup < 0) return false;
            foreach (var o in layout.Displays)
                if (!ReferenceEquals(o, self) && o.Enabled && o.CloneGroup == self.CloneGroup) return true;
            return false;
        }

        private static bool FindTemplate(Native.DISPLAYCONFIG_PATH_INFO[] all, MonitorInfo mon, uint sourceId,
            out Native.DISPLAYCONFIG_PATH_INFO path)
        {
            // точное совпадение source+target
            foreach (var p in all)
            {
                if (p.targetInfo.adapterId.Key == mon.Adapter.Key && p.targetInfo.id == mon.TargetId &&
                    p.sourceInfo.id == sourceId)
                { path = p; return true; }
            }
            // любой путь к этому монитору — source подменим сами
            foreach (var p in all)
            {
                if (p.targetInfo.adapterId.Key == mon.Adapter.Key && p.targetInfo.id == mon.TargetId)
                { path = p; return true; }
            }
            path = new Native.DISPLAYCONFIG_PATH_INFO();
            return false;
        }

        private static Native.DISPLAYCONFIG_RATIONAL ToRational(double hz)
        {
            var r = new Native.DISPLAYCONFIG_RATIONAL();
            if (hz <= 0) { r.Numerator = 0; r.Denominator = 0; return r; }
            if (Math.Abs(hz - Math.Round(hz)) < 0.01)
            {
                r.Numerator = (uint)Math.Round(hz);
                r.Denominator = 1;
            }
            else
            {
                r.Numerator = (uint)Math.Round(hz * 1000.0);
                r.Denominator = 1000;
            }
            return r;
        }
    }
}
