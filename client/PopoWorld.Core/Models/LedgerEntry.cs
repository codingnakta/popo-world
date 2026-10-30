using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace PopoWorld.Core.Models;

public static class LedgerReason
{
    public const string DailyLogin = "daily_login";
    public const string Message = "message";
    public const string Hit = "hit";
    public const string Streak = "streak";
    public const string Admin = "admin";
    public const string Purchase = "purchase";
    public const string GiftOut = "gift_out";
    public const string GiftIn = "gift_in";
}

[Table("popo_ledger")]
public class LedgerEntry : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("delta")]
    public int Delta { get; set; }

    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("ref_id")]
    public string? RefId { get; set; }

    [Column("request_id")]
    public Guid RequestId { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

[Table("balances")]
public class Balance : BaseModel
{
    [PrimaryKey("user_id", false)]
    public Guid UserId { get; set; }

    [Column("balance")]
    public int Value { get; set; }
}
