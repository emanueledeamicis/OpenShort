using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.HttpOverrides;
using ProxyNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace OpenShort.Api.Auth;

/// <summary>
/// Decides which peers are allowed to set the client address through the forwarded headers.
/// Only the proxy bundled in the container (loopback) is trusted by default: any other
/// reverse proxy must be declared, otherwise a client could choose its own address.
/// </summary>
public sealed class TrustedProxies
{
    public const string ConfigurationKey = "TRUSTED_PROXIES";

    // Untrusted peers sending forwarded headers are reported once each, up to this number,
    // so that the log tells which address to declare without being flooded
    private const int MaxReportedPeers = 20;

    private const string ForwardedForHeader = "X-Forwarded-For";

    private TrustedProxies(IReadOnlyList<string> entries, IReadOnlyList<IPAddress> addresses, IReadOnlyList<ProxyNetwork> networks)
    {
        Entries = entries;
        Addresses = addresses;
        Networks = networks;
    }

    /// <summary>The declared entries, as written in the configuration.</summary>
    public IReadOnlyList<string> Entries { get; }

    public IReadOnlyList<IPAddress> Addresses { get; }

    public IReadOnlyList<ProxyNetwork> Networks { get; }

    /// <summary>True when a declared network covers every address, which disables the protection.</summary>
    public bool TrustsEveryAddress => Networks.Any(network => network.PrefixLength == 0);

    /// <summary>
    /// Reads a comma-separated list of IP addresses and CIDR networks.
    /// Throws when an entry is not valid: a typo must not silently leave a proxy untrusted.
    /// </summary>
    public static TrustedProxies Parse(string? value)
    {
        var entries = (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var addresses = new List<IPAddress>();
        var networks = new List<ProxyNetwork>();

        foreach (var entry in entries)
        {
            var separatorIndex = entry.IndexOf('/');
            if (separatorIndex < 0 && TryParseAddress(entry, out var address))
            {
                addresses.Add(address);
            }
            else if (separatorIndex >= 0
                && TryParseAddress(entry[..separatorIndex], out _)
                && ProxyNetwork.TryParse(entry, out var network))
            {
                networks.Add(network);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Invalid {ConfigurationKey} entry '{entry}': expected an IP address or a CIDR network, for example 10.0.0.5 or 10.0.0.0/24.");
            }
        }

        return new TrustedProxies(entries, addresses, networks);
    }

    /// <summary>
    /// Configures the forwarded headers so that they are accepted from loopback, trusted by default,
    /// and from the declared proxies.
    /// </summary>
    public void Configure(ForwardedHeadersOptions options)
    {
        // The path prefix header is left out: nothing in the application uses it
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

        // No fixed number of hops: the chain is followed for as long as every hop is a trusted proxy,
        // so the client address is the first one that was not written by a proxy we know
        options.ForwardLimit = null;

        foreach (var address in Addresses)
        {
            options.KnownProxies.Add(address);
        }

        foreach (var network in Networks)
        {
            options.KnownNetworks.Add(network);
        }
    }

    // IPAddress.TryParse accepts shortened IPv4 forms ("172.17" is read as 172.0.0.17): only the full form is allowed
    private static bool TryParseAddress(string text, out IPAddress address)
    {
        if (!IPAddress.TryParse(text, out var parsed)
            || (parsed.AddressFamily == AddressFamily.InterNetwork && parsed.ToString() != text))
        {
            address = IPAddress.None;
            return false;
        }

        address = parsed;
        return true;
    }

    /// <summary>
    /// Applies the forwarded headers and reports the peers whose headers were ignored.
    /// </summary>
    public static IApplicationBuilder Use(IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger<TrustedProxies>();
        var reportedPeers = new ConcurrentDictionary<string, bool>();

        app.UseForwardedHeaders();

        return app.Use((httpContext, next) =>
        {
            // Entries still in the header were not accepted: they come from a peer that is not a trusted proxy
            if (httpContext.Request.Headers.ContainsKey(ForwardedForHeader)
                && reportedPeers.Count < MaxReportedPeers
                && reportedPeers.TryAdd(ClientAddress.GetPartitionKey(httpContext), true))
            {
                logger.LogWarning(
                    "Ignored forwarded headers received from {Address}, which is not a trusted proxy. If it is your reverse proxy, add its address to {ConfigurationKey}",
                    httpContext.Connection.RemoteIpAddress,
                    ConfigurationKey);
            }

            return next(httpContext);
        });
    }
}
