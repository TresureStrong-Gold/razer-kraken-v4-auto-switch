using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

// Kraken V4 동글(VID 1532 / PID 056C)의 HID 입력 리포트를 모두 출력하는 진단 도구
class HidDiag
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

    static DateTime t0 = DateTime.Now;
    static object lk = new object();

    static string filter = "vid_1532&pid_056c";
    static List<string> Paths()
    {
        var res = new List<string>();
        Guid g; HidD_GetHidGuid(out g);
        IntPtr set = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0x12);
        for (int i = 0; ; i++)
        {
            var d = new SP_DEVICE_INTERFACE_DATA(); d.cbSize = Marshal.SizeOf(d);
            if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i, ref d)) break;
            int req; SetupDiGetDeviceInterfaceDetail(set, ref d, IntPtr.Zero, 0, out req, IntPtr.Zero);
            IntPtr buf = Marshal.AllocHGlobal(req);
            Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
            if (SetupDiGetDeviceInterfaceDetail(set, ref d, buf, req, out req, IntPtr.Zero))
            {
                string p = Marshal.PtrToStringAuto(new IntPtr(buf.ToInt64() + 4));
                if (p.ToLower().Contains(filter)) res.Add(p);
            }
            Marshal.FreeHGlobal(buf);
        }
        SetupDiDestroyDeviceInfoList(set);
        return res;
    }

    static void Log(string s) { lock (lk) Console.WriteLine("[{0,6:F1}s] {1}", (DateTime.Now - t0).TotalSeconds, s); }

    static void Reader(string path)
    {
        string tag = path.Substring(path.IndexOf("mi_"), 6) + path.Substring(path.IndexOf("col") > 0 ? path.IndexOf("col") : 0, path.IndexOf("col") > 0 ? 5 : 0);
        var h = CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (h.IsInvalid) h = CreateFile(path, 0x80000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (h.IsInvalid) { Log(tag + " open fail " + Marshal.GetLastWin32Error()); return; }
        IntPtr pp; HIDP_CAPS caps;
        HidD_GetPreparsedData(h, out pp); HidP_GetCaps(pp, out caps); HidD_FreePreparsedData(pp);
        Log(string.Format("{0} usagePage=0x{1:X} usage=0x{2:X} inLen={3}", tag, caps.UsagePage, caps.Usage, caps.InputReportByteLength));
        if (caps.InputReportByteLength == 0) return;
        var b = new byte[caps.InputReportByteLength];
        while (true)
        {
            int n;
            if (!ReadFile(h, b, b.Length, out n, IntPtr.Zero)) { Log(tag + " read err " + Marshal.GetLastWin32Error()); return; }
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++) sb.AppendFormat("{0:X2} ", b[i]);
            Log(tag + " : " + sb);
        }
    }

    static void Main(string[] args)
    {
        if (args.Length > 0) filter = args[0].ToLower();
        foreach (var p in Paths()) { var pp = p; new Thread(() => Reader(pp)) { IsBackground = true }.Start(); }
        Thread.Sleep(int.Parse(Environment.GetEnvironmentVariable("DIAG_SECS") ?? "60") * 1000);
    }
}
