using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PopoWorld.Core.Layout;
using PopoWorld.Core.Models;
using Serilog;

namespace PopoWorld.App.Overlay;

/// <summary>
/// 큰 표시 창. 완전히 클릭을 통과시키고(WS_EX_TRANSPARENT) 캐릭터·말풍선·공만 그린다.
/// 30fps 틱 루프를 돌리며 CharacterView 와 ProjectileView 를 갱신한다.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private double _lastTime;
    private readonly List<CharacterView> _characters = new();
    private readonly List<ProjectileView> _projectiles = new();
    private readonly Random _random = new();

    public IntPtr Handle { get; private set; }
    public EdgeLayout Layout { get; private set; }
    public MonitorInfo Monitor { get; private set; }
    public double Dpi { get; private set; } = 1.0;
    public bool WorldVisible { get; private set; } = true;

    public IReadOnlyList<CharacterView> Characters => _characters;
    public Random Random => _random;

    /// <summary>틱마다 먼저 불린다 (dt 초).</summary>
    public event Action<double>? Ticked;
    public event Action<CharacterView>? CharacterClicked;
    public event Action<CharacterView>? CharacterDoubleClicked;
    public event Action<CharacterView, PointD>? CharacterRightClicked;

    public OverlayWindow(MonitorInfo monitor, Edge edge, double span)
    {
        InitializeComponent();
        Monitor = monitor;
        Layout = new EdgeLayout(monitor.WorkArea, edge, span);

        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            Win32.AddExStyle(Handle, Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE);
            ApplyBounds();
        };
        DpiChanged += (_, e) =>
        {
            Dpi = e.NewDpi.DpiScaleX;
            ApplyBounds();
        };
        Loaded += (_, _) => Dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        _clock.Start();
        _lastTime = _clock.Elapsed.TotalSeconds;
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _clock.Stop();
    }

    /// <summary>모니터·변·비율이 바뀌면 다시 배치한다.</summary>
    public void Relayout(MonitorInfo monitor, Edge edge, double span)
    {
        Monitor = monitor;
        Layout = new EdgeLayout(monitor.WorkArea, edge, span);
        ApplyBounds();
        foreach (var c in _characters)
            c.Walk.SetRange(Layout.ClampX(0, c.HalfWidth), Layout.ClampX(Layout.Length, c.HalfWidth));
    }

    private void ApplyBounds()
    {
        if (Handle == IntPtr.Zero) return;
        var b = Layout.Band;
        Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, (int)b.X, (int)b.Y, (int)b.Width, (int)b.Height, Win32.SWP_NOACTIVATE);
        Dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
    }

    public void AddCharacter(CharacterView view)
    {
        _characters.Add(view);
        foreach (var el in view.Elements) Root.Children.Add(el);
        view.Hotspot.Owner = this;
        view.Hotspot.Clicked += _ => CharacterClicked?.Invoke(view);
        view.Hotspot.DoubleClicked += _ => CharacterDoubleClicked?.Invoke(view);
        view.Hotspot.RightClicked += p => CharacterRightClicked?.Invoke(view, p);
        view.Hotspot.DragStarted += p => view.Drag.BeginDrag(CurrentFoot(view), Layout.ToLocal(p));
        view.Hotspot.Dragging += p => view.Drag.DragTo(ClampToBand(Layout.ToLocal(p), view));
        view.Hotspot.DragEnded += _ => view.Drag.EndDrag();
        view.SetVisible(WorldVisible);
    }

    private PointD ClampToBand(PointD local, CharacterView view) =>
        new(Layout.ClampX(local.X, view.HalfWidth), Math.Clamp(local.Y, 0, EdgeLayout.Thickness - view.HalfWidth * 2));

    private PointD CurrentFoot(CharacterView view) => view.Drag.IsActive ? view.Drag.Position : new PointD(view.Walk.X, 0);

    public void RemoveCharacter(CharacterView view)
    {
        if (!_characters.Remove(view)) return;
        foreach (var el in view.Elements) Root.Children.Remove(el);
        view.Close();
    }

    public void AddProjectile(ProjectileView p)
    {
        _projectiles.Add(p);
        Root.Children.Add(p.Image);
    }

    /// <summary>숨기기 / 보이기 (설정 hidden, 전체화면 앱 감지).</summary>
    public void SetWorldVisible(bool visible)
    {
        WorldVisible = visible;
        if (visible)
        {
            if (!IsVisible) Show();
            foreach (var c in _characters) c.SetVisible(true);
        }
        else
        {
            foreach (var c in _characters) c.SetVisible(false);
            if (IsVisible) Hide();
        }
    }

    public IEnumerable<IntPtr> AllHandles()
    {
        yield return Handle;
        foreach (var c in _characters) yield return c.Hotspot.Handle;
    }

    private void Tick()
    {
        var t = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(t - _lastTime, 0, 0.1);
        _lastTime = t;

        try
        {
            Ticked?.Invoke(dt);

            if (!WorldVisible) return;

            var now = DateTime.UtcNow;
            var placed = new List<RectD>();
            var screen = Monitor.WorkArea;
            foreach (var c in _characters)
            {
                if (!c.Visible) continue;
                c.Update(dt, Layout, Dpi, Layout.Band, screen, placed, now, allowWalk: true);
            }

            for (var i = _projectiles.Count - 1; i >= 0; i--)
            {
                if (_projectiles[i].Update(dt, Dpi, Layout.Band))
                {
                    Root.Children.Remove(_projectiles[i].Image);
                    _projectiles.RemoveAt(i);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "틱 처리 중 오류");
        }
    }
}
