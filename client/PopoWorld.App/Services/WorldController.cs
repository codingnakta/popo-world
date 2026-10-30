using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using PopoWorld.App.Composer;
using PopoWorld.App.Login;
using PopoWorld.App.Overlay;
using PopoWorld.App.Settings;
using PopoWorld.App.Store;
using PopoWorld.Core.Models;
using PopoWorld.Core.Validation;
using Serilog;

namespace PopoWorld.App.Services;

/// <summary>
/// 앱 전체를 잇는 조정자. 로그인 → 세계 로드 → 실시간 구독 → 입력/이벤트 처리.
/// 모든 멤버는 UI 스레드에서만 부른다. Realtime 콜백은 Dispatcher 로 옮긴다.
/// </summary>
public sealed class WorldController : StoreWindow.IStoreHost, IDisposable
{
    private readonly SettingsStore _settingsStore;
    private readonly SupabaseService _supabase;
    private readonly PopoService _popo;
    private readonly RealtimeService _realtime;
    private readonly PresenceService _presence;
    private readonly SoundService _sound;
    private readonly AssetCache _assets;
    private readonly HotkeyService _hotkeys;
    private readonly TrayIcon _tray;
    private readonly Dispatcher _dispatcher;
    private readonly EventFilter _eventFilter = new();

    private OverlayWindow _overlay = null!;
    private FullscreenDetector? _fullscreen;
    private ComposerWindow? _composer;
    private StoreWindow? _store;

    private readonly Dictionary<Guid, CharacterView> _views = new();
    private readonly Dictionary<Guid, PresenceState> _presenceMap = new();
    private List<CatalogItem> _catalog = new();
    private HashSet<string> _inventory = new();
    private int _balance;
    private int _unread;
    private DateTime _lastThrow = DateTime.MinValue;
    private DateTime _lastTypingSent = DateTime.MinValue;
    private bool _typingOn;
    private bool _started;
    private bool _exiting;
    private CatalogItem? _previewItem;
    private string? _previewOriginalCharacter;

    public Guid Me { get; private set; }
    public User MyUser { get; private set; } = new();
    private AppSettings Settings => _settingsStore.Current;

    public WorldController(SettingsStore settingsStore, SupabaseService supabase, PopoService popo, RealtimeService realtime,
        PresenceService presence, SoundService sound, AssetCache assets, HotkeyService hotkeys, TrayIcon tray)
    {
        _settingsStore = settingsStore;
        _supabase = supabase;
        _popo = popo;
        _realtime = realtime;
        _presence = presence;
        _sound = sound;
        _assets = assets;
        _hotkeys = hotkeys;
        _tray = tray;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    // ---------- 시작 ----------

    public async Task<bool> StartAsync()
    {
        CreateOverlay();
        WireTray();

        if (!await _supabase.TryRestoreSessionAsync())
        {
            var login = new LoginWindow(_supabase);
            if (login.ShowDialog() != true || !_supabase.IsSignedIn)
                return false;
        }

        Me = _supabase.UserId!.Value;
        await LoadWorldAsync(initial: true);

        WireRealtime();
        _ = ConnectRealtimeWithRetryAsync();

        _presence.StateChanged += OnMyPresenceChanged;
        _presence.LastSeenTick += () => _ = _popo.UpdateLastSeenAsync();
        _presence.Start();
        _ = _popo.UpdateLastSeenAsync();

        _fullscreen = new FullscreenDetector(() => _overlay.AllHandles());
        _fullscreen.Changed += full => ApplyVisibility();
        _fullscreen.Start();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        ApplyHotkeys();
        StartupRegistry.Apply(Settings.StartWithWindows);

        _overlay.Start();
        ApplyVisibility();
        _started = true;

        _ = EarnAsync(LedgerReason.DailyLogin);
        _ = NotifyOfflineGiftsAsync();
        UpdateTray();
        return true;
    }

    /// <summary>처음 연결이 안 되면(인터넷 없음 등) 15초마다 다시 시도한다. 연결된 뒤의 재접속은 라이브러리가 한다.</summary>
    private async Task ConnectRealtimeWithRetryAsync()
    {
        var delay = TimeSpan.FromSeconds(5);
        while (!_exiting)
        {
            try
            {
                await _realtime.ConnectAsync();
                await _realtime.TrackPresenceAsync(_presence.State);
                return;
            }
            catch (Exception ex)
            {
                Log.Warning("realtime 연결 실패, {Delay}초 뒤 재시도: {Message}", delay.TotalSeconds, ex.Message);
                UpdateTray();
                await Task.Delay(delay);
                delay = TimeSpan.FromSeconds(Math.Min(60, delay.TotalSeconds * 2));
            }
        }
    }

    private void CreateOverlay()
    {
        var monitor = Monitors.Resolve(Settings.Monitor);
        _overlay = new OverlayWindow(monitor, Settings.EdgeValue, Settings.Span);
        _overlay.CharacterClicked += OnCharacterClicked;
        _overlay.CharacterDoubleClicked += OnCharacterDoubleClicked;
        _overlay.CharacterRightClicked += OnCharacterRightClicked;
        _overlay.Show();
    }

    private void WireTray()
    {
        _tray.ToggleHiddenRequested += () => SetHidden(!Settings.Hidden);
        _tray.StoreRequested += OpenStore;
        _tray.SettingsRequested += OpenSettings;
        _tray.ComposerRequested += ToggleComposer;
        _tray.ExitRequested += () => _ = ExitAsync();
    }

    private void WireRealtime()
    {
        _realtime.MessageInserted += m => Ui(() => OnMessage(m));
        _realtime.UserUpdated += u => Ui(() => _ = OnUserUpdatedAsync(u));
        _realtime.LedgerInserted += l => Ui(() => _ = OnLedgerAsync(l));
        _realtime.TypingReceived += t => Ui(() => OnTyping(t));
        _realtime.ThrowReceived += t => Ui(() => OnThrow(t));
        _realtime.GiftAnimReceived += g => Ui(() => OnGiftAnim(g));
        _realtime.PresenceSynced += p => Ui(() => OnPresenceSynced(p));
        _realtime.ConnectionChanged += c => Ui(() => _ = OnConnectionChangedAsync(c));
    }

    private void Ui(Action action) => _dispatcher.BeginInvoke(action);

    // ---------- 세계 로드 ----------

    private async Task LoadWorldAsync(bool initial)
    {
        try
        {
            _catalog = await _popo.GetCatalogAsync();
            _assets.RegisterCatalog(_catalog);
        }
        catch (Exception ex) { Log.Warning(ex, "카탈로그 로드 실패"); }

        List<User> users;
        try { users = await _popo.GetUsersAsync(); }
        catch (Exception ex)
        {
            Log.Error(ex, "users 로드 실패");
            if (initial) MessageBox.Show("친구 목록을 불러오지 못했어요. 인터넷 연결을 확인하세요.", "포포월드", MessageBoxButton.OK, MessageBoxImage.Warning);
            users = new List<User>();
        }

        var mine = users.FirstOrDefault(u => u.Id == Me);
        if (mine is null)
        {
            Log.Error("users 표에 내 행이 없습니다: {Me}", Me);
            if (initial) MessageBox.Show("관리자가 아직 내 캐릭터를 만들지 않았어요. 관리자에게 문의하세요.", "포포월드", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            MyUser = mine;
        }

        try
        {
            _inventory = await _popo.GetInventoryAsync();
            _balance = await _popo.GetBalanceAsync();
        }
        catch (Exception ex) { Log.Warning(ex, "보유·잔액 로드 실패"); }

        // 캐릭터 추가·갱신·삭제
        var seen = new HashSet<Guid>();
        foreach (var u in users)
        {
            seen.Add(u.Id);
            if (_views.TryGetValue(u.Id, out var v)) await ApplyUserAsync(v, u);
            else await AddCharacterAsync(u);
        }
        foreach (var gone in _views.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _overlay.RemoveCharacter(_views[gone]);
            _views.Remove(gone);
        }
        ApplyPresence();
        UpdateTray();
    }

    private async Task AddCharacterAsync(User u)
    {
        var sheet = await _assets.GetCharacterAsync(u.CharacterId);
        var view = new CharacterView(u, u.Id == Me, sheet, 0, _overlay.Layout.Length, _overlay.Random);
        _views[u.Id] = view;
        _overlay.AddCharacter(view);
        view.SetPresence(_presenceMap.TryGetValue(u.Id, out var s) ? s : PresenceState.Offline);
    }

    private async Task ApplyUserAsync(CharacterView view, User u)
    {
        var characterChanged = view.User.CharacterId != u.CharacterId;
        view.SetUser(u);
        if (characterChanged && _previewItem is null)
            view.SetSheet(await _assets.GetCharacterAsync(u.CharacterId));
        if (u.Id == Me) MyUser = u;
    }

    // ---------- 실시간 이벤트 ----------

    private void OnMessage(Message m)
    {
        if (!_views.TryGetValue(m.UserId, out var view)) return;
        view.ShowBubble(m.Body);
        view.SetTyping(false);
        if (m.UserId != Me)
        {
            _unread++;
            UpdateTray();
            if (!Settings.QuietMode) _sound.Play(SoundService.Message);
        }
    }

    private async Task OnUserUpdatedAsync(User u)
    {
        if (_views.TryGetValue(u.Id, out var view)) await ApplyUserAsync(view, u);
        else await AddCharacterAsync(u);
        if (u.Id == Me) _store?.Refresh();
    }

    private async Task OnLedgerAsync(LedgerEntry l)
    {
        if (l.UserId != Me) return;
        try { _balance = await _popo.GetBalanceAsync(); }
        catch (Exception ex) { Log.Debug(ex, "잔액 재조회 실패"); _balance += l.Delta; }
        UpdateTray();
        _store?.Refresh();

        if (l.Reason == LedgerReason.GiftIn)
            ShowGiftBubble(l);
        else if (l.Reason == LedgerReason.Streak && _views.TryGetValue(Me, out var me))
            me.ShowBubble($"7일 연속 출석! +{l.Delta}포포");
    }

    private void ShowGiftBubble(LedgerEntry l)
    {
        var from = l.PeerId is { } pid && _views.TryGetValue(pid, out var sender) ? sender.User.Name : "친구";
        var text = $"{from}님이 {l.Delta}포포를 보냈어요";
        if (!string.IsNullOrWhiteSpace(l.Memo)) text += $": {l.Memo}";
        if (_views.TryGetValue(Me, out var me)) me.ShowBubble(text, TimeSpan.FromSeconds(10));
        if (!Settings.QuietMode) _sound.Play(SoundService.Gift);
    }

    private async Task NotifyOfflineGiftsAsync()
    {
        try
        {
            // 로그인 전 마지막 접속 이후 받은 선물
            var since = MyUser.LastSeen == default ? DateTime.UtcNow.AddDays(-3) : DateTime.SpecifyKind(MyUser.LastSeen, DateTimeKind.Utc);
            var gifts = await _popo.GetGiftsReceivedSinceAsync(since);
            foreach (var g in gifts.TakeLast(3))
            {
                ShowGiftBubble(g);
                await Task.Delay(1500);
            }
        }
        catch (Exception ex) { Log.Debug(ex, "오프라인 선물 조회 실패"); }
    }

    private void OnTyping(TypingEvent t)
    {
        if (t.UserId == Me) return;
        if (_views.TryGetValue(t.UserId, out var view)) view.SetTyping(t.On);
    }

    private void OnThrow(ThrowEvent ev)
    {
        if (!_eventFilter.ShouldPlay(ev.EventId, ev.SentAt, DateTimeOffset.UtcNow)) return;
        PlayThrow(ev);
    }

    private void PlayThrow(ThrowEvent ev)
    {
        if (!_views.TryGetValue(ev.FromId, out var from) || !_views.TryGetValue(ev.ToId, out var to)) return;
        if (!from.Visible || !to.Visible || !_overlay.WorldVisible) return;

        var ball = _catalog.FirstOrDefault(c => c.Id == ev.BallId);
        var effect = ball?.Effect;
        _ = SpawnProjectileAsync(ev.BallId, from.ScreenCenter, to.ScreenCenter, 32, () =>
        {
            to.PlayHit(effect);
            if (!Settings.QuietMode) _sound.Play(SoundService.Hit);
            if (ev.ToId == Me && ev.FromId != Me) _ = EarnAsync(LedgerReason.Hit);
        });
        if (!Settings.QuietMode) _sound.Play(SoundService.Throw);
    }

    private void OnGiftAnim(GiftAnimEvent ev)
    {
        if (!_eventFilter.ShouldPlay(ev.EventId, ev.SentAt, DateTimeOffset.UtcNow)) return;
        PlayGiftAnim(ev);
    }

    private void PlayGiftAnim(GiftAnimEvent ev)
    {
        if (!_views.TryGetValue(ev.FromId, out var from) || !_views.TryGetValue(ev.ToId, out var to)) return;
        if (!from.Visible || !to.Visible || !_overlay.WorldVisible) return;
        _ = SpawnProjectileAsync("gift_box", from.ScreenCenter, to.ScreenCenter, 32, null);
    }

    private async Task SpawnProjectileAsync(string imageId, PointD from, PointD to, double size, Action? onArrive)
    {
        var bmp = await _assets.GetItemImageAsync(imageId) ?? _assets.GetBuiltInImage(AssetCache.DefaultBall);
        _overlay.AddProjectile(new ProjectileView(bmp, from, to, size, onArrive));
    }

    private void OnPresenceSynced(IReadOnlyDictionary<Guid, PresenceState> map)
    {
        _presenceMap.Clear();
        foreach (var (id, s) in map) _presenceMap[id] = s;
        ApplyPresence();
    }

    private void ApplyPresence()
    {
        foreach (var (id, view) in _views)
        {
            var state = id == Me ? _presence.State : _presenceMap.TryGetValue(id, out var s) ? s : PresenceState.Offline;
            view.SetPresence(state);
        }
        ApplyVisibility();
    }

    private async Task OnConnectionChangedAsync(bool connected)
    {
        UpdateTray();
        if (connected && _started)
        {
            // 재접속: users 전체와 balances 를 다시 읽고 presence 를 다시 올린다
            await LoadWorldAsync(initial: false);
            await _realtime.TrackPresenceAsync(_presence.State);
        }
    }

    private void OnMyPresenceChanged(PresenceState state)
    {
        _ = _realtime.TrackPresenceAsync(state);
        if (_views.TryGetValue(Me, out var me)) me.SetPresence(state);
    }

    // ---------- 입력 ----------

    private void OnCharacterClicked(CharacterView view)
    {
        if (view.IsMe) ToggleComposer();
        else Throw(view.User.Id);
    }

    private void OnCharacterDoubleClicked(CharacterView view)
    {
        if (view.IsMe) OpenComposer();
        // 친구: 더블클릭의 첫 클릭에서 이미 던졌으므로 두 번째는 무시
    }

    private void OnCharacterRightClicked(CharacterView view, PointD screen)
    {
        var menu = new ContextMenu { Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
        if (view.IsMe)
        {
            Add(menu, "말하기", OpenComposer);
            Add(menu, "상점", OpenStore);
            Add(menu, "설정", OpenSettings);
            Add(menu, Settings.Hidden ? "보이기" : "숨기기", () => SetHidden(!Settings.Hidden));
        }
        else
        {
            Add(menu, $"{view.User.Name}님에게 포포 선물", () => OpenGift(view.User));
            Add(menu, "이 친구에게 던지기", () => Throw(view.User.Id));
        }
        menu.IsOpen = true;

        static void Add(ContextMenu m, string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            m.Items.Add(item);
        }
    }

    public void Throw(Guid to)
    {
        var now = DateTime.UtcNow;
        if (now - _lastThrow < PopoRules.ThrowCooldown) return;
        if (to == Me || !_views.ContainsKey(to)) return;
        _lastThrow = now;

        var ev = new ThrowEvent { EventId = Guid.NewGuid(), FromId = Me, ToId = to, BallId = MyUser.BallId, SentAt = DateTimeOffset.UtcNow };
        _ = _realtime.SendThrowAsync(ev);
        if (_eventFilter.ShouldPlay(ev.EventId, ev.SentAt, DateTimeOffset.UtcNow)) PlayThrow(ev);
    }

    // ---------- 메시지 ----------

    private ComposerWindow Composer
    {
        get
        {
            if (_composer is null)
            {
                _composer = new ComposerWindow(SendMessageAsync, OnTypingLocal, OnComposerClosed);
                var p = Settings.ComposerPosition;
                if (p is not null)
                {
                    _composer.WindowStartupLocation = WindowStartupLocation.Manual;
                    _composer.Left = p.X;
                    _composer.Top = p.Y;
                }
                else
                {
                    _composer.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
            }
            return _composer;
        }
    }

    public void ToggleComposer() => Composer.Toggle();
    public void OpenComposer() => Composer.ShowAndFocus();

    private async Task<bool> SendMessageAsync(string body)
    {
        try
        {
            await _popo.SendMessageAsync(body);
            StopTyping();
            _ = EarnAsync(LedgerReason.Message);
            // 내 말풍선은 Postgres Changes 로도 오지만, 끊겨 있어도 보이게 바로 띄운다
            if (_views.TryGetValue(Me, out var me)) me.ShowBubble(body);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "메시지 전송 실패");
            return false;
        }
    }

    private void OnTypingLocal()
    {
        var now = DateTime.UtcNow;
        if (now - _lastTypingSent < PopoRules.TypingSendInterval) return;
        _lastTypingSent = now;
        _typingOn = true;
        _ = _realtime.SendTypingAsync(Me, true);
    }

    private void OnComposerClosed()
    {
        StopTyping();
        if (_composer is null || double.IsNaN(_composer.Left)) return;
        var s = Settings;
        var pos = new PositionSettings { X = (int)_composer.Left, Y = (int)_composer.Top };
        if (s.ComposerPosition is { } old && old.X == pos.X && old.Y == pos.Y) return;
        s.ComposerPosition = pos;
        _settingsStore.Save(s);
    }

    private void StopTyping()
    {
        if (!_typingOn) return;
        _typingOn = false;
        _ = _realtime.SendTypingAsync(Me, false);
    }

    // ---------- 포포 ----------

    private async Task EarnAsync(string reason)
    {
        var r = await _popo.EarnAsync(reason, Guid.NewGuid());
        if (r.Ok && r.Balance is { } b) { _balance = b; UpdateTray(); }
        else if (!r.Ok) Log.Information("earn {Reason} 거부: {Error}", reason, r.Error);
    }

    // IStoreHost
    public int Balance => _balance;
    public IReadOnlySet<string> Inventory => _inventory;
    public IReadOnlyList<CatalogItem> Catalog => _catalog;
    User StoreWindow.IStoreHost.Me => MyUser;

    public void Preview(CatalogItem? item)
    {
        if (!_views.TryGetValue(Me, out var me)) return;
        if (item is null || item.Category != ItemCategory.Character || string.IsNullOrEmpty(item.Id))
        {
            if (_previewItem is not null)
            {
                _previewItem = null;
                _ = RestoreCharacterAsync(me);
            }
            return;
        }
        _previewOriginalCharacter ??= MyUser.CharacterId;
        _previewItem = item;
        _ = ApplyPreviewAsync(me, item.Id);
    }

    private async Task ApplyPreviewAsync(CharacterView me, string characterId)
    {
        try { me.SetSheet(await _assets.GetCharacterAsync(characterId)); }
        catch (Exception ex) { Log.Debug(ex, "미리보기 실패"); }
    }

    private async Task RestoreCharacterAsync(CharacterView me)
    {
        var id = MyUser.CharacterId ?? _previewOriginalCharacter ?? AssetCache.DefaultCharacter;
        _previewOriginalCharacter = null;
        me.SetSheet(await _assets.GetCharacterAsync(id));
    }

    public async Task<RpcResult> PurchaseAsync(CatalogItem item)
    {
        var r = await _popo.PurchaseAsync(item.Id, Guid.NewGuid());
        if (r.Ok)
        {
            _inventory.Add(item.Id);
            if (r.Balance is { } b) _balance = b;
            UpdateTray();
        }
        return r;
    }

    public async Task<RpcResult> EquipAsync(string slot, string? itemId)
    {
        var r = await _popo.EquipAsync(slot, itemId);
        if (r.Ok)
        {
            _previewItem = null;
            _previewOriginalCharacter = null;
            var fresh = await _popo.GetUserAsync(Me);
            if (fresh is not null && _views.TryGetValue(Me, out var me))
            {
                MyUser = fresh;
                me.SetUser(fresh);
                me.SetSheet(await _assets.GetCharacterAsync(fresh.CharacterId));
            }
        }
        return r;
    }

    public async Task RefreshAsync()
    {
        try
        {
            _inventory = await _popo.GetInventoryAsync();
            _balance = await _popo.GetBalanceAsync();
            UpdateTray();
        }
        catch (Exception ex) { Log.Debug(ex, "상점 새로고침 실패"); }
    }

    public void OpenStore()
    {
        if (_store is { IsVisible: true }) { _store.Activate(); return; }
        _store = new StoreWindow(this);
        _store.Closed += (_, _) => _store = null;
        _store.Show();
    }

    private async void OpenGift(User to)
    {
        int sentToday = 0;
        try
        {
            var ledger = await _popo.GetLedgerAsync();
            sentToday = GiftValidator.SentToday(ledger, DateTimeOffset.UtcNow);
            _balance = await _popo.GetBalanceAsync();
        }
        catch (Exception ex) { Log.Debug(ex, "선물 전 원장 조회 실패"); }

        var dialog = new GiftDialog(Me, to, _balance, sentToday, (toId, amount, memo) => _popo.GiftAsync(toId, amount, memo, Guid.NewGuid()));
        if (dialog.ShowDialog() == true)
        {
            var ev = new GiftAnimEvent { EventId = Guid.NewGuid(), FromId = Me, ToId = to.Id, Amount = dialog.Amount, SentAt = DateTimeOffset.UtcNow };
            _ = _realtime.SendGiftAnimAsync(ev);
            if (_eventFilter.ShouldPlay(ev.EventId, ev.SentAt, DateTimeOffset.UtcNow)) PlayGiftAnim(ev);
            if (!Settings.QuietMode) _sound.Play(SoundService.Gift);
            await RefreshAsync();
        }
    }

    // ---------- 설정 ----------

    public void OpenSettings()
    {
        var win = new SettingsWindow(Settings, MyUser.Name);
        if (win.ShowDialog() != true) return;

        if (win.LogoutRequested)
        {
            _ = LogoutAsync();
            return;
        }
        if (win.Result is { } s) ApplySettings(s);
        if (win.NewName is { } name) _ = RenameAsync(name);
    }

    private async Task RenameAsync(string name)
    {
        try { await _popo.UpdateNameAsync(name); }
        catch (Exception ex) { Log.Warning(ex, "이름 변경 실패"); MessageBox.Show("이름을 바꾸지 못했어요.", "포포월드"); }
    }

    public void ApplySettings(AppSettings s)
    {
        var old = Settings;
        _settingsStore.Save(s);

        if (old.Monitor != s.Monitor || old.Edge != s.Edge || Math.Abs(old.Span - s.Span) > 0.001)
            _overlay.Relayout(Monitors.Resolve(s.Monitor), s.EdgeValue, s.Span);

        ApplyHotkeys();
        StartupRegistry.Apply(s.StartWithWindows);
        ApplyVisibility();
        UpdateTray();
    }

    private void ApplyHotkeys()
    {
        _hotkeys.Pressed -= OnHotkey;
        _hotkeys.Pressed += OnHotkey;
        _hotkeys.Register("composer", Settings.Hotkeys.Composer);
        _hotkeys.Register("toggleWorld", Settings.Hotkeys.ToggleWorld);
    }

    private void OnHotkey(string name)
    {
        if (name == "composer") ToggleComposer();
        else if (name == "toggleWorld") SetHidden(!Settings.Hidden);
    }

    public void SetHidden(bool hidden)
    {
        var s = Settings;
        s.Hidden = hidden;
        _settingsStore.Save(s);
        ApplyVisibility();
        UpdateTray();
    }

    private void ApplyVisibility()
    {
        var visible = !Settings.Hidden && !(_fullscreen?.IsFullscreen ?? false);
        _overlay.SetWorldVisible(visible);
        if (visible)
        {
            foreach (var (id, view) in _views)
                view.SetVisible(id == Me || Settings.ShowOffline || view.Presence != PresenceState.Offline);
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Ui(() =>
        {
            try { _overlay.Relayout(Monitors.Resolve(Settings.Monitor), Settings.EdgeValue, Settings.Span); }
            catch (Exception ex) { Log.Warning(ex, "모니터 변경 재배치 실패"); }
        });
    }

    private void UpdateTray()
    {
        _tray.SetState(_realtime.IsConnected, Settings.QuietMode);
        _tray.SetHidden(Settings.Hidden);
        var online = _views.Values.Count(v => !v.IsMe && v.Presence != PresenceState.Offline);
        var tip = $"포포월드 · {_balance}포포 · 친구 {online}명 접속";
        if (_unread > 0) tip += $" · 새 말 {_unread}";
        if (!_realtime.IsConnected) tip += " · 연결 끊김";
        _tray.SetTooltip(tip);
    }

    // ---------- 종료 ----------

    private async Task LogoutAsync()
    {
        await _realtime.DisconnectAsync();
        await _supabase.SignOutAsync();
        MessageBox.Show("로그아웃했어요. 앱을 다시 실행하면 로그인 창이 떠요.", "포포월드");
        await ExitAsync();
    }

    public async Task ExitAsync()
    {
        if (_exiting) return;
        _exiting = true;
        try
        {
            StopTyping();
            _overlay.Stop();
            _fullscreen?.Dispose();
            _presence.Dispose();
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            await Task.WhenAny(_realtime.DisconnectAsync(), Task.Delay(1500));
        }
        catch (Exception ex) { Log.Warning(ex, "종료 정리 중 오류"); }
        finally
        {
            Dispose();
            Application.Current.Shutdown();
        }
    }

    public void Dispose()
    {
        foreach (var v in _views.Values) v.Close();
        _views.Clear();
        _composer?.Close();
        _store?.Close();
        try { _overlay.Close(); } catch { /* 무시 */ }
        _hotkeys.Dispose();
        _tray.Dispose();
    }
}
