// DIIIX — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DIIIX
{
    /// <summary>
    /// Следит за тем, что происходит на каждом экране, и держит вид панели задач
    /// в согласии с профилем: развернули окно — один вид, свернули всё — другой,
    /// открыли «Пуск» — третий. Состояние считается для каждой панели отдельно,
    /// поэтому на разных мониторах вид может отличаться.
    /// </summary>
    internal static class TaskbarWatcher
    {
        private static TaskbarSettings _settings;
        private static System.Windows.Threading.DispatcherTimer _timer;
        private static readonly Dictionary<IntPtr, string> _appliedPerBar = new Dictionary<IntPtr, string>();

        /// <summary>Задать настройки текущего профиля и начать следить.</summary>
        public static void Use(TaskbarSettings settings)
        {
            ResetCache();
            _settings = settings;

            if (settings == null || !settings.AnyEnabled)
            {
                Stop();
                if (ExplorerTap.Available) ExplorerTap.RestoreAll();
                return;
            }

            if (_timer == null)
            {
                _timer = new System.Windows.Threading.DispatcherTimer();
                _timer.Interval = TimeSpan.FromMilliseconds(250);
                _timer.Tick += delegate { Tick(); };
            }
            _timer.Start();
            Tick();
        }

        public static void Stop()
        {
            if (_timer != null) _timer.Stop();
        }

        /// <summary>
        /// Полный сброс: и связь со службой в проводнике, и память о том, что уже применено.
        /// Обязателен при смене раскладки — панели задач пересоздаются, прежние окна
        /// исчезают, и всё, что мы про них помнили, становится ложью.
        /// </summary>
        public static void ResetCache()
        {
            _appliedPerBar.Clear();
            ExplorerTap.Forget();
        }

        private static void Tick()
        {
            if (_settings == null || !ExplorerTap.Available) return;

            bool battery = BatterySaverOn();
            IntPtr foreground = GetForegroundWindow();
            bool start = IsShellWindow(foreground, "StartMenuExperienceHost");
            bool search = IsShellWindow(foreground, "SearchHost");
            bool taskView = IsTaskView(foreground);

            foreach (IntPtr bar in ExplorerTap.FindTaskbars())
            {
                IntPtr monitor = MonitorFromWindow(bar, MONITOR_DEFAULTTONEAREST);
                var state = Evaluate(monitor, battery, start, search, taskView);

                var look = _settings[state];
                if (!look.Enabled) look = _settings[TaskbarState.Desktop];

                string signature = state + "|" + look.Serialize();
                string applied;
                if (_appliedPerBar.TryGetValue(bar, out applied) && applied == signature) continue;

                if (look.Enabled)
                {
                    ExplorerTap.ApplyTo(bar, ExplorerTap.Parse(look.Accent), look.Color, look.BlurRadius, look.ShowLine);
                    _appliedPerBar[bar] = signature;
                }
            }
        }

        private static TaskbarState Evaluate(IntPtr monitor, bool battery, bool start, bool search, bool taskView)
        {
            foreach (TaskbarState state in TaskbarSettings.Order)
            {
                if (!_settings[state].Enabled) continue;
                switch (state)
                {
                    case TaskbarState.BatterySaver: if (battery) return state; break;
                    case TaskbarState.TaskViewOpened: if (taskView) return state; break;
                    case TaskbarState.SearchOpened: if (search) return state; break;
                    case TaskbarState.StartOpened: if (start) return state; break;
                    case TaskbarState.MaximizedWindow: if (HasWindow(monitor, true)) return state; break;
                    case TaskbarState.VisibleWindow: if (HasWindow(monitor, false)) return state; break;
                    case TaskbarState.Desktop: return state;
                }
            }
            return TaskbarState.Desktop;
        }

        // ---------------------------------------------------------------- окна
        private static bool HasWindow(IntPtr monitor, bool maximizedOnly)
        {
            bool found = false;
            var buffer = new StringBuilder(64);

            Native.EnumWindows(delegate(IntPtr hwnd, IntPtr param)
            {
                if (found) return false;
                if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;
                if (MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST) != monitor) return true;

                // окна-невидимки: служебные, «залакированные» оболочкой, нулевого размера
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                if ((exStyle & WS_EX_TOOLWINDOW) != 0) return true;
                if (IsCloaked(hwnd)) return true;

                buffer.Length = 0;
                Native.GetClassName(hwnd, buffer, buffer.Capacity);
                string cls = buffer.ToString();
                if (cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd" ||
                    cls == "Progman" || cls == "WorkerW" || cls == "Windows.UI.Core.CoreWindow") return true;

                if (GetWindowTextLength(hwnd) == 0) return true;

                RECT rect;
                if (!GetWindowRect(hwnd, out rect)) return true;
                if (rect.right - rect.left < 40 || rect.bottom - rect.top < 40) return true;

                if (maximizedOnly && !IsZoomed(hwnd)) return true;

                found = true;
                return false;
            }, IntPtr.Zero);

            return found;
        }

        private static bool IsShellWindow(IntPtr hwnd, string processName)
        {
            if (hwnd == IntPtr.Zero) return false;
            try
            {
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                return string.Equals(proc.ProcessName, processName, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool IsTaskView(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            var buffer = new StringBuilder(64);
            Native.GetClassName(hwnd, buffer, buffer.Capacity);
            string cls = buffer.ToString();
            return cls == "Windows.UI.Core.CoreWindow" && IsShellWindow(hwnd, "explorer");
        }

        private static bool BatterySaverOn()
        {
            SYSTEM_POWER_STATUS status;
            if (!GetSystemPowerStatus(out status)) return false;
            return status.SystemStatusFlag == 1;
        }

        private static bool IsCloaked(IntPtr hwnd)
        {
            int cloaked = 0;
            if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out cloaked, sizeof(int)) != 0) return false;
            return cloaked != 0;
        }

        // ---------------------------------------------------------------- WinAPI
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int DWMWA_CLOAKED = 14;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
    }
}
