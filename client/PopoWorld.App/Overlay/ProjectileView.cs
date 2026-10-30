using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PopoWorld.Core.Models;
using PopoWorld.Core.Physics;

namespace PopoWorld.App.Overlay;

/// <summary>날아가는 공 또는 선물 상자.</summary>
public sealed class ProjectileView
{
    public Image Image { get; } = new();
    public ThrowArc Arc { get; }
    public Action? OnArrive { get; }
    public double Size { get; }

    private readonly RotateTransform _rotate = new();

    public ProjectileView(BitmapSource? bitmap, PointD from, PointD to, double size, Action? onArrive)
    {
        Arc = new ThrowArc(from, to);
        OnArrive = onArrive;
        Size = size;
        Image.Source = bitmap;
        RenderOptions.SetBitmapScalingMode(Image, BitmapScalingMode.NearestNeighbor);
        Image.RenderTransform = _rotate;
        Image.RenderTransformOrigin = new Point(0.5, 0.5);
        Image.IsHitTestVisible = false;
        if (bitmap is null)
        {
            // 그림이 없으면 둥근 점
            var rt = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen()) dc.DrawEllipse(Brushes.HotPink, null, new Point(8, 8), 7, 7);
            rt.Render(dv);
            rt.Freeze();
            Image.Source = rt;
        }
    }

    /// <summary>true 면 도착해서 치워도 된다.</summary>
    public bool Update(double dt, double dpi, RectD overlayOrigin)
    {
        Arc.Step(dt);
        var p = Arc.Position;
        Canvas.SetLeft(Image, (p.X - Size / 2 - overlayOrigin.X) / dpi);
        Canvas.SetTop(Image, (p.Y - Size / 2 - overlayOrigin.Y) / dpi);
        Image.Width = Size / dpi;
        Image.Height = Size / dpi;
        _rotate.Angle = Arc.Angle;
        if (Arc.Done)
        {
            OnArrive?.Invoke();
            return true;
        }
        return false;
    }
}
