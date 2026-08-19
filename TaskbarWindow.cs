// DIIIX — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DIIIX
{
    /// <summary>
    /// Экран настроек панели задач: по строке на каждое состояние. Настройки принадлежат
    /// профилю, поэтому «Работа» и «Кино» могут вести себя по-разному.
    /// </summary>
    internal class TaskbarWindow : Window
    {
        private readonly TaskbarSettings _settings;
        private readonly string _profileName;
        private readonly StackPanel _rows = new StackPanel();
        private readonly TextBlock _hint = new TextBlock();
        private bool _updating;

        public TaskbarWindow(string profileName, TaskbarSettings settings)
        {
            _profileName = profileName;
            _settings = settings;

            Title = "Панель задач — " + profileName;
            Width = 880;
            Height = 620;
            MinWidth = 760;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["Bg"];
            UseLayoutRounding = true;

            Content = Build();
            SourceInitialized += delegate { Fluent.ApplyWindowStyling(this); };
            BuildRows();
        }

        private UIElement Build()
        {
            var root = new Grid();
            root.Margin = new Thickness(20, 16, 20, 16);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var title = new TextBlock();
            title.Style = (Style)Application.Current.Resources["Subtitle"];
            title.Text = "Вид панели задач в профиле «" + _profileName + "»";
            title.Margin = new Thickness(2, 0, 0, 6);

            var sub = new TextBlock();
            sub.Style = (Style)Application.Current.Resources["Caption"];
            sub.Text = "Состояния проверяются сверху вниз: побеждает первое включённое и подходящее. " +
                       "«Виден рабочий стол» — запасной вариант, когда не подошло ничто выше.";
            sub.TextWrapping = TextWrapping.Wrap;
            sub.Margin = new Thickness(2, 0, 0, 14);

            var head = new StackPanel();
            head.Children.Add(title);
            head.Children.Add(sub);
            head.Children.Add(BuildTransfer());
            root.Children.Add(head);

            var card = new Border();
            card.Style = (Style)Application.Current.Resources["Card"];
            card.Padding = new Thickness(6, 8, 6, 8);
            var scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.Content = _rows;
            card.Child = scroll;
            Grid.SetRow(card, 1);
            root.Children.Add(card);

            var bottom = new Grid();
            bottom.Margin = new Thickness(0, 14, 0, 0);
            bottom.ColumnDefinitions.Add(new ColumnDefinition());
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(bottom, 2);

            _hint.Style = (Style)Application.Current.Resources["Caption"];
            _hint.VerticalAlignment = VerticalAlignment.Center;
            _hint.TextTrimming = TextTrimming.CharacterEllipsis;
            bottom.Children.Add(_hint);

            var close = new Button();
            close.Style = (Style)Application.Current.Resources["BtnAccent"];
            close.Content = "Готово";
            close.MinWidth = 120;
            close.Height = 36;
            close.Click += delegate { DialogResult = true; };
            Grid.SetColumn(close, 1);
            bottom.Children.Add(close);

            root.Children.Add(bottom);
            return root;
        }

        /// <summary>
        /// Перенос настроек между профилями: настроив вид один раз, незачем повторять
        /// его вручную в каждом профиле.
        /// </summary>
        private UIElement BuildTransfer()
        {
            var row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.Margin = new Thickness(2, 0, 0, 14);

            var label = new TextBlock();
            label.Style = (Style)Application.Current.Resources["Caption"];
            label.Text = "Взять настройки из профиля:";
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(label);

            var others = new List<Layout>();
            foreach (var l in Store.LoadAll())
                if (!string.Equals(l.Name, _profileName, StringComparison.Ordinal)) others.Add(l);

            var picker = new ComboBox();
            picker.Style = (Style)Application.Current.Resources["Combo"];
            picker.Width = 240;
            picker.IsEnabled = others.Count > 0;
            var items = new List<Item>();
            items.Add(new Item(others.Count > 0 ? "— выберите профиль —" : "других профилей нет", null));
            foreach (var l in others) items.Add(new Item(l.Name, l));
            picker.ItemsSource = items;
            picker.SelectedItem = items[0];
            picker.SelectionChanged += delegate
            {
                if (_updating) return;
                var chosen = picker.SelectedItem as Item;
                var source = chosen != null ? chosen.Value as Layout : null;
                if (source == null) return;

                foreach (TaskbarState s in TaskbarSettings.Order)
                    _settings.States[s] = source.Taskbar[s].Copy();

                BuildRows();
                Preview();
                _hint.Text = "Настройки скопированы из «" + source.Name + "».";
                _updating = true;
                picker.SelectedItem = items[0];
                _updating = false;
            };
            row.Children.Add(picker);

            var toAll = new Button();
            toAll.Style = (Style)Application.Current.Resources["Btn"];
            toAll.Content = "Применить ко всем профилям";
            toAll.Height = 34;
            toAll.Margin = new Thickness(10, 0, 0, 0);
            toAll.IsEnabled = others.Count > 0;
            toAll.Click += delegate { CopyToAllProfiles(); };
            row.Children.Add(toAll);

            return row;
        }

        private void CopyToAllProfiles()
        {
            var others = new List<Layout>();
            foreach (var l in Store.LoadAll())
                if (!string.Equals(l.Name, _profileName, StringComparison.Ordinal)) others.Add(l);
            if (others.Count == 0) return;

            var dialog = new ConfirmDialog("Применить ко всем профилям?",
                "Вид панели задач из «" + _profileName + "» заменит настройки в остальных профилях (" +
                others.Count + " шт.). Раскладки мониторов не изменятся.", "Применить");
            dialog.Owner = this;
            if (dialog.ShowDialog() != true) return;

            foreach (var l in others)
            {
                foreach (TaskbarState s in TaskbarSettings.Order)
                    l.Taskbar.States[s] = _settings[s].Copy();
                Store.Save(l);
            }

            // Свой профиль закрепляем тоже: остальные уже записаны на диск, и если этот
            // останется только в памяти, «ко всем» обернётся «ко всем, кроме этого».
            var mine = Store.LoadByName(_profileName);
            if (mine != null)
            {
                foreach (TaskbarState s in TaskbarSettings.Order)
                    mine.Taskbar.States[s] = _settings[s].Copy();
                Store.Save(mine);
            }

            _hint.Text = "Настройки записаны во все профили: " + (others.Count + (mine != null ? 1 : 0)) + ".";
        }

        private void BuildRows()
        {
            _updating = true;
            _rows.Children.Clear();

            foreach (TaskbarState state in TaskbarSettings.Order)
                _rows.Children.Add(BuildRow(state, _settings[state]));

            _updating = false;
            UpdateHint();
        }

        private UIElement BuildRow(TaskbarState state, TaskbarAppearance look)
        {
            var row = new Grid();
            row.Margin = new Thickness(10, 7, 10, 7);
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new ColumnDefinition());

            // включение состояния
            var toggle = new CheckBox();
            toggle.Style = (Style)Application.Current.Resources["Switch"];
            var label = new TextBlock();
            label.Text = TaskbarSettings.Describe(state);
            toggle.Content = label;
            toggle.VerticalAlignment = VerticalAlignment.Center;
            toggle.IsChecked = look.Enabled;
            bool isDesktop = state == TaskbarState.Desktop;
            row.Children.Add(toggle);

            // вид
            var accent = new ComboBox();
            accent.Style = (Style)Application.Current.Resources["Combo"];
            accent.Margin = new Thickness(8, 0, 8, 0);
            var items = new List<Item>();
            foreach (string name in ExplorerTap.Names) items.Add(new Item(ExplorerTap.Describe(name), name));
            accent.ItemsSource = items;
            foreach (Item it in items) if ((string)it.Value == look.Accent) accent.SelectedItem = it;
            if (accent.SelectedItem == null) accent.SelectedItem = items[0];
            Grid.SetColumn(accent, 1);
            row.Children.Add(accent);

            // цвет и прозрачность
            var color = new TextBox();
            color.Style = (Style)Application.Current.Resources["Input"];
            color.Margin = new Thickness(0, 0, 8, 0);
            color.Text = look.Color.ToString("X8", CultureInfo.InvariantCulture);
            color.ToolTip = "Цвет в виде AARRGGBB: первые две цифры — непрозрачность";
            Grid.SetColumn(color, 2);
            row.Children.Add(color);

            // линия у края
            var line = new CheckBox();
            line.Style = (Style)Application.Current.Resources["Switch"];
            var lineLabel = new TextBlock();
            lineLabel.Text = "линия у края";
            line.Content = lineLabel;
            line.VerticalAlignment = VerticalAlignment.Center;
            line.IsChecked = look.ShowLine;
            Grid.SetColumn(line, 3);
            row.Children.Add(line);

            Action refresh = delegate
            {
                if (_updating) return;
                look.Enabled = toggle.IsChecked == true;
                var chosen = accent.SelectedItem as Item;
                if (chosen != null) look.Accent = (string)chosen.Value;
                uint parsed;
                if (ExplorerTap.TryParseColor(color.Text, out parsed)) look.Color = parsed;
                color.Text = look.Color.ToString("X8", CultureInfo.InvariantCulture);   // вернуть непонятое к прежнему
                look.ShowLine = line.IsChecked == true;

                accent.IsEnabled = look.Enabled;
                color.IsEnabled = look.Enabled;
                line.IsEnabled = look.Enabled;
                UpdateHint();
                Preview();
            };

            toggle.Checked += delegate { refresh(); };
            toggle.Unchecked += delegate { refresh(); };
            accent.SelectionChanged += delegate { refresh(); };
            color.LostFocus += delegate { refresh(); };
            line.Checked += delegate { refresh(); };
            line.Unchecked += delegate { refresh(); };

            accent.IsEnabled = look.Enabled;
            color.IsEnabled = look.Enabled;
            line.IsEnabled = look.Enabled;

            var wrap = new Border();
            wrap.CornerRadius = new CornerRadius(6);
            wrap.Padding = new Thickness(4, 2, 4, 2);
            if (isDesktop) wrap.Background = (Brush)Application.Current.Resources["Hover"];
            wrap.Child = row;
            return wrap;
        }

        /// <summary>Показать изменения сразу — настройки без предпросмотра выбирать вслепую.</summary>
        private void Preview()
        {
            if (!ExplorerTap.Available) return;
            TaskbarWatcher.ResetCache();
            TaskbarWatcher.Use(_settings);
        }

        private void UpdateHint()
        {
            if (!ExplorerTap.Available)
            {
                _hint.Text = "Рядом с программой нет DIIIX.ExplorerTAP.dll — вид панели задавать нечем.";
                return;
            }
            if (!_settings.AnyEnabled)
            {
                _hint.Text = "Ни одно состояние не включено — панель останется такой, какой её рисует Windows.";
                return;
            }
            _hint.Text = "Изменения видны сразу и сохраняются вместе с профилем.";
        }
    }
}
