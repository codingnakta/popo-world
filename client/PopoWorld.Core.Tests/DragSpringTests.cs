using PopoWorld.Core.Models;
using PopoWorld.Core.Physics;

namespace PopoWorld.Core.Tests;

public class DragSpringTests
{
    [Fact]
    public void Follows_mouse_with_grab_offset()
    {
        var s = new DragSpring();
        s.BeginDrag(new PointD(100, 0), new PointD(110, 20));
        s.DragTo(new PointD(210, 120));
        Assert.Equal(new PointD(200, 100), s.Position);
        Assert.True(s.IsDragging);
    }

    [Fact]
    public void Returns_to_floor_after_release()
    {
        var s = new DragSpring();
        s.BeginDrag(new PointD(100, 0), new PointD(100, 0));
        s.DragTo(new PointD(150, 120));
        s.EndDrag();
        Assert.True(s.IsReturning);

        var ticks = 0;
        while (s.IsReturning && ticks++ < 30 * 10) s.Step(1.0 / 30);

        Assert.False(s.IsReturning);
        Assert.Equal(0, s.Position.Y);
        Assert.Equal(150, s.Position.X);
        Assert.True(ticks < 30 * 5, $"settled in {ticks} ticks");
    }

    [Fact]
    public void Never_goes_below_floor_while_dragging()
    {
        var s = new DragSpring();
        s.BeginDrag(new PointD(100, 0), new PointD(100, 0));
        s.DragTo(new PointD(100, -50));
        Assert.Equal(0, s.Position.Y);
    }

    [Fact]
    public void Release_on_floor_is_not_returning()
    {
        var s = new DragSpring();
        s.BeginDrag(new PointD(100, 0), new PointD(100, 0));
        s.EndDrag();
        Assert.False(s.IsActive);
    }
}
