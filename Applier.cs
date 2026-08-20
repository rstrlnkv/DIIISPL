// DIIIX — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;

namespace DIIIX
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

        private static DateTime _lastApply = DateTime.MinValue;
        private const int MinIntervalMs = 1200;

        public static ApplyResult Apply(Layout layout)
        {
            var res = new ApplyResult();

            // Перестройка конфигурации дисплеев — тяжёлая операция в ядре графического стека.
            // Частые подряд идущие смены (зажатая горячая клавиша, скрипт в цикле) способны
            // подвесить или уронить видеодрайвер, поэтому выдерживаем паузу между применениями.
            double since = (DateTime.UtcNow - _lastApply).TotalMilliseconds;
            if (since < MinIntervalMs) System.Threading.Thread.Sleep((int)(MinIntervalMs - since));
            _lastApply = DateTime.UtcNow;

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
                res.Error = "Windows отклонила раскладку: " + Native.ErrorText(err);
                return res;
            }
            res.Success = true;

            // Погашенный выход первая попытка поднимает не всегда: Windows применяет остальное
            // и рапортует успех. Поэтому сверяем результат и, если надо, настаиваем.
            var missing = Missing(layout, res, 1300);
            if (missing.Count > 0)
            {
                res.Warnings.Clear();
                Submit(layout, res);
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
                Submit(layout, res);
                System.Threading.Thread.Sleep(600);
            }

            Verify(layout, res);
            ApplyHdr(layout, res);
            ApplyTaskbar(layout, res);
            return res;
        }

        /// <summary>
        /// Передать наблюдателю настройки панели из профиля. Кеш сбрасывается обязательно:
        /// смена раскладки пересоздаёт панели задач, и всё, что мы помнили об их окнах
        /// и о связи со службой в проводнике, после этого недействительно.
        /// </summary>
        private static void ApplyTaskbar(Layout layout, ApplyResult res)
        {
            TaskbarWatcher.ResetCache();

            if (layout.Taskbar == null || !layout.Taskbar.AnyEnabled) { TaskbarWatcher.Use(null); return; }
            if (!ExplorerTap.Available)
            {
                res.Warnings.Add("Профиль задаёт вид панели задач, но в программе не хватает файла для этого.");
                return;
            }

            TaskbarWatcher.Use(layout.Taskbar);
            if (ExplorerTap.LastError != null)
                res.Warnings.Add("Панель задач: " + ExplorerTap.LastError);
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
                if (!byId.TryGetValue(d.Identity, out mon) || !mon.Active) return false;
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
                    res.Warnings.Add("«" + (d.Name ?? d.Identity) + "» сейчас не подключён — пропущен.");
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
                res.Error = "Ни один монитор из профиля сейчас не подключён.";
                return -1;
            }

            Native.DISPLAYCONFIG_PATH_INFO[] allPaths;
            Native.DISPLAYCONFIG_MODE_INFO[] allModes;
            if (!Monitors.QueryAllPaths(out allPaths, out allModes))
            {
                res.Error = "Windows не отдала список подключений мониторов.";
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
                    res.Error = "Не удалось разместить «" + g.Rep.Name + "»: видеокарта не даёт столько независимых рабочих столов.";
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
            Group primary = null;
            foreach (var g in groups)
                foreach (var d in g.Members)
                    if (d.Primary) { primary = g; break; }
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
                        res.Warnings.Add("«" + d.Name + "» нельзя подключить к выбранному рабочему столу — пропущен.");
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
                res.Error = "Не получилось составить раскладку: ни один монитор не удалось задействовать.";
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
            int err = Native.SetDisplayConfig((uint)pathArr.Length, pathArr, (uint)modeArr.Length, modeArr, baseFlags);
            if (err != 0)
                err = Native.SetDisplayConfig((uint)pathArr.Length, pathArr, (uint)modeArr.Length, modeArr,
                        baseFlags | Native.SDC_FORCE_MODE_ENUMERATION);

            if (err != 0)
            {
                // разрешаем системе подобрать режимы самой
                // расхождения, если они есть, покажет сверка ниже — internals пользователю не нужны
                err = Native.SetDisplayConfig((uint)pathArr.Length, pathArr, (uint)modeArr.Length, modeArr,
                        baseFlags | Native.SDC_ALLOW_CHANGES | Native.SDC_FORCE_MODE_ENUMERATION);
            }
            if (err != 0)
            {
                // последняя попытка: без явной частоты
                var relaxed = (Native.DISPLAYCONFIG_PATH_INFO[])pathArr.Clone();
                for (int i = 0; i < relaxed.Length; i++)
                {
                    relaxed[i].targetInfo.refreshRate.Numerator = 0;
                    relaxed[i].targetInfo.refreshRate.Denominator = 0;
                }
                err = Native.SetDisplayConfig((uint)relaxed.Length, relaxed, (uint)modeArr.Length, modeArr,
                        baseFlags | Native.SDC_ALLOW_CHANGES | Native.SDC_FORCE_MODE_ENUMERATION);
                if (err == 0) res.Warnings.Add("Частоту выбрал драйвер — заданную Windows не приняла.");
            }
            return err;
        }

        /// <summary>
        /// Сверяем полученное с заказанным: SetDisplayConfig умеет вернуть успех,
        /// применив не то, о чём просили (особенно если монитор физически погашен).
        /// </summary>
        /// <summary>
        /// Кто из заказанных мониторов так и не включился. Монитор, пропавший из перечисления,
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
                    res.Warnings.Add("«" + d.Name + "»: получилось " + mon.Width + "×" + mon.Height +
                                     " вместо " + d.Width + "×" + d.Height + ".");
                else if (d.Hz > 0 && Math.Abs(mon.Hz - d.Hz) > 1.5)
                    res.Warnings.Add("«" + d.Name + "»: частота " + mon.Hz.ToString("0.##") +
                                     " Гц вместо " + d.Hz.ToString("0.##") + " Гц.");
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
                    res.Warnings.Add("«" + d.Name + "» не встал в дублирование — Windows развела экраны.");
            }

            if (notTurnedOn.Count > 0)
                res.Warnings.Add("Не включились: " + string.Join(", ", notTurnedOn.ToArray()) +
                                 ". Чаще всего монитор обесточен или спит так, что видеокарта его не видит.");
            if (notTurnedOff.Count > 0)
                res.Warnings.Add("Остались включёнными вопреки профилю: " + string.Join(", ", notTurnedOff.ToArray()) + ".");
        }

        /// <summary>
        /// HDR ставится отдельно и уже после того, как топология применена: при смене раскладки
        /// (особенно при дублировании) Windows пересобирает цепочку вывода и гасит расширенный цвет.
        /// В группе дублирования состояние HDR должно быть одинаковым на всех выходах, иначе
        /// Windows принудительно переводит всю группу в SDR — поэтому группу выставляем целиком.
        /// </summary>
        public static void ApplyHdr(Layout layout, ApplyResult res)
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
                            res.Warnings.Add("При дублировании HDR не работает. Разведите экраны — тогда его можно включить на каждом.");
                        }
                    }
                    else res.Warnings.Add("«" + names[kv.Key] + "»: этот выход сейчас не отдаёт HDR — включить нечего.");
                    continue;
                }
                if (mon.HdrEnabled == kv.Value) { touched.Add(mon); continue; }

                int err = Monitors.SetHdr(mon.Adapter, mon.TargetId, kv.Value);
                if (err != 0)
                {
                    res.Warnings.Add("«" + names[kv.Key] + "»: не удалось " + (kv.Value ? "включить" : "выключить") +
                                     " HDR — " + Native.ErrorText(err));
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
                    res.Warnings.Add("«" + names[mon.Identity] + "»: не удалось выключить HDR — система включает его обратно.");

                if (want && !check.HdrEnabled)
                {
                    bool cloned = IsCloned(layout, mon.Identity);
                    res.Warnings.Add("«" + names[mon.Identity] + "»: HDR не включился." +
                        (cloned
                            ? " При дублировании HDR держится, только если оба выхода тянут HDR в общем режиме — попробуйте задать группе 3840×2160 60 Гц."
                            : " Обычно это нехватка пропускной способности кабеля на выбранном режиме."));
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
