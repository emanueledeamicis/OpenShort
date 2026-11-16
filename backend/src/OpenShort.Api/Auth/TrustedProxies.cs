using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace OpenShort.Api.Auth;

/// <summary>
/// Decides which peers are allowed to set the client address through the forwarded headers.
/// Only the proxy bundled in the container (loopback) is trusted by default: any other
/// reverse proxy must be declared, otherwise a client could choose its own address.
/// </summary>
public static class TrustedProxies
{
    public const string ConfigurationKey = "TRUSTED_PROXIES";

    private static readonly char[] EntrySeparators = [',', ';', ' '];

    /// <summary>
    /// Configures the forwarded headers. <paramref name="trustedProxies"/> is a list of
    /// IP addresses or CIDR networks, added to the loopback addresses trusted by default.
    /// </summary>
    public static void Configure(ForwardedHeadersOptions options, string? trustedProxies)
    {
        options.ForwardedHeaders = ForwardedHeaders.All;

        // No fixed number of hops: the chain is followed for as long as every hop is a trusted proxy,
        // so the client address is the first one that was not written by a proxy we know
        options.ForwardLimit = null;

        foreach (var entry in ParseEntries(trustedProxies))
        {
            if (entry.Contains('/') && Microsoft.AspNetCore.HttpOverrides.IPNetwork.TryParse(entry, out var network))
            {
                options.KnownNetworks.Add(network);
            }
            else if (!entry.Contains('/') && IPAddress.TryParse(entry, out var address))
            {
                options.KnownProxies.Add(address);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Invalid {ConfigurationKey} entry '{entry}': expected an IP address or a CIDR network, for example 10.0.0.5 or 172.16.0.0/12.");
            }
        }
    }

    public static IReadOnlyList<string> ParseEntries(string? trustedProxies)
    {
        return (trustedProxies ?? string.Empty)
            .Split(EntrySeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
