using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media.Imaging;
using PopoWorld.App.Overlay;
using PopoWorld.Core.Models;
using Serilog;

namespace PopoWorld.App.Services;

/// <summary>
/// 에셋 로더. 기본 캐릭터 3종과 기본 공·말풍선은 exe 안의 pack 리소스에서,
/// 유료 아이템은 catalog.image_url 에서 첫 사용 시 내려받아 %APPDATA%\PopoWorld\cache\ 에 둔다.
/// </summary>
public sealed class AssetCache
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly Dictionary<string, SpriteSheet> _sheets = new();
    private readonly Dictionary<string, BitmapSource?> _images = new();
    private readonly Dictionary<string, string?> _urls = new();

    public const string DefaultCharacter = "char_hamster";
    public const string DefaultBall = "ball_soft";
    public const string DefaultBubble = "bubble_basic";

    /// <summary>카탈로그에서 image_url 을 알려 준다.</summary>
    public void RegisterCatalog(IEnumerable<CatalogItem> items)
    {
        foreach (var it in items) _urls[it.Id] = it.ImageUrl;
    }

    public async Task<SpriteSheet> GetCharacterAsync(string characterId)
    {
        if (_sheets.TryGetValue(characterId, out var cached)) return cached;

        var sheet = TryLoadBuiltInCharacter(characterId)
                    ?? await TryLoadRemoteCharacterAsync(characterId)
                    ?? TryLoadBuiltInCharacter(DefaultCharacter)
                    ?? throw new InvalidOperationException("기본 캐릭터 리소스가 없습니다.");
        _sheets[characterId] = sheet;
        return sheet;
    }

    public async Task<BitmapSource?> GetItemImageAsync(string itemId)
    {
        if (_images.TryGetValue(itemId, out var cached)) return cached;
        var img = TryLoadPack($"pack://application:,,,/Assets/items/{itemId}.png")
                  ?? await TryLoadRemoteImageAsync(itemId);
        _images[itemId] = img;
        return img;
    }

    public BitmapSource? GetBuiltInImage(string itemId) => TryLoadPack($"pack://application:,,,/Assets/items/{itemId}.png");

    private static SpriteSheet? TryLoadBuiltInCharacter(string id)
    {
        var png = TryLoadPack($"pack://application:,,,/Assets/characters/{id}/sheet.png");
        if (png is null) return null;
        var manifest = TryReadPackText($"pack://application:,,,/Assets/characters/{id}/manifest.json");
        var m = manifest is null ? CharacterManifest.Default(id) : CharacterManifest.FromJson(manifest);
        return new SpriteSheet(m, png);
    }

    private async Task<SpriteSheet?> TryLoadRemoteCharacterAsync(string id)
    {
        if (!_urls.TryGetValue(id, out var url) || string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            var dir = Path.Combine(AppPaths.CacheDirectory, "characters", id);
            Directory.CreateDirectory(dir);
            var sheetPath = Path.Combine(dir, "sheet.png");
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(sheetPath)) await DownloadAsync(url, sheetPath);
            if (!File.Exists(manifestPath))
            {
                // sheet.png 와 같은 폴더의 manifest.json
                var manifestUrl = url[..(url.LastIndexOf('/') + 1)] + "manifest.json";
                try { await DownloadAsync(manifestUrl, manifestPath); }
                catch (Exception ex) { Log.Information(ex, "{Id} manifest 없음, 기본 규격 사용", id); }
            }
            var manifest = File.Exists(manifestPath) ? CharacterManifest.FromJson(File.ReadAllText(manifestPath)) : CharacterManifest.Default(id);
            return new SpriteSheet(manifest, LoadFile(sheetPath));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "캐릭터 {Id} 내려받기 실패", id);
            return null;
        }
    }

    private async Task<BitmapSource?> TryLoadRemoteImageAsync(string id)
    {
        if (!_urls.TryGetValue(id, out var url) || string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            var path = Path.Combine(AppPaths.CacheDirectory, "items", id + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!File.Exists(path)) await DownloadAsync(url, path);
            return LoadFile(path);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "아이템 {Id} 내려받기 실패", id);
            return null;
        }
    }

    private static async Task DownloadAsync(string url, string path)
    {
        var bytes = await Http.GetByteArrayAsync(url);
        var tmp = path + ".tmp";
        await File.WriteAllBytesAsync(tmp, bytes);
        File.Move(tmp, path, overwrite: true);
    }

    public static BitmapSource? TryLoadPack(string uri)
    {
        try
        {
            var info = Application.GetResourceStream(new Uri(uri));
            if (info is null) return null;
            using var s = info.Stream;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = s;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadPackText(string uri)
    {
        try
        {
            var info = Application.GetResourceStream(new Uri(uri));
            if (info is null) return null;
            using var r = new StreamReader(info.Stream);
            return r.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource LoadFile(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
