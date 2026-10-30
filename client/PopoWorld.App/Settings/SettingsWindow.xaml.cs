using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using PopoWorld.App.Overlay;
using PopoWorld.App.Services;
using PopoWorld.Core.Models;

namespace PopoWorld.App.Settings;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly string _currentName;

    public AppSettings? Result { get; private set; }
    public string? NewName { get; private set; }
    public bool LogoutRequested { get; private set; }

    public SettingsWindow(AppSettings settings, string currentName)
    {
        InitializeComponent();
        _settings = settings.Clone();
        _currentName = currentName;

        foreach (var m in Monitors.All())
            MonitorBox.Items.Add(new ComboBoxItem { Content = m.ToString(), Tag = m.Name });
        SelectByTag(MonitorBox, _settings.Monitor ?? Monitors.Resolve(null).Name);
        if (MonitorBox.SelectedIndex < 0 && MonitorBox.Items.Count > 0) MonitorBox.SelectedIndex = 0;

        SelectByTag(EdgeBox, _settings.Edge);
        SelectByTag(SpanBox, _settings.Span >= 0.75 ? "1" : _settings.Span >= 0.4 ? "0.5" : "0.333");

        ShowOfflineBox.IsChecked = _settings.ShowOffline;
        QuietModeBox.IsChecked = _settings.QuietMode;
        StartWithWindowsBox.IsChecked = _settings.StartWithWindows;
        SoundEnabledBox.IsChecked = _settings.Sound.Enabled;
        VolumeSlider.Value = _settings.Sound.Volume;
        ComposerHotkeyBox.Text = _settings.Hotkeys.Composer;
        ToggleHotkeyBox.Text = _settings.Hotkeys.ToggleWorld;
        NameBox.Text = currentName;
        LogPathText.Text = $"문제가 생기면 로그 폴더의 파일을 보내 주세요: {AppPaths.LogDirectory}";
    }

    private static void SelectByTag(ComboBox box, string tag)
    {
        foreach (var item in box.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var name = NameBox.Text.Trim();
        if (name.Length is < 1 or > PopoRules.NameMaxLength)
        {
            ShowError($"이름은 1~{PopoRules.NameMaxLength}자여야 해요.");
            return;
        }
        if (!HotkeyService.TryParse(ComposerHotkeyBox.Text, out _, out _) || !HotkeyService.TryParse(ToggleHotkeyBox.Text, out _, out _))
        {
            ShowError("단축키는 Ctrl+Shift+Space 처럼 적어 주세요.");
            return;
        }

        _settings.Monitor = (MonitorBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        _settings.Edge = (EdgeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "bottom";
        _settings.Span = double.Parse((SpanBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "0.5", System.Globalization.CultureInfo.InvariantCulture);
        _settings.ShowOffline = ShowOfflineBox.IsChecked == true;
        _settings.QuietMode = QuietModeBox.IsChecked == true;
        _settings.StartWithWindows = StartWithWindowsBox.IsChecked == true;
        _settings.Sound.Enabled = SoundEnabledBox.IsChecked == true;
        _settings.Sound.Volume = VolumeSlider.Value;
        _settings.Hotkeys.Composer = ComposerHotkeyBox.Text.Trim();
        _settings.Hotkeys.ToggleWorld = ToggleHotkeyBox.Text.Trim();
        _settings.Normalize();

        Result = _settings;
        NewName = name != _currentName ? name : null;
        DialogResult = true;
        Close();
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "로그아웃하면 다음 실행 때 다시 로그인해야 해요. 계속할까요?", "로그아웃", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;
        LogoutRequested = true;
        DialogResult = true;
        Close();
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.LogDirectory) { UseShellExecute = true }); }
        catch { /* 무시 */ }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
