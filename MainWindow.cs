// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace DIIISPL
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

        /// <summary>Сказать то, что не поместилось бы в спрятанное окно. Слушает трей.</summary>
        public event Action<string, bool> ApplyReported;

        /// <summary>Окно ушло в трей. Слушает трей — ему есть чем об этом сказать.</summary>
        public event Action HiddenToTray;

        private readonly ListBox _profileList = new ListBox();
        private readonly TextBlock _nameText = new TextBlock();
        private readonly TextBlock _nameHint = new TextBlock();
        private readonly TextBox _nameBox = new TextBox();
        private readonly Button _hotkeyButton = new Button();
        private Button _moreButton;
#if DEV
        private readonly Button _taskbarButton = new Button();   // только в сборках Dev
        // Экран с полосой у нижнего края — это и есть панель задач. Прежде
        // здесь стоял «TVMonitor»: монитор на подставке, в программе, где
        // монитор значит монитор.
        private const string TaskbarGlyph = "\uE90E";
        // Шапка и ряд кнопок — чтобы померить, помещается ли подпись словами.
        private Grid _headerGrid;
        private StackPanel _headerActions;
#endif
        private readonly Button _applyButton = new Button();
        private readonly Button _saveButton = new Button();
        private readonly MonitorCanvas _canvas = new MonitorCanvas();
        private readonly TextBlock _status = new TextBlock();
        private readonly Border _statusBar = new Border();

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

        public MainWindow()
        {
            Title = "DIIISPL";
            Width = 1180;
            Height = 760;
            MinWidth = 960;
            MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = (Brush)Application.Current.Resources["Bg"];
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);

            Content = BuildUi();

#if DEV
            // Ширина окна решает, словами подписана кнопка панели задач или числом.
            SizeChanged += delegate { UpdateTaskbarButton(); };
#endif

            SourceInitialized += delegate { Fluent.ApplyWindowStyling(this); };
            PreviewKeyDown += OnPreviewKeyDown;

            // Ушли из окна — вышли и из захвата сочетания: иначе окно остаётся
            // глухим к клавиатуре, а вернувшись, человек об этом не знает.
            Deactivated += delegate
            {
                if (!_capturingHotkey) return;
                _capturingHotkey = false;
                UpdateHeader();
                UpdateStatus(null);
            };
            Closing += delegate(object s, System.ComponentModel.CancelEventArgs e)
            {
                e.Cancel = true;                  // окно не закрывается, а прячется в трей

                // Набранное имя принимаем, а не выбрасываем: человек его набрал,
                // и молча терять это при закрытии окна не за что. Делаем до
                // вопроса о сохранении — иначе имя в него не попадёт.
                if (_renaming) CommitRename(true, true);
                if (_dirty && !AskToSave()) return;

                // Незавершённые режимы окно с собой в трей не уносит: иначе при
                // следующем открытии поверх заголовка висит поле ввода со старым
                // именем или окно молча ждёт нажатия клавиши для сочетания.
                CancelRename();
                _capturingHotkey = false;
                Hide();

                // Крестик прячет окно, а не закрывает программу. Молча это читается
                // как «закрыл»: дальше человек либо удивляется, что «закрытая»
                // программа переключает экраны, либо ищет выход и не находит —
                // «Выход» есть только в меню значка, о котором ещё надо догадаться.
                Raise(HiddenToTray);
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

            // Полоса внизу появляется, только когда есть что сказать, — иначе внизу окна
            // оставалась бы пустая ступенька.
            _statusBar.Padding = new Thickness(20, 8, 20, 12);
            _statusBar.Visibility = Visibility.Collapsed;
            _status.Style = (Style)Application.Current.Resources["Caption"];
            // Перенос, а не многоточие: после применения сюда попадают все
            // предупреждения разом, и в одну строку они не помещались — человек
            // видел «Раскладка применена» и обрезанное начало того, что пошло не
            // так. Читать их негде: подсказки и прокрутки у полосы нет.
            _status.TextWrapping = TextWrapping.Wrap;
            // Выше четырёх строк полоса не растёт, а всё, что не поместилось,
            // лежит в подсказке: обрезать молча — то же многоточие, только хуже.
            _status.MaxHeight = 72;
            _statusBar.Child = _status;
            Grid.SetRow(_statusBar, 1);
            root.Children.Add(_statusBar);

            return root;
        }

        private UIElement BuildSidebar()
        {
            var panel = new Grid();
            panel.Margin = new Thickness(12, 8, 6, 12);
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // название
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // шапка списка
            panel.RowDefinitions.Add(new RowDefinition());                              // список
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // «О программе»

            // Название программы над списком — как в окне MagicKeys: сначала кто это,
            // потом уже содержимое.
            var top = new StackPanel();
            top.Margin = new Thickness(10, 6, 10, 14);

            var brandName = new TextBlock();
            brandName.Text = App.AppTitle;
            brandName.Style = (Style)Application.Current.Resources["Subtitle"];
            top.Children.Add(brandName);

            var brandSub = new TextBlock();
            brandSub.Text = Text.AppTagline;
            brandSub.Style = (Style)Application.Current.Resources["Caption"];
            brandSub.Margin = new Thickness(0, 2, 0, 0);
            top.Children.Add(brandSub);

            panel.Children.Add(top);

            // Создание профиля и действия над ним — в шапке списка, рядом с его
            // заголовком: они относятся к списку, а не к окну целиком.
            //
            // Шапка лежит отдельным рядом сетки, а не внутри блока с названием: у того
            // свой отступ справа, и кнопки оказывались левее правого края списка.
            // Здесь правый край общий со строками списка, и всё стоит по одной линии.
            var headerRow = new Grid();
            headerRow.Margin = new Thickness(10, 0, 0, 6);
            Grid.SetRow(headerRow, 1);
            headerRow.ColumnDefinitions.Add(new ColumnDefinition());
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var header = new TextBlock();
            header.Text = Text.ProfilesHeader;
            header.Style = (Style)Application.Current.Resources["SectionHeader"];
            header.VerticalAlignment = VerticalAlignment.Center;
            headerRow.Children.Add(header);

            var headerButtons = new StackPanel();
            headerButtons.Orientation = Orientation.Horizontal;
            Grid.SetColumn(headerButtons, 1);

            var newButton = SmallButton("+", Text.NewProfileTip);
            newButton.Click += delegate { NewFromCurrent(); };
            headerButtons.Children.Add(newButton);

            var moreButton = SmallButton("···", Text.ProfileActionsTip);
            // Без профиля эта кнопка и «Сочетание клавиш» на нажатие не отвечали
            // ничем: ни действия, ни объяснения. Соседние «Сохранить» и «Применить»
            // в этом состоянии честно погашены — образец уже был.
            moreButton.IsEnabled = false;
            _moreButton = moreButton;
            moreButton.Margin = new Thickness(2, 0, 0, 0);
            moreButton.Click += delegate
            {
                if (_current == null) return;
                var menu = BuildProfileMenu(_current);
                menu.PlacementTarget = moreButton;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                menu.IsOpen = true;
            };
            headerButtons.Children.Add(moreButton);

            headerRow.Children.Add(headerButtons);
            panel.Children.Add(headerRow);

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
            Grid.SetRow(_profileList, 2);
            panel.Children.Add(_profileList);

            // «О программе» — строкой того же вида, что и профили выше: в MagicKeys
            // это такой же пункт списка, и глазу привычнее видеть здесь строку, а не
            // кнопку другой формы.
            var about = new Button();
            about.Style = (Style)Application.Current.Resources["BtnSubtle"];
            about.Content = Text.About;
            about.Padding = new Thickness(10, 8, 10, 8);
            about.Height = double.NaN;
            about.HorizontalContentAlignment = HorizontalAlignment.Left;
            about.HorizontalAlignment = HorizontalAlignment.Stretch;
            about.Margin = new Thickness(0, 8, 0, 0);
            about.Click += delegate { ShowAbout(); };
            Grid.SetRow(about, 3);
            panel.Children.Add(about);

            return panel;
        }

        /// <summary>Квадратная кнопка на одну-две буквы — для шапки списка.</summary>
        private static Button SmallButton(string caption, string tip)
        {
            var b = new Button();
            b.Style = (Style)Application.Current.Resources["BtnSubtle"];
            b.Content = caption;
            b.Width = 28;
            b.Height = 26;
            b.Padding = new Thickness(0);
            b.FontSize = 15;
            b.ToolTip = tip;
            return b;
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
#if DEV
            _headerGrid = header;
#endif

            // Название по умолчанию — обычный заголовок. Правка включается двойным щелчком,
            // клавишей F2 или пунктом «Переименовать» в списке слева.
            var nameStack = new Grid();
            nameStack.HorizontalAlignment = HorizontalAlignment.Left;

            _nameText.Style = (Style)Application.Current.Resources["Subtitle"];
            _nameText.FontSize = 24;
            _nameText.VerticalAlignment = VerticalAlignment.Center;
            _nameText.Margin = new Thickness(2, 0, 0, 0);
            // Сжиматься название должно сильнее, чем кажется разумным. Ряд кнопок
            // справа растёт по своему содержимому, и при узком окне — особенно в
            // сборках Dev, где кнопок на одну больше, — сумма перестаёт помещаться:
            // раньше при 240 сетка вылезала за край и кнопки уезжали за границу
            // окна. Название под многоточием читается хуже, но остаётся на месте
            // и целиком видно в списке слева.
            _nameText.MinWidth = 120;
            _nameText.TextTrimming = TextTrimming.CharacterEllipsis;
            _nameText.Cursor = Cursors.IBeam;
            _nameText.ToolTip = Text.RenameTip;
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
            _nameBox.MaxLength = 80;      // длиннее — и путь к файлу перешагнёт предел в 260 знаков
            _nameBox.Visibility = Visibility.Collapsed;
            _nameBox.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { CommitRename(true); e.Handled = true; }
                else if (e.Key == Key.Escape) { CommitRename(false); e.Handled = true; }
            };
            _nameBox.LostFocus += delegate { if (_renaming) CommitRename(true, true); };
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
#if DEV
            _headerActions = actions;
#endif

#if DEV
            // вид панели задач — свойство профиля, а не монитора, поэтому здесь
            _taskbarButton.Style = (Style)Application.Current.Resources["Btn"];
            _taskbarButton.Height = 42;
            _taskbarButton.Margin = new Thickness(0, 0, 8, 0);
            _taskbarButton.Click += delegate { OpenTaskbarSettings(); };
            actions.Children.Add(_taskbarButton);
#endif

            _hotkeyButton.Style = (Style)Application.Current.Resources["Btn"];
            _hotkeyButton.Height = 42;
            _hotkeyButton.Margin = new Thickness(0, 0, 8, 0);
            _hotkeyButton.Click += delegate { BeginHotkeyCapture(); };
            actions.Children.Add(_hotkeyButton);

            _saveButton.Style = (Style)Application.Current.Resources["Btn"];
            _saveButton.Height = 42;
            _saveButton.Content = Glyph("", Text.Save);
            _saveButton.Margin = new Thickness(0, 0, 8, 0);
            _saveButton.Click += delegate { SaveCurrent(true); };
            actions.Children.Add(_saveButton);

            _applyButton.Style = (Style)Application.Current.Resources["BtnAccent"];
            _applyButton.Height = 42;
            _applyButton.Content = Glyph("", Text.Apply);
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
            _canvas.DropRequested += delegate
            {
                var d = _canvas.Selected;
                if (d != null) OnEnabledChanged(true, true);
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

            // Название слева, «Основной» — справа в той же строке: это свойство самого
            // монитора в раскладке, а не настройка режима, и рядом с именем ему уместнее.
            var titleRow = new Grid();
            titleRow.Margin = new Thickness(0, 0, 0, 12);
            titleRow.ColumnDefinitions.Add(new ColumnDefinition());
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleLeft = new StackPanel();
            titleLeft.Orientation = Orientation.Horizontal;
            _monitorTitle.Style = (Style)Application.Current.Resources["Subtitle"];
            _monitorTitle.FontSize = 16;
            _monitorTitle.VerticalAlignment = VerticalAlignment.Center;
            titleLeft.Children.Add(_monitorTitle);
            _monitorSubtitle.Style = (Style)Application.Current.Resources["Caption"];
            _monitorSubtitle.Margin = new Thickness(12, 0, 0, 0);
            _monitorSubtitle.VerticalAlignment = VerticalAlignment.Center;
            titleLeft.Children.Add(_monitorSubtitle);
            titleRow.Children.Add(titleLeft);

            _primarySwitch.ToolTip = Text.PrimaryTip;
            UIElement primary = WrapSwitch(_primarySwitch, Text.PrimaryScreen);
            Grid.SetColumn(primary, 1);
            titleRow.Children.Add(primary);

            outer.Children.Add(titleRow);

            var switches = new StackPanel();
            switches.Margin = new Thickness(0, 0, 18, 0);
            switches.VerticalAlignment = VerticalAlignment.Center;
            switches.Children.Add(WrapSwitch(_enabledSwitch, Text.Enabled));

            _fieldRes = Field(Text.Resolution, _resCombo, 150);
            _fieldHz = Field(Text.Refresh, _hzCombo, 100);
            _fieldRot = Field(Text.Rotation, _rotCombo, 120);
            _fieldClone = Field(Text.Cloning, _cloneCombo, 170);
            _fieldHdr = Field(Text.Hdr, _hdrCombo, 120);

            _offNote.Style = (Style)Application.Current.Resources["Caption"];
            _offNote.Text = Text.TurnOnNote;
            _offNote.VerticalAlignment = VerticalAlignment.Center;

            _fieldsRow.Orientation = Orientation.Horizontal;
            outer.Children.Add(_fieldsRow);
            _fieldsRow.Children.Add(switches);

            _enabledSwitch.Checked += delegate { OnEnabledChanged(true, false); };
            _enabledSwitch.Unchecked += delegate { OnEnabledChanged(false, false); };
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

        /// <summary>
        /// Перечитать список, оставшись на том же профиле и не тронув несохранённое.
        ///
        /// Зовётся, когда список надо освежить не по просьбе человека: сменилась
        /// картинка экранов, окно достали из трея. Прежде обе точки звали
        /// `ReloadProfiles(null)`, а это значит «выбрать первый»: человек ничего
        /// не делал, а выделение прыгало на чужой профиль. С несохранённой правкой
        /// выходило хуже — на ровном месте появлялся вопрос «Сохранить правки?».
        /// </summary>
        public void RefreshProfiles()
        {
            // Пока идёт перестроение, список не трогаем вовсе: смену картинки
            // экранов вызывает само перестроение, а перечитывание списка гасит
            // и надпись «Перестраиваю экраны…», и признак занятости кнопки.
            if (ApplyBusy.Active) return;

            if (_dirty)
            {
                // Несохранённое чужими руками не трогаем — но только несохранённое.
                // Только что подключённый монитор попадает в раскладку лишь здесь,
                // и без этого вызова он не появлялся ни в профиле, ни в нижнем ряду
                // до перехода на другой профиль и обратно.
                if (_current != null)
                {
                    SyncLayoutWithLive(_current);
                    _canvas.Live = _live;
                    _canvas.Rebuild();
                }
                RefreshProfileList();
                SelectListItemFor(_current);
                UpdateHeader();
                UpdateProperties();
                return;
            }
            ReloadProfiles(_current != null ? _current.Name : null);
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
                UpdateStatus(Text.NoProfilesHint);
            }
        }

        private void RefreshProfileList()
        {
            _updating = true;
            _profileList.Items.Clear();
            var live = new List<MonitorInfo>(_live.Values);

            foreach (var l in _profiles)
            {
                bool isActive = l.MatchesCurrent(live);

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
                dot.ToolTip = Text.ActiveDotTip;
                dot.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
                titleRow.Children.Add(dot);

                sp.Children.Add(titleRow);

                var sub = new TextBlock();
                sub.Style = (Style)Application.Current.Resources["Caption"];
                sub.Margin = new Thickness(0, 2, 0, 0);
                int on = 0;
                foreach (var d in l.Displays) if (d.Enabled) on++;
                sub.Text = string.IsNullOrEmpty(l.Hotkey)
                    ? Text.MonitorsCount(on)
                    : (l.Hotkey + "   ·   " + Text.MonitorsCount(on));
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

            // Ответ SelectProfile читаем обязательно: если человек передумал в
            // вопросе о несохранённом, перехода не было, и «текущий» — прежний
            // профиль. Прежде пункт молча переименовывал и удалял не ту строку,
            // по которой щёлкнули.
            menu.Items.Add(MenuAction(Text.Rename, "F2", delegate
            {
                if (!SelectProfile(layout)) return;
                BeginRename();
            }));
            menu.Items.Add(MenuAction(Text.MoveUp, "Alt+↑", delegate { MoveProfile(layout, -1); }));
            menu.Items.Add(MenuAction(Text.MoveDown, "Alt+↓", delegate { MoveProfile(layout, +1); }));

            var sep = new Separator();
            sep.Style = (Style)Application.Current.Resources["MenuSeparator"];
            menu.Items.Add(sep);

            menu.Items.Add(MenuAction(Text.Delete, "", delegate
            {
                if (!SelectProfile(layout)) return;
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

            // Выделение остаётся на том профиле, который открыт, а не прыгает на
            // переставленный. Прежде из меню чужой строки список подсвечивал одно,
            // а заголовок, холст и «Сохранить» относились к другому.
            SelectListItemFor(_current);
            Raise(ProfilesChanged);
        }

        private void SelectListItemFor(Layout layout)
        {
            _updating = true;
            foreach (ListBoxItem item in _profileList.Items)
                if (ReferenceEquals(item.Tag, layout)) { _profileList.SelectedItem = item; break; }
            _updating = false;
        }

        /// <summary>
        /// Перейти на профиль. Ложь означает «не перешли»: человек передумал в
        /// вопросе о несохранённом. Ответ обязаны читать все, кто зовёт: следом
        /// за переходом обычно идёт действие над «текущим» профилем, и если
        /// переход не состоялся, действие достанется прежнему. Так пункт
        /// «Переименовать» в меню чужой строки переименовывал не ту.
        /// </summary>
        private bool SelectProfile(Layout layout)
        {
            // Переходить некуда — значит и делать нечего.
            //
            // Прежде эта ветка проваливалась дальше и сбрасывала `_dirty` вместе
            // с `_currentOriginalName`. Пока имя уходило на диск сразу, вреда не
            // было; теперь имя — правка в памяти, и сброс отрывал её от файла.
            // Пункты меню открытого профиля зовут именно `SelectProfile(_current)`:
            // после «Переименовать» окно показывало новое имя и утверждало, что
            // сохранять нечего, а профиль на диске оставался прежним — правка
            // пропадала молча или, наоборот, плодила второй файл.
            if (ReferenceEquals(layout, _current)) return true;

            // Раньше правки сохранялись молча при любом переключении — так профиль можно было
            // перезаписать, сам того не заметив. Теперь решение всегда за пользователем.
            if (_dirty && _current != null)
            {
                // Передумали — возвращаем выделение на прежний профиль, иначе
                // список показывал бы один, а на холсте остался бы другой.
                if (!AskToSave()) { SelectListItemFor(_current); return false; }
            }
            _current = layout;
            _currentOriginalName = layout.Name;
            _dirty = false;
            SyncLayoutWithLive(_current);
            _canvas.Layout = _current;
            // Кого выбрать, решает сам холст: он же следит, чтобы выбранный был
            // всегда — иначе панель свойств внизу оставалась бы пустой.
            _canvas.Rebuild();
            UpdateHeader();
            UpdateProperties();
            UpdateStatus(null);
            SelectListItemFor(layout);
            return true;
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

        /// <summary>
        /// Поставить монитор вплотную справа от раскладки.
        ///
        /// Считаем только по включённым: у выключенных в профиле остаются координаты
        /// с тех времён, когда они были в раскладке, и по ним место находилось далеко
        /// правее — между мониторами оставался разрыв на пустом месте.
        /// </summary>
        private void PlaceRightOfAll(Layout layout, LayoutDisplay d)
        {
            int right = 0;
            foreach (var o in layout.Displays)
            {
                if (ReferenceEquals(o, d) || !o.Enabled) continue;
                right = Math.Max(right, o.X + o.DesktopWidth);
            }
            d.X = right;
            d.Y = 0;
        }

        // ---------------------------------------------------------------- действия
        private void NewFromCurrent()
        {
            ReloadLive();
            var layout = Layout.FromCurrent(Store.UniqueName(Text.NewProfileName));
            Store.Save(layout);
            ReloadProfiles(layout.Name);
            Raise(ProfilesChanged);
            UpdateStatus(Text.ProfileCreated);
            // Просить переименовать и не открыть поле — значит просить впустую:
            // прежде фокус отдавали спрятанному _nameBox, курсор не появлялся
            // нигде, и набранное уходило в никуда.
            BeginRename();
        }

        private void DeleteCurrent()
        {
            if (_current == null) return;
            var dialog = new ConfirmDialog(Text.DeleteTitle, Text.DeleteBody(_current.Name), Text.Delete);
            dialog.Owner = this;
            if (dialog.ShowDialog() != true) return;

            Store.Delete(_current);
            _dirty = false;
            _current = null;
            ReloadProfiles(null);
            Raise(ProfilesChanged);
        }

        /// <summary>
        /// Спросить про несохранённое перед выходом из программы. Ложь — человек
        /// передумал, выходить нельзя. Зовётся из меню значка в трее: там выход
        /// идёт мимо закрытия окна.
        /// </summary>
        public bool AskBeforeClosing()
        {
            if (!_dirty) return true;
            if (!IsVisible) Show();       // спрашивать о профиле, которого не видно, нечестно
            return AskToSave();
        }

        /// <summary>
        /// Спросить про несохранённые правки — молча перезаписывать профиль нельзя.
        /// Возвращает, можно ли продолжать начатое: Esc означает «я передумал»,
        /// и тогда ни переключаться на другой профиль, ни прятать окно не надо.
        /// </summary>
        private bool AskToSave()
        {
            if (_current == null || !_dirty) return true;
            // Вторая кнопка называется «Не сохранять», а не «Отмена». Диалог
            // появляется при закрытии окна и при переходе на другой профиль — там
            // «Отмена» читается как «остаться и ничего не менять», а делала она
            // ровно обратное: молча выбрасывала правки.
            var dialog = new ConfirmDialog(Text.SaveTitle(_current.Name), Text.SaveBody, Text.Save, Text.DontSave);
            dialog.Owner = IsVisible ? this : null;

            bool? answer = dialog.ShowDialog();
            if (answer == null) return false;            // Esc — остаёмся как были
            if (answer == true) SaveCurrent(false);
            else DiscardEdits();
            return true;
        }

        /// <summary>
        /// Вернуть профиль к тому, что лежит на диске.
        ///
        /// Гасить один флаг было мало: правка оставалась в объекте, и программа
        /// считала, что сохранять нечего, показывая при этом изменённое. А кнопка
        /// «Применить» начинается с `SaveCurrent`, то есть **записала бы выброшенную
        /// правку на диск и повернула настоящий монитор**. Выбросить — значит
        /// перечитать, а не забыть.
        /// </summary>
        private void DiscardEdits()
        {
            _dirty = false;
            if (_current == null) return;

            // Читаем свой файл, а не первого попавшегося тёзку по имени: имя внутри
            // файла и имя самого файла могут разойтись, и тогда «Не сохранять»
            // вернуло бы содержимое чужого профиля.
            Layout fresh = null;
            try
            {
                if (!string.IsNullOrEmpty(_current.SourcePath) && System.IO.File.Exists(_current.SourcePath))
                    fresh = Layout.Load(_current.SourcePath);
            }
            catch { }
            if (fresh == null) fresh = Store.LoadByName(_currentOriginalName);
            if (fresh == null) return;   // на диске его ещё нет — возвращать не к чему

            _current.RestoreFrom(fresh);
            _canvas.Rebuild();
            // Список тоже: имя он держит простым текстом, набранным при последней
            // перестройке, — без этого в строке оставалось бы выброшенное имя.
            RefreshProfileList();
            SelectListItemFor(_current);
            UpdateHeader();
            UpdateProperties();
        }

        private void SaveCurrent(bool report)
        {
            if (_current == null) return;
            if (string.IsNullOrEmpty(_current.Name.Trim())) _current.Name = Store.UniqueName(Text.DefaultProfileName);
            _canvas.Normalize();
            Store.Rename(_currentOriginalName, _current);
            _currentOriginalName = _current.Name;
            _dirty = false;
            _saveButton.IsEnabled = false;
            Raise(ProfilesChanged);
            if (report) UpdateStatus(Text.Saved);
        }

        /// <summary>
        /// Применить раскладку. Работа идёт в стороннем потоке, а не здесь.
        ///
        /// Прежде `Applier.Apply` вызывался прямо в потоке окна, а внутри — паузы
        /// на секунды и запасные попытки: окно замирало, Windows вешала на
        /// заголовок «Не отвечает», и никакого «применяю…» при этом не было.
        /// Человек решал, что промазал мимо кнопки, и жал ещё раз-другой —
        /// нажатия копились и после разморозки перестраивали дисплеи подряд.
        /// Это ровно та нагрузка, от которой 19.08.2026 машина ушла в синий
        /// экран, и единственной защитой от неё оставался затвор в 1,2 с.
        /// </summary>
        private void ApplyCurrent()
        {
            if (_current == null) return;

            // Наложенную раскладку Windows не примет — она расставит экраны
            // по-своему, а профиль останется с невозможными координатами. Прежде
            // про наложение только писали внизу, а применяли всё равно.
            //
            // Проверка стоит до признака занятости: занимать его ради отказа
            // незачем, а выход отсюда без освобождения погасил бы кнопку навсегда.
            string overlap = _canvas.OverlapProblem();
            if (overlap != null) { UpdateStatus(overlap); return; }

            // Признак занятости спрашиваем до записи на диск: иначе на «подождите»
            // профиль уже сохранён и признак несохранённого погашен, хотя фраза
            // обещает, что ничего не случилось.
            if (!ApplyBusy.Begin())
            {
                UpdateStatus(Text.BusyWait);
                return;
            }

            SaveCurrent(false);

            // В поток уходит копия, а не живой профиль. Окно больше не заперто:
            // пока перестроение идёт, человек может двигать мониторы, а
            // «Не сохранять» вызывает Displays.Clear() — прямо под руками у
            // Applier, который этот же список перебирает. В лучшем случае на
            // экраны уехала бы раскладка, которой нет ни на диске, ни на холсте;
            // в худшем — исключение в потоке пула, где его никто не ловит, то
            // есть тихая смерть процесса посреди перестроения дисплеев.
            Layout layout = _current.DeepCopy();
            _applyButton.IsEnabled = false;
            UpdateStatus(Text.Rebuilding);

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                ApplyResult result;
                // Перехват обязателен: это поток пула, и вылетевшее отсюда
                // исключение уносит весь процесс — вместе со значком в трее и
                // горячими клавишами, посреди перестроения дисплеев.
                try { result = Applier.Apply(layout); }
                catch (Exception ex)
                {
                    result = new ApplyResult();
                    result.Error = ex.Message;
                }
                finally { ApplyBusy.End(); }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _applyButton.IsEnabled = _current != null;

                    ReloadLive();
                    _canvas.Refresh();
                    RefreshProfileList();

                    // Спрятанное окно результат не покажет: полоса внизу останется
                    // в невидимом окне, а следующее открытие начнётся с чистой.
                    // Поэтому там, где сказать некому, зовём того, у кого есть трей.
                    var parts = new List<string>();
                    // Имя профиля в строке обязательно: пока шло перестроение, человек
                    // мог перейти на другой профиль, и «Раскладка применена» относилась
                    // бы к тому, которого на экране уже нет.
                    if (result.Success) parts.Add(Text.ApplyDone(layout.Name));
                    else parts.Add(Text.ApplyFailedShort(layout.Name, result.Error));
                    foreach (string w in result.Warnings) parts.Add(w);
                    string text = string.Join("  ", parts.ToArray());
                    UpdateStatus(text);

                    // В подсказку трея — только первое предложение и счёт остальных:
                    // она живёт несколько секунд, обрезается на 255 знаках и молча,
                    // а весь набор предупреждений за 255 уходит легко.
                    if (!IsVisible)
                    {
                        string brief = parts[0];
                        if (parts.Count > 1) brief += Text.MoreNotes(parts.Count - 1);
                        Raise(ApplyReported, brief, !result.Success);
                    }
                }));
            });
        }

        private static void Raise(Action a) { if (a != null) a(); }

        private static void Raise(Action<string, bool> a, string text, bool alarming)
        {
            if (a != null) a(text, alarming);
        }

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

        private void CommitRename(bool keep) { CommitRename(keep, false); }

        private void CommitRename(bool keep, bool fromFocusLoss)
        {
            if (!_renaming) return;

            if (keep && _current != null)
            {
                string name = _nameBox.Text.Trim();
                if (name.Length > 0 && name != _current.Name)
                {
                    // Имя, уже занятое другим профилем, не берём. Прежде брали:
                    // Store.Rename удалял свой файл и писал поверх чужого — тёзка
                    // исчезал молча, вместе со всей раскладкой.
                    //
                    // Поле при этом остаётся открытым: закрыть его и потерять
                    // набранное — значит заставить набирать всё заново, и уже
                    // не видя перед собой того, что не подошло.
                    string clash = Store.NameInUse(name, _currentOriginalName);
                    if (clash != null)
                    {
                        UpdateStatus(fromFocusLoss
                            ? Text.NameUnchanged(clash)
                            : Text.NameTaken(clash));

                        // Фокус возвращаем только если правку подтвердили сами —
                        // Enter. По уходу мышью не держим: тянуть фокус обратно
                        // из обработчика его потери значит не выпускать человека
                        // из поля, пока он не угадает имя.
                        if (!fromFocusLoss) { _nameBox.Focus(); _nameBox.SelectAll(); return; }
                    }
                    else
                    {
                        // Имя — такая же правка профиля, как всё остальное, и на
                        // диск уходит вместе со всем остальным. Прежде здесь стоял
                        // SaveCurrent: переименование молча записывало заодно и
                        // несохранённую раскладку — без вопроса, который программа
                        // обязательно задаёт в других местах.
                        _current.Name = name;
                        MarkDirty();
                        RefreshProfileList();
                        SelectListItemFor(_current);
                    }
                }
            }

            _renaming = false;
            _nameBox.Visibility = Visibility.Collapsed;
            _nameText.Visibility = Visibility.Visible;
            UpdateHeader();
        }

        /// <summary>
        /// Написать в полосу внизу итог применения, начатого не отсюда — горячей
        /// клавишей или из меню значка. Без этого полного разбора не было нигде:
        /// в подсказку трея он не помещается, а в окно не попадал вовсе, и совет
        /// «откройте окно» вёл в пустоту.
        /// </summary>
        public void ShowApplyResult(string profileName, ApplyResult result)
        {
            var parts = new List<string>();
            if (result.Success) parts.Add(Text.ApplyDone(profileName));
            else parts.Add(Text.ApplyFailedShort(profileName, result.Error));
            foreach (string w in result.Warnings) parts.Add(w);
            UpdateStatus(string.Join("  ", parts.ToArray()));
        }

        /// <summary>Закрыть поле имени, ничего не меняя.</summary>
        private void CancelRename()
        {
            if (!_renaming) return;
            _renaming = false;
            _nameBox.Visibility = Visibility.Collapsed;
            _nameText.Visibility = Visibility.Visible;
            UpdateHeader();
        }

        /// <summary>Окно «О программе». Открывается и отсюда, и из меню значка в трее.</summary>
        public void ShowAbout()
        {
            var window = new AboutWindow();
            if (IsVisible) window.Owner = this;
            window.ShowDialog();
        }

#if DEV
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

            string count = on > 0 ? on.ToString(CultureInfo.InvariantCulture) : "";
            string words = on > 0 ? Text.TaskbarButtonN(on) : Text.TaskbarButton;

            // Подпись словами, пока она помещается. Прежде на кнопке всегда
            // стояло одно число: сжатие делалось ради узкого окна, а действовало
            // при любой ширине — и в широком окне «4» рядом с «F16» не говорило
            // ничего, пока между кнопкой и названием профиля пустовала половина
            // шапки.
            //
            // Помещается ли — не порог в пикселях, а мерка: ряд кнопок просят
            // назвать свою ширину и складывают с названием, ужатым до предела.
            // Так подпись остаётся словами и по-английски, где она короче, и
            // сжимается там, где в сочетании клавиш выросло «Ctrl+Shift+F12».
            bool compact = false;
            _taskbarButton.Content = Glyph(TaskbarGlyph, words);
            if (_headerGrid != null && _headerGrid.ActualWidth > 0)
            {
                _headerActions.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                compact = _headerActions.DesiredSize.Width + _nameText.MinWidth > _headerGrid.ActualWidth;
                if (compact) _taskbarButton.Content = Glyph(TaskbarGlyph, count);
            }

            // В подсказке — то, чего нет на кнопке. Пока подпись словами,
            // повторять её незачем; сжатую до числа — наоборот, надо назвать.
            _taskbarButton.ToolTip = (ExplorerTap.Available ? Text.TaskbarTip : Text.TaskbarMissing)
                                     + (compact ? Environment.NewLine + words : "");
        }
#endif

        private void UpdateHeader()
        {
            // Захват сочетания живёт ровно до следующей перерисовки заголовка.
            //
            // Прежде флаг гасился только по Esc, Delete и удачному сочетанию, а
            // кнопке возвращали обычную надпись отсюда — из любого места, где
            // меняется профиль. Оба признака захвата исчезали, сам захват
            // оставался: окно переставало слышать клавиатуру вовсе, а первая же
            // нажатая клавиша уезжала горячей в чужой профиль и сразу на диск.
            _capturingHotkey = false;

            _updating = true;
            _nameText.Text = _current != null ? _current.Name : "";
            _nameHint.Text = DescribeCurrent();
            _applyButton.IsEnabled = _current != null && !ApplyBusy.Active;
#if DEV
            UpdateTaskbarButton();
#endif
            _saveButton.IsEnabled = _current != null && _dirty;
            _hotkeyButton.IsEnabled = _current != null;
            if (_moreButton != null) _moreButton.IsEnabled = _current != null;
            _hotkeyButton.Content = Glyph("", _current != null && !string.IsNullOrEmpty(_current.Hotkey)
                ? _current.Hotkey : Text.HotkeyButton);
            _updating = false;
        }

        /// <summary>Короткая строка под названием профиля: что в нём и чем переключается.</summary>
        private string DescribeCurrent()
        {
            if (_current == null) return "";

            int on = 0;
            foreach (var d in _current.Displays) if (d.Enabled) on++;

            var parts = new List<string>();
            parts.Add(on == 1 ? Text.OneMonitor : Text.MonitorsCount(on));
            if (!string.IsNullOrEmpty(_current.Hotkey)) parts.Add(Text.KeyLabel(_current.Hotkey));
#if DEV
            if (_current.Taskbar != null && _current.Taskbar.AnyEnabled) parts.Add(Text.OwnTaskbarLook);
#endif

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
                _monitorTitle.Text = Text.PickMonitor;
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
                ? (live.ConnectionType + " · " + (live.Active ? Text.NowOn : Text.NowOff))
                : Text.NowUnplugged;
            if (d.Enabled && d.CloneGroup >= 0) subtitle += " · " + Text.NoHdrInClone;
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
            // Со стороной, а не голыми градусами: «270°» не говорит человеку,
            // куда повернётся экран, а проверять это, перевернув рабочий стол, —
            // дорогое удовольствие.
            rotItems.Add(new Item(Text.RotPlain, Native.DISPLAYCONFIG_ROTATION_IDENTITY));
            rotItems.Add(new Item(Text.Rot90, Native.DISPLAYCONFIG_ROTATION_ROTATE90));
            rotItems.Add(new Item(Text.Rot180, Native.DISPLAYCONFIG_ROTATION_ROTATE180));
            rotItems.Add(new Item(Text.Rot270, Native.DISPLAYCONFIG_ROTATION_ROTATE270));
            _rotCombo.ItemsSource = rotItems;
            foreach (Item it in rotItems) if ((uint)it.Value == d.Rotation) _rotCombo.SelectedItem = it;

            var cloneItems = new List<Item>();
            cloneItems.Add(new Item(Text.CloneSeparate, null));
            foreach (var other in _current.Displays)
            {
                if (ReferenceEquals(other, d) || !other.Enabled) continue;
                cloneItems.Add(new Item(Text.CloneDup(other.Name), other));
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
            hdrItems.Add(new Item(Text.HdrLeave, -1));
            hdrItems.Add(new Item(Text.HdrOn, 1));
            hdrItems.Add(new Item(Text.HdrOff, 0));
            _hdrCombo.ItemsSource = hdrItems;
            foreach (Item it in hdrItems) if ((int)it.Value == d.Hdr) _hdrCombo.SelectedItem = it;

            // Windows не предоставляет расширенный цвет выходам, объединённым в дублирование —
            // проверено на LG UltraGear + Q80A и на 60, и на 120 Гц
            bool cloned = d.CloneGroup >= 0;
            // Подсказки про недоступность HDR в дубле здесь нет: в этом случае поле
            // не показывается вовсе, а причина написана строкой под названием монитора.
            _hdrCombo.IsEnabled = d.Enabled && !cloned;

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
            foreach (double hz in rates) items.Add(new Item(hz.ToString("0.##") + " " + Text.Hz, hz));
            _hzCombo.ItemsSource = items;
            foreach (Item it in items) if (Math.Abs((double)it.Value - d.Hz) < 0.6) { _hzCombo.SelectedItem = it; break; }
            if (_hzCombo.SelectedItem == null && items.Count > 0) _hzCombo.SelectedItem = items[0];
        }

        /// <param name="keepPosition">
        /// Монитор перетащили на холст — координаты уже выбраны пользователем, и
        /// подбирать их заново не нужно.
        /// </param>
        private void OnEnabledChanged(bool enabled, bool keepPosition)
        {
            if (_updating || _canvas.Selected == null) return;
            var d = _canvas.Selected;
            d.Enabled = enabled;
            if (enabled)
            {
                // Наложение проверяем всегда, даже когда место выбрал пользователь:
                // уронить монитор поверх соседа легко, а рабочие столы наложенными
                // быть не могут — Windows такую раскладку не примет.
                bool overlaps = false;
                foreach (var o in _current.Displays)
                {
                    if (ReferenceEquals(o, d) || !o.Enabled) continue;
                    if (d.X < o.X + o.DesktopWidth && o.X < d.X + d.DesktopWidth &&
                        d.Y < o.Y + o.DesktopHeight && o.Y < d.Y + d.DesktopHeight) { overlaps = true; break; }
                }
                if (overlaps || (!keepPosition && d.X == 0 && d.Y == 0 && AnyOtherAtOrigin(d)))
                    PlaceRightOfAll(_current, d);
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
            if (!primary)
            {
                // Основной обязан быть ровно один, поэтому снять галочку нельзя —
                // но раньше она просто прыгала обратно, и переключатель выглядел
                // заедающим. Сказать, что делать вместо этого, стоит одной строки.
                _updating = true; _primarySwitch.IsChecked = true; _updating = false;
                UpdateStatus(Text.PrimaryOnlyOne);
                return;
            }
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
                    UpdateStatus(Text.CloneModesDiffer);
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
                UpdateStatus(Text.HdrNotInClone);
        }

        private void MarkDirty()
        {
            _dirty = true;
            _saveButton.IsEnabled = true;
        }

        /// <summary>
        /// Строка внизу окна. Сама по себе она молчит: о том, какая раскладка применена,
        /// говорит точка в списке слева, а имя профиля и его состав видны в заголовке
        /// и на холсте. Здесь остаются только помехи и ответы на действия.
        /// </summary>
        private void UpdateStatus(string text)
        {
            if (text == null && _current != null) text = _canvas.Validate();

            _status.Text = text ?? "";
            _status.ToolTip = string.IsNullOrEmpty(_status.Text) ? null : _status.Text;
            _statusBar.Visibility = string.IsNullOrEmpty(_status.Text)
                ? Visibility.Collapsed : Visibility.Visible;
        }
        // ---------------------------------------------------------------- горячая клавиша
        private void BeginHotkeyCapture()
        {
            if (_current == null) return;
            _capturingHotkey = true;
            _hotkeyButton.Content = Glyph("", Text.PressCombo);
            UpdateStatus(Text.PressComboHint);
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
                UpdateStatus(Text.ComboRemoved);
                return;
            }
            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;

            string combo = Hotkey.Describe(Keyboard.Modifiers, key);
            if (combo == null) { UpdateStatus(Text.ComboImpossible); return; }

            // Проверяем **до** записи. Прежде сочетание сохранялось сразу, а
            // разбор шёл после и молча: одиночная буква уезжала в профиль, и
            // программа забирала её у всей системы, ничего об этом не сказав.
            uint mods, vk; string err;
            if (!Hotkey.TryParse(combo, out mods, out vk, out err))
            {
                UpdateStatus(Text.ComboRejected(combo, err));
                return;
            }

            // Одно сочетание на два профиля назначалось бодрым «назначено», а
            // потом всплывала подсказка «занято другой программой»: никакой
            // другой программы нет, занято своим же профилем, и какой из двух
            // остался рабочим — из окна не видно. Ищем тёзку заранее.
            foreach (var other in _profiles)
            {
                if (ReferenceEquals(other, _current)) continue;
                if (!string.Equals(other.Hotkey, combo, StringComparison.OrdinalIgnoreCase)) continue;
                UpdateStatus(Text.ComboBusy(combo, other.Name));
                return;
            }

            _capturingHotkey = false;
            _current.Hotkey = combo;
            MarkDirty();
            SaveCurrent(false);
            UpdateHeader();
            RefreshProfileList();
            // Сочетание работает, только пока программа запущена, а автозапуск по
            // умолчанию выключен и спрятан в меню значка. Молчать об этом нельзя:
            // человек назначает клавишу, перезагружается — и она не работает, а
            // окно по-прежнему уверяет, что всё назначено. Ради этой клавиши
            // программа и существует.
            UpdateStatus(Text.ComboSet(combo, _current.Name) +
                (App.AutoStartEnabled ? "" : Text.ComboNeedsAutostart));
        }
    }
}
