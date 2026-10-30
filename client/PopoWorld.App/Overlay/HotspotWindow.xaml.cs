using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PopoWorld.Core.Models;
using PopoWorld.Core.Physics;

namespace PopoWorld.App.Overlay;

/// <summary>
/// 캐릭터 밑에 따라다니는 작은 입력 창. 완전 투명(알파 1/255)이지만 클릭·더블클릭·우클릭·드래그를 받는다.
/// WS_EX_NOACTIVATE 라 클릭해도 작업 중인 앱의 포커스를 뺏지 않는다.
/// </summary>
public partial class HotspotWindow : Window
{
    private Point _downPoint;
    private bool _leftDown;
    private bool _dragging;

    public IntPtr Handle { get; private set; }

    /// <summary>화면 물리 픽셀 좌표를 넘긴다.</summary>
    public event Action<PointD>? Clicked;
    public event Action<PointD>? DoubleClicked;
    public event Action<PointD>? RightClicked;
    public event Action<PointD>? DragStarted;
    public event Action<PointD>? Dragging;
    public event Action<PointD>? DragEnded;

    public HotspotWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            Win32.AddExStyle(Handle, Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE);
        };
    }

    /// <summary>물리 픽셀로 창을 옮긴다.</summary>
    public void MoveTo(RectD rect)
    {
        if (Handle == IntPtr.Zero) return;
        Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST,
            (int)Math.Round(rect.X), (int)Math.Round(rect.Y),
            (int)Math.Max(1, Math.Round(rect.Width)), (int)Math.Max(1, Math.Round(rect.Height)),
            Win32.SWP_NOACTIVATE);
    }

    private PointD Screen(MouseEventArgs e)
    {
        var p = PointToScreen(e.GetPosition(this));
        return new PointD(p.X, p.Y);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ClickCount == 2)
        {
            DoubleClicked?.Invoke(Screen(e));
            _leftDown = false;
            e.Handled = true;
            return;
        }
        _leftDown = true;
        _dragging = false;
        _downPoint = PointToScreen(e.GetPosition(this));
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_leftDown) return;
        var now = PointToScreen(e.GetPosition(this));
        if (!_dragging)
        {
            var dx = now.X - _downPoint.X;
            var dy = now.Y - _downPoint.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < DragSpring.DragThreshold) return;
            _dragging = true;
            DragStarted?.Invoke(new PointD(_downPoint.X, _downPoint.Y));
        }
        Dragging?.Invoke(new PointD(now.X, now.Y));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_leftDown) return;
        _leftDown = false;
        ReleaseMouseCapture();
        var p = Screen(e);
        if (_dragging)
        {
            _dragging = false;
            DragEnded?.Invoke(p);
        }
        else
        {
            Clicked?.Invoke(p);
        }
        e.Handled = true;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        RightClicked?.Invoke(Screen(e));
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_leftDown && _dragging)
        {
            _leftDown = false;
            _dragging = false;
            var p = PointToScreen(Mouse.GetPosition(this));
            DragEnded?.Invoke(new PointD(p.X, p.Y));
        }
        _leftDown = false;
    }
}
