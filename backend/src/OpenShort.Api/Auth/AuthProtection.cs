using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace OpenShort.Api.Auth;

/// <summary>
/// Brute-force protections for the authentication endpoints:
/// password policy, account lockout and request rate limiting.
/// </summary>
public static class AuthProtection
{
    public const string RateLimitPolicy = "auth";
    public const int RateLimitPermitsPerWindow = 10;
    public const int MaxFailedAccessAttempts = 5;
    public const int RequiredPasswordLength = 8;

    public static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private const string UnknownClientPartition = "unknown";
    private const string RateLimitExceededMessage = "Too many attempts. Please try again later.";

    public static void ConfigureIdentity(IdentityOptions options)
    {
        options.Password.RequiredLength = RequiredPasswordLength;
        options.Password.RequiredUniqueChars = 1;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = MaxFailedAccessAttempts;
        options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;
    }

    public static void ConfigureRateLimiter(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // One window per client address
        options.AddPolicy(RateLimitPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownClientPartition,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = RateLimitPermitsPerWindow,
                Window = RateLimitWindow,
                QueueLimit = 0
            }));

        options.OnRejected = async (context, cancellationToken) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            await context.HttpContext.Response.WriteAsJsonAsync(new { message = RateLimitExceededMessage }, cancellationToken);
        };
    }
}
