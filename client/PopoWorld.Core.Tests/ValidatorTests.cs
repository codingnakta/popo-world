using PopoWorld.Core.Models;
using PopoWorld.Core.Validation;

namespace PopoWorld.Core.Tests;

public class MessageValidatorTests
{
    [Fact]
    public void Accepts_200_chars_and_rejects_201()
    {
        Assert.Equal(MessageError.None, MessageValidator.Validate(new string('가', 200)));
        Assert.Equal(MessageError.TooLong, MessageValidator.Validate(new string('가', 201)));
    }

    [Fact]
    public void Rejects_empty_and_whitespace()
    {
        Assert.Equal(MessageError.Empty, MessageValidator.Validate(""));
        Assert.Equal(MessageError.Empty, MessageValidator.Validate("   \n "));
        Assert.Equal(MessageError.Empty, MessageValidator.Validate(null));
    }

    [Fact]
    public void Accepts_3_lines_and_rejects_4()
    {
        Assert.Equal(MessageError.None, MessageValidator.Validate("a\nb\nc"));
        Assert.Equal(MessageError.TooManyLines, MessageValidator.Validate("a\nb\nc\nd"));
        Assert.Equal(MessageError.TooManyLines, MessageValidator.Validate("a\r\nb\r\nc\r\nd"));
    }

    [Fact]
    public void Emoji_counts_as_one_char()
    {
        var s = string.Concat(Enumerable.Repeat("👍", 200));
        Assert.Equal(200, MessageValidator.CountChars(s));
        Assert.Equal(MessageError.None, MessageValidator.Validate(s));
    }
}

public class GiftValidatorTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Friend = Guid.NewGuid();

    [Fact]
    public void Rejects_self()
    {
        Assert.Equal("self", GiftValidator.Validate(Me, Me, 1, null, 10, 0));
    }

    [Fact]
    public void Rejects_zero_amount()
    {
        Assert.Equal("bad_amount", GiftValidator.Validate(Me, Friend, 0, null, 10, 0));
    }

    [Fact]
    public void Reports_shortfall()
    {
        Assert.Equal("insufficient:3", GiftValidator.Validate(Me, Friend, 10, null, 7, 0));
    }

    [Fact]
    public void Rejects_31st_popo_today()
    {
        Assert.Null(GiftValidator.Validate(Me, Friend, 3, null, 100, 27));
        Assert.Equal("daily_limit", GiftValidator.Validate(Me, Friend, 4, null, 100, 27));
    }

    [Fact]
    public void Rejects_long_memo()
    {
        Assert.Equal("memo_too_long", GiftValidator.Validate(Me, Friend, 1, new string('a', 21), 10, 0));
        Assert.Null(GiftValidator.Validate(Me, Friend, 1, new string('a', 20), 10, 0));
    }

    [Fact]
    public void SentToday_sums_gift_out_in_seoul_date()
    {
        var now = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.FromHours(9));
        var ledger = new[]
        {
            new LedgerEntry { Reason = LedgerReason.GiftOut, Delta = -3, CreatedAt = new DateTime(2026, 10, 30, 1, 0, 0, DateTimeKind.Utc) },
            new LedgerEntry { Reason = LedgerReason.GiftOut, Delta = -5, CreatedAt = new DateTime(2026, 10, 29, 16, 0, 0, DateTimeKind.Utc) }, // 서울 30일 01시
            new LedgerEntry { Reason = LedgerReason.GiftOut, Delta = -7, CreatedAt = new DateTime(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc) }, // 서울 29일
            new LedgerEntry { Reason = LedgerReason.GiftIn, Delta = 4, CreatedAt = new DateTime(2026, 10, 30, 1, 0, 0, DateTimeKind.Utc) },
        };
        Assert.Equal(8, GiftValidator.SentToday(ledger, now));
    }
}

public class PurchaseValidatorTests
{
    [Fact]
    public void Free_items_are_owned()
    {
        var item = new CatalogItem { Id = "char_cat", Price = 0 };
        Assert.True(PurchaseValidator.IsOwned(item, new HashSet<string>()));
        Assert.Equal("already_owned", PurchaseValidator.Validate(item, new HashSet<string>(), 0));
    }

    [Fact]
    public void Shortfall_is_reported()
    {
        var item = new CatalogItem { Id = "ball_snow", Price = 10 };
        Assert.Equal("insufficient:3", PurchaseValidator.Validate(item, new HashSet<string>(), 7));
        Assert.Null(PurchaseValidator.Validate(item, new HashSet<string>(), 10));
    }
}
