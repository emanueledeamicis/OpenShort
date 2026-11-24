using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace OpenShort.Api.Auth;

/// <summary>
/// Remembers the authenticator codes that already opened a session, so that a code seen by someone else
/// (over the shoulder, on a shared screen) cannot be used a second time while it is still valid.
/// State is kept in memory: it is lost when the application restarts.
/// </summary>
public class UsedTwoFactorCodes
{
    // Longer than the two and a half minutes for which ASP.NET Core Identity accepts a code
    public static readonly TimeSpan RememberFor = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _forgetAt = new();
    private readonly TimeProvider _timeProvider;

    public UsedTwoFactorCodes(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Marks a valid code as used. Returns false when the same user already used it.
    /// </summary>
    public bool TryUse(string userId, string code)
    {
        var now = _timeProvider.GetUtcNow();
        RemoveExpiredEntries(now);

        return _forgetAt.TryAdd(CreateKey(userId, code), now + RememberFor);
    }

    // Entries are added by successful logins only, so there are never many of them
    private void RemoveExpiredEntries(DateTimeOffset now)
    {
        foreach (var (key, forgetAt) in _forgetAt)
        {
            if (forgetAt <= now)
            {
                _forgetAt.TryRemove(key, out _);
            }
        }
    }

    // Hashed so that the codes themselves are not kept in memory
    private static string CreateKey(string userId, string code)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}\n{code}")));
    }
}
