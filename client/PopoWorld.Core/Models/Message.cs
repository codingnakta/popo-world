using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace PopoWorld.Core.Models;

[Table("messages")]
public class Message : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("body")]
    public string Body { get; set; } = string.Empty;

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }
}
