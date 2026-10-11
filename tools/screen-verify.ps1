# 真实屏幕抓图 + 点导航 —— 验证 on-screen 渲染（不是 DrawToBitmap）
# 用法: . .\screen-verify.ps1 ; Verify-Page -Index 4 -Out "d:\a.png"
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Text; using System.Runtime.InteropServices;
public class SV {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
  public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumWindowsProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public static readonly IntPtr TOPMOST = new IntPtr(-1);
  public static readonly IntPtr NOTOPMOST = new IntPtr(-2);
  public static IntPtr FindMain(uint want) {
    IntPtr found = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid != want) return true;
      StringBuilder sb = new StringBuilder(512); GetWindowTextW(h, sb, 512);
      if (sb.ToString().IndexOf("存档修改器") < 0) return true;
      RECT r; GetWindowRect(h, out r);
      if (r.R - r.L < 400) return true;
      found = h; return false;
    }, IntPtr.Zero);
    return found;
  }
  public static IntPtr FindChildW(IntPtr form, int w) {
    IntPtr found = IntPtr.Zero;
    EnumChildWindows(form, delegate(IntPtr c, IntPtr l) {
      if (GetParent(c) != form) return true;
      RECT r; GetWindowRect(c, out r);
      if ((r.R-r.L) == w) { found = c; return false; }
      return true;
    }, IntPtr.Zero);
    return found;
  }
}
"@ -ErrorAction Stop

function Verify-Page {
  param([int]$Index, [string]$Out, [string]$ProcName = "留学模拟器修改器")
  $p = Get-Process -Name $ProcName -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $p) { Write-Output "  进程不在"; return }
  $MAIN = [SV]::FindMain([uint32]$p.Id)
  $NAV  = [SV]::FindChildW($MAIN, 170)
  if ($MAIN -eq [IntPtr]::Zero -or $NAV -eq [IntPtr]::Zero) { Write-Output "  找不到窗口"; return }

  [SV]::SetWindowPos($MAIN, [SV]::TOPMOST, 0, 0, 0, 0, 0x0043) | Out-Null
  Start-Sleep -Milliseconds 700

  # NavBar 里第 i 项中心: 8 + i*44 + 22
  $y = 8 + $Index * 44 + 22
  $lp = [IntPtr](($y -shl 16) -bor 85)
  [SV]::SendMessage($NAV, 0x0201, [IntPtr]1, $lp) | Out-Null
  [SV]::SendMessage($NAV, 0x0202, [IntPtr]0, $lp) | Out-Null
  Start-Sleep -Milliseconds 2200

  $r = New-Object SV+RECT
  [SV]::GetWindowRect($MAIN, [ref]$r) | Out-Null
  $w = $r.R - $r.L; $ht = $r.B - $r.T
  $bmp = New-Object System.Drawing.Bitmap($w, $ht)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($r.L, $r.T, 0, 0, (New-Object System.Drawing.Size($w, $ht)))
  $g.Dispose()
  $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  [SV]::SetWindowPos($MAIN, [SV]::NOTOPMOST, 0, 0, 0, 0, 0x0043) | Out-Null
  Write-Output "  已抓图 index=$Index -> $Out"
}
