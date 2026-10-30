using System.Windows;
using PopoWorld.Core.Models;
using PopoWorld.Core.Validation;

namespace PopoWorld.App.Store;

/// <summary>선물 보내기. amount ≥ 1, memo ≤ 20자, 하루 30포포.</summary>
public partial class GiftDialog : Window
{
    private readonly Guid _me;
    private readonly User _to;
    private readonly int _balance;
    private readonly int _sentToday;
    private readonly Func<Guid, int, string?, Task<RpcResult>> _send;

    public int Amount { get; private set; }
    public string? Memo { get; private set; }

    public GiftDialog(Guid me, User to, int balance, int sentToday, Func<Guid, int, string?, Task<RpcResult>> send)
    {
        InitializeComponent();
        _me = me;
        _to = to;
        _balance = balance;
        _sentToday = sentToday;
        _send = send;

        TitleText.Text = $"{to.Name}님에게 포포 보내기";
        InfoText.Text = $"내 잔액 {balance}포포 · 오늘 더 보낼 수 있는 양 {GiftValidator.RemainingToday(sentToday)}포포";
        AmountBox.Text = "1";
        Validate();
    }

    private int ParseAmount() => int.TryParse(AmountBox.Text.Trim(), out var n) ? n : 0;

    private void MinusButton_Click(object sender, RoutedEventArgs e) => AmountBox.Text = Math.Max(1, ParseAmount() - 1).ToString();
    private void PlusButton_Click(object sender, RoutedEventArgs e) => AmountBox.Text = (ParseAmount() + 1).ToString();
    private void AmountBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => Validate();

    private string? Validate()
    {
        var memo = MemoBox.Text.Trim();
        var error = GiftValidator.Validate(_me, _to.Id, ParseAmount(), memo, _balance, _sentToday);
        ErrorText.Text = error is null ? string.Empty : RpcResult.Fail(error).Describe();
        SendButton.IsEnabled = error is null;
        return error;
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (Validate() is not null) return;
        SendButton.IsEnabled = false;
        try
        {
            var amount = ParseAmount();
            var memo = MemoBox.Text.Trim();
            var r = await _send(_to.Id, amount, string.IsNullOrEmpty(memo) ? null : memo);
            if (r.Ok)
            {
                Amount = amount;
                Memo = memo;
                DialogResult = true;
                Close();
            }
            else
            {
                ErrorText.Text = r.Describe();
                SendButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = "보내지 못했어요: " + ex.Message;
            SendButton.IsEnabled = true;
        }
    }
}
