using System.Reflection;
using System.Text.Json;

namespace PopoWorld.App.Services;

/// <summary>
/// SUPABASE_URL, SUPABASE_ANON_KEY. exe 옆의 appsettings.json 이 있으면 그것을, 없으면 exe 에 포함된 것을 쓴다.
/// anon key 는 친구에게 공개돼도 RLS 와 RPC 가 막아 주므로 괜찮다. service_role 키는 절대 넣지 않는다.
/// </summary>
public sealed record AppConfig(string SupabaseUrl, string SupabaseAnonKey)
{
    public bool IsPlaceholder => SupabaseUrl.Contains("YOUR-PROJECT", StringComparison.OrdinalIgnoreCase)
                                 || SupabaseAnonKey.Contains("YOUR-ANON-KEY", StringComparison.OrdinalIgnoreCase);

    public static AppConfig Load()
    {
        var exeDir = Path.GetDirectoryName(AppPaths.ExecutablePath) ?? AppContext.BaseDirectory;
        var sidecar = Path.Combine(exeDir, "appsettings.json");
        if (File.Exists(sidecar))
            return Parse(File.ReadAllText(sidecar));

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("appsettings.json")
                           ?? throw new InvalidOperationException("appsettings.json 리소스가 없습니다.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    private static AppConfig Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var url = root.TryGetProperty("SUPABASE_URL", out var u) ? u.GetString() : null;
        var key = root.TryGetProperty("SUPABASE_ANON_KEY", out var k) ? k.GetString() : null;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("appsettings.json 에 SUPABASE_URL 과 SUPABASE_ANON_KEY 가 필요합니다.");
        return new AppConfig(url.TrimEnd('/'), key);
    }
}
