// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

#if DEV
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DIIISPL
{
    /// <summary>
    /// Licences of the third-party components the Dev build bundles (the Stable build
    /// bundles none, so it has no such window). The texts are embedded by build.cmd as
    /// resources; NOTICE.md stays the authoritative list.
    ///
    /// The TranslucentTB card shows the repository's own LICENSE: the upstream
    /// LICENSE.md is the plain GNU GPL v3 text with no project copyright line, so it is
    /// the same text, not a substitute.
    /// </summary>
    internal class LicensesWindow : Window
    {
        public LicensesWindow()
        {
            Title = Text.LicensesTitle;
            Width = 620;
            MinWidth = 480;
            Height = 660;
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
            stack.Margin = new Thickness(28, 22, 28, 24);

            TextBlock note = Ui.Note(Text.LicensesNote);
            note.Margin = new Thickness(0, 0, 0, 14);
            stack.Children.Add(note);

            // Component names, holders' proper names and licence ids stay as they are; only the
            // plain words come from Lang. The repository's LICENSE is the plain GPL v3 text, the
            // same text as upstream TranslucentTB's LICENSE.md (which has no project copyright line).
            stack.Children.Add(Card("TranslucentTB ExplorerTAP — " + Text.LicensesTranslucentHolder + " — " + Text.LicensesGplOnly,
                                    "licenses.gpl3", Text.LicensesSameGpl));
            stack.Children.Add(Card("Detours — Microsoft Corporation — MIT", "licenses.detours", null));
            stack.Children.Add(Card("wil — Microsoft Corporation — MIT", "licenses.wil", null));
            stack.Children.Add(Card("C++/WinRT — Microsoft Corporation — MIT", "licenses.cppwinrt", null));

            var scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Content = stack;
            return scroll;
        }

        /// <summary>One component: a heading, an optional note and the licence text, selectable.</summary>
        private static UIElement Card(string heading, string resource, string note)
        {
            var title = new TextBlock();
            title.Text = heading;
            title.FontWeight = FontWeights.SemiBold;
            title.TextWrapping = TextWrapping.Wrap;

            var body = new TextBox();
            body.Text = Read(resource);
            body.IsReadOnly = true;
            body.TextWrapping = TextWrapping.Wrap;
            // No inner scrolling: the window's single ScrollViewer scrolls (an inner box would swallow the wheel).
            body.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            body.FontFamily = new FontFamily("Consolas");
            body.FontSize = 11;
            body.Foreground = (Brush)Application.Current.Resources["TextSec"];
            body.Background = Brushes.Transparent;
            body.BorderThickness = new Thickness(0);
            body.Margin = new Thickness(0, 8, 0, 0);
            // The TextBox's template ScrollViewer marks the wheel handled even when it cannot scroll, so forward it to the outer one.
            body.PreviewMouseWheel += delegate(object s, System.Windows.Input.MouseWheelEventArgs e)
            {
                var outer = FindOuter((DependencyObject)s);
                if (outer != null) outer.ScrollToVerticalOffset(outer.VerticalOffset - e.Delta * SystemParameters.WheelScrollLines * 16.0 / 120);
                e.Handled = true;
            };

            var stack = new StackPanel();
            stack.Children.Add(title);
            if (note != null)
            {
                TextBlock hint = Ui.Note(note);
                hint.Margin = new Thickness(0, 4, 0, 0);
                stack.Children.Add(hint);
            }
            stack.Children.Add(body);

            var card = new Border();
            card.Style = (Style)Application.Current.Resources["Card"];
            card.Padding = new Thickness(18, 14, 18, 14);
            card.Margin = new Thickness(0, 0, 0, 12);
            card.Child = stack;
            return card;
        }

        private static ScrollViewer FindOuter(DependencyObject d)
        {
            while (d != null && !(d is ScrollViewer)) d = VisualTreeHelper.GetParent(d);
            return d as ScrollViewer;
        }

        /// <summary>The embedded text, or the "missing" line if the build lacks it.</summary>
        private static string Read(string resource)
        {
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    if (s == null) return Text.LicensesMissing;
                    using (var reader = new StreamReader(s, Encoding.UTF8)) return reader.ReadToEnd();
                }
            }
            catch { return Text.LicensesMissing; }
        }
    }
}
#endif
