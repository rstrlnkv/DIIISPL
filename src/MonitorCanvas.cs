// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DIIISPL
{
    /// <summary>Layout canvas: monitors are dragged with the mouse and snap to each other.</summary>
    internal class MonitorCanvas : Canvas
    {
        public Layout Layout;
        public Dictionary<string, MonitorInfo> Live = new Dictionary<string, MonitorInfo>();
        public LayoutDisplay Selected;

        public event Action SelectionChanged;
        public event Action LayoutChanged;

        /// <summary>
        /// A monitor from the bottom row was dragged into the layout. Its coordinates are already set —
        /// all that is left is to enable it in the profile.
        /// </summary>
        public event Action DropRequested;

        private readonly Dictionary<LayoutDisplay, Border> _cards = new Dictionary<LayoutDisplay, Border>();
        private double _scale = 0.1;
        private double _offX, _offY;

        private LayoutDisplay _drag;
        private Point _dragStart;
        private readonly Dictionary<LayoutDisplay, Point> _dragOrigin = new Dictionary<LayoutDisplay, Point>();
        private bool _moved;
        private bool _fromDock;      // a monitor is being dragged from the bottom row, not within the layout
        private Point _grab;         // the point of the card that was grabbed

        public MonitorCanvas()
        {
            Background = Brushes.Transparent;
            ClipToBounds = true;
            SizeChanged += delegate { Relayout(); };
            // A click past the cards clears nothing. It used to leave the canvas
            // with nothing selected, and the properties panel below turned into a row of grey
            // fields captioned "Select a monitor" — a state in which the window
            // shows nothing and lets you do nothing.
        }

        /// <summary>
        /// Someone must always be selected: otherwise the properties panel has nothing
        /// to show. Called on every rebuild — the profile may have changed, the
        /// monitor may have dropped out of the list, and the reference would be left dangling.
        /// </summary>
        private bool EnsureSelection()
        {
            LayoutDisplay was = Selected;
            if (Layout == null || Layout.Displays.Count == 0) Selected = null;
            else if (Selected == null || !Layout.Displays.Contains(Selected))
            {
                Selected = null;
                foreach (var d in Layout.Displays) if (d.Enabled && d.Primary) { Selected = d; break; }
                if (Selected == null)
                    foreach (var d in Layout.Displays) if (d.Enabled) { Selected = d; break; }
                if (Selected == null) Selected = Layout.Displays[0];
            }
            return !ReferenceEquals(was, Selected);
        }

        private static void Raise(Action a) { if (a != null) a(); }

        public void Rebuild()
        {
            Children.Clear();
            _cards.Clear();
            bool changed = EnsureSelection();
            if (Layout == null) { if (changed) Raise(SelectionChanged); return; }

            // The strip is broken by a caption: two segments at the sides and the text between them.
            foreach (Border line in new[] { _dockLineLeft, _dockLineRight })
            {
                line.Height = 1;
                line.Background = (Brush)Application.Current.Resources["Stroke"];
                Children.Add(line);
            }

            _dockLabel.Text = Text.NotInProfile;
            _dockLabel.Style = (Style)Application.Current.Resources["SectionHeader"];
            Children.Add(_dockLabel);

            foreach (var d in Layout.Displays)
            {
                var card = BuildCard(d);
                _cards[d] = card;
                Children.Add(card);
            }
            Relayout();

            // After the rebuild, not before: the handler will go read the properties of
            // the selected one, and the cards must already be in place by then.
            if (changed) Raise(SelectionChanged);
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
            if (d.Hz > 0) res += "  ·  " + d.Hz.ToString("0.##") + " " + Text.Hz;
            if (!live) res = Text.NotConnected;
            info.Text = res;

            badges.Children.Clear();
            if (d.Enabled && d.Primary) badges.Children.Add(Badge(Text.BadgePrimary, true));
            if (d.Enabled && d.CloneGroup >= 0) badges.Children.Add(Badge(Text.BadgeClone, false));
            if (d.Enabled && d.Hdr == 1 && d.CloneGroup < 0) badges.Children.Add(Badge("HDR", false));
            // No "no signal" label here: the line above already says "not connected",
            // and repeating the same thing in other words in one card is pointless.

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

        /// <summary>Recompute the scale and lay out the cards.</summary>
        public void Relayout()
        {
            if (Layout == null || Layout.Displays.Count == 0) return;
            if (ActualWidth < 40 || ActualHeight < 40) return;

            var enabled = new List<LayoutDisplay>();
            var disabled = new List<LayoutDisplay>();
            foreach (var d in Layout.Displays) { if (d.Enabled) enabled.Add(d); else disabled.Add(d); }

            double dockHeight = disabled.Count > 0 ? DockHeight + 56 : 0;
            Visibility dockVisible = disabled.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            _dockLabel.Visibility = dockVisible;
            _dockLineLeft.Visibility = dockVisible;
            _dockLineRight.Visibility = dockVisible;

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

            // Disabled ones go in a separate row at the bottom — otherwise they hide under
            // the enabled ones. The row is centred, with a line and a heading above it: so it is clear that this is
            // not part of the layout but a reserve.
            _dockSlots.Clear();
            double y = ActualHeight - DockHeight - 14;
            _dockTop = y - 44;

            // The caption in the middle, the strip as two segments at its edges.
            _dockLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double textW = _dockLabel.DesiredSize.Width;
            double textH = _dockLabel.DesiredSize.Height;
            double center = ActualWidth / 2;
            double gap = 14;

            SetLeft(_dockLabel, center - textW / 2);
            SetTop(_dockLabel, _dockTop);

            double lineY = _dockTop + textH / 2;
            double leftEnd = center - textW / 2 - gap;
            double rightStart = center + textW / 2 + gap;

            _dockLineLeft.Width = Math.Max(0, leftEnd - pad);
            SetLeft(_dockLineLeft, pad);
            SetTop(_dockLineLeft, lineY);

            _dockLineRight.Width = Math.Max(0, ActualWidth - pad - rightStart);
            SetLeft(_dockLineRight, rightStart);
            SetTop(_dockLineRight, lineY);

            double total = disabled.Count * DockWidth + Math.Max(0, disabled.Count - 1) * 12;
            double x = Math.Max(pad, (ActualWidth - total) / 2);
            foreach (var d in disabled)
            {
                _dockSlots[d] = new Point(x, y);
                x += DockWidth + 12;
            }

            Refresh();
        }

        private const double DockWidth = 150;
        private const double DockHeight = 84;
        private double _dockTop = double.MaxValue;
        private readonly Dictionary<LayoutDisplay, Point> _dockSlots = new Dictionary<LayoutDisplay, Point>();
        private readonly TextBlock _dockLabel = new TextBlock();
        private readonly Border _dockLineLeft = new Border();
        private readonly Border _dockLineRight = new Border();

        /// <summary>Update positions and styling without recreating.</summary>
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

        /// <summary>Clones are drawn as an offset stack, so it is visible that there are several.</summary>
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

        // ---------------- dragging ----------------
        private void CardMouseDown(object sender, MouseButtonEventArgs e)
        {
            var card = (Border)sender;
            var d = (LayoutDisplay)card.Tag;
            Selected = d;
            Raise(SelectionChanged);
            Refresh();

            _drag = d;
            _moved = false;
            _fromDock = !d.Enabled;
            _dragStart = e.GetPosition(this);
            _grab = e.GetPosition(card);

            _dragOrigin.Clear();
            if (!_fromDock)
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
            if (!_moved && Math.Abs(p.X - _dragStart.X) < 3 && Math.Abs(p.Y - _dragStart.Y) < 3) return;
            _moved = true;

            // A monitor from the bottom row simply follows the cursor: it is not in the layout yet,
            // and there is nothing yet to recompute desktop coordinates from.
            if (_fromDock)
            {
                Border dragged = (Border)sender;
                SetLeft(dragged, p.X - _grab.X);
                SetTop(dragged, p.Y - _grab.Y);
                Panel.SetZIndex(dragged, 200);
                return;
            }

            double ddx = (p.X - _dragStart.X) / _scale;
            double ddy = (p.Y - _dragStart.Y) / _scale;

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

            if (_drag != null && _moved && _fromDock)
            {
                Point p = e.GetPosition(this);
                if (p.Y < _dockTop)
                {
                    // Dropped above the line — the monitor enters the layout. We put it where
                    // it was dropped and pull it to the neighbours' edges, as in ordinary dragging.
                    var d = _drag;
                    int nx = (int)Math.Round((p.X - _grab.X - _offX) / _scale);
                    int ny = (int)Math.Round((p.Y - _grab.Y - _offY) / _scale);
                    Snap(d, ref nx, ref ny);
                    d.X = nx;
                    d.Y = ny;
                    _drag = null;
                    _fromDock = false;
                    Raise(DropRequested);
                    return;
                }
                Relayout();     // dropped in the same row — the card returns to its place
            }
            else if (_drag != null && _moved)
            {
                Normalize();
                Relayout();
                Raise(LayoutChanged);
            }

            _drag = null;
            _fromDock = false;
        }

        /// <summary>Snapping to neighbours' edges — like Windows display settings, only fairer.</summary>
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

                TrySnap(ref bestX, ref snapX, nx, ox + ow, threshold);      // right of the neighbour
                TrySnap(ref bestX, ref snapX, nx, ox - w, threshold);       // left of the neighbour
                TrySnap(ref bestX, ref snapX, nx, ox, threshold);           // left-edge alignment
                TrySnap(ref bestX, ref snapX, nx, ox + ow - w, threshold);  // right-edge alignment
                TrySnap(ref bestX, ref snapX, nx, ox + (ow - w) / 2, threshold); // centre alignment

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

        /// <summary>The primary monitor is always at (0,0) — Windows requires it.</summary>
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

        /// <summary>Gap check: the desktop must be contiguous.</summary>
        public string Validate()
        {
            var enabled = new List<LayoutDisplay>();
            foreach (var d in Layout.Displays) if (d.Enabled) enabled.Add(d);
            if (enabled.Count == 0) return Text.NoMonitorPicked;
            if (enabled.Count == 1) return null;

            string overlap = OverlapProblem();
            if (overlap != null) return overlap;

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
                return Text.GapBetween;
            return null;
        }

        /// <summary>
        /// Whether monitors overlap each other. The one thing Windows definitely
        /// will not accept: it would arrange the screens its own way, and the profile would stay with
        /// impossible coordinates — and its "applied now" dot would
        /// never light up.
        ///
        /// This was checked only when a monitor was enabled with the switch, and
        /// dragging with the mouse bypassed it. Separate from Validate — because
        /// applying must stop on this, but must not on a gap between
        /// monitors: Windows closes a gap by itself.
        /// </summary>
        public string OverlapProblem()
        {
            return Layout == null ? null : Layout.OverlapProblem();
        }

        private static bool Touches(LayoutDisplay a, LayoutDisplay b)
        {
            if (a.CloneGroup >= 0 && a.CloneGroup == b.CloneGroup) return true;
            int ax1 = a.X, ay1 = a.Y, ax2 = a.X + a.DesktopWidth, ay2 = a.Y + a.DesktopHeight;
            int bx1 = b.X, by1 = b.Y, bx2 = b.X + b.DesktopWidth, by2 = b.Y + b.DesktopHeight;
            bool overlapX = ax1 < bx2 && bx1 < ax2;
            bool overlapY = ay1 < by2 && by1 < ay2;
            if (overlapX && overlapY) return true;                 // overlapping
            if (overlapX && (ay2 == by1 || by2 == ay1)) return true;
            if (overlapY && (ax2 == bx1 || bx2 == ax1)) return true;
            return false;
        }
    }
}
