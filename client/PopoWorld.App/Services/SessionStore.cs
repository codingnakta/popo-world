using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Serilog;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace PopoWorld.App.Services;

/// <summary>
/// 인증 토큰을 %APPDATA%\PopoWorld\session.bin 에 DPAPI(CurrentUser)로 암호화해 둔다.
/// settings.json 에는 절대 넣지 않는다.
/// </summary>
public sealed class SessionStore : IGotrueSessionPersistence<Session>
{
    private readonly string _path;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PopoWorld.session.v1");

    public SessionStore(string? path = null)
    {
        _path = path ?? AppPaths.SessionFile;
    }

    private sealed class Stored
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public long ExpiresIn { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? UserJson { get; set; }
    }

    public void SaveSession(Session session)
    {
        try
        {
            if (string.IsNullOrEmpty(session.AccessToken) || string.IsNullOrEmpty(session.RefreshToken))
                return;

            var stored = new Stored
            {
                AccessToken = session.AccessToken!,
                RefreshToken = session.RefreshToken!,
                ExpiresIn = session.ExpiresIn,
                CreatedAt = session.CreatedAt,
                UserJson = session.User is null ? null : JsonSerializer.Serialize(session.User),
            };
            var plain = JsonSerializer.SerializeToUtf8Bytes(stored);
            var cipher = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tmp = _path + ".tmp";
            File.WriteAllBytes(tmp, cipher);
            File.Move(tmp, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "세션 저장 실패");
        }
    }

    public Session? LoadSession()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var cipher = File.ReadAllBytes(_path);
            var plain = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
            var stored = JsonSerializer.Deserialize<Stored>(plain);
            if (stored is null || string.IsNullOrEmpty(stored.RefreshToken)) return null;
            return new Session
            {
                AccessToken = stored.AccessToken,
                RefreshToken = stored.RefreshToken,
                ExpiresIn = stored.ExpiresIn,
                CreatedAt = stored.CreatedAt,
                TokenType = "bearer",
                User = stored.UserJson is null ? null : JsonSerializer.Deserialize<User>(stored.UserJson),
            };
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "세션 복원 실패. 다시 로그인합니다");
            DestroySession();
            return null;
        }
    }

    public void DestroySession()
    {
        try { if (File.Exists(_path)) File.Delete(_path); }
        catch (Exception ex) { Log.Warning(ex, "세션 삭제 실패"); }
    }

    public Task SaveSessionAsync(Session session, CancellationToken cancellationToken = default)
    {
        SaveSession(session);
        return Task.CompletedTask;
    }

    public Task DestroySessionAsync(CancellationToken cancellationToken = default)
    {
        DestroySession();
        return Task.CompletedTask;
    }

    public Task<Session?> LoadSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult(LoadSession());
}
