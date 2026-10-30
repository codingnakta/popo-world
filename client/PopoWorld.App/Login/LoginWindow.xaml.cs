using System.Windows;
using System.Windows.Input;
using PopoWorld.App.Services;

namespace PopoWorld.App.Login;

public partial class LoginWindow : Window
{
    private readonly SupabaseService _supabase;

    public LoginWindow(SupabaseService supabase)
    {
        InitializeComponent();
        _supabase = supabase;
        Loaded += (_, _) => EmailBox.Focus();
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) _ = TryLoginAsync();
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e) => _ = TryLoginAsync();

    private async Task TryLoginAsync()
    {
        var email = EmailBox.Text.Trim();
        var password = PasswordBox.Password;
        if (email.Length == 0 || password.Length == 0)
        {
            ShowError("이메일과 비밀번호를 입력하세요.");
            return;
        }

        LoginButton.IsEnabled = false;
        ErrorText.Visibility = Visibility.Collapsed;
        try
        {
            await _supabase.SignInAsync(email, password);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
