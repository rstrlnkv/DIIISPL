// DIIIX — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace DIIIX
{
    internal class Item
    {
        public string Text;
        public object Value;
        public Item(string text, object value) { Text = text; Value = value; }
        public override string ToString() { return Text; }
    }

    internal class MainWindow : Window
    {
        public event Action ProfilesChanged;

        private readonly ListBox _profileList = new ListBox();
        private readonly TextBlock _nameText = new TextBlock();
        private readonly TextBlock _nameHint = new TextBlock();
        private readonly TextBox _nameBox = new TextBox();
        private readonly Button _hotkeyButton = new Button();
        private readonly Button _taskbarButton = new Button();
        private readonly Button _applyButton = new Button();
        private readonly Button _saveButton = new Button();
        private readonly MonitorCanvas _canvas = new MonitorCanvas();
        private readonly TextBlock _status = new TextBlock();

        private readonly CheckBox _enabledSwitch = new CheckBox();
        private readonly CheckBox _primarySwitch = new CheckBox();
        private readonly ComboBox _resCombo = new ComboBox();
        private readonly ComboBox _hzCombo = new ComboBox();
        private readonly ComboBox _rotCombo = new ComboBox();
        private readonly ComboBox _cloneCombo = new ComboBox();
        private readonly ComboBox _hdrCombo = new ComboBox();
        private readonly TextBlock _monitorTitle = new TextBlock();
        private readonly TextBlock _monitorSubtitle = new TextBlock();
        private readonly Border _propsPanel = new Border();

        private readonly WrapPanel _fieldsRow = new WrapPanel();
        private readonly TextBlock _offNote = new TextBlock();
        private UIElement _fieldRes, _fieldHz, _fieldRot, _fieldClone, _fieldHdr;

        private List<Layout> _profiles = new List<Layout>();
        private Layout _current;
        private string _currentOriginalName;
        private Dictionary<string, MonitorInfo> _live = new Dictionary<string, MonitorInfo>();
        private bool _updating;
        private bool _dirty;
        private bool _capturingHotkey;
        private bool _renaming;
        private string _activeProfileName;

        public MainWindow()
        {
            Title = "DIIIX";
            Width = 1180;
            Height = 760;
            MinWidth = 960;
            MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = (Brush)Application.Current.Resources["Bg"];
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);

            Content = BuildUi();

            SourceInitialized += delegate { Fluent.ApplyWindowStyling(this); };
            PreviewKeyDown += OnPreviewKeyDown;
            Closing += delegate(object s, System.ComponentModel.CancelEventArgs e)
            {
                if (_dirty) AskToSave();
                e.Cancel = true;
                Hide();
            };

            ReloadLive();
            ReloadProfiles(null);
        }

        // ---------------------------------------------------------------- UI
        private UIElement BuildUi()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(248) });
            body.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetRow(body, 0);
            root.Children.Add(body);

            body.Children.Add(BuildSidebar());

            var main = BuildMain();
            Grid.SetColumn(main, 1);
            body.Children.Add(main);

            var statusBar = new Border();
            statusBar.Padding = new Thickness(20, 8, 20, 12);
            _status.Style = (Style)Application.Current.Resources["Caption"];
            _status.TextTrimming = TextTrimming.CharacterEllipsis;
            statusBar.Child = _status;
            Grid.SetRow(statusBar, 1);
            root.Children.Add(statusBar);

            return root;
        }

        private UIElement BuildSidebar()
        {
            var panel = new Grid();
            panel.Margin = new Thickness(12, 8, 6, 12);
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition());
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Название программы над списком — как в окне MagicKeys: сначала кто это,
            // потом уже содержимое.
            var top = new StackPanel();
            top.Margin = new Thickness(10, 6, 10, 4);

            var brandName = new TextBlock();
            brandName.Text = App.AppTitle;
            brandName.Style = (Style)Application.Current.Resources["Subtitle"];
            top.Children.Add(brandName);

            var brandSub = new TextBlock();
            brandSub.Text = "Раскладки мониторов";
            brandSub.Style = (Style)Application.Current.Resources["Caption"];
            brandSub.Margin = new Thickness(0, 2, 0, 14);
            top.Children.Add(brandSub);

            var header = new TextBlock();
            header.Text = "ПРОФИЛИ";
            header.Style = (Style)Application.Current.Resources["SectionHeader"];
            header.Margin = new Thickness(0, 0, 0, 8);
            top.Children.Add(header);

            panel.Children.Add(top);

            _profileList.Background = Brushes.Transparent;
            _profileList.BorderThickness = new Thickness(0);
            _profileList.ItemContainerStyle = (Style)Application.Current.Resources["ProfileItem"];
            _profileList.SelectionChanged += delegate
            {
                if (_updating) return;
                var item = _profileList.SelectedItem as ListBoxItem;
                if (item == null) return;
                var layout = item.Tag as Layout;
                if (layout != null && !ReferenceEquals(layout, _current)) SelectProfile(layout);
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(_profileList, ScrollBarVisibility.Disabled);
            Grid.SetRow(_profileList, 1);
            panel.Children.Add(_profileList);

            var buttons = new StackPanel();
            buttons.Margin = new Thickness(0, 10, 0, 8);
            Grid.SetRow(buttons, 2);

            var newButton = new Button();
            newButton.Style = (Style)Application.Current.Resources["Btn"];
            newButton.Content = Glyph("", "Новый профиль");
            newButton.HorizontalContentAlignment = HorizontalAlignment.Left;
            newButton.ToolTip = "Создать профиль из текущей раскладки мониторов";
            newButton.Click += delegate { NewFromCurrent(); };

            // Действия над самим профилем — переименовать, переставить, удалить — собраны
            // в одном меню. Раньше «Удалить» стояло отдельной кнопкой в общем ряду: самое
            // опасное действие оказывалось самым доступным, да ещё рядом со справкой.
            var moreButton = new Button();
            moreButton.Style = (Style)Application.Current.Resources["Btn"];
            moreButton.Content = "···";
            moreButton.Width = 38;
            moreButton.Margin = new Thickness(6, 0, 0, 0);
            moreButton.ToolTip = "Переименовать, переставить, удалить";
            moreButton.Click += delegate
            {
                if (_current == null) return;
                var menu = BuildProfileMenu(_current);
                menu.PlacementTarget = moreButton;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                menu.IsOpen = true;
            };

            var newRow = new Grid();
            newRow.ColumnDefinitions.Add(new ColumnDefinition());
            newRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(moreButton, 1);
            newRow.Children.Add(newButton);
            newRow.Children.Add(moreButton);
            buttons.Children.Add(newRow);

            var aboutButton = new Button();
            aboutButton.Style = (Style)Application.Current.Resources["BtnSubtle"];
            aboutButton.Content = Glyph("", "О программе");
            aboutButton.HorizontalContentAlignment = HorizontalAlignment.Left;
            aboutButton.Margin = new Thickness(0, 4, 0, 0);
            aboutButton.Click += delegate { ShowAbout(); };
            buttons.Children.Add(aboutButton);

            panel.Children.Add(buttons);
            return panel;
        }

        private static UIElement Glyph(string glyph, string text)
        {
            var sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            var icon = new TextBlock();
            icon.Text = glyph;
            icon.FontFamily = (FontFamily)Application.Current.Resources["IconFont"];
            icon.FontSize = 14;
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.Margin = new Thickness(0, 0, 10, 0);
            sp.Children.Add(icon);
            var t = new TextBlock();
            t.Text = text;
            t.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(t);
            return sp;
        }

        private UIElement BuildMain()
        {
            var grid = new Grid();
            grid.Margin = new Thickness(8, 12, 20, 4);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // --- шапка ---
            var header = new Grid();
            header.Margin = new Thickness(4, 0, 0, 14);
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Название по умолчанию — обычный заголовок. Правка включается двойным щелчком,
            // клавишей F2 или пунктом «Переименовать» в списке слева.
            var nameStack = new Grid();
            nameStack.HorizontalAlignment = HorizontalAlignment.Left;

            _nameText.Style = (Style)Application.Current.Resources["Subtitle"];
            _nameText.FontSize = 24;
            _nameText.VerticalAlignment = VerticalAlignment.Center;
            _nameText.Margin = new Thickness(2, 0, 0, 0);
            _nameText.MinWidth = 240;
            _nameText.TextTrimming = TextTrimming.CharacterEllipsis;
            _nameText.Cursor = Cursors.IBeam;
            _nameText.ToolTip = "Двойной щелчок или F2 — переименовать";
            _nameText.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2) BeginRename();
            };
            nameStack.Children.Add(_nameText);

            _nameBox.Style = (Style)Application.Current.Resources["Input"];
            _nameBox.FontSize = 20;
            _nameBox.FontWeight = FontWeights.SemiBold;
            _nameBox.Height = 42;
            _nameBox.MinWidth = 320;
            _nameBox.Visibility = Visibility.Collapsed;
            _nameBox.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { CommitRename(true); e.Handled = true; }
                else if (e.Key == Key.Escape) { CommitRename(false); e.Handled = true; }
            };
            _nameBox.LostFocus += delegate { if (_renaming) CommitRename(true); };
            nameStack.Children.Add(_nameBox);

            // Под названием — строка о том, что в профиле: сколько мониторов и какая
            // клавиша. Так заголовок отвечает не только «что это», но и «что внутри».
            var titleBlock = new StackPanel();
            titleBlock.VerticalAlignment = VerticalAlignment.Center;
            titleBlock.Children.Add(nameStack);
            _nameHint.Style = (Style)Application.Current.Resources["Caption"];
            _nameHint.Margin = new Thickness(4, 3, 0, 0);
            _nameHint.TextTrimming = TextTrimming.CharacterEllipsis;
            titleBlock.Children.Add(_nameHint);

            header.Children.Add(titleBlock);

            var actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            Grid.SetColumn(actions, 1);

            // вид панели задач — свойство профиля, а не монитора, поэтому здесь
            _taskbarButton.Style = (Style)Application.Current.Resources["Btn"];
            _taskbarButton.Height = 42;

            _taskbarButton.Margin = new Thickness(0, 0, 8, 0);
            _taskbarButton.Click += delegate { OpenTaskbarSettings(); };
            actions.Children.Add(_taskbarButton);

            _hotkeyButton.Style = (Style)Application.Current.Resources["Btn"];
            _hotkeyButton.Height = 42;
            _hotkeyButton.Margin = new Thickness(0, 0, 8, 0);
            _hotkeyButton.Click += delegate { BeginHotkeyCapture(); };
            actions.Children.Add(_hotkeyButton);

            _saveButton.Style = (Style)Application.Current.Resources["Btn"];
            _saveButton.Height = 42;
            _saveButton.Content = Glyph("", "Сохранить");
            _saveButton.Margin = new Thickness(0, 0, 8, 0);
            _saveButton.Click += delegate { SaveCurrent(true); };
            actions.Children.Add(_saveButton);

            _applyButton.Style = (Style)Application.Current.Resources["BtnAccent"];
            _applyButton.Height = 42;
            _applyButton.Content = Glyph("", "Применить");
            _applyButton.Click += delegate { ApplyCurrent(); };
            actions.Children.Add(_applyButton);

            header.Children.Add(actions);
            grid.Children.Add(header);

            // --- холст ---
            var canvasCard = new Border();
            canvasCard.Style = (Style)Application.Current.Resources["Card"];
            canvasCard.Margin = new Thickness(4, 0, 0, 14);
            canvasCard.Child = _canvas;
            _canvas.SelectionChanged += delegate { UpdateProperties(); };
            _canvas.LayoutChanged += delegate { MarkDirty(); UpdateStatus(null); };
            _canvas.ToggleRequested += delegate
            {
                var d = _canvas.Selected;
                if (d != null) OnEnabledChanged(!d.Enabled);
            };
            Grid.SetRow(canvasCard, 1);
            grid.Children.Add(canvasCard);

            // --- свойства выбранного монитора ---
            _propsPanel.Style = (Style)Application.Current.Resources["Card"];
            _propsPanel.Margin = new Thickness(4, 0, 0, 8);
            _propsPanel.Padding = new Thickness(18, 14, 18, 16);
            _propsPanel.Child = BuildProperties();
            Grid.SetRow(_propsPanel, 2);
            grid.Children.Add(_propsPanel);

            return grid;
        }

        /// <summary>
        /// Свойства выбранного монитора.
        ///
        /// Поля не прибиты к сетке, а собираются под конкретный монитор: у выключенного
        /// настраивать нечего, у выхода в дублировании HDR невозможен, а частоту
        /// незачем спрашивать, если она у разрешения одна. Показывать всё всегда —
        /// значит заставлять глазами отсеивать неприменимое.
        /// </summary>
        private UIElement BuildProperties()
        {
            var outer = new StackPanel();

            var titleRow = new StackPanel();
            titleRow.Orientation = Orientation.Horizontal;
            titleRow.Margin = new Thickness(0, 0, 0, 12);
            _monitorTitle.Style = (Style)Application.Current.Resources["Subtitle"];
            _monitorTitle.FontSize = 16;
            _monitorTitle.VerticalAlignment = VerticalAlignment.Center;
            titleRow.Children.Add(_monitorTitle);
            _monitorSubtitle.Style = (Style)Application.Current.Resources["Caption"];
            _monitorSubtitle.Margin = new Thickness(12, 0, 0, 0);
            _monitorSubtitle.VerticalAlignment = VerticalAlignment.Center;
            titleRow.Children.Add(_monitorSubtitle);
            outer.Children.Add(titleRow);

            // Переключатели держатся вместе слева, поля — справа от них.
            var switches = new StackPanel();
            switches.Margin = new Thickness(0, 0, 18, 0);
            switches.VerticalAlignment = VerticalAlignment.Center;
            switches.Children.Add(WrapSwitch(_enabledSwitch, "Включён"));
            _primarySwitch.ToolTip = "На основном мониторе живут панель задач и новые окна";
            switches.Children.Add(WrapSwitch(_primarySwitch, "Основной"));

            _fieldRes = Field("Разрешение", _resCombo, 150);
            _fieldHz = Field("Частота", _hzCombo, 100);
            _fieldRot = Field("Поворот", _rotCombo, 120);
            _fieldClone = Field("Дублирование", _cloneCombo, 170);
            _fieldHdr = Field("HDR", _hdrCombo, 120);

            _offNote.Style = (Style)Application.Current.Resources["Caption"];
            _offNote.Text = "Включите монитор, чтобы настроить его в этом профиле.";
            _offNote.VerticalAlignment = VerticalAlignment.Center;

            _fieldsRow.Orientation = Orientation.Horizontal;
            outer.Children.Add(_fieldsRow);
            _fieldsRow.Children.Add(switches);

            _enabledSwitch.Checked += delegate { OnEnabledChanged(true); };
            _enabledSwitch.Unchecked += delegate { OnEnabledChanged(false); };
            _primarySwitch.Checked += delegate { OnPrimaryChanged(true); };
            _primarySwitch.Unchecked += delegate { OnPrimaryChanged(false); };
            _resCombo.SelectionChanged += delegate { OnResChanged(); };
            _hzCombo.SelectionChanged += delegate { OnHzChanged(); };
            _rotCombo.SelectionChanged += delegate { OnRotChanged(); };
            _cloneCombo.SelectionChanged += delegate { OnCloneChanged(); };
            _hdrCombo.SelectionChanged += delegate { OnHdrChanged(); };

            foreach (var c in new[] { _resCombo, _hzCombo, _rotCombo, _cloneCombo, _hdrCombo })
                c.Style = (Style)Application.Current.Resources["Combo"];

            return outer;
        }

        private UIElement WrapSwitch(CheckBox box, string text)
        {
            box.Style = (Style)Application.Current.Resources["Switch"];
            var t = new TextBlock();
            t.Text = text;
            box.Content = t;
            box.VerticalAlignment = VerticalAlignment.Center;
            box.Height = 34;
            return box;
        }

        private UIElement Field(string label, UIElement control, double width)
        {
            var sp = new StackPanel();
            sp.Width = width;
            sp.Margin = new Thickness(0, 0, 8, 0);
            var l = new TextBlock();
            l.Text = label;
            l.Style = (Style)Application.Current.Resources["Caption"];
            l.Margin = new Thickness(2, 0, 0, 6);
            sp.Children.Add(l);
            sp.Children.Add(control);
            return sp;
        }

        /// <summary>Оставить в строке свойств только то, что к этому монитору применимо.</summary>
        private void ShowFields(params UIElement[] fields)
        {
            while (_fieldsRow.Children.Count > 1) _fieldsRow.Children.RemoveAt(1);
            foreach (UIElement f in fields) if (f != null) _fieldsRow.Children.Add(f);
        }

        // ---------------------------------------------------------------- данные
        public void ReloadLive()
        {
            _live.Clear();
            foreach (var m in Monitors.Enumerate()) _live[m.Identity] = m;
            _canvas.Live = _live;
        }

        public void ReloadProfiles(string selectName)
        {
            _profiles = Store.LoadAll();
            RefreshProfileList();

            Layout target = null;
            if (selectName != null)
                foreach (var l in _profiles) if (l.Name == selectName) { target = l; break; }
            if (target == null && _profiles.Count > 0) target = _profiles[0];

            if (target != null) SelectProfile(target);
            else
            {
                _current = null;
                _canvas.Layout = null;
                _canvas.Rebuild();
                UpdateHeader();
                UpdateProperties();
                UpdateStatus("Профилей пока нет — нажмите «Новый из текущей раскладки».");
            }
        }

        private void RefreshProfileList()
        {
            _updating = true;
            _profileList.Items.Clear();
            var live = new List<MonitorInfo>(_live.Values);
            _activeProfileName = null;

            foreach (var l in _profiles)
            {
                bool isActive = l.MatchesCurrent(live);
                if (isActive) _activeProfileName = l.Name;

                var item = new ListBoxItem();
                item.Style = (Style)Application.Current.Resources["ProfileItem"];
                item.Tag = l;

                var sp = new StackPanel();

                var titleRow = new StackPanel();
                titleRow.Orientation = Orientation.Horizontal;

                var name = new TextBlock();
                name.Text = l.Name;
                name.FontWeight = FontWeights.SemiBold;
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                titleRow.Children.Add(name);

                // действующая раскладка помечена точкой акцентного цвета справа от названия
                var dot = new System.Windows.Shapes.Ellipse();
                dot.Width = 7; dot.Height = 7;
                dot.Margin = new Thickness(8, 1, 0, 0);
                dot.VerticalAlignment = VerticalAlignment.Center;
                dot.Fill = (Brush)Application.Current.Resources["Accent"];
                dot.ToolTip = "Эта раскладка применена сейчас";
                dot.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
                titleRow.Children.Add(dot);

                sp.Children.Add(titleRow);

                var sub = new TextBlock();
                sub.Style = (Style)Application.Current.Resources["Caption"];
                sub.Margin = new Thickness(0, 2, 0, 0);
                int on = 0;
                foreach (var d in l.Displays) if (d.Enabled) on++;
                sub.Text = string.IsNullOrEmpty(l.Hotkey)
                    ? ("мониторов: " + on)
                    : (l.Hotkey + "   ·   мониторов: " + on);
                sp.Children.Add(sub);

                // Строки «применена сейчас» здесь нет намеренно: точка у названия
                // говорит то же самое, а от неё строки списка перестают быть одной
                // высоты и список начинает прыгать при переключении.

                item.Content = sp;
                item.ContextMenu = BuildProfileMenu(l);
                item.MouseDoubleClick += delegate { BeginRename(); };
                _profileList.Items.Add(item);
                if (ReferenceEquals(l, _current)) _profileList.SelectedItem = item;
            }
            _updating = false;
        }

        /// <summary>Правка списка профилей: переименовать, переставить, удалить.</summary>
        private ContextMenu BuildProfileMenu(Layout layout)
        {
            var menu = new ContextMenu();
            menu.Style = (Style)Application.Current.Resources["FlyoutMenu"];

            menu.Items.Add(MenuAction("Переименовать", "F2", delegate
            {
                SelectProfile(layout);
                BeginRename();
            }));
            menu.Items.Add(MenuAction("Переместить вверх", "Alt+↑", delegate { MoveProfile(layout, -1); }));
            menu.Items.Add(MenuAction("Переместить вниз", "Alt+↓", delegate { MoveProfile(layout, +1); }));

            var sep = new Separator();
            sep.Style = (Style)Application.Current.Resources["MenuSeparator"];
            menu.Items.Add(sep);

            menu.Items.Add(MenuAction("Удалить", "", delegate
            {
                SelectProfile(layout);
                DeleteCurrent();
            }));
            return menu;
        }

        private MenuItem MenuAction(string header, string gesture, Action action)
        {
            var item = new MenuItem();
            item.Style = (Style)Application.Current.Resources["FlyoutMenuItem"];
            item.Header = header;
            item.InputGestureText = gesture;
            item.Click += delegate { action(); };
            return item;
        }

        private void MoveProfile(Layout layout, int delta)
        {
            int index = _profiles.IndexOf(layout);
            int target = index + delta;
            if (index < 0 || target < 0 || target >= _profiles.Count) return;

            _profiles.RemoveAt(index);
            _profiles.Insert(target, layout);
            Store.SaveOrder(_profiles);
            RefreshProfileList();
            SelectListItemFor(layout);
            Raise(ProfilesChanged);
        }

        private void SelectListItemFor(Layout layout)
        {
            _updating = true;
            foreach (ListBoxItem item in _profileList.Items)
                if (ReferenceEquals(item.Tag, layout)) { _profileList.SelectedItem = item; break; }
            _updating = false;
        }

        private void SelectProfile(Layout layout)
        {
            // Раньше правки сохранялись молча при любом переключении — так профиль можно было
            // перезаписать, сам того не заметив. Теперь решение всегда за пользователем.
            if (_dirty && _current != null && !ReferenceEquals(layout, _current)) AskToSave();
            _current = layout;
            _currentOriginalName = layout.Name;
            _dirty = false;
            SyncLayoutWithLive(_current);
            _canvas.Layout = _current;
            _canvas.Selected = FirstInteresting(_current);
            _canvas.Rebuild();
            UpdateHeader();
            UpdateProperties();
            UpdateStatus(null);
            SelectListItemFor(layout);
        }

        private static LayoutDisplay FirstInteresting(Layout layout)
        {
            foreach (var d in layout.Displays) if (d.Enabled && d.Primary) return d;
            foreach (var d in layout.Displays) if (d.Enabled) return d;
            return layout.Displays.Count > 0 ? layout.Displays[0] : null;
        }

        /// <summary>Добавляет в профиль мониторы, которых в нём ещё нет (подключили новый — он сразу виден).</summary>
        private void SyncLayoutWithLive(Layout layout)
        {
            foreach (var kv in _live)
            {
                if (layout.Find(kv.Key) != null) continue;
                var m = kv.Value;
                var d = new LayoutDisplay();
                d.Identity = m.Identity;
                d.Name = m.Name;
                d.Enabled = false;
                d.Width = m.PreferredWidth > 0 ? m.PreferredWidth : 1920;
                d.Height = m.PreferredHeight > 0 ? m.PreferredHeight : 1080;
                d.Hz = m.PreferredHz > 0 ? m.PreferredHz : 60;
                d.Hdr = -1;
                PlaceRightOfAll(layout, d);
                layout.Displays.Add(d);
            }
        }

        private void PlaceRightOfAll(Layout layout, LayoutDisplay d)
        {
            int right = 0;
            foreach (var o in layout.Displays) right = Math.Max(right, o.X + o.DesktopWidth);
            d.X = right;
            d.Y = 0;
        }

        // ---------------------------------------------------------------- действия
        private void NewFromCurrent()
        {
            ReloadLive();
            var layout = Layout.FromCurrent(Store.UniqueName("Новая раскладка"));
            Store.Save(layout);
            ReloadProfiles(layout.Name);
            Raise(ProfilesChanged);
            UpdateStatus("Создан профиль из текущей раскладки. Переименуйте и назначьте сочетание клавиш.");
            _nameBox.Focus();
            _nameBox.SelectAll();
        }

        private void DeleteCurrent()
        {
            if (_current == null) return;
            var dialog = new ConfirmDialog("Удалить профиль?", "«" + _current.Name + "» будет удалён безвозвратно.", "Удалить");
            dialog.Owner = this;
            if (dialog.ShowDialog() != true) return;

            Store.Delete(_current);
            _dirty = false;
            _current = null;
            ReloadProfiles(null);
            Raise(ProfilesChanged);
        }

        /// <summary>Спросить про несохранённые правки — молча перезаписывать профиль нельзя.</summary>
        private void AskToSave()
        {
            if (_current == null || !_dirty) return;
            var dialog = new ConfirmDialog("Сохранить изменения?",
                "В профиле «" + _current.Name + "» есть несохранённые правки.", "Сохранить");
            dialog.Owner = IsVisible ? this : null;
            if (dialog.ShowDialog() == true) SaveCurrent(false);
            else _dirty = false;
        }

        private void SaveCurrent(bool report)
        {
            if (_current == null) return;
            if (string.IsNullOrEmpty(_current.Name.Trim())) _current.Name = Store.UniqueName("Раскладка");
            _canvas.Normalize();
            Store.Rename(_currentOriginalName, _current);
            _currentOriginalName = _current.Name;
            _dirty = false;
            _saveButton.IsEnabled = false;
            Raise(ProfilesChanged);
            if (report) UpdateStatus("Сохранено.");
        }

        private void ApplyCurrent()
        {
            if (_current == null) return;
            SaveCurrent(false);
            var result = Applier.Apply(_current);
            ReloadLive();
            _canvas.Refresh();
            RefreshProfileList();

            var parts = new List<string>();
            if (result.Success) parts.Add("Раскладка применена.");
            else parts.Add("Не удалось: " + result.Error);
            foreach (string w in result.Warnings) parts.Add(w);
            UpdateStatus(string.Join("  ", parts.ToArray()));
        }

        public void ApplyByName(string name)
        {
            var layout = Store.LoadByName(name);
            if (layout == null) return;
            Applier.Apply(layout);
        }

        private static void Raise(Action a) { if (a != null) a(); }

        // ---------------------------------------------------------------- свойства
        // ---------------------------------------------------------------- переименование
        private void BeginRename()
        {
            if (_current == null) return;
            _renaming = true;
            _updating = true;
            _nameBox.Text = _current.Name;
            _updating = false;
            _nameText.Visibility = Visibility.Collapsed;
            _nameBox.Visibility = Visibility.Visible;
            _nameBox.Focus();
            _nameBox.SelectAll();
        }

        private void CommitRename(bool keep)
        {
            if (!_renaming) return;
            _renaming = false;
            _nameBox.Visibility = Visibility.Collapsed;
            _nameText.Visibility = Visibility.Visible;

            if (keep && _current != null)
            {
                string name = _nameBox.Text.Trim();
                if (name.Length > 0 && name != _current.Name)
                {
                    _current.Name = name;
                    SaveCurrent(false);
                    RefreshProfileList();
                    Raise(ProfilesChanged);
                }
            }
            UpdateHeader();
        }

        /// <summary>Открыть экран настроек панели задач для текущего профиля.</summary>
        public void ShowTaskbarSettings() { OpenTaskbarSettings(); }

        /// <summary>Окно «О программе». Открывается и отсюда, и из меню значка в трее.</summary>
        public void ShowAbout()
        {
            var window = new AboutWindow();
            if (IsVisible) window.Owner = this;
            window.ShowDialog();
        }

        /// <summary>Открыть отдельный экран настроек панели задач для текущего профиля.</summary>
        private void OpenTaskbarSettings()
        {
            if (_current == null) return;

            var before = _current.Taskbar.Copy();
            var window = new TaskbarWindow(_current.Name, _current.Taskbar);
            window.Owner = this;
            window.ShowDialog();

            // предпросмотр менял вид на лету, поэтому считаем изменённым по факту
            if (!SameSettings(before, _current.Taskbar)) MarkDirty();
            UpdateTaskbarButton();
            UpdateStatus(null);
        }

        private static bool SameSettings(TaskbarSettings a, TaskbarSettings b)
        {
            foreach (TaskbarState s in TaskbarSettings.Order)
                if (a[s].Serialize() != b[s].Serialize()) return false;
            return true;
        }

        private void UpdateTaskbarButton()
        {
            _taskbarButton.IsEnabled = _current != null;
            int on = 0;
            if (_current != null)
                foreach (TaskbarState s in TaskbarSettings.Order) if (_current.Taskbar[s].Enabled) on++;

            _taskbarButton.Content = Glyph("", on > 0 ? "Панель задач · " + on : "Панель задач");
            _taskbarButton.ToolTip = ExplorerTap.Available
                ? "Как выглядит панель задач в этом профиле"
                : "Нет DIIIX.ExplorerTAP.dll рядом с программой";
        }

        private void UpdateHeader()
        {
            _updating = true;
            _nameText.Text = _current != null ? _current.Name : "";
            _nameHint.Text = DescribeCurrent();
            _applyButton.IsEnabled = _current != null;
            UpdateTaskbarButton();
            _saveButton.IsEnabled = _current != null && _dirty;
            _hotkeyButton.Content = Glyph("", _current != null && !string.IsNullOrEmpty(_current.Hotkey)
                ? _current.Hotkey : "Сочетание клавиш");
            _updating = false;
        }

        /// <summary>Короткая строка под названием профиля: что в нём и чем переключается.</summary>
        private string DescribeCurrent()
        {
            if (_current == null) return "";

            int on = 0;
            foreach (var d in _current.Displays) if (d.Enabled) on++;

            var parts = new List<string>();
            parts.Add(on == 1 ? "один монитор" : "мониторов: " + on);
            if (!string.IsNullOrEmpty(_current.Hotkey)) parts.Add("клавиша " + _current.Hotkey);
            if (_current.Taskbar != null && _current.Taskbar.AnyEnabled) parts.Add("свой вид панели задач");

            return string.Join(" · ", parts.ToArray());
        }

        private void UpdateProperties()
        {
            _updating = true;
            var d = _canvas.Selected;
            bool has = d != null;
            _propsPanel.IsEnabled = has;
            _propsPanel.Opacity = has ? 1.0 : 0.45;

            if (!has)
            {
                _monitorTitle.Text = "Выберите монитор";
                _monitorSubtitle.Text = "";
                _resCombo.ItemsSource = null;
                _hzCombo.ItemsSource = null;
                ShowFields();
                _updating = false;
                return;
            }

            MonitorInfo live;
            _live.TryGetValue(d.Identity, out live);

            _monitorTitle.Text = d.Name;
            string subtitle = live != null
                ? (live.ConnectionType + " · " + (live.Active ? "сейчас включён" : "сейчас выключен"))
                : "сейчас не подключён";
            if (d.Enabled && d.CloneGroup >= 0) subtitle += " · при дублировании HDR недоступен";
            _monitorSubtitle.Text = subtitle;

            _enabledSwitch.IsChecked = d.Enabled;
            _primarySwitch.IsChecked = d.Primary;
            _primarySwitch.IsEnabled = d.Enabled;

            // разрешения
            var resItems = new List<Item>();
            var resList = live != null ? live.Modes : new List<Resolution>();
            bool found = false;
            foreach (var r in resList)
            {
                resItems.Add(new Item(r.Width + " × " + r.Height, r));
                if (r.Width == d.Width && r.Height == d.Height) found = true;
            }
            if (!found)
            {
                var custom = new Resolution();
                custom.Width = d.Width; custom.Height = d.Height;
                custom.Rates.Add(d.Hz > 0 ? d.Hz : 60);
                resItems.Insert(0, new Item(d.Width + " × " + d.Height, custom));
            }
            _resCombo.ItemsSource = resItems;
            foreach (Item it in resItems)
            {
                var r = (Resolution)it.Value;
                if (r.Width == d.Width && r.Height == d.Height) { _resCombo.SelectedItem = it; break; }
            }

            FillRates(d, live);

            var rotItems = new List<Item>();
            rotItems.Add(new Item("Обычный", Native.DISPLAYCONFIG_ROTATION_IDENTITY));
            rotItems.Add(new Item("90°", Native.DISPLAYCONFIG_ROTATION_ROTATE90));
            rotItems.Add(new Item("180°", Native.DISPLAYCONFIG_ROTATION_ROTATE180));
            rotItems.Add(new Item("270°", Native.DISPLAYCONFIG_ROTATION_ROTATE270));
            _rotCombo.ItemsSource = rotItems;
            foreach (Item it in rotItems) if ((uint)it.Value == d.Rotation) _rotCombo.SelectedItem = it;

            var cloneItems = new List<Item>();
            cloneItems.Add(new Item("Отдельно", null));
            foreach (var other in _current.Displays)
            {
                if (ReferenceEquals(other, d) || !other.Enabled) continue;
                cloneItems.Add(new Item("Дубль: " + other.Name, other));
            }
            _cloneCombo.ItemsSource = cloneItems;
            _cloneCombo.SelectedItem = cloneItems[0];
            if (d.CloneGroup >= 0)
            {
                foreach (Item it in cloneItems)
                {
                    var other = it.Value as LayoutDisplay;
                    if (other != null && other.CloneGroup == d.CloneGroup) { _cloneCombo.SelectedItem = it; break; }
                }
            }
            _cloneCombo.IsEnabled = d.Enabled;

            var hdrItems = new List<Item>();
            hdrItems.Add(new Item("Не менять", -1));
            hdrItems.Add(new Item("Включить", 1));
            hdrItems.Add(new Item("Выключить", 0));
            _hdrCombo.ItemsSource = hdrItems;
            foreach (Item it in hdrItems) if ((int)it.Value == d.Hdr) _hdrCombo.SelectedItem = it;

            // Windows не предоставляет расширенный цвет выходам, объединённым в дублирование —
            // проверено на LG UltraGear + Q80A и на 60, и на 120 Гц
            bool cloned = d.CloneGroup >= 0;
            _hdrCombo.IsEnabled = d.Enabled && !cloned;
            _hdrCombo.ToolTip = cloned
                ? "Windows отключает HDR на выходах, объединённых в дублирование, независимо от разрешения и частоты. Чтобы получить HDR на обоих экранах, разведите их как отдельные рабочие столы."
                : null;

            // Что показать: у выключенного монитора настраивать нечего, частота нужна
            // только при выборе, дублирование — только если есть с чем, HDR — только
            // там, где он в принципе возможен. Причина отсутствия HDR в дубле уже
            // написана строкой выше, под названием монитора.
            if (!d.Enabled)
            {
                ShowFields(_offNote);
            }
            else
            {
                int others = 0;
                foreach (var other in _current.Displays)
                    if (!ReferenceEquals(other, d) && other.Enabled) others++;

                ShowFields(_fieldRes,
                           _hzCombo.Items.Count > 1 ? _fieldHz : null,
                           _fieldRot,
                           others > 0 ? _fieldClone : null,
                           cloned ? null : _fieldHdr);
            }

            _updating = false;
        }

        private void FillRates(LayoutDisplay d, MonitorInfo live)
        {
            var rates = new List<double>();
            if (live != null)
            {
                foreach (var r in live.Modes)
                    if (r.Width == d.Width && r.Height == d.Height) { rates.AddRange(r.Rates); break; }
            }
            if (rates.Count == 0 && d.Hz > 0) rates.Add(d.Hz);
            bool has = false;
            foreach (double hz in rates) if (Math.Abs(hz - d.Hz) < 0.6) has = true;
            if (!has && d.Hz > 0) rates.Insert(0, d.Hz);

            var items = new List<Item>();
            foreach (double hz in rates) items.Add(new Item(hz.ToString("0.##") + " Гц", hz));
            _hzCombo.ItemsSource = items;
            foreach (Item it in items) if (Math.Abs((double)it.Value - d.Hz) < 0.6) { _hzCombo.SelectedItem = it; break; }
            if (_hzCombo.SelectedItem == null && items.Count > 0) _hzCombo.SelectedItem = items[0];
        }

        private void OnEnabledChanged(bool enabled)
        {
            if (_updating || _canvas.Selected == null) return;
            var d = _canvas.Selected;
            d.Enabled = enabled;
            if (enabled)
            {
                bool overlaps = false;
                foreach (var o in _current.Displays)
                    if (!ReferenceEquals(o, d) && o.Enabled && o.X == d.X && o.Y == d.Y) overlaps = true;
                if (overlaps || (d.X == 0 && d.Y == 0 && AnyOtherAtOrigin(d))) PlaceRightOfAll(_current, d);
            }
            else
            {
                d.Primary = false;
                d.CloneGroup = -1;
            }
            EnsurePrimary();
            _canvas.Normalize();
            _canvas.Relayout();
            MarkDirty();
            UpdateProperties();
            UpdateStatus(null);
        }

        private bool AnyOtherAtOrigin(LayoutDisplay d)
        {
            foreach (var o in _current.Displays)
                if (!ReferenceEquals(o, d) && o.Enabled && o.X == 0 && o.Y == 0) return true;
            return false;
        }

        private void EnsurePrimary()
        {
            bool any = false;
            foreach (var o in _current.Displays) if (o.Enabled && o.Primary) any = true;
            if (any) return;
            foreach (var o in _current.Displays) if (o.Enabled) { o.Primary = true; return; }
        }

        private void OnPrimaryChanged(bool primary)
        {
            if (_updating || _canvas.Selected == null) return;
            var d = _canvas.Selected;
            if (!primary) { _updating = true; _primarySwitch.IsChecked = true; _updating = false; return; }
            foreach (var o in _current.Displays) o.Primary = ReferenceEquals(o, d);
            _canvas.Normalize();
            _canvas.Relayout();
            MarkDirty();
            UpdateStatus(null);
        }

        private void OnResChanged()
        {
            if (_updating || _canvas.Selected == null) return;
            var item = _resCombo.SelectedItem as Item;
            if (item == null) return;
            var r = (Resolution)item.Value;
            var d = _canvas.Selected;
            d.Width = r.Width;
            d.Height = r.Height;
            MonitorInfo live;
            _live.TryGetValue(d.Identity, out live);
            _updating = true;
            FillRates(d, live);
            _updating = false;
            var sel = _hzCombo.SelectedItem as Item;
            if (sel != null) d.Hz = (double)sel.Value;
            SyncCloneGroup(d);
            _canvas.Relayout();
            MarkDirty();
            UpdateStatus(null);
        }

        private void OnHzChanged()
        {
            if (_updating || _canvas.Selected == null) return;
            var item = _hzCombo.SelectedItem as Item;
            if (item == null) return;
            _canvas.Selected.Hz = (double)item.Value;
            _canvas.Refresh();
            MarkDirty();
        }

        private void OnRotChanged()
        {
            if (_updating || _canvas.Selected == null) return;
            var item = _rotCombo.SelectedItem as Item;
            if (item == null) return;
            _canvas.Selected.Rotation = (uint)item.Value;
            SyncCloneGroup(_canvas.Selected);
            _canvas.Relayout();
            MarkDirty();
            UpdateStatus(null);
        }

        private void OnCloneChanged()
        {
            if (_updating || _canvas.Selected == null) return;
            var item = _cloneCombo.SelectedItem as Item;
            if (item == null) return;
            var d = _canvas.Selected;
            var other = item.Value as LayoutDisplay;

            if (other == null) { d.CloneGroup = -1; }
            else
            {
                if (other.CloneGroup < 0) other.CloneGroup = NextCloneGroup();
                d.CloneGroup = other.CloneGroup;
                d.X = other.X; d.Y = other.Y;
                if (d.DesktopWidth != other.DesktopWidth || d.DesktopHeight != other.DesktopHeight)
                    UpdateStatus("Разрешения различаются — при дублировании Windows подгонит картинку под общий режим.");
            }
            CleanupCloneGroups();
            _canvas.Relayout();
            MarkDirty();
            UpdateProperties();
        }

        private void SyncCloneGroup(LayoutDisplay d)
        {
            if (d.CloneGroup < 0) return;
            foreach (var o in _current.Displays)
                if (!ReferenceEquals(o, d) && o.CloneGroup == d.CloneGroup) { o.X = d.X; o.Y = d.Y; }
        }

        private int NextCloneGroup()
        {
            int max = -1;
            foreach (var o in _current.Displays) max = Math.Max(max, o.CloneGroup);
            return max + 1;
        }

        private void CleanupCloneGroups()
        {
            var counts = new Dictionary<int, int>();
            foreach (var o in _current.Displays)
            {
                if (o.CloneGroup < 0 || !o.Enabled) continue;
                int c; counts.TryGetValue(o.CloneGroup, out c); counts[o.CloneGroup] = c + 1;
            }
            foreach (var o in _current.Displays)
                if (o.CloneGroup >= 0 && (!o.Enabled || counts[o.CloneGroup] < 2)) o.CloneGroup = -1;
        }

        private void OnHdrChanged()
        {
            if (_updating || _canvas.Selected == null) return;
            var item = _hdrCombo.SelectedItem as Item;
            if (item == null) return;
            var d = _canvas.Selected;
            d.Hdr = (int)item.Value;

            // в группе дублирования HDR обязан быть одинаковым, иначе Windows уводит всю группу в SDR
            if (d.CloneGroup >= 0)
                foreach (var o in _current.Displays)
                    if (o.CloneGroup == d.CloneGroup) o.Hdr = d.Hdr;

            _canvas.Refresh();
            MarkDirty();

            if (d.Hdr == 1 && d.CloneGroup >= 0)
                UpdateStatus("HDR не работает при дублировании: Windows не даёт расширенный цвет выходам, сидящим на одном рабочем столе. Разведите экраны — тогда HDR можно включить на каждом.");
        }

        private void MarkDirty()
        {
            _dirty = true;
            _saveButton.IsEnabled = true;
        }

        private void UpdateStatus(string text)
        {
            if (text == null && _current != null)
            {
                string problem = _canvas.Validate();
                text = problem ?? Summary();
            }
            _status.Text = text ?? "";
        }

        private string Summary()
        {
            if (_current == null) return "";
            var names = new List<string>();
            foreach (var d in _current.Displays)
                if (d.Enabled) names.Add(d.Name);

            string now = _activeProfileName != null
                ? ("Сейчас применена: «" + _activeProfileName + "»")
                : "Сейчас применена раскладка, которой нет среди профилей";

            if (names.Count == 0) return now + ".   В этом профиле не выбран ни один монитор.";
            return now + ".   В профиле «" + _current.Name + "»: " + string.Join(", ", names.ToArray());
        }

        // ---------------------------------------------------------------- горячая клавиша
        private void BeginHotkeyCapture()
        {
            if (_current == null) return;
            _capturingHotkey = true;
            _hotkeyButton.Content = Glyph("", "Нажмите сочетание…");
            UpdateStatus("Нажмите сочетание клавиш. Esc — отмена, Delete — убрать сочетание.");
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_capturingHotkey && !_renaming && _current != null)
            {
                if (e.Key == Key.F2) { BeginRename(); e.Handled = true; return; }
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    Key arrow = e.Key == Key.System ? e.SystemKey : e.Key;
                    if (arrow == Key.Up) { MoveProfile(_current, -1); e.Handled = true; return; }
                    if (arrow == Key.Down) { MoveProfile(_current, +1); e.Handled = true; return; }
                }
            }

            if (!_capturingHotkey) return;
            e.Handled = true;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape) { _capturingHotkey = false; UpdateHeader(); UpdateStatus(null); return; }
            if (key == Key.Delete || key == Key.Back)
            {
                _current.Hotkey = "";
                _capturingHotkey = false;
                MarkDirty(); SaveCurrent(false);
                UpdateHeader(); RefreshProfileList();
                UpdateStatus("Сочетание убрано.");
                return;
            }
            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;

            string combo = Hotkey.Describe(Keyboard.Modifiers, key);
            if (combo == null) { UpdateStatus("Такую клавишу назначить нельзя — выберите другую."); return; }

            _capturingHotkey = false;
            _current.Hotkey = combo;
            MarkDirty();
            SaveCurrent(false);
            UpdateHeader();
            RefreshProfileList();

            uint mods, vk; string err;
            if (Hotkey.TryParse(combo, out mods, out vk, out err))
                UpdateStatus("Сочетание " + combo + " назначено профилю «" + _current.Name + "».");
        }
    }
}
