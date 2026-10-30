using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PopoWorld.Core.Models;
using Serilog;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Models;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.Constants;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace PopoWorld.App.Services;

/// <summary>Broadcast 페이로드 포장. event 로 구분하고 payload 는 JSON 그대로 받는다.</summary>
public sealed class WorldBroadcast : BaseBroadcast<JsonObject>
{
}

/// <summary>Presence track 페이로드.</summary>
public sealed class PresenceModel : BasePresence
{
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = "online";
}

/// <summary>
/// 채널 room:main 하나로 Postgres Changes(영속), Broadcast(일시), Presence(접속)를 받는다.
/// 모든 이벤트는 백그라운드 스레드에서 올라오므로 받는 쪽이 Dispatcher 로 옮겨야 한다.
/// </summary>
public sealed class RealtimeService : IDisposable
{
    private readonly SupabaseService _supabase;
    private RealtimeChannel? _channel;
    private RealtimeBroadcast<WorldBroadcast>? _broadcast;
    private RealtimePresence<PresenceModel>? _presence;
    private PresenceModel? _lastTracked;
    private bool _everConnected;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public event Action<Message>? MessageInserted;
    public event Action<User>? UserUpdated;
    public event Action<LedgerEntry>? LedgerInserted;
    public event Action<TypingEvent>? TypingReceived;
    public event Action<ThrowEvent>? ThrowReceived;
    public event Action<GiftAnimEvent>? GiftAnimReceived;
    public event Action<IReadOnlyDictionary<Guid, PresenceState>>? PresenceSynced;
    /// <summary>true: 연결됨(처음 또는 재접속), false: 끊김.</summary>
    public event Action<bool>? ConnectionChanged;

    public bool IsConnected { get; private set; }

    public RealtimeService(SupabaseService supabase)
    {
        _supabase = supabase;
    }

    private bool _socketHandlerAdded;

    /// <summary>처음 연결. 실패하면 예외를 던지고, 다시 불러도 안전하다 (채널을 새로 만든다).</summary>
    public async Task ConnectAsync()
    {
        var realtime = _supabase.Client.Realtime;
        if (!_socketHandlerAdded)
        {
            realtime.AddStateChangedHandler(OnSocketStateChanged);
            _socketHandlerAdded = true;
        }
        if (!string.IsNullOrEmpty(_supabase.AccessToken))
            realtime.SetAuth(_supabase.AccessToken!);

        if (_channel is not null)
        {
            try { _channel.Unsubscribe(); } catch { /* 무시 */ }
            _channel = null;
            _broadcast = null;
            _presence = null;
        }

        await realtime.ConnectAsync();

        _channel = realtime.Channel(Channels.Main);

        _channel.OnPostgresChange(OnPostgresInsert, ListenType.Inserts, new PostgresChangesFilter { Schema = "public", Table = "messages" });
        _channel.OnPostgresChange(OnPostgresUpdate, ListenType.Updates, new PostgresChangesFilter { Schema = "public", Table = "users" });
        _channel.OnPostgresChange(OnPostgresInsert, ListenType.Inserts, new PostgresChangesFilter { Schema = "public", Table = "popo_ledger" });

        _broadcast = _channel.Register<WorldBroadcast>(broadcastSelf: false, broadcastAck: false);
        _broadcast.AddBroadcastEventHandler(OnBroadcast);

        var me = _supabase.UserId?.ToString() ?? Guid.NewGuid().ToString();
        _presence = _channel.Register<PresenceModel>(me);
        _presence.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, OnPresence);
        _presence.AddPresenceEventHandler(IRealtimePresence.EventType.Join, OnPresence);
        _presence.AddPresenceEventHandler(IRealtimePresence.EventType.Leave, OnPresence);

        _channel.AddStateChangedHandler(OnChannelStateChanged);
        _channel.AddErrorHandler((_, ex) => Log.Warning(ex, "realtime 채널 오류"));

        await _channel.Subscribe();
        Log.Information("realtime 채널 구독: {Topic}", Channels.Main);
    }

    public async Task TrackPresenceAsync(PresenceState state)
    {
        if (_presence is null || _supabase.UserId is null) return;
        var payload = new PresenceModel { UserId = _supabase.UserId.Value, State = state == PresenceState.Idle ? "idle" : "online" };
        _lastTracked = payload;
        try { await _presence.Track(payload); }
        catch (Exception ex) { Log.Debug(ex, "presence track 실패"); }
    }

    public async Task UntrackAsync()
    {
        if (_presence is null) return;
        try { await _presence.Untrack(); }
        catch (Exception ex) { Log.Debug(ex, "presence untrack 실패"); }
    }

    public Task SendTypingAsync(Guid me, bool on) =>
        SendAsync(BroadcastEvents.Typing, new TypingEvent { UserId = me, On = on });

    public Task SendThrowAsync(ThrowEvent ev) => SendAsync(BroadcastEvents.Throw, ev);

    public Task SendGiftAnimAsync(GiftAnimEvent ev) => SendAsync(BroadcastEvents.GiftAnim, ev);

    private async Task SendAsync(string eventName, object payload)
    {
        if (_broadcast is null || _channel is null || !_channel.IsJoined) return;
        try
        {
            var msg = new WorldBroadcast { Event = eventName, Payload = JsonSerializer.SerializeToNode(payload, Json) as JsonObject };
            await _broadcast.Send(eventName, msg);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "broadcast {Event} 전송 실패", eventName);
        }
    }

    // ---------- 핸들러 ----------

    private void OnSocketStateChanged(IRealtimeClient<RealtimeSocket, RealtimeChannel> sender, SocketState state)
    {
        Log.Information("realtime socket {State}", state);
        switch (state)
        {
            case SocketState.Open:
                // 채널이 다시 join 될 때 ConnectionChanged(true) 를 올린다.
                break;
            case SocketState.Close:
            case SocketState.Error:
                if (IsConnected)
                {
                    IsConnected = false;
                    ConnectionChanged?.Invoke(false);
                }
                break;
        }
    }

    private void OnChannelStateChanged(IRealtimeChannel sender, ChannelState state)
    {
        Log.Debug("realtime channel {State}", state);
        if (state == ChannelState.Joined)
        {
            var wasConnected = IsConnected;
            IsConnected = true;
            if (!wasConnected)
            {
                // 재접속이면 presence 를 다시 track 해야 상대 화면에 돌아온다.
                if (_everConnected && _lastTracked is not null && _presence is not null)
                    _ = _presence.Track(_lastTracked).ContinueWith(t => Log.Debug(t.Exception, "re-track 실패"), TaskContinuationOptions.OnlyOnFaulted);
                _everConnected = true;
                ConnectionChanged?.Invoke(true);
            }
        }
        else if (state is ChannelState.Closed or ChannelState.Errored)
        {
            if (IsConnected)
            {
                IsConnected = false;
                ConnectionChanged?.Invoke(false);
            }
        }
    }

    private void OnPostgresInsert(IRealtimeChannel sender, PostgresChangesResponse change)
    {
        try
        {
            switch (change.Payload?.Data?.Table)
            {
                case "messages":
                    var m = change.Model<Message>();
                    if (m is not null) MessageInserted?.Invoke(m);
                    break;
                case "popo_ledger":
                    var l = change.Model<LedgerEntry>();
                    if (l is not null) LedgerInserted?.Invoke(l);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "postgres insert 이벤트 처리 실패");
        }
    }

    private void OnPostgresUpdate(IRealtimeChannel sender, PostgresChangesResponse change)
    {
        try
        {
            if (change.Payload?.Data?.Table == "users")
            {
                var u = change.Model<User>();
                if (u is not null) UserUpdated?.Invoke(u);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "postgres update 이벤트 처리 실패");
        }
    }

    private void OnBroadcast(IRealtimeBroadcast sender, BaseBroadcast? broadcast)
    {
        try
        {
            var current = _broadcast?.Current();
            if (current is null) return;
            var payload = current.Payload;
            if (payload is null) return;

            switch (current.Event)
            {
                case BroadcastEvents.Typing:
                    var t = payload.Deserialize<TypingEvent>(Json);
                    if (t is not null) TypingReceived?.Invoke(t);
                    break;
                case BroadcastEvents.Throw:
                    var th = payload.Deserialize<ThrowEvent>(Json);
                    if (th is not null) ThrowReceived?.Invoke(th);
                    break;
                case BroadcastEvents.GiftAnim:
                    var g = payload.Deserialize<GiftAnimEvent>(Json);
                    if (g is not null) GiftAnimReceived?.Invoke(g);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "broadcast 처리 실패");
        }
    }

    private void OnPresence(IRealtimePresence sender, IRealtimePresence.EventType eventType)
    {
        try
        {
            if (_presence is null) return;
            var map = new Dictionary<Guid, PresenceState>();
            foreach (var (_, metas) in _presence.CurrentState)
            {
                foreach (var meta in metas)
                {
                    if (meta.UserId == Guid.Empty) continue;
                    var s = meta.State == "idle" ? PresenceState.Idle : PresenceState.Online;
                    // 같은 사람이 두 PC 에 있으면 online 우선
                    if (!map.TryGetValue(meta.UserId, out var prev) || prev == PresenceState.Idle)
                        map[meta.UserId] = s;
                }
            }
            PresenceSynced?.Invoke(map);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "presence 처리 실패");
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            await UntrackAsync();
            _channel?.Unsubscribe();
            _supabase.Client.Realtime.Disconnect();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "realtime 종료 중 오류");
        }
    }

    public void Dispose()
    {
        try { _channel?.Unsubscribe(); } catch { /* 무시 */ }
    }
}
