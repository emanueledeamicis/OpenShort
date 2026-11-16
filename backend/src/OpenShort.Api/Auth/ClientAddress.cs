using System.Net;
using System.Net.Sockets;

namespace OpenShort.Api.Auth;

/// <summary>
/// Identifies the origin of a request for the brute-force protections.
/// </summary>
public static class ClientAddress
{
    private const string UnknownClient = "unknown";
    private const int Ipv6PrefixBytes = 8;

    public static string GetPartitionKey(HttpContext httpContext)
    {
        return GetPartitionKey(httpContext.Connection.RemoteIpAddress);
    }

    public static string GetPartitionKey(IPAddress? address)
    {
        if (address == null)
        {
            return UnknownClient;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        // A single IPv6 host usually controls a whole /64: count it as one client
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, Ipv6PrefixBytes, bytes.Length - Ipv6PrefixBytes);
        return $"{new IPAddress(bytes)}/64";
    }
}
