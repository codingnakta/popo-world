using PopoWorld.Core.Physics;

namespace PopoWorld.Core.Tests;

public class WalkAgentTests
{
    [Fact]
    public void Starts_idle_then_walks_to_target_and_stops()
    {
        var a = new WalkAgent(0, 800, new Random(1), startX: 400);
        Assert.Equal(WalkState.Idle, a.State);

        // idle 최대 6초 뒤엔 걷기 시작
        a.Step(6.1);
        Assert.Equal(WalkState.Walk, a.State);
        var target = a.TargetX;
        Assert.NotEqual(400, target);
        Assert.Equal(Math.Sign(target - 400), a.Facing);

        // 40px/s 로 가면 800px 는 20초 안에 도착
        for (var i = 0; i < 20 * 30 && a.State == WalkState.Walk; i++) a.Step(1.0 / 30);
        Assert.Equal(WalkState.Idle, a.State);
        Assert.Equal(target, a.X, 3);
    }

    [Fact]
    public void Speed_is_40_px_per_second()
    {
        var a = new WalkAgent(0, 1000, new Random(2), startX: 0);
        a.Step(6.1);
        Assert.Equal(WalkState.Walk, a.State);
        var x0 = a.X;
        a.Step(1);
        Assert.Equal(40, Math.Abs(a.X - x0), 3);
    }

    [Fact]
    public void Never_leaves_range()
    {
        var a = new WalkAgent(100, 300, new Random(3));
        for (var i = 0; i < 30 * 120; i++)
        {
            a.Step(1.0 / 30);
            Assert.InRange(a.X, 100, 300);
        }
    }

    [Fact]
    public void Frozen_agent_does_not_move()
    {
        var a = new WalkAgent(0, 800, new Random(4), startX: 100) { Frozen = true };
        a.Step(100);
        Assert.Equal(100, a.X);
        Assert.Equal(WalkState.Idle, a.State);
    }

    [Fact]
    public void ResetAt_clamps_and_idles()
    {
        var a = new WalkAgent(0, 800, new Random(5), startX: 100);
        a.ResetAt(5000);
        Assert.Equal(800, a.X);
        Assert.Equal(WalkState.Idle, a.State);
    }

    [Fact]
    public void SetRange_clamps_position()
    {
        var a = new WalkAgent(0, 800, new Random(6), startX: 700);
        a.SetRange(0, 400);
        Assert.Equal(400, a.X);
    }
}
