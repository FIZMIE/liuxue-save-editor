/* =====================================================================
   留学模拟器 存档修改器 —— 核心逻辑（原生窗口版共用）
   和之前网页版的字段路径 / 镜像规则 / 备份策略完全一致。
   ===================================================================== */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

class SaveDoc
{
    public Dictionary<string, object> Doc;
    public Dictionary<string, object> Orig;
    public string OrigText = "";
    public string FilePath = "";
    public string FileName = "";
    public List<Change> Changes = new List<Change>();

    public class Change
    {
        public string Path = "";
        public string Label = "";
        public object From;
        public object To;
    }

    /* 存档系统把 state_modules 同步了一份到 flags，改一处必须同时改另一处 */
    public static readonly string[][] Mirrors = new string[][]
    {
        new string[] { "flags.character_profiles",    "state_modules.characters" },
        new string[] { "flags.goal_mode",             "state_modules.goal_mode" },
        new string[] { "flags.social_clues",          "state_modules.phone_clues.social_clues" },
        new string[] { "flags.social_post_reactions", "state_modules.phone_clues.social_post_reactions" },
        new string[] { "flags.encounter_state",       "state_modules.encounters" }
    };

    static JavaScriptSerializer Ser()
    {
        JavaScriptSerializer s = new JavaScriptSerializer();
        s.MaxJsonLength = int.MaxValue;
        s.RecursionLimit = 500;
        return s;
    }

    public static SaveDoc Load(string path)
    {
        SaveDoc d = new SaveDoc();
        string text = File.ReadAllText(path, Encoding.UTF8);
        d.Doc = Ser().Deserialize<Dictionary<string, object>>(text);
        if (d.Doc == null) throw new Exception("存档不是合法的 JSON 对象。");
        d.OrigText = text;
        d.Orig = Ser().Deserialize<Dictionary<string, object>>(text);
        d.FilePath = path;
        d.FileName = Path.GetFileName(path);
        return d;
    }

    public string Serialize()
    {
        return Ser().Serialize(Doc);
    }

    /* 注意：JavaScriptSerializer 反序列化 JSON 数组得到的是 ArrayList，
       不是 object[]。以前按 object[] 强转会直接抛异常，所以统一走 IList。 */
    public static System.Collections.IList AsList(object o)
    {
        return o as System.Collections.IList;
    }

    /* ---------------- 路径读写 ---------------- */
    public static object Get(Dictionary<string, object> root, string path)
    {
        if (root == null) return null;
        string[] parts = path.Split('.');
        object cur = root;
        for (int i = 0; i < parts.Length; i++)
        {
            Dictionary<string, object> d = cur as Dictionary<string, object>;
            if (d != null)
            {
                if (!d.ContainsKey(parts[i])) return null;
                cur = d[parts[i]];
            }
            else
            {
                System.Collections.IList arr = AsList(cur);
                int idx;
                if (arr != null && int.TryParse(parts[i], out idx))
                {
                    if (idx < 0 || idx >= arr.Count) return null;
                    cur = arr[idx];
                }
                else return null;
            }
        }
        return cur;
    }

    public static void Set(Dictionary<string, object> root, string path, object val)
    {
        string[] parts = path.Split('.');
        object cur = root;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            Dictionary<string, object> d = cur as Dictionary<string, object>;
            if (d != null)
            {
                if (!d.ContainsKey(parts[i]) || d[parts[i]] == null) d[parts[i]] = new Dictionary<string, object>();
                cur = d[parts[i]];
            }
            else
            {
                System.Collections.IList arr = AsList(cur);
                int idx;
                if (arr != null && int.TryParse(parts[i], out idx) && idx >= 0 && idx < arr.Count)
                {
                    if (arr[idx] == null) arr[idx] = new Dictionary<string, object>();
                    cur = arr[idx];
                }
                else return;
            }
        }
        Dictionary<string, object> last = cur as Dictionary<string, object>;
        if (last != null) last[parts[parts.Length - 1]] = val;
    }

    public static List<string> Expand(string path)
    {
        List<string> outp = new List<string>();
        outp.Add(path);
        foreach (string[] pair in Mirrors)
        {
            string a = pair[0], b = pair[1];
            if (path == a) outp.Add(b);
            else if (path.StartsWith(a + ".")) outp.Add(b + path.Substring(a.Length));
            else if (path == b) outp.Add(a);
            else if (path.StartsWith(b + ".")) outp.Add(a + path.Substring(b.Length));
        }
        return outp;
    }

    /* ---------------- 修改 ---------------- */
    public object GetVal(string path) { return Get(Doc, path); }
    public object GetOrig(string path) { return Get(Orig, path); }

    public bool Commit(string path, object value, string label)
    {
        if (Eq(Get(Doc, path), value)) return false;
        foreach (string t in Expand(path)) Set(Doc, t, value);
        Record(path, null, label);
        return true;
    }

    public bool CommitEntry(string path, string key, object value, string label, Dictionary<string, object> shape)
    {
        Dictionary<string, object> cur = Get(Doc, path) as Dictionary<string, object>;
        Dictionary<string, object> bas;
        if (cur != null)
        {
            bas = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> kv in cur) bas[kv.Key] = kv.Value;
        }
        else bas = shape != null ? Clone(shape) : new Dictionary<string, object>();

        if (Eq(bas.ContainsKey(key) ? bas[key] : null, value)) return false;
        bas[key] = value;
        foreach (string t in Expand(path)) Set(Doc, t, bas);
        Record(path, key, label);
        return true;
    }

    void Record(string path, string sub, string label)
    {
        string full = sub == null ? path : path + "." + sub;
        object from = Get(Orig, full);
        object to = Get(Doc, full);
        int idx = -1;
        for (int i = 0; i < Changes.Count; i++) if (Changes[i].Path == full) { idx = i; break; }
        if (idx >= 0) Changes.RemoveAt(idx);
        if (!Eq(from, to)) Changes.Add(new Change { Path = full, Label = label, From = from, To = to });
    }

    public void RevertAll()
    {
        /* 必须重新解析一份，不能浅拷贝 —— 浅拷贝会让嵌套对象和原始副本共享，
           撤销之后再改就相当于把"原始值"也改了。 */
        Doc = Ser().Deserialize<Dictionary<string, object>>(OrigText);
        Changes.Clear();
    }

    public static bool Eq(object a, object b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (IsNum(a) && IsNum(b)) return Convert.ToDouble(a) == Convert.ToDouble(b);
        return a.ToString() == b.ToString();
    }

    public static bool IsNum(object o)
    {
        return o is int || o is long || o is double || o is float || o is decimal || o is short || o is byte;
    }

    public static double Num(object o, double def)
    {
        if (o == null) return def;
        try { return Convert.ToDouble(o); } catch { return def; }
    }

    public static string Str(object o)
    {
        return o == null ? "" : Convert.ToString(o);
    }

    public static bool Bool(object o)
    {
        if (o == null) return false;
        if (o is bool) return (bool)o;
        string s = Convert.ToString(o).ToLowerInvariant();
        return s == "true" || s == "1";
    }

    public static Dictionary<string, object> Clone(Dictionary<string, object> src)
    {
        Dictionary<string, object> d = new Dictionary<string, object>();
        if (src != null) foreach (KeyValuePair<string, object> kv in src) d[kv.Key] = kv.Value;
        return d;
    }

    /* ---------------- 保存 ---------------- */
    public string Backup()
    {
        string dir = BackupDir(FilePath);
        string baseName = FileName + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string target = Path.Combine(dir, baseName);
        int i = 1;
        while (File.Exists(target)) target = Path.Combine(dir, baseName + "-" + (i++));
        File.Copy(FilePath, target, false);
        return Path.GetFileName(target);
    }

    public static string BackupDir(string savePath)
    {
        string dir = Path.Combine(Path.GetDirectoryName(savePath), "备份");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return dir;
    }

    public void Save()
    {
        string text = Serialize();
        /* 先写临时文件再替换，写一半断电也不会毁掉存档 */
        string tmp = FilePath + ".tmp_write";
        File.WriteAllText(tmp, text, new UTF8Encoding(false));
        if (File.Exists(FilePath)) File.Delete(FilePath);
        File.Move(tmp, FilePath);
        OrigText = text;
        Orig = Ser().Deserialize<Dictionary<string, object>>(text);
        Changes.Clear();
    }

    /* ---------------- 找存档 ---------------- */
    public static List<string> FindSaves()
    {
        List<string> found = new List<string>();
        string[] bases = new string[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Godot", "app_userdata"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Godot", "app_userdata")
        };
        foreach (string b in bases)
        {
            if (!Directory.Exists(b)) continue;
            foreach (string d in SafeDirs(b)) found.AddRange(SavesIn(d));
        }
        return found;
    }

    static string[] SafeDirs(string p)
    {
        try { return Directory.GetDirectories(p); } catch { return new string[0]; }
    }

    public static List<string> SavesIn(string dir)
    {
        List<string> l = new List<string>();
        try
        {
            foreach (string f in Directory.GetFiles(dir, "save_*.dat"))
                if (IsSave(f)) l.Add(f);
        }
        catch { }
        return l;
    }

    public static bool IsSave(string path)
    {
        try
        {
            FileInfo fi = new FileInfo(path);
            if (fi.Length < 512 || fi.Length > 512L * 1024 * 1024) return false;
            byte[] buf = new byte[256];
            using (FileStream fs = File.OpenRead(path))
            {
                int n = fs.Read(buf, 0, buf.Length);
                string head = Encoding.UTF8.GetString(buf, 0, n);
                return head.IndexOf("\"academic_nodes\"", StringComparison.Ordinal) >= 0;
            }
        }
        catch { return false; }
    }

    public static string SaveDirDefault()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Godot", "app_userdata", "留学模拟器");
    }
}
