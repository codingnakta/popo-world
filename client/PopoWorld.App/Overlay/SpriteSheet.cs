using System.Windows;
using System.Windows.Media.Imaging;
using PopoWorld.Core.Models;

namespace PopoWorld.App.Overlay;

/// <summary>스프라이트 시트 + manifest. 프레임은 미리 잘라 캐시한다.</summary>
public sealed class SpriteSheet
{
    private readonly Dictionary<(int Row, int Index), BitmapSource> _frames = new();

    public CharacterManifest Manifest { get; }
    public BitmapSource Sheet { get; }

    public int FrameWidth => Manifest.Frame.W;
    public int FrameHeight => Manifest.Frame.H;

    public SpriteSheet(CharacterManifest manifest, BitmapSource sheet)
    {
        Manifest = manifest;
        Sheet = sheet;
    }

    public BitmapSource Frame(string animation, double elapsedSeconds)
    {
        var a = Manifest.GetAnimation(animation);
        return FrameAt(a.Row, a.FrameAt(elapsedSeconds));
    }

    public BitmapSource FrameAt(int row, int index)
    {
        if (_frames.TryGetValue((row, index), out var f)) return f;

        var x = index * FrameWidth;
        var y = row * FrameHeight;
        BitmapSource result;
        if (x + FrameWidth <= Sheet.PixelWidth && y + FrameHeight <= Sheet.PixelHeight)
        {
            var cropped = new CroppedBitmap(Sheet, new Int32Rect(x, y, FrameWidth, FrameHeight));
            cropped.Freeze();
            result = cropped;
        }
        else
        {
            // 시트가 짧으면 첫 프레임으로 대신한다
            result = (row, index) == (0, 0) ? Sheet : FrameAt(0, 0);
        }
        _frames[(row, index)] = result;
        return result;
    }
}
