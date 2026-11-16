using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenShort.Api.Auth;

namespace OpenShort.Tests.Api;

/// <summary>
/// Runs the forwarded headers configuration on an in-memory server with a minimal pipeline.
/// </summary>
[TestFixture]
public class TrustedProxiesTests
{
    private const string AddressPath = "/address";
    private const string LimitedPath = "/limited";
    private const string PeerHeader = "X-Test-Peer";
    private const string ForwardedForHeader = "X-Forwarded-For";
    private const string Loopback = "127.0.0.1";
    private const string PublicPeer = "203.0.113.10";
    private const string Client = "198.51.100.20";
    private const string ExternalProxy = "10.0.0.5";

    private WebApplication? _app;
    private HttpClient? _client;

    [TearDown]
    public async Task TearDown()
    {
        _client?.Dispose();
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }

    [Test]
    public async Task ClientAddress_ShouldBeThePeer_WhenTheRequestDoesNotComeFromATrustedProxy()
    {
        // Arrange
        await StartAsync(trustedProxies: null);

        // Act
        var address = await GetAddressAsync(peer: PublicPeer, forwardedFor: Client);

        // Assert
        address.Should().Be(PublicPeer);
    }

    [TestCase(Loopback)]
    [TestCase("::1")]
    [TestCase("::ffff:127.0.0.1")]
    public async Task ClientAddress_ShouldBeTheForwardedOne_WhenTheRequestComesFromTheBundledProxy(string peer)
    {
        // Arrange
        await StartAsync(trustedProxies: null);

        // Act
        var address = await GetAddressAsync(peer, forwardedFor: Client);

        // Assert
        address.Should().Be(Client);
    }

    [Test]
    public async Task ClientAddress_ShouldBeTheExternalProxy_WhenItIsNotDeclared()
    {
        // Arrange
        await StartAsync(trustedProxies: null);

        // Act
        var address = await GetAddressAsync(peer: Loopback, forwardedFor: $"{Client}, {ExternalProxy}");

        // Assert
        address.Should().Be(ExternalProxy);
    }

    [TestCase(ExternalProxy)]
    [TestCase("10.0.0.0/8")]
    [TestCase("192.168.1.1, 10.0.0.5")]
    public async Task ClientAddress_ShouldBeTheRealClient_BehindADeclaredProxyAndTheBundledOne(string trustedProxies)
    {
        // Arrange
        await StartAsync(trustedProxies);

        // Act
        var address = await GetAddressAsync(peer: Loopback, forwardedFor: $"{Client}, {ExternalProxy}");

        // Assert
        address.Should().Be(Client);
    }

    [Test]
    public async Task ClientAddress_ShouldBeTheRealClient_WhenADeclaredProxyCallsTheApiDirectly()
    {
        // Arrange
        await StartAsync(trustedProxies: ExternalProxy);

        // Act
        var address = await GetAddressAsync(peer: ExternalProxy, forwardedFor: Client);

        // Assert
        address.Should().Be(Client);
    }

    [Test]
    public async Task ClientAddress_ShouldIgnoreAddressesWrittenByTheClient_BehindTrustedProxies()
    {
        // Arrange
        await StartAsync(trustedProxies: ExternalProxy);

        // Act
        var address = await GetAddressAsync(peer: Loopback, forwardedFor: $"192.0.2.99, {Client}, {ExternalProxy}");

        // Assert
        address.Should().Be(Client);
    }

    [Test]
    public async Task RateLimit_ShouldCountThePeer_WhenForwardedAddressesChangeAtEveryRequest()
    {
        // Arrange
        await StartAsync(trustedProxies: null);

        // Act
        var statusCodes = new List<HttpStatusCode>();
        for (var request = 0; request <= AuthProtection.RateLimitPermitsPerWindow; request++)
        {
            using var response = await SendAsync(HttpMethod.Post, LimitedPath, PublicPeer, forwardedFor: $"192.0.2.{request}");
            statusCodes.Add(response.StatusCode);
        }

        // Assert
        statusCodes.Take(AuthProtection.RateLimitPermitsPerWindow).Should().AllSatisfy(
            statusCode => statusCode.Should().Be(HttpStatusCode.OK));
        statusCodes.Last().Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task RateLimit_ShouldCountEachClient_BehindTheBundledProxy()
    {
        // Arrange
        await StartAsync(trustedProxies: null);

        // Act
        var statusCodes = new List<HttpStatusCode>();
        for (var request = 0; request <= AuthProtection.RateLimitPermitsPerWindow; request++)
        {
            using var response = await SendAsync(HttpMethod.Post, LimitedPath, Loopback, forwardedFor: $"192.0.2.{request}");
            statusCodes.Add(response.StatusCode);
        }

        // Assert
        statusCodes.Should().AllSatisfy(statusCode => statusCode.Should().Be(HttpStatusCode.OK));
    }

    [TestCase("not-an-address")]
    [TestCase("10.0.0.0/99")]
    [TestCase("10.0.0.5, proxy.example.com")]
    public void Configure_ShouldThrow_WhenAnEntryIsNotAnAddressOrANetwork(string trustedProxies)
    {
        // Act
        var act = () => TrustedProxies.Configure(new ForwardedHeadersOptions(), trustedProxies);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"*{TrustedProxies.ConfigurationKey}*");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void Configure_ShouldTrustOnlyLoopback_WhenNothingIsDeclared(string? trustedProxies)
    {
        // Act
        var options = new ForwardedHeadersOptions();
        TrustedProxies.Configure(options, trustedProxies);

        // Assert
        options.KnownProxies.Should().Equal(IPAddress.IPv6Loopback);
        options.KnownNetworks.Should().ContainSingle()
            .Which.Contains(IPAddress.Parse("127.0.0.1")).Should().BeTrue();
        options.KnownNetworks.Single().Contains(IPAddress.Parse(ExternalProxy)).Should().BeFalse();
    }

    private async Task StartAsync(string? trustedProxies)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.Configure<ForwardedHeadersOptions>(options => TrustedProxies.Configure(options, trustedProxies));
        builder.Services.AddRateLimiter(AuthProtection.ConfigureRateLimiter);

        _app = builder.Build();
        _app.Use((httpContext, next) =>
        {
            // The test server has no connection: tests choose the peer address with a header
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(httpContext.Request.Headers[PeerHeader].ToString());
            return next(httpContext);
        });
        _app.UseForwardedHeaders();
        _app.UseRateLimiter();
        _app.MapGet(AddressPath, (HttpContext httpContext) => httpContext.Connection.RemoteIpAddress!.ToString());
        _app.MapPost(LimitedPath, () => Results.Ok()).RequireRateLimiting(AuthProtection.RateLimitPolicy);

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    private async Task<string> GetAddressAsync(string peer, string forwardedFor)
    {
        using var response = await SendAsync(HttpMethod.Get, AddressPath, peer, forwardedFor);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string peer, string forwardedFor)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add(PeerHeader, peer);
        request.Headers.Add(ForwardedForHeader, forwardedFor);
        return await _client!.SendAsync(request);
    }
}
