using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DIIIX
{
    /// <summary>Холст раскладки: мониторы перетаскиваются мышью и прилипают друг к другу.</summary>
    internal class MonitorCanvas : Canvas
    {
        public Layout Layout;
        public Dictionary<string, MonitorInfo> Live = new Dictionary<string, MonitorInfo>();
        public LayoutDisplay Selected;

        public event Action SelectionChanged;
        public event Action LayoutChanged;

        private readonly Dictionary<LayoutDisplay, Border> _cards = new Dictionary<LayoutDisplay, Border>();
        private double _scale = 0.1;
        private double _offX, _offY;

        private LayoutDisplay _drag;
        private Point _dragStart;
        private readonly Dictionary<LayoutDisplay, Point> _dragOrigin = new Dictionary<LayoutDisplay, Point>();
        private bool _moved;

        public MonitorCanvas()
        {
            Background = Brushes.Transparent;
            ClipToBounds = true;
            SizeChanged += delegate { Relayout(); };
            MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (e.OriginalSource == this) { Selected = null; Refresh(); Raise(SelectionChanged); }
            };
        }

        private static void Raise(Action a) { if (a != null) a(); }

        public void Rebuild()
        {
            Children.Clear();
            _cards.Clear();
            if (Layout == null) return;

            _dockLabel.Text = "НЕ ИСПОЛЬЗУЮТСЯ В ЭТОМ ПРОФИЛЕ";
            _dockLabel.Style = (Style)Application.Current.Resources["SectionHeader"];
            Children.Add(_dockLabel);

            foreach (var d in Layout.Displays)
            {
                var card = BuildCard(d);
                _cards[d] = card;
                Children.Add(card);
            }
            Relayout();
        }

        private Border BuildCard(LayoutDisplay d)
        {
            var border = new Border();
            border.CornerRadius = new CornerRadius(6);
            border.BorderThickness = new Thickness(1);
            border.Cursor = Cursors.SizeAll;
            border.Tag = d;

            var stack = new StackPanel();
            stack.VerticalAlignment = VerticalAlignment.Center;
            stack.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Margin = new Thickness(8, 4, 8, 4);

            var title = new TextBlock();
            title.Text = d.Name;
            title.FontWeight = FontWeights.SemiBold;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.TextAlignment = TextAlignment.Center;
            title.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(title);

            var info = new TextBlock();
            info.Style = (Style)FindResource("Caption");
            info.TextAlignment = TextAlignment.Center;
            info.HorizontalAlignment = HorizontalAlignment.Center;
            info.Margin = new Thickness(0, 2, 0, 0);
            stack.Children.Add(info);

            var badges = new StackPanel();
            badges.Orientation = Orientation.Horizontal;
            badges.HorizontalAlignment = HorizontalAlignment.Center;
            badges.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(badges);

            border.Child = stack;
            border.MouseLeftButtonDown += CardMouseDown;
            border.MouseMove += CardMouseMove;
            border.MouseLeftButtonUp += CardMouseUp;
            return border;
        }

        private void StyleCard(LayoutDisplay d, Border card)
        {
            var stack = (StackPanel)card.Child;
            var title = (TextBlock)stack.Children[0];
            var info = (TextBlock)stack.Children[1];
            var badges = (StackPanel)stack.Children[2];

            bool live = Live.ContainsKey(d.Identity);
            bool selected = ReferenceEquals(d, Selected);

            title.Text = d.Name;
            title.Foreground = (Brush)FindResource(d.Enabled ? "Text" : "TextSec");

            string res = d.Width + " × " + d.Height;
            if (d.Hz > 0) res += "  ·  " + d.Hz.ToString("0.##") + " Гц";
            if (!live) res = "не подключён";
            info.Text = res;

            badges.Children.Clear();
            if (d.Enabled && d.Primary) badges.Children.Add(Badge("Основной", true));
            if (d.Enabled && d.CloneGroup >= 0) badges.Children.Add(Badge("Дубль", false));
            if (d.Enabled && d.Hdr == 1 && d.CloneGroup < 0) badges.Children.Add(Badge("HDR", false));
            if (!live) badges.Children.Add(Badge("нет сигнала", false));

            card.Background = (Brush)FindResource(d.Enabled ? "Screen" : "ScreenOff");
            card.Opacity = d.Enabled ? 1.0 : 0.55;
            card.BorderBrush = selected ? (Brush)FindResource("Accent") : (Brush)FindResource("StrokeStrong");
            card.BorderThickness = new Thickness(selected ? 2 : 1);
        }

        private UIElement Badge(string text, bool accent)
        {
            var b = new Border();
            b.CornerRadius = new CornerRadius(4);
            b.Padding = new Thickness(6, 1, 6, 2);
            b.Margin = new Thickness(0, 0, 4, 0);
            b.Background = accent ? (Brush)FindResource("Accent") : (Brush)FindResource("Hover");
            var t = new TextBlock();
            t.Text = text;
            t.FontSize = 11;
            t.Foreground = accent ? (Brush)FindResource("AccentText") : (Brush)FindResource("TextSec");
            b.Child = t;
            return b;
        }

        /// <summary>Пересчитать масштаб и разложить карточки.</summary>
        public void Relayout()
        {
            if (Layout == null || Layout.Displays.Count == 0) return;
            if (ActualWidth < 40 || ActualHeight < 40) return;

            var enabled = new List<LayoutDisplay>();
            var disabled = new List<LayoutDisplay>();
            foreach (var d in Layout.Displays) { if (d.Enabled) enabled.Add(d); else disabled.Add(d); }

            double dockHeight = disabled.Count > 0 ? DockHeight + 26 : 0;
            _dockLabel.Visibility = disabled.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            double pad = 26;
            double areaH = Math.Max(60, ActualHeight - dockHeight);

            if (enabled.Count > 0)
            {
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                foreach (var d in enabled)
                {
                    minX = Math.Min(minX, d.X);
                    minY = Math.Min(minY, d.Y);
                    maxX = Math.Max(maxX, d.X + d.DesktopWidth);
                    maxY = Math.Max(maxY, d.Y + d.DesktopHeight);
                }
                double w = Math.Max(1, maxX - minX), h = Math.Max(1, maxY - minY);
                _scale = Math.Min((ActualWidth - pad * 2) / w, (areaH - pad * 2) / h);
                if (_scale <= 0 || double.IsInfinity(_scale) || double.IsNaN(_scale)) _scale = 0.05;
                _scale = Math.Min(_scale, 0.35);
                _offX = pad + (ActualWidth - pad * 2 - w * _scale) / 2 - minX * _scale;
                _offY = pad + (areaH - pad * 2 - h * _scale) / 2 - minY * _scale;
            }

            // выключенные складываем в отдельный ряд внизу — иначе они прячутся под включёнными
            _dockSlots.Clear();
            double x = pad;
            double y = ActualHeight - DockHeight - 12;
            SetLeft(_dockLabel, pad);
            SetTop(_dockLabel, y - 22);
            foreach (var d in disabled)
            {
                if (x + DockWidth > ActualWidth - pad) { x = pad; }
                _dockSlots[d] = new Point(x, y);
                x += DockWidth + 10;
            }

            Refresh();
        }

        private const double DockWidth = 150;
        private const double DockHeight = 84;
        private readonly Dictionary<LayoutDisplay, Point> _dockSlots = new Dictionary<LayoutDisplay, Point>();
        private readonly TextBlock _dockLabel = new TextBlock();

        /// <summary>Обновить позиции и оформление без пересоздания.</summary>
        public void Refresh()
        {
            if (Layout == null) return;
            foreach (var d in Layout.Displays)
            {
                Border card;
                if (!_cards.TryGetValue(d, out card)) continue;

                if (d.Enabled)
                {
                    card.Width = Math.Max(56, d.DesktopWidth * _scale);
                    card.Height = Math.Max(38, d.DesktopHeight * _scale);
                    double extra = (d.CloneGroup >= 0 ? CloneOffset(d) : 0);
                    SetLeft(card, _offX + d.X * _scale + extra);
                    SetTop(card, _offY + d.Y * _scale + extra);
                    card.Cursor = Cursors.SizeAll;
                }
                else
                {
                    Point slot;
                    if (!_dockSlots.TryGetValue(d, out slot)) slot = new Point(-500, -500);
                    card.Width = DockWidth;
                    card.Height = DockHeight;
                    SetLeft(card, slot.X);
                    SetTop(card, slot.Y);
                    card.Cursor = Cursors.Hand;
                }
                Panel.SetZIndex(card, ReferenceEquals(d, Selected) ? 100 : (d.Enabled ? 10 : 5));
                StyleCard(d, card);
            }
        }

        /// <summary>Клоны рисуем стопкой со сдвигом, чтобы было видно, что их несколько.</summary>
        private double CloneOffset(LayoutDisplay d)
        {
            int index = 0;
            foreach (var other in Layout.Displays)
            {
                if (ReferenceEquals(other, d)) break;
                if (other.Enabled && other.CloneGroup == d.CloneGroup) index++;
            }
            return index * 10;
        }

        // ---------------- перетаскивание ----------------
        private void CardMouseDown(object sender, MouseButtonEventArgs e)
        {
            var card = (Border)sender;
            var d = (LayoutDisplay)card.Tag;
            Selected = d;
            Raise(SelectionChanged);
            Refresh();

            if (!d.Enabled) { e.Handled = true; return; }   // выключенные лежат в нижнем ряду, их не двигают

            _drag = d;
            _moved = false;
            _dragStart = e.GetPosition(this);
            _dragOrigin.Clear();
            foreach (var other in Layout.Displays)
                if (MovesWith(d, other)) _dragOrigin[other] = new Point(other.X, other.Y);

            card.CaptureMouse();
            e.Handled = true;
        }

        private bool MovesWith(LayoutDisplay dragged, LayoutDisplay other)
        {
            if (ReferenceEquals(dragged, other)) return true;
            return dragged.CloneGroup >= 0 && other.CloneGroup == dragged.CloneGroup;
        }

        private void CardMouseMove(object sender, MouseEventArgs e)
        {
            if (_drag == null || e.LeftButton != MouseButtonState.Pressed) return;
            Point p = e.GetPosition(this);
            double ddx = (p.X - _dragStart.X) / _scale;
            double ddy = (p.Y - _dragStart.Y) / _scale;
            if (!_moved && Math.Abs(p.X - _dragStart.X) < 3 && Math.Abs(p.Y - _dragStart.Y) < 3) return;
            _moved = true;

            Point origin = _dragOrigin[_drag];
            int nx = (int)Math.Round(origin.X + ddx);
            int ny = (int)Math.Round(origin.Y + ddy);
            Snap(_drag, ref nx, ref ny);

            int shiftX = nx - (int)origin.X, shiftY = ny - (int)origin.Y;
            foreach (var kv in _dragOrigin)
            {
                kv.Key.X = (int)kv.Value.X + shiftX;
                kv.Key.Y = (int)kv.Value.Y + shiftY;
            }
            Refresh();
        }

        private void CardMouseUp(object sender, MouseButtonEventArgs e)
        {
            var card = (Border)sender;
            card.ReleaseMouseCapture();
            if (_drag != null && _moved)
            {
                Normalize();
                Relayout();
                Raise(LayoutChanged);
            }
            _drag = null;
        }

        /// <summary>Прилипание к краям соседей — как в параметрах экрана Windows, только честнее.</summary>
        private void Snap(LayoutDisplay d, ref int nx, ref int ny)
        {
            double threshold = 18 / Math.Max(_scale, 0.0001);
            int w = d.DesktopWidth, h = d.DesktopHeight;
            double bestX = threshold, bestY = threshold;
            int snapX = nx, snapY = ny;

            foreach (var o in Layout.Displays)
            {
                if (MovesWith(d, o) || !o.Enabled) continue;
                int ox = o.X, oy = o.Y, ow = o.DesktopWidth, oh = o.DesktopHeight;

                TrySnap(ref bestX, ref snapX, nx, ox + ow, threshold);      // справа от соседа
                TrySnap(ref bestX, ref snapX, nx, ox - w, threshold);       // слева от соседа
                TrySnap(ref bestX, ref snapX, nx, ox, threshold);           // выравнивание по левому краю
                TrySnap(ref bestX, ref snapX, nx, ox + ow - w, threshold);  // по правому краю
                TrySnap(ref bestX, ref snapX, nx, ox + (ow - w) / 2, threshold); // по центру

                TrySnap(ref bestY, ref snapY, ny, oy + oh, threshold);
                TrySnap(ref bestY, ref snapY, ny, oy - h, threshold);
                TrySnap(ref bestY, ref snapY, ny, oy, threshold);
                TrySnap(ref bestY, ref snapY, ny, oy + oh - h, threshold);
                TrySnap(ref bestY, ref snapY, ny, oy + (oh - h) / 2, threshold);
            }
            nx = snapX; ny = snapY;
        }

        private static void TrySnap(ref double best, ref int result, int value, int candidate, double threshold)
        {
            double dist = Math.Abs(value - candidate);
            if (dist <= threshold && dist < best) { best = dist; result = candidate; }
        }

        /// <summary>Основной монитор всегда в (0,0) — этого требует Windows.</summary>
        public void Normalize()
        {
            if (Layout == null) return;
            LayoutDisplay primary = null;
            foreach (var d in Layout.Displays) if (d.Enabled && d.Primary) { primary = d; break; }
            if (primary == null)
                foreach (var d in Layout.Displays) if (d.Enabled) { primary = d; primary.Primary = true; break; }
            if (primary == null) return;

            int dx = -primary.X, dy = -primary.Y;
            if (dx == 0 && dy == 0) return;
            foreach (var d in Layout.Displays) { d.X += dx; d.Y += dy; }
        }

        /// <summary>Проверка на разрывы: рабочий стол должен быть единым.</summary>
        public string Validate()
        {
            var enabled = new List<LayoutDisplay>();
            foreach (var d in Layout.Displays) if (d.Enabled) enabled.Add(d);
            if (enabled.Count == 0) return "Не выбран ни один монитор.";
            if (enabled.Count == 1) return null;

            var reached = new List<LayoutDisplay>();
            reached.Add(enabled[0]);
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var d in enabled)
                {
                    if (reached.Contains(d)) continue;
                    foreach (var r in reached)
                    {
                        if (Touches(d, r)) { reached.Add(d); grew = true; break; }
                    }
                    if (grew) break;
                }
            }
            if (reached.Count < enabled.Count)
                return "Между мониторами есть разрыв — придвиньте их вплотную, иначе Windows сдвинет их сама.";
            return null;
        }

        private static bool Touches(LayoutDisplay a, LayoutDisplay b)
        {
            if (a.CloneGroup >= 0 && a.CloneGroup == b.CloneGroup) return true;
            int ax1 = a.X, ay1 = a.Y, ax2 = a.X + a.DesktopWidth, ay2 = a.Y + a.DesktopHeight;
            int bx1 = b.X, by1 = b.Y, bx2 = b.X + b.DesktopWidth, by2 = b.Y + b.DesktopHeight;
            bool overlapX = ax1 < bx2 && bx1 < ax2;
            bool overlapY = ay1 < by2 && by1 < ay2;
            if (overlapX && overlapY) return true;                 // перекрываются
            if (overlapX && (ay2 == by1 || by2 == ay1)) return true;
            if (overlapY && (ax2 == bx1 || bx2 == ax1)) return true;
            return false;
        }
    }
}
