/* =====================================================================
   界面外观
     - 字体：Maple Mono CN（装了就用，没装自动回落微软雅黑）
     - 背景：纯色（不做桌面模糊，那个太吃性能）
     - 玻璃感只做在按钮/卡片上：渐变 + 顶部高光 + 细描边
     - 圆角一律用抗锯齿绘制，不用 Region 硬裁（Region 是锯齿的来源）
   ===================================================================== */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

/* ---------------- 字体 ---------------- */
static class Fonts
{
    public static readonly string Ui;
    public static readonly string Medium;
    public static readonly string Semi;

    static Fonts()
    {
        Ui = Pick(new string[] { "Maple Mono CN", "MapleMono-CN" }, "Microsoft YaHei UI");
        Medium = Pick(new string[] { "Maple Mono CN Medium" }, Ui);
        Semi = Pick(new string[] { "Maple Mono CN SemiBold" }, Ui);
    }

    static string Pick(string[] wanted, string fallback)
    {
        try
        {
            FontFamily[] all = FontFamily.Families;
            foreach (string w in wanted)
                foreach (FontFamily f in all)
                    if (string.Equals(f.Name, w, StringComparison.OrdinalIgnoreCase)) return f.Name;
        }
        catch { }
        return fallback;
    }

    public static Font Get(float size) { return Get(size, FontStyle.Regular); }
    public static Font Get(float size, FontStyle style)
    {
        try { return new Font(Ui, size, style); }
        catch { try { return new Font("Microsoft YaHei UI", size, style); } catch { return new Font(FontFamily.GenericSansSerif, size); } }
    }
    public static Font Mid(float size)
    {
        try { return new Font(Medium, size, FontStyle.Regular); }
        catch { return Get(size); }
    }
    public static Font Sbx(float size)
    {
        try { return new Font(Semi, size, FontStyle.Regular); }
        catch { return Get(size, FontStyle.Bold); }
    }
}

/* ---------------- 配色 ---------------- */
static class Theme
{
    public static readonly Color Bg        = Color.FromArgb(246, 247, 249);  // 窗口底色
    public static readonly Color Card      = Color.White;
    public static readonly Color Border    = Color.FromArgb(228, 231, 236);
    public static readonly Color Text      = Color.FromArgb(28, 32, 38);
    public static readonly Color Muted     = Color.FromArgb(105, 113, 125);
    public static readonly Color Faint     = Color.FromArgb(150, 158, 170);
    public static readonly Color Accent    = Color.FromArgb(10, 132, 255);   // 苹果蓝
    public static readonly Color AccentDim = Color.FromArgb(0, 110, 230);
    public static readonly Color AccentBg  = Color.FromArgb(46, 10, 132, 255);
    public static readonly Color Header    = Color.FromArgb(30, 41, 59);
    public static readonly Color HeaderSub = Color.FromArgb(170, 182, 198);
    public static readonly Color Ok        = Color.FromArgb(48, 209, 88);    // 苹果绿
    public static readonly Color OkDim     = Color.FromArgb(36, 178, 72);
    public static readonly Color Warn      = Color.FromArgb(161, 98, 7);
    public static readonly Color Hover     = Color.FromArgb(24, 120, 130, 150);

    /* 玻璃表面：上下两端颜色 + 描边，靠渐变和高光做质感 */
    public static readonly Color GlassTop  = Color.FromArgb(255, 255, 255, 255);
    public static readonly Color GlassBot  = Color.FromArgb(236, 242, 246, 250);
    public static readonly Color GlassEdge = Color.FromArgb(219, 224, 231);
    public static readonly Color GlassHi   = Color.FromArgb(190, 255, 255, 255);
    public static readonly Color HairLine  = Color.FromArgb(232, 235, 240);

    public static readonly Font Base    = Fonts.Get(9.5f);
    public static readonly Font Bold    = Fonts.Get(9.5f, FontStyle.Bold);
    public static readonly Font Title   = Fonts.Sbx(11.5f);
    public static readonly Font Small   = Fonts.Get(8.5f);
    public static readonly Font Mono    = Fonts.Get(9.5f);
    public static readonly Font CardTitle   = Fonts.Sbx(9.5f);
    public static readonly Font HeaderTitle = Fonts.Sbx(12.5f);
    public static readonly Font NavBold     = Fonts.Mid(10f);
    public static readonly Font NavItem     = Fonts.Get(10f);

    public const int Radius   = 16;   // 卡片 / 侧边栏
    public const int RadiusSm = 10;   // 按钮 / 高亮

    /* 统一打开抗锯齿 */
    public static void AA(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
    }

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
        AA(g);
        using (GraphicsPath p = Round(box, 5))
        {
            if (on)
            {
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new Rectangle(box.X, box.Y, box.Width, box.Height + 1),
                    Color.FromArgb(90, 220, 120), hover ? OkDim : Ok, 90f))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(Color.FromArgb(150, 255, 255, 255), 2f))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                    float x = box.X, y = box.Y, w = box.Width, h = box.Height;
                    g.DrawLines(pen, new PointF[]
                    {
                        new PointF(x + w * 0.26f, y + h * 0.52f),
                        new PointF(x + w * 0.43f, y + h * 0.70f),
                        new PointF(x + w * 0.76f, y + h * 0.31f)
                    });
                }
            }
            else
            {
                using (SolidBrush b = new SolidBrush(hover ? Color.FromArgb(250, 251, 252) : Color.FromArgb(240, 242, 245)))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(hover ? Faint : Color.FromArgb(200, 207, 216), 1.4f))
                    g.DrawPath(pen, p);
            }
        }
    }

    /* 画一块玻璃面：渐变底 + 描边 + 顶部高光 */
    public static void GlassFace(Graphics g, Rectangle r, int radius,
                                 Color top, Color bottom, Color edge, bool highlight)
    {
        AA(g);
        if (r.Width <= 0 || r.Height <= 0) return;
        using (GraphicsPath p = Round(r, radius))
        {
            using (LinearGradientBrush b = new LinearGradientBrush(
                new Rectangle(r.X, r.Y, r.Width, r.Height + 1), top, bottom, 90f))
                g.FillPath(b, p);
            using (Pen pen = new Pen(edge)) g.DrawPath(pen, p);
            if (highlight)
                using (Pen hp = new Pen(GlassHi))
                {
                    hp.StartCap = LineCap.Round; hp.EndCap = LineCap.Round;
                    g.DrawLine(hp, r.X + radius / 2, r.Y + 1, r.Right - radius / 2 - 1, r.Y + 1);
                }
        }
    }
}

/* ---------------- 面板基类 ---------------- */
class Glass : Panel
{
    public Color FillTop = Color.White;
    public Color FillBottom = Color.FromArgb(246, 247, 249);
    public Color Edge = Theme.Border;
    public bool DrawEdge = true;
    public int Radius = 0;
    public string Title = "";
    public int BodyTop = 38;
    public bool HairBottom = false;
    public bool HairTop = false;
    public bool Highlight = false;

    public Glass()
    {
        /* 刻意不用透明：WinForms 的透明子控件会反过来让父级重画背景，
           卡片套卡片时会串味（把别的卡片标题画到这一行上）。
           这里改成不透明 + 填入所在底色，圆角照样是抗锯齿画的。 */
        SetStyle(ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint
               | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Bg;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        if (r.Width <= 0 || r.Height <= 0) return;

        if (Radius > 0)
            Theme.GlassFace(g, r, Radius, FillTop, FillBottom, Edge, Highlight && DrawEdge);
        else
        {
            if (FillTop == FillBottom)
                using (SolidBrush b = new SolidBrush(FillTop)) g.FillRectangle(b, 0, 0, Width, Height);
            else
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new Rectangle(0, 0, Width, Height + 1), FillTop, FillBottom, 90f))
                    g.FillRectangle(b, 0, 0, Width, Height);
            if (DrawEdge)
                using (Pen pen = new Pen(Edge)) g.DrawRectangle(pen, r);
            if (Highlight)
                using (Pen hp = new Pen(Theme.GlassHi))
                    g.DrawLine(hp, 0, 0, Width, 0);
        }

        if (HairBottom)
            using (Pen pen = new Pen(Theme.HairLine)) g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        if (HairTop)
            using (Pen pen = new Pen(Theme.HairLine)) g.DrawLine(pen, 0, 0, Width, 0);

        if (Title.Length > 0)
        {
            Theme.AA(g);
            using (SolidBrush b = new SolidBrush(Theme.Text))
                g.DrawString(Title, Theme.CardTitle, b, 16, 12);
            using (Pen p = new Pen(Color.FromArgb(160, 232, 235, 240)))
                g.DrawLine(p, 14, BodyTop - 1, Width - 14, BodyTop - 1);
        }

        base.OnPaint(e);   // 让外部订阅的 Paint 在底色之后触发
    }
}

/* 卡片：圆角 + 轻微玻璃渐变 */
class Card : Glass
{
    public Card(string title)
    {
        Title = title;
        Radius = Theme.Radius;
        FillTop = Color.FromArgb(255, 255, 255, 255);
        FillBottom = Color.FromArgb(249, 251, 253, 255);
        Edge = Theme.Border;
        Highlight = true;
        BackColor = Theme.Bg;      // 方角与页面同色 -> 只剩抗锯齿的圆角
    }
}

/* ---------------- 玻璃按钮 ---------------- */
class Flat : Button
{
    public bool Primary;
    bool hover, down;

    public Flat(string text, bool primary, int w, int h)
    {
        Text = text;
        Primary = primary;
        Size = new Size(w, h);
        Font = Theme.Base;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;   // 圆角外的像素要有东西填，否则是黑的
        MouseEnter += delegate { hover = true; Invalidate(); };
        MouseLeave += delegate { hover = false; down = false; Invalidate(); };
        MouseDown += delegate { down = true; Invalidate(); };
        MouseUp += delegate { down = false; Invalidate(); };
        EnabledChanged += delegate { Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        if (r.Width <= 0 || r.Height <= 0) return;

        Color top, bot, edge;
        Color fore;

        if (!Enabled)
        {
            top = Color.FromArgb(250, 251, 252); bot = Color.FromArgb(244, 246, 248);
            edge = Color.FromArgb(230, 233, 238); fore = Theme.Faint;
        }
        else if (Primary)
        {
            top = down ? Theme.AccentDim : (hover ? Color.FromArgb(64, 156, 255) : Color.FromArgb(48, 148, 255));
            bot = down ? Color.FromArgb(0, 96, 210) : (hover ? Theme.AccentDim : Theme.Accent);
            edge = Color.FromArgb(0, 100, 210);
            fore = Color.White;
        }
        else
        {
            top = down ? Color.FromArgb(232, 236, 241) : (hover ? Color.FromArgb(255, 255, 255) : Color.FromArgb(253, 254, 255));
            bot = down ? Color.FromArgb(222, 227, 233) : (hover ? Color.FromArgb(240, 244, 248) : Color.FromArgb(234, 239, 244));
            edge = hover ? Color.FromArgb(196, 204, 214) : Theme.GlassEdge;
            fore = Theme.Text;
        }

        Theme.GlassFace(g, r, Theme.RadiusSm, top, bot, edge, true);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(0, down ? 1 : 0, Width, Height), fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

/* ---------------- 绿色打勾方框 ---------------- */
class GreenCheck : CheckBox
{
    bool hover;

    public GreenCheck()
    {
        Font = Theme.Base;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;   // 圆角外的像素要有东西填，否则是黑的
        MouseEnter += delegate { hover = true; Invalidate(); };
        MouseLeave += delegate { hover = false; Invalidate(); };
        CheckedChanged += delegate { Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        int box = 17;
        Rectangle r = new Rectangle(0, (Height - box) / 2, box, box);
        Theme.DrawCheck(e.Graphics, r, Checked, hover);

        if (Text.Length > 0)
        {
            Rectangle t = new Rectangle(box + 9, 0, Math.Max(10, Width - box - 9), Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, t, Enabled ? Theme.Text : Theme.Faint,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix
                | TextFormatFlags.EndEllipsis);
        }
    }
}

/* ---------------- 导航（自绘 + 滑动高亮）----------------
   不用 ListBox：它的 BackColor 不支持透明，而且做不了高亮滑动。 */
class NavBar : Glass
{
    public readonly List<string> Items = new List<string>();
    public event EventHandler SelectedIndexChanged;

    int selected = -1;
    int hoverIndex = -1;
    float pillY = -1;
    float pillTarget = 0;
    public int ItemHeight = 44;

    Timer anim;

    public NavBar()
    {
        FillTop = Color.White;
        FillBottom = Color.White;
        Radius = Theme.Radius;
        Edge = Theme.Border;
        Highlight = false;
        BackColor = Theme.Bg;
        Font = Theme.NavItem;
        Cursor = Cursors.Hand;

        anim = new Timer();
        anim.Interval = 15;
        anim.Tick += delegate
        {
            float d = pillTarget - pillY;
            if (Math.Abs(d) < 0.4f) { pillY = pillTarget; anim.Stop(); }
            else pillY += d * 0.32f;
            Invalidate();
        };
    }

    public int SelectedIndex
    {
        get { return selected; }
        set
        {
            if (value < 0 || value >= Items.Count || value == selected) return;
            selected = value;
            pillTarget = value * ItemHeight;
            if (pillY < 0) pillY = pillTarget;
            anim.Start();
            Invalidate();
            if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int i = (e.Y - 8) / ItemHeight;
        if (i < 0 || i >= Items.Count) i = -1;
        if (i != hoverIndex) { hoverIndex = i; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hoverIndex != -1) { hoverIndex = -1; Invalidate(); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int i = (e.Y - 8) / ItemHeight;
        if (i >= 0 && i < Items.Count) SelectedIndex = i;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        Theme.AA(g);

        const int pad = 9;
        const int top = 8;
        int w = Math.Max(10, Width - pad * 2);

        if (pillY >= 0)
        {
            Rectangle pill = new Rectangle(pad, top + (int)Math.Round(pillY), w, ItemHeight - 8);
            using (GraphicsPath p = Theme.Round(pill, 12))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new Rectangle(pill.X, pill.Y, pill.Width, pill.Height + 1),
                    Color.FromArgb(64, 84, 168, 255), Color.FromArgb(52, 10, 132, 255), 90f))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(Color.FromArgb(90, 10, 132, 255), 1f)) g.DrawPath(pen, p);
                using (Pen hp = new Pen(Color.FromArgb(110, 255, 255, 255)))
                    g.DrawLine(hp, pill.X + 10, pill.Y + 1, pill.Right - 11, pill.Y + 1);
            }
        }

        for (int i = 0; i < Items.Count; i++)
        {
            int y = top + i * ItemHeight;

            if (i == hoverIndex && i != selected)
            {
                Rectangle hr = new Rectangle(pad, y, w, ItemHeight - 8);
                using (GraphicsPath p = Theme.Round(hr, 12))
                using (SolidBrush b = new SolidBrush(Theme.Hover))
                    g.FillPath(b, p);
            }

            bool sel = (i == selected);
            Font f = sel ? Theme.NavBold : Theme.NavItem;
            Color fg = sel ? Theme.Accent : Theme.Text;
            Rectangle tr = new Rectangle(pad + 18, y, w - 24, ItemHeight - 8);
            TextRenderer.DrawText(g, Items[i], f, tr, fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix
                | TextFormatFlags.EndEllipsis);
        }
    }
}

/* ---------------- 概览页的一行键值对 ---------------- */
class InfoRow
{
    public static void Add(Panel host, string key, string val, int y, int w, bool mono)
    {
        Label k = new Label();
        k.Text = key;
        k.ForeColor = Theme.Muted;
        k.Font = Theme.Base;
        k.BackColor = Color.White;
        k.SetBounds(18, y, 96, 20);
        host.Controls.Add(k);

        Label v = new Label();
        v.Text = val;
        v.ForeColor = Theme.Text;
        v.Font = mono ? Theme.Mono : Theme.Base;
        v.BackColor = Color.White;
        v.SetBounds(120, y, w - 142, 20);
        v.AutoEllipsis = true;
        host.Controls.Add(v);
    }
}
