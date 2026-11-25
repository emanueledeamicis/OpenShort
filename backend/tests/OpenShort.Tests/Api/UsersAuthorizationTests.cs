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
/// Runs the users controller on an in-memory server behind the two authentication schemes of the
/// application, as <see cref="SettingsAuthorizationTests"/> does for the settings: only the dashboard login
/// of an administrator manages the users.
/// </summary>
[TestFixture]
public class UsersAuthorizationTests
{
    private const string UsersPath = "/api/users";
    private const string RegisterPath = "/api/auth/register";
    private const string ValidApiKey = "os_valid-key-for-the-test";
    private const string SigningKey = "signing-key-used-only-by-this-test-0123456789";
    private const string Issuer = "test-issuer";
    private const string Audience = "test-audience";

    private Mock<IUserService> _userServiceMock = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    [SetUp]
    public async Task Setup()
    {
        _userServiceMock = new Mock<IUserService>();
        _userServiceMock
            .Setup(service => service.GetAllAsync())
            .ReturnsAsync(
            [
                new UserAccount(new AppUser { Id = "user-1", UserName = "admin" }, IsAdmin: true),
                new UserAccount(new AppUser { Id = "user-2", UserName = "plain@example.com" }, IsAdmin: false)
            ]);

        var apiKeyServiceMock = new Mock<IApiKeyService>();
        apiKeyServiceMock.Setup(service => service.ValidateKeyAsync(ValidApiKey)).ReturnsAsync(true);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(_userServiceMock.Object);
        builder.Services.AddSingleton(apiKeyServiceMock.Object);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<TwoFactorCodeThrottle>();
        builder.Services.AddControllers().AddApplicationPart(typeof(UsersController).Assembly);

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

    [TestCase("GET", UsersPath)]
    [TestCase("POST", UsersPath)]
    [TestCase("DELETE", UsersPath + "/user-2")]
    [TestCase("DELETE", UsersPath + "/user-2/two-factor")]
    public async Task Users_ShouldReturnUnauthorized_WithTheApiKeyOnly(string method, string path)
    {
        // Act
        using var response = await SendAsync(method, path, apiKey: ValidApiKey);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _userServiceMock.VerifyNoOtherCalls();
    }

    // A user without the Admin role is listed in the Users page, but does not manage the users
    [TestCase("GET", UsersPath)]
    [TestCase("POST", UsersPath)]
    [TestCase("DELETE", UsersPath + "/user-1")]
    [TestCase("DELETE", UsersPath + "/user-1/two-factor")]
    public async Task Users_ShouldReturnForbidden_WithALoginWithoutTheAdminRole(string method, string path)
    {
        // Act
        using var response = await SendAsync(method, path, token: CreateToken(role: null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _userServiceMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Users_ShouldListTheUsersWithoutTheAdminRoleToo_WithAnAdministratorLogin()
    {
        // Act
        using var response = await SendAsync("GET", UsersPath, token: CreateToken("Admin"));
        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        users.Should().HaveCount(2);
        users!.Single(user => user.Id == "user-2").IsAdmin.Should().BeFalse();
    }

    // The endpoint created users without the Admin role: it is gone, whoever asks
    [TestCase(null)]
    [TestCase("Admin")]
    public async Task Register_ShouldReturnNotFound(string? role)
    {
        // Act
        using var response = await SendAsync("POST", RegisterPath, token: role == null ? null : CreateToken(role));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private Task<HttpResponseMessage> SendAsync(string method, string path, string? token = null, string? apiKey = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { email = "new@example.com", password = "StrongPass123!" });
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
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-9"), new(ClaimTypes.Name, "user") };
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
}
