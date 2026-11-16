using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace OpenShort.Api.Auth;

/// <summary>
/// Slows down repeated failed logins with a delay that grows at every consecutive failure.
/// The delay applies to the pair of login subject and client address, so an attacker
/// slows down only their own attempts and cannot keep the account owner out.
/// The subject is chosen by the caller: the account when the identifier matches one,
/// so that every spelling of the same account shares one count.
/// State is kept in memory: it is lost when the application restarts.
/// </summary>
public class LoginThrottle
{
    public const int FailuresWithoutDelay = 2;
    public const int MaxTrackedClients = 10_000;

    public static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan ForgetAfter = TimeSpan.FromMinutes(15);

    // Failures that already caused a delay are remembered for longer,
    // otherwise waiting out the idle period would be faster than waiting out the delays
    public static readonly TimeSpan ForgetDelayedAfter = TimeSpan.FromHours(24);

    private const int GateCount = 64;
    private const int MaxDelayDoublings = 30;

    private readonly ConcurrentDictionary<string, Entry> _entries = new();
    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, GateCount).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LoginThrottle> _logger;

    public LoginThrottle(TimeProvider timeProvider, ILogger<LoginThrottle> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Returns the delay applied after the given number of consecutive failures.
    /// </summary>
    public static TimeSpan GetDelay(int failures)
    {
        var doublings = failures - FailuresWithoutDelay - 1;
        if (doublings < 0)
        {
            return TimeSpan.Zero;
        }

        if (doublings > MaxDelayDoublings)
        {
            return MaxDelay;
        }

        var delay = InitialDelay * (1L << doublings);
        return delay < MaxDelay ? delay : MaxDelay;
    }

    /// <summary>
    /// Runs a login attempt. Credentials are verified only if no delay is pending,
    /// and attempts of the same client on the same subject never run in parallel.
    /// </summary>
    public async Task<LoginAttemptResult> AttemptAsync(
        string subject,
        string clientAddress,
        Func<Task<bool>> verifyCredentials,
        CancellationToken cancellationToken = default)
    {
        var key = CreateKey(subject, clientAddress);
        var gate = _gates[(key.GetHashCode() & int.MaxValue) % GateCount];

        await gate.WaitAsync(cancellationToken);
        try
        {
            var now = _timeProvider.GetUtcNow();
            var entry = GetEntry(key, now);
            if (entry != null && entry.BlockedUntil > now)
            {
                return LoginAttemptResult.Delayed(entry.BlockedUntil - now);
            }

            if (await verifyCredentials())
            {
                _entries.TryRemove(key, out _);
                return LoginAttemptResult.Success;
            }

            RegisterFailure(key, entry, _timeProvider.GetUtcNow());
            return LoginAttemptResult.Failure;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Reset(string subject, string clientAddress)
    {
        _entries.TryRemove(CreateKey(subject, clientAddress), out _);
    }

    private Entry? GetEntry(string key, DateTimeOffset now)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (entry.ForgetAt > now)
        {
            return entry;
        }

        _entries.TryRemove(key, out _);
        return null;
    }

    private void RegisterFailure(string key, Entry? entry, DateTimeOffset now)
    {
        if (entry == null && _entries.Count >= MaxTrackedClients)
        {
            RemoveExpiredEntries(now);
            if (_entries.Count >= MaxTrackedClients)
            {
                _logger.LogWarning("Login throttle is tracking the maximum number of clients: a failed login was not recorded");
                return;
            }
        }

        var failures = (entry?.Failures ?? 0) + 1;
        var blockedUntil = now + GetDelay(failures);
        var forgetAfter = failures > FailuresWithoutDelay ? ForgetDelayedAfter : ForgetAfter;
        _entries[key] = new Entry(failures, blockedUntil, blockedUntil + forgetAfter);
    }

    private void RemoveExpiredEntries(DateTimeOffset now)
    {
        foreach (var (key, entry) in _entries)
        {
            if (entry.ForgetAt <= now)
            {
                _entries.TryRemove(key, out _);
            }
        }
    }

    // Hashed so that the size of a tracked entry does not depend on the submitted identifier
    private static string CreateKey(string subject, string clientAddress)
    {
        var value = $"{subject.Trim().ToUpperInvariant()}\n{clientAddress}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private sealed record Entry(int Failures, DateTimeOffset BlockedUntil, DateTimeOffset ForgetAt);
}

/// <summary>
/// Outcome of a login attempt. When <see cref="RetryAfter"/> is set the credentials were not verified.
/// </summary>
public readonly record struct LoginAttemptResult(bool Succeeded, TimeSpan? RetryAfter)
{
    public static readonly LoginAttemptResult Success = new(true, null);
    public static readonly LoginAttemptResult Failure = new(false, null);

    public static LoginAttemptResult Delayed(TimeSpan retryAfter) => new(false, retryAfter);
}
