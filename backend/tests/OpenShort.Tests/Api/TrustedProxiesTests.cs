using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenShort.Api.Auth;

namespace OpenShort.Tests.Api;

/// <summary>
/// Runs the forwarded headers configuration on an in-memory server with a minimal pipeline.
/// </summary>
[TestFixture]
public class TrustedProxiesTests
{
    private const string AddressPath = "/address";
    private const string RequestPath = "/request";
    private const string LimitedPath = "/limited";
    private const string PeerHeader = "X-Test-Peer";
    private const string ForwardedForHeader = "X-Forwarded-For";
    private const string Loopback = "127.0.0.1";
    private const string PublicPeer = "203.0.113.10";
    private const string Client = "198.51.100.20";
    private const string ExternalProxy = "10.0.0.5";

    private WebApplication? _app;
    private HttpClient? _client;
    private RecordingLoggerProvider _logs = null!;

    [SetUp]
    public void Setup()
    {
        _logs = new RecordingLoggerProvider();
    }

    [TearDown]
    public async Task TearDown()
    {
        _client?.Dispose();
        if (_app != null)
        {
            await _app.DisposeAsync();
        }

        _logs.Dispose();
    }

    [TestCase(PublicPeer)]
    [TestCase("::ffff:203.0.113.10")]
    [TestCase("2001:db8::10")]
    public async Task ClientAddress_ShouldBeThePeer_WhenTheRequestDoesNotComeFromATrustedProxy(string peer)
    {
        // Arrange
        await StartAsync(trustedProxies: null);

        // Act
        var address = await GetAddressAsync(peer, forwardedFor: Client);

        // Assert
        address.Should().Be(peer);
    }

    [Test]
    public async Task ClientAddress_ShouldBeThePeer_WhenAnUntrustedPeerClaimsToBeLoopback()
    {
        // Arrange
        await StartAsync(trustedProxies: null);

        // Act
        var address = await GetAddressAsync(peer: PublicPeer, forwardedFor: Loopback);

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
    [TestCase("10.0.0.1/8")]
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

    [TestCase(ExternalProxy, ExternalProxy)]
    [TestCase(ExternalProxy, "::ffff:10.0.0.5")]
    [TestCase("fd00:1::/64", "fd00:1::5")]
    public async Task ClientAddress_ShouldBeTheRealClient_WhenADeclaredProxyCallsTheApiDirectly(string trustedProxies, string peer)
    {
        // Arrange
        await StartAsync(trustedProxies);

        // Act
        var address = await GetAddressAsync(peer, forwardedFor: Client);

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
    public async Task HostAndScheme_ShouldBeIgnored_WhenTheRequestDoesNotComeFromATrustedProxy()
    {
        // Arrange
        await StartAsync(trustedProxies: null);

        // Act
        var request = await GetRequestAsync(peer: PublicPeer);

        // Assert
        request.Should().Be("http://localhost");
    }

    [Test]
    public async Task HostAndScheme_ShouldBeTheForwardedOnes_WhenTheRequestComesFromATrustedProxy()
    {
        // Arrange
        await StartAsync(trustedProxies: null);

        // Act
        var request = await GetRequestAsync(peer: Loopback);

        // Assert
        request.Should().Be("https://short.example");
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

    [Test]
    public async Task Warning_ShouldBeLoggedOncePerPeer_WhenForwardedHeadersAreIgnored()
    {
        // Arrange
        await StartAsync(trustedProxies: null);

        // Act
        await GetAddressAsync(peer: PublicPeer, forwardedFor: Client);
        await GetAddressAsync(peer: PublicPeer, forwardedFor: Client);
        await GetAddressAsync(peer: Loopback, forwardedFor: $"{Client}, {ExternalProxy}");

        // Assert
        _logs.Warnings.Should().HaveCount(2);
        _logs.Warnings[0].Should().Contain(PublicPeer).And.Contain(TrustedProxies.ConfigurationKey);
        _logs.Warnings[1].Should().Contain(ExternalProxy);
    }

    [Test]
    public async Task Warning_ShouldNotBeLogged_WhenEveryForwardedAddressIsAccepted()
    {
        // Arrange
        await StartAsync(trustedProxies: ExternalProxy);

        // Act
        await GetAddressAsync(peer: Loopback, forwardedFor: $"{Client}, {ExternalProxy}");

        // Assert
        _logs.Warnings.Should().BeEmpty();
    }

    [TestCase("not-an-address")]
    [TestCase("proxy.example.com")]
    [TestCase("10.0.0.0/99")]
    [TestCase("10.0.0.0 /8")]
    [TestCase("10.0.0.5, proxy.example.com")]
    [TestCase("10.0.0.5;10.0.0.6")]
    [TestCase("10.0.0.5 10.0.0.6")]
    [TestCase("172.17")]
    [TestCase("1")]
    [TestCase("0x7f.1")]
    [TestCase("10.1/16")]
    public void Parse_ShouldThrow_WhenAnEntryIsNotAnAddressOrANetwork(string trustedProxies)
    {
        // Act
        var act = () => TrustedProxies.Parse(trustedProxies);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"*{TrustedProxies.ConfigurationKey}*");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase(" , ")]
    public void Configure_ShouldTrustOnlyLoopback_WhenNothingIsDeclared(string? trustedProxies)
    {
        // Act
        var options = new ForwardedHeadersOptions();
        TrustedProxies.Parse(trustedProxies).Configure(options);

        // Assert
        options.KnownProxies.Should().Equal(IPAddress.IPv6Loopback);
        options.KnownIPNetworks.Should().ContainSingle()
            .Which.Contains(IPAddress.Parse("127.0.0.1")).Should().BeTrue();
        options.KnownIPNetworks.Single().Contains(IPAddress.Parse(ExternalProxy)).Should().BeFalse();
    }

    [Test]
    public void Configure_ShouldNotAcceptThePathPrefixHeader()
    {
        // Act
        var options = new ForwardedHeadersOptions();
        TrustedProxies.Parse(null).Configure(options);

        // Assert
        options.ForwardedHeaders.Should().Be(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost);
    }

    [TestCase("10.0.0.5, 10.0.0.0/24", false)]
    [TestCase("0.0.0.0/0", true)]
    [TestCase("10.0.0.5, ::/0", true)]
    public void TrustsEveryAddress_ShouldBeTrue_OnlyWhenANetworkCoversEveryAddress(string trustedProxies, bool expected)
    {
        // Act
        var parsed = TrustedProxies.Parse(trustedProxies);

        // Assert
        parsed.TrustsEveryAddress.Should().Be(expected);
    }

    private async Task StartAsync(string? trustedProxies)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(_logs);
        builder.Services.Configure<ForwardedHeadersOptions>(TrustedProxies.Parse(trustedProxies).Configure);
        builder.Services.AddRateLimiter(AuthProtection.ConfigureRateLimiter);

        _app = builder.Build();
        _app.Use((httpContext, next) =>
        {
            // The test server has no connection: tests choose the peer address with a header
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(httpContext.Request.Headers[PeerHeader].ToString());
            return next(httpContext);
        });
        TrustedProxies.Use(_app);
        _app.UseRateLimiter();
        _app.MapGet(AddressPath, (HttpContext httpContext) => httpContext.Connection.RemoteIpAddress!.ToString());
        _app.MapGet(RequestPath, (HttpContext httpContext) =>
            $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}");
        _app.MapPost(LimitedPath, () => Results.Ok()).RequireRateLimiting(AuthProtection.RateLimitPolicy);

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    private async Task<string> GetAddressAsync(string peer, string forwardedFor)
    {
        using var response = await SendAsync(HttpMethod.Get, AddressPath, peer, forwardedFor);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> GetRequestAsync(string peer)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestPath);
        request.Headers.Add(PeerHeader, peer);
        request.Headers.Add(ForwardedForHeader, Client);
        request.Headers.Add("X-Forwarded-Host", "short.example");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Prefix", "/prefix");
        using var response = await _client!.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string peer, string forwardedFor)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add(PeerHeader, peer);
        request.Headers.Add(ForwardedForHeader, forwardedFor);
        return await _client!.SendAsync(request);
    }

    /// <summary>
    /// Collects the warnings written by <see cref="TrustedProxies"/>.
    /// </summary>
    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _warnings = [];

        public IReadOnlyList<string> Warnings
        {
            get
            {
                lock (_warnings)
                {
                    return _warnings.ToList();
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(RecordingLoggerProvider provider, string categoryName) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning && categoryName == typeof(TrustedProxies).FullName)
                {
                    lock (provider._warnings)
                    {
                        provider._warnings.Add(formatter(state, exception));
                    }
                }
            }
        }
    }
}
