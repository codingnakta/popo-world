using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace PopoWorld.Core.Models;

/// <summary>public.users 행. id는 auth.users.id와 같다.</summary>
[Table("users")]
public class User : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("character_id")]
    public string CharacterId { get; set; } = "char_hamster";

    [Column("bubble_id")]
    public string BubbleId { get; set; } = "bubble_basic";

    [Column("ball_id")]
    public string BallId { get; set; } = "ball_soft";

    [Column("chair_id")]
    public string? ChairId { get; set; }

    [Column("is_admin")]
    public bool IsAdmin { get; set; }

    [Column("last_seen")]
    public DateTime LastSeen { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>슬롯 이름으로 장착 아이템 id를 읽는다.</summary>
    public string? GetSlot(string slot) => slot switch
    {
        ItemCategory.Character => CharacterId,
        ItemCategory.Bubble => BubbleId,
        ItemCategory.Ball => BallId,
        ItemCategory.Chair => ChairId,
        _ => null,
    };
}
