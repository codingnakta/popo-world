using Microsoft.Win32;
using Serilog;

namespace PopoWorld.App.Services;

/// <summary>Windows 시작 시 자동 실행 (HKCU\...\Run).</summary>
public static class StartupRegistry
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PopoWorld";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
            if (key is null) return;
            if (enabled) key.SetValue(ValueName, $"\"{AppPaths.ExecutablePath}\"");
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "자동 실행 설정 실패");
        }
    }
}
