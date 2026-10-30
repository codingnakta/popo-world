using PopoWorld.Core.Models;

namespace PopoWorld.Core.Tests;

public class EventFilterTests
{
    [Fact]
    public void Drops_duplicates()
    {
        var f = new EventFilter();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Assert.True(f.ShouldPlay(id, now, now));
        Assert.False(f.ShouldPlay(id, now, now));
    }

    [Fact]
    public void Drops_events_older_than_10_seconds()
    {
        var f = new EventFilter();
        var now = DateTimeOffset.UtcNow;
        Assert.False(f.ShouldPlay(Guid.NewGuid(), now.AddSeconds(-11), now));
        Assert.True(f.ShouldPlay(Guid.NewGuid(), now.AddSeconds(-9), now));
    }

    [Fact]
    public void Remembers_only_last_200()
    {
        var f = new EventFilter();
        var now = DateTimeOffset.UtcNow;
        var first = Guid.NewGuid();
        f.ShouldPlay(first, now, now);
        for (var i = 0; i < 200; i++) f.ShouldPlay(Guid.NewGuid(), now, now);
        Assert.Equal(200, f.Count);
        Assert.True(f.ShouldPlay(first, now, now)); // 밀려나서 다시 재생됨
    }
}

public class RpcResultTests
{
    [Fact]
    public void Parses_success()
    {
        var r = RpcResult.Parse("{\"ok\":true,\"balance\":27}");
        Assert.True(r.Ok);
        Assert.Equal(27, r.Balance);
        Assert.Null(r.ErrorCode);
    }

    [Fact]
    public void Parses_insufficient_with_shortfall()
    {
        var r = RpcResult.Parse("{\"ok\":false,\"error\":\"insufficient:3\",\"balance\":7}");
        Assert.False(r.Ok);
        Assert.Equal("insufficient", r.ErrorCode);
        Assert.Equal(3, r.Shortfall);
        Assert.Equal("3포포 부족", r.Describe());
    }

    [Fact]
    public void Bad_json_is_a_failure_not_an_exception()
    {
        Assert.Equal("bad_response", RpcResult.Parse("not json").Error);
        Assert.Equal("empty_response", RpcResult.Parse("").Error);
    }
}

public class AppSettingsTests
{
    [Fact]
    public void Round_trips_through_json()
    {
        var s = new AppSettings { Monitor = @"\\.\DISPLAY1", Edge = "left", Span = 1.0 / 3, QuietMode = true, ComposerPosition = new PositionSettings { X = 400, Y = 900 } };
        var back = AppSettings.FromJson(s.ToJson());
        Assert.Equal(Edge.Left, back.EdgeValue);
        Assert.Equal(1.0 / 3, back.Span, 6);
        Assert.True(back.QuietMode);
        Assert.Equal(400, back.ComposerPosition!.X);
        Assert.Equal("Ctrl+Shift+Space", back.Hotkeys.Composer);
    }

    [Fact]
    public void Normalize_fixes_bad_values()
    {
        var s = AppSettings.FromJson("{\"edge\":\"diagonal\",\"span\":7,\"sound\":{\"volume\":3}}");
        Assert.Equal(Edge.Bottom, s.EdgeValue);
        Assert.Equal(1.0, s.Span);
        Assert.Equal(1.0, s.Sound.Volume);
    }

    [Fact]
    public void Spec_example_parses()
    {
        const string json = """
        {
          "version": 1,
          "monitor": "\\\\.\\DISPLAY1",
          "edge": "bottom",
          "span": 0.5,
          "showOffline": true,
          "quietMode": false,
          "hidden": false,
          "sound": { "enabled": true, "volume": 0.6 },
          "hotkeys": { "composer": "Ctrl+Shift+Space", "toggleWorld": "Ctrl+Shift+H" },
          "composerPosition": { "x": 400, "y": 900 },
          "startWithWindows": false
        }
        """;
        var s = AppSettings.FromJson(json);
        Assert.Equal(@"\\.\DISPLAY1", s.Monitor);
        Assert.Equal(0.5, s.Span);
        Assert.Equal(0.6, s.Sound.Volume);
    }
}

public class CharacterManifestTests
{
    [Fact]
    public void Spec_example_parses_and_frames_advance()
    {
        const string json = """
        {
          "id": "char_penguin",
          "frame": { "w": 48, "h": 48 },
          "anchor": { "x": 24, "y": 46 },
          "head": { "x": 24, "y": 6 },
          "animations": {
            "idle":    { "row": 0, "frames": 4, "fps": 4, "loop": true },
            "walk":    { "row": 1, "frames": 6, "fps": 8, "loop": true },
            "sleep":   { "row": 2, "frames": 2, "fps": 1, "loop": true },
            "hit":     { "row": 3, "frames": 3, "fps": 10, "loop": false },
            "offline": { "row": 4, "frames": 1, "fps": 1, "loop": true }
          }
        }
        """;
        var m = CharacterManifest.FromJson(json);
        Assert.Equal("char_penguin", m.Id);
        var idle = m.GetAnimation("idle");
        Assert.Equal(0, idle.FrameAt(0));
        Assert.Equal(1, idle.FrameAt(0.25));
        Assert.Equal(0, idle.FrameAt(1.0));
        var hit = m.GetAnimation("hit");
        Assert.Equal(2, hit.FrameAt(5));
        Assert.True(hit.IsFinished(0.3));
        Assert.False(hit.IsFinished(0.2));
    }

    [Fact]
    public void Rejects_too_many_frames()
    {
        Assert.Throws<InvalidDataException>(() => CharacterManifest.FromJson("{\"id\":\"x\",\"frame\":{\"w\":48,\"h\":48},\"animations\":{\"idle\":{\"row\":0,\"frames\":9,\"fps\":1}}}"));
    }
}
