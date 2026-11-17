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
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using OpenShort.Api.Auth;
using OpenShort.Api.Controllers;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;

namespace OpenShort.Tests.Api;

/// <summary>
/// Runs the settings controller on an in-memory server behind the two authentication schemes of the
/// application: the dashboard login (JWT) and the API key, with the real API key handler.
/// The default policy accepts both schemes: a plain [Authorize] on the controller would let the API key in.
/// </summary>
[TestFixture]
public class SettingsAuthorizationTests
{
    private const string SettingsPath = "/api/settings";
    private const string ValidApiKey = "os_valid-key-for-the-test";
    private const string SigningKey = "signing-key-used-only-by-this-test-0123456789";
    private const string Issuer = "test-issuer";
    private const string Audience = "test-audience";

    private Mock<ISettingService> _settingServiceMock = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    [SetUp]
    public async Task Setup()
    {
        _settingServiceMock = new Mock<ISettingService>();
        _settingServiceMock
            .Setup(service => service.GetAllSettingsAsync())
            .ReturnsAsync(new List<SystemSetting>
            {
                new() { Key = "JwtSecretKey", Value = SigningKey },
                new() { Key = "CacheDurationSeconds", Value = "600" }
            });
        _settingServiceMock.Setup(service => service.GetSettingAsync("JwtSecretKey")).ReturnsAsync(SigningKey);
        _settingServiceMock.Setup(service => service.GetSettingAsync("CacheDurationSeconds")).ReturnsAsync("600");

        var apiKeyServiceMock = new Mock<IApiKeyService>();
        apiKeyServiceMock.Setup(service => service.ValidateKeyAsync(ValidApiKey)).ReturnsAsync(true);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(_settingServiceMock.Object);
        builder.Services.AddSingleton(apiKeyServiceMock.Object);
        builder.Services.AddControllers().AddApplicationPart(typeof(SettingsController).Assembly);

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

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [TestCase("GET", SettingsPath)]
    [TestCase("GET", SettingsPath + "/JwtSecretKey")]
    [TestCase("GET", SettingsPath + "/CacheDurationSeconds")]
    [TestCase("POST", SettingsPath)]
    public async Task Settings_ShouldReturnUnauthorized_WithTheApiKeyOnly(string method, string path)
    {
        // Act
        using var response = await SendAsync(method, path, apiKey: ValidApiKey);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body.Should().NotContain(SigningKey);
        VerifySettingsWereNotTouched();
    }

    [TestCase("GET", SettingsPath)]
    [TestCase("POST", SettingsPath)]
    public async Task Settings_ShouldReturnUnauthorized_WithoutCredentials(string method, string path)
    {
        // Act
        using var response = await SendAsync(method, path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        VerifySettingsWereNotTouched();
    }

    [TestCase(null, Description = "Signed-in user without any role")]
    [TestCase("User")]
    public async Task Settings_ShouldReturnForbidden_WithALoginThatIsNotAdministrator(string? role)
    {
        // Act
        using var response = await SendAsync("GET", SettingsPath, token: CreateToken(role));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        VerifySettingsWereNotTouched();
    }

    [Test]
    public async Task Settings_ShouldReturnForbidden_WithANonAdministratorLoginPlusTheApiKey()
    {
        // Act: the Admin role carried by the API key identity must not be added to the login
        using var response = await SendAsync("GET", SettingsPath, token: CreateToken(role: null), apiKey: ValidApiKey);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        VerifySettingsWereNotTouched();
    }

    [Test]
    public async Task Settings_ShouldReturnOnlyDashboardSettings_WithAnAdministratorLogin()
    {
        // Act
        using var response = await SendAsync("GET", SettingsPath, token: CreateToken("Admin"));
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("CacheDurationSeconds");
        body.Should().NotContain("JwtSecretKey");
        body.Should().NotContain(SigningKey);
    }

    [Test]
    public async Task Settings_ShouldStoreTheCacheDuration_WithAnAdministratorLogin()
    {
        // Act
        using var response = await SendAsync("POST", SettingsPath, token: CreateToken("Admin"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _settingServiceMock.Verify(
            service => service.SetSettingAsync("CacheDurationSeconds", "120", It.IsAny<string>()),
            Times.Once);
    }

    private Task<HttpResponseMessage> SendAsync(string method, string path, string? token = null, string? apiKey = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { key = "CacheDurationSeconds", value = "120" });
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

    private static string CreateToken(string? role)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-1"), new(ClaimTypes.Name, "user") };
        if (role != null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(5),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256Signature)
        }));
    }

    private void VerifySettingsWereNotTouched()
    {
        _settingServiceMock.VerifyNoOtherCalls();
    }
}
