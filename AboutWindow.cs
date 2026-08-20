// DIIIX — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DIIIX
{
    /// <summary>
    /// О программе: что она делает, чего не делает, на чём стоит и под какой лицензией
    /// распространяется. Последнее здесь не формальность: часть кода взята из чужой
    /// программы под GPL, и указать это — условие лицензии, а не вежливость.
    /// </summary>
    internal class AboutWindow : Window
    {
        public AboutWindow()
        {
            Title = "О программе";
            Width = 620;
            Height = 640;
            MinWidth = 520;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["Bg"];
            UseLayoutRounding = true;

            Content = Build();
            SourceInitialized += delegate { Fluent.ApplyWindowStyling(this); };
        }

        private UIElement Build()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var head = new StackPanel();
            head.Margin = new Thickness(28, 18, 28, 10);
            var title = new TextBlock();
            title.Text = App.AppTitle;
            title.Style = (Style)Application.Current.Resources["Subtitle"];
            head.Children.Add(title);
            var hint = new TextBlock();
            hint.Text = "Раскладки мониторов · версия " + Version();
            hint.Style = (Style)Application.Current.Resources["Caption"];
            hint.Margin = new Thickness(0, 4, 0, 0);
            head.Children.Add(hint);
            root.Children.Add(head);

            var stack = new StackPanel();
            stack.Children.Add(Card("Что она делает", null,
                Bullet("Держит раскладки мониторов профилями и переключает их горячей клавишей " +
                       "или из меню значка в трее."),
                Bullet("Опознаёт мониторы по EDID — по производителю, модели и серийному номеру, — " +
                       "поэтому смена порта HDMI или DisplayPort профиль не ломает."),
                Bullet("Собирает конфигурацию сама, а не просит Windows вспомнить сохранённую: " +
                       "позиции, разрешения, частоты, повороты, дублирование."),
                Bullet("Выставляет HDR после смены раскладки — одинаково на всех выходах группы " +
                       "дублирования, иначе Windows уводит группу в SDR."),
                Bullet("Задаёт вид панели задач, свой на каждое состояние экрана, и меняет его " +
                       "вместе с профилем.")));

            stack.Children.Add(Card("Чего она не делает", null,
                Bullet("Не включает HDR при дублировании, если общий режим не проходит по " +
                       "пропускной способности: это ограничение линка, а не программы."),
                Bullet("Не хранит ничего в системе, кроме своей записи автозапуска: профили " +
                       "лежат обычным текстом в %AppData%\\DIIIX."),
                Bullet("Не переключает раскладки чаще раза в секунду — плотная очередь таких " +
                       "перестроек способна уронить видеодрайвер.")));

            stack.Children.Add(Card("На чём стоит", null,
                Note("Вид панели задач задаёт библиотека DIIIX.ExplorerTAP.dll, работающая внутри " +
                     "проводника. Она основана на компоненте ExplorerTAP из программы TranslucentTB, " +
                     "© Charles Milette и участники, распространяемой под GNU GPL v3. Снаружи панель " +
                     "задач Windows 11 не поддаётся: её фон рисует собственное дерево XAML."),
                Link("Исходники TranslucentTB", "https://github.com/TranslucentTB/TranslucentTB")));

            stack.Children.Add(Card("Лицензия", "GNU General Public License v3 или новее",
                Note("Свободная программа: её можно использовать, изучать, изменять и передавать " +
                     "дальше на условиях GNU General Public License версии 3 или любой более поздней. " +
                     "Программа распространяется без всяких гарантий. Полный текст лицензии — в файле " +
                     "LICENSE.txt рядом с программой."),
                Link("Открыть текст лицензии", LicensePath())));

            var scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Padding = new Thickness(28, 4, 22, 8);
            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            // Иначе окно открывается прокрученным: фокус достаётся первой ссылке внутри
            // карточек, а список подтягивает её в видимую часть.
            Loaded += delegate { scroll.ScrollToTop(); };

            var bottom = new Grid();
            bottom.Margin = new Thickness(28, 12, 28, 18);
            var close = new Button();
            close.Style = (Style)Application.Current.Resources["BtnAccent"];
            close.Content = "Закрыть";
            close.MinWidth = 120;
            close.Height = 36;
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.Click += delegate { Close(); };
            close.IsDefault = true;
            Loaded += delegate { close.Focus(); };
            bottom.Children.Add(close);
            Grid.SetRow(bottom, 2);
            root.Children.Add(bottom);

            return root;
        }

        private static string Version()
        {
            try
            {
                return FileVersionInfo.GetVersionInfo(
                    Assembly.GetExecutingAssembly().Location).FileVersion;
            }
            catch { return "1.0.0.0"; }
        }

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

        // ---------------------------------------------------------------- блоки
        private static Border Card(string title, string subtitle, params UIElement[] children)
        {
            var card = new Border();
            card.Style = (Style)Application.Current.Resources["Card"];
            card.Padding = new Thickness(18, 15, 18, 16);
            card.Margin = new Thickness(0, 0, 0, 12);

            var stack = new StackPanel();
            if (!string.IsNullOrEmpty(title))
            {
                var t = new TextBlock();
                t.Text = title;
                t.FontWeight = FontWeights.SemiBold;
                t.FontSize = 14;
                stack.Children.Add(t);
            }
            if (!string.IsNullOrEmpty(subtitle))
            {
                var s = new TextBlock();
                s.Text = subtitle;
                s.Margin = new Thickness(0, 2, 0, 0);
                s.TextWrapping = TextWrapping.Wrap;
                s.Style = (Style)Application.Current.Resources["Caption"];
                stack.Children.Add(s);
            }
            foreach (UIElement child in children)
            {
                var fe = child as FrameworkElement;
                if (fe != null)
                    fe.Margin = new Thickness(fe.Margin.Left, fe.Margin.Top + 12,
                                              fe.Margin.Right, fe.Margin.Bottom);
                stack.Children.Add(child);
            }
            card.Child = stack;
            return card;
        }

        private static TextBlock Note(string text)
        {
            var t = new TextBlock();
            t.Text = text;
            t.TextWrapping = TextWrapping.Wrap;
            t.LineHeight = 19;
            t.Style = (Style)Application.Current.Resources["Caption"];
            return t;
        }

        private static UIElement Bullet(string text)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dot = new TextBlock();
            dot.Text = "—";
            dot.VerticalAlignment = VerticalAlignment.Top;
            dot.Style = (Style)Application.Current.Resources["Caption"];

            TextBlock body = Note(text);
            body.Margin = new Thickness(0);

            Grid.SetColumn(dot, 0);
            Grid.SetColumn(body, 1);
            grid.Children.Add(dot);
            grid.Children.Add(body);
            return grid;
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
            button.Click += delegate
            {
                try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
                catch { /* нечем открыть — молча, ради ссылки ругаться не на что */ }
            };
            return button;
        }
    }
}
