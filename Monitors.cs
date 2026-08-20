// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DIIISPL
{
    internal class Resolution
    {
        public int Width;
        public int Height;
        public List<double> Rates = new List<double>();
        public override string ToString() { return Width + " × " + Height; }
    }

    /// <summary>Физический монитор, как его видит программа (а не «Дисплей 2» из Windows).</summary>
    internal class MonitorInfo
    {
        public Native.LUID Adapter;
        public uint TargetId;
        public string DevicePath;
        public EdidInfo Edid;

        public string Identity;          // устойчивый ключ (EDID)
        public string Name;              // «LG ULTRAGEAR+»
        public string ConnectionType;    // HDMI / DisplayPort / ...

        public bool Active;
        public uint SourceId;
        public string GdiName;           // \\.\DISPLAY1 — только для активных

        public int X, Y;
        public int Width, Height;        // текущий режим рабочего стола
        public double Hz;
        public uint Rotation = Native.DISPLAYCONFIG_ROTATION_IDENTITY;
        public uint Scaling = Native.DISPLAYCONFIG_SCALING_PREFERRED;
        public bool Primary;

        public bool HdrSupported;
        public bool HdrEnabled;

        public int PreferredWidth, PreferredHeight;
        public double PreferredHz;

        public List<uint> ValidSourceIds = new List<uint>();
        public List<Resolution> Modes = new List<Resolution>();
        public bool ModesAreApproximate;

    }

    internal static class Monitors
    {
        private static readonly int[] CommonWidths = { 3840, 3440, 2560, 2560, 1920, 1680, 1600, 1440, 1280 };
        private static readonly int[] CommonHeights = { 2160, 1440, 1600, 1440, 1080, 1050, 900, 900, 720 };
        private static readonly double[] CommonRates = { 240, 165, 144, 120, 100, 75, 60, 50, 30, 24 };

        /// <summary>Все подключённые сейчас мониторы — включённые и выключенные.</summary>
        public static List<MonitorInfo> Enumerate()
        {
            var result = new List<MonitorInfo>();

            Native.DISPLAYCONFIG_PATH_INFO[] paths;
            Native.DISPLAYCONFIG_MODE_INFO[] modes;
            if (!QueryAllPaths(out paths, out modes)) return result;

            var byTarget = new Dictionary<string, MonitorInfo>();

            foreach (var path in paths)
            {
                bool active = (path.flags & Native.DISPLAYCONFIG_PATH_ACTIVE) != 0;
                bool available = path.targetInfo.targetAvailable != 0;
                if (!active && !available) continue;

                string key = path.targetInfo.adapterId.Key + "/" + path.targetInfo.id;
                MonitorInfo mon;
                if (!byTarget.TryGetValue(key, out mon))
                {
                    mon = new MonitorInfo();
                    mon.Adapter = path.targetInfo.adapterId;
                    mon.TargetId = path.targetInfo.id;

                    var tn = GetTargetName(path.targetInfo.adapterId, path.targetInfo.id);
                    mon.DevicePath = tn.monitorDevicePath;
                    mon.ConnectionType = OutputTechName(tn.outputTechnology);
                    mon.Edid = EdidInfo.FromDevicePath(tn.monitorDevicePath);

                    if (mon.Edid != null)
                    {
                        mon.Identity = mon.Edid.Identity;
                        mon.Name = mon.Edid.DisplayName;
                        mon.PreferredWidth = mon.Edid.PreferredWidth;
                        mon.PreferredHeight = mon.Edid.PreferredHeight;
                    }
                    if (string.IsNullOrEmpty(mon.Name))
                        // Запасное имя — по-английски и мимо словаря: оно попадает
                        // в файл профиля, а служебные файлы пишутся на одном языке.
                        // Иначе профиль, снятый в русском окне, приносил бы
                        // «Дисплей 2» на английскую машину — и наоборот.
                        mon.Name = string.IsNullOrEmpty(tn.monitorFriendlyDeviceName)
                            ? "Display " + path.targetInfo.id
                            : tn.monitorFriendlyDeviceName;
                    if (string.IsNullOrEmpty(mon.Identity))
                        mon.Identity = "PATH:" + (string.IsNullOrEmpty(tn.monitorDevicePath) ? key : tn.monitorDevicePath);

                    ReadPreferredMode(mon);
                    ReadHdr(mon);
                    byTarget[key] = mon;
                    result.Add(mon);
                }

                if (!mon.ValidSourceIds.Contains(path.sourceInfo.id))
                    mon.ValidSourceIds.Add(path.sourceInfo.id);

                if (active)
                {
                    mon.Active = true;
                    mon.SourceId = path.sourceInfo.id;
                    mon.Rotation = path.targetInfo.rotation;
                    mon.Scaling = path.targetInfo.scaling;
                    if (path.targetInfo.refreshRate.Denominator != 0)
                        mon.Hz = (double)path.targetInfo.refreshRate.Numerator / path.targetInfo.refreshRate.Denominator;

                    if (path.sourceInfo.modeInfoIdx != Native.DISPLAYCONFIG_PATH_MODE_IDX_INVALID &&
                        path.sourceInfo.modeInfoIdx < modes.Length)
                    {
                        var sm = modes[path.sourceInfo.modeInfoIdx].modeInfo.sourceMode;
                        mon.Width = (int)sm.width;
                        mon.Height = (int)sm.height;
                        mon.X = sm.position.x;
                        mon.Y = sm.position.y;
                        mon.Primary = (sm.position.x == 0 && sm.position.y == 0);
                    }
                    mon.GdiName = GetSourceGdiName(path.sourceInfo.adapterId, path.sourceInfo.id);
                }
            }

            foreach (var mon in result) FillModes(mon);
            DisambiguateIdentities(result);
            return result;
        }

        public static bool QueryAllPaths(out Native.DISPLAYCONFIG_PATH_INFO[] paths, out Native.DISPLAYCONFIG_MODE_INFO[] modes)
        {
            paths = new Native.DISPLAYCONFIG_PATH_INFO[0];
            modes = new Native.DISPLAYCONFIG_MODE_INFO[0];
            // Между «сколько надо места» и «отдай данные» конфигурация может
            // смениться, и тогда система отвечает ERROR_INSUFFICIENT_BUFFER —
            // это не поломка, а просьба спросить размеры заново. Прежде все коды
            // сливались в один отказ, Enumerate отдавал пустой список, и Missing,
            // который зовёт его через 1300 мс после смены режима — в самый
            // неустойчивый момент, — читал это как «не включился ни один
            // монитор» и запускал лишнее тяжёлое перестроение.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                uint pathCount, modeCount;
                if (Native.GetDisplayConfigBufferSizes(Native.QDC_ALL_PATHS, out pathCount, out modeCount) != 0) return false;

                var p = new Native.DISPLAYCONFIG_PATH_INFO[pathCount];
                var m = new Native.DISPLAYCONFIG_MODE_INFO[modeCount];
                int rc = Native.QueryDisplayConfig(Native.QDC_ALL_PATHS, ref pathCount, p, ref modeCount, m, IntPtr.Zero);
                if (rc == Native.ERROR_INSUFFICIENT_BUFFER) continue;
                if (rc != 0) return false;

                if (p.Length != pathCount) Array.Resize(ref p, (int)pathCount);
                if (m.Length != modeCount) Array.Resize(ref m, (int)modeCount);
                paths = p; modes = m;
                return true;
            }
            return false;
        }

        private static void DisambiguateIdentities(List<MonitorInfo> list)
        {
            var seen = new Dictionary<string, int>();
            foreach (var m in list)
            {
                int n;
                if (seen.TryGetValue(m.Identity, out n))
                {
                    seen[m.Identity] = n + 1;
                    m.Identity = m.Identity + "#" + m.TargetId;
                }
                else seen[m.Identity] = 1;
            }
        }

        public static Native.DISPLAYCONFIG_TARGET_DEVICE_NAME GetTargetName(Native.LUID adapter, uint targetId)
        {
            var tn = new Native.DISPLAYCONFIG_TARGET_DEVICE_NAME();
            tn.header.type = Native.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
            tn.header.size = (uint)Marshal.SizeOf(typeof(Native.DISPLAYCONFIG_TARGET_DEVICE_NAME));
            tn.header.adapterId = adapter;
            tn.header.id = targetId;
            Native.DisplayConfigGetDeviceInfo(ref tn);
            return tn;
        }

        public static string GetSourceGdiName(Native.LUID adapter, uint sourceId)
        {
            var sn = new Native.DISPLAYCONFIG_SOURCE_DEVICE_NAME();
            sn.header.type = Native.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
            sn.header.size = (uint)Marshal.SizeOf(typeof(Native.DISPLAYCONFIG_SOURCE_DEVICE_NAME));
            sn.header.adapterId = adapter;
            sn.header.id = sourceId;
            if (Native.DisplayConfigGetDeviceInfo(ref sn) == 0) return sn.viewGdiDeviceName;
            return null;
        }

        private static void ReadPreferredMode(MonitorInfo mon)
        {
            var pm = new Native.DISPLAYCONFIG_TARGET_PREFERRED_MODE();
            pm.header.type = Native.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_PREFERRED_MODE;
            pm.header.size = (uint)Marshal.SizeOf(typeof(Native.DISPLAYCONFIG_TARGET_PREFERRED_MODE));
            pm.header.adapterId = mon.Adapter;
            pm.header.id = mon.TargetId;
            if (Native.DisplayConfigGetDeviceInfo(ref pm) == 0 && pm.width > 0)
            {
                mon.PreferredWidth = (int)pm.width;
                mon.PreferredHeight = (int)pm.height;
                var vs = pm.targetMode.targetVideoSignalInfo;
                if (vs.vSyncFreq.Denominator != 0)
                    mon.PreferredHz = (double)vs.vSyncFreq.Numerator / vs.vSyncFreq.Denominator;
            }
        }

        /// <summary>
        /// Состояние HDR. На Windows 11 24H2+ спрашиваем новый пакет: старый флаг
        /// advancedColorEnabled там означает широкий цветовой охват и остаётся включённым
        /// даже когда HDR выключен, так что судить по нему нельзя.
        /// </summary>
        public static void ReadHdr(MonitorInfo mon)
        {
            var v2 = new Native.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2();
            v2.header.type = Native.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2;
            v2.header.size = (uint)Marshal.SizeOf(typeof(Native.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2));
            v2.header.adapterId = mon.Adapter;
            v2.header.id = mon.TargetId;
            if (Native.DisplayConfigGetDeviceInfo(ref v2) == 0)
            {
                mon.HdrSupported = v2.HdrSupported && !v2.LimitedByPolicy;
                mon.HdrEnabled = v2.HdrActive;
                return;
            }

            // Windows 10 и ранние сборки 11 нового пакета не знают
            var ac = new Native.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
            ac.header.type = Native.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
            ac.header.size = (uint)Marshal.SizeOf(typeof(Native.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO));
            ac.header.adapterId = mon.Adapter;
            ac.header.id = mon.TargetId;
            if (Native.DisplayConfigGetDeviceInfo(ref ac) == 0)
            {
                mon.HdrSupported = ac.Supported && !ac.ForceDisabled;
                mon.HdrEnabled = ac.Enabled;
            }
        }

        /// <summary>
        /// Включить/выключить HDR. Сначала новым вызовом (24H2+), затем старым:
        /// старый на новых сборках отвечает успехом, но HDR не выключает.
        /// </summary>
        public static int SetHdr(Native.LUID adapter, uint targetId, bool enable)
        {
            var hdr = new Native.DISPLAYCONFIG_SET_HDR_STATE();
            hdr.header.type = Native.DISPLAYCONFIG_DEVICE_INFO_SET_HDR_STATE;
            hdr.header.size = (uint)Marshal.SizeOf(typeof(Native.DISPLAYCONFIG_SET_HDR_STATE));
            hdr.header.adapterId = adapter;
            hdr.header.id = targetId;
            hdr.value = enable ? 1u : 0u;
            int rc = Native.DisplayConfigSetDeviceInfo(ref hdr);
            if (rc == 0)
            {
                // Дать линку согласоваться. Мгновенным переключение не бывает, и в
                // соседнем коде это признано паузами в 600–700 мс. Без выдержки
                // проверка почти всегда читала «ещё не изменилось», и вдогонку
                // уходил старый вызов — а на сборках 24H2+ он управляет уже не
                // HDR, а расширенным цветовым охватом, который никто не просил
                // трогать и который программа не умеет вернуть.
                System.Threading.Thread.Sleep(700);   // столько же, сколько сверка в Applier

                var check = new MonitorInfo();
                check.Adapter = adapter;
                check.TargetId = targetId;
                ReadHdr(check);
                if (check.HdrEnabled == enable) return 0;
            }

            var set = new Native.DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE();
            set.header.type = Native.DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE;
            set.header.size = (uint)Marshal.SizeOf(typeof(Native.DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE));
            set.header.adapterId = adapter;
            set.header.id = targetId;
            set.value = enable ? 1u : 0u;
            int rc2 = Native.DisplayConfigSetDeviceInfo(ref set);

            // Успех старого вызова — это успех, даже если новый пакет система не
            // знает. Прежде здесь возвращалась ошибка нового: на Windows 10 и на
            // сборках 11 до 24H2 — ровно там, ради чего запасной путь и написан, —
            // HDR включался, а программа сообщала «видеокарта не принимает такое
            // сочетание режимов» и не заносила монитор в список изменённых.
            return rc2 == 0 ? 0 : (rc != 0 ? rc : rc2);
        }

        private static void FillModes(MonitorInfo mon)
        {
            var byRes = new Dictionary<string, Resolution>();

            if (mon.Active && !string.IsNullOrEmpty(mon.GdiName))
            {
                var dm = new Native.DEVMODE();
                dm.dmSize = (ushort)Marshal.SizeOf(typeof(Native.DEVMODE));
                for (int i = 0; Native.EnumDisplaySettingsEx(mon.GdiName, i, ref dm, 0); i++)
                {
                    if (dm.dmBitsPerPel < 32) continue;
                    AddMode(byRes, (int)dm.dmPelsWidth, (int)dm.dmPelsHeight, dm.dmDisplayFrequency);
                    dm = new Native.DEVMODE();
                    dm.dmSize = (ushort)Marshal.SizeOf(typeof(Native.DEVMODE));
                }
            }

            if (byRes.Count == 0)
            {
                // монитор сейчас выключен — строим список из предпочитаемого режима EDID
                mon.ModesAreApproximate = true;
                int pw = mon.PreferredWidth > 0 ? mon.PreferredWidth : 1920;
                int ph = mon.PreferredHeight > 0 ? mon.PreferredHeight : 1080;
                double phz = mon.PreferredHz > 0 ? mon.PreferredHz : 60;

                AddMode(byRes, pw, ph, phz);
                foreach (double r in CommonRates) if (r <= phz + 0.5) AddMode(byRes, pw, ph, r);
                for (int i = 0; i < CommonWidths.Length; i++)
                {
                    int w = CommonWidths[i], h = CommonHeights[i];
                    if (w > pw || h > ph) continue;
                    AddMode(byRes, w, h, 60);
                    if (phz > 60) AddMode(byRes, w, h, phz);
                }
            }

            var list = new List<Resolution>(byRes.Values);
            list.Sort(delegate(Resolution a, Resolution b)
            {
                long areaA = (long)a.Width * a.Height, areaB = (long)b.Width * b.Height;
                if (areaA != areaB) return areaB.CompareTo(areaA);
                return b.Width.CompareTo(a.Width);
            });
            foreach (var r in list) r.Rates.Sort(delegate(double a, double b) { return b.CompareTo(a); });
            mon.Modes = list;
        }

        private static void AddMode(Dictionary<string, Resolution> byRes, int w, int h, double hz)
        {
            if (w <= 0 || h <= 0) return;
            string key = w + "x" + h;
            Resolution r;
            if (!byRes.TryGetValue(key, out r))
            {
                r = new Resolution();
                r.Width = w; r.Height = h;
                byRes[key] = r;
            }
            if (hz <= 0) return;
            foreach (double existing in r.Rates) if (Math.Abs(existing - hz) < 0.6) return;
            r.Rates.Add(hz);
        }

        public static string OutputTechName(uint tech)
        {
            switch (tech)
            {
                case 0: return "VGA";
                case 4: return "DVI";
                case 5: return "HDMI";
                case 10: return "DisplayPort";
                case 11: return "DisplayPort";
                case 0x80000000: return Text.ConnBuiltIn;
                default: return Text.ConnDigital;
            }
        }
    }
}
