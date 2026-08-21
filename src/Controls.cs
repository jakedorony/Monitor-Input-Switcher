// Controls.cs - the handful of owner-drawn controls the themed window needs.
// All metrics are in logical pixels scaled by the control's DeviceDpi so
// the window looks right at 100-200% display scaling.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MonitorSwitch
{
    static class Draw
    {
        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2;
            if (d <= 0 || r.Width <= 0 || r.Height <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Setup(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        }

        public static float Scale(Control c) { return c.DeviceDpi / 96f; }
    }

    // A flat card: filled background, 1px border, rounded corners. Children
    // lay out inside Padding.
    class Card : Panel
    {
        public Color Fill = Color.White;
        public Color Border = Color.Gainsboro;
        public int BorderWidth = 1;
        public float Radius = 8f;

        public Card()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Draw.Setup(e.Graphics);
            float s = Draw.Scale(this);
            float bw = BorderWidth * s;
            var r = new RectangleF(bw / 2, bw / 2, Width - bw, Height - bw);
            using (var path = Draw.RoundRect(r, Radius * s))
            using (var fill = new SolidBrush(Fill))
            using (var pen = new Pen(Border, bw))
            {
                e.Graphics.FillPath(fill, path);
                if (BorderWidth > 0) e.Graphics.DrawPath(pen, path);
            }
        }
    }

    // Flat rounded button with hover/pressed states. Primary = accent fill.
    class FlatButton : Control
    {
        public bool Primary;
        public Color Fill, Border, TextColor, HoverFill, AccentFill, AccentText;
        public float Radius = 6f;
        bool hover, down;

        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Draw.Setup(e.Graphics);
            float s = Draw.Scale(this);
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Color fill = Primary ? AccentFill : Fill;
            if (hover && Enabled) fill = Primary ? ControlPaint.Light(AccentFill, 0.15f) : HoverFill;
            if (down) fill = ControlPaint.Dark(fill, 0.08f);
            if (!Enabled) fill = ControlPaint.LightLight(fill);
            using (var path = Draw.RoundRect(r, Radius * s))
            using (var b = new SolidBrush(fill))
            using (var pen = new Pen(Primary ? AccentFill : Border, 1f * s))
            {
                e.Graphics.FillPath(b, path);
                e.Graphics.DrawPath(pen, path);
            }
            Color tc = Primary ? AccentText : TextColor;
            if (!Enabled) tc = Color.FromArgb(120, tc);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, tc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    // Text-only action ("Save current setup", "Rename") in the accent color.
    class LinkAction : Control
    {
        public Color TextColor;
        bool hover;

        public LinkAction()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            AutoSize = false;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        public void FitToText()
        {
            Size sz = TextRenderer.MeasureText(Text, Font);
            Width = sz.Width + 2;
            Height = sz.Height + 2;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Draw.Setup(e.Graphics);
            var f = hover ? new Font(Font, Font.Style | FontStyle.Underline) : Font;
            TextRenderer.DrawText(e.Graphics, Text, f, ClientRectangle, TextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (hover) f.Dispose();
        }
    }

    // The hero: two segments, the active one filled with the accent color.
    class SegmentedSwitch : Control
    {
        public string LeftTitle = "A", RightTitle = "B";
        public string LeftSub = "", RightSub = "";
        public int Active = -1;                 // 0 left, 1 right, -1 neither
        public bool LeftEnabled = true, RightEnabled = true;
        public Color Track, Accent, AccentText, Muted, TextColor;
        public event Action<int> SegmentClicked;
        int hoverSeg = -1;

        public SegmentedSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        int SegAt(int x) { return x < Width / 2 ? 0 : 1; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int s = SegAt(e.X);
            if (s != hoverSeg) { hoverSeg = s; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { hoverSeg = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            int s = SegAt(e.X);
            if ((s == 0 && !LeftEnabled) || (s == 1 && !RightEnabled)) return;
            var h = SegmentClicked;
            if (h != null) h(s);
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Draw.Setup(e.Graphics);
            float s = Draw.Scale(this);
            float pad = 6 * s, gap = 6 * s;
            var outer = new RectangleF(0, 0, Width, Height);
            using (var path = Draw.RoundRect(outer, 14 * s))
            using (var b = new SolidBrush(Track))
                e.Graphics.FillPath(b, path);

            float segW = (Width - pad * 2 - gap) / 2f;
            for (int i = 0; i < 2; i++)
            {
                var r = new RectangleF(pad + i * (segW + gap), pad, segW, Height - pad * 2);
                bool active = Active == i;
                bool enabled = i == 0 ? LeftEnabled : RightEnabled;
                if (active)
                {
                    using (var path = Draw.RoundRect(r, 10 * s))
                    using (var b = new SolidBrush(Accent))
                        e.Graphics.FillPath(b, path);
                }
                else if (hoverSeg == i && enabled)
                {
                    using (var path = Draw.RoundRect(r, 10 * s))
                    using (var b = new SolidBrush(Color.FromArgb(28, TextColor)))
                        e.Graphics.FillPath(b, path);
                }
                string title = i == 0 ? LeftTitle : RightTitle;
                string sub = i == 0 ? LeftSub : RightSub;
                Color tc = active ? AccentText : (enabled ? TextColor : Color.FromArgb(110, Muted));
                Color sc = active ? Color.FromArgb(220, AccentText) : Muted;
                var titleRect = new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)(r.Height * 0.58f));
                var subRect = new Rectangle((int)r.X, (int)(r.Y + r.Height * 0.52f), (int)r.Width, (int)(r.Height * 0.4f));
                TextRenderer.DrawText(e.Graphics, title, Theme.Hero, titleRect, tc,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(e.Graphics, sub, Theme.Small, subRect, sc,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }
    }

    // On/off toggle (the "Start with Windows" switch).
    class ToggleSwitch : Control
    {
        bool isOn;
        public bool On { get { return isOn; } set { if (isOn != value) { isOn = value; Invalidate(); } } }
        public Color Accent, Track, Knob;
        public event EventHandler Toggled;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            isOn = !isOn;
            Invalidate();
            var h = Toggled;
            if (h != null) h(this, EventArgs.Empty);
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Draw.Setup(e.Graphics);
            float s = Draw.Scale(this);
            var r = new RectangleF(0, 0, Width, Height);
            using (var path = Draw.RoundRect(r, Height / 2f))
            using (var b = new SolidBrush(isOn ? Accent : Track))
                e.Graphics.FillPath(b, path);
            float k = Height - 4 * s;
            float x = isOn ? Width - k - 2 * s : 2 * s;
            using (var b = new SolidBrush(Knob))
                e.Graphics.FillEllipse(b, x, 2 * s, k, k);
        }
    }

    // Two-segment sun/moon pill that cycles the theme.
    class ThemePill : Control
    {
        public bool Dark;
        public Color Track, Card, TextColor, Muted;
        public event EventHandler Clicked;

        public ThemePill()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            var h = Clicked;
            if (h != null) h(this, EventArgs.Empty);
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Draw.Setup(e.Graphics);
            float s = Draw.Scale(this);
            var r = new RectangleF(0, 0, Width, Height);
            using (var path = Draw.RoundRect(r, Height / 2f))
            using (var b = new SolidBrush(Track))
                e.Graphics.FillPath(b, path);
            float pad = 2 * s;
            float segW = (Width - pad * 2) / 2f;
            for (int i = 0; i < 2; i++)
            {
                bool lit = (i == 1) == Dark;
                var seg = new RectangleF(pad + i * segW, pad, segW, Height - pad * 2);
                if (lit)
                {
                    using (var path = Draw.RoundRect(seg, seg.Height / 2f))
                    using (var b = new SolidBrush(Card))
                        e.Graphics.FillPath(b, path);
                }
                string glyph = i == 0 ? Theme.GlyphSun : Theme.GlyphMoon;
                TextRenderer.DrawText(e.Graphics, glyph, Theme.Glyph, Rectangle.Round(seg),
                    lit ? TextColor : Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    // A single glyph button (gear, etc.).
    class GlyphButton : Control
    {
        public string Glyph = Theme.GlyphGear;
        public Color TextColor, HoverFill;
        bool hover;

        public GlyphButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Draw.Setup(e.Graphics);
            float s = Draw.Scale(this);
            if (hover)
            {
                using (var path = Draw.RoundRect(new RectangleF(0, 0, Width, Height), 6 * s))
                using (var b = new SolidBrush(HoverFill))
                    e.Graphics.FillPath(b, path);
            }
            TextRenderer.DrawText(e.Graphics, Glyph, Theme.GlyphLarge, ClientRectangle, TextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    // Small rounded status chip ("Synced", "Not signed in").
    class StatusPill : Control
    {
        public Color Fill, TextColor;
        public string Glyph = Theme.GlyphCheck;

        public StatusPill()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public void FitToText()
        {
            float s = Draw.Scale(this);
            Size sz = TextRenderer.MeasureText(Text, Theme.Small);
            Width = sz.Width + (int)(34 * s);
            Height = (int)(22 * s);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Draw.Setup(e.Graphics);
            float s = Draw.Scale(this);
            using (var path = Draw.RoundRect(new RectangleF(0, 0, Width, Height), Height / 2f))
            using (var b = new SolidBrush(Fill))
                e.Graphics.FillPath(b, path);
            var gl = new Rectangle((int)(8 * s), 0, (int)(14 * s), Height);
            TextRenderer.DrawText(e.Graphics, Glyph, Theme.Glyph, gl, TextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            var tr = new Rectangle((int)(24 * s), 0, Width - (int)(30 * s), Height);
            TextRenderer.DrawText(e.Graphics, Text, Theme.Small, tr, TextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    // Colors a ContextMenuStrip from the palette (used by InputPicker).
    class MenuColors : ProfessionalColorTable
    {
        readonly Palette p;
        public MenuColors(Palette p) { this.p = p; }
        public override Color MenuBorder { get { return p.Border; } }
        public override Color MenuItemBorder { get { return p.Accent; } }
        public override Color MenuItemSelected { get { return p.Hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return p.Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return p.Hover; } }
        public override Color ToolStripDropDownBackground { get { return p.Card; } }
        public override Color ImageMarginGradientBegin { get { return p.Card; } }
        public override Color ImageMarginGradientMiddle { get { return p.Card; } }
        public override Color ImageMarginGradientEnd { get { return p.Card; } }
        public override Color SeparatorDark { get { return p.Border; } }
        public override Color SeparatorLight { get { return p.Border; } }
    }

    // Themed dropdown: a flat field showing the current choice; click opens a
    // themed menu. Replaces ComboBox, whose borders ignore dark BackColors.
    class InputPicker : Control
    {
        public class Item
        {
            public uint Value; public string Label;
        }

        readonly List<Item> items = new List<Item>();
        int selected = -1;
        bool hover;
        public Color Fill, Border, TextColor, Muted, MenuBack, MenuHover, Accent;
        public event EventHandler ValueChanged;

        public InputPicker()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Height = 26;
        }

        public void SetItems(uint[] values, uint current)
        {
            items.Clear();
            selected = -1;
            foreach (uint v in values)
            {
                items.Add(new Item { Value = v, Label = v == 0 ? "Not set" : TrayApp.InputName((int)v) });
                if (v == current) selected = items.Count - 1;
            }
            if (selected < 0)
            {
                // The saved value isn't in the monitor's advertised list; show
                // it anyway rather than silently picking something else.
                items.Add(new Item { Value = current, Label = current == 0 ? "Not set" : TrayApp.InputName((int)current) });
                selected = items.Count - 1;
            }
            Invalidate();
        }

        public uint SelectedValue2
        {
            get { return selected >= 0 ? items[selected].Value : 0; }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (items.Count == 0) return;
            var menu = new ContextMenuStrip
            {
                Renderer = new ToolStripProfessionalRenderer(new MenuColors(Theme.Current)),
                ShowImageMargin = false, ShowCheckMargin = false, Font = Theme.Body,
                BackColor = MenuBack, ForeColor = TextColor
            };
            for (int i = 0; i < items.Count; i++)
            {
                int idx = i;
                var mi = new ToolStripMenuItem(items[i].Label)
                {
                    ForeColor = i == selected ? Accent : TextColor,
                    Font = i == selected ? Theme.Strong : Theme.Body,
                    BackColor = MenuBack
                };
                mi.Click += delegate
                {
                    if (idx == selected) return;
                    selected = idx;
                    Invalidate();
                    var h = ValueChanged;
                    if (h != null) h(this, EventArgs.Empty);
                };
                menu.Items.Add(mi);
            }
            menu.Closed += delegate { menu.Dispose(); };
            menu.Show(this, new Point(0, Height));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Draw.Setup(e.Graphics);
            float s = Draw.Scale(this);
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using (var path = Draw.RoundRect(r, 6 * s))
            using (var b = new SolidBrush(hover ? ControlPaint.Light(Fill, 0.05f) : Fill))
            using (var pen = new Pen(hover ? Accent : Border, 1f * s))
            {
                e.Graphics.FillPath(b, path);
                e.Graphics.DrawPath(pen, path);
            }
            string label = selected >= 0 ? items[selected].Label : "";
            var tr = new Rectangle((int)(9 * s), 0, Width - (int)(30 * s), Height);
            TextRenderer.DrawText(e.Graphics, label, Font, tr,
                selected >= 0 && items[selected].Value == 0 ? Muted : TextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            var cr = new Rectangle(Width - (int)(22 * s), 0, (int)(18 * s), Height);
            TextRenderer.DrawText(e.Graphics, Theme.GlyphChevron, Theme.Glyph, cr, Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
