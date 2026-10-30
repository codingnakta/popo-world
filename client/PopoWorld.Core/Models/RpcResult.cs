using System.Text.Json;
using System.Text.Json.Serialization;

namespace PopoWorld.Core.Models;

/// <summary>
/// 서버 함수(earn, purchase, gift, equip, admin_grant)가 돌려주는 공통 응답.
/// 실패해도 예외가 아니라 ok=false와 error 코드가 온다.
/// </summary>
public sealed class RpcResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("balance")]
    public int? Balance { get; set; }

    /// <summary>'insufficient:3', 'limit_reached', 'duplicate' 같은 코드.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>error 코드의 접두어 ('insufficient:3' → 'insufficient').</summary>
    [JsonIgnore]
    public string? ErrorCode => Error is null ? null : Error.Split(':', 2)[0];

    /// <summary>'insufficient:3' → 3. 없으면 null.</summary>
    [JsonIgnore]
    public int? Shortfall =>
        Error is not null && Error.StartsWith("insufficient:", StringComparison.Ordinal)
        && int.TryParse(Error.AsSpan("insufficient:".Length), out var n) ? n : null;

    public static RpcResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Fail("empty_response");
        try
        {
            return JsonSerializer.Deserialize<RpcResult>(json) ?? Fail("empty_response");
        }
        catch (JsonException)
        {
            return Fail("bad_response");
        }
    }

    public static RpcResult Fail(string error, int? balance = null) => new() { Ok = false, Error = error, Balance = balance };

    /// <summary>사용자에게 보여줄 한국어 문구.</summary>
    public string Describe() => ErrorCode switch
    {
        null => "완료",
        "insufficient" => $"{Shortfall ?? 0}포포 부족",
        "limit_reached" => "오늘 한도에 도달했어요",
        "daily_limit" => "오늘 선물 한도(30포포)를 넘어요",
        "already_owned" => "이미 가지고 있어요",
        "not_found" => "없는 아이템이에요",
        "not_owned" => "아직 없는 아이템이에요",
        "self" => "나에게는 보낼 수 없어요",
        "bad_amount" => "1포포 이상 보내야 해요",
        "bad_slot" => "잘못된 슬롯이에요",
        "bad_reason" => "잘못된 요청이에요",
        "forbidden" => "권한이 없어요",
        "duplicate" => "이미 처리된 요청이에요",
        "network" => "서버에 연결할 수 없어요",
        _ => $"실패: {Error}",
    };
}
