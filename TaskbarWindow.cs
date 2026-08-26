// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DIIISPL
{
    /// <summary>
    /// Вид панели задач в профиле.
    ///
    /// Устроено как «обычный вид плюс исключения»: сначала задаётся, как панель
    /// выглядит всегда, а потом — для каких случаев вид другой. Прежний экран
    /// показывал семь равноправных строк с четырьмя полями в каждой; из двадцати
    /// восьми полей смысл имели два-три, а остальные приходилось глазами
    /// пропускать.
    /// </summary>
    internal class TaskbarWindow : Window
    {
        private readonly TaskbarSettings _settings;
        private readonly string _profileName;
        private readonly StackPanel _content = new StackPanel();
        private readonly TextBlock _hint = new TextBlock();
        private bool _updating;

        /// <summary>Случаи, когда вид отличается от обычного, — в порядке проверки.</summary>
        private static readonly TaskbarState[] Special =
        {
            TaskbarState.BatterySaver, TaskbarState.TaskViewOpened, TaskbarState.SearchOpened,
            TaskbarState.StartOpened, TaskbarState.MaximizedWindow, TaskbarState.VisibleWindow
        };

        public TaskbarWindow(string profileName, TaskbarSettings settings)
        {
            _profileName = profileName;
            _settings = settings;

            Title = Text.TbWinTitle;
            Width = 720;
            Height = 700;
            MinWidth = 620;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["Bg"];
            UseLayoutRounding = true;

            Content = Build();
            SourceInitialized += delegate { Fluent.ApplyWindowStyling(this); };
            Rebuild();
        }

        private UIElement Build()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(Ui.Head(Text.TbWinTitle, Text.TbWinSubtitle(_profileName)));

            var scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Padding = new Thickness(28, 4, 22, 8);
            scroll.Content = _content;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var bottom = new Grid();
            bottom.Margin = new Thickness(28, 10, 28, 18);
            bottom.ColumnDefinitions.Add(new ColumnDefinition());
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(bottom, 2);

            _hint.Style = (Style)Application.Current.Resources["Caption"];
            _hint.VerticalAlignment = VerticalAlignment.Center;
            _hint.TextTrimming = TextTrimming.CharacterEllipsis;
            bottom.Children.Add(_hint);

            // Две кнопки в одной колонке: «Сбросить» слева от «Готово», чтобы
            // главная кнопка осталась там, где её ищут глазами, — у самого края.
            var buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            Grid.SetColumn(buttons, 1);

            var reset = new Button();
            reset.Style = (Style)Application.Current.Resources["Btn"];
            reset.Content = Text.TbWinReset;
            reset.ToolTip = Text.TbWinResetTip;
            reset.MinWidth = 120;
            reset.Height = 36;
            reset.Margin = new Thickness(0, 0, 8, 0);
            reset.Click += delegate { Preview(); };
            buttons.Children.Add(reset);

            var close = new Button();
            close.Style = (Style)Application.Current.Resources["BtnAccent"];
            close.Content = Text.TbWinDone;
            close.MinWidth = 120;
            close.Height = 36;
            close.IsDefault = true;
            close.Click += delegate { DialogResult = true; };
            buttons.Children.Add(close);
            bottom.Children.Add(buttons);

            root.Children.Add(bottom);
            return root;
        }

        // ---------------------------------------------------------------- содержимое
        private void Rebuild()
        {
            _updating = true;
            _content.Children.Clear();

            _content.Children.Add(BaseCard());
            if (_settings.AnyEnabled) _content.Children.Add(SpecialCard());
            _content.Children.Add(TransferCard());

            _updating = false;
            UpdateHint();
        }

        /// <summary>Обычный вид: он же запасной, когда ни один особый случай не подошёл.</summary>
        private UIElement BaseCard()
        {
            TaskbarAppearance look = _settings[TaskbarState.Desktop];

            var master = Switch(_settings.AnyEnabled, delegate(bool on)
            {
                if (on)
                {
                    _settings[TaskbarState.Desktop].Enabled = true;
                }
                else
                {
                    // Выключаем всё разом: иначе «не задавать вид» означало бы, что
                    // особые случаи продолжают срабатывать сами по себе.
                    foreach (TaskbarState s in TaskbarSettings.Order) _settings[s].Enabled = false;
                }
                Rebuild();
                Preview();
            });

            if (!_settings.AnyEnabled)
                return Ui.Card(Text.TbWinCard, null,
                    Ui.Row(Text.TbWinMaster,
                           Text.TbWinMasterOff,
                           master, 60));

            return Ui.Card(Text.TbWinCard, null,
                Ui.Row(Text.TbWinMaster, null, master, 60),
                Ui.Row(Text.TbWinUsual, Text.TbWinUsualHint,
                       AccentBox(look, false), 190),
                ColorRow(look),
                Ui.Row(Text.TbWinLine, Text.TbWinLineHint,
                       Switch(look.ShowLine, delegate(bool on)
                       {
                           foreach (TaskbarState s in TaskbarSettings.Order) _settings[s].ShowLine = on;
                           Preview();
                       }), 60));
        }

        /// <summary>Случаи, когда вид отличается от обычного.</summary>
        private UIElement SpecialCard()
        {
            var rows = new List<UIElement>();
            foreach (TaskbarState state in Special)
            {
                TaskbarAppearance look = _settings[state];
                rows.Add(Ui.Row(TaskbarSettings.Describe(state), HintFor(state),
                                StateControls(look), 330));
            }

            return Ui.Card(Text.TbWinCasesCard,
                Text.TbWinCasesHint,
                rows.ToArray());
        }

        /// <summary>Выбор вида для особого случая плюс цвет, если он что-то значит.</summary>
        private UIElement StateControls(TaskbarAppearance look)
        {
            var panel = new Grid();
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            panel.ColumnDefinitions.Add(new ColumnDefinition());

            ComboBox accent = AccentBox(look, true);
            panel.Children.Add(accent);

            UIElement color = ColorField(look);
            color.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
            color.Visibility = look.Enabled && UsesColor(look.Accent) ? Visibility.Visible : Visibility.Hidden;
            Grid.SetColumn(color, 1);
            panel.Children.Add(color);

            return panel;
        }

        /// <summary>Цвет обычного вида отдельной строкой — там для него есть место.</summary>
        private UIElement ColorRow(TaskbarAppearance look)
        {
            if (!UsesColor(look.Accent)) return null;

            string hint = look.Accent == "opaque"
                ? Text.TbWinColorHint(true)
                : Text.TbWinColorHint(false);

            return Ui.Row(Text.TbWinColor, hint, ColorField(look), 190);
        }

        private static bool UsesColor(string accent)
        {
            return accent == "opaque" || accent == "acrylic" || accent == "blur";
        }

        /// <summary>
        /// Пояснение только там, где название не говорит всего. Остальным случаям
        /// подписи не нужны: «Открыт поиск» и без пояснения «открыт поиск» понятно.
        /// </summary>
        private static string HintFor(TaskbarState state)
        {
            if (state == TaskbarState.VisibleWindow)
                return Text.TbWinPerScreen;
            return null;
        }

        // ---------------------------------------------------------------- элементы
        /// <summary>Выбор вида. Для особых случаев первый пункт — «как обычно».</summary>
        private ComboBox AccentBox(TaskbarAppearance look, bool allowInherit)
        {
            var box = new ComboBox();
            box.Style = (Style)Application.Current.Resources["Combo"];

            var items = new List<Item>();
            if (allowInherit) items.Add(new Item(Text.TbWinAsUsual, null));
            foreach (string name in ExplorerTap.Names) items.Add(new Item(ExplorerTap.Describe(name), name));
            box.ItemsSource = items;

            string current = (allowInherit && !look.Enabled) ? null : look.Accent;
            foreach (Item it in items) if ((string)it.Value == current) box.SelectedItem = it;
            if (box.SelectedItem == null) box.SelectedItem = items[0];

            box.SelectionChanged += delegate
            {
                if (_updating) return;
                var chosen = box.SelectedItem as Item;
                string value = chosen != null ? (string)chosen.Value : null;

                if (value == null) look.Enabled = false;
                else { look.Enabled = true; look.Accent = value; }

                Rebuild();      // от вида зависит, нужен ли цвет
                Preview();
            };
            return box;
        }

        /// <summary>Цвет: образец рядом с полем, чтобы не разбирать цифры в уме.</summary>
        private UIElement ColorField(TaskbarAppearance look)
        {
            var panel = new StackPanel();
            panel.Orientation = Orientation.Horizontal;

            var swatch = new Border();
            swatch.Width = 24;
            swatch.Height = 24;
            swatch.CornerRadius = new CornerRadius(4);
            swatch.BorderThickness = new Thickness(1);
            swatch.BorderBrush = (Brush)Application.Current.Resources["StrokeStrong"];
            swatch.VerticalAlignment = VerticalAlignment.Center;
            swatch.Margin = new Thickness(0, 0, 8, 0);
            swatch.Background = Paint(look.Color);
            panel.Children.Add(swatch);

            var box = new TextBox();
            box.Style = (Style)Application.Current.Resources["Input"];
            box.Width = 110;
            box.Text = look.Color.ToString("X8", CultureInfo.InvariantCulture);
            box.ToolTip = Text.TbWinColorTip;
            box.LostFocus += delegate
            {
                if (_updating) return;
                uint parsed;
                if (ExplorerTap.TryParseColor(box.Text, out parsed)) look.Color = parsed;
                box.Text = look.Color.ToString("X8", CultureInfo.InvariantCulture);
                swatch.Background = Paint(look.Color);
                Preview();
            };
            panel.Children.Add(box);

            return panel;
        }

        /// <summary>Образец цвета: полупрозрачный показываем поверх клетчатой подложки.</summary>
        private static Brush Paint(uint argb)
        {
            var color = Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16),
                                       (byte)(argb >> 8), (byte)argb);
            if (color.A == 255) return new SolidColorBrush(color);

            var stack = new DrawingGroup();
            var light = new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB)), null,
                                            new RectangleGeometry(new Rect(0, 0, 8, 8)));
            var dark = new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)), null,
                                           new GeometryGroup());
            var group = (GeometryGroup)dark.Geometry;
            group.Children.Add(new RectangleGeometry(new Rect(0, 0, 4, 4)));
            group.Children.Add(new RectangleGeometry(new Rect(4, 4, 4, 4)));
            stack.Children.Add(light);
            stack.Children.Add(dark);
            stack.Children.Add(new GeometryDrawing(new SolidColorBrush(color), null,
                                                   new RectangleGeometry(new Rect(0, 0, 8, 8))));

            var brush = new DrawingBrush(stack);
            brush.TileMode = TileMode.Tile;
            brush.Viewport = new Rect(0, 0, 8, 8);
            brush.ViewportUnits = BrushMappingMode.Absolute;
            brush.Stretch = Stretch.None;
            return brush;
        }

        private CheckBox Switch(bool value, Action<bool> onSet)
        {
            var box = new CheckBox();
            box.Style = (Style)Application.Current.Resources["Switch"];
            box.IsChecked = value;
            box.HorizontalAlignment = HorizontalAlignment.Right;
            box.VerticalAlignment = VerticalAlignment.Center;
            box.Checked += delegate { if (!_updating) onSet(true); };
            box.Unchecked += delegate { if (!_updating) onSet(false); };
            return box;
        }

        // ---------------------------------------------------------------- перенос
        private UIElement TransferCard()
        {
            var others = new List<Layout>();
            foreach (var l in Store.LoadAll())
                if (!string.Equals(l.Name, _profileName, StringComparison.Ordinal)) others.Add(l);

            var row = new StackPanel();
            row.Orientation = Orientation.Horizontal;

            var picker = new ComboBox();
            picker.Style = (Style)Application.Current.Resources["Combo"];
            picker.Width = 220;
            picker.IsEnabled = others.Count > 0;

            var items = new List<Item>();
            items.Add(new Item(others.Count > 0 ? Text.TbWinPickProfile : Text.TbWinNoOthers, null));
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

                Rebuild();
                Preview();
                _hint.Text = Text.TbWinCopied(source.Name);
            };
            row.Children.Add(picker);

            var toAll = new Button();
            toAll.Style = (Style)Application.Current.Resources["Btn"];
            toAll.Content = Text.TbWinApplyAll;
            toAll.Height = 34;
            toAll.Margin = new Thickness(8, 0, 0, 0);
            toAll.IsEnabled = others.Count > 0;
            toAll.Click += delegate { CopyToAllProfiles(); };
            row.Children.Add(toAll);

            return Ui.Card(Text.TbWinTransferCard, Text.TbWinTransferHint, row);
        }

        private void CopyToAllProfiles()
        {
            var others = new List<Layout>();
            foreach (var l in Store.LoadAll())
                if (!string.Equals(l.Name, _profileName, StringComparison.Ordinal)) others.Add(l);
            if (others.Count == 0) return;

            var dialog = new ConfirmDialog(Text.TbWinApplyAllTitle,
                Text.TbWinApplyAllBody(_profileName, others.Count), Text.Apply);
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

            _hint.Text = Text.TbWinWrittenAll(others.Count + (mine != null ? 1 : 0));
        }

        // ---------------------------------------------------------------- прочее
        /// <summary>Показать изменения сразу — настройки без предпросмотра выбирать вслепую.</summary>
        private void Preview()
        {
            if (!ExplorerTap.Available) return;
            TaskbarWatcher.ResetCache();
            TaskbarWatcher.Use(_settings);
            UpdateHint();
        }

        private void UpdateHint()
        {
            if (!ExplorerTap.Available)
            {
                _hint.Text = Text.TbWinNoLibrary;
                return;
            }
            _hint.Text = _settings.AnyEnabled
                ? Text.TbWinLiveHint
                : Text.TbWinOffHint;
        }
    }
}
