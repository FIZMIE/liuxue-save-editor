/* =====================================================================
   界面外观：统一配色 + 圆角卡片 + 圆角按钮 + 自绘导航 + 绿色打勾框
   刻意避开 WinForms 默认那种灰扑扑的 GroupBox 观感。
   ===================================================================== */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

static class Theme
{
    public static readonly Color Bg        = Color.FromArgb(245, 246, 248);
    public static readonly Color Card      = Color.White;
    public static readonly Color Border    = Color.FromArgb(226, 230, 235);
    public static readonly Color Text      = Color.FromArgb(31, 35, 40);
    public static readonly Color Muted     = Color.FromArgb(107, 114, 128);
    public static readonly Color Faint     = Color.FromArgb(156, 163, 175);
    public static readonly Color Accent    = Color.FromArgb(37, 99, 235);
    public static readonly Color AccentDim = Color.FromArgb(29, 78, 216);
    public static readonly Color AccentBg  = Color.FromArgb(235, 242, 254);
    public static readonly Color Header    = Color.FromArgb(30, 41, 59);
    public static readonly Color HeaderSub = Color.FromArgb(148, 163, 184);
    public static readonly Color Ok        = Color.FromArgb(22, 163, 74);   // 打勾的绿
    public static readonly Color OkDim     = Color.FromArgb(21, 128, 61);
    public static readonly Color Warn      = Color.FromArgb(161, 98, 7);
    public static readonly Color Hover     = Color.FromArgb(241, 243, 245);

    public static Font Base    = new Font("Microsoft YaHei UI", 9f);
    public static Font Bold    = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
    public static Font Title   = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold);
    public static Font Small   = new Font("Microsoft YaHei UI", 8.25f);
    public static Font Mono    = new Font("Consolas", 9f);
    public static Font CardTitle   = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
    public static Font HeaderTitle = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
    public static Font NavBold     = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);

    public const int Radius = 8;

    /* 圆角矩形路径 */
    public static GraphicsPath Round(Rectangle r, int radius)
    {
        GraphicsPath p = new GraphicsPath();
        int d = Math.Max(1, Math.Min(radius, Math.Min(r.Width, r.Height) / 2)) * 2;
        if (d < 2) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /* 圆角打勾的小方框：勾上就是绿底白勾 */
    public static void DrawCheck(Graphics g, Rectangle box, bool on, bool hover)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath p = Round(box, 4))
        {
            if (on)
            {
                using (SolidBrush b = new SolidBrush(hover ? OkDim : Ok)) g.FillPath(b, p);
                using (Pen pen = new Pen(Color.White, 2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    float x = box.X, y = box.Y, w = box.Width, h = box.Height;
                    PointF[] pts = new PointF[]
                    {
                        new PointF(x + w * 0.26f, y + h * 0.52f),
                        new PointF(x + w * 0.43f, y + h * 0.70f),
                        new PointF(x + w * 0.76f, y + h * 0.31f)
                    };
                    g.DrawLines(pen, pts);
                }
            }
            else
            {
                using (SolidBrush b = new SolidBrush(hover ? Color.FromArgb(249, 250, 251) : Color.White))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(hover ? Faint : Color.FromArgb(203, 210, 219), 1.4f))
                    g.DrawPath(pen, p);
            }
        }
    }
}

/* 白色圆角卡片：1px 边框 + 标题栏 */
class Card : Panel
{
    public string Title = "";
    public int BodyTop = 38;

    public Card(string title)
    {
        Title = title;
        BackColor = Theme.Card;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width > 2 && Height > 2)
        {
            using (GraphicsPath p = Theme.Round(new Rectangle(0, 0, Width, Height), Theme.Radius))
                Region = new Region(p);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath p = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), Theme.Radius))
        {
            using (SolidBrush b = new SolidBrush(Theme.Card)) g.FillPath(b, p);
            using (Pen pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
        }
        if (Title.Length > 0)
        {
            using (SolidBrush b = new SolidBrush(Theme.Text))
                g.DrawString(Title, Theme.CardTitle, b, 14, 11);
            using (Pen p = new Pen(Theme.Border))
                g.DrawLine(p, 12, BodyTop - 1, Width - 12, BodyTop - 1);
        }
        else BodyTop = 10;
    }
}

/* 圆角扁平按钮 */
class Flat : Button
{
    public bool Primary;
    bool hover;

    public Flat(string text, bool primary, int w, int h)
    {
        Text = text;
        Primary = primary;
        Size = new Size(w, h);
        Font = Theme.Base;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        MouseEnter += delegate { hover = true; Invalidate(); };
        MouseLeave += delegate { hover = false; Invalidate(); };
        EnabledChanged += delegate { Invalidate(); };
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width > 2 && Height > 2)
        {
            using (GraphicsPath p = Theme.Round(new Rectangle(0, 0, Width, Height), 6))
                Region = new Region(p);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color back, fore, line;

        if (!Enabled)
        {
            back = Color.FromArgb(243, 244, 246); fore = Theme.Faint; line = Theme.Border;
        }
        else if (Primary)
        {
            back = hover ? Theme.AccentDim : Theme.Accent; fore = Color.White; line = back;
        }
        else
        {
            back = hover ? Theme.Hover : Color.White;
            fore = Theme.Text;
            line = hover ? Color.FromArgb(200, 206, 214) : Theme.Border;
        }

        using (GraphicsPath p = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), 6))
        {
            using (SolidBrush b = new SolidBrush(back)) g.FillPath(b, p);
            using (Pen pen = new Pen(line)) g.DrawPath(pen, p);
        }
        TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

/* 绿色打勾的方框（替代系统默认那种小灰勾） */
class GreenCheck : CheckBox
{
    bool hover;

    public GreenCheck()
    {
        Font = Theme.Base;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        MouseEnter += delegate { hover = true; Invalidate(); };
        MouseLeave += delegate { hover = false; Invalidate(); };
        CheckedChanged += delegate { Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Color bg = Parent != null ? Parent.BackColor : Theme.Card;
        using (SolidBrush b = new SolidBrush(bg)) g.FillRectangle(b, ClientRectangle);

        int box = 17;
        Rectangle r = new Rectangle(0, (Height - box) / 2, box, box);
        Theme.DrawCheck(g, r, Checked, hover);

        if (Text.Length > 0)
        {
            Rectangle t = new Rectangle(box + 8, 0, Math.Max(10, Width - box - 8), Height);
            TextRenderer.DrawText(g, Text, Font, t, Enabled ? Theme.Text : Theme.Faint,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix
                | TextFormatFlags.EndEllipsis);
        }
    }
}

/* 自绘导航：选中项圆角高亮 + 强调色 */
class NavList : ListBox
{
    int hoverIndex = -1;

    public NavList()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        BorderStyle = BorderStyle.None;
        IntegralHeight = false;
        ItemHeight = 38;
        HorizontalScrollbar = false;
        HorizontalExtent = 0;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9.5f);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle b = e.Bounds;
        bool sel = (e.State & DrawItemState.Selected) != 0;

        using (SolidBrush bg = new SolidBrush(Color.White)) g.FillRectangle(bg, b);

        Rectangle pill = new Rectangle(8, b.Y + 4, Math.Max(10, b.Width - 16), b.Height - 8);
        if (sel || e.Index == hoverIndex)
        {
            using (GraphicsPath p = Theme.Round(pill, 6))
            using (SolidBrush br = new SolidBrush(sel ? Theme.AccentBg : Theme.Hover))
                g.FillPath(br, p);
        }

        /* 固定 x，不用 b.X：ListBox 万一有横向滚动偏移，b.X 会是负数 */
        Font f = sel ? Theme.NavBold : Font;
        using (SolidBrush br = new SolidBrush(sel ? Theme.Accent : Theme.Text))
            g.DrawString(Items[e.Index].ToString(), f, br, 20, b.Y + (b.Height - f.Height) / 2 + 1);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int i = IndexFromPoint(e.Location);
        if (i != hoverIndex) { hoverIndex = i; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hoverIndex != -1) { hoverIndex = -1; Invalidate(); }
    }
}

/* 一行键值对（概览页用） */
class InfoRow
{
    public static void Add(Panel host, string key, string val, int y, int w, bool mono)
    {
        Label k = new Label();
        k.Text = key;
        k.ForeColor = Theme.Muted;
        k.Font = Theme.Base;
        k.SetBounds(18, y, 84, 20);
        host.Controls.Add(k);

        Label v = new Label();
        v.Text = val;
        v.ForeColor = Theme.Text;
        v.Font = mono ? Theme.Mono : Theme.Base;
        v.SetBounds(108, y, w - 130, 20);
        v.AutoEllipsis = true;
        host.Controls.Add(v);
    }
}
