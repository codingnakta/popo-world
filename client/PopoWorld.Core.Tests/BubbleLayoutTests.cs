using PopoWorld.Core.Layout;
using PopoWorld.Core.Models;

namespace PopoWorld.Core.Tests;

public class BubbleLayoutTests
{
    private static readonly RectD Screen = new(0, 0, 1920, 1040);

    [Fact]
    public void Placed_above_head_by_gap()
    {
        var r = BubbleLayout.Place(new PointD(500, 900), new PointD(200, 60), Screen, [], Edge.Bottom);
        Assert.Equal(400, r.X);
        Assert.Equal(900 - 8 - 60, r.Y);
    }

    [Fact]
    public void Clamped_to_screen_horizontally()
    {
        var r = BubbleLayout.Place(new PointD(20, 900), new PointD(200, 60), Screen, [], Edge.Bottom);
        Assert.Equal(0, r.X);
        var r2 = BubbleLayout.Place(new PointD(1910, 900), new PointD(200, 60), Screen, [], Edge.Bottom);
        Assert.Equal(1720, r2.X);
    }

    [Fact]
    public void Flips_below_when_no_room_above()
    {
        var r = BubbleLayout.Place(new PointD(500, 30), new PointD(200, 60), Screen, [], Edge.Bottom);
        Assert.Equal(38, r.Y);
    }

    [Fact]
    public void Moves_up_in_steps_when_overlapping()
    {
        var first = BubbleLayout.Place(new PointD(500, 900), new PointD(200, 60), Screen, [], Edge.Bottom);
        var second = BubbleLayout.Place(new PointD(520, 900), new PointD(200, 60), Screen, [first], Edge.Bottom);
        Assert.False(BubbleLayout.Overlaps(first, second));
        Assert.Equal(first.Y - 24 * 3, second.Y);
    }

    [Fact]
    public void Top_edge_places_below_head()
    {
        var r = BubbleLayout.Place(new PointD(500, 100), new PointD(200, 60), Screen, [], Edge.Top);
        Assert.Equal(108, r.Y);
    }
}
