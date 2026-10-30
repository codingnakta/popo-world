using PopoWorld.Core.Models;

namespace PopoWorld.Core.Validation;

/// <summary>
/// 선물 보내기 전 클라이언트 검사. 오류 코드는 서버 gift() 와 같은 문자열을 쓴다.
/// </summary>
public static class GiftValidator
{
    /// <summary>
    /// 문제가 없으면 null, 있으면 서버와 같은 error 코드 ('self', 'bad_amount', 'insufficient:n', 'daily_limit', 'memo_too_long').
    /// </summary>
    public static string? Validate(Guid me, Guid to, int amount, string? memo, int balance, int sentToday)
    {
        if (to == me || to == Guid.Empty) return "self";
        if (amount < 1) return "bad_amount";
        if (memo is not null && MessageValidator.CountChars(memo) > PopoRules.GiftMemoMaxLength) return "memo_too_long";
        if (balance < amount) return $"insufficient:{amount - balance}";
        if (sentToday + amount > PopoRules.GiftDailyLimit) return "daily_limit";
        return null;
    }

    /// <summary>오늘 더 보낼 수 있는 양.</summary>
    public static int RemainingToday(int sentToday) => Math.Max(0, PopoRules.GiftDailyLimit - sentToday);

    /// <summary>오늘 보낸 gift_out 합계 (원장 항목에서, 서울 날짜 기준).</summary>
    public static int SentToday(IEnumerable<LedgerEntry> ledger, DateTimeOffset now)
    {
        var today = PopoRules.SeoulDate(now);
        return ledger
            .Where(e => e.Reason == LedgerReason.GiftOut && PopoRules.SeoulDate(new DateTimeOffset(DateTime.SpecifyKind(e.CreatedAt, DateTimeKind.Utc))) == today)
            .Sum(e => -e.Delta);
    }
}
