using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using OpenShort.Api.Auth;
using OpenShort.Api.Controllers;
using OpenShort.Api.Models;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Tests.Api;

/// <summary>
/// Runs the two-factor controller on an in-memory server, with Identity, the two authentication schemes
/// and the rate limiter registered as in Program.cs.
/// </summary>
[TestFixture]
public class TwoFactorAuthorizationTests
{
    private const string TwoFactorPath = "/api/security/two-factor";
    private const string Password = "StrongPass123!";
    private const string ValidApiKey = "os_valid-key-for-the-test";
    private const string SigningKey = "signing-key-used-only-by-this-test-0123456789";
    private const string Issuer = "test-issuer";
    private const string Audience = "test-audience";

    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private string _userId = null!;

    [SetUp]
    public async Task Setup()
    {
        var apiKeyServiceMock = new Mock<IApiKeyService>();
        apiKeyServiceMock.Setup(service => service.ValidateKeyAsync(ValidApiKey)).ReturnsAsync(true);

        var databaseName = Guid.NewGuid().ToString();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(apiKeyServiceMock.Object);
        builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddControllers().AddApplicationPart(typeof(TwoFactorController).Assembly);

        // Same schemes and default policy as Program.cs
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                    ValidateIssuer = true,
                    ValidIssuer = Issuer,
                    ValidateAudience = true,
                    ValidAudience = Audience,
                    ClockSkew = TimeSpan.Zero
                };
            })
            .AddScheme<ApiKeyAuthOptions, ApiKeyAuthHandler>(ApiKeyAuthOptions.DefaultScheme, _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(
                    JwtBearerDefaults.AuthenticationScheme,
                    ApiKeyAuthOptions.DefaultScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        // Same Identity registration as Program.cs
        builder.Services.AddIdentityCore<AppUser>(AuthProtection.ConfigureIdentity)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddApiEndpoints();
        builder.Services.AddRateLimiter(AuthProtection.ConfigureRateLimiter);

        _app = builder.Build();
        _app.UseRouting();
        _app.UseRateLimiter();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();

        await _app.StartAsync();
        _client = _app.GetTestClient();

        using var scope = _app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = "admin", CreatedAt = DateTime.UtcNow };
        (await userManager.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
        _userId = user.Id;
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [TestCase("GET", "")]
    [TestCase("POST", "/setup")]
    [TestCase("POST", "/enable")]
    [TestCase("POST", "/recovery-codes")]
    [TestCase("POST", "/disable")]
    public async Task Endpoint_ShouldReturnUnauthorized_WithTheApiKeyOnly(string method, string path)
    {
        // Act
        using var response = await SendAsync(method, path, new { password = Password, code = "123456" }, apiKey: ValidApiKey);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await IsTwoFactorTouchedAsync()).Should().BeFalse();
    }

    [TestCase("GET", "")]
    [TestCase("POST", "/setup")]
    [TestCase("POST", "/enable")]
    [TestCase("POST", "/recovery-codes")]
    [TestCase("POST", "/disable")]
    public async Task Endpoint_ShouldReturnUnauthorized_WithoutCredentials(string method, string path)
    {
        // Act
        using var response = await SendAsync(method, path, new { password = Password, code = "123456" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await IsTwoFactorTouchedAsync()).Should().BeFalse();
    }

    [Test]
    public async Task TwoFactor_ShouldBeTurnedOnAndOff_WithTheDashboardLogin()
    {
        // Arrange
        var token = CreateToken();

        // Act
        using var setupResponse = await SendAsync("POST", "/setup", new { password = Password }, token);
        var setup = await setupResponse.Content.ReadFromJsonAsync<TwoFactorSetupResponse>();
        using var enableResponse = await SendAsync("POST", "/enable", new { code = TotpGenerator.Generate(setup!.SharedKey) }, token);
        var enabled = await enableResponse.Content.ReadFromJsonAsync<TwoFactorRecoveryCodesResponse>();
        using var statusResponse = await SendAsync("GET", "", body: null, token);
        var status = await statusResponse.Content.ReadFromJsonAsync<TwoFactorStatusResponse>();
        using var disableResponse = await SendAsync("POST", "/disable", new { password = Password }, token);

        // Assert
        setupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        setupResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
        enableResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        enableResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
        enabled!.RecoveryCodes.Should().HaveCount(TwoFactorController.RecoveryCodeCount);
        status!.IsEnabled.Should().BeTrue();
        disableResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await IsTwoFactorTouchedAsync()).Should().BeFalse();
    }

    [TestCase("/setup", "{}")]
    [TestCase("/enable", "{}")]
    [TestCase("/enable", "{\"code\":\"12345678901234567\"}")]
    public async Task Endpoint_ShouldReturnBadRequest_WhenTheBodyIsNotValid(string path, string json)
    {
        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, TwoFactorPath + path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
        using var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await IsTwoFactorTouchedAsync()).Should().BeFalse();
    }

    [Test]
    public async Task Setup_ShouldReturnTooManyRequests_WhenTheRateLimitIsReached()
    {
        // Arrange
        var token = CreateToken();
        for (var attempt = 0; attempt < AuthProtection.RateLimitPermitsPerWindow; attempt++)
        {
            using var allowed = await SendAsync("POST", "/setup", new { password = "WrongPass123!" }, token);
            allowed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // Act
        using var response = await SendAsync("POST", "/setup", new { password = Password }, token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await IsTwoFactorTouchedAsync()).Should().BeFalse();
    }

    private Task<HttpResponseMessage> SendAsync(string method, string path, object? body, string? token = null, string? apiKey = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), TwoFactorPath + path);
        if (method == "POST" && body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (token != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (apiKey != null)
        {
            request.Headers.Add("X-Api-Key", apiKey);
        }

        return _client.SendAsync(request);
    }

    // True when the account has two-factor authentication on or a secret stored
    private async Task<bool> IsTwoFactorTouchedAsync()
    {
        using var scope = _app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByIdAsync(_userId);
        return await userManager.GetTwoFactorEnabledAsync(user!)
            || await userManager.GetAuthenticatorKeyAsync(user!) != null;
    }

    private string CreateToken()
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _userId), new Claim(ClaimTypes.Name, "admin")]),
            Expires = DateTime.UtcNow.AddMinutes(5),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256Signature)
        }));
    }
}
