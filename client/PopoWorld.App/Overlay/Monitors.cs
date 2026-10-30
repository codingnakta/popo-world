using System.Runtime.InteropServices;
using PopoWorld.Core.Models;

namespace PopoWorld.App.Overlay;

/// <summary>모니터 하나. 좌표는 물리 픽셀.</summary>
public sealed record MonitorInfo(string Name, RectD Bounds, RectD WorkArea, bool IsPrimary, IntPtr Handle)
{
    public override string ToString() => $"{Name} ({Bounds.Width}x{Bounds.Height}){(IsPrimary ? " 주" : "")}";
}

public static class Monitors
{
    public static List<MonitorInfo> All()
    {
        var list = new List<MonitorInfo>();
        Win32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr _, ref Win32.RECT _, IntPtr _) =>
        {
            var info = Get(h);
            if (info is not null) list.Add(info);
            return true;
        }, IntPtr.Zero);
        return list.OrderByDescending(m => m.IsPrimary).ThenBy(m => m.Name).ToList();
    }

    public static MonitorInfo? Get(IntPtr hMonitor)
    {
        var mi = new Win32.MONITORINFOEX { cbSize = Marshal.SizeOf<Win32.MONITORINFOEX>() };
        if (!Win32.GetMonitorInfo(hMonitor, ref mi)) return null;
        return new MonitorInfo(
            mi.szDevice,
            new RectD(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Width, mi.rcMonitor.Height),
            new RectD(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Width, mi.rcWork.Height),
            (mi.dwFlags & Win32.MONITORINFOF_PRIMARY) != 0,
            hMonitor);
    }

    public static MonitorInfo? FromWindow(IntPtr hwnd)
    {
        var h = Win32.MonitorFromWindow(hwnd, Win32.MONITOR_DEFAULTTONEAREST);
        return h == IntPtr.Zero ? null : Get(h);
    }

    /// <summary>설정의 장치 이름으로 찾고, 없으면 주 모니터.</summary>
    public static MonitorInfo Resolve(string? name)
    {
        var all = All();
        if (all.Count == 0)
            return new MonitorInfo(@"\\.\DISPLAY1", new RectD(0, 0, 1920, 1080), new RectD(0, 0, 1920, 1040), true, IntPtr.Zero);
        return all.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? all.First(m => m.IsPrimary);
    }
}
