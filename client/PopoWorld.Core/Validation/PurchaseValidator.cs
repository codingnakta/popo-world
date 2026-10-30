using PopoWorld.Core.Models;

namespace PopoWorld.Core.Validation;

/// <summary>상점 버튼 상태를 정하기 위한 클라이언트 검사. 최종 판정은 서버 purchase().</summary>
public static class PurchaseValidator
{
    public static bool IsOwned(CatalogItem item, IReadOnlySet<string> inventory) => item.IsFree || inventory.Contains(item.Id);

    /// <summary>null 이면 살 수 있음. 아니면 서버와 같은 error 코드.</summary>
    public static string? Validate(CatalogItem item, IReadOnlySet<string> inventory, int balance)
    {
        if (!item.Active) return "not_found";
        if (IsOwned(item, inventory)) return "already_owned";
        if (balance < item.Price) return $"insufficient:{item.Price - balance}";
        return null;
    }
}
