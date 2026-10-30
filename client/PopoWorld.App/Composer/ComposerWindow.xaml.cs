using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PopoWorld.Core.Models;
using PopoWorld.Core.Validation;

namespace PopoWorld.App.Composer;

/// <summary>메시지 입력 창. 201자는 버튼이 꺼지고, 4줄은 보낼 수 없다.</summary>
public partial class ComposerWindow : Window
{
    private readonly Func<string, Task<bool>> _send;
    private readonly Action _typing;
    private readonly Action _closed;
    private bool _sending;

    public ComposerWindow(Func<string, Task<bool>> send, Action typing, Action closed)
    {
        InitializeComponent();
        _send = send;
        _typing = typing;
        _closed = closed;
        UpdateState();
        Closing += (_, e) =>
        {
            // 닫기 대신 숨겨서 위치와 입력 중이던 글을 유지한다
            e.Cancel = true;
            Hide();
            _closed();
        };
        Deactivated += (_, _) => { if (BodyBox.Text.Length == 0 && IsVisible) { Hide(); _closed(); } };
    }

    public void ShowAndFocus()
    {
        Show();
        Activate();
        BodyBox.Focus();
        BodyBox.CaretIndex = BodyBox.Text.Length;
    }

    public void Toggle()
    {
        if (IsVisible) { Hide(); _closed(); }
        else ShowAndFocus();
    }

    private void BodyBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateState();
        if (BodyBox.Text.Length > 0) _typing();
    }

    private void UpdateState()
    {
        var text = BodyBox.Text;
        var error = MessageValidator.Validate(text);
        var count = MessageValidator.CountChars(MessageValidator.Normalize(text));
        CounterText.Text = $"{count}/{PopoRules.MessageMaxLength}";
        CounterText.Foreground = error is MessageError.TooLong or MessageError.TooManyLines ? Brushes.Crimson : Brushes.Gray;
        SendButton.IsEnabled = error == MessageError.None && !_sending;
        HintText.Text = error is MessageError.None or MessageError.Empty ? "Enter 보내기 · Shift+Enter 줄바꿈" : MessageValidator.Describe(error);
        HintText.Foreground = error is MessageError.None or MessageError.Empty ? Brushes.Gray : Brushes.Crimson;
    }

    private void BodyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            _closed();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            _ = SendAsync();
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e) => _ = SendAsync();

    private async Task SendAsync()
    {
        if (_sending) return;
        var body = MessageValidator.Normalize(BodyBox.Text);
        if (!MessageValidator.IsValid(body)) return;

        _sending = true;
        UpdateState();
        try
        {
            if (await _send(body))
            {
                BodyBox.Clear();
                Hide();
                _closed();
            }
            else
            {
                HintText.Text = "보내지 못했어요. 다시 시도하세요.";
                HintText.Foreground = Brushes.Crimson;
            }
        }
        finally
        {
            _sending = false;
            UpdateState();
        }
    }
}
