using System.Text.Json;
using System.Text.Json.Serialization;

namespace PopoWorld.Core.Models;

/// <summary>%APPDATA%\PopoWorld\settings.json 의 내용 (7절).</summary>
public sealed class AppSettings
{
    public const int CurrentVersion = 1;

    [JsonPropertyName("version")]
    public int Version { get; set; } = CurrentVersion;

    /// <summary>장치 이름. 예: \\.\DISPLAY1. 없거나 못 찾으면 주 모니터.</summary>
    [JsonPropertyName("monitor")]
    public string? Monitor { get; set; }

    [JsonPropertyName("edge")]
    public string Edge { get; set; } = "bottom";

    /// <summary>변 길이 대비 띠 길이 비율: 1, 0.5, 0.333…</summary>
    [JsonPropertyName("span")]
    public double Span { get; set; } = 0.5;

    [JsonPropertyName("showOffline")]
    public bool ShowOffline { get; set; } = true;

    [JsonPropertyName("quietMode")]
    public bool QuietMode { get; set; }

    [JsonPropertyName("hidden")]
    public bool Hidden { get; set; }

    [JsonPropertyName("sound")]
    public SoundSettings Sound { get; set; } = new();

    [JsonPropertyName("hotkeys")]
    public HotkeySettings Hotkeys { get; set; } = new();

    [JsonPropertyName("composerPosition")]
    public PositionSettings? ComposerPosition { get; set; }

    [JsonPropertyName("startWithWindows")]
    public bool StartWithWindows { get; set; }

    [JsonIgnore]
    public Edge EdgeValue
    {
        get => EdgeExtensions.ParseEdge(Edge);
        set => Edge = value.ToKey();
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static AppSettings FromJson(string json)
    {
        var s = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        s.Normalize();
        return s;
    }

    /// <summary>손으로 고친 파일이나 옛 버전에서 온 값을 안전한 범위로 맞춘다.</summary>
    public void Normalize()
    {
        Version = CurrentVersion;
        Edge = EdgeValue.ToKey();
        Span = Span switch
        {
            >= 0.75 => 1.0,
            >= 0.4 => 0.5,
            _ => 1.0 / 3.0,
        };
        Sound ??= new SoundSettings();
        Sound.Volume = Math.Clamp(Sound.Volume, 0, 1);
        Hotkeys ??= new HotkeySettings();
        if (string.IsNullOrWhiteSpace(Hotkeys.Composer)) Hotkeys.Composer = HotkeySettings.DefaultComposer;
        if (string.IsNullOrWhiteSpace(Hotkeys.ToggleWorld)) Hotkeys.ToggleWorld = HotkeySettings.DefaultToggleWorld;
    }

    public AppSettings Clone() => FromJson(ToJson());
}

public sealed class SoundSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("volume")]
    public double Volume { get; set; } = 0.6;
}

public sealed class HotkeySettings
{
    public const string DefaultComposer = "Ctrl+Shift+Space";
    public const string DefaultToggleWorld = "Ctrl+Shift+H";

    [JsonPropertyName("composer")]
    public string Composer { get; set; } = DefaultComposer;

    [JsonPropertyName("toggleWorld")]
    public string ToggleWorld { get; set; } = DefaultToggleWorld;
}

public sealed class PositionSettings
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}
