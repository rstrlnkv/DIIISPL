// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.
//
// Этот модуль работает со вспомогательной библиотекой DIIISPL.ExplorerTAP.dll, основанной
// на компоненте ExplorerTAP проекта TranslucentTB (https://github.com/TranslucentTB/TranslucentTB),
// © Charles Milette и участники, GPLv3. Библиотека публикует внутри проводника COM-службу
// оформления панели задач, а здесь — только вызовы к ней.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DIIISPL
{
    /// <summary>Вид панели задач, как его понимает служба оформления.</summary>
    internal enum TaskbarLook
    {
        Default,      // вернуть системный вид
        Opaque,       // сплошная заливка
        Clear,        // прозрачная
        Blur,         // размытие
        Acrylic       // акрил
    }

    /// <summary>
    /// Доступ к службе оформления панели задач.
    ///
    /// Снаружи панель задач Windows 11 не поддаётся: фон рисует её XAML-дерево, и политика
    /// акцента до него не достаёт. Поэтому оформление задаёт библиотека, работающая внутри
    /// проводника; сюда она отдаёт COM-интерфейс, а мы дёргаем его методы.
    /// </summary>
    internal static class ExplorerTap
    {
        private const string TapDll = "DIIISPL.ExplorerTAP.dll";
        private static readonly Guid IID_TaskbarAppearanceService = new Guid("d255d70a-b6f7-4b66-9fc6-b290a598a1d3");

        [DllImport(TapDll, CharSet = CharSet.Unicode)]
        private static extern int InjectExplorerTAP(IntPtr window, ref Guid riid, out IntPtr service);

        [ComImport, Guid("d255d70a-b6f7-4b66-9fc6-b290a598a1d3"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarAppearanceService
        {
            void SetTaskbarAppearance(IntPtr taskbar, int brush, uint color);
            void SetTaskbarBlur(IntPtr taskbar, uint color, float blurAmount);
            void ReturnTaskbarToDefaultAppearance(IntPtr taskbar);
            void SetTaskbarBorderVisibility(IntPtr taskbar, int visible);
            void RestoreAllTaskbarsToDefault();
            void RestoreAllTaskbarsToDefaultWhenProcessDies(uint pid);
            void KillExplorerWhenPackageUninstalls([MarshalAs(UnmanagedType.LPWStr)] string packageFullName);
        }

        private const int BrushAcrylic = 0;
        private const int BrushSolidColor = 1;

        /// <summary>Возвращать ли панель к системному виду при выходе — только для долгоживущего трея.</summary>
        public static bool RestoreWhenProcessExits;

        private static ITaskbarAppearanceService _service;
        private static string _lastError;

        public static string LastError { get { return _lastError; } }

        public static bool Available
        {
            get
            {
                try { return System.IO.File.Exists(System.IO.Path.Combine(AppFolder(), TapDll)); }
                catch { return false; }
            }
        }

        private static string AppFolder()
        {
            return System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        /// <summary>
        /// Получить службу, при необходимости подняв её в проводнике. Служба живёт вместе
        /// с проводником: он перезапустился — связь потеряна, нужно подключаться заново.
        /// </summary>
        private static ITaskbarAppearanceService Service()
        {
            if (_service != null) return _service;

            IntPtr taskbar = PrimaryTaskbar();
            if (taskbar == IntPtr.Zero) { _lastError = Text.TbNoTaskbar; return null; }

            try
            {
                // Сразу после перезапуска проводника первая попытка нередко не дожидается
                // готовности библиотеки внутри него, поэтому пробуем несколько раз.
                Guid iid = IID_TaskbarAppearanceService;
                IntPtr raw = IntPtr.Zero;
                int hr = 0;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    hr = InjectExplorerTAP(taskbar, ref iid, out raw);
                    if (hr == 0 && raw != IntPtr.Zero) break;
                    if (attempt < 3) System.Threading.Thread.Sleep(1500);
                }
                if (hr != 0 || raw == IntPtr.Zero)
                {
                    _lastError = Text.TbNoExplorer;
                    return null;
                }

                _service = (ITaskbarAppearanceService)Marshal.GetObjectForIUnknown(raw);
                Marshal.Release(raw);

                // Просим вернуть панель к системному виду при закрытии DIIISPL — но только если
                // программа живёт долго. Для однократной команды это самоубийственно:
                // процесс завершается сразу, и служба тут же откатывает всё, что мы задали.
                if (RestoreWhenProcessExits)
                {
                    try { _service.RestoreAllTaskbarsToDefaultWhenProcessDies((uint)System.Diagnostics.Process.GetCurrentProcess().Id); }
                    catch { }
                }

                _lastError = null;
                return _service;
            }
            catch (Exception)
            {
                _lastError = Text.TbLostExplorer;
                _service = null;
                return null;
            }
        }

        /// <summary>Связь с проводником потеряна — при следующем обращении подключимся заново.</summary>
        public static void Forget()
        {
            _service = null;
        }

        private static IntPtr PrimaryTaskbar()
        {
            var bars = FindTaskbars();
            return bars.Count > 0 ? bars[0] : IntPtr.Zero;
        }

        /// <summary>
        /// Панель основного экрана и панели на остальных мониторах.
        ///
        /// Ищем окна по классу напрямую, а не перечислением всех окон: пока открыт «Пуск»,
        /// поиск или представление задач, окна оболочки быстро создаются и уничтожаются,
        /// EnumWindows обрывается на середине и панели в списке не оказывается вовсе —
        /// а это ровно те состояния, ради которых всё и затевалось.
        /// </summary>
        public static List<IntPtr> FindTaskbars()
        {
            var found = new List<IntPtr>();

            IntPtr primary = Native.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
            if (primary != IntPtr.Zero) found.Add(primary);                     // основная — первой

            IntPtr next = IntPtr.Zero;
            while ((next = Native.FindWindowEx(IntPtr.Zero, next, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
                found.Add(next);

            return found;
        }

        /// <summary>
        /// Служба принимает цвет как ABGR, а хранить настройки удобнее в привычном
        /// AARRGGBB — переставляем красный с синим.
        /// </summary>
        private static uint ToAbgr(uint argb)
        {
            return (argb & 0xFF00FF00) | ((argb & 0x00FF0000) >> 16) | ((argb & 0x000000FF) << 16);
        }

        /// <summary>
        /// Разобрать цвет вида AARRGGBB (допустимы «#» и «0x» впереди, а также короткая
        /// запись RRGGBB — тогда цвет непрозрачный). Отдельный разбор нужен потому, что
        /// обрезать нули по краям нельзя: «00000000» — вполне законный прозрачный цвет.
        /// </summary>
        public static bool TryParseColor(string text, out uint color)
        {
            color = 0;
            if (text == null) return false;
            string s = text.Trim();
            if (s.StartsWith("#", StringComparison.Ordinal)) s = s.Substring(1);
            else if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            if (s.Length != 6 && s.Length != 8) return false;
            if (!uint.TryParse(s, System.Globalization.NumberStyles.HexNumber,
                               System.Globalization.CultureInfo.InvariantCulture, out color)) return false;
            if (s.Length == 6) color |= 0xFF000000;
            return true;
        }

        /// <summary>Задать вид одной панели задач.</summary>
        public static bool ApplyTo(IntPtr bar, TaskbarLook look, uint colorArgb, float blurRadius, bool showLine)
        {
            var service = Service();
            if (service == null) return false;

            try
            {
                uint color = ToAbgr(colorArgb);
                switch (look)
                {
                    case TaskbarLook.Opaque:
                        service.SetTaskbarAppearance(bar, BrushSolidColor, color | 0xFF000000);
                        break;
                    case TaskbarLook.Clear:
                        service.SetTaskbarAppearance(bar, BrushSolidColor, color & 0x00FFFFFF);
                        break;
                    case TaskbarLook.Acrylic:
                        service.SetTaskbarAppearance(bar, BrushAcrylic, color);
                        break;
                    case TaskbarLook.Blur:
                        service.SetTaskbarBlur(bar, color, blurRadius);
                        break;
                    default:
                        service.ReturnTaskbarToDefaultAppearance(bar);
                        break;
                }
                service.SetTaskbarBorderVisibility(bar, showLine ? 1 : 0);
                _lastError = null;
                return true;
            }
            catch (Exception)
            {
                _lastError = Text.TbLostExplorer;
                _service = null;         // проводник мог перезапуститься
                return false;
            }
        }

        /// <summary>Вернуть все панели к системному виду.</summary>
        public static void RestoreAll()
        {
            var service = Service();
            if (service == null) return;
            try { service.RestoreAllTaskbarsToDefault(); }
            catch { _service = null; }
        }

        /// <summary>
        /// Задать вид сразу всем панелям задач. Намеренно сделано через ApplyTo, а не своим
        /// проходом по службе: иначе легко разъезжаются мелочи — раньше здесь терялся перевод
        /// цвета в ABGR, и одна и та же настройка из окна и из командной строки давала разный цвет.
        /// </summary>
        public static bool ApplyAll(TaskbarLook look, uint colorArgb, float blurRadius, bool showLine)
        {
            var bars = FindTaskbars();
            if (bars.Count == 0) { _lastError = Text.TbNoTaskbar; return false; }

            bool all = true;
            foreach (IntPtr bar in bars)
                if (!ApplyTo(bar, look, colorArgb, blurRadius, showLine)) all = false;
            return all;
        }

        /// <summary>Вернуть панелям системный вид.</summary>
        public static void Restore()
        {
            if (_service == null) return;
            try { _service.RestoreAllTaskbarsToDefault(); }
            catch { _service = null; }
        }

        // ---- перевод названий из профиля ----
        public static readonly string[] Names = { "default", "opaque", "clear", "blur", "acrylic" };

        public static string Describe(string name)
        {
            switch (name)
            {
                case "default": return Text.TbLook("default");
                case "opaque": return Text.TbLook("opaque");
                case "clear": return Text.TbLook("clear");
                case "blur": return Text.TbLook("blur");
                case "acrylic": return Text.TbLook("acrylic");
                default: return Text.TbLook("keep");
            }
        }

        public static TaskbarLook Parse(string name)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "opaque": return TaskbarLook.Opaque;
                case "clear": return TaskbarLook.Clear;
                case "blur": return TaskbarLook.Blur;
                case "acrylic": return TaskbarLook.Acrylic;
                default: return TaskbarLook.Default;
            }
        }
    }
}
