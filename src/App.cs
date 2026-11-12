// DIIISPL — a monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

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
        private System.Windows.Threading.DispatcherTimer _recheck;
        /// <summary>What the set of displays was when we last looked. null — not looked yet.</summary>
        private List<string> _composition;
        /// <summary>Wait for quiet after WM_DISPLAYCHANGE: Windows sends them in a burst.</summary>
        private System.Windows.Threading.DispatcherTimer _settle;
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
                // already running — just show the window of the running copy
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
                // it must not crash silently: the user would see only a vanished icon
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
            MatchCultureToLanguage();
            Resources.MergedDictionaries.Add(Theme.Build());

            _messageWindow = new HwndSource(0, 0, 0, 0, 0, "DIIISPLMessages", IntPtr.Zero);
            _messageWindow.AddHook(WndProc);

            AdoptOldAutoStart();
            FixAutoStartPath();

#if DEV
            ExplorerTap.RestoreWhenProcessExits = true;   // the tray lives long — restore the taskbar on exit
#endif
            // We do not set the window icon: without it WPF takes the program's own icon, and there
            // each size has its own frame — in the title bar, on the taskbar,
            // and in Alt+Tab the window gets a fitting one, not one stretched from a single image.
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
            // In the background, and this is no decoration. Connecting to the taskbar waits for
            // a readiness signal from Explorer — up to 35 seconds per taskbar, that
            // is up to a minute and a half on three monitors. While we waited here, the program
            // window did not open at all: the person launched it, nothing
            // happened, and they launched it again. Measured on a live machine: 110
            // seconds until the window appeared, when the signal never came.
            //
            // Not getting the signal is normal: Explorer may still hold the previous
            // copy of the library (it cannot be unloaded, it pins itself for good),
            // a foreign program like TranslucentTB may interfere, an
            // antivirus may not allow it. The taskbar is a decoration of the profile, while the window is the
            // program itself; one must not wait for the first for the sake of the second.
            ThreadPool.QueueUserWorkItem(delegate { StartTaskbarForActiveProfile(); });
#endif

            CheckForUpdateQuietly();

            // ...and then once an hour. It used to ask exactly once, at
            // launch — yet the program starts together with Windows, when the network
            // is most often not up yet. Unable to get through, the check quietly gave up and
            // there was nobody to repeat it: the next attempt came only at the
            // next launch, and launches are rare, the program lives in the tray
            // for weeks.
            //
            // Measured on 26.08.2026 on a live machine: copy 0.1.2 was running,
            // 0.1.3 had come out five days earlier, the window said "checked
            // 4 days ago" — while the "Check now" button found it at once.
            //
            // An hour is the alarm period, not the frequency of requests to GitHub:
            // inside sits the condition "have 24 hours passed since the last successful
            // check". So a successful one stays at one a day, and a failed one
            // repeats after an hour until it succeeds.
            _recheck = new System.Windows.Threading.DispatcherTimer();
            _recheck.Interval = TimeSpan.FromHours(1);
            _recheck.Tick += delegate { CheckForUpdateQuietly(); };
            _recheck.Start();

            // And right away see whether another profile fits the set of displays.
            // This is the very case the whole thing was written for: the person turns on
            // the PC, the TV is available, and the layout stays yesterday's. We look not
            // this second but after a pause — after signing in to Windows the displays are still
            // negotiating among themselves.
            ScheduleAutoSwitch();

            if (!startHidden) ShowManager();
            Run();
        }

        /// <summary>
        /// Once a day ask GitHub whether a new one is out, and if so, say so
        /// with a balloon tip. Quietly: there is no need to show a failed check,
        /// and the person will open the About page themselves when they want to.
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

        // ---------------------------------------------------------------- tray
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
                    item.IsChecked = layout.MatchesCurrent(live);   // the active layout is marked with a dot
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

            // Next to autostart: both settings are about the program's behaviour,
            // not about a single profile.
            var follow = new MenuItem();
            follow.Style = (Style)Resources["FlyoutMenuItem"];
            follow.Header = Text.AutoSwitchMenu;
            follow.ToolTip = Text.AutoSwitchTip;
            follow.IsChecked = Settings.AutoSwitch;
            follow.Click += delegate
            {
                Settings.AutoSwitch = !Settings.AutoSwitch;
                // Turned on — remember the current set and wait for a real change.
                // Otherwise the switch would itself rebuild the displays, while the person
                // only asked to turn the setting on.
                if (Settings.AutoSwitch)
                    try { _composition = AutoSwitch.Composition(Monitors.Enumerate()); }
                    catch { _composition = null; }
            };
            menu.Items.Add(follow);

#if DEV
            // A manual reset — the same one that happens by itself after a profile is applied.
            // Needed because it may be late on its own: taskbars are recreated when
            // Windows sees fit, and the person can spot a stuck look
            // before the program does.
            var repaint = new MenuItem();
            repaint.Style = (Style)Resources["FlyoutMenuItem"];
            repaint.Header = Text.TrayTaskbarReset;
            repaint.IsEnabled = ExplorerTap.Available;
            repaint.Click += delegate
            {
                TaskbarWatcher.ResetCache();
                StartTaskbarForActiveProfile();
            };
            menu.Items.Add(repaint);
#endif

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
        /// The separator specifically as a Separator, not as an arbitrary element: the menu wraps
        /// foreign elements in ordinary items, and empty stripes appear in the list.
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
        /// At launch we pick up the taskbar look of the profile that is currently applied:
        /// otherwise after a reboot the taskbar would stay the system one until the first switch.
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

        /// <summary>
        /// Pull in, for the chosen language, everything else that Windows shows:
        /// the decimal separator in the refresh rate and the labels in foreign windows —
        /// the edit menu in an input box, the system message buttons.
        ///
        /// Without this an English window on a Russian Windows showed "59,94 Hz":
        /// the text was translated, but the number stayed Russian. Internal files are not
        /// affected — the program writes and reads them independently of culture.
        /// </summary>
        private static void MatchCultureToLanguage()
        {
            try
            {
                var culture = System.Globalization.CultureInfo.GetCultureInfo(Text.Code);
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
                System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
            }
            catch { /* a language from a file may have no culture in Windows — no matter */ }
        }

        /// <summary>
        /// Restart the program in another language.
        ///
        /// The setting is written here, not before the call: one can leave here with nothing —
        /// a display rebuild is under way, or the person changed their mind about closing a window with
        /// edits. A language written in advance would in that case stay in
        /// the file and change the window on the next launch, although the person cancelled.
        ///
        /// The single-instance mutex is released before the new copy launches:
        /// otherwise it would see the name taken, show this copy's window and exit, and
        /// the language change would not happen at all.
        /// </summary>
        public void RestartForLanguage(string code)
        {
            if (ApplyBusy.Active)
            {
                Balloon(Text.BusyWait, Forms.ToolTipIcon.Info);
                return;
            }
            if (_window != null && !_window.AskBeforeClosing()) return;

            string previous = Settings.Language;
            Settings.Language = code;

            try
            {
                if (_mutex != null)
                {
                    try { _mutex.ReleaseMutex(); } catch { }
                    _mutex.Close();
                    _mutex = null;
                }
                var start = new System.Diagnostics.ProcessStartInfo(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                start.UseShellExecute = true;
                System.Diagnostics.Process.Start(start);
            }
            catch
            {
                // The new copy did not come up. Closing this one would mean leaving
                // the person without the program altogether: no icon, no hotkeys,
                // and nothing said. We stay on the previous language — and again
                // take the single-instance name that was just
                // released, otherwise a second copy could appear beside this one.
                Settings.Language = previous;
                try { bool mine; _mutex = new Mutex(true, "Local\\DIIISPL.SingleInstance", out mine); }
                catch { }
                Balloon(Text.LangRestartFailed, Forms.ToolTipIcon.Warning);
                return;
            }

            Cleanup();
            Shutdown();
        }

        private void Quit()
        {
            // We do not exit in the middle of a rebuild: the work runs on a background thread, and
            // Shutdown would cut it off in the middle of driver calls, without letting it
            // set the gate mark.
            if (ApplyBusy.Active)
            {
                Balloon(Text.BusyWait, Forms.ToolTipIcon.Info);
                return;
            }

            // We ask before exiting, not via the window's Closing: Shutdown closes windows
            // ignoring cancellation — the question was shown, the answer "I changed my mind" changed
            // nothing, and the edits vanished along with the program.
            if (_window != null && !_window.AskBeforeClosing()) return;

            Cleanup();
            Shutdown();
        }

        /// <summary>Clean up after ourselves: taskbar look, hotkeys, tray icon.</summary>
        private void Cleanup()
        {
#if DEV
            TaskbarWatcher.Stop();
            if (ExplorerTap.Available) ExplorerTap.RestoreAll();
#endif
            UnregisterHotkeys();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
        }

        // ---------------------------------------------------------------- hotkeys
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
                // A namesake among our own profiles is looked for BEFORE the registration attempt: it
                // will certainly fail on the second profile — the combination was already taken by
                // the first, from this very window — and both lines would end up in the hint,
                // the first of which blames an outside program for something it
                // did not do.
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
        /// Apply a profile by name — by hotkey or from the tray menu.
        ///
        /// The work runs on a separate thread. This is the main, daily path: the button in
        /// the window was already freed from freezing, but here `Applier.Apply` was still
        /// called on the window thread — with pauses of seconds and a gate wait of up to
        /// thirty. The whole program froze, hotkey presses piled up
        /// in the message queue and after the thaw rebuilt the displays back to back —
        /// exactly the load that sent the machine to a blue screen on 19.08.2026.
        ///
        /// While a rebuild is going, we do not start a second: the gate would not let it
        /// through anyway, and a queue of waiting threads is needed by no one.
        /// </summary>
        /// <summary>
        /// Wait for quiet and see whether the set of displays has changed.
        ///
        /// Each new event pushes the decision back: Windows sends them in a burst while it
        /// chews through a connection, and a set counted midway is
        /// a set that will be gone a second later.
        /// </summary>
        private void ScheduleAutoSwitch()
        {
            if (_settle == null)
            {
                _settle = new System.Windows.Threading.DispatcherTimer();
                _settle.Interval = TimeSpan.FromSeconds(2);
                _settle.Tick += delegate { _settle.Stop(); ConsiderAutoSwitch(); };
            }
            _settle.Stop();
            _settle.Start();
        }

        /// <summary>
        /// Whether another profile fits the set of displays — and apply it if so.
        ///
        /// The decision is made on the set, not on the event, and this is the main
        /// protection against a loop: applying a layout sends WM_DISPLAYCHANGE, but
        /// the set of connected monitors does not change. So the second
        /// time nothing happens here, even if all the other locks are removed.
        /// </summary>
        private void ConsiderAutoSwitch()
        {
            if (!Settings.AutoSwitch) return;

            // Our own apply is going on right now — we will come back when it finishes.
            if (ApplyBusy.Active) { ScheduleAutoSwitch(); return; }

            List<MonitorInfo> live;
            List<Layout> profiles;
            try { live = Monitors.Enumerate(); profiles = Store.LoadAll(); }
            catch { return; }

            List<string> now = AutoSwitch.Composition(live);

            // The set is the same — so the layout changed, not the set of displays.
            // The person could have changed it, and there is no reason to come here with our own opinion.
            if (AutoSwitch.Same(now, _composition)) return;
            _composition = now;

            Layout pick = AutoSwitch.Pick(now, profiles);
            if (pick == null) return;

            // Already in place — we stay silent: otherwise every TV power-on would be answered
            // with a hint that nothing happened.
            if (pick.ChangesNothing(live)) return;

            ApplyByName(pick.Name, true);
        }

        private void ApplyByName(string name)
        {
            ApplyByName(name, false);
        }

        /// <param name="announce">Say it with a hint on success too: the person did not ask for this apply.</param>
        private void ApplyByName(string name, bool announce)
        {
            var layout = Store.LoadByName(name);
            if (layout == null) return;

            // The busy flag is one for the whole program: the window can apply too.
            // A refusal must not be silent — from the tray menu a click otherwise
            // looks like "nothing happened".
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
                    // The full breakdown goes to the window, the short one to the hint.
                    //
                    // All the warnings used to be dumped here separated by spaces, while
                    // the hint is cut at 255 characters and cut silently. This is a
                    // daily path — the key and the tray menu — and the full text
                    // was nowhere: it never reached the window, so it
                    // could not be opened and read to the end.
                    if (_window != null) _window.ShowApplyResult(layout.Name, result);

                    if (!result.Success)
                        Balloon(Text.ApplyFailedShort(layout.Name, result.Error), Forms.ToolTipIcon.Error);
                    else if (result.Warnings.Count > 0)
                        Balloon(Text.ApplyDoneShort(layout.Name, result.Warnings.Count), Forms.ToolTipIcon.Warning);
                    else if (announce)
                        Balloon(Text.AutoSwitched(layout.Name), Forms.ToolTipIcon.Info);

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
                ScheduleAutoSwitch();
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

        // ---------------------------------------------------------------- autostart
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string OldAppName = "DIIIX";      // what the program was called before

        /// <summary>
        /// Remove the autostart entry left over from the former name: it points to a file
        /// that no longer exists. If autostart was on — we move it to ourselves so that
        /// the setting does not vanish silently.
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
        /// Fix the autostart entry if it points to the wrong place.
        ///
        /// The path is written once — at the moment the person turned on
        /// autostart. Since then the program may have been moved to another folder or
        /// deleted and installed anew: the entry stays the old one, Windows at each
        /// sign-in launches a nonexistent file, and in the program the menu item
        /// is ticked. The setting is there, the work from it is not.
        ///
        /// Since the entry exists, the person turned autostart on deliberately, and our
        /// business here is only to correct the path, not to decide for them.
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

                    // We fix only an entry pointing to a nonexistent file. Not "to another" —
                    // there may be several copies of the program on the machine, and the one running
                    // has no right to silently take the Windows sign-in away from the one the
                    // person put there.
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

        /// <summary>The string with which the program registers itself in autostart.</summary>
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
