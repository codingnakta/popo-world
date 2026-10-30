namespace PopoWorld.Core.Models;

/// <summary>WPF에 의존하지 않는 2D 점 (물리 픽셀).</summary>
public readonly record struct PointD(double X, double Y)
{
    public static PointD operator +(PointD a, PointD b) => new(a.X + b.X, a.Y + b.Y);
    public static PointD operator -(PointD a, PointD b) => new(a.X - b.X, a.Y - b.Y);
    public static PointD operator *(PointD a, double k) => new(a.X * k, a.Y * k);

    public double Length => Math.Sqrt(X * X + Y * Y);

    public static double Distance(PointD a, PointD b) => (a - b).Length;

    public static PointD Lerp(PointD a, PointD b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}

/// <summary>WPF에 의존하지 않는 사각형 (물리 픽셀).</summary>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public bool Contains(PointD p) => p.X >= X && p.X < Right && p.Y >= Y && p.Y < Bottom;
}
