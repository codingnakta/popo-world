using PopoWorld.Core.Models;

namespace PopoWorld.Core.Physics;

/// <summary>
/// 공·선물 상자의 포물선 궤적. 화면 좌표(y 아래가 +)로 계산한다. 기본 0.6초.
/// </summary>
public sealed class ThrowArc
{
    public const double DefaultDuration = 0.6;

    public PointD From { get; }
    public PointD To { get; }
    public double Duration { get; }

    /// <summary>궤적 최고점 높이. 거리에 비례하되 최소 40px.</summary>
    public double ArcHeight { get; }

    public double Elapsed { get; private set; }

    public double Progress => Duration <= 0 ? 1 : Math.Clamp(Elapsed / Duration, 0, 1);
    public bool Done => Elapsed >= Duration;

    public ThrowArc(PointD from, PointD to, double duration = DefaultDuration)
    {
        From = from;
        To = to;
        Duration = duration;
        ArcHeight = Math.Max(40, PointD.Distance(from, to) * 0.25);
    }

    public void Step(double dt)
    {
        if (dt > 0) Elapsed = Math.Min(Duration, Elapsed + dt);
    }

    public PointD Position => PositionAt(Progress);

    public PointD PositionAt(double t)
    {
        t = Math.Clamp(t, 0, 1);
        var p = PointD.Lerp(From, To, t);
        var lift = ArcHeight * 4 * t * (1 - t);
        return new PointD(p.X, p.Y - lift);
    }

    /// <summary>회전 각도(도). 공이 굴러가는 느낌.</summary>
    public double Angle => Progress * 360 * (To.X >= From.X ? 1 : -1);
}
