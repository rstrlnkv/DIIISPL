// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DIIISPL
{
    /// <summary>
    /// О программе: значок, версия, автор и обновление.
    ///
    /// Про чужой код и лицензию здесь ничего нет намеренно: текст лицензии едет
    /// вместе с программой файлом LICENSE.txt, чужой код описан в NOTICE.md, а в
    /// подвале страницы стоит строка с лицензией. Исключение — сборка Dev: она
    /// несёт чужие компоненты, и их лицензии показывает кнопка «Лицензии».
    /// </summary>
    internal class AboutWindow : Window
    {
        private readonly TextBlock _updateStatus = new TextBlock();
        private readonly Button _updateAction = new Button();
        private readonly Button _stableButton = new Button();
        private readonly Button _devButton = new Button();
        private readonly TextBlock _channelHint = new TextBlock();
        private readonly TextBlock _badge = new TextBlock();
        private readonly Button _copy = new Button();
        private readonly DispatcherTimer _copyReset = new DispatcherTimer();

        private Updater.Release _found;      // найденный выпуск, если он новее
        /// <summary>«Что нового» под строкой находки — пока не нашли, пусто и не видно.</summary>
        private readonly StackPanel _notes = new StackPanel();
        private bool _busy;

        public AboutWindow()
        {
            Title = Text.About;
            Width = 560;
            MinWidth = 480;
            MinHeight = 520;

            // Высота по содержимому, а не намертво заданная. Прежде стояло 700, и
            // окно всегда открывалось с полосой прокрутки — при том что не влезала
            // из него пара десятков точек. Прокрутка внутри осталась: на невысоком
            // экране она нужна, и потолок ей ставит рабочая область.
            SizeToContent = SizeToContent.Height;
            MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 80);
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

            _badge.Text = "DEV";
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
            t.Text = Text.AboutTagline;
            t.Style = (Style)Application.Current.Resources["Caption"];
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(0, 6, 0, 20);
            return t;
        }

        /// <summary>Три числа в строку: версия, номер сборки, сколько профилей.</summary>
        private UIElement Numbers()
        {
            int profiles = 0;
            try { profiles = Store.LoadAll().Count; } catch { }

            var grid = new Grid();
            for (int i = 0; i < 3; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // The full version (with -dev.N) is longer than "0.2.4", so it gets a smaller size.
            Add(grid, 0, Updater.DisplayText, Text.AboutVersion, 18);
            Add(grid, 1, BuildInfo.Number, Text.AboutBuild, 22);
            Add(grid, 2, profiles.ToString(), Text.AboutProfiles, 22);

            // Copies "version (build)", for a bug report.
            _copy.Style = (Style)Application.Current.Resources["BtnSubtle"];
            _copy.Content = Text.AboutCopy;
            _copy.Height = 30;
            _copy.HorizontalAlignment = HorizontalAlignment.Center;
            _copy.Margin = new Thickness(0, 12, 0, 0);
            _copy.Click += delegate { CopyVersion(); };

            _copyReset.Interval = TimeSpan.FromMilliseconds(1500);
            _copyReset.Tick += delegate { _copyReset.Stop(); _copy.Content = Text.AboutCopy; };
            Closed += delegate { _copyReset.Stop(); };

            var stack = new StackPanel();
            stack.Children.Add(grid);
            stack.Children.Add(_copy);

            var card = new Border();
            card.Style = (Style)Application.Current.Resources["Card"];
            card.Padding = new Thickness(18, 16, 18, 16);
            card.Margin = new Thickness(0, 0, 0, 12);
            card.Child = stack;
            return card;
        }

        private void CopyVersion()
        {
            try { Clipboard.SetText(Updater.VersionBuild); }
            catch { return; }   // the clipboard can be held by another program

            _copy.Content = Text.AboutCopied;
            _copyReset.Stop();
            _copyReset.Start();
        }

        private static void Add(Grid grid, int column, string value, string caption, double size)
        {
            var box = new StackPanel();
            box.HorizontalAlignment = HorizontalAlignment.Center;

            var big = new TextBlock();
            big.Text = value;
            big.FontSize = size;
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
            label.Text = Text.AboutAuthor;
            label.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(label);

            var right = new StackPanel();
            right.Orientation = Orientation.Horizontal;
            Grid.SetColumn(right, 1);

            var name = new TextBlock();
            name.Text = Text.AboutAuthorName;
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
            label.Text = Text.AboutChannel;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(0, 0, 12, 0);   // иначе подпись упирается в кнопку
            bottom.Children.Add(label);

            var switcher = new StackPanel();
            switcher.Orientation = Orientation.Horizontal;
            Grid.SetColumn(switcher, 1);

            _stableButton.Content = "Stable";
            _stableButton.Height = 30;
            _stableButton.MinWidth = 96;
            _stableButton.Click += delegate { SetChannel(Settings.Stable); };
            switcher.Children.Add(_stableButton);

            _devButton.Content = "Dev";
            _devButton.Height = 30;
            _devButton.MinWidth = 76;
            _devButton.Margin = new Thickness(6, 0, 0, 0);
            _devButton.Click += delegate { SetChannel(Settings.Dev); };
            switcher.Children.Add(_devButton);

            bottom.Children.Add(switcher);

            _channelHint.Style = (Style)Application.Current.Resources["Caption"];
            _channelHint.TextWrapping = TextWrapping.Wrap;
            _channelHint.Margin = new Thickness(0, 10, 0, 0);

            var stack = new StackPanel();
            stack.Children.Add(top);
            _notes.Visibility = Visibility.Collapsed;
            stack.Children.Add(_notes);
            stack.Children.Add(line);
            stack.Children.Add(bottom);
            stack.Children.Add(_channelHint);
            stack.Children.Add(LanguageRow());

            var card = new Border();
            card.Style = (Style)Application.Current.Resources["Card"];
            card.Padding = new Thickness(18, 16, 18, 16);
            card.Margin = new Thickness(0, 0, 0, 12);
            card.Child = stack;
            return card;
        }

        /// <summary>
        /// Выбор языка — рядом с каналом обновлений, в той же карточке: обе строки
        /// про то, «какой быть программе», и обе меняются раз в жизни.
        ///
        /// Кнопок ровно столько, сколько языков видит программа — встроенных и
        /// лежащих файлами, — и подписаны они на себе самих: человек, ищущий свой
        /// язык, узнаёт его по написанию, а не по переводу названия на чужой.
        /// </summary>
        private UIElement LanguageRow()
        {
            var line = new Border();
            line.Height = 1;
            line.Background = (Brush)Application.Current.Resources["Stroke"];
            line.Margin = new Thickness(0, 14, 0, 14);

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock();
            label.Text = Text.AboutLanguage;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(0, 0, 12, 0);
            row.Children.Add(label);

            var buttons = new WrapPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(buttons, 1);

            bool first = true;
            foreach (string code in Text.AvailableCodes())
            {
                string pick = code;
                var b = new Button();
                b.Content = Text.NameOf(code);
                b.Height = 30;
                b.MinWidth = 96;
                b.Margin = new Thickness(first ? 0 : 6, 0, 0, 0);
                b.Style = (Style)Application.Current.Resources[code == Text.Code ? "BtnAccent" : "Btn"];
                b.Click += delegate { SetLanguage(pick); };
                buttons.Children.Add(b);
                first = false;
            }
            row.Children.Add(buttons);

            var hint = new TextBlock();
            hint.Style = (Style)Application.Current.Resources["Caption"];
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 10, 0, 0);
            hint.Text = Text.LangRestart;

            var stack = new StackPanel();
            stack.Children.Add(line);
            stack.Children.Add(row);
            stack.Children.Add(hint);
            return stack;
        }

        /// <summary>
        /// Сменить язык. Перезапуск, а не пересборка окон на ходу: окна собраны в
        /// коде, и обойти их все, ничего не забыв, — работа, которую пришлось бы
        /// повторять при каждой новой надписи. Перезапуск честнее и не врёт.
        /// </summary>
        private void SetLanguage(string code)
        {
            if (code == Text.Code) return;

            // Text.NameOf, а не Lang.NativeName: у языка, приехавшего файлом,
            // встроенного имени нет, и вопрос звучал бы «Перейти на English?»
            // при нажатии на «Čeština».
            var ask = new ConfirmDialog(Text.LangRestartTitle(Text.NameOf(code)),
                                        Text.LangRestart, Text.LangRestartOk);
            ask.Owner = this;
            if (ask.ShowDialog() != true) return;

            // Выбор своего же системного языка записываем как «как в Windows».
            // Иначе вернуться к нему было бы нельзя: отдельной кнопки для этого
            // нет, а однажды сделанный явный выбор сильнее системы навсегда —
            // включая случай, когда человек потом сменит язык самой Windows.
            string keep = code == Text.Resolve(Text.Auto) ? Text.Auto : code;

            var app = Application.Current as App;
            if (app != null) app.RestartForLanguage(keep);
        }

        private UIElement Buttons()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Margin = new Thickness(0, 0, 0, 12);

            var news = new Button();
            news.Style = (Style)Application.Current.Resources["Btn"];
            news.Content = Text.WhatsNew;
            news.Height = 38;
            news.Margin = new Thickness(0, 0, 6, 0);
            news.Click += delegate { ShowChangelog(); };
            grid.Children.Add(news);

            var github = new Button();
            github.Style = (Style)Application.Current.Resources["Btn"];
            github.Content = "GitHub";
            github.Height = 38;
            github.Margin = new Thickness(6, 0, 0, 0);
            github.Click += delegate { Updater.OpenPage(Updater.ProjectPage); };
            Grid.SetColumn(github, 1);
            grid.Children.Add(github);

#if DEV
            // Only Dev bundles third-party components, so only Dev has a Licences page.
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
            var licenses = new Button();
            licenses.Style = (Style)Application.Current.Resources["Btn"];
            licenses.Content = Text.AboutLicenses;
            licenses.Height = 38;
            licenses.Margin = new Thickness(0, 12, 0, 0);
            licenses.Click += delegate { new LicensesWindow { Owner = this }.ShowDialog(); };
            Grid.SetRow(licenses, 1);
            Grid.SetColumnSpan(licenses, 2);
            grid.Children.Add(licenses);
#endif

            return grid;
        }

        /// <summary>История выпусков — своим окном, из того, что лежит внутри программы.</summary>
        private void ShowChangelog()
        {
            var window = new ChangelogWindow();
            window.Owner = this;
            window.ShowDialog();
        }

        private UIElement Footer()
        {
            var t = new TextBlock();
            t.Text = Text.AboutFooter(App.AppTitle);
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
            bool dev = Settings.Channel == Settings.Dev;

            _stableButton.Style = (Style)Application.Current.Resources[dev ? "Btn" : "BtnAccent"];
            _devButton.Style = (Style)Application.Current.Resources[dev ? "BtnAccent" : "Btn"];
            // Подпись должна называть разницу, а не намекать на неё. Прежде здесь
            // стояло «вместе с недоделками» — выбор между двумя каналами, о котором
            // не сказано, что в одном есть, а в другом нет.
            _channelHint.Text = dev
                ? Text.ChannelDevHint
                : Text.ChannelStableHint;

            // Метка говорит про саму сборку, а не про выбранный канал: канал — это
            // «что присылать дальше», а метка отвечает на вопрос «что у меня сейчас».
            // Пока она показывала выбор каналов, она повторяла соседние кнопки.
#if DEV
            ((UIElement)_badge.Tag).Visibility = Visibility.Visible;
#else
            ((UIElement)_badge.Tag).Visibility = Visibility.Collapsed;
#endif
        }

        /// <summary>Обычный вид: когда проверяли и кнопка «Проверить».</summary>
        private void ShowIdle()
        {
            _busy = false;
            _updateStatus.Text = Updater.Ago(Settings.LastCheck);
            ShowHighlights(null);
            _updateStatus.Foreground = (Brush)Application.Current.Resources["TextSec"];
            _updateAction.Content = Text.CheckButton;
            _updateAction.IsEnabled = true;
        }

        /// <summary>
        /// Показать «что нового» прямо здесь. Описание выпуска и так скачано:
        /// прежде его разбирали и выбрасывали, а человек шёл за тем же текстом
        /// в браузер. У старых выпусков раздела нет — тогда не показываем ничего.
        /// </summary>
        private void ShowHighlights(Updater.Release release)
        {
            _notes.Children.Clear();
            _notes.Visibility = Visibility.Collapsed;
            if (release == null) return;

            int more;
            var lines = Updater.Highlights(release.Notes, 5, out more);
            if (lines.Count == 0) return;

            foreach (string s in lines)
            {
                var t = new TextBlock();
                t.Style = (Style)Application.Current.Resources["Caption"];
                t.Text = "•  " + s;
                t.TextWrapping = TextWrapping.Wrap;
                t.Margin = new Thickness(0, 6, 0, 0);
                _notes.Children.Add(t);
            }

            if (more > 0)
            {
                var link = new TextBlock();
                link.Style = (Style)Application.Current.Resources["Caption"];
                link.Text = Text.MoreInNotes(more);
                link.Margin = new Thickness(0, 8, 0, 0);
                link.Foreground = (Brush)Application.Current.Resources["Accent"];
                link.Cursor = System.Windows.Input.Cursors.Hand;
                string page = release.Page;
                link.MouseLeftButtonUp += delegate { Updater.OpenPage(page); };
                _notes.Children.Add(link);
            }
            _notes.Visibility = Visibility.Visible;
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
            Say(Text.Checking, false);

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
                        _updateAction.Content = Text.AgainButton;
                        return;
                    }
                    if (release == null)
                    {
                        _found = null;
                        Say(Text.UpToDate, false);
                        _updateAction.Content = Text.CheckButton;
                        return;
                    }
                    _found = release;
                    Say(Text.NewVersionFound(release.Version.Major + "." + release.Version.Minor +
                                             "." + release.Version.Build), true);
                    ShowHighlights(release);
                    _updateAction.Content = Text.UpdateButton;
                    _updateAction.Style = (Style)Application.Current.Resources["BtnAccent"];
                }));
            });
        }

        private void StartInstall()
        {
            Updater.Release release = _found;
#if DEV
            // Уход со сборки Dev на общий выпуск — это не просто «версия новее»:
            // вид панели задач пропадёт вместе с этой сборкой. Сказать об этом
            // надо до установки, а не показать пустое место после неё. Сами
            // настройки в профилях переживают переход — файл их сохраняет
            // как есть и отдаёт обратно, если снова поставить Dev.
            if (release != null && !release.Early)
            {
                var warn = new ConfirmDialog(Text.DowngradeTitle,
                    Text.DowngradeBody(release.Version.Major + "." + release.Version.Minor +
                                       "." + release.Version.Build),
                    Text.DowngradeOk);
                warn.Owner = this;
                if (warn.ShowDialog() != true) return;
            }
#endif
            _busy = true;
            _updateAction.IsEnabled = false;
            Say(Text.Downloading, false);

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
                        _updateAction.Content = Text.AgainButton;
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
                    Say(Text.InstallerStarted, false);
                }));
            });
        }

        // ---------------------------------------------------------------- мелочи
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
