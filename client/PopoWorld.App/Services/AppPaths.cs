namespace PopoWorld.App.Services;

/// <summary>%APPDATA%\PopoWorld 아래 파일 위치.</summary>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PopoWorld");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string SessionFile => Path.Combine(Root, "session.bin");
    public static string LogDirectory => Path.Combine(Root, "logs");
    public static string CacheDirectory => Path.Combine(Root, "cache");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(CacheDirectory);
    }

    public static string ExecutablePath => Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
}
