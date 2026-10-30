using PopoWorld.Core.Models;
using Serilog;

namespace PopoWorld.App.Services;

/// <summary>settings.json 읽고 쓰기. 임시 파일에 쓰고 File.Move(overwrite) 로 교체한다.</summary>
public sealed class SettingsStore
{
    private readonly string _path;
    private readonly object _gate = new();

    public SettingsStore(string? path = null)
    {
        _path = path ?? AppPaths.SettingsFile;
    }

    public AppSettings Current { get; private set; } = new();

    public event Action<AppSettings>? Changed;

    public AppSettings Load()
    {
        lock (_gate)
        {
            try
            {
                if (File.Exists(_path))
                {
                    Current = AppSettings.FromJson(File.ReadAllText(_path));
                    return Current;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "settings.json 을 읽지 못해 기본값으로 시작합니다");
                try { File.Copy(_path, _path + ".bad", overwrite: true); } catch { /* 무시 */ }
            }

            Current = new AppSettings();
            SaveInternal(Current);
            return Current;
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            settings.Normalize();
            SaveInternal(settings);
            Current = settings;
        }
        Changed?.Invoke(settings);
    }

    private void SaveInternal(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, $"settings.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tmp, settings.ToJson());
        File.Move(tmp, _path, overwrite: true);
    }
}
