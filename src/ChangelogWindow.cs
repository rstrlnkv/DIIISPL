// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DIIISPL
{
    /// <summary>
    /// What changed from version to version — the whole history, kept inside the program.
    ///
    /// Before, the 'What's new' button led to the browser, to the releases page. That
    /// worked right up to the case the button exists for: a copy that is newer than
    /// the latest release — or built on one's own machine — had nothing to read
    /// there. Now there is nothing to read only if the changelog did not make it
    /// into the build.
    ///
    /// Layout: version number and date on one line, the changes below, each with its own
    /// badge on the left. The badge sits in a separate column, not glued to the text:
    /// so the kind of change is seen at a glance, before reading, and the eye finds
    /// what it needs while running down the list.
    /// </summary>
    internal class ChangelogWindow : Window
    {
        public ChangelogWindow()
        {
            Title = Text.WhatsNew;
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

            List<ChangelogEntry> entries = Changelog.Read();
            if (entries.Count == 0)
            {
                stack.Children.Add(Ui.Note(Text.ChangelogEmpty));
            }
            else
            {
                Version mine = Updater.Current;
                bool first = true;

                foreach (ChangelogEntry e in entries)
                {
                    Version v = Updater.ParseVersion(e.Version);
                    bool yours = v != null && v.Major == mine.Major
                                           && v.Minor == mine.Minor
                                           && v.Build == mine.Build;

                    stack.Children.Add(Head(e, yours, first));
                    first = false;

                    if (e.Note.Length > 0)
                    {
                        TextBlock note = Ui.Note(e.Note);
                        note.Margin = new Thickness(0, 12, 0, 0);
                        stack.Children.Add(note);
                    }

                    foreach (ChangelogLine line in e.Lines) stack.Children.Add(Row(line));
                }
            }

            var scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Content = stack;
            return scroll;
        }

        /// <summary>Version number and date on one line: number large, date beside it and quieter.</summary>
        private static UIElement Head(ChangelogEntry entry, bool yours, bool first)
        {
            var row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.Margin = new Thickness(0, first ? 0 : 30, 0, 4);

            var number = new TextBlock();
            number.Text = entry.Version;
            number.FontSize = 19;
            number.FontWeight = FontWeights.SemiBold;
            number.Foreground = (Brush)Application.Current.Resources["Text"];
            row.Children.Add(number);

            string aside = Pretty(entry.Date);
            if (yours) aside = aside.Length > 0 ? aside + " · " + Text.ChangelogYours : Text.ChangelogYours;

            if (aside.Length > 0)
            {
                var when = new TextBlock();
                when.Text = aside;
                when.FontSize = 13;
                when.Margin = new Thickness(10, 0, 0, 2);
                when.VerticalAlignment = VerticalAlignment.Bottom;
                when.Foreground = (Brush)Application.Current.Resources["TextTer"];
                row.Children.Add(when);
            }
            return row;
        }

        /// <summary>One change: badge in its own column, text wraps to the right.</summary>
        private static UIElement Row(ChangelogLine line)
        {
            var grid = new Grid();
            grid.Margin = new Thickness(0, 12, 0, 0);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            if (line.Badge.Length > 0)
            {
                UIElement badge = Badge(line.Badge);
                Grid.SetColumn(badge, 0);
                grid.Children.Add(badge);
            }

            var text = new TextBlock();
            text.Text = line.Text;
            text.TextWrapping = TextWrapping.Wrap;
            text.LineHeight = 21;
            text.FontSize = 13;
            text.Foreground = (Brush)Application.Current.Resources["TextSec"];
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            return grid;
        }

        /// <summary>
        /// Badge: a chip of its own colour with the label on it. The colours live in the
        /// theme palette, not here: the light theme needs other tones for the same six
        /// kinds (DEL and DEP use the Upd colours, SEC the Fix colours), or the label is unreadable.
        /// </summary>
        private static UIElement Badge(string name)
        {
            string kind = name.Trim().ToUpperInvariant();
            string bg, fg;
            switch (kind)
            {
                case "NEW": bg = "BadgeNewBg"; fg = "BadgeNewText"; break;
                case "FIX":
                case "SEC": bg = "BadgeFixBg"; fg = "BadgeFixText"; break;   // DEL and DEP fall to Upd colours
                default:    bg = "BadgeUpdBg"; fg = "BadgeUpdText"; break;
            }

            var label = new TextBlock();
            label.Text = kind;
            label.FontSize = 10;
            label.FontWeight = FontWeights.Bold;
            label.Foreground = (Brush)Application.Current.Resources[fg];
            label.HorizontalAlignment = HorizontalAlignment.Center;

            var chip = new Border();
            chip.Background = (Brush)Application.Current.Resources[bg];
            chip.CornerRadius = new CornerRadius(4);
            chip.Padding = new Thickness(8, 3, 8, 4);
            // One width for every badge: "FIX" is shorter than "NEW", and without it the
            // badge column wavers while the eye looks for an even edge.
            chip.MinWidth = 44;
            chip.HorizontalAlignment = HorizontalAlignment.Left;
            chip.VerticalAlignment = VerticalAlignment.Top;
            chip.Margin = new Thickness(0, 1, 10, 0);
            chip.Child = label;
            return chip;
        }

        /// <summary>
        /// The date in the file is written as '2026-08-27' — no reader in any country can
        /// misread it. In the window we show it in words, as the window's language does it;
        /// if it does not parse, we leave it as is.
        /// </summary>
        private static string Pretty(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "";
            DateTime parsed;
            if (DateTime.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out parsed))
                return parsed.ToString("D", CultureInfo.CurrentCulture);
            return iso;
        }
    }
}
