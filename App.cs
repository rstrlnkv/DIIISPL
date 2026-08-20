// DIIISPL — менеджер раскладок мониторов для Windows.
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

namespace DIIISPL
{
    internal class App : Application
    {
        public const string AppName = "DIIISPL";
        public const string AppTitle = "DIIISPL";

        private Forms.NotifyIcon _tray;
        private MainWindow _window;
        private HwndSource _messageWindow;
        private readonly Dictionary<int, string> _hotkeyProfiles = new Dictionary<int, string>();
        private static uint _showMessage;
        private static Mutex _mutex;

        [STAThread]
        private static int Main(string[] args)
        {
            _showMessage = Native.RegisterWindowMessage("DIIISPLShowManager");

            if (args.Length > 0 && args[0].TrimStart('-', '/').ToLowerInvariant() != "tray")
                return Cli.Run(args);

            bool created;
            _mutex = new Mutex(true, "Local\\DIIISPL.SingleInstance", out created);
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
                        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" +
                        Environment.NewLine + ex);
                }
                catch { }
                Forms.MessageBox.Show(Text.StartFailed(AppTitle, ex.Message, Store.AppFolder), AppTitle);
                return 1;
            }
            return 0;
        }

        private void Start(bool startHidden)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Resources.MergedDictionaries.Add(Theme.Build());

            _messageWindow = new HwndSource(0, 0, 0, 0, 0, "DIIISPLMessages", IntPtr.Zero);
            _messageWindow.AddHook(WndProc);

            AdoptOldAutoStart();
            FixAutoStartPath();

#if DEV
            ExplorerTap.RestoreWhenProcessExits = true;   // трей живёт долго — при выходе вернём панель
#endif
            // Значок окну не задаём: без него WPF берёт значок самой программы, а там
            // под каждый размер лежит свой кадр — и в заголовке, и на панели задач,
            // и в Alt+Tab окно получает подходящий, а не растянутый из одного.
            _window = new MainWindow();
            _window.ProfilesChanged += ReloadHotkeys;
            _window.ApplyReported += delegate(string text, bool alarming)
            {
                Balloon(text, alarming ? Forms.ToolTipIcon.Error : Forms.ToolTipIcon.Info);
            };
            _window.HiddenToTray += delegate
            {
                if (Settings.TrayHintShown) return;
                Settings.TrayHintShown = true;
                Balloon(Text.TrayHint, Forms.ToolTipIcon.Info);
            };

            BuildTray();
            ReloadHotkeys();
#if DEV
            StartTaskbarForActiveProfile();
#endif

            CheckForUpdateQuietly();

            if (!startHidden) ShowManager();
            Run();
        }

        /// <summary>
        /// Раз в сутки спросить GitHub, не вышло ли новое, и если вышло — сказать
        /// всплывающей подсказкой. Молча: неудачную проверку показывать незачем,
        /// а страницу «О программе» человек откроет сам, когда захочет.
        /// </summary>
        private void CheckForUpdateQuietly()
        {
            DateTime? last = Settings.LastCheck;
            if (last.HasValue && (DateTime.UtcNow - last.Value).TotalHours < 24) return;

            string channel = Settings.Channel;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                Updater.Release release = Updater.Check(channel, out error);
                if (release == null || error != null) return;

                string version = release.Version.Major + "." + release.Version.Minor + "." + release.Version.Build;
                if (Settings.SkippedVersion == version) return;

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    Balloon(Text.NewVersionBalloon(version), Forms.ToolTipIcon.Info);
                }));
            });
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
                empty.Header = Text.NoProfilesYet;
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
            manager.Header = Text.OpenManager;
            manager.Click += delegate { ShowManager(); };
            menu.Items.Add(manager);

            var auto = new MenuItem();
            auto.Style = (Style)Resources["FlyoutMenuItem"];
            auto.Header = Text.RunAtLogon;
            auto.IsChecked = AutoStartEnabled;
            auto.Click += delegate { AutoStartEnabled = !AutoStartEnabled; };
            menu.Items.Add(auto);

            var about = new MenuItem();
            about.Style = (Style)Resources["FlyoutMenuItem"];
            about.Header = Text.AboutMenu;
            about.Click += delegate { _window.ShowAbout(); };
            menu.Items.Add(about);

            menu.Items.Add(NewSeparator());

            var exit = new MenuItem();
            exit.Style = (Style)Resources["FlyoutMenuItem"];
            exit.Header = Text.Exit;
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
            _window.RefreshProfiles();
            _window.Show();
            if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
            _window.Activate();
            _window.Topmost = true;
            _window.Topmost = false;
        }

#if DEV
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
#endif

        private void Quit()
        {
            // Посреди перестроения не выходим: работа идёт в фоновом потоке, и
            // Shutdown оборвал бы её на середине вызовов в драйвер, не дав
            // поставить отметку затвора.
            if (ApplyBusy.Active)
            {
                Balloon(Text.BusyWait, Forms.ToolTipIcon.Info);
                return;
            }

            // Спрашиваем до выхода, а не через Closing окна: Shutdown закрывает окна,
            // не глядя на отмену, — вопрос показывался, ответ «я передумал» ничего
            // не менял, и правки пропадали вместе с программой.
            if (_window != null && !_window.AskBeforeClosing()) return;

#if DEV
            TaskbarWatcher.Stop();
            if (ExplorerTap.Available) ExplorerTap.RestoreAll();
#endif

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
            var taken = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var layout in Store.LoadAll())
            {
                if (string.IsNullOrEmpty(layout.Hotkey)) continue;
                uint mods, vk; string err;
                if (!Hotkey.TryParse(layout.Hotkey, out mods, out vk, out err)) { problems.Add(Text.HotkeyProblem(layout.Hotkey, err)); continue; }
                // Тёзку среди своих же профилей ищем ДО попытки регистрации: она
                // на втором профиле заведомо не пройдёт — сочетание уже занял
                // первый, из этого же окна, — и в подсказку уехали бы обе строки,
                // первая из которых валит на постороннюю программу то, чего она
                // не делала.
                string already;
                if (taken.TryGetValue(layout.Hotkey, out already))
                {
                    problems.Add(Text.HotkeyShared(layout.Hotkey, already, layout.Name));
                    continue;
                }
                taken[layout.Hotkey] = layout.Name;

                if (Native.RegisterHotKey(_messageWindow.Handle, id, mods, vk))
                {
                    _hotkeyProfiles[id] = layout.Name;
                    id++;
                }
                else problems.Add(Text.HotkeyTakenElsewhere(layout.Hotkey));
            }

            if (problems.Count > 0)
                Balloon(Text.HotkeysNotSet(string.Join("; ", problems.ToArray())), Forms.ToolTipIcon.Warning);
        }

        private void UnregisterHotkeys()
        {
            foreach (int id in _hotkeyProfiles.Keys) Native.UnregisterHotKey(_messageWindow.Handle, id);
            _hotkeyProfiles.Clear();
        }

        /// <summary>
        /// Применить профиль по имени — горячей клавишей или из меню значка.
        ///
        /// Работа идёт в стороннем потоке. Это главный, ежедневный путь: кнопку в
        /// окне от заморозки уже избавили, а здесь `Applier.Apply` по-прежнему
        /// вызывался в потоке окна — с паузами на секунды и ожиданием затвора до
        /// тридцати. Программа замирала целиком, нажатия горячей клавиши копились
        /// в очереди сообщений и после разморозки перестраивали дисплеи подряд —
        /// ровно та нагрузка, из-за которой 19.08.2026 машина ушла в синий экран.
        ///
        /// Пока перестроение идёт, второе не начинаем: затвор его всё равно не
        /// пустит, а очередь из ждущих потоков не нужна никому.
        /// </summary>
        private void ApplyByName(string name)
        {
            var layout = Store.LoadByName(name);
            if (layout == null) return;

            // Признак занятости один на всю программу: применять умеет и окно.
            // Отказ молчаливым быть не должен — из меню значка щелчок иначе
            // выглядит как «ничего не произошло».
            if (!ApplyBusy.Begin())
            {
                Balloon(Text.BusyWait, Forms.ToolTipIcon.Info);
                return;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                ApplyResult result;
                try { result = Applier.Apply(layout); }
                catch (Exception ex)
                {
                    result = new ApplyResult();
                    result.Error = ex.Message;
                }
                finally { ApplyBusy.End(); }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    // Полный разбор — в окно, короткое — в подсказку.
                    //
                    // Прежде сюда сваливались все предупреждения через пробел, а
                    // подсказка обрезается на 255 знаках и обрезается молча. Это
                    // ежедневный путь — клавиша и меню значка, — и полного текста
                    // при этом не было нигде: в окно он не попадал вовсе, так что
                    // открыть его и дочитать было нельзя.
                    if (_window != null) _window.ShowApplyResult(layout.Name, result);

                    if (!result.Success)
                        Balloon(Text.ApplyFailedShort(layout.Name, result.Error), Forms.ToolTipIcon.Error);
                    else if (result.Warnings.Count > 0)
                        Balloon(Text.ApplyDoneShort(layout.Name, result.Warnings.Count), Forms.ToolTipIcon.Warning);

                    if (_window != null && _window.IsVisible)
                    {
                        _window.ReloadLive();
                        _window.RefreshProfiles();
                    }
                }));
            });
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
                    _window.RefreshProfiles();
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
        private const string OldAppName = "DIIIX";      // как программа называлась раньше

        /// <summary>
        /// Убрать запись автозапуска, оставшуюся от прежнего имени: она указывает на файл,
        /// которого больше нет. Если автозапуск был включён — переносим его на себя, чтобы
        /// настройка не пропала молча.
        /// </summary>
        private static void AdoptOldAutoStart()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null || k.GetValue(OldAppName) == null) return;
                    k.DeleteValue(OldAppName, false);
                    if (k.GetValue(AppName) == null) AutoStartEnabled = true;
                }
            }
            catch { }
        }

        /// <summary>
        /// Поправить запись автозапуска, если она указывает не туда.
        ///
        /// Путь записывается один раз — в тот момент, когда человек включил
        /// автозапуск. Программу с тех пор могли переставить в другую папку или
        /// удалить и поставить заново: запись остаётся прежней, Windows каждый
        /// вход запускает несуществующий файл, а в программе пункт меню при этом
        /// отмечен галочкой. Настройка есть, работы от неё нет.
        ///
        /// Раз запись существует — автозапуск человек включал сознательно, и наше
        /// дело здесь только выправить путь, а не решать за него.
        /// </summary>
        private static void FixAutoStartPath()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    string current = k.GetValue(AppName) as string;
                    if (string.IsNullOrEmpty(current)) return;

                    // Правим только запись на несуществующий файл. Не «на другой» —
                    // копий программы на машине может быть несколько, и запущенная
                    // не имеет права молча забирать вход в Windows у той, которую
                    // человек туда поставил.
                    string path = current.Trim();
                    if (path.StartsWith("\""))
                    {
                        int end = path.IndexOf('"', 1);
                        if (end < 0) return;
                        path = path.Substring(1, end - 1);
                    }
                    else
                    {
                        int space = path.IndexOf(' ');
                        if (space > 0) path = path.Substring(0, space);
                    }

                    if (System.IO.File.Exists(path)) return;
                    k.SetValue(AppName, AutoStartCommand);
                }
            }
            catch { }
        }

        /// <summary>Строка, которой программа прописывает себя в автозапуск.</summary>
        private static string AutoStartCommand
        {
            get
            {
                return "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\" /tray";
            }
        }

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
                        if (value) k.SetValue(AppName, AutoStartCommand);
                        else if (k.GetValue(AppName) != null) k.DeleteValue(AppName, false);
                    }
                }
                catch { }
            }
        }
    }
}
