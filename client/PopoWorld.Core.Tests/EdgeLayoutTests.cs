using PopoWorld.Core.Layout;
using PopoWorld.Core.Models;

namespace PopoWorld.Core.Tests;

public class EdgeLayoutTests
{
    private static readonly RectD Work = new(0, 0, 1920, 1040);

    [Fact]
    public void Bottom_band_is_centered_and_sized_by_span()
    {
        var l = new EdgeLayout(Work, Edge.Bottom, 0.5);
        Assert.Equal(960, l.Band.Width);
        Assert.Equal(EdgeLayout.Thickness, l.Band.Height);
        Assert.Equal(480, l.Band.X);
        Assert.Equal(1040 - EdgeLayout.Thickness, l.Band.Y);
        Assert.Equal(960, l.Length);
    }

    [Fact]
    public void Left_band_is_vertical()
    {
        var l = new EdgeLayout(Work, Edge.Left, 1.0 / 3.0);
        Assert.Equal(EdgeLayout.Thickness, l.Band.Width);
        Assert.Equal(346, l.Band.Height);
        Assert.Equal(0, l.Band.X);
        Assert.Equal(347, l.Band.Y);
    }

    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void ToScreen_and_ToLocal_round_trip(Edge edge)
    {
        var l = new EdgeLayout(Work, edge, 1);
        var local = new PointD(123, 45);
        var screen = l.ToScreen(local);
        var back = l.ToLocal(screen);
        Assert.Equal(local.X, back.X, 6);
        Assert.Equal(local.Y, back.Y, 6);
        Assert.True(l.Band.Contains(screen) || screen.X == l.Band.Right || screen.Y == l.Band.Bottom);
    }

    [Fact]
    public void Floor_is_on_the_chosen_edge()
    {
        Assert.Equal(1040, new EdgeLayout(Work, Edge.Bottom, 1).ToScreen(new PointD(10, 0)).Y);
        Assert.Equal(0, new EdgeLayout(Work, Edge.Top, 1).ToScreen(new PointD(10, 0)).Y);
        Assert.Equal(0, new EdgeLayout(Work, Edge.Left, 1).ToScreen(new PointD(10, 0)).X);
        Assert.Equal(1920, new EdgeLayout(Work, Edge.Right, 1).ToScreen(new PointD(10, 0)).X);
    }

    [Fact]
    public void Right_edge_height_moves_inward()
    {
        var l = new EdgeLayout(Work, Edge.Right, 1);
        Assert.True(l.ToScreen(new PointD(0, 50)).X < l.ToScreen(new PointD(0, 0)).X);
    }

    [Fact]
    public void ClampX_keeps_character_inside_band()
    {
        var l = new EdgeLayout(Work, Edge.Bottom, 0.5);
        Assert.Equal(48, l.ClampX(-10, 48));
        Assert.Equal(960 - 48, l.ClampX(5000, 48));
        Assert.Equal(300, l.ClampX(300, 48));
    }

    [Fact]
    public void Sprite_transform_flips_by_edge()
    {
        Assert.Equal(new SpriteTransform(true, false), new EdgeLayout(Work, Edge.Bottom, 1).SpriteFor(-1));
        Assert.Equal(new SpriteTransform(false, true), new EdgeLayout(Work, Edge.Top, 1).SpriteFor(1));
        Assert.Equal(new SpriteTransform(false, false), new EdgeLayout(Work, Edge.Left, 1).SpriteFor(-1));
        Assert.Equal(new SpriteTransform(true, false), new EdgeLayout(Work, Edge.Right, 1).SpriteFor(1));
    }

    [Fact]
    public void Sprite_top_left_puts_feet_on_floor_for_bottom()
    {
        var l = new EdgeLayout(Work, Edge.Bottom, 1);
        var tl = l.SpriteTopLeft(new PointD(100, 0), 96, 96, new PointD(48, 92), new SpriteTransform(false, false));
        Assert.Equal(100 - 48, tl.X);
        Assert.Equal(1040 - 92, tl.Y);
    }
}
