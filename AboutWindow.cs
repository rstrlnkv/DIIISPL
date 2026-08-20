// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DIIISPL
{
    /// <summary>
    /// О программе: значок, версия, автор, обновление и лицензия.
    ///
    /// Последнее — не формальность: часть кода взята из чужой программы под GPL,
    /// и указать это здесь и в NOTICE.md — условие лицензии, а не вежливость.
    /// </summary>
    internal class AboutWindow : Window
    {
        private readonly TextBlock _updateStatus = new TextBlock();
        private readonly Button _updateAction = new Button();
        private readonly Button _stableButton = new Button();
        private readonly Button _earlyButton = new Button();
        private readonly TextBlock _channelHint = new TextBlock();
        private readonly TextBlock _badge = new TextBlock();

        private Updater.Release _found;      // найденный выпуск, если он новее
        private bool _busy;

        public AboutWindow()
        {
            Title = "О программе";
            Width = 560;
            Height = 780;
            MinWidth = 480;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["Bg"];
            UseLayoutRounding = true;

            Content = Build();
            SourceInitialized += delegate { Fluent.ApplyWindowStyling(this); };
            ShowChannel();
            ShowIdle();
        }

        // ---------------------------------------------------------------- разметка
        private UIElement Build()
        {
            var stack = new StackPanel();
            stack.MaxWidth = 420;
            stack.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Margin = new Thickness(0, 28, 0, 24);

            stack.Children.Add(Mark());
            stack.Children.Add(NameRow());
            stack.Children.Add(Tagline());
            stack.Children.Add(Numbers());
            stack.Children.Add(Author());
            stack.Children.Add(UpdateCard());
            stack.Children.Add(Buttons());
            stack.Children.Add(Credits());
            stack.Children.Add(Footer());

            var scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Padding = new Thickness(24, 0, 18, 0);
            scroll.Content = stack;
            Loaded += delegate { scroll.ScrollToTop(); };
            return scroll;
        }

        /// <summary>Значок программы, крупно. Берётся из неё же — отдельный файл не нужен.</summary>
        private UIElement Mark()
        {
            var image = new Image();
            image.Width = 96;
            image.Height = 96;
            image.HorizontalAlignment = HorizontalAlignment.Center;
            image.Margin = new Thickness(0, 0, 0, 18);

            try
            {
                IntPtr handle = Native.LoadImage(Native.GetModuleHandle(null), "#32512",
                                                 Native.IMAGE_ICON, 256, 256, 0);
                if (handle != IntPtr.Zero)
                {
                    try
                    {
                        image.Source = Imaging.CreateBitmapSourceFromHIcon(
                            handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    }
                    finally { Native.DestroyIcon(handle); }
                }
            }
            catch { /* без значка страница всё равно читается */ }

            return image;
        }

        /// <summary>Название и, если канал ранний, метка рядом с ним.</summary>
        private UIElement NameRow()
        {
            var row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.HorizontalAlignment = HorizontalAlignment.Center;

            var name = new TextBlock();
            name.Text = App.AppTitle;
            name.FontSize = 34;
            name.FontWeight = FontWeights.SemiBold;
            name.FontFamily = (FontFamily)Application.Current.Resources["UiFontDisplay"];
            name.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(name);

            _badge.Text = "РАННИЙ";
            _badge.FontSize = 11;
            _badge.FontWeight = FontWeights.SemiBold;
            _badge.Foreground = (Brush)Application.Current.Resources["AccentText"];
            _badge.VerticalAlignment = VerticalAlignment.Center;

            var badgeBox = new Border();
            badgeBox.Background = (Brush)Application.Current.Resources["Accent"];
            badgeBox.CornerRadius = new CornerRadius(9);
            badgeBox.Padding = new Thickness(9, 3, 9, 4);
            badgeBox.Margin = new Thickness(10, 6, 0, 0);
            badgeBox.VerticalAlignment = VerticalAlignment.Center;
            badgeBox.Child = _badge;
            row.Children.Add(badgeBox);

            _badge.Tag = badgeBox;   // прячем и показываем вместе с рамкой
            return row;
        }

        private UIElement Tagline()
        {
            var t = new TextBlock();
            t.Text = "Раскладки мониторов для Windows 11";
            t.Style = (Style)Application.Current.Resources["Caption"];
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(0, 6, 0, 20);
            return t;
        }

        /// <summary>Три числа в строку: версия, сколько профилей, сколько мониторов сейчас.</summary>
        private UIElement Numbers()
        {
            int profiles = 0, monitors = 0;
            try { profiles = Store.LoadAll().Count; } catch { }
            try { monitors = Monitors.Enumerate().Count; } catch { }

            var grid = new Grid();
            for (int i = 0; i < 3; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Add(grid, 0, Updater.CurrentText, "ВЕРСИЯ");
            Add(grid, 1, profiles.ToString(), "ПРОФИЛИ");
            Add(grid, 2, monitors.ToString(), "МОНИТОРЫ");

            var card = new Border();
            card.Style = (Style)Application.Current.Resources["Card"];
            card.Padding = new Thickness(18, 16, 18, 16);
            card.Margin = new Thickness(0, 0, 0, 12);
            card.Child = grid;
            return card;
        }

        private static void Add(Grid grid, int column, string value, string caption)
        {
            var box = new StackPanel();
            box.HorizontalAlignment = HorizontalAlignment.Center;

            var big = new TextBlock();
            big.Text = value;
            big.FontSize = 22;
            big.FontWeight = FontWeights.SemiBold;
            big.HorizontalAlignment = HorizontalAlignment.Center;
            box.Children.Add(big);

            var small = new TextBlock();
            small.Text = caption;
            small.FontSize = 10;
            small.Margin = new Thickness(0, 3, 0, 0);
            small.HorizontalAlignment = HorizontalAlignment.Center;
            small.Foreground = (Brush)Application.Current.Resources["TextTer"];
            box.Children.Add(small);

            Grid.SetColumn(box, column);
            grid.Children.Add(box);
        }

        private UIElement Author()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock();
            label.Text = "Автор";
            label.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(label);

            var right = new StackPanel();
            right.Orientation = Orientation.Horizontal;
            Grid.SetColumn(right, 1);

            var name = new TextBlock();
            name.Text = "Ростислав Стрельников";
            name.VerticalAlignment = VerticalAlignment.Center;
            name.Foreground = (Brush)Application.Current.Resources["TextSec"];
            right.Children.Add(name);

            UIElement telegram = Link("@r_strlnkv", "https://t.me/r_strlnkv");
            ((FrameworkElement)telegram).Margin = new Thickness(10, 0, 0, 0);
            right.Children.Add(telegram);

            grid.Children.Add(right);

            var card = new Border();
            card.Style = (Style)Application.Current.Resources["Card"];
            card.Padding = new Thickness(18, 14, 18, 14);
            card.Margin = new Thickness(0, 0, 0, 12);
            card.Child = grid;
            return card;
        }

        /// <summary>Проверка обновлений и выбор канала — одной карточкой.</summary>
        private UIElement UpdateCard()
        {
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition());
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _updateStatus.VerticalAlignment = VerticalAlignment.Center;
            _updateStatus.TextWrapping = TextWrapping.Wrap;
            _updateStatus.Margin = new Thickness(0, 0, 12, 0);
            top.Children.Add(_updateStatus);

            _updateAction.Style = (Style)Application.Current.Resources["Btn"];
            _updateAction.MinWidth = 108;
            _updateAction.Height = 32;
            _updateAction.Click += delegate { Act(); };
            Grid.SetColumn(_updateAction, 1);
            top.Children.Add(_updateAction);

            var line = new Border();
            line.Height = 1;
            line.Background = (Brush)Application.Current.Resources["Stroke"];
            line.Margin = new Thickness(0, 14, 0, 14);

            var bottom = new Grid();
            bottom.ColumnDefinitions.Add(new ColumnDefinition());
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock();
            label.Text = "Канал обновлений";
            label.VerticalAlignment = VerticalAlignment.Center;
            bottom.Children.Add(label);

            var switcher = new StackPanel();
            switcher.Orientation = Orientation.Horizontal;
            Grid.SetColumn(switcher, 1);

            _stableButton.Content = "Стабильный";
            _stableButton.Height = 30;
            _stableButton.MinWidth = 96;
            _stableButton.Click += delegate { SetChannel(Settings.Stable); };
            switcher.Children.Add(_stableButton);

            _earlyButton.Content = "Ранний";
            _earlyButton.Height = 30;
            _earlyButton.MinWidth = 76;
            _earlyButton.Margin = new Thickness(6, 0, 0, 0);
            _earlyButton.Click += delegate { SetChannel(Settings.Early); };
            switcher.Children.Add(_earlyButton);

            bottom.Children.Add(switcher);

            _channelHint.Style = (Style)Application.Current.Resources["Caption"];
            _channelHint.TextWrapping = TextWrapping.Wrap;
            _channelHint.Margin = new Thickness(0, 10, 0, 0);

            var stack = new StackPanel();
            stack.Children.Add(top);
            stack.Children.Add(line);
            stack.Children.Add(bottom);
            stack.Children.Add(_channelHint);

            var card = new Border();
            card.Style = (Style)Application.Current.Resources["Card"];
            card.Padding = new Thickness(18, 16, 18, 16);
            card.Margin = new Thickness(0, 0, 0, 12);
            card.Child = stack;
            return card;
        }

        private UIElement Buttons()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Margin = new Thickness(0, 0, 0, 12);

            var news = new Button();
            news.Style = (Style)Application.Current.Resources["Btn"];
            news.Content = "Что нового";
            news.Height = 38;
            news.Margin = new Thickness(0, 0, 6, 0);
            news.Click += delegate { Updater.OpenPage(Updater.ReleasesPage); };
            grid.Children.Add(news);

            var github = new Button();
            github.Style = (Style)Application.Current.Resources["Btn"];
            github.Content = "GitHub";
            github.Height = 38;
            github.Margin = new Thickness(6, 0, 0, 0);
            github.Click += delegate { Updater.OpenPage(Updater.ProjectPage); };
            Grid.SetColumn(github, 1);
            grid.Children.Add(github);

            return grid;
        }

        /// <summary>Чужой код внутри программы и лицензия. Убирать это нельзя.</summary>
        private UIElement Credits()
        {
            return Ui.Card("На чём стоит", null,
                Ui.Note("Вид панели задач задаёт вспомогательная библиотека, работающая внутри " +
                     "проводника: снаружи панель задач Windows 11 не поддаётся. Библиотека основана " +
                     "на компоненте ExplorerTAP из программы TranslucentTB, © Charles Milette " +
                     "и участники, распространяемой под GNU GPL v3."),
                Link("Исходники TranslucentTB", "https://github.com/TranslucentTB/TranslucentTB"),
                Ui.Note("DIIISPL — свободная программа: её можно использовать, изучать, изменять " +
                     "и передавать дальше на условиях GNU General Public License версии 3 или любой " +
                     "более поздней. Программа распространяется без всяких гарантий."),
                Link("Открыть текст лицензии", LicensePath()));
        }

        private UIElement Footer()
        {
            var t = new TextBlock();
            t.Text = "© 2026 " + App.AppTitle + " · GPL-3.0";
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(0, 8, 0, 0);
            t.FontSize = 11;
            t.Foreground = (Brush)Application.Current.Resources["TextTer"];
            return t;
        }

        // ---------------------------------------------------------------- обновление
        private void SetChannel(string channel)
        {
            if (Settings.Channel == channel) return;
            Settings.Channel = channel;
            _found = null;
            ShowChannel();
            ShowIdle();
        }

        private void ShowChannel()
        {
            bool early = Settings.Channel == Settings.Early;

            _stableButton.Style = (Style)Application.Current.Resources[early ? "Btn" : "BtnAccent"];
            _earlyButton.Style = (Style)Application.Current.Resources[early ? "BtnAccent" : "Btn"];
            _channelHint.Text = early
                ? "Ранние сборки приходят раньше остальных — вместе с недоделками."
                : "Только готовые версии.";

            ((UIElement)_badge.Tag).Visibility = early ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Обычный вид: когда проверяли и кнопка «Проверить».</summary>
        private void ShowIdle()
        {
            _busy = false;
            _updateStatus.Text = Updater.Ago(Settings.LastCheck);
            _updateStatus.Foreground = (Brush)Application.Current.Resources["TextSec"];
            _updateAction.Content = "Проверить";
            _updateAction.IsEnabled = true;
        }

        private void Say(string text, bool alarming)
        {
            _updateStatus.Text = text;
            _updateStatus.Foreground = (Brush)Application.Current.Resources[alarming ? "Text" : "TextSec"];
        }

        /// <summary>Одна кнопка на два действия: сперва проверить, потом поставить.</summary>
        private void Act()
        {
            if (_busy) return;
            if (_found == null) StartCheck();
            else StartInstall();
        }

        private void StartCheck()
        {
            _busy = true;
            _updateAction.IsEnabled = false;
            Say("проверяю…", false);

            string channel = Settings.Channel;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                Updater.Release release = Updater.Check(channel, out error);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _busy = false;
                    _updateAction.IsEnabled = true;
                    if (error != null)
                    {
                        Say(error, true);
                        _updateAction.Content = "Ещё раз";
                        return;
                    }
                    if (release == null)
                    {
                        _found = null;
                        Say("установлена последняя версия", false);
                        _updateAction.Content = "Проверить";
                        return;
                    }
                    _found = release;
                    Say("вышла версия " + release.Version.Major + "." + release.Version.Minor +
                        "." + release.Version.Build, true);
                    _updateAction.Content = "Обновить";
                    _updateAction.Style = (Style)Application.Current.Resources["BtnAccent"];
                }));
            });
        }

        private void StartInstall()
        {
            Updater.Release release = _found;
            _busy = true;
            _updateAction.IsEnabled = false;
            Say("скачиваю установщик…", false);

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                string file = Updater.Download(release, out error);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (file == null)
                    {
                        _busy = false;
                        _updateAction.IsEnabled = true;
                        _updateAction.Content = "Ещё раз";
                        Say(error, true);
                        return;
                    }

                    string problem;
                    if (!Updater.Install(file, out problem))
                    {
                        _busy = false;
                        _updateAction.IsEnabled = true;
                        Say(problem, true);
                        return;
                    }
                    Say("установщик запущен", false);
                }));
            });
        }

        // ---------------------------------------------------------------- мелочи
        /// <summary>Лицензия лежит рядом с программой; у собранной вручную — под именем LICENSE.</summary>
        private static string LicensePath()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string txt = System.IO.Path.Combine(dir, "LICENSE.txt");
                if (System.IO.File.Exists(txt)) return txt;
                return System.IO.Path.Combine(dir, "LICENSE");
            }
            catch { return "LICENSE"; }
        }

        /// <summary>Ссылка, открываемая тем, чем её открывает система.</summary>
        private static UIElement Link(string text, string target)
        {
            var button = new Button();
            button.Style = (Style)Application.Current.Resources["BtnSubtle"];
            button.Content = text;
            button.Padding = new Thickness(0);
            button.HorizontalAlignment = HorizontalAlignment.Left;
            button.Foreground = (Brush)Application.Current.Resources["Accent"];
            button.Click += delegate { Updater.OpenPage(target); };
            return button;
        }
    }
}
