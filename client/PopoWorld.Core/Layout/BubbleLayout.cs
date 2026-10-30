using PopoWorld.Core.Models;

namespace PopoWorld.Core.Layout;

/// <summary>
/// 말풍선 위치. 캐릭터 머리 위 8px, 화면 밖이면 반대쪽으로 뒤집고, 겹치면 24px씩 위로 올린다.
/// 좌표는 전부 화면(물리 픽셀).
/// </summary>
public static class BubbleLayout
{
    public const double Gap = 8;
    public const double Step = 24;

    /// <summary>
    /// head: 말풍선 꼬리가 가리킬 점(화면). size: 말풍선 크기. screen: 모니터 작업 영역.
    /// placed: 이미 놓인 말풍선들. edge: 띠 방향 (위 변이면 머리 "아래"로 편다).
    /// </summary>
    public static RectD Place(PointD head, PointD size, RectD screen, IReadOnlyList<RectD> placed, Edge edge)
    {
        var up = edge != Edge.Top; // 위 변은 거꾸로 매달려 있어 말풍선을 아래로
        var x = head.X - size.X / 2;
        if (x < screen.Left) x = screen.Left;
        if (x + size.X > screen.Right) x = screen.Right - size.X;

        var y = up ? head.Y - Gap - size.Y : head.Y + Gap;
        if (up && y < screen.Top) { y = head.Y + Gap; up = false; }
        else if (!up && y + size.Y > screen.Bottom) { y = head.Y - Gap - size.Y; up = true; }

        var rect = new RectD(x, y, size.X, size.Y);
        var guard = 0;
        while (guard++ < 40 && placed.Any(p => Overlaps(p, rect)))
        {
            y += up ? -Step : Step;
            rect = new RectD(x, y, size.X, size.Y);
        }
        return rect;
    }

    public static bool Overlaps(RectD a, RectD b) =>
        a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
}
