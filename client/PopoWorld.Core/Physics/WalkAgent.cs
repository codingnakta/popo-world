namespace PopoWorld.Core.Physics;

public enum WalkState
{
    Idle,
    Walk,
}

/// <summary>
/// 캐릭터 걷기 상태기계: idle → walk → idle. 목표 x는 띠 안의 난수, 속도 40px/s, idle 2~6초.
/// 좌표는 로컬(x 진행 방향)이고 단위는 물리 픽셀.
/// </summary>
public sealed class WalkAgent
{
    public const double Speed = 40;
    public const double IdleMin = 2;
    public const double IdleMax = 6;
    public const double ArriveEpsilon = 0.5;

    private readonly Random _random;

    public double MinX { get; private set; }
    public double MaxX { get; private set; }

    public double X { get; set; }
    public WalkState State { get; private set; } = WalkState.Idle;

    /// <summary>+1 오른쪽(진행 방향), -1 왼쪽.</summary>
    public int Facing { get; private set; } = 1;

    public double TargetX { get; private set; }

    /// <summary>true면 걷지 않는다 (졸기, 드래그 중, 오프라인, 앉음).</summary>
    public bool Frozen { get; set; }

    private double _idleRemaining;

    public WalkAgent(double minX, double maxX, Random? random = null, double? startX = null)
    {
        _random = random ?? Random.Shared;
        SetRange(minX, maxX);
        X = startX is { } s ? Math.Clamp(s, MinX, MaxX) : Lerp(MinX, MaxX, _random.NextDouble());
        TargetX = X;
        _idleRemaining = NextIdle();
    }

    public void SetRange(double minX, double maxX)
    {
        if (maxX < minX) (minX, maxX) = (maxX, minX);
        MinX = minX;
        MaxX = maxX;
        X = Math.Clamp(X, MinX, MaxX);
        TargetX = Math.Clamp(TargetX, MinX, MaxX);
    }

    /// <summary>드래그로 옮겨진 뒤 그 자리에서 다시 시작할 때.</summary>
    public void ResetAt(double x)
    {
        X = Math.Clamp(x, MinX, MaxX);
        TargetX = X;
        State = WalkState.Idle;
        _idleRemaining = NextIdle();
    }

    public void Step(double dt)
    {
        if (dt <= 0 || Frozen) return;

        switch (State)
        {
            case WalkState.Idle:
                _idleRemaining -= dt;
                if (_idleRemaining <= 0)
                {
                    TargetX = PickTarget();
                    Facing = TargetX >= X ? 1 : -1;
                    State = WalkState.Walk;
                }
                break;

            case WalkState.Walk:
                var dir = Math.Sign(TargetX - X);
                if (dir != 0) Facing = dir;
                var step = Speed * dt;
                if (Math.Abs(TargetX - X) <= Math.Max(step, ArriveEpsilon))
                {
                    X = TargetX;
                    State = WalkState.Idle;
                    _idleRemaining = NextIdle();
                }
                else
                {
                    X += dir * step;
                }
                break;
        }
    }

    private double PickTarget()
    {
        if (MaxX - MinX < 1) return MinX;
        // 너무 가까운 목표는 다시 뽑아 "걷는 느낌"이 나게 한다.
        for (var i = 0; i < 4; i++)
        {
            var t = Lerp(MinX, MaxX, _random.NextDouble());
            if (Math.Abs(t - X) >= Math.Min(40, (MaxX - MinX) / 4)) return t;
        }
        return Lerp(MinX, MaxX, _random.NextDouble());
    }

    private double NextIdle() => Lerp(IdleMin, IdleMax, _random.NextDouble());

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
