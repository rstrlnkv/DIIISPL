// DIIISPL — a monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DIIISPL
{
    /// <summary>
    /// Watches what happens on each screen and keeps the taskbar look
    /// in line with the profile: a window maximized — one look, everything minimized — another,
    /// "Start" opened — a third. The state is computed for each taskbar separately,
    /// so the look can differ between monitors.
    /// </summary>
    internal static class TaskbarWatcher
    {
        private static TaskbarSettings _settings;
        private static System.Windows.Threading.DispatcherTimer _timer;
        private static readonly Dictionary<IntPtr, string> _appliedPerBar = new Dictionary<IntPtr, string>();

        /// <summary>
        /// The memory of what has been applied is edited from two threads: Tick runs on the
        /// window thread, while ResetCache is called by the layout application — from a pool thread. Before,
        /// both went to one unprotected Dictionary, and this is not theory: a write
        /// begun before a reset survived it. The watcher then saw "this taskbar
        /// already has this look applied" and skipped the real repaint — the taskbar
        /// kept the look from the previous layout.
        /// </summary>
        private static readonly object Lock = new object();

        /// <summary>Deferred reset: taskbars are not recreated at once, see ResetSoon.</summary>
        private static System.Windows.Threading.DispatcherTimer _settle;

        /// <summary>Set the current profile's settings and start watching.</summary>
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
                // Bind the timer to the window's dispatcher, not to the thread where
                // the first call happened. Layout application now runs on a
                // pool thread, where nobody pumps a dispatcher: a timer created from there
                // is born dead and does not revive until restart — measured:
                // zero ticks in 700 ms at an interval of 50. The watcher exists
                // precisely because taskbars get recreated, and a silent one is useless.
                _timer = new System.Windows.Threading.DispatcherTimer(
                    System.Windows.Threading.DispatcherPriority.Normal,
                    System.Windows.Application.Current != null
                        ? System.Windows.Application.Current.Dispatcher
                        : System.Windows.Threading.Dispatcher.CurrentDispatcher);
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
        /// Full reset: both the link to the service in Explorer and the memory of what is already applied.
        /// Required when the layout changes — taskbars are recreated, the old windows
        /// vanish, and everything we remembered about them becomes a lie.
        /// </summary>
        public static void ResetCache()
        {
            lock (Lock)
            {
                _appliedPerBar.Clear();
                ExplorerTap.Forget();
            }
        }

        private static void Tick()
        {
            lock (Lock) { TickCore(); }
        }

        /// <summary>
        /// One more reset, once the taskbars settle.
        ///
        /// The reset on layout application happens right after SetDisplayConfig,
        /// while Windows recreates the taskbars a bit later. In between,
        /// the watcher manages to look at the old windows and remember something
        /// false about them — the lock does not save from this, it only protects against corruption of the
        /// memory itself. So after a second and a half the memory is cleared once more, on
        /// the window thread, where it cannot interleave with Tick.
        ///
        /// From the command line there is no dispatcher and nobody to watch — so
        /// there is nothing to defer.
        /// </summary>
        public static void ResetSoon()
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            app.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (_settle == null)
                {
                    _settle = new System.Windows.Threading.DispatcherTimer(
                        System.Windows.Threading.DispatcherPriority.Normal, app.Dispatcher);
                    _settle.Interval = TimeSpan.FromMilliseconds(1500);
                    _settle.Tick += delegate { _settle.Stop(); ResetCache(); Tick(); };
                }
                _settle.Stop();
                _settle.Start();
            }));
        }

        private static void TickCore()
        {
            if (_settings == null || !ExplorerTap.Available) return;

            bool battery = BatterySaverOn();
            bool start = StartOpened();
            bool search = SearchOpened();
            bool taskView = TaskViewOpened();

            foreach (IntPtr bar in ExplorerTap.FindTaskbars())
            {
                IntPtr monitor = MonitorFromWindow(bar, MONITOR_DEFAULTTONEAREST);
                var state = Evaluate(monitor, battery, start, search, taskView);

                var look = _settings[state];
                // There is deliberately no "take the usual look" fallback here: Evaluate
                // returns only the enabled cases. A case without a special look
                // means not "look as usual" but "do not consider" —
                // the look is then taken from the next matching one, which is already chosen.

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

        /// <summary>What the program sees right now — for checking the settings.</summary>
        public static string Report(TaskbarSettings settings)
        {
            var sb = new StringBuilder();
            bool battery = BatterySaverOn();
            bool start = StartOpened();
            bool search = SearchOpened();
            bool taskView = TaskViewOpened();

            sb.AppendLine(Text.TbSignals(battery, start, search, taskView));

            var bars = ExplorerTap.FindTaskbars();
            sb.AppendLine(Text.TbBarsFound(bars.Count));

            var saved = _settings;
            _settings = settings ?? new TaskbarSettings();
            foreach (IntPtr bar in bars)
            {
                IntPtr monitor = MonitorFromWindow(bar, MONITOR_DEFAULTTONEAREST);
                var state = Evaluate(monitor, battery, start, search, taskView);
                var look = _settings[state];
                sb.AppendLine(Text.TbBarLine(bar.ToInt64().ToString("X"),
                                             HasWindow(monitor, true), HasWindow(monitor, false),
                                             TaskbarSettings.Describe(state),
                                             look.Enabled ? ExplorerTap.Describe(look.Accent) : Text.TbStateOff));
            }
            _settings = saved;
            return sb.ToString();
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

        // ---------------------------------------------------------------- windows
        /// <summary>
        /// Whether this monitor has a window (any, or only a maximized one).
        ///
        /// Window enumeration can break off if some window closed during the walk;
        /// then "found nothing" is not an answer but a failure, and the attempt must be repeated.
        /// </summary>
        private static bool HasWindow(IntPtr monitor, bool maximizedOnly)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                bool found;
                if (TryFindWindow(monitor, maximizedOnly, out found)) return found;
            }
            return false;
        }

        /// <summary>Returns false if the walk broke off and the answer cannot be trusted.</summary>
        private static bool TryFindWindow(IntPtr monitor, bool maximizedOnly, out bool found)
        {
            bool hit = false;
            var buffer = new StringBuilder(64);

            bool ok = Native.EnumWindows(delegate(IntPtr hwnd, IntPtr param)
            {
                if (hit) return false;
                if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;
                if (MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST) != monitor) return true;

                // invisible windows: service ones, "cloaked" by the shell, of zero size
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

                hit = true;
                return false;
            }, IntPtr.Zero);

            found = hit;
            return hit || ok;         // found — there is an answer; reached the end — "no" is an answer too
        }

        /// <summary>
        /// Whether the named process has an open window of this class.
        ///
        /// The focused window will not do: on current Windows 11 builds "Start" hands
        /// focus to the SearchHost window, so by focus "Start" and search are indistinguishable. But an open
        /// "Start" is a visible StartMenuExperienceHost window across the full screen width, while search
        /// on its own is just a SearchHost window.
        /// </summary>
        private static bool HasShellWindow(string className, string processName)
        {
            IntPtr hwnd = IntPtr.Zero;
            while ((hwnd = Native.FindWindowEx(IntPtr.Zero, hwnd, className, null)) != IntPtr.Zero)
            {
                if (!IsWindowVisible(hwnd) || IsCloaked(hwnd)) continue;
                if (BelongsTo(hwnd, processName)) return true;
            }
            return false;
        }

        private static bool BelongsTo(IntPtr hwnd, string processName)
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

        private static bool StartOpened()
        {
            return HasShellWindow("Windows.UI.Core.CoreWindow", "StartMenuExperienceHost");
        }

        /// <summary>Search on its own: a SearchHost window exists, with no "Start" above it.</summary>
        private static bool SearchOpened()
        {
            return HasShellWindow("Windows.UI.Core.CoreWindow", "SearchHost") && !StartOpened();
        }

        /// <summary>Task View is a separate Explorer window, not a CoreWindow.</summary>
        private static bool TaskViewOpened()
        {
            return HasShellWindow("XamlExplorerHostIslandWindow", "explorer");
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
