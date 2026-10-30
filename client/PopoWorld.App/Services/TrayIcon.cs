using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Drawing = System.Drawing;

namespace PopoWorld.App.Services;

/// <summary>트레이 아이콘 + 메뉴. 아이콘 색: 초록 연결됨, 회색 끊김, 파랑 조용히 모드.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly TaskbarIcon _icon = new();
    private readonly MenuItem _hideItem = new() { Header = "숨기기" };
    private readonly Dictionary<string, Drawing.Icon> _icons = new();

    public event Action? ToggleHiddenRequested;
    public event Action? StoreRequested;
    public event Action? SettingsRequested;
    public event Action? ComposerRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        var menu = new ContextMenu();
        var composer = new MenuItem { Header = "말하기" };
        composer.Click += (_, _) => ComposerRequested?.Invoke();
        var store = new MenuItem { Header = "상점" };
        store.Click += (_, _) => StoreRequested?.Invoke();
        var settings = new MenuItem { Header = "설정" };
        settings.Click += (_, _) => SettingsRequested?.Invoke();
        var exit = new MenuItem { Header = "종료" };
        exit.Click += (_, _) => ExitRequested?.Invoke();
        _hideItem.Click += (_, _) => ToggleHiddenRequested?.Invoke();

        menu.Items.Add(composer);
        menu.Items.Add(store);
        menu.Items.Add(_hideItem);
        menu.Items.Add(settings);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);

        _icon.ContextMenu = menu;
        _icon.ToolTipText = "포포월드";
        _icon.TrayLeftMouseUp += (_, _) => ComposerRequested?.Invoke();
        _icon.TrayMouseDoubleClick += (_, _) => StoreRequested?.Invoke();
        SetState(connected: false, quiet: false);
    }

    public void SetTooltip(string text) => _icon.ToolTipText = text;

    public void SetHidden(bool hidden) => _hideItem.Header = hidden ? "보이기" : "숨기기";

    public void SetState(bool connected, bool quiet)
    {
        var key = quiet ? "quiet" : connected ? "on" : "off";
        if (!_icons.TryGetValue(key, out var icon))
        {
            icon = Draw(quiet ? Drawing.Color.SteelBlue : connected ? Drawing.Color.MediumSeaGreen : Drawing.Color.Gray);
            _icons[key] = icon;
        }
        _icon.Icon = icon;
    }

    private static Drawing.Icon Draw(Drawing.Color color)
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);
            using var fill = new Drawing.SolidBrush(color);
            g.FillEllipse(fill, 3, 3, 26, 26);
            using var eye = new Drawing.SolidBrush(Drawing.Color.White);
            g.FillEllipse(eye, 9, 11, 5, 5);
            g.FillEllipse(eye, 18, 11, 5, 5);
            using var pupil = new Drawing.SolidBrush(Drawing.Color.Black);
            g.FillEllipse(pupil, 11, 13, 2, 2);
            g.FillEllipse(pupil, 20, 13, 2, 2);
        }
        var h = bmp.GetHicon();
        return (Drawing.Icon)Drawing.Icon.FromHandle(h).Clone();
    }

    public void Dispose()
    {
        _icon.Dispose();
        foreach (var i in _icons.Values) i.Dispose();
    }
}
