namespace OpenShort.Api.Auth;

/// <summary>
/// Slows down repeated wrong codes of the authenticator app at login, with the delays of <see cref="LoginThrottle"/>.
/// Failures are counted on the account alone, whatever the client address: a code has six digits, and spreading
/// the attempts over many addresses must not help guessing it. Someone who knows the password can therefore
/// delay the codes of the account owner; recovery codes are counted elsewhere and keep working.
/// The counts are kept apart from the ones of the passwords: only a right password adds an entry here, one for
/// each account, so failed logins on made-up names cannot fill the table and stop the counting.
/// </summary>
public class TwoFactorCodeThrottle : LoginThrottle
{
    // Stands for the client address: the failures of every address are counted together
    private const string AnyClientAddress = "any";

    public TwoFactorCodeThrottle(TimeProvider timeProvider, ILogger<LoginThrottle> logger)
        : base(timeProvider, logger)
    {
    }

    /// <summary>
    /// Runs the check of a code for the given account. The code is verified only if no delay is pending.
    /// </summary>
    public Task<LoginAttemptResult> AttemptAsync(
        string userId,
        Func<Task<bool>> verifyCode,
        CancellationToken cancellationToken = default)
    {
        return AttemptAsync(userId, AnyClientAddress, verifyCode, cancellationToken);
    }
}
