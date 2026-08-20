// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DIIISPL
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

            root.Children.Add(Ui.Head(App.AppTitle, "Раскладки мониторов · версия " + Version()));

            var stack = new StackPanel();
            stack.Children.Add(Ui.Card("Что она делает", null,
                Ui.Bullet("Держит раскладки мониторов профилями и переключает их горячей клавишей " +
                       "или из меню значка в трее."),
                Ui.Bullet("Опознаёт мониторы по EDID — по производителю, модели и серийному номеру, — " +
                       "поэтому смена порта HDMI или DisplayPort профиль не ломает."),
                Ui.Bullet("Собирает конфигурацию сама, а не просит Windows вспомнить сохранённую: " +
                       "позиции, разрешения, частоты, повороты, дублирование."),
                Ui.Bullet("Выставляет HDR после смены раскладки — одинаково на всех дублирующих " +
                       "друг друга экранах, иначе Windows выключает его у всех сразу."),
                Ui.Bullet("Задаёт вид панели задач, свой на каждое состояние экрана, и меняет его " +
                       "вместе с профилем.")));

            stack.Children.Add(Ui.Card("Чего она не делает", null,
                Ui.Bullet("Не включает HDR при дублировании, если выбранный режим не проходит " +
                       "по кабелю: это ограничение железа, а не программы."),
                Ui.Bullet("Не хранит ничего в системе, кроме своей записи автозапуска: профили " +
                       "лежат обычным текстом в %AppData%\\DIIISPL."),
                Ui.Bullet("Не переключает раскладки чаще раза в секунду — плотная очередь таких " +
                       "перестроек способна уронить видеодрайвер.")));

            stack.Children.Add(Ui.Card("На чём стоит", null,
                Ui.Note("Вид панели задач задаёт вспомогательная библиотека, работающая внутри " +
                     "проводника: снаружи панель задач Windows 11 не поддаётся. Библиотека основана " +
                     "на компоненте ExplorerTAP из программы TranslucentTB, © Charles Milette " +
                     "и участники, распространяемой под GNU GPL v3."),
                Link("Исходники TranslucentTB", "https://github.com/TranslucentTB/TranslucentTB")));

            stack.Children.Add(Ui.Card("Лицензия", "GNU General Public License v3 или новее",
                Ui.Note("Свободная программа: её можно использовать, изучать, изменять и передавать " +
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

        /// <summary>Версия без хвостовой доли: «1.0.0» читается лучше, чем «1.0.0.0».</summary>
        private static string Version()
        {
            try
            {
                string v = FileVersionInfo.GetVersionInfo(
                    Assembly.GetExecutingAssembly().Location).FileVersion;
                if (v != null && v.EndsWith(".0") && v.Split('.').Length == 4)
                    v = v.Substring(0, v.Length - 2);
                return v;
            }
            catch { return "1.0.0"; }
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
