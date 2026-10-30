using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Resources;
using PopoWorld.Core.Models;
using Serilog;

namespace PopoWorld.App.Services;

/// <summary>
/// 효과음. exe 에 포함된 wav 를 처음 쓸 때 cache 폴더로 꺼내고 MediaPlayer 로 재생한다 (음량 조절 때문).
/// 음소거(sound.enabled=false)나 조용히 모드면 아무 소리도 내지 않는다.
/// </summary>
public sealed class SoundService
{
    public const string Throw = "throw";
    public const string Hit = "hit";
    public const string Gift = "gift";
    public const string Message = "message";

    private readonly SettingsStore _settings;
    private readonly Dictionary<string, string> _files = new();
    private readonly List<MediaPlayer> _players = new();

    public SoundService(SettingsStore settings)
    {
        _settings = settings;
    }

    public void Play(string name)
    {
        var s = _settings.Current;
        if (!s.Sound.Enabled || s.QuietMode || s.Sound.Volume <= 0) return;

        try
        {
            var path = Ensure(name);
            if (path is null) return;

            // 끝난 플레이어는 재사용
            var player = _players.FirstOrDefault(p => p.Position >= p.NaturalDuration.TimeSpan || !p.NaturalDuration.HasTimeSpan && p.Position > TimeSpan.Zero);
            if (player is null)
            {
                if (_players.Count >= 6) return; // 광클 방지
                player = new MediaPlayer();
                _players.Add(player);
            }
            player.Volume = Math.Clamp(s.Sound.Volume, 0, 1);
            player.Open(new Uri(path));
            player.Play();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "효과음 {Name} 재생 실패", name);
        }
    }

    private string? Ensure(string name)
    {
        if (_files.TryGetValue(name, out var cached) && File.Exists(cached)) return cached;

        var dir = Path.Combine(AppPaths.CacheDirectory, "sounds");
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, name + ".wav");
        if (!File.Exists(target))
        {
            StreamResourceInfo? info;
            try { info = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/sounds/{name}.wav")); }
            catch { info = null; }
            if (info is null) return null;
            using var src = info.Stream;
            using var dst = File.Create(target);
            src.CopyTo(dst);
        }
        _files[name] = target;
        return target;
    }
}
