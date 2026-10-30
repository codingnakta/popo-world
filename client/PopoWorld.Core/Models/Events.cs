using System.Text.Json.Serialization;

namespace PopoWorld.Core.Models;

/// <summary>Realtime 채널 이름. 하나만 쓴다.</summary>
public static class Channels
{
    public const string Main = "room:main";
}

public static class BroadcastEvents
{
    public const string Typing = "typing";
    public const string Throw = "throw";
    public const string GiftAnim = "gift_anim";
}

public sealed class TypingEvent
{
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }

    [JsonPropertyName("on")]
    public bool On { get; set; }
}

public sealed class ThrowEvent
{
    [JsonPropertyName("event_id")]
    public Guid EventId { get; set; }

    [JsonPropertyName("from_id")]
    public Guid FromId { get; set; }

    [JsonPropertyName("to_id")]
    public Guid ToId { get; set; }

    [JsonPropertyName("ball_id")]
    public string BallId { get; set; } = "ball_soft";

    [JsonPropertyName("sent_at")]
    public DateTimeOffset SentAt { get; set; }
}

public sealed class GiftAnimEvent
{
    [JsonPropertyName("event_id")]
    public Guid EventId { get; set; }

    [JsonPropertyName("from_id")]
    public Guid FromId { get; set; }

    [JsonPropertyName("to_id")]
    public Guid ToId { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("sent_at")]
    public DateTimeOffset SentAt { get; set; }
}

public enum PresenceState
{
    Offline,
    Online,
    Idle,
}

public sealed class PresencePayload
{
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }

    /// <summary>"online" | "idle"</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = "online";

    public PresenceState ToState() => State == "idle" ? PresenceState.Idle : PresenceState.Online;
}
