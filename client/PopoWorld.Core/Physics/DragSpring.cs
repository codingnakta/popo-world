using PopoWorld.Core.Models;

namespace PopoWorld.Core.Physics;

/// <summary>
/// 드래그 중엔 마우스를 따라가고, 놓으면 감쇠 스프링(강성 120, 감쇠 14)으로 띠 바닥(y=0)으로 돌아온다.
/// 좌표는 로컬(x 진행, y 높이).
/// </summary>
public sealed class DragSpring
{
    public const double Stiffness = 120;
    public const double Damping = 14;
    public const double SettleDistance = 0.5;
    public const double SettleVelocity = 2;

    /// <summary>드래그로 판정하는 최소 이동 거리(px).</summary>
    public const double DragThreshold = 4;

    public PointD Position { get; private set; }
    public double VelocityY { get; private set; }

    public bool IsDragging { get; private set; }

    /// <summary>놓은 뒤 아직 튀고 있는 중.</summary>
    public bool IsReturning { get; private set; }

    public bool IsActive => IsDragging || IsReturning;

    private PointD _grabOffset;

    public void BeginDrag(PointD characterFoot, PointD mouse)
    {
        IsDragging = true;
        IsReturning = false;
        Position = characterFoot;
        _grabOffset = characterFoot - mouse;
        VelocityY = 0;
    }

    public void DragTo(PointD mouse)
    {
        if (!IsDragging) return;
        var p = mouse + _grabOffset;
        Position = new PointD(p.X, Math.Max(0, p.Y));
    }

    /// <summary>놓는다. 이후 Step 이 바닥으로 되돌린다.</summary>
    public void EndDrag()
    {
        if (!IsDragging) return;
        IsDragging = false;
        IsReturning = Position.Y > SettleDistance;
        VelocityY = 0;
    }

    public void Step(double dt)
    {
        if (!IsReturning || dt <= 0) return;

        // 안정성을 위해 큰 dt는 잘게 쪼갠다.
        var steps = Math.Max(1, (int)Math.Ceiling(dt / (1.0 / 120)));
        var h = dt / steps;
        for (var i = 0; i < steps; i++)
        {
            var accel = -Stiffness * Position.Y - Damping * VelocityY;
            VelocityY += accel * h;
            var y = Position.Y + VelocityY * h;
            if (y < 0)
            {
                // 바닥에 닿으면 조금 튄다 (통통)
                y = 0;
                VelocityY = -VelocityY * 0.35;
            }
            Position = new PointD(Position.X, y);
        }

        if (Position.Y <= SettleDistance && Math.Abs(VelocityY) <= SettleVelocity)
        {
            Position = new PointD(Position.X, 0);
            VelocityY = 0;
            IsReturning = false;
        }
    }
}
