using System.Windows.Input;
using System.Windows.Interop;
using PopoWorld.App.Overlay;
using Serilog;

namespace PopoWorld.App.Services;

/// <summary>전역 단축키. "Ctrl+Shift+Space" 같은 문자열을 RegisterHotKey 로 등록한다.</summary>
public sealed class HotkeyService : IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<int, string> _ids = new();
    private int _nextId = 0x5000;

    public event Action<string>? Pressed;

    public HotkeyService()
    {
        var p = new HwndSourceParameters("PopoWorldHotkeys") { Width = 0, Height = 0, WindowStyle = 0, ExtendedWindowStyle = 0, ParentWindow = Win32.HWND_MESSAGE };
        _source = new HwndSource(p);
        _source.AddHook(WndProc);
    }

    /// <summary>등록 성공 여부. 다른 앱이 이미 쓰는 조합이면 false.</summary>
    public bool Register(string name, string combo)
    {
        Unregister(name);
        if (!TryParse(combo, out var mods, out var vk))
        {
            Log.Warning("단축키 {Name} 해석 실패: {Combo}", name, combo);
            return false;
        }
        var id = _nextId++;
        if (!Win32.RegisterHotKey(_source.Handle, id, mods | Win32.MOD_NOREPEAT, vk))
        {
            Log.Warning("단축키 {Name} 등록 실패: {Combo} (다른 앱이 쓰는 중일 수 있음)", name, combo);
            return false;
        }
        _ids[id] = name;
        return true;
    }

    public void Unregister(string name)
    {
        foreach (var (id, n) in _ids.Where(kv => kv.Value == name).ToList())
        {
            Win32.UnregisterHotKey(_source.Handle, id);
            _ids.Remove(id);
        }
    }

    public static bool TryParse(string combo, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(combo)) return false;
        Key key = Key.None;
        foreach (var raw in combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": modifiers |= Win32.MOD_CONTROL; break;
                case "shift": modifiers |= Win32.MOD_SHIFT; break;
                case "alt": modifiers |= Win32.MOD_ALT; break;
                case "win": modifiers |= Win32.MOD_WIN; break;
                default:
                    if (!Enum.TryParse(raw, ignoreCase: true, out key)) return false;
                    break;
            }
        }
        if (key == Key.None) return false;
        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        return vk != 0;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_HOTKEY && _ids.TryGetValue(wParam.ToInt32(), out var name))
        {
            Pressed?.Invoke(name);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _ids.Keys.ToList()) Win32.UnregisterHotKey(_source.Handle, id);
        _ids.Clear();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
