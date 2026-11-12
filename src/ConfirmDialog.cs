// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DIIISPL
{
    /// <summary>Fluent-style dialog: a card with rounded corners and a shadow, no system frame.</summary>
    internal class ConfirmDialog : Window
    {
        public ConfirmDialog(string title, string message, string okText)
            : this(title, message, okText, Text.Cancel)
        {
        }

        /// <summary>
        /// The second button gets its own name where 'Cancel' lies. In the save dialog
        /// it did not cancel closing the window, as it reads, but silently
        /// discarded the edits: the user pressed 'Cancel' so that nothing would happen,
        /// and lost their work.
        /// </summary>
        public ConfirmDialog(string title, string message, string okText, string cancelText)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            SizeToContent = SizeToContent.Height;
            Width = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var card = new Border();
            card.Background = (Brush)Application.Current.Resources["Flyout"];
            card.BorderBrush = (Brush)Application.Current.Resources["Stroke"];
            card.BorderThickness = new Thickness(1);
            card.CornerRadius = new CornerRadius(10);
            card.Margin = new Thickness(20);
            card.Padding = new Thickness(24, 22, 24, 20);
            card.Effect = new DropShadowEffect
            {
                BlurRadius = 28,
                ShadowDepth = 6,
                Opacity = 0.35,
                Color = Colors.Black
            };

            var stack = new StackPanel();

            var titleBlock = new TextBlock();
            titleBlock.Text = title;
            titleBlock.Style = (Style)Application.Current.Resources["Subtitle"];
            titleBlock.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(titleBlock);

            var messageBlock = new TextBlock();
            messageBlock.Text = message;
            messageBlock.Foreground = (Brush)Application.Current.Resources["TextSec"];
            messageBlock.TextWrapping = TextWrapping.Wrap;
            messageBlock.Margin = new Thickness(0, 10, 0, 22);
            stack.Children.Add(messageBlock);

            var buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;

            var cancel = new Button();
            cancel.Style = (Style)Application.Current.Resources["Btn"];
            cancel.Content = cancelText;
            cancel.MinWidth = 110;
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += delegate { DialogResult = false; };
            buttons.Children.Add(cancel);

            var ok = new Button();
            ok.Style = (Style)Application.Current.Resources["BtnAccent"];
            ok.Content = okText;
            ok.MinWidth = 110;
            ok.IsDefault = true;
            ok.Click += delegate { DialogResult = true; };
            buttons.Children.Add(ok);

            stack.Children.Add(buttons);
            card.Child = stack;
            Content = card;

            // Esc is not the second button but 'I changed my mind about asking'. It used to be
            // on it (IsCancel), and in the save dialog pressing Esc discarded the
            // edits — exactly what it was meant to avoid. Now the
            // window closes without giving an answer: ShowDialog returns null, and
            // the asker decides. For 'Delete?' it is still a refusal.
            PreviewKeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key != Key.Escape) return;
                e.Handled = true;
                Close();
            };

            MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };
            Loaded += delegate { ok.Focus(); };
        }
    }
}
