using System.Text;
using System.Windows.Threading;
using PopoWorld.App.Overlay;

namespace PopoWorld.App.Services;

/// <summary>
/// 1초마다 전경 창을 검사해 그 창이 모니터 전체를 덮고 있으면 전체화면 앱(게임 등)으로 본다.
/// 우리 창과 바탕화면·작업표시줄은 제외한다.
/// </summary>
public sealed class FullscreenDetector : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Func<IEnumerable<IntPtr>> _ownWindows;

    public bool IsFullscreen { get; private set; }

    public event Action<bool>? Changed;

    public FullscreenDetector(Func<IEnumerable<IntPtr>> ownWindows)
    {
        _ownWindows = ownWindows;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Check();
    }

    public void Start() => _timer.Start();

    private void Check()
    {
        var now = Detect();
        if (now != IsFullscreen)
        {
            IsFullscreen = now;
            Changed?.Invoke(now);
        }
    }

    private bool Detect()
    {
        var hwnd = Win32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;
        if (_ownWindows().Contains(hwnd)) return false;

        var cls = new StringBuilder(64);
        Win32.GetClassName(hwnd, cls, cls.Capacity);
        var className = cls.ToString();
        if (className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;

        if (!Win32.GetWindowRect(hwnd, out var rect)) return false;
        var monitor = Monitors.FromWindow(hwnd);
        if (monitor is null) return false;

        var b = monitor.Bounds;
        return rect.Left <= b.Left && rect.Top <= b.Top && rect.Right >= b.Right && rect.Bottom >= b.Bottom;
    }

    public void Dispose() => _timer.Stop();
}
