using Serilog;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;

namespace PopoWorld.App.Services;

/// <summary>로그인, 토큰 저장, Supabase 클라이언트 1개.</summary>
public sealed class SupabaseService
{
    private readonly AppConfig _config;
    private readonly SessionStore _sessionStore;

    public Supabase.Client Client { get; private set; } = null!;

    public Guid? UserId => Guid.TryParse(Client.Auth.CurrentUser?.Id, out var id) ? id : null;
    public string? AccessToken => Client.Auth.CurrentSession?.AccessToken;
    public bool IsSignedIn => UserId is not null && !string.IsNullOrEmpty(AccessToken);

    public SupabaseService(AppConfig config, SessionStore sessionStore)
    {
        _config = config;
        _sessionStore = sessionStore;
    }

    public async Task InitializeAsync()
    {
        var options = new Supabase.SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = false,
            SessionHandler = _sessionStore,
        };
        Client = new Supabase.Client(_config.SupabaseUrl, _config.SupabaseAnonKey, options);
        await Client.InitializeAsync();
    }

    /// <summary>session.bin 의 토큰으로 조용히 로그인한다. 실패하면 false.</summary>
    public async Task<bool> TryRestoreSessionAsync()
    {
        var saved = _sessionStore.LoadSession();
        if (saved is null) return false;
        try
        {
            var session = await Client.Auth.SetSession(saved.AccessToken!, saved.RefreshToken!, forceAccessTokenRefresh: true);
            if (session?.User is null)
            {
                Log.Information("저장된 세션이 만료돼 다시 로그인합니다");
                _sessionStore.DestroySession();
                return false;
            }
            Log.Information("세션 복원: {UserId}", session.User.Id);
            return true;
        }
        catch (GotrueException ex)
        {
            Log.Information("저장된 세션이 유효하지 않습니다 ({Reason})", ex.Reason);
            _sessionStore.DestroySession();
            return false;
        }
        catch (Exception ex)
        {
            // 네트워크 오류 등: 세션은 지우지 않고 이번엔 실패로 본다
            Log.Warning(ex, "세션 복원 중 오류");
            return false;
        }
    }

    /// <summary>이메일+비밀번호 로그인. 실패하면 사용자에게 보여줄 메시지를 던진다.</summary>
    public async Task SignInAsync(string email, string password)
    {
        try
        {
            var session = await Client.Auth.SignIn(email, password);
            if (session?.User is null)
                throw new InvalidOperationException("로그인에 실패했어요.");
            Log.Information("로그인: {UserId}", session.User.Id);
        }
        catch (GotrueException ex)
        {
            Log.Warning("로그인 거부: {Status} {Reason}", ex.StatusCode, ex.Reason);
            throw new InvalidOperationException(ex.StatusCode == 400
                ? "이메일 또는 비밀번호가 맞지 않아요."
                : "서버에 연결할 수 없어요. 잠시 후 다시 시도하세요.");
        }
    }

    public async Task SignOutAsync()
    {
        try { await Client.Auth.SignOut(); }
        catch (Exception ex) { Log.Warning(ex, "로그아웃 중 오류"); }
        _sessionStore.DestroySession();
    }
}
