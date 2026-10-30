using PopoWorld.Core.Models;

namespace PopoWorld.Core.Layout;

/// <summary>
/// 네 방향 띠 배치와 좌표 변환.
/// 모든 물리는 "바닥이 아래"인 로컬 좌표(x: 변을 따라 진행, y: 바닥에서 높이)로 계산하고,
/// ToScreen 이 마지막에 회전·반사해 화면(물리 픽셀) 좌표로 바꾼다.
/// </summary>
public sealed class EdgeLayout
{
    public const double Thickness = 160;

    public Edge Edge { get; }
    public double Span { get; }

    /// <summary>선택한 모니터의 작업 영역 (물리 픽셀).</summary>
    public RectD WorkArea { get; }

    /// <summary>띠가 차지하는 화면 영역 (물리 픽셀).</summary>
    public RectD Band { get; }

    /// <summary>로컬 x 가 움직일 수 있는 길이 (변 길이 × span).</summary>
    public double Length { get; }

    public EdgeLayout(RectD workArea, Edge edge, double span)
    {
        if (span <= 0 || span > 1) throw new ArgumentOutOfRangeException(nameof(span));
        WorkArea = workArea;
        Edge = edge;
        Span = span;

        if (edge.IsVertical())
        {
            Length = Math.Floor(workArea.Height * span);
            var y = workArea.Y + Math.Floor((workArea.Height - Length) / 2);
            var x = edge == Edge.Left ? workArea.X : workArea.Right - Thickness;
            Band = new RectD(x, y, Thickness, Length);
        }
        else
        {
            Length = Math.Floor(workArea.Width * span);
            var x = workArea.X + Math.Floor((workArea.Width - Length) / 2);
            var y = edge == Edge.Top ? workArea.Y : workArea.Bottom - Thickness;
            Band = new RectD(x, y, Length, Thickness);
        }
    }

    /// <summary>로컬(x 진행, y 높이) → 화면 좌표.</summary>
    public PointD ToScreen(PointD local) => Edge switch
    {
        Edge.Bottom => new PointD(Band.X + local.X, Band.Bottom - local.Y),
        Edge.Top => new PointD(Band.X + local.X, Band.Y + local.Y),
        Edge.Left => new PointD(Band.X + local.Y, Band.Y + local.X),
        Edge.Right => new PointD(Band.Right - local.Y, Band.Y + local.X),
        _ => throw new InvalidOperationException(),
    };

    /// <summary>화면 좌표 → 로컬. 드래그할 때 마우스 위치를 넘길 때 쓴다.</summary>
    public PointD ToLocal(PointD screen) => Edge switch
    {
        Edge.Bottom => new PointD(screen.X - Band.X, Band.Bottom - screen.Y),
        Edge.Top => new PointD(screen.X - Band.X, screen.Y - Band.Y),
        Edge.Left => new PointD(screen.Y - Band.Y, screen.X - Band.X),
        Edge.Right => new PointD(screen.Y - Band.Y, Band.Right - screen.X),
        _ => throw new InvalidOperationException(),
    };

    /// <summary>로컬 x 를 띠 안으로 자른다 (캐릭터 반폭만큼 여유).</summary>
    public double ClampX(double x, double halfWidth) =>
        Length <= halfWidth * 2 ? Length / 2 : Math.Clamp(x, halfWidth, Length - halfWidth);

    /// <summary>
    /// 스프라이트를 어떻게 그릴지. 위 변은 거꾸로 매달린 것처럼 상하 반전,
    /// 좌·우 변은 세워진 채로 걷되 화면 안쪽을 본다.
    /// </summary>
    public SpriteTransform SpriteFor(int facing)
    {
        return Edge switch
        {
            Edge.Bottom => new SpriteTransform(FlipX: facing < 0, FlipY: false),
            Edge.Top => new SpriteTransform(FlipX: facing < 0, FlipY: true),
            Edge.Left => new SpriteTransform(FlipX: false, FlipY: false),
            Edge.Right => new SpriteTransform(FlipX: true, FlipY: false),
            _ => new SpriteTransform(false, false),
        };
    }

    /// <summary>
    /// 띠 바닥에 발을 붙인 스프라이트의 화면 좌상단 좌표.
    /// localFootX/Y 는 캐릭터 발 위치(로컬), size 는 화면에 그릴 크기, anchor 는 스프라이트 안의 발 위치(화면 배율 적용 후).
    /// </summary>
    public PointD SpriteTopLeft(PointD localFoot, double width, double height, PointD anchor, SpriteTransform t)
    {
        var foot = ToScreen(localFoot);
        var ax = t.FlipX ? width - anchor.X : anchor.X;
        var ay = t.FlipY ? height - anchor.Y : anchor.Y;
        return Edge switch
        {
            // 위·아래 변: 발이 바닥에, 가로로 선 그림
            Edge.Bottom or Edge.Top => new PointD(foot.X - ax, foot.Y - ay),
            // 좌·우 변: 세워진 그림을 변에 붙인다. 발 y(세로 진행 방향)는 그림 세로 중앙, 가로는 변에 붙임
            Edge.Left => new PointD(Band.X, foot.Y - height / 2),
            Edge.Right => new PointD(Band.Right - width, foot.Y - height / 2),
            _ => foot,
        };
    }
}

public readonly record struct SpriteTransform(bool FlipX, bool FlipY);
