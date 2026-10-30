using PopoWorld.Core.Models;
using Serilog;
using Supabase.Postgrest;
using Supabase.Postgrest.Exceptions;
using static Supabase.Postgrest.Constants;

namespace PopoWorld.App.Services;

/// <summary>
/// 서버 함수(RPC) 호출 래퍼와 표 조회. 포포와 보유를 바꾸는 길은 RPC 5개뿐이다.
/// 네트워크 오류는 예외 대신 ok=false, error="network" 로 돌려준다.
/// </summary>
public sealed class PopoService
{
    private readonly SupabaseService _supabase;

    public PopoService(SupabaseService supabase)
    {
        _supabase = supabase;
    }

    private Supabase.Client Client => _supabase.Client;
    private Guid Me => _supabase.UserId ?? throw new InvalidOperationException("로그인되어 있지 않습니다.");

    // ---------- RPC ----------

    public Task<RpcResult> EarnAsync(string reason, Guid requestId) =>
        CallAsync("earn", new Dictionary<string, object> { ["reason"] = reason, ["request_id"] = requestId });

    public Task<RpcResult> PurchaseAsync(string itemId, Guid requestId) =>
        CallAsync("purchase", new Dictionary<string, object> { ["item_id"] = itemId, ["request_id"] = requestId });

    public Task<RpcResult> GiftAsync(Guid toUser, int amount, string? memo, Guid requestId) =>
        CallAsync("gift", new Dictionary<string, object>
        {
            ["to_user"] = toUser,
            ["amount"] = amount,
            ["memo"] = memo ?? string.Empty,
            ["request_id"] = requestId,
        });

    public Task<RpcResult> EquipAsync(string slot, string? itemId) =>
        CallAsync("equip", new Dictionary<string, object?> { ["slot"] = slot, ["item_id"] = itemId });

    public Task<RpcResult> AdminGrantAsync(Guid toUser, int amount, string? memo) =>
        CallAsync("admin_grant", new Dictionary<string, object> { ["to_user"] = toUser, ["amount"] = amount, ["memo"] = memo ?? string.Empty });

    private async Task<RpcResult> CallAsync(string function, object parameters)
    {
        try
        {
            var response = await Client.Rpc(function, parameters);
            var result = RpcResult.Parse(response.Content);
            if (result.Ok) Log.Information("rpc {Function} ok balance={Balance}", function, result.Balance);
            else Log.Information("rpc {Function} rejected: {Error}", function, result.Error);
            return result;
        }
        catch (PostgrestException ex)
        {
            Log.Warning("rpc {Function} failed: {Status} {Content}", function, ex.StatusCode, ex.Content);
            return RpcResult.Fail(ex.StatusCode == 0 ? "network" : $"http_{ex.StatusCode}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "rpc {Function} failed", function);
            return RpcResult.Fail("network");
        }
    }

    // ---------- 조회 ----------

    public async Task<List<User>> GetUsersAsync()
    {
        var r = await Client.From<User>().Order("created_at", Ordering.Ascending).Get();
        return r.Models;
    }

    public async Task<User?> GetUserAsync(Guid id)
    {
        var r = await Client.From<User>().Filter("id", Operator.Equals, id.ToString()).Get();
        return r.Models.FirstOrDefault();
    }

    public async Task<List<CatalogItem>> GetCatalogAsync()
    {
        var r = await Client.From<CatalogItem>()
            .Filter("active", Operator.Equals, "true")
            .Order("category", Ordering.Ascending)
            .Order("sort", Ordering.Ascending)
            .Get();
        return r.Models;
    }

    public async Task<HashSet<string>> GetInventoryAsync()
    {
        var r = await Client.From<InventoryItem>().Filter("user_id", Operator.Equals, Me.ToString()).Get();
        return r.Models.Select(i => i.ItemId).ToHashSet();
    }

    public async Task<int> GetBalanceAsync()
    {
        var r = await Client.From<Balance>().Filter("user_id", Operator.Equals, Me.ToString()).Get();
        return r.Models.FirstOrDefault()?.Value ?? 0;
    }

    public async Task<List<LedgerEntry>> GetLedgerAsync(int limit = 200)
    {
        var r = await Client.From<LedgerEntry>()
            .Filter("user_id", Operator.Equals, Me.ToString())
            .Order("created_at", Ordering.Descending)
            .Limit(limit)
            .Get();
        return r.Models;
    }

    /// <summary>특정 시각 이후의 gift_in 원장 (오프라인 중 받은 선물 알림용).</summary>
    public async Task<List<LedgerEntry>> GetGiftsReceivedSinceAsync(DateTime sinceUtc)
    {
        var r = await Client.From<LedgerEntry>()
            .Filter("user_id", Operator.Equals, Me.ToString())
            .Filter("reason", Operator.Equals, LedgerReason.GiftIn)
            .Filter("created_at", Operator.GreaterThan, sinceUtc.ToString("o"))
            .Order("created_at", Ordering.Ascending)
            .Get();
        return r.Models;
    }

    public async Task<List<Message>> GetMessagesSinceAsync(DateTime sinceUtc, int limit = 50)
    {
        var r = await Client.From<Message>()
            .Filter("created_at", Operator.GreaterThan, sinceUtc.ToString("o"))
            .Order("created_at", Ordering.Ascending)
            .Limit(limit)
            .Get();
        return r.Models;
    }

    // ---------- 쓰기 (RLS 가 허용하는 것만) ----------

    public async Task<Message> SendMessageAsync(string body)
    {
        var r = await Client.From<Message>().Insert(new Message { UserId = Me, Body = body });
        return r.Models.FirstOrDefault() ?? new Message { UserId = Me, Body = body, CreatedAt = DateTime.UtcNow };
    }

    public async Task UpdateLastSeenAsync()
    {
        try
        {
            await Client.From<User>()
                .Filter("id", Operator.Equals, Me.ToString())
                .Set(u => u.LastSeen, DateTime.UtcNow)
                .Update();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "last_seen 갱신 실패");
        }
    }

    public async Task UpdateNameAsync(string name)
    {
        await Client.From<User>()
            .Filter("id", Operator.Equals, Me.ToString())
            .Set(u => u.Name, name)
            .Update();
    }
}
