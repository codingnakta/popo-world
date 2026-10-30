namespace PopoWorld.Core.Models;

/// <summary>
/// Broadcast 이벤트의 중복과 만료를 거른다.
/// 최근 200개 event_id를 기억하고, sent_at이 10초 넘게 오래된 이벤트는 재생하지 않는다.
/// </summary>
public sealed class EventFilter
{
    public const int Capacity = 200;
    public static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(10);

    private readonly HashSet<Guid> _seen = new();
    private readonly Queue<Guid> _order = new();
    private readonly object _gate = new();

    /// <summary>재생해야 하면 true. 같은 id가 또 오거나 너무 오래됐으면 false.</summary>
    public bool ShouldPlay(Guid eventId, DateTimeOffset sentAt, DateTimeOffset now)
    {
        if (now - sentAt > MaxAge)
            return false;

        lock (_gate)
        {
            if (!_seen.Add(eventId))
                return false;

            _order.Enqueue(eventId);
            while (_order.Count > Capacity)
                _seen.Remove(_order.Dequeue());
            return true;
        }
    }

    public int Count
    {
        get { lock (_gate) return _seen.Count; }
    }
}
