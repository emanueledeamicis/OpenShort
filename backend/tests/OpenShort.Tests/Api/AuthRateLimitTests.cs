using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenShort.Api.Auth;

namespace OpenShort.Tests.Api;

/// <summary>
/// Runs the auth rate limit policy on an in-memory server with a minimal pipeline.
/// </summary>
[TestFixture]
public class AuthRateLimitTests
{
    private const string LimitedPath = "/limited";
    private const string UnlimitedPath = "/unlimited";

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    [SetUp]
    public async Task Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRateLimiter(AuthProtection.ConfigureRateLimiter);

        _app = builder.Build();
        _app.UseRateLimiter();
        _app.MapPost(LimitedPath, () => Results.Ok()).RequireRateLimiting(AuthProtection.RateLimitPolicy);
        _app.MapPost(UnlimitedPath, () => Results.Ok());

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Test]
    public async Task RateLimitPolicy_ShouldReturnTooManyRequests_WhenLimitIsExceeded()
    {
        // Act
        var statusCodes = new List<HttpStatusCode>();
        for (var request = 0; request < AuthProtection.RateLimitPermitsPerWindow; request++)
        {
            using var allowedResponse = await _client.PostAsync(LimitedPath, null);
            statusCodes.Add(allowedResponse.StatusCode);
        }

        using var rejectedResponse = await _client.PostAsync(LimitedPath, null);
        var rejectedBody = await rejectedResponse.Content.ReadAsStringAsync();

        // Assert
        statusCodes.Should().AllSatisfy(statusCode => statusCode.Should().Be(HttpStatusCode.OK));
        rejectedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejectedResponse.Headers.RetryAfter.Should().NotBeNull();
        rejectedResponse.Headers.RetryAfter!.Delta.Should().BePositive();
        rejectedBody.Should().Contain("Too many attempts");
    }

    [Test]
    public async Task RateLimitPolicy_ShouldNotLimitEndpoints_WithoutThePolicy()
    {
        // Act
        var statusCodes = new List<HttpStatusCode>();
        for (var request = 0; request < AuthProtection.RateLimitPermitsPerWindow + 5; request++)
        {
            using var response = await _client.PostAsync(UnlimitedPath, null);
            statusCodes.Add(response.StatusCode);
        }

        // Assert
        statusCodes.Should().AllSatisfy(statusCode => statusCode.Should().Be(HttpStatusCode.OK));
    }
}
