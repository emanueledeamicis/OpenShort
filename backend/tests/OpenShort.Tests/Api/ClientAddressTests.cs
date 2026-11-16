using System.Net;
using FluentAssertions;
using OpenShort.Api.Auth;

namespace OpenShort.Tests.Api;

[TestFixture]
public class ClientAddressTests
{
    [Test]
    public void GetPartitionKey_ShouldReturnUnknown_WhenAddressIsMissing()
    {
        // Act
        var key = ClientAddress.GetPartitionKey((IPAddress?)null);

        // Assert
        key.Should().Be("unknown");
    }

    [TestCase("203.0.113.10", "203.0.113.10")]
    [TestCase("::ffff:203.0.113.10", "203.0.113.10")]
    [TestCase("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    [TestCase("2001:db8:1:2::1", "2001:db8:1:2::/64")]
    [TestCase("2001:db8:1:3::1", "2001:db8:1:3::/64")]
    [TestCase("::1", "::/64")]
    public void GetPartitionKey_ShouldGroupIpv6AddressesByPrefix(string address, string expectedKey)
    {
        // Act
        var key = ClientAddress.GetPartitionKey(IPAddress.Parse(address));

        // Assert
        key.Should().Be(expectedKey);
    }
}
