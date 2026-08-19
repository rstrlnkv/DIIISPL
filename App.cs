// DIIIX — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace DIIIX
{
    internal class App : Application
    {
        public const string AppName = "DIIIX";
        public const string AppTitle = "DIIIX";

        private Forms.NotifyIcon _tray;
        private MainWindow _window;
        private HwndSource _messageWindow;
        private readonly Dictionary<int, string> _hotkeyProfiles = new Dictionary<int, string>();
        private static uint _showMessage;
        private static Mutex _mutex;

        [STAThread]
        private static int Main(string[] args)
        {
            _showMessage = Native.RegisterWindowMessage("DIIIXShowManager");

            if (args.Length > 0 && args[0].TrimStart('-', '/').ToLowerInvariant() != "tray")
                return Cli.Run(args);

            bool created;
            _mutex = new Mutex(true, "Local\\DIIIX.SingleInstance", out created);
            if (!created)
            {
                // уже запущено — просто показываем окно работающей копии
                Native.PostMessage(Native.HWND_BROADCAST, _showMessage, IntPtr.Zero, IntPtr.Zero);
                return 0;
            }

            bool startHidden = args.Length > 0;
            try
            {
                var app = new App();
                app.Start(startHidden);
            }
            catch (Exception ex)
            {
                // молча падать нельзя: пользователь увидит только исчезнувший значок
                try
                {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(Store.DataDir, "error.log"),
                        DateTime.Now + Environment.NewLine + ex);
                }
                catch { }
                Forms.MessageBox.Show("Не удалось запустить " + AppTitle + ":" + Environment.NewLine +
                    Environment.NewLine + ex.Message + Environment.NewLine +
                    Environment.NewLine + "Подробности: %AppData%\\" + Store.AppFolder + "\\error.log",
                    AppTitle);
                return 1;
            }
            return 0;
        }

        private void Start(bool startHidden)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Resources.MergedDictionaries.Add(Theme.Build());

            _messageWindow = new HwndSource(0, 0, 0, 0, 0, "DIIIXMessages", IntPtr.Zero);
            _messageWindow.AddHook(WndProc);

            ExplorerTap.RestoreWhenProcessExits = true;   // трей живёт долго — при выходе вернём панель
            _window = new MainWindow();
            _window.Icon = Fluent.MakeWindowIcon();
            _window.ProfilesChanged += ReloadHotkeys;

            BuildTray();
            ReloadHotkeys();
            StartTaskbarForActiveProfile();

            if (!startHidden) ShowManager();
            Run();
        }

        // ---------------------------------------------------------------- трей
        private void BuildTray()
        {
            _tray = new Forms.NotifyIcon();
            _tray.Icon = Fluent.MakeTrayIcon();
            _tray.Text = AppTitle;
            _tray.Visible = true;
            _tray.MouseUp += delegate(object s, Forms.MouseEventArgs e)
            {
                if (e.Button == Forms.MouseButtons.Right) ShowTrayMenu();
                else if (e.Button == Forms.MouseButtons.Left) ShowTrayMenu();
            };
            _tray.DoubleClick += delegate { ShowManager(); };
        }

        private void ShowTrayMenu()
        {
            var menu = new ContextMenu();
            menu.Style = (Style)Resources["FlyoutMenu"];
            menu.HasDropShadow = true;

            var profiles = Store.LoadAll();
            if (profiles.Count == 0)
            {
                var empty = new MenuItem();
                empty.Style = (Style)Resources["FlyoutMenuItem"];
                empty.Header = "Профилей пока нет";
                empty.IsEnabled = false;
                menu.Items.Add(empty);
            }
            else
            {
                var live = Monitors.Enumerate();
                foreach (var layout in profiles)
                {
                    var item = new MenuItem();
                    item.Style = (Style)Resources["FlyoutMenuItem"];
                    item.Header = layout.Name;
                    item.InputGestureText = layout.Hotkey;
                    item.IsChecked = layout.MatchesCurrent(live);   // точкой отмечена действующая раскладка
                    string name = layout.Name;
                    item.Click += delegate { ApplyByName(name); };
                    menu.Items.Add(item);
                }
            }

            menu.Items.Add(NewSeparator());

            var manager = new MenuItem();
            manager.Style = (Style)Resources["FlyoutMenuItem"];
            manager.Header = "Менеджер раскладок…";
            manager.Click += delegate { ShowManager(); };
            menu.Items.Add(manager);

            var auto = new MenuItem();
            auto.Style = (Style)Resources["FlyoutMenuItem"];
            auto.Header = "Запускать при входе в Windows";
            auto.IsChecked = AutoStartEnabled;
            auto.Click += delegate { AutoStartEnabled = !AutoStartEnabled; };
            menu.Items.Add(auto);

            menu.Items.Add(NewSeparator());

            var exit = new MenuItem();
            exit.Style = (Style)Resources["FlyoutMenuItem"];
            exit.Header = "Выход";
            exit.Click += delegate { Quit(); };
            menu.Items.Add(exit);

            Native.SetForegroundWindow(_messageWindow.Handle);
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        /// <summary>
        /// Разделитель именно как Separator, а не как произвольный элемент: меню оборачивает
        /// посторонние элементы в обычные пункты, и в списке появляются пустые полосы.
        /// </summary>
        private Separator NewSeparator()
        {
            var sep = new Separator();
            sep.Style = (Style)Resources["MenuSeparator"];
            return sep;
        }

        private void ShowManager()
        {
            _window.ReloadLive();
            _window.ReloadProfiles(null);
            _window.Show();
            if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
            _window.Activate();
            _window.Topmost = true;
            _window.Topmost = false;
        }

        /// <summary>
        /// При запуске подхватываем вид панели того профиля, который сейчас применён:
        /// иначе после перезагрузки панель осталась бы системной до первого переключения.
        /// </summary>
        private void StartTaskbarForActiveProfile()
        {
            if (!ExplorerTap.Available) return;
            try
            {
                var live = Monitors.Enumerate();
                foreach (var layout in Store.LoadAll())
                {
                    if (!layout.MatchesCurrent(live)) continue;
                    if (layout.Taskbar != null && layout.Taskbar.AnyEnabled) TaskbarWatcher.Use(layout.Taskbar);
                    return;
                }
            }
            catch { }
        }

        private void Quit()
        {
            TaskbarWatcher.Stop();
            if (ExplorerTap.Available) ExplorerTap.RestoreAll();

            UnregisterHotkeys();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            Shutdown();
        }

        // ---------------------------------------------------------------- горячие клавиши
        private void ReloadHotkeys()
        {
            UnregisterHotkeys();
            int id = 1;
            var problems = new List<string>();

            foreach (var layout in Store.LoadAll())
            {
                if (string.IsNullOrEmpty(layout.Hotkey)) continue;
                uint mods, vk; string err;
                if (!Hotkey.TryParse(layout.Hotkey, out mods, out vk, out err)) { problems.Add(layout.Hotkey + " — " + err); continue; }
                if (Native.RegisterHotKey(_messageWindow.Handle, id, mods, vk))
                {
                    _hotkeyProfiles[id] = layout.Name;
                    id++;
                }
                else problems.Add(layout.Hotkey + " — занято другой программой");
            }

            if (problems.Count > 0)
                Balloon("Не удалось назначить: " + string.Join("; ", problems.ToArray()), Forms.ToolTipIcon.Warning);
        }

        private void UnregisterHotkeys()
        {
            foreach (int id in _hotkeyProfiles.Keys) Native.UnregisterHotKey(_messageWindow.Handle, id);
            _hotkeyProfiles.Clear();
        }

        private void ApplyByName(string name)
        {
            var layout = Store.LoadByName(name);
            if (layout == null) return;
            var result = Applier.Apply(layout);

            if (!result.Success)
                Balloon(layout.Name + ": " + result.Error, Forms.ToolTipIcon.Error);
            else if (result.Warnings.Count > 0)
                Balloon(layout.Name + ": " + string.Join(" ", result.Warnings.ToArray()), Forms.ToolTipIcon.Warning);

            if (_window != null && _window.IsVisible)
            {
                _window.ReloadLive();
                _window.ReloadProfiles(name);
            }
        }

        private void Balloon(string text, Forms.ToolTipIcon icon)
        {
            if (_tray == null) return;
            _tray.BalloonTipTitle = AppTitle;
            _tray.BalloonTipText = text;
            _tray.BalloonTipIcon = icon;
            _tray.ShowBalloonTip(6000);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Native.WM_HOTKEY)
            {
                string name;
                if (_hotkeyProfiles.TryGetValue(wParam.ToInt32(), out name)) ApplyByName(name);
                handled = true;
            }
            else if (msg == Native.WM_DISPLAYCHANGE)
            {
                if (_window != null && _window.IsVisible)
                {
                    _window.ReloadLive();
                    _window.ReloadProfiles(null);
                }
            }
            else if (_showMessage != 0 && msg == (int)_showMessage)
            {
                ShowManager();
                handled = true;
            }
            return IntPtr.Zero;
        }

        // ---------------------------------------------------------------- автозапуск
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool AutoStartEnabled
        {
            get
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                        return k != null && k.GetValue(AppName) != null;
                }
                catch { return false; }
            }
            set
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    {
                        if (k == null) return;
                        if (value) k.SetValue(AppName, "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\" /tray");
                        else if (k.GetValue(AppName) != null) k.DeleteValue(AppName, false);
                    }
                }
                catch { }
            }
        }
    }
}
