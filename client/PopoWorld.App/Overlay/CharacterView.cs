using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PopoWorld.Core.Layout;
using PopoWorld.Core.Models;
using PopoWorld.Core.Physics;

namespace PopoWorld.App.Overlay;

/// <summary>
/// 캐릭터 한 마리: 스프라이트 Image, 이름표, 말풍선, 입력 중 표시, 피격 효과, 그리고 입력을 받는 HotspotWindow.
/// 물리는 로컬 좌표(WalkAgent, DragSpring)로 하고, Update 에서 EdgeLayout 으로 화면에 놓는다.
/// </summary>
public sealed class CharacterView
{
    public const int HotspotMargin = 8;
    private const double DisplayScale = CharacterManifest.DisplayScale;

    public User User { get; private set; }
    public bool IsMe { get; }
    public SpriteSheet Sheet { get; private set; }
    public WalkAgent Walk { get; }
    public DragSpring Drag { get; } = new();
    public HotspotWindow Hotspot { get; }

    public PresenceState Presence { get; private set; } = PresenceState.Offline;
    public bool Visible { get; private set; } = true;

    // 그리기 요소
    public Image Sprite { get; } = new();
    public TextBlock NameLabel { get; } = new();
    public Border Bubble { get; } = new();
    private readonly TextBlock _bubbleText = new();
    public Border Typing { get; } = new();
    private readonly System.Windows.Shapes.Shape _effect = new System.Windows.Shapes.Ellipse();
    private readonly ScaleTransform _flip = new(1, 1);

    private string _animation = AnimationNames.Idle;
    private double _animElapsed;
    private double _hitRemaining;
    private string? _hitEffect;
    private double _effectRemaining;
    private DateTime _bubbleUntil = DateTime.MinValue;
    private DateTime _typingUntil = DateTime.MinValue;

    // 마지막 틱에 계산한 화면 좌표 (물리 픽셀)
    public PointD ScreenCenter { get; private set; }
    public PointD ScreenHead { get; private set; }
    public RectD ScreenRect { get; private set; }
    public RectD? BubbleRect { get; private set; }

    public CharacterView(User user, bool isMe, SpriteSheet sheet, double rangeMin, double rangeMax, Random random)
    {
        User = user;
        IsMe = isMe;
        Sheet = sheet;
        Walk = new WalkAgent(rangeMin, rangeMax, random);
        Hotspot = new HotspotWindow();

        RenderOptions.SetBitmapScalingMode(Sprite, BitmapScalingMode.NearestNeighbor);
        Sprite.SnapsToDevicePixels = true;
        Sprite.RenderTransform = _flip;
        Sprite.RenderTransformOrigin = new Point(0.5, 0.5);
        Sprite.IsHitTestVisible = false;

        NameLabel.FontSize = 11;
        NameLabel.FontWeight = FontWeights.SemiBold;
        NameLabel.Foreground = Brushes.White;
        NameLabel.TextAlignment = TextAlignment.Center;
        NameLabel.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 3, ShadowDepth = 0, Opacity = 0.9 };
        NameLabel.IsHitTestVisible = false;

        _bubbleText.TextWrapping = TextWrapping.Wrap;
        _bubbleText.MaxWidth = 180;
        _bubbleText.FontSize = 12;
        _bubbleText.Foreground = Brushes.Black;
        Bubble.Child = _bubbleText;
        Bubble.Background = Brushes.White;
        Bubble.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));
        Bubble.BorderThickness = new Thickness(2);
        Bubble.CornerRadius = new CornerRadius(8);
        Bubble.Padding = new Thickness(8, 5, 8, 5);
        Bubble.Visibility = Visibility.Collapsed;
        Bubble.IsHitTestVisible = false;

        Typing.Child = new TextBlock { Text = "…", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brushes.Black };
        Typing.Background = Brushes.White;
        Typing.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));
        Typing.BorderThickness = new Thickness(2);
        Typing.CornerRadius = new CornerRadius(8);
        Typing.Padding = new Thickness(6, 0, 6, 2);
        Typing.Visibility = Visibility.Collapsed;
        Typing.IsHitTestVisible = false;

        _effect.Visibility = Visibility.Collapsed;
        _effect.IsHitTestVisible = false;

        UpdateNameLabel();
    }

    public UIElement[] Elements => [_effect, Sprite, NameLabel, Typing, Bubble];

    public void SetUser(User user)
    {
        User = user;
        UpdateNameLabel();
    }

    public void SetSheet(SpriteSheet sheet)
    {
        Sheet = sheet;
        _animElapsed = 0;
    }

    public void SetPresence(PresenceState state)
    {
        Presence = state;
        UpdateNameLabel();
    }

    public void SetVisible(bool visible)
    {
        Visible = visible;
        var v = visible ? Visibility.Visible : Visibility.Collapsed;
        Sprite.Visibility = v;
        NameLabel.Visibility = v;
        if (!visible)
        {
            Bubble.Visibility = Visibility.Collapsed;
            Typing.Visibility = Visibility.Collapsed;
            _effect.Visibility = Visibility.Collapsed;
            if (Hotspot.IsVisible) Hotspot.Hide();
        }
    }

    private void UpdateNameLabel()
    {
        NameLabel.Text = User.Name;
        NameLabel.Opacity = Presence == PresenceState.Offline ? 0.6 : 1;
        Sprite.Opacity = Presence == PresenceState.Offline ? 0.75 : 1;
    }

    public void ShowBubble(string text, TimeSpan? duration = null)
    {
        _bubbleText.Text = text;
        Bubble.Visibility = Visibility.Visible;
        _bubbleUntil = DateTime.UtcNow + (duration ?? PopoRules.BubbleDuration);
    }

    public void SetTyping(bool on)
    {
        _typingUntil = on ? DateTime.UtcNow + PopoRules.TypingTimeout : DateTime.MinValue;
    }

    /// <summary>피격: hit 애니메이션 + 공 효과(white_flash, red_stain).</summary>
    public void PlayHit(string? effect)
    {
        _animation = AnimationNames.Hit;
        _animElapsed = 0;
        var a = Sheet.Manifest.GetAnimation(AnimationNames.Hit);
        _hitRemaining = Math.Max(0.3, a.Frames / a.Fps);
        _hitEffect = effect;
        _effectRemaining = effect switch
        {
            "white_flash" => 0.35,
            "red_stain" => 1.2,
            null or "" => 0,
            _ => 0.5,
        };
        if (_effectRemaining > 0)
        {
            _effect.Fill = effect switch
            {
                "white_flash" => Brushes.White,
                "red_stain" => new SolidColorBrush(Color.FromArgb(200, 220, 40, 40)),
                _ => new SolidColorBrush(Color.FromArgb(160, 255, 230, 120)),
            };
            _effect.Visibility = Visibility.Visible;
        }
    }

    /// <summary>한 틱. dpi 는 WPF 좌표로 바꿀 때만 쓴다.</summary>
    public void Update(double dt, EdgeLayout layout, double dpi, RectD overlayOrigin, RectD screen, List<RectD> placedBubbles, DateTime now, bool allowWalk)
    {
        Walk.SetRange(layout.ClampX(0, HalfWidth), layout.ClampX(layout.Length, HalfWidth));
        Walk.Frozen = !allowWalk || Drag.IsActive || Presence != PresenceState.Online || _hitRemaining > 0;

        // 1. 물리
        if (Drag.IsActive)
        {
            Drag.Step(dt);
            if (!Drag.IsActive) Walk.ResetAt(Drag.Position.X);
        }
        else
        {
            Walk.Step(dt);
        }

        // 2. 애니메이션 선택
        if (_hitRemaining > 0)
        {
            _hitRemaining -= dt;
            if (_hitRemaining <= 0) _animation = AnimationNames.Idle;
        }
        else
        {
            var next = Presence switch
            {
                PresenceState.Offline => AnimationNames.Offline,
                PresenceState.Idle => AnimationNames.Sleep,
                _ when Drag.IsActive => AnimationNames.Idle,
                _ when Walk.State == WalkState.Walk => AnimationNames.Walk,
                _ => AnimationNames.Idle,
            };
            if (next != _animation) { _animation = next; _animElapsed = 0; }
        }
        _animElapsed += dt;
        Sprite.Source = Sheet.Frame(_animation, _animElapsed);

        // 3. 위치
        var foot = Drag.IsActive ? Drag.Position : new PointD(Walk.X, 0);
        var w = Sheet.FrameWidth * DisplayScale;
        var h = Sheet.FrameHeight * DisplayScale;
        var transform = layout.SpriteFor(Walk.Facing);
        var anchor = new PointD(Sheet.Manifest.Anchor.X * DisplayScale, Sheet.Manifest.Anchor.Y * DisplayScale);
        var topLeft = layout.SpriteTopLeft(foot, w, h, anchor, transform);
        ScreenRect = new RectD(topLeft.X, topLeft.Y, w, h);
        ScreenCenter = new PointD(topLeft.X + w / 2, topLeft.Y + h / 2);

        var headX = transform.FlipX ? w - Sheet.Manifest.Head.X * DisplayScale : Sheet.Manifest.Head.X * DisplayScale;
        var headY = transform.FlipY ? h - Sheet.Manifest.Head.Y * DisplayScale : Sheet.Manifest.Head.Y * DisplayScale;
        ScreenHead = new PointD(topLeft.X + headX, topLeft.Y + headY);

        _flip.ScaleX = transform.FlipX ? -1 : 1;
        _flip.ScaleY = transform.FlipY ? -1 : 1;

        Place(Sprite, topLeft, w, h, dpi, overlayOrigin);

        // 이름표: 발 아래 (위 변이면 머리 위쪽이 바닥이므로 반대)
        NameLabel.Width = Math.Max(w, 80) / dpi;
        var nameY = layout.Edge == Edge.Top ? topLeft.Y + h + 2 : topLeft.Y - 16;
        if (layout.Edge.IsVertical()) nameY = topLeft.Y + h + 2;
        Place(NameLabel, new PointD(ScreenCenter.X - Math.Max(w, 80) / 2, nameY), Math.Max(w, 80), 16, dpi, overlayOrigin);

        // 4. 피격 효과
        if (_effectRemaining > 0)
        {
            _effectRemaining -= dt;
            if (_effectRemaining <= 0) _effect.Visibility = Visibility.Collapsed;
            else
            {
                var size = _hitEffect == "red_stain" ? w * 0.7 : w * 1.1;
                _effect.Opacity = Math.Min(1, _effectRemaining * 2);
                Place(_effect, new PointD(ScreenCenter.X - size / 2, ScreenCenter.Y - size / 2), size, size, dpi, overlayOrigin);
            }
        }

        // 5. 말풍선 / 입력 중
        BubbleRect = null;
        if (Bubble.Visibility == Visibility.Visible)
        {
            if (now >= _bubbleUntil) Bubble.Visibility = Visibility.Collapsed;
            else
            {
                Bubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var size = new PointD(Bubble.DesiredSize.Width * dpi, Bubble.DesiredSize.Height * dpi);
                var rect = BubbleLayout.Place(ScreenHead, size, screen, placedBubbles, layout.Edge);
                placedBubbles.Add(rect);
                BubbleRect = rect;
                Place(Bubble, new PointD(rect.X, rect.Y), rect.Width, rect.Height, dpi, overlayOrigin, setSize: false);
            }
        }

        var typingOn = now < _typingUntil && Presence != PresenceState.Offline;
        Typing.Visibility = typingOn ? Visibility.Visible : Visibility.Collapsed;
        if (typingOn)
        {
            Typing.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = new PointD(Typing.DesiredSize.Width * dpi, Typing.DesiredSize.Height * dpi);
            var rect = BubbleLayout.Place(ScreenHead, size, screen, placedBubbles, layout.Edge);
            placedBubbles.Add(rect);
            Place(Typing, new PointD(rect.X, rect.Y), rect.Width, rect.Height, dpi, overlayOrigin, setSize: false);
        }

        // 6. 핫스팟
        if (Visible)
        {
            if (!Hotspot.IsVisible) Hotspot.Show();
            Hotspot.MoveTo(new RectD(topLeft.X - HotspotMargin, topLeft.Y - HotspotMargin, w + HotspotMargin * 2, h + HotspotMargin * 2));
        }
    }

    public double HalfWidth => Sheet.FrameWidth * DisplayScale / 2.0;

    private static void Place(FrameworkElement el, PointD screenTopLeft, double w, double h, double dpi, RectD overlayOrigin, bool setSize = true)
    {
        Canvas.SetLeft(el, (screenTopLeft.X - overlayOrigin.X) / dpi);
        Canvas.SetTop(el, (screenTopLeft.Y - overlayOrigin.Y) / dpi);
        if (setSize)
        {
            el.Width = w / dpi;
            el.Height = h / dpi;
        }
    }

    public void Close()
    {
        try { Hotspot.Close(); } catch { /* 이미 닫힘 */ }
    }
}
