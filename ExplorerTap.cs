// DIIIX — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.
//
// Этот модуль работает со вспомогательной библиотекой DIIIX.ExplorerTAP.dll, основанной
// на компоненте ExplorerTAP проекта TranslucentTB (https://github.com/TranslucentTB/TranslucentTB),
// © Charles Milette и участники, GPLv3. Библиотека публикует внутри проводника COM-службу
// оформления панели задач, а здесь — только вызовы к ней.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DIIIX
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
        private const string TapDll = "DIIIX.ExplorerTAP.dll";
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
            if (taskbar == IntPtr.Zero) { _lastError = "не найдена панель задач"; return null; }

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
                    _lastError = "не удалось подключиться к проводнику (0x" + hr.ToString("X8") + ")";
                    return null;
                }

                _service = (ITaskbarAppearanceService)Marshal.GetObjectForIUnknown(raw);
                Marshal.Release(raw);

                // Просим вернуть панель к системному виду при закрытии DIIIX — но только если
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
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _service = null;
                return null;
            }
        }

        [DllImport("oleaut32.dll")]
        private static extern int GetActiveObject(ref Guid clsid, IntPtr reserved, out IntPtr obj);

        /// <summary>Пошаговая диагностика подключения — какой именно шаг не проходит.</summary>
        public static string Diagnose()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("  апартамент потока: " + System.Threading.Thread.CurrentThread.GetApartmentState());
            sb.AppendLine("  разрядность: " + (IntPtr.Size * 8) + " бит");

            IntPtr bar = PrimaryTaskbar();
            sb.AppendLine("  панель задач: 0x" + bar.ToInt64().ToString("X"));

            var unknown = new Guid("00000000-0000-0000-C000-000000000046");
            IntPtr p1;
            int hr1 = InjectExplorerTAP(bar, ref unknown, out p1);
            sb.AppendLine("  Inject(IUnknown)   = 0x" + hr1.ToString("X8") + (p1 != IntPtr.Zero ? "  объект есть" : "  пусто"));

            var clsid = new Guid("5a6ba2a9-37cb-4ed4-91bd-012125ecc43d");
            IntPtr p2;
            int hr2 = GetActiveObject(ref clsid, IntPtr.Zero, out p2);
            sb.AppendLine("  GetActiveObject    = 0x" + hr2.ToString("X8") + (p2 != IntPtr.Zero ? "  служба в ROT" : "  нет"));

            if (p2 != IntPtr.Zero)
            {
                var ver = new Guid("e0811f94-c7fd-4895-9df6-297b71e81e33");
                IntPtr o;
                sb.AppendLine("  QI IVersionedApi   = 0x" + Marshal.QueryInterface(p2, ref ver, out o).ToString("X8"));
                var svc = IID_TaskbarAppearanceService;
                IntPtr o2;
                sb.AppendLine("  QI ITaskbarService = 0x" + Marshal.QueryInterface(p2, ref svc, out o2).ToString("X8"));
            }

            var direct = IID_TaskbarAppearanceService;
            IntPtr p3;
            int hr3 = InjectExplorerTAP(bar, ref direct, out p3);
            sb.AppendLine("  Inject(службу)     = 0x" + hr3.ToString("X8"));
            return sb.ToString();
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

        /// <summary>Панель основного экрана и панели на остальных мониторах.</summary>
        public static List<IntPtr> FindTaskbars()
        {
            var found = new List<IntPtr>();
            var buffer = new System.Text.StringBuilder(64);
            Native.EnumWindows(delegate(IntPtr hwnd, IntPtr param)
            {
                buffer.Length = 0;
                if (Native.GetClassName(hwnd, buffer, buffer.Capacity) > 0)
                {
                    string cls = buffer.ToString();
                    if (cls == "Shell_TrayWnd") found.Insert(0, hwnd);          // основная — первой
                    else if (cls == "Shell_SecondaryTrayWnd") found.Add(hwnd);
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        /// <summary>Задать вид всем панелям задач. Возвращает false, если не вышло.</summary>
        public static bool Apply(TaskbarLook look, uint color, float blurRadius)
        {
            var service = Service();
            if (service == null) return false;

            var bars = FindTaskbars();
            if (bars.Count == 0) { _lastError = "не найдена панель задач"; return false; }

            try
            {
                foreach (IntPtr bar in bars)
                {
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
                }
                _lastError = null;
                return true;
            }
            catch (Exception ex)
            {
                // проводник мог перезапуститься — сбрасываем связь, следующая попытка её поднимет
                _lastError = ex.Message;
                _service = null;
                return false;
            }
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
                case "default": return "Как в системе";
                case "opaque": return "Непрозрачная";
                case "clear": return "Прозрачная";
                case "blur": return "Размытие";
                case "acrylic": return "Акрил";
                default: return "Не менять";
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
