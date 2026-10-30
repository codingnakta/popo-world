using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Win32;
using PopoWorld.App.Overlay;
using PopoWorld.Core.Models;

namespace PopoWorld.App.Services;

/// <summary>
/// 내 상태(online / idle) 감지. 5분 동안 입력이 없거나 잠금 화면이면 idle.
/// 60초마다 LastSeenTick 을 올려 users.last_seen 을 갱신하게 한다.
/// </summary>
public sealed class PresenceService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _lastSeenTimer;
    private bool _locked;

    public PresenceState State { get; private set; } = PresenceState.Online;

    public event Action<PresenceState>? StateChanged;
    public event Action? LastSeenTick;

    public PresenceService()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Evaluate();
        _lastSeenTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = PopoRules.LastSeenInterval };
        _lastSeenTimer.Tick += (_, _) => LastSeenTick?.Invoke();
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    public void Start()
    {
        _timer.Start();
        _lastSeenTimer.Start();
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        _locked = e.Reason switch
        {
            SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect => true,
            SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect => false,
            _ => _locked,
        };
        Evaluate();
    }

    private void Evaluate()
    {
        var idle = _locked || IdleTime() >= PopoRules.IdleAfter;
        var next = idle ? PresenceState.Idle : PresenceState.Online;
        if (next != State)
        {
            State = next;
            StateChanged?.Invoke(next);
        }
    }

    public static TimeSpan IdleTime()
    {
        var info = new Win32.LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<Win32.LASTINPUTINFO>() };
        if (!Win32.GetLastInputInfo(ref info)) return TimeSpan.Zero;
        var ticks = unchecked((uint)Environment.TickCount) - info.dwTime;
        return TimeSpan.FromMilliseconds(ticks);
    }

    public void Dispose()
    {
        _timer.Stop();
        _lastSeenTimer.Stop();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }
}
