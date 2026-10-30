using System.Text.Json;
using System.Text.Json.Serialization;

namespace PopoWorld.Core.Models;

/// <summary>Assets/characters/&lt;id&gt;/manifest.json (7절).</summary>
public sealed class CharacterManifest
{
    public const int DefaultFrameSize = 48;
    public const int DisplayScale = 2;
    public const int FramesPerRow = 8;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("frame")]
    public SizeSpec Frame { get; set; } = new() { W = DefaultFrameSize, H = DefaultFrameSize };

    /// <summary>발 위치. 띠 바닥에 닿는 점.</summary>
    [JsonPropertyName("anchor")]
    public PointSpec Anchor { get; set; } = new() { X = 24, Y = 46 };

    /// <summary>말풍선 꼬리가 가리키는 점.</summary>
    [JsonPropertyName("head")]
    public PointSpec Head { get; set; } = new() { X = 24, Y = 6 };

    [JsonPropertyName("animations")]
    public Dictionary<string, AnimationSpec> Animations { get; set; } = new();

    public AnimationSpec GetAnimation(string name)
    {
        if (Animations.TryGetValue(name, out var a)) return a;
        if (Animations.TryGetValue(AnimationNames.Idle, out var idle)) return idle;
        return new AnimationSpec { Row = 0, Frames = 1, Fps = 1, Loop = true };
    }

    public static CharacterManifest FromJson(string json)
    {
        var m = JsonSerializer.Deserialize<CharacterManifest>(json)
                ?? throw new InvalidDataException("manifest.json 이 비어 있습니다.");
        if (m.Frame.W <= 0 || m.Frame.H <= 0) throw new InvalidDataException("frame 크기가 잘못됐습니다.");
        foreach (var (name, a) in m.Animations)
        {
            if (a.Frames < 1 || a.Frames > FramesPerRow) throw new InvalidDataException($"{name}: frames 는 1~{FramesPerRow}.");
            if (a.Fps <= 0) throw new InvalidDataException($"{name}: fps 는 양수.");
        }
        return m;
    }

    public static CharacterManifest Default(string id) => new()
    {
        Id = id,
        Animations = new Dictionary<string, AnimationSpec>
        {
            [AnimationNames.Idle] = new() { Row = 0, Frames = 4, Fps = 4, Loop = true },
            [AnimationNames.Walk] = new() { Row = 1, Frames = 6, Fps = 8, Loop = true },
            [AnimationNames.Sleep] = new() { Row = 2, Frames = 2, Fps = 1, Loop = true },
            [AnimationNames.Hit] = new() { Row = 3, Frames = 3, Fps = 10, Loop = false },
            [AnimationNames.Offline] = new() { Row = 4, Frames = 1, Fps = 1, Loop = true },
        },
    };
}

public static class AnimationNames
{
    public const string Idle = "idle";
    public const string Walk = "walk";
    public const string Sleep = "sleep";
    public const string Hit = "hit";
    public const string Offline = "offline";
}

public sealed class SizeSpec
{
    [JsonPropertyName("w")] public int W { get; set; }
    [JsonPropertyName("h")] public int H { get; set; }
}

public sealed class PointSpec
{
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
}

public sealed class AnimationSpec
{
    [JsonPropertyName("row")] public int Row { get; set; }
    [JsonPropertyName("frames")] public int Frames { get; set; } = 1;
    [JsonPropertyName("fps")] public double Fps { get; set; } = 1;
    [JsonPropertyName("loop")] public bool Loop { get; set; } = true;

    /// <summary>경과 시간으로 프레임 인덱스를 구한다. loop=false면 마지막 프레임에서 멈춘다.</summary>
    public int FrameAt(double elapsedSeconds)
    {
        if (Frames <= 1) return 0;
        var i = (int)Math.Floor(Math.Max(0, elapsedSeconds) * Fps);
        return Loop ? i % Frames : Math.Min(i, Frames - 1);
    }

    public bool IsFinished(double elapsedSeconds) => !Loop && elapsedSeconds * Fps >= Frames;
}
