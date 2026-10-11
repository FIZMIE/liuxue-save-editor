/* =====================================================================
   留学模拟器 存档修改器 —— 原生窗口版
   编译： csc /target:winexe /platform:x64 /win32icon:app.ico
                /out:留学模拟器修改器.exe wfsave.cs savecore.cs
   功能、字段、备份策略和之前的网页版完全一样，只是换成了原生窗口。
   ===================================================================== */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

static class Program
{
    const string APPNAME = "留学模拟器 存档修改器";
    const string VERSION = "2.0";

    /* 把窗口自己画成图，用来验证界面（不依赖抓屏） */
    static void ShotMode(string[] args)
    {
        string outPath = args[1];
        int page = args.Length >= 3 ? int.Parse(args[2]) : 0;
        SaveForm f = new SaveForm();
        f.Show();
        Application.DoEvents();
        System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer();
        tm.Interval = 2500;
        tm.Tick += delegate
        {
            tm.Stop();
            try
            {
                f.GotoPage(page);
                Application.DoEvents();
                System.Threading.Thread.Sleep(600);
                Application.DoEvents();
                using (Bitmap bmp = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                    bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(outPath + ".err.txt", ex.ToString()); } catch { }
            }
            f.Close();
        };
        tm.Start();
        Application.Run(f);
    }

    [STAThread]
    static void Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
        {
            try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), e.Exception.ToString()); } catch { }
        };
        AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
        {
            try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash2.log"), e.ExceptionObject.ToString()); } catch { }
        };

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length >= 2 && args[0] == "--shot")
        {
            ShotMode(args);
            return;
        }

        try { Application.Run(new SaveForm()); }
        catch (Exception ex)
        {
            try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), ex.ToString()); } catch { }
            MessageBox.Show("程序出错：\n\n" + ex.ToString(), APPNAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public static string Title { get { return APPNAME; } }
    public static string Ver { get { return VERSION; } }
}

/* ===================================================================== */
class SaveForm : Form
{
    const string APPNAME = "留学模拟器 存档修改器";
    const string AUTHOR = "FIZMIE";
    const string APPVER  = "2.0";

    SaveDoc doc;
    bool loading = false;

    ComboBox cmbSave;
    Button btnRescan, btnOpenFile, btnOpenFolder;
    NavBar nav;
    Panel host;
    Panel content;
    Label lblStatus;
    Button btnSave, btnBackup, btnRestore, btnRevert;

    Panel topPanel, navPanel, bottomPanel;
    string[] NavNames = new string[] {
        "概览", "金钱资产", "角色", "人物关系", "任务进度", "学业工作", "身体状态", "原始数据", "说明"
    };

    public SaveForm()
    {
        Text = APPNAME + "  v" + APPVER;
        ClientSize = new Size(1100, 760);
        MinimumSize = new Size(960, 640);
        StartPosition = FormStartPosition.CenterScreen;
        Font = Theme.Base;
        BackColor = Theme.Bg;
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        try { Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { }

        BuildTop();
        BuildNav();
        BuildBottom();

        Resize += delegate { DoLayout(); };
        ResizeEnd += delegate { DoLayout(); ShowPage(); };
        Load += delegate { DoLayout(); ReloadSaveList(); };
    }

    /* 整页重排时先把窗口冻结，排完再一次性画出来。
       不这样做的话新控件会一个个往下刷，看着就是"从上往下刷新"的老式观感。 */
    [DllImport("user32.dll")] static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    void Freeze() { try { SendMessage(Handle, 0x000B, 0, 0); } catch { } }
    void Thaw()
    {
        try { SendMessage(Handle, 0x000B, 1, 0); } catch { }
        Invalidate(true);
    }

    /* 标签默认是不透明的系统灰，会把磨砂玻璃盖住，统一改成透明 */
    static void MakeGlass(Control c)
    {
        try
        {
            if (!(c is Glass) && (c is Label || c is CheckBox || c is Panel
                || c is FlowLayoutPanel || c is TableLayoutPanel))
                c.BackColor = Color.Transparent;
        }
        catch { }
        foreach (Control ch in c.Controls) MakeGlass(ch);
    }

    /* 四个区域全部按像素算，不依赖 WinForms 的 Dock 顺序 */
    void DoLayout()
    {
        int W = ClientSize.Width, H = ClientSize.Height;
        const int topH = 108, botH = 52, gap = 12, navW = 170;
        if (topPanel != null) topPanel.SetBounds(0, 0, W, topH);
        if (bottomPanel != null) bottomPanel.SetBounds(0, H - botH, W, botH);
        if (navPanel != null) navPanel.SetBounds(gap, topH, navW, Math.Max(10, H - topH - botH - gap));
        if (host != null) host.SetBounds(gap + navW + gap, topH,
            Math.Max(10, W - navW - gap * 3), Math.Max(10, H - topH - botH - gap));
    }
    /* ==================== 顶栏 ==================== */
    void BuildTop()
    {
        topPanel = new Panel();
        topPanel.SetBounds(0, 0, 10, 108);
        topPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(topPanel);
        Panel p = topPanel;

        /* 深色标题条 */
        Glass hd = new Glass();
        hd.SetBounds(0, 0, 4000, 56);
        hd.FillTop = Theme.Header;
        hd.FillBottom = Theme.Header;
        hd.DrawEdge = false;
        hd.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        hd.Paint += delegate(object s, PaintEventArgs e)
        {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (SolidBrush b = new SolidBrush(Color.White))
                e.Graphics.DrawString("留学模拟器 存档修改器", Theme.HeaderTitle, b, 20, 14);
            /* 版本号跟在标题后面，位置按实测宽度算 —— 换字体后标题宽度会变，
               写死 x 就会叠在一起 */
            Size ts = TextRenderer.MeasureText(e.Graphics, "留学模拟器 存档修改器", Theme.HeaderTitle);
            using (SolidBrush b = new SolidBrush(Theme.HeaderSub))
                e.Graphics.DrawString("v" + APPVER, Theme.Small, b, 22 + ts.Width, 26);
            TextRenderer.DrawText(e.Graphics, "by " + AUTHOR, Theme.Small,
                new Rectangle(0, 0, ClientSize.Width - 22, hd.Height), Theme.HeaderSub,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        };
        p.Controls.Add(hd);

        /* 白色存档选择条 */
        Glass bar = new Glass();
        bar.SetBounds(0, 56, 4000, 52);
        bar.FillTop = Color.White;
        bar.FillBottom = Color.White;
        bar.DrawEdge = false;
        bar.HairBottom = true;
        bar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        bar.Paint += delegate(object s, PaintEventArgs e)
        {
            using (Pen pen = new Pen(Theme.Border))
                e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
            using (SolidBrush b = new SolidBrush(Theme.Muted))
                e.Graphics.DrawString("存档", Theme.Base, b, 20, 17);
        };
        p.Controls.Add(bar);

        cmbSave = new ComboBox();
        cmbSave.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbSave.Font = Theme.Base;
        cmbSave.SetBounds(62, 13, 540, 26);
        cmbSave.SelectedIndexChanged += delegate { OnSaveSelected(); };
        bar.Controls.Add(cmbSave);

        btnRescan = new Flat("重新扫描", false, 92, 28);
        btnRescan.SetBounds(614, 12, 92, 28);
        btnRescan.Click += delegate { ReloadSaveList(); };
        bar.Controls.Add(btnRescan);

        btnOpenFile = new Flat("打开存档文件…", false, 124, 28);
        btnOpenFile.SetBounds(714, 12, 124, 28);
        btnOpenFile.Click += delegate { OpenFileDialog(); };
        bar.Controls.Add(btnOpenFile);

        btnOpenFolder = new Flat("打开存档目录", false, 124, 28);
        btnOpenFolder.SetBounds(846, 12, 124, 28);
        btnOpenFolder.Click += delegate
        {
            string d = SaveDoc.SaveDirDefault();
            if (Directory.Exists(d)) Process.Start("explorer.exe", "\"" + d + "\"");
            else MessageBox.Show("目录不存在：\n" + d, APPNAME);
        };
        bar.Controls.Add(btnOpenFolder);
    }
    /* ==================== 左侧导航 ==================== */
    void BuildNav()
    {
        navPanel = new NavBar();
        navPanel.SetBounds(0, 108, 170, 10);
        navPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;
        NavBar left = (NavBar)navPanel;
        left.Paint += delegate(object s, PaintEventArgs e)
        {
            using (Pen pen = new Pen(Theme.Border))
                e.Graphics.DrawLine(pen, left.Width - 1, 0, left.Width - 1, left.Height);
        };
        Controls.Add(left);

        nav = new NavBar();
        nav.Dock = DockStyle.Fill;
        foreach (string n in NavNames) nav.Items.Add(n);
        nav.SelectedIndexChanged += delegate { ShowPage(); };
        left.Controls.Add(nav);

        host = new Panel();
        host.SetBounds(170, 108, 10, 10);
        host.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        host.BackColor = Color.Transparent;
        host.AutoScroll = false;
        Controls.Add(host);

        /* 只用一个常显的内容面板。
           之前是 9 个面板各自隐藏/显示，结果是：建面板时它是隐藏的，
           WinForms 不会给它里面的控件建窗口句柄，之后改成可见也不会补建，
           页面就是一片空白。一个常显面板就没这问题。 */
        content = new Panel();
        content.Dock = DockStyle.Fill;
        content.AutoScroll = true;
        content.BackColor = Color.Transparent;
        host.Controls.Add(content);

        nav.SelectedIndex = 0;
        ShowPage();
    }
    public static void Log(string s)
    {
        try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui.log"),
            DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s + Environment.NewLine); } catch { }
    }

    public void GotoPage(int i)
    {
        if (nav != null && i >= 0 && i < nav.Items.Count) nav.SelectedIndex = i;
        ShowPage();
    }

    void ShowPage()
    {
        if (nav.SelectedIndex < 0 || doc == null) return;
        string key = NavNames[nav.SelectedIndex];

        Freeze();
        content.SuspendLayout();
        content.Controls.Clear();
        content.AutoScrollPosition = new Point(0, 0);

        if (key == "概览") BuildOverview();
        else if (key == "金钱资产") BuildMoney();
        else if (key == "角色") BuildPerson();
        else if (key == "人物关系") BuildRel();
        else if (key == "任务进度") BuildQuest();
        else if (key == "学业工作") BuildStudy();
        else if (key == "身体状态") BuildStatus();
        else if (key == "原始数据") BuildRaw();
        else if (key == "说明") BuildHelp();

        content.ResumeLayout(true);
        content.PerformLayout();

        /* 关键：窗口已经显示出来之后再往里加控件，光靠 ResumeLayout 是不够的，
           控件不会自己显示出来（点导航就白屏就是这个原因）。
           这里自顶向下强制把句柄建出来，再逐个刷新。 */
        MakeGlass(content);
        Realize(content);
        Thaw();
    }

    static void Realize(Control c)
    {
        if (!c.Visible) return;
        c.CreateControl();
        foreach (Control ch in c.Controls) Realize(ch);
        c.Invalidate();
    }
    void BuildBottom()
    {
        bottomPanel = new Glass();
        bottomPanel.SetBounds(0, 0, 10, 52);
        bottomPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        ((Glass)bottomPanel).FillTop = Color.White;
        ((Glass)bottomPanel).FillBottom = Color.White;
        ((Glass)bottomPanel).DrawEdge = false;
        ((Glass)bottomPanel).HairTop = true;
        Controls.Add(bottomPanel);
        Panel p = bottomPanel;
        p.Paint += delegate(object s, PaintEventArgs e)
        {
            using (Pen pen = new Pen(Theme.Border))
                e.Graphics.DrawLine(pen, 0, 0, p.Width, 0);
        };

        lblStatus = new Label();
        lblStatus.Dock = DockStyle.Fill;
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        lblStatus.Padding = new Padding(20, 0, 0, 0);
        lblStatus.ForeColor = Theme.Muted;
        lblStatus.Font = Theme.Base;
        lblStatus.Text = "未打开存档";
        p.Controls.Add(lblStatus);

        FlowLayoutPanel fl = new FlowLayoutPanel();
        fl.Dock = DockStyle.Right;
        fl.FlowDirection = FlowDirection.RightToLeft;
        fl.WrapContents = false;
        fl.AutoSize = true;
        fl.BackColor = Color.Transparent;
        fl.Padding = new Padding(0, 12, 18, 0);
        p.Controls.Add(fl);

        btnSave = new Flat("保存", true, 92, 28);
        btnSave.Click += delegate { DoSave(); };
        fl.Controls.Add(btnSave);

        btnBackup = new Flat("备份", false, 74, 28);
        btnBackup.Click += delegate { DoBackup(); };
        fl.Controls.Add(btnBackup);

        btnRevert = new Flat("撤销修改", false, 92, 28);
        btnRevert.Click += delegate { RevertAll(); };
        fl.Controls.Add(btnRevert);

        btnRestore = new Flat("还原备份…", false, 106, 28);
        btnRestore.Click += delegate { RestoreBackup(); };
        fl.Controls.Add(btnRestore);
    }
    /* ==================== 存档列表 ==================== */
    void ReloadSaveList()
    {
        List<string> saves = SaveDoc.FindSaves();
        cmbSave.Items.Clear();
        foreach (string s in saves) cmbSave.Items.Add(new SaveItem(s));
        if (cmbSave.Items.Count == 0)
        {
            lblStatus.Text = "没有找到存档。点「打开存档文件…」手动选一个。";
            return;
        }
        cmbSave.SelectedIndex = 0;
    }

    class SaveItem
    {
        public string Path;
        public string Summary = "";
        public SaveItem(string p)
        {
            Path = p;
            try
            {
                FileInfo fi = new FileInfo(p);
                string extra = "";
                try
                {
                    SaveDoc d = SaveDoc.Load(p);
                    object cash = d.GetVal("cash");
                    object wk = d.GetVal("weekTotal");
                    object nm = d.GetVal("flags.player_name");
                    extra = "　—　第" + SaveDoc.Num(wk, 0) + "周 · " +
                            SaveDoc.Num(cash, 0).ToString("0.##") + " · " + SaveDoc.Str(nm);
                }
                catch { }
                Summary = fi.Name + extra + "　(" + fi.LastWriteTime.ToString("MM-dd HH:mm") + ")";
            }
            catch { Summary = System.IO.Path.GetFileName(p); }
        }
        public override string ToString() { return Summary; }
    }

    void OnSaveSelected()
    {
        SaveItem it = cmbSave.SelectedItem as SaveItem;
        if (it == null) return;
        LoadSave(it.Path);
    }

    void LoadSave(string path)
    {
        try
        {
            doc = SaveDoc.Load(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show("读不了这个存档：\n\n" + ex.Message, APPNAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        BuildOverview();
        ShowPage();
        UpdateStatus();
    }

    void OpenFileDialog()
    {
        OpenFileDialog o = new OpenFileDialog();
        o.Title = "选择存档文件";
        o.Filter = "存档 (*.dat)|*.dat|所有文件 (*.*)|*.*";
        o.InitialDirectory = SaveDoc.SaveDirDefault();
        if (o.ShowDialog() == DialogResult.OK) LoadSave(o.FileName);
    }

    /* ==================== 字段辅助 ==================== */
    /* 按文字实际宽度做按钮：换成等宽字体后写死宽度会被截断 */
    static Flat Btn(string text, bool primary)
    {
        Size sz = TextRenderer.MeasureText(text, Theme.Base);
        return new Flat(text, primary, sz.Width + 28, 28);
    }

    Panel     Page(string key)
    {
        content.Controls.Clear();
        return content;
    }
    Card NewGroup(Panel page, string title, int left, int top, int width)
    {
        Card g = new Card(title);
        g.SetBounds(left, top, width, 60);
        page.Controls.Add(g);
        return g;
    }

    int rowY = 46;
    Control AddRow(Card g, string label, string hint, Control c, int w)
    {
        int y = rowY;

        Label l = new Label();
        l.Text = label;
        l.ForeColor = Theme.Muted;
        l.Font = Theme.Base;
        l.SetBounds(18, y + 5, 132, 20);
        g.Controls.Add(l);

        c.Font = Theme.Base;
        c.SetBounds(154, y, w, c is CheckBox ? 24 : 26);
        g.Controls.Add(c);

        if (hint != null && hint.Length > 0)
        {
            Label h = new Label();
            h.Text = hint;
            h.ForeColor = Theme.Faint;
            h.Font = Theme.Small;
            h.SetBounds(154 + w + 12, y + 7, Math.Max(100, g.Width - (154 + w + 34)), 18);
            g.Controls.Add(h);
        }

        rowY += 34;
        return c;
    }
    NumericUpDown NumCtrl(string path, decimal min, decimal max, decimal step, int dec)
    {
        NumericUpDown n = new NumericUpDown();
        n.Minimum = min; n.Maximum = max; n.Increment = step;
        n.DecimalPlaces = dec;
        object v = doc.GetVal(path);
        n.Value = Clamp((decimal)SaveDoc.Num(v, 0), min, max);
        n.Tag = path;
        n.ValueChanged += delegate { if (loading) return; CommitNum(n, path); };
        return n;
    }

    static decimal Clamp(decimal v, decimal lo, decimal hi)
    {
        if (v < lo) return lo;
        if (v > hi) return hi;
        return v;
    }

    void CommitNum(NumericUpDown n, string path)
    {
        object old = doc.GetVal(path);
        object val = (n.DecimalPlaces == 0) ? (object)(long)n.Value : (object)(double)n.Value;
        if (old is int) val = (int)n.Value;
        else if (old is long) val = (long)n.Value;
        else if (old is decimal) val = n.Value;
        else if (old is float) val = (float)n.Value;
        else if (old is double) val = (double)n.Value;
        doc.Commit(path, val, LabelFor(path));
        UpdateStatus();
    }

    TextBox TxtCtrl(string path, int w)
    {
        TextBox t = new TextBox();
        t.Text = SaveDoc.Str(doc.GetVal(path));
        t.Tag = path;
        t.TextChanged += delegate {
            if (loading) return;
            doc.Commit(path, t.Text, LabelFor(path));
            UpdateStatus();
        };
        return t;
    }

    ComboBox SelCtrl(string path, string[] values, string[] labels, bool allowCustom)
    {
        ComboBox c = new ComboBox();
        c.DropDownStyle = ComboBoxStyle.DropDownList;
        c.Tag = path;
        string cur = SaveDoc.Str(doc.GetVal(path));
        bool known = false;
        for (int i = 0; i < values.Length; i++)
        {
            c.Items.Add(new SelItem(values[i], labels[i]));
            if (values[i] == cur) known = true;
        }
        if (allowCustom) c.Items.Add(new SelItem("__custom__", "自定义…"));
        if (!known && cur.Length > 0) { c.Items.Insert(0, new SelItem(cur, cur + "（原值）")); }
        for (int i = 0; i < c.Items.Count; i++)
            if (((SelItem)c.Items[i]).Value == cur) { c.SelectedIndex = i; break; }
        if (c.SelectedIndex < 0 && c.Items.Count > 0) c.SelectedIndex = 0;

        c.SelectedIndexChanged += delegate {
            if (loading) return;
            SelItem si = c.SelectedItem as SelItem;
            if (si == null) return;
            if (si.Value == "__custom__")
            {
                string nv = HotPrompt("输入自定义值：", cur);
                if (nv == null || nv.Length == 0) return;
                doc.Commit(path, nv, LabelFor(path));
            }
            else doc.Commit(path, si.Value, LabelFor(path));
            UpdateStatus();
        };
        return c;
    }

    class SelItem
    {
        public string Value, Text;
        public SelItem(string v, string t) { Value = v; Text = t; }
        public override string ToString() { return Text; }
    }

    CheckBox ChkCtrl(string path, string text)
    {
        CheckBox c = new CheckBox();
        c.Text = text;
        c.Checked = SaveDoc.Bool(doc.GetVal(path));
        c.Tag = path;
        c.CheckedChanged += delegate {
            if (loading) return;
            doc.Commit(path, c.Checked, LabelFor(path));
            UpdateStatus();
        };
        return c;
    }

    Label InfoLabel(string text, int x, int y, int w, bool big)
    {
        Label l = new Label();
        l.Text = text;
        l.SetBounds(x, y, w, big ? 28 : 20);
        if (big) l.Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
        return l;
    }

    /* ==================== 概览 ==================== */
    void BuildOverview()
    {
        Panel page = Page("概览");
        if (doc == null) return;
        loading = true;
        int W = Math.Max(700, host.ClientSize.Width - 40);

        Card g = NewGroup(page, "当前存档", 10, 10, W);
        string g0 = SaveDoc.Str(doc.GetVal("flags.gender"));
        string gLabel = g0 == "female" ? "女" : (g0 == "male" ? "男" : g0);
        object y1 = doc.GetVal("year"), m1 = doc.GetVal("month"), d1 = doc.GetVal("day");
        string date = (y1 == null ? "—" : y1 + "-" + Pad(SaveDoc.Num(m1, 0)) + "-" + Pad(SaveDoc.Num(d1, 0)));

        string[][] rows = new string[][]
        {
            new string[] { "文件", doc.FileName },
            new string[] { "位置", doc.FilePath },
            new string[] { "姓名", SaveDoc.Str(doc.GetVal("flags.player_name")) },
            new string[] { "性别", gLabel },
            new string[] { "金钱", SaveDoc.Num(doc.GetVal("cash"),0).ToString("0.##") + "　总资产 " + SaveDoc.Num(doc.GetVal("total_assets"),0).ToString("0.##") },
            new string[] { "日期", date + "　" + SlotName(SaveDoc.Str(doc.GetVal("day_slot"))) },
            new string[] { "进度", "第 " + SaveDoc.Num(doc.GetVal("weekTotal"),0) + " 周　第 " +
                                  SaveDoc.Num(doc.GetVal("full_game_foundation.run.semester_index"),0) + " 学期　已完成 " +
                                  SaveDoc.Num(doc.GetVal("completed_semesters"),0) + " 个学期" },
            new string[] { "成绩", "GPA " + SaveDoc.Num(doc.GetVal("gpa"),0).ToString("0.##") +
                                  "　本学期 " + SaveDoc.Num(doc.GetVal("semester_gpa"),0).ToString("0.##") },
            new string[] { "身体", "健康 " + SaveDoc.Num(doc.GetVal("health"),0) + "　心态 " +
                                  SaveDoc.Num(doc.GetVal("mental"),0) + "　饱食 " + SaveDoc.Num(doc.GetVal("hunger"),0) },
            new string[] { "数据版本", SaveDoc.Str(doc.GetVal("save_schema_version")) }
        };
        int yy = 52;
        foreach (string[] r in rows)
        {
            Label a = new Label(); a.Text = r[0]; a.SetBounds(18, yy, 96, 20);
            a.ForeColor = Color.FromArgb(110, 110, 110);
            g.Controls.Add(a);
            Label b = new Label(); b.Text = r[1]; b.SetBounds(120, yy, W - 150, 20);
            g.Controls.Add(b);
            yy += 24;
        }
        g.Height = yy + 14;
        loading = false;
    }

    static string Pad(double v) { return v < 10 ? "0" + (int)v : ((int)v).ToString(); }
    static string SlotName(string s)
    {
        if (s == "morning") return "上午";
        if (s == "afternoon") return "下午";
        if (s == "evening") return "晚上";
        if (s == "night") return "深夜";
        return s;
    }

    /* ==================== 金钱 ==================== */
    void BuildMoney()
    {
        Panel page = Page("金钱资产");
        if (doc == null) return;
        loading = true; rowY = 46;
        int W = Math.Max(700, host.ClientSize.Width - 40);

        Card g = NewGroup(page, "现金", 10, 10, W);
        AddRow(g, "金钱", "主界面显示的余额", NumCtrl("cash", 0, 1000000000m, 50m, 2), 160);
        AddRow(g, "总资产", "现金加股票市值", NumCtrl("total_assets", 0, 1000000000m, 1m, 2), 160);

        FlowLayoutPanel fl = new FlowLayoutPanel();
        fl.SetBounds(14, rowY + 4, W - 40, 34);
        string[] quick = new string[] { "1000", "10000", "100000", "1000000" };
        foreach (string q in quick)
        {
            Flat b = Btn("设为 " + q, false);
            string qq = q;
            b.Click += delegate { SetMoney(double.Parse(qq)); };
            fl.Controls.Add(b);
        }
        Flat b1 = Btn("+1000", false);
        b1.Click += delegate { SetMoney(SaveDoc.Num(doc.GetVal("cash"), 0) + 1000); };
        fl.Controls.Add(b1);
        Flat b2 = Btn("+10000", false);
        b2.Click += delegate { SetMoney(SaveDoc.Num(doc.GetVal("cash"), 0) + 10000); };
        fl.Controls.Add(b2);
        Flat b3 = Btn("同步总资产", false);
        b3.Click += delegate {
            double cash = SaveDoc.Num(doc.GetVal("cash"), 0);
            double units = SaveDoc.Num(doc.GetVal("stock_units"), 0);
            double price = SaveDoc.Num(doc.GetVal("stock_price"), 0);
            doc.Commit("total_assets", Math.Round((cash + units * price) * 100) / 100, "总资产");
            ShowPage(); UpdateStatus();
        };
        fl.Controls.Add(b3);
        g.Controls.Add(fl);
        g.Height = rowY + 46;

        rowY = 46;
        Card g2 = NewGroup(page, "其他收入", 10, g.Bottom + 12, W);
        AddRow(g2, "父母每月生活费", null, NumCtrl("flags.parent_support_monthly_amount", 0, 1000000m, 100m, 2), 160);
        AddRow(g2, "累计打工收入", null, NumCtrl("flags.work_income_total_v1", 0, 100000000m, 1m, 2), 160);
        AddRow(g2, "接受父母资助", null, ChkCtrl("flags.parent_support_enabled", "开"), 200);
        AddRow(g2, "已拒绝父母资助", null, ChkCtrl("flags.parent_support_declined", "已拒绝"), 200);
        g2.Height = rowY + 12;

        rowY = 46;
        Card g3 = NewGroup(page, "股票", 10, g2.Bottom + 12, W);
        object hold = doc.GetVal("flags.stock_market.portfolio.holdings");
        Dictionary<string, object> hd = hold as Dictionary<string, object>;
        if (hd != null && hd.Count > 0)
        {
            DataGridView dgv = new DataGridView();
        GridStyle.Apply(dgv);
            dgv.SetBounds(14, rowY, W - 40, 130);
            dgv.AllowUserToAddRows = false;
            dgv.RowHeadersVisible = false;
            dgv.Columns.Add("sym", "代码");
            dgv.Columns.Add("units", "持仓");
            dgv.Columns.Add("cost", "成本");
            dgv.Columns.Add("pnl", "已实现盈亏");
            foreach (KeyValuePair<string, object> kv in hd)
            {
                int i = dgv.Rows.Add(kv.Key,
                    SaveDoc.Num(doc.GetVal("flags.stock_market.portfolio.holdings." + kv.Key), 0),
                    SaveDoc.Num(doc.GetVal("flags.stock_market.portfolio.avg_cost." + kv.Key), 0),
                    SaveDoc.Num(doc.GetVal("flags.stock_market.portfolio.realized_pnl." + kv.Key), 0));
                dgv.Rows[i].Tag = kv.Key;
            }
            dgv.CellValueChanged += delegate(object s, DataGridViewCellEventArgs e) {
                if (loading || e.RowIndex < 0 || e.ColumnIndex < 1) return;
                string sym = Convert.ToString(dgv.Rows[e.RowIndex].Tag);
                string[] kinds = new string[] { "", "holdings", "avg_cost", "realized_pnl" };
                string path = "flags.stock_market.portfolio." + kinds[e.ColumnIndex] + "." + sym;
                double v;
                double.TryParse(Convert.ToString(dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value), out v);
                doc.Commit(path, v, "股票 " + sym);
                UpdateStatus();
            };
            g3.Controls.Add(dgv);
            dgv.ReadOnly = false;
            foreach (DataGridViewColumn c in dgv.Columns) if (c.Index > 0) c.ReadOnly = false;
            dgv.Columns[0].ReadOnly = true;
            rowY += 140;
        }
        else
        {
            Label l = new Label(); l.Text = "存档里没有股票数据"; l.SetBounds(14, rowY, 400, 20);
            l.ForeColor = Color.FromArgb(140, 140, 140);
            g3.Controls.Add(l);
            rowY += 30;
        }
        AddRow(g3, "汇总持仓", null, NumCtrl("stock_units", 0, 100000000m, 1m, 0), 160);
        AddRow(g3, "当前股价", null, NumCtrl("stock_price", 0, 1000000m, 0.1m, 2), 160);
        AddRow(g3, "汇总成本", null, NumCtrl("stock_avg_cost", 0, 1000000m, 0.1m, 2), 160);
        g3.Height = rowY + 12;

        loading = false;
    }

    void SetMoney(double v)
    {
        if (v < 0) v = 0;
        doc.Commit("cash", v, "金钱");
        doc.Commit("total_assets", v, "总资产");
        ShowPage();
        UpdateStatus();
    }

    /* ==================== 角色 ==================== */
    void BuildPerson()
    {
        Panel page = Page("角色");
        if (doc == null) return;
        loading = true; rowY = 46;
        int W = Math.Max(700, host.ClientSize.Width - 40);

        Card g = NewGroup(page, "基本信息", 10, 10, W);
        AddRow(g, "姓名", "出现在对话和存档列表里", TxtCtrl("flags.player_name", 160), 200);
        AddRow(g, "性别", "部分剧情和立绘会用到",
            SelCtrl("flags.gender", new string[] { "female", "male" }, new string[] { "女", "男" }, true), 160);
        AddRow(g, "性取向", "影响可攻略对象",
            SelCtrl("flags.orientation", new string[] { "bi", "hetero", "homo", "ace" },
                    new string[] { "双性恋", "异性恋", "同性恋", "无性恋" }, true), 160);
        g.Height = rowY + 12;

        rowY = 46;
        Card g2 = NewGroup(page, "开档设定（改动后可能和已有剧情文本对不上）", 10, g.Bottom + 12, W);
        AddRow(g2, "专业", null, TxtCtrl("flags.major", 160), 200);
        AddRow(g2, "地区", "us / uk 等", TxtCtrl("region", 160), 200);
        AddRow(g2, "留学国家", null, TxtCtrl("flags.intro_country", 160), 200);
        AddRow(g2, "玩法倾向", null,
            SelCtrl("flags.life_mode_primary", new string[] { "money", "study", "social", "health" },
                    new string[] { "金钱", "学业", "社交", "健康" }, true), 160);
        g2.Height = rowY + 12;
        loading = false;
    }

    /* ==================== 人物关系 ==================== */
    DataGridView relGrid;
    void BuildRel()
    {
        Panel page = Page("人物关系");
        if (doc == null) return;
        loading = true;
        int W = Math.Max(700, host.ClientSize.Width - 40);

        Dictionary<string, object> profs =
            doc.GetVal("state_modules.characters.profiles") as Dictionary<string, object>;
        if (profs == null) profs = doc.GetVal("flags.character_profiles.profiles") as Dictionary<string, object>;
        if (profs == null || profs.Count == 0)
        {
            Label l = new Label();
            l.Text = "这个存档里没有人物数据。";
            l.SetBounds(20, 20, 400, 24);
            page.Controls.Add(l);
            loading = false;
            return;
        }

        Card g = NewGroup(page, "批量操作", 10, 10, W);
        FlowLayoutPanel fl = new FlowLayoutPanel();
        fl.SetBounds(16, 50, W - 44, 34);
        string[][] bulk = new string[][]
        {
            new string[] { "好感全部拉满", "max" },
            new string[] { "全部解锁并加为联系人", "unlock" },
            new string[] { "全部设为朋友", "friend" },
            new string[] { "全部重置为认识", "reset" },
            new string[] { "好感全部归零", "zero" }
        };
        foreach (string[] b in bulk)
        {
            Flat btn = Btn(b[0], false);
            string mode = b[1];
            btn.Click += delegate { BulkRel(mode); };
            fl.Controls.Add(btn);
        }
        g.Controls.Add(fl);
        g.Height = 92;

        Label tip = new Label();
        tip.Text = "好感是数值，关系是阶段，两个一起改比较自然。0-14 认识，15-59 朋友，60 以上挚友。";
        tip.ForeColor = Color.FromArgb(110, 110, 110);
        tip.SetBounds(20, g.Bottom + 8, W - 40, 20);
        page.Controls.Add(tip);

        relGrid = new DataGridView();
        relGrid.SetBounds(10, g.Bottom + 32, W, Math.Max(320, content.ClientSize.Height - g.Bottom - 52));
        relGrid.AllowUserToAddRows = false;
        relGrid.RowHeadersVisible = false;
        relGrid.EditMode = DataGridViewEditMode.EditOnEnter;
        GridStyle.Apply(relGrid);

        relGrid.Columns.Add("nm", "角色");
        relGrid.Columns[0].ReadOnly = true;
        relGrid.Columns.Add("rel", "好感");
        DataGridViewComboBoxColumn fc = new DataGridViewComboBoxColumn();
        fc.HeaderText = "关系"; fc.Name = "fs";
        fc.Items.AddRange("认识", "朋友", "挚友");
        relGrid.Columns.Add(fc);
        relGrid.Columns.Add("inti", "亲密度");
        DataGridViewComboBoxColumn rc = new DataGridViewComboBoxColumn();
        rc.HeaderText = "恋爱"; rc.Name = "rs";
        rc.Items.AddRange("无", "暧昧中", "恋人");
        relGrid.Columns.Add(rc);
        string[][] boolCols = new string[][]
        {
            new string[] { "unl", "解锁", "60" },
            new string[] { "known", "已认识", "72" },
            new string[] { "seen", "见过", "60" },
            new string[] { "cont", "有联系方式", "100" }
        };
        foreach (string[] bc in boolCols)
        {
            DataGridViewCheckBoxColumn ck = new DataGridViewCheckBoxColumn();
            ck.Name = bc[0];
            ck.HeaderText = bc[1];
            ck.Width = int.Parse(bc[2]);
            ck.SortMode = DataGridViewColumnSortMode.NotSortable;
            relGrid.Columns.Add(ck);
        }
        relGrid.Columns["nm"].Width = 150;
        relGrid.Columns["rel"].Width = 70;
        relGrid.Columns["fs"].Width = 90;
        relGrid.Columns["inti"].Width = 70;
        relGrid.Columns["rs"].Width = 90;

        foreach (KeyValuePair<string, object> kv in profs)
        {
            string id = kv.Key;
            Dictionary<string, object> p = kv.Value as Dictionary<string, object>;
            if (p == null) continue;
            Dictionary<string, object> prog =
                doc.GetVal("flags.person_relationship_progress." + id) as Dictionary<string, object>;

            int i = relGrid.Rows.Add();
            DataGridViewRow r = relGrid.Rows[i];
            r.Tag = id;
            r.Cells[0].Value = SaveDoc.Str(p.ContainsKey("display_name") ? p["display_name"] : id);
            r.Cells[1].Value = SaveDoc.Num(p.ContainsKey("relationship") ? p["relationship"] : null, 0);
            r.Cells[2].Value = RelStageName(prog, "friendship_stage");
            r.Cells[3].Value = prog != null ? SaveDoc.Num(prog.ContainsKey("intimacy") ? prog["intimacy"] : null, 0) : 0;
            r.Cells[4].Value = RomStageName(prog, "romance_stage");
            r.Cells[5].Value = p.ContainsKey("unlocked") && SaveDoc.Bool(p["unlocked"]);
            r.Cells[6].Value = doc.GetVal("flags.person_known_level_" + id) != null;
            r.Cells[7].Value = SaveDoc.Bool(doc.GetVal("flags.person_seen_" + id));
            r.Cells[8].Value = SaveDoc.Bool(doc.GetVal("flags.person_contact_added_" + id));
        }

        relGrid.CurrentCellDirtyStateChanged += delegate {
            if (relGrid.IsCurrentCellDirty) relGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        relGrid.CellValueChanged += delegate(object s, DataGridViewCellEventArgs e) {
            if (loading || e.RowIndex < 0) return;
            string id = Convert.ToString(relGrid.Rows[e.RowIndex].Tag);
            DataGridViewRow r = relGrid.Rows[e.RowIndex];
            switch (e.ColumnIndex)
            {
                case 1:
                    doc.Commit("state_modules.characters.profiles." + id + ".relationship",
                        SaveDoc.Num(r.Cells[1].Value, 0), "好感 · " + r.Cells[0].Value);
                    break;
                case 2:
                    doc.CommitEntry("flags.person_relationship_progress." + id, "friendship_stage",
                        StageValue(Convert.ToString(r.Cells[2].Value)), "关系", RelShape());
                    break;
                case 3:
                    doc.CommitEntry("flags.person_relationship_progress." + id, "intimacy",
                        SaveDoc.Num(r.Cells[3].Value, 0), "亲密度", RelShape());
                    break;
                case 4:
                    doc.CommitEntry("flags.person_relationship_progress." + id, "romance_stage",
                        RomValue(Convert.ToString(r.Cells[4].Value)), "恋爱", RelShape());
                    break;
                case 5:
                    doc.Commit("state_modules.characters.profiles." + id + ".unlocked",
                        Convert.ToBoolean(r.Cells[5].Value), "解锁 · " + r.Cells[0].Value);
                    break;
                case 6:
                    doc.Commit("flags.person_known_level_" + id,
                        Convert.ToBoolean(r.Cells[6].Value) ? (object)1 : null, "已认识 · " + r.Cells[0].Value);
                    break;
                case 7:
                    doc.Commit("flags.person_seen_" + id,
                        Convert.ToBoolean(r.Cells[7].Value), "见过 · " + r.Cells[0].Value);
                    break;
                case 8:
                    doc.Commit("flags.person_contact_added_" + id,
                        Convert.ToBoolean(r.Cells[8].Value), "联系方式 · " + r.Cells[0].Value);
                    break;
            }
            UpdateStatus();
        };
        page.Controls.Add(relGrid);
        loading = false;
    }

    static Dictionary<string, object> RelShape()
    {
        Dictionary<string, object> d = new Dictionary<string, object>();
        d["friendship_stage"] = "acquaintance";
        d["intimacy"] = 0;
        d["romance_stage"] = "none";
        return d;
    }
    static string RelStageName(Dictionary<string, object> p, string k)
    {
        string v = p != null && p.ContainsKey(k) ? SaveDoc.Str(p[k]) : "acquaintance";
        if (v == "friend") return "朋友";
        if (v == "close_friend") return "挚友";
        return "认识";
    }
    static string StageValue(string cn)
    {
        if (cn == "朋友") return "friend";
        if (cn == "挚友") return "close_friend";
        return "acquaintance";
    }
    static string RomStageName(Dictionary<string, object> p, string k)
    {
        string v = p != null && p.ContainsKey(k) ? SaveDoc.Str(p[k]) : "none";
        if (v == "flirting") return "暧昧中";
        if (v == "partner") return "恋人";
        return "无";
    }
    static string RomValue(string cn)
    {
        if (cn == "暧昧中") return "flirting";
        if (cn == "恋人") return "partner";
        return "none";
    }

    void BulkRel(string mode)
    {
        Dictionary<string, object> profs =
            doc.GetVal("state_modules.characters.profiles") as Dictionary<string, object>;
        if (profs == null) return;
        foreach (KeyValuePair<string, object> kv in profs)
        {
            string id = kv.Key;
            Dictionary<string, object> p = kv.Value as Dictionary<string, object>;
            string nm = p != null && p.ContainsKey("display_name") ? SaveDoc.Str(p["display_name"]) : id;
            if (mode == "max")
            {
                doc.Commit("state_modules.characters.profiles." + id + ".relationship", 100, "好感 · " + nm);
                doc.Commit("state_modules.characters.profiles." + id + ".unlocked", true, "解锁 · " + nm);
                doc.CommitEntry("flags.person_relationship_progress." + id, "friendship_stage", "close_friend", "关系", RelShape());
                doc.CommitEntry("flags.person_relationship_progress." + id, "intimacy", 100, "亲密度", RelShape());
                doc.Commit("flags.person_known_level_" + id, 1, "已认识 · " + nm);
                doc.Commit("flags.person_seen_" + id, true, "见过 · " + nm);
                doc.Commit("flags.person_contact_added_" + id, true, "联系方式 · " + nm);
            }
            else if (mode == "unlock")
            {
                doc.Commit("state_modules.characters.profiles." + id + ".unlocked", true, "解锁 · " + nm);
                doc.Commit("flags.person_known_level_" + id, 1, "已认识 · " + nm);
                doc.Commit("flags.person_seen_" + id, true, "见过 · " + nm);
                doc.Commit("flags.person_contact_added_" + id, true, "联系方式 · " + nm);
            }
            else if (mode == "friend")
            {
                doc.Commit("state_modules.characters.profiles." + id + ".relationship", 40, "好感 · " + nm);
                doc.CommitEntry("flags.person_relationship_progress." + id, "friendship_stage", "friend", "关系", RelShape());
            }
            else if (mode == "reset")
            {
                doc.Commit("state_modules.characters.profiles." + id + ".relationship", 0, "好感 · " + nm);
                doc.CommitEntry("flags.person_relationship_progress." + id, "friendship_stage", "acquaintance", "关系", RelShape());
                doc.CommitEntry("flags.person_relationship_progress." + id, "intimacy", 0, "亲密度", RelShape());
                doc.CommitEntry("flags.person_relationship_progress." + id, "romance_stage", "none", "恋爱", RelShape());
            }
            else if (mode == "zero")
                doc.Commit("state_modules.characters.profiles." + id + ".relationship", 0, "好感 · " + nm);
        }
        BuildRel();
        UpdateStatus();
    }

    /* ==================== 任务 ==================== */
    static readonly string[][] OnbSteps = new string[][]
    {
        new string[] { "timetable", "查看课表" },
        new string[] { "nap_to_class", "小睡后去上课" },
        new string[] { "first_contact", "添加第一个联系人" },
        new string[] { "first_class", "上完第一节课" },
        new string[] { "first_job", "找到第一份工作" },
        new string[] { "first_shift", "完成第一次上班" },
        new string[] { "money_bills", "支付账单" },
        new string[] { "sleep_night", "夜间睡觉" },
        new string[] { "tour_time", "时间系统引导" }
    };
    static readonly string[][] OnbStages = new string[][]
    {
        new string[] { "first_day", "第一天" },
        new string[] { "first_job", "第一份工作" }
    };

    void BuildQuest()
    {
        Panel page = Page("任务进度");
        if (doc == null) return;
        loading = true;
        int W = Math.Max(700, host.ClientSize.Width - 40);
        int top = 10;

        /* --- 新手引导任务 --- */
        Card g = NewGroup(page, "新手引导任务", 10, top, W);
        Dictionary<string, object> latched = doc.GetVal("flags.onboarding_latched") as Dictionary<string, object>;
        System.Collections.IList rewarded = doc.GetVal("flags.onboarding_tasks_rewarded") as System.Collections.IList;
        int yy = 52;
        foreach (string[] s in OnbSteps)
        {
            GreenCheck c = new GreenCheck();
            c.Text = s[1];
            c.SetBounds(14, yy, 180, 22);
            bool on = latched != null && latched.ContainsKey("step:" + s[0]) && SaveDoc.Bool(latched["step:" + s[0]]);
            c.Checked = on;
            string id = s[0];
            c.CheckedChanged += delegate {
                if (loading) return;
                Dictionary<string, object> lt = doc.GetVal("flags.onboarding_latched") as Dictionary<string, object>;
                Dictionary<string, object> nw = SaveDoc.Clone(lt);
                nw["step:" + id] = c.Checked;
                doc.Commit("flags.onboarding_latched", nw, "引导任务 " + id);
                UpdateStatus();
            };
            g.Controls.Add(c);

            bool rw = false;
            if (rewarded != null) foreach (object o in rewarded) if (SaveDoc.Str(o) == s[0]) rw = true;
            Label l = new Label();
            l.Text = rw ? "已发放" : "未发放";
            l.ForeColor = rw ? Color.FromArgb(15, 123, 63) : Color.FromArgb(140, 140, 140);
            l.SetBounds(210, yy + 2, 70, 20);
            g.Controls.Add(l);
            yy += 26;
        }
        Flat ba = Btn("全部完成", false); ba.SetBounds(16, yy + 10, ba.Width, 28);
        ba.Click += delegate {
            Dictionary<string, object> lt = doc.GetVal("flags.onboarding_latched") as Dictionary<string, object>;
            Dictionary<string, object> nw = SaveDoc.Clone(lt);
            List<object> rw = new List<object>();
            foreach (string[] s in OnbSteps)
            {
                nw["step:" + s[0]] = true;
                if (s[0] != "tour_time") rw.Add(s[0]);
            }
            doc.Commit("flags.onboarding_latched", nw, "引导任务");
            doc.Commit("flags.onboarding_tasks_rewarded", rw.ToArray(), "引导任务奖励");
            List<object> st = new List<object>();
            foreach (string[] s in OnbStages) st.Add(s[0]);
            doc.Commit("flags.onboarding_stages_rewarded", st.ToArray(), "引导阶段奖励");
            BuildQuest(); UpdateStatus();
        };
        g.Controls.Add(ba);
        Flat br = Btn("全部重置", false); br.SetBounds(16 + ba.Width + 10, yy + 10, br.Width, 28);
        br.Click += delegate {
            Dictionary<string, object> lt = doc.GetVal("flags.onboarding_latched") as Dictionary<string, object>;
            Dictionary<string, object> nw = SaveDoc.Clone(lt);
            foreach (string[] s in OnbSteps) nw["step:" + s[0]] = false;
            doc.Commit("flags.onboarding_latched", nw, "引导任务");
            doc.Commit("flags.onboarding_tasks_rewarded", new object[0], "引导任务奖励");
            doc.Commit("flags.onboarding_stages_rewarded", new object[0], "引导阶段奖励");
            BuildQuest(); UpdateStatus();
        };
        g.Controls.Add(br);
        g.Height = yy + 42;
        top = g.Bottom + 12;

        /* --- 引导阶段 --- */
        Card g2 = NewGroup(page, "引导阶段", 10, top, W);
        System.Collections.IList stages = doc.GetVal("flags.onboarding_stages_rewarded") as System.Collections.IList;
        yy = 52;
        foreach (string[] s in OnbStages)
        {
            GreenCheck c = new GreenCheck();
            c.Text = s[1];
            c.SetBounds(14, yy, 180, 22);
            bool on = false;
            if (stages != null) foreach (object o in stages) if (SaveDoc.Str(o) == s[0]) on = true;
            c.Checked = on;
            string id = s[0];
            c.CheckedChanged += delegate {
                if (loading) return;
                System.Collections.IList cur = doc.GetVal("flags.onboarding_stages_rewarded") as System.Collections.IList;
                List<object> l = new List<object>();
                if (cur != null) foreach (object o in cur) l.Add(o);
                if (c.Checked) { if (!l.Contains(id)) l.Add(id); }
                else l.Remove(id);
                doc.Commit("flags.onboarding_stages_rewarded", l.ToArray(), "引导阶段 " + id);
                UpdateStatus();
            };
            g2.Controls.Add(c);
            yy += 26;
        }
        g2.Height = yy + 12;
        top = g2.Bottom + 12;

        /* --- 作业与考试 --- */
        Card g3 = NewGroup(page, "作业与考试", 10, top, W);
        System.Collections.IList nodes = doc.GetVal("academic_nodes") as System.Collections.IList;
        DataGridView dgv = new DataGridView();
        GridStyle.Apply(dgv);
        dgv.SetBounds(16, 52, W - 44, Math.Max(200, content.ClientSize.Height - top - 56));
        dgv.AllowUserToAddRows = false;
        dgv.RowHeadersVisible = false;
        dgv.Columns.Add("nm", "项目");
        dgv.Columns[0].ReadOnly = true;
        dgv.Columns[0].Width = 320;
        dgv.Columns.Add("prog", "进度");
        dgv.Columns.Add("req", "需要");
        dgv.Columns["req"].ReadOnly = true;
        dgv.Columns["req"].Width = 70;
        DataGridViewCheckBoxColumn subC = new DataGridViewCheckBoxColumn();
        subC.Name = "sub"; subC.HeaderText = "已提交"; subC.Width = 70;
        subC.SortMode = DataGridViewColumnSortMode.NotSortable;
        dgv.Columns.Add(subC);
        DataGridViewCheckBoxColumn resC = new DataGridViewCheckBoxColumn();
        resC.Name = "res"; resC.HeaderText = "已结算"; resC.Width = 70;
        resC.SortMode = DataGridViewColumnSortMode.NotSortable;
        dgv.Columns.Add(resC);
        if (nodes != null)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                Dictionary<string, object> n = nodes[i] as Dictionary<string, object>;
                if (n == null) continue;
                int r = dgv.Rows.Add();
                dgv.Rows[r].Tag = i;
                dgv.Rows[r].Cells[0].Value = SaveDoc.Str(n.ContainsKey("label_zh") ? n["label_zh"] : (n.ContainsKey("label") ? n["label"] : n["id"]));
                dgv.Rows[r].Cells[1].Value = SaveDoc.Num(n.ContainsKey("work_progress") ? n["work_progress"] : null, 0);
                dgv.Rows[r].Cells[2].Value = SaveDoc.Num(n.ContainsKey("required") ? n["required"] : null, 0);
                dgv.Rows[r].Cells[3].Value = n.ContainsKey("submitted") && SaveDoc.Bool(n["submitted"]);
                dgv.Rows[r].Cells[4].Value = n.ContainsKey("resolved") && SaveDoc.Bool(n["resolved"]);
            }
        }
        dgv.CurrentCellDirtyStateChanged += delegate {
            if (dgv.IsCurrentCellDirty) dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        dgv.CellValueChanged += delegate(object s, DataGridViewCellEventArgs e) {
            if (loading || e.RowIndex < 0) return;
            int i = Convert.ToInt32(dgv.Rows[e.RowIndex].Tag);
            if (e.ColumnIndex == 1)
                doc.Commit("academic_nodes." + i + ".work_progress",
                    SaveDoc.Num(dgv.Rows[e.RowIndex].Cells[1].Value, 0), "作业进度");
            else if (e.ColumnIndex == 3)
                doc.Commit("academic_nodes." + i + ".submitted",
                    Convert.ToBoolean(dgv.Rows[e.RowIndex].Cells[3].Value), "已提交");
            else if (e.ColumnIndex == 4)
                doc.Commit("academic_nodes." + i + ".resolved",
                    Convert.ToBoolean(dgv.Rows[e.RowIndex].Cells[4].Value), "已结算");
            UpdateStatus();
        };
        g3.Controls.Add(dgv);
        Flat bfa = Btn("完成所有作业", false); bfa.SetBounds(16, dgv.Bottom + 12, bfa.Width, 28);
        bfa.Click += delegate {
            System.Collections.IList ns = doc.GetVal("academic_nodes") as System.Collections.IList;
            if (ns != null)
                for (int i = 0; i < ns.Count; i++)
                {
                    Dictionary<string, object> n = ns[i] as Dictionary<string, object>;
                    if (n == null) continue;
                    if (SaveDoc.Str(n.ContainsKey("type") ? n["type"] : "").ToLowerInvariant() == "assignment")
                    {
                        doc.Commit("academic_nodes." + i + ".work_progress",
                            SaveDoc.Num(n.ContainsKey("required") ? n["required"] : null, 0), "作业进度");
                        doc.Commit("academic_nodes." + i + ".submitted", true, "已提交");
                    }
                }
            BuildQuest(); UpdateStatus();
        };
        g3.Controls.Add(bfa);
        Flat bfp = Btn("所有进度填满", false); bfp.SetBounds(16 + bfa.Width + 10, dgv.Bottom + 12, bfp.Width, 28);
        bfp.Click += delegate {
            System.Collections.IList ns = doc.GetVal("academic_nodes") as System.Collections.IList;
            if (ns != null)
                for (int i = 0; i < ns.Count; i++)
                {
                    Dictionary<string, object> n = ns[i] as Dictionary<string, object>;
                    if (n == null) continue;
                    doc.Commit("academic_nodes." + i + ".work_progress",
                        SaveDoc.Num(n.ContainsKey("required") ? n["required"] : null, 0), "作业进度");
                }
            BuildQuest(); UpdateStatus();
        };
        g3.Controls.Add(bfp);
        g3.Height = 240;
        top = g3.Bottom + 12;

        /* --- 打工剧情线 --- */
        Card g4 = NewGroup(page, "打工剧情线", 10, top, W);
        Dictionary<string, object> sl = doc.GetVal("flags.job_storylines_v1") as Dictionary<string, object>;
        int row = 52;
        if (sl != null && sl.Count > 0)
        {
            foreach (KeyValuePair<string, object> kv in sl)
            {
                Dictionary<string, object> s = kv.Value as Dictionary<string, object>;
                if (s == null) continue;
                Dictionary<string, object> ch = s.ContainsKey("choices") ? s["choices"] as Dictionary<string, object> : null;
                int total = ch != null ? ch.Count : 0;
                System.Collections.IList done = s.ContainsKey("completed_stages") ? s["completed_stages"] as System.Collections.IList : null;
                int dn = done != null ? done.Count : 0;

                Label l = new Label();
                l.Text = kv.Key + "　" + dn + " / " + total + " 阶段　结局 " +
                         SaveDoc.Str(s.ContainsKey("ending_id") ? s["ending_id"] : "未定");
                l.SetBounds(14, row + 2, W - 200, 20);
                g4.Controls.Add(l);

                Flat b = Btn("全部完成", false);
                b.SetBounds(W - b.Width - 16, row - 2, b.Width, 28);
                        string key = kv.Key;
                b.Click += delegate { FinishStoryline(key); };
                g4.Controls.Add(b);
                row += 30;
            }
        }
        else
        {
            Label l = new Label(); l.Text = "还没触发打工剧情"; l.SetBounds(14, row, 300, 20);
            l.ForeColor = Color.FromArgb(140, 140, 140);
            g4.Controls.Add(l);
            row += 30;
        }
        g4.Height = row + 12;
        top = g4.Bottom + 12;

        /* --- 时间与阶段 --- */
        rowY = 46;
        Card g5 = NewGroup(page, "时间与阶段（改动容易让日历对不上，先备份）", 10, top, W);
        AddRow(g5, "总周数", null, NumCtrl("weekTotal", 1, 128, 1, 0), 120);
        AddRow(g5, "日历周", null, NumCtrl("flags.formal_calendar_week", 1, 400, 1, 0), 120);
        AddRow(g5, "学期内周数", null, NumCtrl("semester_week", 1, 16, 1, 0), 120);
        AddRow(g5, "已完成学期", null, NumCtrl("completed_semesters", 0, 8, 1, 0), 120);
        AddRow(g5, "当前学期", null, NumCtrl("full_game_foundation.run.semester_index", 1, 9, 1, 0), 120);
        Flat bs = Btn("同步时间字段", false); bs.SetBounds(14, rowY + 6, bs.Width, 28);
        bs.Click += delegate {
            object y = doc.GetVal("year"), m = doc.GetVal("month"), d = doc.GetVal("day");
            if (y != null && m != null && d != null)
                doc.Commit("formal_calendar.current_date",
                    (int)SaveDoc.Num(y, 0) + "-" + Pad(SaveDoc.Num(m, 0)) + "-" + Pad(SaveDoc.Num(d, 0)), "日历日期");
            doc.Commit("flags.formal_calendar_week", (long)SaveDoc.Num(doc.GetVal("weekTotal"), 1), "日历周");
            doc.Commit("full_game_foundation.run.semester_index",
                (long)(SaveDoc.Num(doc.GetVal("completed_semesters"), 0) + 1), "当前学期");
            BuildQuest(); UpdateStatus();
        };
        g5.Controls.Add(bs);
        g5.Height = rowY + 44;
        loading = false;
    }

    void FinishStoryline(string key)
    {
        Dictionary<string, object> s = doc.GetVal("flags.job_storylines_v1." + key) as Dictionary<string, object>;
        if (s == null) return;
        Dictionary<string, object> ch = s.ContainsKey("choices") ? s["choices"] as Dictionary<string, object> : null;
        if (ch == null || ch.Count == 0) { MessageBox.Show("这条剧情线没有阶段数据。", APPNAME); return; }
        List<string> keys = new List<string>();
        foreach (KeyValuePair<string, object> kv in ch) keys.Add(kv.Key);
        keys.Sort();
        List<object> all = new List<object>();
        foreach (string k in keys) all.Add(k);
        Dictionary<string, object> nw = SaveDoc.Clone(s);
        nw["completed_stages"] = all.ToArray();
        nw["last_stage_id"] = keys[keys.Count - 1];
        doc.Commit("flags.job_storylines_v1." + key, nw, "剧情线 " + key);
        BuildQuest(); UpdateStatus();
    }

    /* ==================== 学业工作 ==================== */
    void BuildStudy()
    {
        Panel page = Page("学业工作");
        if (doc == null) return;
        loading = true; rowY = 46;
        int W = Math.Max(700, host.ClientSize.Width - 40);

        Card g = NewGroup(page, "成绩", 10, 10, W);
        AddRow(g, "综合 GPA", "满分 4.0", NumCtrl("gpa", 0, 4, 0.01m, 2), 120);
        AddRow(g, "本学期 GPA", null, NumCtrl("semester_gpa", 0, 4, 0.01m, 2), 120);
        AddRow(g, "知识", null, NumCtrl("flags.academic_knowledge", 0, 100000m, 1m, 1), 120);
        AddRow(g, "声望", null, NumCtrl("reputation", 0, 100000m, 1m, 0), 120);
        g.Height = rowY + 12;

        rowY = 46;
        Card g2 = NewGroup(page, "成长属性", 10, g.Bottom + 12, W);
        AddRow(g2, "魅力", null, NumCtrl("flags.growth_axis.charm", 0, 100000m, 1m, 1), 120);
        AddRow(g2, "情商", null, NumCtrl("flags.growth_axis.eq", 0, 100000m, 1m, 1), 120);
        AddRow(g2, "知识", "和上面的知识保持一致", NumCtrl("flags.growth_axis.knowledge", 0, 100000m, 1m, 1), 120);
        AddRow(g2, "语言", null, NumCtrl("flags.growth_axis.language", 0, 100000m, 1m, 1), 120);
        g2.Height = rowY + 12;

        rowY = 46;
        Dictionary<string, object> job = doc.GetVal("current_job") as Dictionary<string, object>;
        string title = job != null && job.Count > 0
            ? SaveDoc.Str(job.ContainsKey("title") ? job["title"] : "") + "　" + SaveDoc.Str(job.ContainsKey("company") ? job["company"] : "")
            : "当前没有工作。";
        Card g3 = NewGroup(page, "当前工作　" + title, 10, g2.Bottom + 12, W);
        if (job != null && job.Count > 0)
        {
            AddRow(g3, "每次行动工资", null, NumCtrl("current_job.pay_per_action", 0, 1000000m, 10m, 2), 120);
            AddRow(g3, "基础工资", null, NumCtrl("current_job.base_pay_per_action", 0, 1000000m, 10m, 2), 120);
            AddRow(g3, "每周工资", null, NumCtrl("current_job.weekly_pay", 0, 10000000m, 10m, 2), 120);
            AddRow(g3, "老板评价", null, NumCtrl("current_job.boss_rating", 0, 100, 1m, 0), 120);
            AddRow(g3, "熟练度", null, NumCtrl("current_job.proficiency", 0, 100000m, 10m, 0), 120);
            AddRow(g3, "每周工时", null, NumCtrl("current_job.weekly_hours", 0, 40, 1m, 0), 120);
            AddRow(g3, "缺勤", null, NumCtrl("current_job.absences", 0, 100, 1m, 0), 120);
        }
        g3.Height = rowY + 12;
        loading = false;
    }

    /* ==================== 身体状态 ==================== */
    void BuildStatus()
    {
        Panel page = Page("身体状态");
        if (doc == null) return;
        loading = true; rowY = 46;
        int W = Math.Max(700, host.ClientSize.Width - 40);

        Card g = NewGroup(page, "基础状态", 10, 10, W);
        AddRow(g, "健康", "0 - 100", NumCtrl("health", 0, 100, 1m, 0), 120);
        AddRow(g, "心态", "0 - 100", NumCtrl("mental", 0, 100, 1m, 0), 120);
        AddRow(g, "饱食度", "0 - 100", NumCtrl("hunger", 0, 100, 1m, 0), 120);
        AddRow(g, "热量负担", "越低越好", NumCtrl("calorie_burden", 0, 100000m, 1m, 0), 120);
        Flat b = Btn("三项拉满", false); b.SetBounds(14, rowY + 6, b.Width, 28);
        b.Click += delegate {
            doc.Commit("health", 100, "健康");
            doc.Commit("mental", 100, "心态");
            doc.Commit("hunger", 100, "饱食度");
            BuildStatus(); UpdateStatus();
        };
        g.Controls.Add(b);
        g.Height = rowY + 42;

        rowY = 46;
        Card g2 = NewGroup(page, "其他", 10, g.Bottom + 12, W);
        AddRow(g2, "游戏结束", null, ChkCtrl("is_game_over", "已结束（关掉可以继续玩）"), 260);
        AddRow(g2, "最大周数", null, NumCtrl("maxWeeks", 1, 1000, 1m, 0), 120);
        g2.Height = rowY + 12;
        loading = false;
    }

    /* ==================== 原始数据 ==================== */
    TextBox rawBox;
    Label rawState;
    void BuildRaw()
    {
        Panel page = Page("原始数据");
        if (doc == null) return;
        int W = Math.Max(700, host.ClientSize.Width - 40);

        Label tip = new Label();
        tip.Text = "这里直接编辑存档 JSON。格式错误不会被写入，但字段改错仍可能让存档读不出来。";
        tip.ForeColor = Color.FromArgb(138, 82, 0);
        tip.SetBounds(14, 12, W - 40, 20);
        page.Controls.Add(tip);

        Flat ba = Btn("校验并应用", true); ba.SetBounds(16, 50, ba.Width, 28);
        ba.Click += delegate { ApplyRaw(); };
        page.Controls.Add(ba);
        Flat br = Btn("重新载入", false); br.SetBounds(16 + ba.Width + 10, 50, br.Width, 28);
        br.Click += delegate { ShowPage(); };
        page.Controls.Add(br);
        Flat bf = Btn("格式化", false); bf.SetBounds(16 + ba.Width + br.Width + 20, 50, bf.Width, 28);
        bf.Click += delegate { FormatRaw(); };
        page.Controls.Add(bf);

        rawState = new Label();
        rawState.SetBounds(16 + ba.Width + br.Width + bf.Width + 30, 54, Math.Max(120, W - (60 + ba.Width + br.Width + bf.Width)), 20);
        rawState.ForeColor = Color.FromArgb(110, 110, 110);
        page.Controls.Add(rawState);

        rawBox = new TextBox();
        rawBox.Multiline = true;
        rawBox.ScrollBars = ScrollBars.Both;
        rawBox.WordWrap = false;
        rawBox.Font = new Font("Consolas", 9f);
        rawBox.SetBounds(14, 72, W - 40, Math.Max(300, host.ClientSize.Height - 120));
        rawBox.Text = doc.Serialize();
        page.Controls.Add(rawBox);
    }

    void FormatRaw()
    {
        try
        {
            Dictionary<string, object> d = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string, object>>(rawBox.Text);
            System.Web.Script.Serialization.JavaScriptSerializer s = new System.Web.Script.Serialization.JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            rawBox.Text = s.Serialize(d);
            rawState.Text = "格式化完成";
            rawState.ForeColor = Color.FromArgb(15, 123, 63);
        }
        catch (Exception ex) { rawState.Text = "JSON 语法错误：" + ex.Message; rawState.ForeColor = Color.FromArgb(179, 38, 30); }
    }

    void ApplyRaw()
    {
        try
        {
            System.Web.Script.Serialization.JavaScriptSerializer s = new System.Web.Script.Serialization.JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            s.RecursionLimit = 500;
            Dictionary<string, object> d = s.Deserialize<Dictionary<string, object>>(rawBox.Text);
            if (d == null) throw new Exception("顶层必须是对象");
            doc.Doc = d;
            doc.Changes.Clear();
            rawState.Text = "已应用，记得点保存";
            rawState.ForeColor = Color.FromArgb(15, 123, 63);
            UpdateStatus();
        }
        catch (Exception ex)
        {
            rawState.Text = "语法错误，未应用：" + ex.Message;
            rawState.ForeColor = Color.FromArgb(179, 38, 30);
        }
    }

    /* ==================== 说明 ==================== */
    void BuildHelp()
    {
        Panel page = Page("说明");
        int W = Math.Max(700, content.ClientSize.Width - 40);

        Card g = NewGroup(page, "使用说明", 10, 10, W);
        TextBox t = new TextBox();
        t.Multiline = true;
        t.ReadOnly = true;
        t.ScrollBars = ScrollBars.Vertical;
        t.BorderStyle = BorderStyle.None;
        t.BackColor = Theme.Card;
        t.ForeColor = Theme.Text;
        t.Font = Theme.Base;
        t.SetBounds(18, 48, W - 48, Math.Max(340, content.ClientSize.Height - 220));
        t.Text =
            "使用步骤\r\n" +
            "  1. 退出游戏，要完全退出，不能只是最小化\r\n" +
            "  2. 在上面选要改的存档（默认就是最新的那个）\r\n" +
            "  3. 改数值，点右下角「保存」—— 保存前会自动备份原文件\r\n" +
            "  4. 重新进游戏读档\r\n" +
            "\r\n" +
            "改了没生效\r\n" +
            "  1. 游戏没完全退出，退出时把内存里的旧数据写回去了\r\n" +
            "  2. Steam 云同步用云端的旧存档覆盖了本地文件\r\n" +
            "     关闭方法：Steam 库右键游戏 → 属性 → 通用 → 关闭「Steam 云」\r\n" +
            "  3. 改的是 .bak 文件，游戏读的是没有 .bak 后缀的那个\r\n" +
            "\r\n" +
            "备份与还原\r\n" +
            "  每次保存前，原文件会自动复制到存档目录的「备份」子文件夹里，\r\n" +
            "  名字像 save_0.dat.20261010-153000。\r\n" +
            "  点右下角「还原备份…」可以选一个备份直接还原，\r\n" +
            "  还原前还会先把当前状态再备份一次，所以还原也能反悔。\r\n" +
            "  游戏自己也会留一份 save_N.dat.bak。\r\n" +
            "\r\n" +
            "关于存档格式\r\n" +
            "  存档是纯 JSON 文本。游戏把人物、目标线这些数据同时存了两份\r\n" +
            "  （state_modules 和 flags），本工具会同时写入两处，避免只改一半不生效。\r\n" +
            "\r\n" +
            "其他\r\n" +
            "  改人物关系时，好感是数值、关系是阶段，两个一起改比较自然：\r\n" +
            "  0-14 认识，15-59 朋友，60 以上挚友。恋爱建议先把关系提到朋友再选。\r\n" +
            "  性别改完，只有新触发的剧情会用新性别，已经触发过的对话文本不会变。\r\n";
        g.Controls.Add(t);
        g.Height = t.Bottom + 18;

        Card g2 = NewGroup(page, "关于", 10, g.Bottom + 12, W);
        InfoRow.Add(g2, "作者", AUTHOR, 50, W, false);
        InfoRow.Add(g2, "版本", APPVER, 76, W, false);

        Label lk = new Label();
        lk.Text = "github.com/" + AUTHOR;
        lk.ForeColor = Theme.Accent;
        lk.Font = Theme.Base;
        lk.Cursor = Cursors.Hand;
        lk.SetBounds(108, 102, W - 140, 20);
        lk.Click += delegate { try { Process.Start("https://github.com/" + AUTHOR); } catch { } };
        g2.Controls.Add(lk);

        Label lk2 = new Label();
        lk2.Text = "开源地址";
        lk2.ForeColor = Theme.Muted;
        lk2.Font = Theme.Base;
        lk2.SetBounds(18, 102, 84, 20);
        g2.Controls.Add(lk2);

        Label note = new Label();
        note.Text = "本项目基于 MIT 协议开源，仅供单机存档使用，请勿用于任何联机或商业化用途。";
        note.ForeColor = Theme.Faint;
        note.Font = Theme.Small;
        note.SetBounds(18, 134, W - 46, 20);
        g2.Controls.Add(note);
        g2.Height = 168;
    }
    /* ==================== 保存 / 备份 / 还原 ==================== */
    void UpdateStatus()
    {
        if (doc == null) { lblStatus.Text = "未打开存档"; return; }
        lblStatus.Text = "已打开 " + doc.FileName + "　已修改 " + doc.Changes.Count + " 项";
        btnSave.Enabled = true;
    }

    void DoSave()
    {
        if (doc == null) return;
        if (doc.Changes.Count == 0)
        {
            if (MessageBox.Show("还没有做任何修改，仍要写回文件吗？", APPNAME,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        }
        try
        {
            string bak = doc.Backup();
            doc.Save();
            UpdateStatus();
            lblStatus.Text = "已保存 " + doc.FileName + "　原文件备份为 " + bak;
            MessageBox.Show("已保存。\n\n原文件已备份到「备份」子文件夹：\n" + bak, APPNAME,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("保存失败：\n\n" + ex.Message, APPNAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void DoBackup()
    {
        if (doc == null) return;
        try
        {
            string b = doc.Backup();
            MessageBox.Show("已备份为：\n" + b, APPNAME, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show("备份失败：" + ex.Message, APPNAME); }
    }

    void RestoreBackup()
    {
        if (doc == null) return;
        string dir = SaveDoc.BackupDir(doc.FilePath);
        string prefix = doc.FileName + ".";
        List<string> list = new List<string>();
        try
        {
            foreach (string f in Directory.GetFiles(dir))
                if (Path.GetFileName(f).StartsWith(prefix)) list.Add(f);
        }
        catch { }
        if (list.Count == 0)
        {
            MessageBox.Show("这个存档还没有备份。\n\n备份会在每次保存时自动生成。", APPNAME);
            return;
        }
        list.Sort();
        list.Reverse();

        Form f2 = new Form();
        f2.Text = "选择一个备份还原（列表最上面是最新的）";
        f2.ClientSize = new Size(560, 340);
        f2.StartPosition = FormStartPosition.CenterParent;
        f2.Font = new Font("Microsoft YaHei UI", 9f);
        ListBox lb = new ListBox();
        lb.Dock = DockStyle.Fill;
        foreach (string f in list)
        {
            FileInfo fi = new FileInfo(f);
            lb.Items.Add(fi.Name + "　" + (fi.Length / 1024) + " KB　" + fi.LastWriteTime.ToString("MM-dd HH:mm"));
        }
        if (lb.Items.Count > 0) lb.SelectedIndex = 0;
        f2.Controls.Add(lb);
        Button ok = new Button(); ok.Text = "还原这个"; ok.DialogResult = DialogResult.OK;
        ok.Dock = DockStyle.Bottom; ok.Height = 34;
        f2.Controls.Add(ok);
        if (f2.ShowDialog() != DialogResult.OK || lb.SelectedIndex < 0) return;

        string chosen = list[lb.SelectedIndex];
        if (MessageBox.Show("确定用这个备份覆盖当前存档？\n\n" + Path.GetFileName(chosen) +
            "\n\n（当前状态也会先备份一次）", APPNAME, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            doc.Backup();
            File.Copy(chosen, doc.FilePath, true);
            LoadSave(doc.FilePath);
            MessageBox.Show("已还原。", APPNAME, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show("还原失败：" + ex.Message, APPNAME); }
    }

    void RevertAll()
    {
        if (doc == null) return;
        if (doc.Changes.Count == 0) return;
        if (MessageBox.Show("撤销全部修改，恢复到刚打开时的内容？", APPNAME,
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        doc.RevertAll();
        ShowPage();
        UpdateStatus();
    }

    /* ==================== 杂项 ==================== */
    static string LabelFor(string path)
    {
        string[] parts = path.Split('.');
        string tail = parts[parts.Length - 1];
        Dictionary<string, string> map = new Dictionary<string, string>();
        map["cash"] = "金钱"; map["total_assets"] = "总资产"; map["health"] = "健康";
        map["mental"] = "心态"; map["hunger"] = "饱食度"; map["gpa"] = "综合GPA";
        map["semester_gpa"] = "本学期GPA"; map["reputation"] = "声望"; map["gender"] = "性别";
        map["player_name"] = "姓名"; map["orientation"] = "性取向"; map["relationship"] = "好感度";
        map["unlocked"] = "解锁"; map["work_progress"] = "作业进度"; map["submitted"] = "已提交";
        map["resolved"] = "已结算"; map["weekTotal"] = "总周数"; map["completed_semesters"] = "已完成学期";
        map["calorie_burden"] = "热量负担"; map["intimacy"] = "亲密度"; map["friendship_stage"] = "关系";
        map["romance_stage"] = "恋爱"; map["pay_per_action"] = "每次工资"; map["boss_rating"] = "老板评价";
        map["proficiency"] = "熟练度"; map["knowledge"] = "知识"; map["charm"] = "魅力";
        map["eq"] = "情商"; map["language"] = "语言"; map["is_game_over"] = "游戏结束";
        if (map.ContainsKey(tail)) return map[tail];
        return parts.Length >= 2 ? parts[parts.Length - 2] + "." + tail : path;
    }

    public static string HotPrompt(string text, string def)
    {
        Form f = new Form();
        f.Text = APPNAME;
        f.ClientSize = new Size(360, 130);
        f.StartPosition = FormStartPosition.CenterParent;
        f.FormBorderStyle = FormBorderStyle.FixedDialog;
        f.MaximizeBox = false; f.MinimizeBox = false;
        f.Font = new Font("Microsoft YaHei UI", 9f);
        Label l = new Label(); l.Text = text; l.SetBounds(14, 16, 330, 22);
        f.Controls.Add(l);
        TextBox t = new TextBox(); t.Text = def; t.SetBounds(14, 44, 330, 24);
        f.Controls.Add(t);
        Button ok = new Button(); ok.Text = "确定"; ok.SetBounds(180, 84, 78, 28);
        ok.DialogResult = DialogResult.OK; f.Controls.Add(ok);
        Button no = new Button(); no.Text = "取消"; no.SetBounds(266, 84, 78, 28);
        no.DialogResult = DialogResult.Cancel; f.Controls.Add(no);
        f.AcceptButton = ok; f.CancelButton = no;
        return f.ShowDialog() == DialogResult.OK ? t.Text : null;
    }
}
