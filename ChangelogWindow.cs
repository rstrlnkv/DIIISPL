// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DIIISPL
{
    /// <summary>
    /// Что менялось от версии к версии — вся история, лежащая внутри программы.
    ///
    /// Прежде кнопка «Что нового» уводила в браузер, на страницу выпусков. Это
    /// работало ровно до того случая, ради которого кнопка и нужна: у копии,
    /// которая новее последнего выпуска — или собрана на своей машине, — читать
    /// там было нечего. Теперь читать нечего только если чейнджлог не попал
    /// в сборку.
    /// </summary>
    internal class ChangelogWindow : Window
    {
        public ChangelogWindow()
        {
            Title = Text.WhatsNew;
            Width = 600;
            MinWidth = 480;
            Height = 640;
            MinHeight = 380;
            MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 80);
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["Bg"];
            UseLayoutRounding = true;

            Content = Build();
            SourceInitialized += delegate { Fluent.ApplyWindowStyling(this); };
        }

        private UIElement Build()
        {
            var stack = new StackPanel();
            stack.Margin = new Thickness(24, 20, 24, 20);

            var entries = Changelog.Read();
            if (entries.Count == 0)
            {
                stack.Children.Add(Ui.Note(Text.ChangelogEmpty));
            }
            else
            {
                // Своя версия — та, с которой сравниваются выпуски. Сравниваем
                // числами, а не строками: «0.2.0» в файле и «0.2.0.0» у сборки —
                // одно и то же, а «0.1.4-dev.1» отличается от «0.1.4» только
                // суффиксом, который к сравнению отношения не имеет.
                Version mine = Updater.Current;

                foreach (ChangelogEntry e in entries)
                {
                    Version v = Updater.ParseVersion(e.Version);
                    bool yours = v != null && v.Major == mine.Major
                                           && v.Minor == mine.Minor
                                           && v.Build == mine.Build;

                    string subtitle = Pretty(e.Date);
                    if (yours)
                        subtitle = subtitle.Length > 0 ? subtitle + " · " + Text.ChangelogYours : Text.ChangelogYours;

                    var parts = new System.Collections.Generic.List<UIElement>();
                    if (e.Note.Length > 0)
                    {
                        TextBlock note = Ui.Note(e.Note);
                        note.Margin = new Thickness(0, 10, 0, 0);
                        parts.Add(note);
                    }
                    foreach (string line in e.Lines)
                    {
                        UIElement bullet = Ui.Bullet(line);
                        ((FrameworkElement)bullet).Margin = new Thickness(0, 8, 0, 0);
                        parts.Add(bullet);
                    }

                    stack.Children.Add(Ui.Card(e.Version, subtitle, parts.ToArray()));
                }
            }

            var scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Content = stack;
            return scroll;
        }

        /// <summary>
        /// Дата в файле записана как «2026-08-26» — так её не спутает ни один
        /// читатель и ни одна страна. В окне показываем привычную местную запись,
        /// а не разобрали — оставляем как есть.
        /// </summary>
        private static string Pretty(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "";
            DateTime parsed;
            if (DateTime.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out parsed))
                return parsed.ToString("d", CultureInfo.CurrentCulture);
            return iso;
        }
    }
}
