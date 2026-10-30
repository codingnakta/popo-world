using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace PopoWorld.Core.Models;

public static class ItemCategory
{
    public const string Character = "character";
    public const string Bubble = "bubble";
    public const string Ball = "ball";
    public const string Chair = "chair";

    public static readonly string[] All = [Character, Bubble, Ball, Chair];

    public static string Label(string category) => category switch
    {
        Character => "캐릭터",
        Bubble => "말풍선",
        Ball => "공",
        Chair => "의자",
        _ => category,
    };
}

[Table("catalog")]
public class CatalogItem : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("category")]
    public string Category { get; set; } = ItemCategory.Character;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("price")]
    public int Price { get; set; }

    [Column("image_url")]
    public string? ImageUrl { get; set; }

    /// <summary>'white_flash', 'red_stain' 등. 클라이언트가 해석한다.</summary>
    [Column("effect")]
    public string? Effect { get; set; }

    [Column("sort")]
    public int Sort { get; set; }

    [Column("active")]
    public bool Active { get; set; } = true;

    /// <summary>가격 0인 기본 아이템은 모두 보유한 것으로 본다.</summary>
    public bool IsFree => Price == 0;
}

[Table("inventory")]
public class InventoryItem : BaseModel
{
    [PrimaryKey("user_id", false)]
    public Guid UserId { get; set; }

    [PrimaryKey("item_id", false)]
    public string ItemId { get; set; } = string.Empty;

    [Column("acquired_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime AcquiredAt { get; set; }
}
