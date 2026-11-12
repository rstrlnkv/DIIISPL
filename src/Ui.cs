// DIIISPL — a monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DIIISPL
{
    /// <summary>
    /// Building blocks of the windows: a card, a row with a label and a hint, a footnote.
    /// Kept in one place so the screens look alike not by coincidence
    /// but because they are built from the same parts.
    /// </summary>
    internal static class Ui
    {
        /// <summary>A card with a title and content.</summary>
        public static Border Card(string title, string subtitle, params UIElement[] children)
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

            bool first = true;
            foreach (UIElement child in children)
            {
                if (child == null) continue;
                var fe = child as FrameworkElement;
                if (fe != null)
                {
                    double gap = first && string.IsNullOrEmpty(title) ? 0 : 12;
                    fe.Margin = new Thickness(fe.Margin.Left, fe.Margin.Top + gap,
                                              fe.Margin.Right, fe.Margin.Bottom);
                }
                stack.Children.Add(child);
                first = false;
            }
            card.Child = stack;
            return card;
        }

        /// <summary>A settings row: label with hint on the left, the control on the right.</summary>
        public static UIElement Row(string label, string hint, UIElement control, double controlWidth)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(controlWidth) });

            var left = new StackPanel();
            left.VerticalAlignment = VerticalAlignment.Center;
            left.Margin = new Thickness(0, 0, 16, 0);

            var caption = new TextBlock();
            caption.Text = label;
            caption.TextWrapping = TextWrapping.Wrap;
            left.Children.Add(caption);

            if (!string.IsNullOrEmpty(hint))
            {
                var h = new TextBlock();
                h.Text = hint;
                h.TextWrapping = TextWrapping.Wrap;
                h.Margin = new Thickness(0, 2, 0, 0);
                h.Style = (Style)Application.Current.Resources["Caption"];
                left.Children.Add(h);
            }

            Grid.SetColumn(left, 0);
            Grid.SetColumn(control, 1);
            grid.Children.Add(left);
            grid.Children.Add(control);
            return grid;
        }

        /// <summary>A hint in small type.</summary>
        public static TextBlock Note(string text)
        {
            var t = new TextBlock();
            t.Text = text;
            t.TextWrapping = TextWrapping.Wrap;
            t.LineHeight = 19;
            t.Style = (Style)Application.Current.Resources["Caption"];
            return t;
        }

        /// <summary>A bullet-list item with a dash.</summary>
        public static UIElement Bullet(string text)
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

        /// <summary>Window title and the line under it — the common header of all screens.</summary>
        public static UIElement Head(string title, string hint)
        {
            var head = new StackPanel();
            head.Margin = new Thickness(28, 18, 28, 10);

            var t = new TextBlock();
            t.Text = title;
            t.Style = (Style)Application.Current.Resources["Subtitle"];
            head.Children.Add(t);

            var h = new TextBlock();
            h.Text = hint;
            h.Style = (Style)Application.Current.Resources["Caption"];
            h.Margin = new Thickness(0, 4, 0, 0);
            h.TextWrapping = TextWrapping.Wrap;
            head.Children.Add(h);

            return head;
        }
    }
}
