using PopoWorld.Core.Models;
using PopoWorld.Core.Physics;

namespace PopoWorld.Core.Tests;

public class ThrowArcTests
{
    [Fact]
    public void Starts_at_from_and_ends_at_to()
    {
        var arc = new ThrowArc(new PointD(0, 500), new PointD(400, 500));
        Assert.Equal(new PointD(0, 500), arc.Position);
        arc.Step(0.6);
        Assert.True(arc.Done);
        Assert.Equal(new PointD(400, 500), arc.Position);
    }

    [Fact]
    public void Midpoint_is_above_the_line()
    {
        var arc = new ThrowArc(new PointD(0, 500), new PointD(400, 500));
        var mid = arc.PositionAt(0.5);
        Assert.Equal(200, mid.X);
        Assert.Equal(500 - arc.ArcHeight, mid.Y, 6);
        Assert.Equal(100, arc.ArcHeight);
    }

    [Fact]
    public void Duration_is_0_6_seconds_by_default()
    {
        var arc = new ThrowArc(new PointD(0, 0), new PointD(10, 0));
        for (var i = 0; i < 17; i++) arc.Step(1.0 / 30); // 0.566s
        Assert.False(arc.Done);
        arc.Step(1.0 / 30);
        Assert.True(arc.Done);
    }

    [Fact]
    public void Minimum_arc_height_is_40()
    {
        var arc = new ThrowArc(new PointD(0, 0), new PointD(10, 0));
        Assert.Equal(40, arc.ArcHeight);
    }
}
