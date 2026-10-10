/* =====================================================================
   存档修改器核心逻辑自测（不开窗口，命令行跑）
   编译： csc /target:exe /main:SelfTest /out:selftest.exe wfsave.cs savecore.cs selftest.cs
   结果同时写到 %TEMP%\liuxue_selftest_result.txt（避免控制台编码问题）
   ===================================================================== */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

static class SelfTest
{
    static int pass = 0, fail = 0;
    static StringBuilder log = new StringBuilder();

    public static void Main(string[] args) { Run(args); }

    static void Out(string s) { log.Append(s).Append("\r\n"); }

    static void Check(string name, bool ok, object extra = null)
    {
        if (ok) { pass++; Out("  [OK]   " + name); }
        else { fail++; Out("  [FAIL] " + name + (extra != null ? "  -> " + extra : "")); }
    }

    public static void Run(string[] args)
    {
        string resultFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest_result.txt");
        try
        {
            RunInner();
        }
        catch (Exception ex)
        {
            fail++;
            Out("  [FATAL] " + ex.GetType().Name + ": " + ex.Message);
            Out(ex.StackTrace);
        }
        Out("");
        Out("================================");
        Out("通过 " + pass + " 项，失败 " + fail + " 项");

        try { File.WriteAllText(resultFile, log.ToString(), new UTF8Encoding(false)); } catch { }
        try { Console.WriteLine(log.ToString()); } catch { }
        Environment.Exit(fail > 0 ? 1 : 0);
    }

    static void RunInner()
    {
        string dir = SaveDoc.SaveDirDefault();
        Out("存档目录: " + dir);

        string[] names = new string[] { "save_0.dat", "save_5.dat" };
        string work = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_selftest_work");
        if (Directory.Exists(work)) Directory.Delete(work, true);
        Directory.CreateDirectory(work);

        foreach (string n in names)
        {
            string src = Path.Combine(dir, n);
            if (!File.Exists(src)) { Out("\r\n跳过（不存在）: " + n); continue; }
            Out("");
            Out("=============== " + n + " ===============");

            string copy = Path.Combine(work, n);
            File.Copy(src, copy, true);
            string origHash = Sha(copy);
            string origText = File.ReadAllText(src, Encoding.UTF8);

            SaveDoc d = SaveDoc.Load(copy);
            Check("能读出来", d.Doc != null && d.Doc.Count > 0, d.Doc == null ? "null" : d.Doc.Count.ToString());
            Check("顶层键数量 >= 50", d.Doc.Count >= 50, d.Doc.Count);

            /* ---- 序列化往返 ---- */
            string outText = d.Serialize();
            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            ser.RecursionLimit = 500;
            Dictionary<string, object> back = ser.Deserialize<Dictionary<string, object>>(outText);
            Check("序列化后还能解析", back != null && back.Count > 0);
            Check("顶层键数量不变", back.Count == d.Doc.Count, back.Count + " vs " + d.Doc.Count);
            Check("中文没乱码", outText.Contains("编程导论") || outText.Contains("第1学期"));
            Check("cash 值一致", SaveDoc.Num(back["cash"], -1) == SaveDoc.Num(d.Doc["cash"], -2),
                SaveDoc.Num(back["cash"], -1));
            Check("academic_nodes 数量一致",
                SaveDoc.AsList(back["academic_nodes"]).Count == SaveDoc.AsList(d.Doc["academic_nodes"]).Count);
            Check("flags 仍然存在", back.ContainsKey("flags") && back["flags"] != null);

            /* ---- 路径读写 ---- */
            Check("读 cash", SaveDoc.Num(d.GetVal("cash"), -1) >= 0, d.GetVal("cash"));
            Check("读嵌套 gender", SaveDoc.Str(d.GetVal("flags.gender")).Length > 0, d.GetVal("flags.gender"));
            Check("读数组下标", SaveDoc.Get(d.Doc, "academic_nodes.0.id") != null,
                SaveDoc.Get(d.Doc, "academic_nodes.0.id"));
            d.Commit("academic_nodes.0.work_progress", 999.0, "进度");
            Check("写数组下标", SaveDoc.Num(d.GetVal("academic_nodes.0.work_progress"), -1) == 999,
                d.GetVal("academic_nodes.0.work_progress"));

            /* ---- 镜像同步 ---- */
            Dictionary<string, object> profs =
                d.GetVal("state_modules.characters.profiles") as Dictionary<string, object>;
            Check("读得到人物表", profs != null && profs.Count > 0, profs == null ? "null" : profs.Count.ToString());
            if (profs != null && profs.Count > 0)
            {
                string first = null;
                foreach (KeyValuePair<string, object> kv in profs) { first = kv.Key; break; }
                d.Commit("state_modules.characters.profiles." + first + ".relationship", 77, "好感");
                Check("人物好感写入 state_modules",
                    SaveDoc.Num(d.GetVal("state_modules.characters.profiles." + first + ".relationship"), -1) == 77);
                Check("人物好感【镜像】同步到 flags",
                    SaveDoc.Num(d.GetVal("flags.character_profiles.profiles." + first + ".relationship"), -1) == 77,
                    d.GetVal("flags.character_profiles.profiles." + first + ".relationship"));

                Dictionary<string, object> shape = new Dictionary<string, object>();
                shape["friendship_stage"] = "acquaintance";
                shape["intimacy"] = 0;
                shape["romance_stage"] = "none";
                d.CommitEntry("flags.person_relationship_progress." + first, "friendship_stage", "close_friend", "关系", shape);
                d.CommitEntry("flags.person_relationship_progress." + first, "intimacy", 88, "亲密度", shape);
                Check("commitEntry 写入 friendship_stage",
                    SaveDoc.Str(d.GetVal("flags.person_relationship_progress." + first + ".friendship_stage")) == "close_friend",
                    d.GetVal("flags.person_relationship_progress." + first));
                Check("commitEntry 写入 intimacy",
                    SaveDoc.Num(d.GetVal("flags.person_relationship_progress." + first + ".intimacy"), -1) == 88);
                Check("commitEntry 没丢掉同级字段",
                    SaveDoc.Str(d.GetVal("flags.person_relationship_progress." + first + ".friendship_stage")) == "close_friend");
            }

            /* ---- 目标线镜像 ---- */
            d.Commit("state_modules.goal_mode.line_weights.academic_grade", 5.0, "权重");
            Check("goal_mode 镜像同步",
                SaveDoc.Num(d.GetVal("flags.goal_mode.line_weights.academic_grade"), -1) == 5,
                d.GetVal("flags.goal_mode.line_weights.academic_grade"));

            Check("记录了修改项", d.Changes.Count > 0, d.Changes.Count);

            /* ---- 撤销 ---- */
            double cashBefore = SaveDoc.Num(SaveDoc.Get(back, "cash"), 0);
            d.RevertAll();
            Check("撤销后 cash 回到原值", SaveDoc.Num(d.GetVal("cash"), -1) == cashBefore);
            Check("撤销后修改记录清空", d.Changes.Count == 0, d.Changes.Count);

            /* ---- 保存 + 备份 ---- */
            d.Commit("cash", 123456.75, "金钱");
            d.Commit("total_assets", 123456.75, "总资产");
            string bak = d.Backup();
            string bakPath = Path.Combine(SaveDoc.BackupDir(copy), bak);
            Check("生成了备份文件", File.Exists(bakPath), bakPath);
            Check("备份内容 == 原始文件", Sha(bakPath) == origHash);
            d.Save();

            Check("保存后文件是合法 JSON", ParseOk(File.ReadAllText(copy, Encoding.UTF8)));
            SaveDoc re = SaveDoc.Load(copy);
            Check("保存后 cash 是新值", SaveDoc.Num(re.GetVal("cash"), -1) == 123456.75, re.GetVal("cash"));
            Check("保存后没有 BOM", File.ReadAllBytes(copy)[0] != 0xEF);
            Check("保存后顶层键数量不变", re.Doc.Count == back.Count, re.Doc.Count + " vs " + back.Count);
            Check("保存后中文还在", File.ReadAllText(copy, Encoding.UTF8).Contains("编程导论") ||
                                   File.ReadAllText(copy, Encoding.UTF8).Contains("第1学期"));

            /* ---- 还原 ---- */
            File.Copy(bakPath, copy, true);
            Check("还原后逐字节一致（SHA256）", Sha(copy) == origHash);
            Check("还原后文本完全一致", File.ReadAllText(copy, Encoding.UTF8) == origText);
        }

        try { Directory.Delete(work, true); } catch { }
    }

    static bool ParseOk(string text)
    {
        try
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            s.RecursionLimit = 500;
            return s.DeserializeObject(text) != null;
        }
        catch { return false; }
    }

    static string Sha(string path)
    {
        using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
        using (FileStream fs = File.OpenRead(path))
        {
            byte[] h = sha.ComputeHash(fs);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in h) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
