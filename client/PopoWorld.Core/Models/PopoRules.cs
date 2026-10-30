namespace PopoWorld.Core.Models;

/// <summary>
/// 포포 규칙 상수. 서버 함수(0002_functions.sql)의 상수와 같아야 한다.
/// 클라이언트는 미리 거르는 용도로만 쓰고, 최종 판정은 서버가 한다.
/// </summary>
public static class PopoRules
{
    public const int DailyLoginAmount = 5;
    public const int DailyLoginPerDay = 1;

    public const int MessageAmount = 1;
    public const int MessagePerDay = 10;

    public const int HitAmount = 1;
    public const int HitPerDay = 5;

    public const int StreakAmount = 20;
    public const int StreakDays = 7;

    public const int GiftDailyLimit = 30;
    public const int GiftMemoMaxLength = 20;

    public const int MessageMaxLength = 200;
    public const int MessageMaxLines = 3;

    public const int NameMaxLength = 8;

    /// <summary>던지기 쿨다운. 광클해도 초당 2개 이하.</summary>
    public static readonly TimeSpan ThrowCooldown = TimeSpan.FromMilliseconds(500);

    /// <summary>말풍선 표시 시간.</summary>
    public static readonly TimeSpan BubbleDuration = TimeSpan.FromSeconds(8);

    /// <summary>typing on 이 이 시간 동안 갱신되지 않으면 스스로 끈다.</summary>
    public static readonly TimeSpan TypingTimeout = TimeSpan.FromSeconds(3);

    /// <summary>typing on 은 이 간격으로만 보낸다.</summary>
    public static readonly TimeSpan TypingSendInterval = TimeSpan.FromSeconds(1);

    /// <summary>입력이 이만큼 없으면 idle.</summary>
    public static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(5);

    /// <summary>users.last_seen 갱신 주기.</summary>
    public static readonly TimeSpan LastSeenInterval = TimeSpan.FromSeconds(60);

    /// <summary>한도의 "오늘"은 서울 기준이다.</summary>
    public static readonly TimeZoneInfo Seoul = FindSeoul();

    private static TimeZoneInfo FindSeoul()
    {
        foreach (var id in new[] { "Asia/Seoul", "Korea Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("KST", TimeSpan.FromHours(9), "KST", "KST");
    }

    public static DateOnly SeoulDate(DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, Seoul).DateTime);
}
