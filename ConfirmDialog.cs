// DIIIX — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DIIIX
{
    /// <summary>Диалог в стиле Fluent: карточка со скруглением и тенью, без системной рамки.</summary>
    internal class ConfirmDialog : Window
    {
        public ConfirmDialog(string title, string message, string okText)
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
            cancel.Content = "Отмена";
            cancel.MinWidth = 110;
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.IsCancel = true;
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

            MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };
            Loaded += delegate { ok.Focus(); };
        }
    }
}
