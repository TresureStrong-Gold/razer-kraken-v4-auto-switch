using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

// Razer Kraken V4 (동글 상시 연결) 헤드폰 전원 ON/OFF를 동글의 HID 이벤트로 감지해
// Windows 기본 출력 장치를 자동 전환하는 트레이 프로그램.
//   ON  -> Kraken 'Game'(재생) / 'Chat'(통신) 으로 전환, 직전 기본 장치는 저장
//   OFF -> 저장해 둔 직전 장치로 복원 (없으면 config 의 fallback)
static class Program
{
    [STAThread]
    static void Main()
    {
        bool created;
        using (var m = new Mutex(true, "KrakenAutoSwitch_single", out created))
        {
            if (!created) return;
            Application.EnableVisualStyles();
            Application.Run(new TrayApp());
        }
    }
}

class TrayApp : ApplicationContext
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "KrakenAutoSwitch";

    readonly string dir = AppDomain.CurrentDomain.BaseDirectory;
    readonly NotifyIcon tray = new NotifyIcon();
    readonly SynchronizationContext ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
    readonly ToolStripMenuItem stateItem = new ToolStripMenuItem("상태: 알 수 없음") { Enabled = false };
    readonly ToolStripMenuItem autoStartItem = new ToolStripMenuItem("Windows 시작 시 자동 실행");
    int state = -1; // -1 unknown, 0 off, 1 on
    string gameName = "Razer Kraken V4 - Game", chatName = "Razer Kraken V4 - Chat", fallbackName = "";
    string[] saved = new string[3]; // 직전 기본 장치 id (console, multimedia, communications)

    public TrayApp()
    {
        LoadConfig();
        LoadSaved();

        var menu = new ContextMenuStrip();
        menu.Items.Add(stateItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Kraken으로 지금 전환", null, (s, e) => SwitchTo(true));
        menu.Items.Add("이전 장치로 지금 복원", null, (s, e) => SwitchTo(false));
        menu.Items.Add(new ToolStripSeparator());
        autoStartItem.Checked = IsAutoStart();
        autoStartItem.Click += (s, e) => { SetAutoStart(!IsAutoStart()); autoStartItem.Checked = IsAutoStart(); };
        menu.Items.Add(autoStartItem);
        menu.Items.Add("로그 열기", null, (s, e) => { try { System.Diagnostics.Process.Start(Path.Combine(dir, "KrakenAutoSwitch.log")); } catch { } });
        menu.Items.Add("종료", null, (s, e) => { tray.Visible = false; Application.Exit(); });
        tray.ContextMenuStrip = menu;
        SetState(-1);
        tray.Visible = true;

        new Thread(HidLoop) { IsBackground = true }.Start();
    }

    // ---------- 설정 / 상태 파일 ----------
    void LoadConfig()
    {
        string p = Path.Combine(dir, "KrakenAutoSwitch.ini");
        if (!File.Exists(p))
            File.WriteAllText(p, "; 장치 이름은 '포함' 일치입니다 (소리 설정에 보이는 이름 일부).\r\ngame=" + gameName + "\r\nchat=" + chatName + "\r\n; 헤드폰을 끌 때 저장된 직전 장치가 없으면 이 장치로 전환 (비우면 아무것도 안 함)\r\nfallback=\r\n", Encoding.UTF8);
        foreach (var line in File.ReadAllLines(p, Encoding.UTF8))
        {
            int i = line.IndexOf('=');
            if (line.StartsWith(";") || i < 0) continue;
            string k = line.Substring(0, i).Trim().ToLower(), v = line.Substring(i + 1).Trim();
            if (k == "game") gameName = v; else if (k == "chat") chatName = v; else if (k == "fallback") fallbackName = v;
        }
    }

    string SavedPath { get { return Path.Combine(dir, "KrakenAutoSwitch.state"); } }
    void LoadSaved() { try { var l = File.ReadAllLines(SavedPath); for (int i = 0; i < 3 && i < l.Length; i++) saved[i] = l[i].Length > 0 ? l[i] : null; } catch { } }
    void SaveSaved() { try { File.WriteAllLines(SavedPath, new[] { saved[0] ?? "", saved[1] ?? "", saved[2] ?? "" }); } catch { } }

    void Log(string s)
    {
        try
        {
            string p = Path.Combine(dir, "KrakenAutoSwitch.log");
            if (File.Exists(p) && new FileInfo(p).Length > 200000) File.Delete(p);
            File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + s + "\r\n", Encoding.UTF8);
        }
        catch { }
    }

    // ---------- 자동 실행 ----------
    static bool IsAutoStart() { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(RunName) != null; }
    static void SetAutoStart(bool on)
    {
        using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
        {
            if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\""); else k.DeleteValue(RunName, false);
        }
    }

    // ---------- 트레이 표시 ----------
    void SetState(int s)
    {
        state = s;
        Color c = s == 1 ? Color.LimeGreen : s == 0 ? Color.Gray : Color.Orange;
        using (var bmp = new Bitmap(16, 16))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(c)) g.FillEllipse(b, 1, 1, 14, 14);
                g.DrawEllipse(Pens.Black, 1, 1, 14, 14);
            }
            var old = tray.Icon;
            tray.Icon = Icon.FromHandle(bmp.GetHicon());
            if (old != null) old.Dispose();
        }
        string t = s == 1 ? "헤드폰 켜짐" : s == 0 ? "헤드폰 꺼짐" : "헤드폰 상태 알 수 없음 (다음 on/off 때 확정)";
        stateItem.Text = "상태: " + t;
        tray.Text = "Kraken 자동 전환 - " + t;
    }

    // ---------- HID 감시 ----------
    void HidLoop()
    {
        while (true)
        {
            try
            {
                string path = Hid.FindPath(0x1532, 0x056C, 0xFF14);
                if (path != null) Hid.ReadLoop(path, OnReport);
            }
            catch (Exception ex) { Log("HID 오류: " + ex.Message); }
            Thread.Sleep(2000); // 동글 재연결 / 절전 복귀 대비
        }
    }

    void OnReport(byte[] b, int n)
    {
        // 02 0A 60 .. .. .. 05 00 80 80 20 02 01 <상태>   (상태 01=켜짐, 00=꺼짐)
        if (n < 14 || b[1] != 0x0A || b[10] != 0x20 || b[11] != 0x02 || b[12] != 0x01) return;
        int s = b[13] == 1 ? 1 : 0;
        ui.Post(_ => Apply(s), null);
    }

    void Apply(int s)
    {
        if (s == state) return;
        Log("헤드폰 " + (s == 1 ? "ON" : "OFF"));
        SetState(s);
        SwitchTo(s == 1);
    }

    // ---------- 오디오 전환 ----------
    void SwitchTo(bool kraken)
    {
        try
        {
            var devs = Audio.ListRender();
            Func<string, Audio.Dev> find = name => string.IsNullOrEmpty(name) ? null : devs.Find(d => d.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
            var game = find(gameName); var chat = find(chatName) ?? game;
            if (kraken)
            {
                if (game == null) { Log("Kraken Game 장치를 찾지 못함"); return; }
                // 이미 Kraken 이 기본이 아닐 때만 '직전 장치'로 저장
                var cur = new[] { Audio.GetDefault(0), Audio.GetDefault(1), Audio.GetDefault(2) };
                bool curIsKraken = cur[0] == game.Id || (chat != null && cur[0] == chat.Id);
                if (!curIsKraken) { saved = cur; SaveSaved(); }
                Audio.SetDefault(game.Id, 0); Audio.SetDefault(game.Id, 1); Audio.SetDefault(chat.Id, 2);
                Log("-> " + game.Name + " / 통신: " + chat.Name);
            }
            else
            {
                var fb = find(fallbackName);
                for (int role = 0; role < 3; role++)
                {
                    string id = saved[role];
                    bool isKraken = (game != null && id == game.Id) || (chat != null && id == chat.Id);
                    if (isKraken && role == 2) id = saved[0]; // 꺼진 헤드폰이 통신 기본 장치로 남지 않게
                    if (id == null || !devs.Exists(d => d.Id == id) || (isKraken && role < 2)) id = fb != null ? fb.Id : null;
                    if (id == null) continue;
                    Audio.SetDefault(id, role);
                }
                Log("복원: " + string.Join(" | ", Array.ConvertAll(new[] { Audio.GetDefault(0), Audio.GetDefault(1), Audio.GetDefault(2) }, id => devs.Find(d => d.Id == id) != null ? devs.Find(d => d.Id == id).Name : "?")));
            }
            tray.ShowBalloonTip(1500, "Kraken 자동 전환", kraken ? "Kraken 으로 출력 전환" : "이전 출력 장치로 복원", ToolTipIcon.None);
        }
        catch (Exception ex) { Log("전환 오류: " + ex); }
    }
}

// ================== HID ==================
static class Hid
{
    [StructLayout(LayoutKind.Sequential)] struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] struct HIDP_CAPS { public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved; public ushort a, b, c, d, e, f, g, h, i, j; }

    [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid g);
    [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr p);
    [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr p);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr p, out HIDP_CAPS caps);
    [DllImport("setupapi.dll")] static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr e, IntPtr h, int f);
    [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInterfaces(IntPtr s, IntPtr d, ref Guid g, int i, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", CharSet = CharSet.Auto)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr s, ref SP_DEVICE_INTERFACE_DATA d, IntPtr detail, int size, out int req, IntPtr info);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr s);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool ReadFile(SafeFileHandle h, byte[] buf, int len, out int read, IntPtr ov);

    static SafeFileHandle Open(string path)
    {
        var h = CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (h.IsInvalid) h = CreateFile(path, 0x80000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        return h;
    }

    // vid/pid 가 일치하고 최상위 Usage Page 가 usagePage 인 HID 경로
    public static string FindPath(int vid, int pid, int usagePage)
    {
        string tag = string.Format("vid_{0:x4}&pid_{1:x4}", vid, pid);
        Guid g; HidD_GetHidGuid(out g);
        IntPtr set = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0x12);
        try
        {
            for (int i = 0; ; i++)
            {
                var d = new SP_DEVICE_INTERFACE_DATA(); d.cbSize = Marshal.SizeOf(d);
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i, ref d)) break;
                int req; SetupDiGetDeviceInterfaceDetail(set, ref d, IntPtr.Zero, 0, out req, IntPtr.Zero);
                IntPtr buf = Marshal.AllocHGlobal(req);
                try
                {
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref d, buf, req, out req, IntPtr.Zero)) continue;
                    string p = Marshal.PtrToStringAuto(new IntPtr(buf.ToInt64() + 4));
                    if (!p.ToLower().Contains(tag)) continue;
                    using (var h = Open(p))
                    {
                        if (h.IsInvalid) continue;
                        IntPtr pp; HIDP_CAPS caps;
                        if (!HidD_GetPreparsedData(h, out pp)) continue;
                        HidP_GetCaps(pp, out caps); HidD_FreePreparsedData(pp);
                        if (caps.UsagePage == usagePage && caps.InputReportByteLength > 0) return p;
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return null;
    }

    public static void ReadLoop(string path, Action<byte[], int> onReport)
    {
        using (var h = Open(path))
        {
            if (h.IsInvalid) return;
            IntPtr pp; HIDP_CAPS caps;
            HidD_GetPreparsedData(h, out pp); HidP_GetCaps(pp, out caps); HidD_FreePreparsedData(pp);
            var b = new byte[caps.InputReportByteLength];
            int n;
            while (ReadFile(h, b, b.Length, out n, IntPtr.Zero)) onReport(b, n);
        }
    }
}

// ================== 오디오 (Core Audio / IPolicyConfig) ==================
static class Audio
{
    [StructLayout(LayoutKind.Sequential)] struct PROPERTYKEY { public Guid fmtid; public int pid; }
    [StructLayout(LayoutKind.Sequential)] struct PROPVARIANT { public ushort vt, r1, r2, r3; public IntPtr p; public IntPtr p2; }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice dev);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice dev);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr c);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr c);
    }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection { [PreserveSig] int GetCount(out int n); [PreserveSig] int Item(int i, out IMMDevice d); }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore ps);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int s);
    }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int c);
        [PreserveSig] int GetAt(int i, out PROPERTYKEY k);
        [PreserveSig] int GetValue(ref PROPERTYKEY k, out PROPVARIANT v);
        [PreserveSig] int SetValue(ref PROPERTYKEY k, ref PROPVARIANT v);
        [PreserveSig] int Commit();
    }
    [ComImport, Guid("568B9108-44BF-40B4-9006-86AFE5B5A620"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(IntPtr n, IntPtr f);
        [PreserveSig] int GetDeviceFormat(IntPtr n, int d, IntPtr f);
        [PreserveSig] int SetDeviceFormat(IntPtr n, IntPtr a, IntPtr b);
        [PreserveSig] int GetProcessingPeriod(IntPtr n, int d, IntPtr a, IntPtr b);
        [PreserveSig] int SetProcessingPeriod(IntPtr n, IntPtr p);
        [PreserveSig] int GetShareMode(IntPtr n, IntPtr m);
        [PreserveSig] int SetShareMode(IntPtr n, IntPtr m);
        [PreserveSig] int GetPropertyValue(IntPtr n, int b, IntPtr k, IntPtr v);
        [PreserveSig] int SetPropertyValue(IntPtr n, int b, IntPtr k, IntPtr v);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
        [PreserveSig] int SetEndpointVisibility(IntPtr n, int v);
    }

    [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT pv);

    public class Dev { public string Id, Name; }

    static IMMDeviceEnumerator Enumerator()
    {
        return (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
    }

    public static List<Dev> ListRender()
    {
        var res = new List<Dev>();
        IMMDeviceCollection col;
        Marshal.ThrowExceptionForHR(Enumerator().EnumAudioEndpoints(0, 1 /*ACTIVE*/, out col));
        int n; col.GetCount(out n);
        var key = new PROPERTYKEY { fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 14 };
        for (int i = 0; i < n; i++)
        {
            IMMDevice d; col.Item(i, out d);
            string id; d.GetId(out id);
            IPropertyStore ps; d.OpenPropertyStore(0, out ps);
            PROPVARIANT v; ps.GetValue(ref key, out v);
            string name = Marshal.PtrToStringUni(v.p); PropVariantClear(ref v);
            res.Add(new Dev { Id = id, Name = name ?? "" });
        }
        return res;
    }

    // role: 0 console, 1 multimedia, 2 communications
    public static string GetDefault(int role)
    {
        IMMDevice d;
        if (Enumerator().GetDefaultAudioEndpoint(0, role, out d) != 0) return null;
        string id; d.GetId(out id); return id;
    }

    public static void SetDefault(string id, int role)
    {
        var pc = (IPolicyConfig)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("294935CE-F637-4E7C-A41B-AB255460B862")));
        Marshal.ThrowExceptionForHR(pc.SetDefaultEndpoint(id, role));
    }
}
